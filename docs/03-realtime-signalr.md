# 03 — Invites and realtime delivery

## Inviting a mate

There are no usernames to look up and no directory to search — you bring your own
contacts in via **invite links**.

1. You hit **"Invite a mate"**. The client calls `POST /api/invites`.
2. The server mints a random, single-use code and stores it (with your user id and an
   expiry) in the `Invites` table. It returns a URL like:
   `https://blackchannel.example/join/Xy7Qk2...`
3. You send that link to your mate however you like — text, email, carrier pigeon.
4. They open it, sign in (Entra External ID), and the client calls
   `POST /api/invites/{code}/redeem`. The server links the two accounts and burns the
   code so it can't be reused.
5. Both clients now have each other's user id, fetch each other's **public** keys, and
   can start an encrypted conversation.

The invite carries **no key material** — it's just a routing handshake. Keys are always
fetched fresh from the key endpoint, per device.

## Realtime delivery with Azure SignalR

SignalR (in **Serverless** mode, driven by Functions bindings) gives instant delivery
without the backend holding open sockets itself.

### Connecting
On load, the client calls `POST /api/negotiate`. The Function returns a SignalR access
token scoped to that user. The client opens a WebSocket to the SignalR service directly.

### A message arriving
```
Alice POST /api/messages  ──▶  Function:
                                 • store ciphertext envelope in Blob
                                 • [SignalROutput] push to user "bob"
                                          │
                                          ▼
                              SignalR service ──▶ Bob's open WebSocket
                                          │
                                          ▼
                              Bob's client decrypts locally
```

SignalR only ever carries the **ciphertext envelope**. It is a transport, exactly like
the HTTPS POST — it is not a trusted party and cannot read anything.

### When Bob is offline
The push has nowhere to land, so the envelope simply stays in Blob storage. Next time
Bob connects, his client calls `GET /api/messages` to pull everything pending ("sync"),
then the server deletes the delivered envelopes. A storage lifecycle rule expires
anything never collected, so the mailbox never grows without bound.

## Presence (optional, future)
Showing "Bob is online" can be layered on with a SignalR presence group, the same way
delivery works. It's metadata, so think about whether you want to expose it — see the
metadata note at the end of [`02-e2ee-crypto.md`](02-e2ee-crypto.md).
