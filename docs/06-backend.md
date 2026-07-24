# 06 — Backend

## Module overview

```
backend/src/
├── app.module.ts          ← Root module, wires everything
├── main.ts                ← Bootstrap (Helmet, CORS, pipes, port)
├── health.controller.ts   ← GET /health → { status: "ok" }
├── auth/
│   ├── auth.module.ts
│   ├── auth.controller.ts  ← /auth/* endpoints
│   ├── auth.service.ts     ← register, login, refresh, logout, oauth
│   ├── jwt.strategy.ts     ← Bearer token validation
│   ├── jwt-auth.guard.ts   ← @UseGuards(JwtAuthGuard)
│   └── dto/                ← RegisterDto, LoginDto, OauthDto
├── users/
│   ├── users.module.ts
│   ├── users.controller.ts ← GET/PATCH/DELETE /users/me
│   └── users.service.ts
├── linked-wallets/
│   ├── linked-wallets.controller.ts
│   └── linked-wallets.service.ts
├── linked-bank-accounts/
│   ├── linked-bank-accounts.controller.ts
│   └── linked-bank-accounts.service.ts
├── p2p/
│   ├── p2p.controller.ts
│   ├── p2p.service.ts
│   ├── p2p-state.machine.ts  ← State transitions (tested)
│   └── p2p.gateway.ts        ← WebSocket gateway for real-time order updates
├── rates/
│   ├── rates.controller.ts
│   └── rates.service.ts      ← CoinGecko proxy + Redis cache
├── kyc/
│   ├── kyc.controller.ts
│   └── kyc.service.ts
├── cards/
│   ├── cards.controller.ts
│   └── cards.service.ts
├── webhooks/
│   ├── webhooks.controller.ts
│   └── webhooks.service.ts
├── telegram/
│   ├── telegram.module.ts
│   ├── telegram-bot.service.ts
│   ├── telegram-notify.service.ts
│   ├── telegram-auth.controller.ts
│   └── telegram-webhook.controller.ts
├── redis/
│   └── redis.service.ts
├── prisma/
│   └── prisma.service.ts
└── common/
    ├── guards/
    ├── decorators/
    ├── logger/
    ├── env.validation.ts
    └── privacy-mode.middleware.ts
```

---

## Database schema (Prisma)

### `users`
| Field | Type | Notes |
|-------|------|-------|
| id | UUID PK | Auto-generated |
| email | String unique | `@umbra.local` suffix for anonymous users |
| emailVerified | Boolean | false until link clicked |
| username | String unique nullable | Display name (3–32 chars) |
| passwordHash | String nullable | Argon2id hash |
| name | String nullable | Display name |
| telegramId | BigInt unique nullable | Telegram user ID |
| telegramUsername | String nullable | |
| telegramNotifications | Boolean | Default true |
| lang | String | "uk" / "en" / "ru" |
| tfaEnabled | Boolean | TOTP 2FA flag |
| oauthProvider | String nullable | "google" / "apple" |
| oauthSub | String nullable | Provider UID |
| pushEnabled | Boolean | Push notifications |
| emailAlerts | Boolean | Email for deals |
| priceAlerts | Boolean | Price alert notifications |
| deletedAt | DateTime nullable | Soft delete (GDPR) |

### `refresh_tokens`
| Field | Type | Notes |
|-------|------|-------|
| id | UUID PK | |
| userId | UUID FK | Cascades on user delete |
| jti | String unique | JWT ID — unique per token |
| tokenHash | String | Argon2id hash of the refresh token value |
| expiresAt | DateTime | 30-day TTL |
| revokedAt | DateTime nullable | Set on logout |

### `linked_wallets`
| Field | Type | Notes |
|-------|------|-------|
| id | UUID PK | |
| userId | UUID FK | |
| chain | String | "ethereum" / "bitcoin" / "solana" / "tron" |
| address | String | Public address ONLY |
| label | String nullable | User label |
| linkedAt | DateTime | |
Unique: `(userId, chain, address)`

### `linked_bank_accounts`
| Field | Type | Notes |
|-------|------|-------|
| id | UUID PK | |
| userId | UUID FK | |
| provider | String | "monobank" / "privatbank" |
| providerAccountId | String | Opaque provider ID |
| bankName | String nullable | Display name |
| maskedNumber | String nullable | "•••• 4242" |
| maskedIban | String nullable | "UA26 0005 2990..." |
| encryptedProviderToken | String nullable | AES-256-GCM encrypted token |
| status | String | "active" / "revoked" |

### `p2p_offers`
| Field | Type | Notes |
|-------|------|-------|
| id | UUID PK | |
| merchantId | UUID FK | |
| asset | String | "BTC" / "ETH" / "USDT" / "SOL" |
| fiatCurrency | String | "UAH" / "USD" / "EUR" or crypto ticker if quoteKind=crypto |
| quoteKind | String | "fiat" / "crypto" |
| price | Decimal(20,8) | Exchange rate |
| minAmount | Decimal(20,8) nullable | Min deal size |
| maxAmount | Decimal(20,8) nullable | Max deal size |
| reservedAmount | Decimal(20,8) | Locked in active orders |
| paymentMethods | String[] | ["Monobank", "PrivatBank"] |
| side | String | "buy" (merchant buys) / "sell" (merchant sells) |
| status | String | "active" / "paused" / "in_deal" / "deleted" |

### `p2p_orders`
| Field | Type | Notes |
|-------|------|-------|
| id | UUID PK | |
| offerId | UUID FK | |
| buyerId | UUID FK | |
| sellerId | UUID FK | |
| amount | Decimal(20,8) | Deal size in fiatCurrency |
| paymentMethod | String nullable | Which method used |
| status | String | State machine (see below) |
| cryptoTxHash | String nullable | On-chain proof |
| fiatPaymentReference | String nullable | Bank transfer ref |
| disputeReason | String nullable | |

**P2P status machine:**
```
created
  → awaiting_fiat_payment  (buyer POSTs fiat-proof)
  → fiat_payment_confirmed (seller confirms fiat received)
  → crypto_sent            (seller POSTs crypto tx hash)
  → completed              (buyer confirms receipt)
  → disputed               (any party, from any active status)
  → cancelled              (from created or awaiting_fiat_payment only)
```

### `kyc_records`
| Field | Type | Notes |
|-------|------|-------|
| id | UUID PK | |
| userId | UUID unique FK | One record per user |
| status | String | "none" / "pending" / "approved" / "rejected" |
| provider | String nullable | "sumsub" / "veriff" |
| providerReference | String nullable | Provider's verification ID |
| level | String | "basic" / "advanced" |

---

## Auth endpoints

```
POST /auth/register
  Body: { username, password }
  → hash password (Argon2id)
  → create user (email = username@umbra.local for anonymous)
  → issue access + refresh tokens

POST /auth/login
  Body: { username, password }
  → verify Argon2id hash
  → rate-limit: 5 attempts / 15min (Redis: rate:login:{ip}:{username})
  → issue access + refresh tokens

POST /auth/oauth/google
  Body: { idToken }
  → verify JWT against https://www.googleapis.com/oauth2/v3/certs
  → check aud === GOOGLE_CLIENT_ID, exp not past
  → find or create user by email + oauthSub
  → issue tokens

POST /auth/oauth/apple
  Body: { idToken, code? }
  → verify JWT against https://appleid.apple.com/auth/keys
  → check aud === APPLE_CLIENT_ID
  → Apple returns email only on first login — save immediately
  → find or create user
  → issue tokens

POST /auth/telegram
  Body: { initData }
  → HMAC-SHA256 verify with TELEGRAM_BOT_TOKEN
  → check auth_date TTL (max 1 hour)
  → find or create user by telegramId
  → issue tokens

POST /auth/refresh
  Cookie: refresh_token (httpOnly)
  → look up tokenHash in refresh_tokens table
  → verify not revoked, not expired
  → issue new access + refresh (rotation)
  → invalidate old refresh token (revokedAt = now)

POST /auth/logout
  → revoke refresh token in DB
  → clear cookie
```

**Token format:**
- Access token: JWT, 15 min TTL, secret = `JWT_ACCESS_SECRET`
- Refresh token: random 32-byte hex, hashed before storage, 30-day TTL, httpOnly Secure SameSite=Strict cookie

---

## Rate limiting

```
// @nestjs/throttler config (in app.module.ts)
ThrottlerModule.forRoot([{
  ttl: 60_000,       // 1 minute window
  limit: 60,         // 60 requests per minute (global)
}])

// auth specific (via @Throttle decorator):
// POST /auth/login   → 5 per 15min per IP
// POST /auth/register → 3 per hour per IP
// POST /auth/refresh  → 10 per 5min per IP
```

---

## WebSocket gateway (P2P real-time)

```typescript
// p2p.gateway.ts — @WebSocketGateway
// Events emitted to connected clients:

server.to(`user:${userId}`).emit('order:updated', {
  orderId: string,
  status: string,
  updatedAt: string,
})

server.to(`user:${userId}`).emit('offer:updated', {
  offerId: string,
  status: string,
})
```

Client subscribes on JWT authentication (socket auth middleware validates Bearer token).

---

## Security headers (Helmet config)

```typescript
helmet({
  contentSecurityPolicy: {
    directives: {
      defaultSrc: ["'self'"],
      scriptSrc: ["'self'"],           // NO unsafe-inline
      styleSrc: ["'self'", "'unsafe-inline'"],
      imgSrc: ["'self'", "data:", "https:"],
      connectSrc: [
        "'self'", "wss:",
        "https://api.coingecko.com",
        "https://api.monobank.ua",
        "https://blockstream.info",
        "https://api.mainnet-beta.solana.com",
      ],
      frameAncestors: ["'none'"],      // anti-clickjacking
      objectSrc: ["'none'"],
      upgradeInsecureRequests: [],
    },
  },
  hsts: { maxAge: 31_536_000, includeSubDomains: true },
  frameguard: { action: "deny" },
  noSniff: true,
  referrerPolicy: { policy: "no-referrer" },
})
```

---

## Environment variables (backend/.env)

```env
# Database
DATABASE_URL=postgresql://umbra:password@localhost:5432/umbra

# Redis
REDIS_URL=redis://localhost:6379

# JWT
JWT_ACCESS_SECRET=change-me-min-32-chars
JWT_REFRESH_SECRET=change-me-min-32-chars-different

# Auth
GOOGLE_CLIENT_ID=
APPLE_CLIENT_ID=
APPLE_TEAM_ID=
APPLE_KEY_ID=
APPLE_PRIVATE_KEY=

# Telegram
TELEGRAM_BOT_TOKEN=
TELEGRAM_WEBHOOK_SECRET=
TELEGRAM_WEBAPP_URL=http://localhost:5173
TELEGRAM_WEBHOOK_URL=

# Bank tokens encryption
BANK_TOKEN_ENCRYPTION_KEY=hex-32-bytes

# KYC
SUMSUB_APP_TOKEN=
SUMSUB_SECRET_KEY=
VERIFF_API_KEY=

# Payments (optional)
STRIPE_SECRET_KEY=
STRIPE_WEBHOOK_SECRET=

# App
PORT=3001
NODE_ENV=development
CORS_ORIGIN=http://localhost:5173

# Rates (if CoinGecko Pro)
COINGECKO_API_KEY=
```

---

## Hardening checklist

- [x] Argon2id for passwords (not bcrypt)
- [x] Helmet with strict CSP
- [x] HSTS enabled
- [x] httpOnly refresh tokens (never in localStorage)
- [x] Refresh token rotation (old token invalidated on each refresh)
- [x] Rate-limit on all auth endpoints
- [x] Validation pipe (whitelist: true, forbidNonWhitelisted: true)
- [x] GDPR: DELETE /users/me → soft delete + anonymize email
- [x] No seed/privateKey/mnemonic in logs (scrubSecrets middleware)
- [x] No PAN stored (only provider tokens)
- [ ] TOTP 2FA (flag exists in DB, implementation pending)
- [ ] WebAuthn (planned)
- [ ] npm audit in CI (add step to ci.yml)
- [ ] Sumsub/Veriff webhook signature verification
- [ ] Input: max body size 1MB (express default is 100kb — verify)
