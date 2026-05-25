# 00 — What BlackChannel is, and who it's for

> **A note on intent.** BlackChannel is, first and foremost, an **engineering exercise in
> end-to-end encryption** and a **portfolio project** — a working demonstration of how to
> build a server-blind E2EE messaging system on Azure (Blazor WASM + Functions + SignalR),
> with the crypto done client-side and in the open. It's genuinely usable and you're
> welcome to self-host it, but it's shared as a reference implementation and learning
> resource, not a commercial product.

BlackChannel is a private messaging service. It exists for the same reason you close the
curtains at home or seal an envelope before posting a letter: **privacy is normal, and
ordinary conversations deserve to stay between the people having them.**

## Who it's for

Everyday people who'd simply rather their private conversations stay private:

- **Family and friends** sharing personal news, photos, addresses, plans.
- **Couples** who don't want their relationship sitting in a tech company's database.
- **People handling sensitive personal details** — health, finances, legal matters.
- **Small businesses and professionals** who owe their clients confidentiality (think
  a bookkeeper, a counsellor, a tradesperson quoting a job).
- **Journalists, researchers and their sources**, where confidentiality is part of the job.
- **Anyone** who believes that "I have nothing to hide" and "I'd still like some privacy"
  are both true at once.

This is the same kind of protection that already ships in apps hundreds of millions of
people use every day. BlackChannel just makes it something you can read, build, and run
yourself.

## What it does

- Encrypts every message **on your device**, before it's sent.
- Stores and delivers only **scrambled ciphertext** — the server can't read your messages.
- Lets you invite the people you want to talk to with a simple link.

## Acceptable use

BlackChannel is built for lawful, private communication. It is **not** intended to enable
or encourage illegal activity, harassment, or harm. By running or using it you're
responsible for complying with the laws that apply to you. Privacy and accountability
aren't opposites — strong encryption protects the innocent far more often than it shields
wrongdoing, which is exactly why mainstream messaging apps use it.

If you operate your own instance, you decide your own terms of use and remain responsible
for them.

## What privacy here does and doesn't mean

- ✅ We (the operator) can't read your message content, and can't be made to hand over
  what we don't have.
- ✅ The code is open: you can verify the claims, or run your own copy and trust no operator
  at all.
- ⚠️ It is **not** a shield for a device that's already been compromised by spyware, and it
  doesn't hide *that* you talk to someone (see [`02-e2ee-crypto.md`](02-e2ee-crypto.md)).

Read on: [`01-architecture.md`](01-architecture.md) for how it's built, and
[`02-e2ee-crypto.md`](02-e2ee-crypto.md) for exactly how the encryption works.
