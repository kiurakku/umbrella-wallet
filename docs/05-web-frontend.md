# 5 · Web frontend (deep dive)

React 19 + TanStack Start + Vite, in `src/`. Two things in one app: a **browser wallet** and an
**aggregator** (P2P / exchange / linked accounts).

## Routes (pages)

File-based routing under `src/routes/`:

| Route | File | Purpose |
|-------|------|---------|
| `/` | `index.tsx` | Onboarding → auth → wallet home (portfolio) |
| `/exchange` | `exchange.tsx` | Swap between currencies (rate quote from backend) |
| `/p2p` | `p2p.tsx` | Browse and create P2P offers/orders |
| `/stats` | `stats.tsx` | Portfolio analytics |
| `/settings` | `settings.tsx` | Profile, theme, language, 2FA, wallet management |
| `/help` | `help.tsx` | In-app help |
| `/verify-email` | `verify-email.tsx` | Optional email verification |
| `/legal/*` | `legal/` | terms, privacy, agreement, rules |
| `__root.tsx` | — | Shell: `<html>`, theme init script, providers, AuthGate |

## The onboarding → wallet flow

Verified end-to-end in a browser. The sequence:

1. **Onboarding carousel** — anonymity, your-keys, P2P intro. "Get started" / "I already have an
   account".
2. **Register** — nickname (3–32) + password (min 8), or Continue with Telegram. No email/phone.
3. **Set up wallet** — Create new / Import phrase / Link existing.
4. **Encryption password** — encrypts the seed on-device (separate from the login password).
5. **24-word phrase** — real BIP39, shown with a "don't screenshot" warning, Copy / written-down.
6. **Confirm phrase** — re-enter a few random words (e.g. #5, #8, #17).
7. **Wallet home** — portfolio: total balance, per-asset price/holdings/value/24h, Withdraw /
   Deposit / Swap / Scan.

## The browser wallet (`src/lib/wallet/`)

This mirrors the desktop vault's cryptography, in TypeScript:

| File | Role |
|------|------|
| `seedManager.ts` | Generate / import BIP39 mnemonic, orchestrate the vault |
| `vault.ts` | **Argon2id (64 MiB, t=3) → AES-GCM**, stored in **IndexedDB** keyed `seed:<userId>` |
| `walletCore.ts` | HD derivation per chain (`@scure/bip32`) |
| `chainBalances.ts` | Read balances from public explorers |
| `monero.ts` | Monero key derivation (view-only in browser) |
| `tor.ts` | Guidance/detection (a browser can't run Tor itself — see limits below) |
| `walletConnect.ts` | Link external EVM wallets by signature |
| `coinjoin.ts` | CoinJoin-related helpers |

**The vault never leaves the browser.** `encryptSeed()` normalizes the mnemonic NFKD, derives a key
with Argon2id from the encryption password + random salt, AES-GCM encrypts it, and stores the blob in
IndexedDB. `decryptSeed()` reverses it. The server has no endpoint that accepts any of this.

## The aggregator (`src/lib/api/`)

The half that *does* talk to the backend — always with public data only:

| File | Role |
|------|------|
| `client.ts` | REST client; picks real API or **demo mode** (`VITE_DEMO_MODE=true`) |
| `demo.ts` | A full in-memory fake of the backend, so the UI runs with no server |
| others | typed wrappers for auth, linked wallets/banks, p2p, rates |

**Demo mode** is important for development and previews: with `VITE_DEMO_MODE=true` the entire app
runs standalone (seeded portfolio, fake P2P offers) without Postgres/Redis. That is how the flow
above is smoke-tested.

## State & data fetching

- **TanStack Query** caches and background-refreshes balances, rates and the linked-account list.
- **Session** is tracked via `useSession`; `AuthGate` in `__root.tsx` decides onboarding vs. wallet.
- **Theme** is applied by an inline script (`THEME_INIT_SCRIPT`) before first paint to avoid a flash;
  the SSR shell ships the default-dark state to match (a `suppressHydrationWarning` on `<html>`
  covers the theme mutation and the CSP nonce React strips from the client tree).

## Telegram mini-app

`@telegram-apps/sdk-react`. When opened inside Telegram, `TelegramInit` lazily injects
`telegram-web-app.js` (ordinary and Tor visitors make **no** third-party request). Telegram login is
verified server-side (see [06-backend.md](06-backend.md) → telegram). It's optional and used for deal
notifications, never for identity.

## What the browser can and cannot do (honest limits)

The web wallet reaches **feature parity with the desktop for everything a browser permits**:

✅ Seed generation, encryption, storage, import — identical crypto to desktop.
✅ Reading balances and prices from public sources.
✅ Linking external wallets by signature; P2P; exchange rate quotes.

❌ **Bundled Tor process** — a web page cannot spawn `tor.exe`. Web privacy comes from the server only
   ever seeing public data and the phrase staying in the browser; true onion routing is desktop-only
   (or the user runs the web app *through* Tor Browser).
❌ **Monero full node** — a browser cannot run `monero-wallet-rpc`. XMR in the browser is view-level;
   full private sending is desktop-only.

These are platform constraints, not missing work — and the docs and UI say so plainly rather than
pretending otherwise.
