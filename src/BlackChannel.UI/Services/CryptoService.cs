using Microsoft.JSInterop;
using BlackChannel.Shared;

namespace BlackChannel.Web.Services;

/// <summary>
/// The client-side encryption boundary. Everything the server must never see goes
/// through here. Implementations live entirely in the browser.
///
/// UPGRADE SEAM: today this is ECIES (per-message ephemeral ECDH). To move to a full
/// Double Ratchet, write a new IMessageCrypto and register it in Program.cs — no UI,
/// API, or server change required.
/// </summary>
public interface IMessageCrypto
{
    /// <summary>Ensure this device has an identity key pair; return its base64 public key.</summary>
    Task<string> EnsureIdentityAsync();

    /// <summary>Encrypt plaintext to a recipient's public key. Server-blind.</summary>
    Task<SealedEnvelope> SealAsync(string recipientUserId, string recipientPublicKey, string plaintext);

    /// <summary>Decrypt an envelope addressed to this device. Returns null if it can't be opened.</summary>
    Task<string?> OpenAsync(SealedEnvelope envelope);
}

/// <summary>
/// Thin wrapper over wwwroot/js/crypto.js, which does ECDH P-256 + HKDF-SHA256 +
/// AES-256-GCM using the browser's native Web Crypto API. The private key is created
/// non-extractable and stored in IndexedDB, so even this app's own JS can't read it.
/// </summary>
public sealed class CryptoService : IMessageCrypto
{
    private readonly IJSRuntime _js;
    public CryptoService(IJSRuntime js) => _js = js;

    public Task<string> EnsureIdentityAsync()
        => _js.InvokeAsync<string>("blackchannelCrypto.ensureIdentity").AsTask();

    public async Task<SealedEnvelope> SealAsync(string recipientUserId, string recipientPublicKey, string plaintext)
    {
        var sealed_ = await _js.InvokeAsync<SealedPayload>(
            "blackchannelCrypto.seal", recipientPublicKey, plaintext);

        return new SealedEnvelope
        {
            To = recipientUserId,
            EphemeralPublicKey = sealed_.Eph,
            Iv = sealed_.Iv,
            Ciphertext = sealed_.Ct
        };
    }

    public async Task<string?> OpenAsync(SealedEnvelope envelope)
    {
        try
        {
            return await _js.InvokeAsync<string>(
                "blackchannelCrypto.open", envelope.EphemeralPublicKey, envelope.Iv, envelope.Ciphertext);
        }
        catch
        {
            return null; // not for this device, or tampered
        }
    }

    /// <summary>Shape returned by crypto.js seal().</summary>
    private sealed class SealedPayload
    {
        public string Eph { get; set; } = "";
        public string Iv { get; set; } = "";
        public string Ct { get; set; } = "";
    }
}
