# 01 — Architecture

BlackChannel is built from four moving parts and one hard rule.

## The hard rule

**The server is blind.** It moves and stores encrypted blobs. It never sees message
text and never holds a private key. Everything that could read a message happens in
the browser. If you remember nothing else, remember that — every design decision
below falls out of it.

## The four parts

### 1. The client — Blazor WebAssembly
Runs entirely in the browser. It:
- generates the user's key pair (private key never leaves the device),
- encrypts each message before sending,
- decrypts incoming messages,
- talks to the API over HTTPS and to SignalR over WebSockets.

Hosted as a **Static Web App** — static files on a CDN, free tier, nothing to run.

### 2. The API — Azure Functions (Flex Consumption)
A blind mailbox with a few jobs:
- `POST /api/keys` — store a user's **public** key bundle.
- `GET  /api/keys/{userId}` — hand someone else's public key to a sender.
- `POST /api/messages` — accept a ciphertext envelope, stash it, fan it out.
- `GET  /api/messages` — deliver a user's pending envelopes (catch-up / "sync").
- `POST /api/invites` / `POST /api/invites/{code}/redeem` — shareable join links.
- `POST /api/negotiate` — hand back a SignalR connection token.

**Flex Consumption** means it scales to zero when idle (you pay ~nothing) and scales
out under load. Perfect for bursty, personal messaging traffic.

### 3. Realtime — Azure SignalR (Serverless)
When Alice sends Bob an envelope, the Function pushes it to Bob's live connection via
SignalR so it arrives instantly. SignalR carries **ciphertext only** — it's a pipe,
not a participant. If Bob is offline, the envelope waits in storage until he syncs.

### 4. Storage — Azure Table + Blob
- **Table Storage:** public keys and invites.
- **Blob Storage:** the ciphertext envelopes themselves (and the Functions deploy package).

No table and no blob ever contains plaintext or a private key.

## Identity — Microsoft Entra External ID
Sign-in is handled by Entra External ID (CIAM — the customer-facing successor to
Azure AD B2C). It tells the server *who* is connected (so envelopes route to the
right mailbox) — it has nothing to do with message encryption. You're authenticated
to the mailbox; you're still the only one with the key to your mail.

## Data flow: Alice messages Bob

```
1. Alice's browser  ──GET /api/keys/bob──▶  Function  ──▶  returns Bob's PUBLIC key
2. Alice's browser  : encrypt(plaintext, Bob's public key)  →  envelope {eph, iv, ct}
3. Alice's browser  ──POST /api/messages──▶  Function  : store the ciphertext envelope
4. Function  ──SignalR push──▶  Bob's browser   (or it waits in storage if Bob is offline)
5. Bob's browser    : decrypt(envelope, Bob's PRIVATE key)  →  plaintext
```

Steps 2 and 5 — the only steps that touch plaintext — happen on the two devices.
Everything in between is blind.

Next: [`02-e2ee-crypto.md`](02-e2ee-crypto.md) for exactly how steps 2 and 5 work.
