# 11 · Glossary

Every term in these docs, one line each.

## Wallet & crypto

- **Non-custodial** — you hold the keys; the developer never can touch your funds (or recover them).
- **Seed / recovery phrase / mnemonic** — the 24 (or 12) words that *are* the wallet. Whoever has
  them has the money.
- **BIP39** — the standard that turns entropy into those words.
- **BIP44 / BIP84** — standards for the derivation *paths* that turn one seed into many coin addresses.
- **SLIP-0010** — the derivation standard for ed25519 chains (Solana, Monero-style).
- **Derivation path** — e.g. `m/84'/0'/0'/0/0`; a recipe for turning the seed into a specific address.
- **Vault** — the seed after it's been encrypted with your password.
- **Argon2id** — the slow, memory-hard function that turns your password into an encryption key
  (resists brute force).
- **AES-256-GCM** — the authenticated encryption that actually protects the seed.
- **CSPRNG** — cryptographically secure random generator; where the seed's entropy comes from.
- **Watch-only** — tracking a public address without holding its key.

## Coins & networks

- **UTXO** — Bitcoin's "unspent output" model; a transaction spends whole outputs and makes change.
- **SegWit / native SegWit (BIP84)** — modern Bitcoin address format (`bc1…`), cheaper fees.
- **Gas** — Ethereum's unit of computation; you pay `gas × gas price`.
- **EIP-155** — the rule that binds an Ethereum signature to a specific chain (replay protection).
- **TRC-20** — TRON's token standard; USDT-on-TRON is a TRC-20 token.
- **Energy / Bandwidth (TRON)** — TRON's resource model; a token transfer burns TRX for Energy if you
  haven't staked (this is why USDT transfers cost several dollars). See [07-financial.md](07-financial.md).
- **RingCT / Bulletproofs** — the cryptography that makes Monero amounts private.
- **View key / spend key (Monero)** — the view key can *see* incoming funds; the spend key can *move*
  them.
- **piconero** — Monero's smallest unit; 1 XMR = 10¹² piconero.

## Privacy & security

- **Tor** — onion-routing network that hides your IP; Umbrella bundles the Tor client.
- **SOCKS5 proxy** — the local port (9250) Tor exposes for other apps to route through.
- **HMAC** — a keyed hash proving a message came from someone who knows a shared secret (webhooks).
- **Nonce** — a one-time number; used so a signed wallet-link message can't be replayed.
- **JWT** — a signed token proving you're logged in (access = short-lived, refresh = renews it).
- **CSP / HSTS** — HTTP security headers (which scripts may run; force HTTPS).
- **Timing-safe compare** — comparing secrets in constant time so an attacker can't guess byte-by-byte.

## Architecture

- **Avalonia** — the cross-platform .NET UI framework the desktop app is built with.
- **MVVM** — Model-View-ViewModel; the desktop's UI pattern (`MainViewModel` holds the state).
- **NestJS** — the Node.js backend framework.
- **Prisma** — the type-safe database layer (no raw SQL).
- **Redis** — in-memory store for sessions, throttle counters, nonces, price cache.
- **TanStack Start / Query** — the web app's router and data-fetching layer.
- **IndexedDB** — the browser's on-device database, where the web vault lives.
- **Demo mode** — an in-memory fake backend so the app runs with no server.
- **Hydration** — React reconciling server-rendered HTML with the client; a "mismatch" is when they
  disagree.

## Product & money

- **the fear** — the maker/brand; owner **kiurakku**.
- **Aggregator (web)** — the P2P / exchange / linked-account half of the web app (talks to the
  backend with public data only).
- **P2P** — peer-to-peer trading; the backend brokers matching and proofs, not custody.
- **Network fee** — what the blockchain charges to include your transaction (goes to miners/validators,
  never to Umbrella).
- **Service fee** — a platform cut; **none exists today**; if added it must be disclosed (see
  [07-financial.md](07-financial.md)).
- **Read-only exchange link** — connecting an exchange with an API key that can *see* balances but
  never trade or withdraw.
