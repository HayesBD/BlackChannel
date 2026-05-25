# 05 — What we store and log (data minimisation)

BlackChannel is built to hold as little about you as possible. The honest goal: if anyone
ever lawfully asks the operator "what do you have on this user?", the complete answer is
short — and this page is that answer.

## What is stored (and for how long)

| Data | Why | Retention |
| --- | --- | --- |
| Your device's **public** key | so others can encrypt to you | until you replace it |
| **Undelivered ciphertext** messages | to deliver when you reconnect | **deleted the moment you receive them**; an auto-delete ceiling (default 14 days) removes anything never collected |
| **Invite codes** you create | to let a mate join you | single-use; auto-expires (default 7 days) |
| Routing metadata on a pending message (sender id, recipient id, timestamp) | the server has to know where to deliver | lives only as long as the undelivered message |

That's the lot. There is **no** message-content store, **no** private keys (they never
leave your device), **no** address book, **no** read receipts, **no** advertising or
analytics profile.

## What is logged

- **Application errors only**, at warning/error level — enough to keep the service
  running. No message content, no recipients, no "who-talked-to-whom" log.
- **No application telemetry by default.** Application Insights is not switched on; the
  app sends no usage analytics unless an operator deliberately configures it.
- **No connection or messaging logs** on the realtime service — that's turned off in the
  infrastructure, so there's no record that a message flowed.
- The cloud platform keeps its own short-lived operational logs (load balancer, etc.) that
  no application can fully eliminate — but the *application* adds nothing on top.

## What we therefore cannot produce

Because it's never collected, the operator can't hand over: your message contents, your
private keys, a history of who you messaged, your contact list, or your reading habits —
they don't exist on the server.

## If you run your own instance

You are the operator and you control all of the above. You can shorten the undelivered
retention (`undeliveredRetentionDays` in `infra/main.bicep`), and you decide your own
logging and lawful-access posture.

> This describes the software's design. It isn't a privacy policy or legal advice — if you
> operate an instance for others, publish your own policy.
