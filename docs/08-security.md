# 8 · Security model

The threat model and every defence, across all three products. This is also the pentest baseline —
`npm audit` reports **0 vulnerabilities** on frontend and backend, and the findings below were the
actual result of a security review.

## Threat model — what we protect against

| Threat | Defence |
|--------|---------|
| Server breach exposes user funds | Server never holds keys/seeds — a full DB dump exposes no private key |
| Malware screenshots the seed | Windows renders the window black to capture while a secret is shown |
| Network eavesdropper sees your IP | Bundled Tor (desktop); server sees only public data (web) |
| Brute-force login | argon2id hashing + throttled auth (5/15 min) |
| Replay a wallet-link signature | Per-user, one-time, TTL'd nonce deleted after use |
| Forged webhook | HMAC-SHA256 over raw body, constant-time compare |
| Weak/absent server secret | Boot fails in production if JWT secret is missing or < 32 chars |
| XSS / injection | No `dangerouslySetInnerHTML` of user data; Prisma (no raw SQL); helmet CSP |
| Dependency CVEs | `npm audit` = 0; transitive vulns pinned via `overrides` |
| Mistaken restore wipes vault | Restore moves the old vault aside as `.replaced-*`, never deletes |

## Cryptography (identical spec, two implementations)

Desktop (C#) and web (TypeScript) use the **same** primitives so a seed is portable between them:

| Purpose | Primitive | Parameters |
|---------|-----------|-----------|
| Seed | BIP39 | 256-bit entropy from OS CSPRNG → 24 words |
| Vault key | Argon2id | m = 64 MiB, t = 3–4, p = 1–2 |
| Vault encryption | AES-256-GCM | random salt + nonce, authenticated |
| Login password (server) | argon2id | server-side only, never the seed |
| Wallet-link proof | secp256k1 personal_sign | recovered signer must equal the claimed address |
| Webhook | HMAC-SHA256 | over raw body, timing-safe compare |

## Where secrets live and where they never go

- **Seed / mnemonic:** desktop `data/vault.json` (encrypted) or browser IndexedDB (encrypted). Never
  transmitted. Grep-verified: no code logs or POSTs a mnemonic.
- **Vault password:** entered by the user, used to derive the vault key in memory, never stored,
  never sent.
- **Exchange API keys (desktop):** read-only, encrypted at rest with a seed-derived key
  (`exchanges.bin`), sent only to the exchange they belong to. Cleared from memory on lock.
- **Login password (web):** argon2id-hashed on the server; the clear password is never stored.
- **JWT secrets, webhook secrets:** environment variables only; `.env` is gitignored and never
  committed. (A short-lived Vercel OIDC token that once sat in a local `.env` was verified gitignored
  and already expired — no leak.)

## Backend hardening checklist

- ✅ argon2id password hashing
- ✅ Access & refresh JWTs signed with **separate** secrets; refresh can't be used as access
- ✅ Production boot fails on missing/weak JWT secret
- ✅ Throttled auth, Redis-backed so it holds across instances
- ✅ helmet: Content-Security-Policy + HSTS (1 year, includeSubDomains)
- ✅ CORS allowlist (not `*`)
- ✅ HMAC-verified webhooks over raw body, timing-safe
- ✅ Per-user one-time nonce for wallet-link challenges (no replay)
- ✅ Prisma only — no raw SQL, no injection surface
- ✅ Soft-delete for offers/users — no destructive data loss

## Frontend hardening checklist

- ✅ Vault crypto matches desktop (argon2id 64 MiB + AES-GCM), IndexedDB, never leaves browser
- ✅ Mnemonic normalized NFKD before encryption
- ✅ No third-party requests unless the user acts (Telegram script injected lazily, only inside TG)
- ✅ Theme applied pre-paint; `<html>` hydration reconciled (no injected-content mismatch)
- ✅ 0 dependency vulnerabilities after `overrides`

## Dependency audit result

The one real class of finding in the review was **transitive dependency CVEs** (not app code):

| Package | Issue | Fix |
|---------|-------|-----|
| axios (via WalletConnect → cdp-sdk) | prototype pollution, DoS | pinned `axios` 1.18.1 in `overrides` |
| ws (via viem) | memory-exhaustion DoS | pinned `ws` |
| valibot (via telegram-apps) | ReDoS | pinned `valibot` |
| brace-expansion, shell-quote | DoS (dev tooling) | pinned |

Result: **0/0** on both projects. Re-run any time with `npm audit`.

## What is intentionally NOT hidden

Honesty is part of the security posture:

- The Monero derivation reaches an internal BouncyCastle method by reflection and **throws loudly**
  if it ever disappears, rather than silently producing wrong keys.
- TON/Cardano are **disabled**, not faked, because their address derivation isn't verified here.
- Fees are the network's, disclosed on the review screen — the wallet does not take a hidden cut (see
  [07-financial.md](07-financial.md)).
