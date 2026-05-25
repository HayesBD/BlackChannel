using Azure;
using Azure.Data.Tables;

namespace BlackChannel.Functions.Storage;

// Azure Table entities. None of these ever hold plaintext or a private key —
// public keys, routing ids and invites only.

/// <summary>A device's published public key bundle. PK = userId, RK = deviceId.</summary>
public sealed class KeyEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";   // userId
    public string RowKey { get; set; } = "";          // deviceId
    public string IdentityPublicKey { get; set; } = "";
    public string? SignedPreKey { get; set; }
    public string? SignedPreKeySignature { get; set; }
    public string? OneTimePreKeysJson { get; set; }   // JSON array, empty in baseline
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>A single-use invite link. PK = "invite", RK = code.</summary>
public sealed class InviteEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "invite";
    public string RowKey { get; set; } = "";          // high-entropy code
    public string InviterUserId { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Redeemed { get; set; }
    public string? RedeemedByUserId { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
