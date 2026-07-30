# Changelog

All notable releases of **Umbrella Wallet**.

Format follows [Keep a Changelog](https://keepachangelog.com/). Versioning follows [SemVer](https://semver.org/).

## [2.1.0] — 2026-07-30

### Desktop
- **Real TON sending** — wallet v4R2 transfers are built, ed25519-signed and broadcast on-device.
  The transaction construction is pinned byte-for-byte against the reference `@ton/ton` library
  (order-cell hash, signing-message hash and the signatures all match); the first send from a
  fresh wallet also deploys it in the same transaction. TON is now fully supported (receive +
  balance + send); Cardano stays receive-only.
- **Glassier, rounder UI** — buttons and tiles gain a top-edge sheen and larger radii; cards round
  further with a faint inset highlight.
- **Ambient rain** — a faint "fear" rain drifts over the window, gated by the motion toggle.

### Web
- **Bolder, more thematic home** — full-colour coin badges with a glow, a shared brand-violet accent
  (section bars, active states, hero glow), and a larger hero.
- **Glassy, rounder UI with ambient rain** to match the desktop.

## [2.0.0] — 2026-07-28

### Desktop
- **Real TON and Cardano (ADA) receive addresses** — wallet v4R2 (TON) and Icarus/CIP-1852 (ADA),
  each verified byte-for-byte against the reference libraries (tonweb, cardano-serialization-lib).
- **Live TON + ADA balances** (toncenter / Koios).
- **Developer fee baked in** (0.5%, obfuscated recipient) — the config UI was removed; the fee is
  still disclosed before confirm. Routed on-chain for BTC/LTC/XMR/SOL.
- **New black low-poly app icon** (replaces the purple one), **bolder primary buttons**.
- **Activity section** finished; **in-app update check** (manual, Tor-aware); **status-bar version**
  now read from the assembly.

### Web
- **Self-hosted fonts** — no Google origins at all (fully anonymous).
- Removed the `/admin` route; fee percentage is baked.

### Contracts
- `FeeSplitter.sol` — one-transaction ETH fee batcher (recipient baked in; awaiting deploy).

## [1.8.0] — 2026-07-28

### Desktop
- TON (wallet v4R2) and Cardano (ADA) receive addresses
- Activity section completed
- In-app update check (manual, Tor-aware)
- Real app version in status bar
- Windows portable build

### Web
- News section
- Self-hosted fonts (no Google CDN)
- Exchange rate improvements

### Contracts
- FeeSplitter utility for efficient ETH forwarding
## [1.7.0] — 2026-07-23

### Desktop
- Monero and Tor startup reliability
- Encrypted backup export
- Panel placement fixes
- Four additional colour themes

### Web & API
- Market rates aggregator (CoinGecko + Binance fallback)
- Tor / privacy mode with IP redaction on backend
- Security hardening (Helmet, rate limiting, log scrubbing)
- Monero view-only derivation, multi-chain balances via public RPCs

## [1.6.0] — 2026-07-22

### Desktop
- Exchange-style candle charts
- Vector tile icons for coin list
- QR receive scrim fix (no white flash)

## [1.5.1] — 2026-07-22

### Desktop
- Fix: the fear brand mark no longer carries a white background

## [1.5.0] — 2026-07-22

### Desktop
- Six colour themes
- Searchable language picker
- Real market charts
- Nine exchange connectors (read-only API keys)

## [1.4.0] — 2026-07-21

### Desktop
- Exchange account linking (Binance, Bybit, OKX, Kraken, KuCoin, Gate.io, MEXC, Bitget, Telegram CryptoBot)
- Improved buttons and dropdown controls

## [1.3.0] — 2026-07-21

### Desktop
- Linked wallets counter on portfolio
- QR code popup for receive
- Six interface languages

## [1.2.0] — 2026-07-20

### Desktop
- USDT (TRC-20) and TRX sending
- Wallet data stored off the system drive (user-chosen path)
- Brand logo restored in title bar

## [1.1.0] — 2026-07-20

### Desktop
- Monero as a full coin (receive, send, balance via local `monero-wallet-rpc`)
- Telegram sticker animations in onboarding

## [1.0.2] — 2026-07-19

### Desktop
- Fix clipping on small window sizes
- Settings panes layout
- Glass-style UI polish

## [1.0.1] — 2026-07-19

### Desktop
- the fear branding
- Network labels on receive addresses
- Chain pickers and layout fixes

## [1.0.0] — 2026-07-18

### Desktop (initial release)
- Native Avalonia wallet for Windows
- BIP39 24-word seed generation and import
- Argon2id + AES-256-GCM encrypted local vault
- BTC, ETH, SOL, TRX, LTC, DOGE receive addresses
- Built-in Tor routing
- Real ETH and BTC send
- Watch-only address tracking
- Live market prices from CoinGecko

### Web (companion)
- React + NestJS stack
- Non-custodial browser wallet shell
- P2P marketplace, exchange view, portfolio stats

---





