# Umbra Wallet — Complete Documentation

> **Goal:** A self-contained documentation set that a developer, AI assistant, or auditor can read to fully understand, build, deploy, and extend Umbra Wallet — from scratch, with no external searches needed.

---

## Document index

| # | File | What's inside |
|---|------|---------------|
| **01** | [Overview](./01-overview.md) | What Umbra is, philosophy, product scope, supported chains, non-goals |
| **02** | [Architecture](./02-architecture.md) | System diagrams, data flows, component map, golden rule (keys never leave device) |
| **03** | [Tech Stack](./03-tech-stack.md) | Every library explained: frontend (React, TanStack), backend (NestJS, Prisma), crypto libs, why each |
| **04** | [Desktop App](./04-desktop.md) | The shipped .NET 8 + Avalonia desktop wallet: project structure, vault, real multi-chain send, bundled Tor & Monero, developer fee, packaging |
| **05** | [Web Routes](./05-web-routes.md) | Route map, onboarding flow, IndexedDB structure, demo mode, aggregator model, privacy mode, honest limits |
| **06** | [Backend](./06-backend.md) | Module overview, DB schema, all API endpoints, auth flow, rate-limiting, WebSocket, Helmet config, hardening checklist |
| **07** | [Financial Model](./07-financial.md) | 💸 **Revenue model, fees, swap spread, TRC-20 costs, anonymity constraints, user disclosure requirements** |
| **08** | [Security](./08-security.md) | Threat model, audit status, legal classification (non-custodial aggregator), GDPR, incident response, bug bounty, compliance roadmap |
| **09** | [Build & Deploy](./09-build-deploy.md) | Local dev setup, env vars, Docker, Prisma commands, production build, Vercel + Fly.io deploy, Tor hidden service, monitoring |
| **10** | [Extending](./10-extending.md) | Recipes: add blockchain, add language, add theme, add Monero, add swap spread, add P2P commission, add exchange API, KYC enforcement |
| **11** | [Glossary](./11-glossary.md) | Every technical term defined in one sentence |

---

## Read in order (for new developers)

**If you're new to the project:**
1. [01 — Overview](./01-overview.md) → understand what Umbra is
2. [02 — Architecture](./02-architecture.md) → see how data flows (no keys to server)
3. [03 — Tech Stack](./03-tech-stack.md) → familiarize with libraries
4. [09 — Build & Deploy](./09-build-deploy.md) → get it running locally
5. [05 — Web Routes](./05-web-routes.md) → explore the UI flows
6. [06 — Backend](./06-backend.md) → understand API and database

**If you're auditing security:**
1. [08 — Security](./08-security.md) → threat model, legal model, GDPR
2. [02 — Architecture](./02-architecture.md) → verify keys never reach server
3. [06 — Backend](./06-backend.md) → check Helmet config, rate-limits, Argon2id usage
4. [07 — Financial Model](./07-financial.md) → fee transparency, legal compliance

**If you're adding features:**
1. [10 — Extending](./10-extending.md) → all recipes in one place
2. [03 — Tech Stack](./03-tech-stack.md) → understand existing libs
3. [11 — Glossary](./11-glossary.md) → quick reference for terms

---

## Design principles (from 01-overview.md)

1. **Non-custodial** — User owns seed, we never see it
2. **Aggregator** — Links external wallets/banks, doesn't create its own
3. **Privacy-first** — Username-only signup, Tor support, no analytics
4. **Self-sovereign** — Seed phrase is portable to any BIP39 wallet
5. **Transparent fees** — any fee (the optional developer fee on sends, off by default) is shown before confirmation; no hidden charges

---

## Key architectural facts

- **Seed encryption:** Argon2id KDF (64MB, 3 iter) → AES-256-GCM → IndexedDB (client-only)
- **Backend auth:** Argon2id password hash, JWT access (15min), refresh (30d, httpOnly cookie, rotated)
- **P2P model:** Proof-based (tx hashes), NO escrow on backend (users trade directly)
- **Fee model:** Optional developer fee on sends — off by default, capped at 2%, disclosed before confirm, routed on-chain for BTC/LTC/XMR (same transaction). A disclosed swap-spread also drives the web Exchange quote. See [07-financial.md](./07-financial.md)
- **Legal model:** Non-custodial aggregator, NOT a VASP/MSB (but consult lawyer before public launch)

---

## File paths quick reference

| What | Path |
|------|------|
| Seed vault (encrypt/decrypt) | `src/lib/wallet/vault.ts` |
| BIP39/44 derivation | `src/lib/wallet/walletCore.ts` |
| WalletConnect integration | `src/lib/wallet/walletConnect.ts` |
| Auth store (session) | `src/lib/authStore.ts` |
| API client | `src/lib/api/client.ts` |
| Backend main | `backend/src/main.ts` |
| Prisma schema | `backend/prisma/schema.prisma` |
| P2P state machine | `backend/src/p2p/p2p-state.machine.ts` |
| Helmet CSP config | `backend/src/main.ts` (line ~20) |
| Swap spread constant | `backend/src/rates/rates.service.ts` (add `PLATFORM_SPREAD_BPS`) |

---

## Common tasks

| Task | Command |
|------|---------|
| Start dev stack | `npm run dev:all` |
| Start infra only | `npm run docker:up` |
| Reset database | `cd backend && npx prisma migrate reset` |
| Add migration | `cd backend && npx prisma migrate dev --name <name>` |
| Build production | `npm run build && cd backend && npm run build` |
| Run backend tests | `cd backend && npm test` |

---

## Security audit checklist (from 08-security.md)

- [x] Argon2id for passwords
- [x] Seed never transmitted (stays in IndexedDB)
- [x] PAN never stored (only provider tokens)
- [x] CSP without unsafe-inline
- [x] HSTS enabled
- [x] Rate-limit on auth endpoints
- [x] Refresh token rotation
- [x] GDPR delete endpoint
- [ ] External security audit (when >1,000 MAU)
- [ ] npm audit in CI
- [ ] TOTP 2FA (flag exists, implementation pending)
- [ ] WebAuthn / Passkeys (roadmap)

---

## Revenue model summary (from 07-financial.md)

| Method | How | Legal complexity | User-visible |
|--------|-----|------------------|--------------|
| **Swap spread** (recommended) | 0.5% built into exchange rate | Low | Yes, shown before confirm |
| P2P matchmaking fee | 0.1-0.2% per completed deal | Medium | Yes, in order summary |
| On-chain fee address | 0.5% sent to fixed address | High (MSB risk) | Yes, extra gas |
| Subscription | $5/month Pro tier | Medium (payment processing) | Yes, recurring charge |

**Chosen model:** an optional, disclosed **developer fee on sends** — off by default, configured in the admin panel with no server, routed on-chain for BTC/LTC/XMR in the same transaction. The swap-spread remains available for the web Exchange quote. Both are always shown before the user confirms.

---

## Privacy mode (from 05-web-routes.md)

When enabled:
- No Telegram SDK
- No Google/Apple OAuth
- No third-party API calls from frontend (rates/balances cached or user-proxied)
- User should use Tor Browser

Auto-enabled on `.onion` domains.

---

## What Umbra will NEVER do

- Store private keys, seed phrases, or PAN on the server
- Custody user funds during P2P trades (no escrow on backend)
- Collect real identity without explicit KYC consent
- Operate as a Money Services Business or VASP (non-custodial aggregator only)
- Charge **hidden** fees — the developer fee on sends is off by default and always disclosed before the user confirms (see 07-financial.md)
- Force KYC to use wallet (only for P2P above limits)

---

## Contributing / extending

See [10 — Extending](./10-extending.md) for recipes:
- Add a new blockchain (Polygon example provided)
- Add Monero (different pattern)
- Add a new language (translation file)
- Add a new theme (CSS variables)
- Add swap spread revenue (one constant)
- Add P2P commission (Prisma field)
- Add exchange integration (Binance example)
- Add WebAuthn / Passkeys (biometric unlock)

---

## Support

- **Issues:** GitHub Issues (if open-source)
- **Security:** `security@umbra.example` (set this up)
- **Legal:** Consult fintech lawyer before public launch (see 08-security.md)
- **FAQ:** `/help` route in-app

---

## License

Proprietary / AGPL-3.0 (choose one) — see LICENSE file.

---

**This documentation is complete as of January 2026.** Update `docs/` when adding features.
