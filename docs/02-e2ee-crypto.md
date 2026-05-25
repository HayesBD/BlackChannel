# 02 — End-to-end encryption

This is the heart of BlackChannel. Read it once and the rest of the system makes sense.

## What "end-to-end" means here

Two devices share a secret that the server in the middle never learns. The server
stores and forwards ciphertext; only the two endpoints can turn it back into text.
That's it. No "encrypted in transit then decrypted on the server" — the server has no
key and never gets one.

## The baseline scheme: ECIES over WebCrypto

The current implementation uses **ECIES** (Elliptic Curve Integrated Encryption
Scheme), built entirely from primitives the browser already ships in the
[Web Crypto API](https://developer.mozilla.org/docs/Web/API/Web_Crypto_API) — no
external libraries, no WASM crypto blobs to trust.

### Keys
Each device generates one long-term **identity key pair** on the P-256 curve:
- The **private** key is created as *non-extractable* and stored in **IndexedDB**.
  The browser will use it for key agreement but will never hand the raw bytes back
  to JavaScript — so it can't be exfiltrated by app code.
- The **public** key is exported and uploaded to the server, where anyone who wants
  to message this device can fetch it.

### Sending (Alice → Bob)
1. Alice fetches Bob's **public** identity key from the server.
2. Alice generates a fresh **ephemeral** P-256 key pair — new for *every message*.
3. ECDH: `sharedSecret = ECDH(ephemeralPrivate, bobPublic)`.
4. HKDF-SHA256 stretches that into a 256-bit AES key.
5. AES-256-GCM encrypts the message with a random 96-bit IV.
6. Alice sends the **envelope**: `{ ephemeralPublic, iv, ciphertext }`. She throws the
   ephemeral private key away.

### Receiving (Bob)
1. Bob takes the envelope's `ephemeralPublic`.
2. ECDH: `sharedSecret = ECDH(bobPrivate, ephemeralPublic)` — same secret as Alice's,
   by the magic of Diffie-Hellman.
3. HKDF → the same AES key.
4. AES-256-GCM decrypts. GCM's auth tag also proves the ciphertext wasn't tampered with.

### Why ephemeral keys?
Because each message uses a brand-new ephemeral key pair, compromising one message's
key tells an attacker nothing about any other message. That's **per-message forward
secrecy**: steal today's key, you still can't read yesterday's mail.

## What the server sees

Exactly this, per message:

```json
{
  "to": "bob",
  "from": "alice",
  "ephemeralPublic": "BHk...base64...",
  "iv": "9f...base64...",
  "ciphertext": "QmxhY2s...base64..."
}
```

No plaintext. No private key. The `from`/`to` are routing metadata (the server has to
know where to deliver) — the *content* is opaque.

## What this baseline is NOT (yet)

This is honest ECIES, not the full Signal protocol. Compared to Signal's **Double
Ratchet + X3DH**, the baseline does **not** yet have:
- a ratcheting session that gives **future** secrecy (self-healing after a key compromise),
- offline **pre-keys** so the very first message has the same guarantees,
- deniability properties from the triple-DH handshake.

It *does* give you: server-blind E2EE, authenticated encryption, and per-message
forward secrecy. That's a strong, real baseline — and a much smaller thing to get
right than a hand-rolled ratchet.

## The upgrade seam

The code is structured so the ratchet can be dropped in without touching the API:
- **Client:** `Services/CryptoService.cs` implements `IMessageCrypto` (`SealAsync` /
  `OpenAsync`). Swap the implementation for a Double Ratchet one and nothing else in
  the UI changes.
- **Contract:** `BlackChannel.Shared/KeyBundle.cs` already has room for a signed
  pre-key and one-time pre-keys (`SignedPreKey`, `OneTimePreKeys`). The baseline
  leaves them empty; X3DH would populate them.
- **Server:** stays blind either way. Envelopes are opaque blobs; their internal
  structure is the client's business.

So: ship the baseline, then evolve the crypto behind a stable interface.

## Is the encryption "military grade"? Can a government break it?

The honest answer matters, so here it is straight.

**The encryption itself is as strong as it gets.** AES-256-GCM and elliptic-curve
Diffie-Hellman are the same class of algorithms used to protect classified government
communications. There is no known practical way for anyone — including any
intelligence agency — to break properly-used AES-256 or ECDH by attacking the
ciphertext. Making the cipher "stronger" wouldn't meaningfully help, because this isn't
where attacks succeed.

**Sophisticated attackers don't break the encryption — they break the device.** Tools
like Pegasus don't crack AES; they compromise the phone or computer itself and read the
messages *after* your device has legitimately decrypted them — straight off the screen.
No messaging app on Earth — not Signal, not iMessage, not BlackChannel — can protect
message content on a device that has been fully taken over. So we won't pretend
otherwise: **BlackChannel protects your messages in transit and at rest on our servers;
it cannot protect you from spyware running on your own phone.** Keep your devices
patched and uncompromised — that's the part only you can control.

What BlackChannel *does* guarantee: we hold no keys and store no plaintext, so we
**cannot** read your messages or hand them over to anyone, even if compelled. And because
the code is open, you don't have to take our word for it — read it, or run your own copy.

## Honest limitations

- **Lose your device key → lose your messages.** There's no server-side recovery,
  because there's no server-side key. That's the point.
- **Multi-device** needs each device to publish its own key and senders to encrypt to
  all of a recipient's devices (fan-out). The `KeyBundle` is per-device to allow this.
- **Metadata** (who talks to whom, when, how much) is visible to the server. E2EE
  protects content, not the social graph. Reducing metadata is a separate, harder problem.
