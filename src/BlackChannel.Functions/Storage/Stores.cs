using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using BlackChannel.Shared;

namespace BlackChannel.Functions.Storage;

// =============================================================================
// Thin data-access wrappers. Tables are created on first use so local Azurite
// and a fresh Azure deploy both "just work".
// =============================================================================

/// <summary>Public key bundles, keyed by user + device.</summary>
public sealed class KeyStore
{
    private readonly TableClient _table;
    public KeyStore(TableServiceClient svc)
    {
        _table = svc.GetTableClient("Users");
        _table.CreateIfNotExists();
    }

    public async Task UpsertAsync(KeyBundle bundle)
    {
        var e = new KeyEntity
        {
            PartitionKey = bundle.UserId,
            RowKey = string.IsNullOrWhiteSpace(bundle.DeviceId) ? "default" : bundle.DeviceId,
            IdentityPublicKey = bundle.IdentityPublicKey,
            SignedPreKey = bundle.SignedPreKey,
            SignedPreKeySignature = bundle.SignedPreKeySignature,
            OneTimePreKeysJson = JsonSerializer.Serialize(bundle.OneTimePreKeys)
        };
        await _table.UpsertEntityAsync(e, TableUpdateMode.Replace);
    }

    /// <summary>All published bundles for a user (one per device). Empty if none.</summary>
    public async Task<List<KeyBundle>> GetBundlesAsync(string userId)
    {
        var result = new List<KeyBundle>();
        var query = _table.QueryAsync<KeyEntity>(e => e.PartitionKey == userId);
        await foreach (var e in query)
        {
            result.Add(new KeyBundle
            {
                UserId = e.PartitionKey,
                DeviceId = e.RowKey,
                IdentityPublicKey = e.IdentityPublicKey,
                SignedPreKey = e.SignedPreKey,
                SignedPreKeySignature = e.SignedPreKeySignature,
                OneTimePreKeys = string.IsNullOrEmpty(e.OneTimePreKeysJson)
                    ? new()
                    : JsonSerializer.Deserialize<List<string>>(e.OneTimePreKeysJson) ?? new(),
                PublishedAt = e.Timestamp ?? DateTimeOffset.UtcNow
            });
        }
        return result;
    }
}

/// <summary>Single-use, expiring invite links.</summary>
public sealed class InviteStore
{
    private readonly TableClient _table;
    public InviteStore(TableServiceClient svc)
    {
        _table = svc.GetTableClient("Invites");
        _table.CreateIfNotExists();
    }

    public async Task<InviteEntity> CreateAsync(string inviterUserId, TimeSpan ttl)
    {
        // 128 bits of entropy, URL-safe.
        var bytes = RandomNumberGenerator.GetBytes(16);
        var code = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var e = new InviteEntity
        {
            RowKey = code,
            InviterUserId = inviterUserId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            Redeemed = false
        };
        await _table.AddEntityAsync(e);
        return e;
    }

    public async Task<InviteEntity?> GetAsync(string code)
    {
        try { return await _table.GetEntityAsync<InviteEntity>("invite", code); }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    /// <summary>Burns the code (single use). Returns the inviter id, or null if invalid/expired/used.</summary>
    public async Task<string?> RedeemAsync(string code, string redeemerUserId)
    {
        var e = await GetAsync(code);
        if (e is null || e.Redeemed || e.ExpiresAt < DateTimeOffset.UtcNow) return null;
        if (e.InviterUserId == redeemerUserId) return null; // can't invite yourself

        e.Redeemed = true;
        e.RedeemedByUserId = redeemerUserId;
        try
        {
            await _table.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            return null; // lost a race — someone redeemed it first
        }
        return e.InviterUserId;
    }
}

/// <summary>
/// The blind mailbox. Ciphertext envelopes stored as blobs under the recipient's prefix:
/// <c>{recipientUserId}/{envelopeId}.json</c>. Listing by prefix gives a user's pending mail.
/// </summary>
public sealed class EnvelopeStore
{
    private readonly BlobContainerClient _container;

    public EnvelopeStore(BlobServiceClient blobs)
    {
        var name = Environment.GetEnvironmentVariable("ENVELOPE_CONTAINER") ?? "envelopes";
        _container = blobs.GetBlobContainerClient(name);
        _container.CreateIfNotExists();
    }

    public async Task StoreAsync(SealedEnvelope envelope)
    {
        var blob = _container.GetBlobClient($"{envelope.To}/{envelope.Id}.json");
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope);
        using var ms = new MemoryStream(json);
        await blob.UploadAsync(ms, overwrite: true);
    }

    public async Task<List<SealedEnvelope>> ListPendingAsync(string userId)
    {
        var result = new List<SealedEnvelope>();
        await foreach (var item in _container.GetBlobsAsync(prefix: $"{userId}/"))
        {
            var blob = _container.GetBlobClient(item.Name);
            var download = await blob.DownloadContentAsync();
            var env = download.Value.Content.ToObjectFromJson<SealedEnvelope>();
            if (env is not null) result.Add(env);
        }
        return result.OrderBy(e => e.SentAt).ToList();
    }

    public async Task DeleteAsync(string userId, string envelopeId)
        => await _container.DeleteBlobIfExistsAsync($"{userId}/{envelopeId}.json");
}
