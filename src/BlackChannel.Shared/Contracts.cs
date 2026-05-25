namespace BlackChannel.Shared;

// =============================================================================
// Wire contracts shared by the Blazor client and the Functions backend.
//
// CARDINAL RULE: nothing here ever carries plaintext or a private key. The
// server only ever handles public keys, opaque ciphertext envelopes, and
// routing metadata. Keep it that way.
// =============================================================================

/// <summary>
/// A device's published <b>public</b> key material. The private counterpart never
/// leaves the browser. The baseline (ECIES) only fills <see cref="IdentityPublicKey"/>;
/// the pre-key fields exist so an X3DH / Double Ratchet upgrade is contract-compatible.
/// </summary>
public sealed class KeyBundle
{
    /// <summary>Owning user id (from the identity provider, or dev-user locally).</summary>
    public string UserId { get; set; } = "";

    /// <summary>Stable device id — a user may publish a bundle per device (multi-device).</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>Base64 raw P-256 public key used for ECDH (the long-term identity key).</summary>
    public string IdentityPublicKey { get; set; } = "";

    // ---- Reserved for the Double Ratchet / X3DH upgrade (empty in the baseline) ----

    /// <summary>Base64 signed pre-key (X3DH). Empty until the ratchet upgrade.</summary>
    public string? SignedPreKey { get; set; }

    /// <summary>Base64 signature over the signed pre-key. Empty until the ratchet upgrade.</summary>
    public string? SignedPreKeySignature { get; set; }

    /// <summary>Base64 one-time pre-keys (X3DH). Empty until the ratchet upgrade.</summary>
    public List<string> OneTimePreKeys { get; set; } = new();

    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One end-to-end encrypted message as it travels and rests. Opaque to the server.
/// </summary>
public sealed class SealedEnvelope
{
    /// <summary>Server-assigned envelope id (set on store).</summary>
    public string Id { get; set; } = "";

    /// <summary>Sender user id (routing metadata only).</summary>
    public string From { get; set; } = "";

    /// <summary>Recipient user id (routing metadata only).</summary>
    public string To { get; set; } = "";

    /// <summary>Base64 ephemeral P-256 public key for this message (ECIES per-message key).</summary>
    public string EphemeralPublicKey { get; set; } = "";

    /// <summary>Base64 96-bit AES-GCM IV.</summary>
    public string Iv { get; set; } = "";

    /// <summary>Base64 AES-256-GCM ciphertext (includes the GCM auth tag).</summary>
    public string Ciphertext { get; set; } = "";

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Request to create a shareable, single-use invite link.</summary>
public sealed class CreateInviteResponse
{
    /// <summary>High-entropy single-use code embedded in the join URL.</summary>
    public string Code { get; set; } = "";

    /// <summary>Full join URL the inviter shares with their mate.</summary>
    public string JoinUrl { get; set; } = "";

    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>Result of redeeming an invite — links the redeemer to the inviter.</summary>
public sealed class RedeemInviteResponse
{
    public bool Success { get; set; }

    /// <summary>The inviter's user id, so the redeemer can fetch their key and message them.</summary>
    public string? InviterUserId { get; set; }

    public string? Error { get; set; }
}

/// <summary>Standard error body for non-2xx responses.</summary>
public sealed class ApiError
{
    public string Message { get; set; } = "";
    public ApiError() { }
    public ApiError(string message) => Message = message;
}
