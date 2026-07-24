# 6 · Backend (deep dive)

NestJS 10 + PostgreSQL (Prisma) + Redis, in `backend/`. Its purpose is narrow and deliberate: store
**public** data, authenticate users, run the Telegram bot. **It never receives a private key or
mnemonic** — there is no endpoint that accepts one.

## Modules

`app.module.ts` wires these together (each is a NestJS module with a controller + service):

| Module | Responsibility |
|--------|----------------|
| `AuthModule` | Register / login / refresh / logout; JWT access + refresh; email verification |
| `UsersModule` | Profile, settings (language, 2FA, notifications) |
| `LinkedWalletsModule` | Link external wallet **addresses** by on-chain signature; list/unlink |
| `LinkedBankAccountsModule` | Link bank accounts (Open Banking); status only |
| `P2pModule` | P2P offers + orders + state machine |
| `RatesModule` | Price aggregation + currency conversion quotes |
| `KycModule` | Optional KYC status tracking |
| `WebhooksModule` | Signed inbound webhooks (KYC provider, Open Banking) |
| `TelegramModule` | Bot commands + mini-app auth verification |
| `DemoModule` | In-memory demo backend (mirrors the frontend demo) |
| `PrismaModule` / `RedisModule` | DB and cache access |

## Database schema (Prisma)

```
User                 id, email, username, passwordHash (argon2id), telegramId, lang,
                     tfaEnabled, notification prefs, deletedAt (soft delete), timestamps
RefreshToken         hashed refresh tokens per user (revocable)
EmailVerificationToken
LinkedWallet         userId, chain, address, label   ← ADDRESS only, never a key
LinkedBankAccount    userId, provider, masked account, status
P2pOffer             merchantId, side, asset, fiat, price, limits, status
P2pOrder             offerId, buyer, seller, amount, status (state machine)
KycRecord            userId, applicantId, reviewStatus
```

The single most important line: **`LinkedWallet` stores an `address`, never a private key.** Linking
proves control of an address by signature; the key stays with the user's real wallet.

## Auth (`AuthModule`)

Endpoints (all under `/auth`, the sensitive ones throttled):

| Endpoint | Throttle | Notes |
|----------|----------|-------|
| `POST /register` | 5 / 15 min | nickname + password, or Telegram |
| `POST /login` | 5 / 15 min | returns access + refresh |
| `POST /refresh` | 10 / 15 min | rotate tokens |
| `POST /email/request-verification` | 3 / 15 min | optional |
| `POST /email/verify` | — | |
| `POST /logout` | — | revoke refresh |

- Passwords hashed with **argon2id** (`common/crypto.util.ts`).
- **Access and refresh JWTs are signed with different secrets** (`getJwtAccessSecret` vs
  `getJwtRefreshSecret`). The access strategy validates with the access secret only, so a refresh
  token can never be used as an access token.
- In production, `getJwt*Secret` **throws** if the secret is missing or < 32 chars — the server won't
  boot with a weak/absent secret.

## P2P (`P2pModule`)

A merchant posts an **offer** (side, asset, fiat, price, limits). A taker opens an **order** against
it. The order moves through a **state machine** with terminal states, guarded so illegal transitions
are rejected (`test/p2p-transitions.test.ts` verifies this). Offers soft-delete (`status: deleted`)
rather than hard-delete so order history stays intact.

> The backend brokers *matching and proofs*, not custody — there is no escrow of funds on the
> server. Settlement is peer-to-peer.

## Rates (`RatesModule`)

Aggregates prices from providers, caches them in Redis, and answers conversion quotes:
`convert(from, to, amount) → { result, rate, fee }`. **Today `fee` is always `0`** — this is the
natural place a future exchange/swap service fee would be applied. See
[07-financial.md](07-financial.md).

## Webhooks (`WebhooksModule`)

Inbound webhooks (KYC provider, Open Banking) are **HMAC-SHA256 verified against the raw request
body** before any handling:

- `main.ts` sets `rawBody: true` so the exact bytes are available for the signature check.
- `verifyHmacSha256Hex` uses a length check + `crypto.timingSafeEqual` (constant-time, no timing
  leak).
- In production, an unconfigured webhook secret makes the endpoint reject rather than accept.

## Telegram (`TelegramModule`)

- **Bot** — commands, deal notifications, help. Branding text reads "the fear".
- **Mini-app auth** — the Telegram `initData` is verified server-side against the bot token (HMAC),
  so a spoofed Telegram user can't authenticate.

## Security hardening (applied globally in `main.ts`)

- **helmet** with a Content-Security-Policy and **HSTS** (1 year, includeSubDomains).
- **CORS** with an explicit allowlist (`CORS_ORIGIN`), not a wildcard.
- **Throttling** backed by Redis (`RedisThrottlerStorage`) so limits hold across instances.
- **No raw SQL** — everything goes through Prisma, so no SQL injection surface.
- Validated env at boot (`common/env.validation.ts`).

## Demo mode (`DemoModule`)

Mirrors the frontend's `demo.ts`: an in-memory implementation of every endpoint, so the whole stack
can be demonstrated or previewed with no database. Toggled by env; the frontend has its own toggle
(`VITE_DEMO_MODE`).
