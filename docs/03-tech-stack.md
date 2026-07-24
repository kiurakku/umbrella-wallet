# 3 · Tech stack

Every technology used, grouped by product, with the reason it was chosen.

## Desktop app

| Layer | Technology | Why |
|-------|-----------|-----|
| Runtime | **.NET 8** (C#) | Cross-platform, strong crypto libraries, single-file self-contained publish |
| UI | **Avalonia 11.3** | Native cross-platform XAML UI (Windows + Linux from one codebase) |
| MVVM | **CommunityToolkit.Mvvm** | `[ObservableProperty]` / `[RelayCommand]` source generators |
| Bitcoin/LTC/DOGE | **NBitcoin** + **NBitcoin.Altcoins** | Battle-tested BIP32/39/44/84, transaction building & signing |
| Ethereum | **Nethereum.Signer** | EIP-155 transaction signing |
| Elliptic curves | **BouncyCastle.Cryptography 2.4** | secp256k1, ed25519 (incl. Monero's raw basepoint mult) |
| QR codes | **QRCoder** | Receive-address QR generation |
| Animated stickers | **Avalonia.Labs.Lottie** | Renders Telegram `.tgs` (gzipped Lottie JSON) |
| Password hashing | **Konscious.Security.Cryptography** (Argon2id) | Vault key derivation |
| Bundled Tor | **Tor Expert Bundle 0.4.9** | Anonymity, run as a child process |
| Bundled Monero | **monero-wallet-rpc 0.18.5** | Full XMR wallet engine (RingCT/Bulletproofs) |
| Installer | **Inno Setup** | Windows installer with folder choice |

**Project split:** `Core` (pure crypto, no I/O — fully unit-tested) → `Infrastructure` (network,
processes, file storage) → `App` (UI). Dependencies point downward only.

## Web frontend

| Layer | Technology | Why |
|-------|-----------|-----|
| Framework | **React 19** + **TanStack Start** | File-based routing, SSR, type-safe router |
| Build | **Vite 8** (Rolldown) | Fast dev server + build |
| Styling | **Tailwind CSS 4** + **shadcn/ui** | Utility CSS + accessible component primitives |
| Data fetching | **TanStack Query** | Caching, background refresh of balances/rates |
| Wallet crypto | **Web Crypto API** + **@noble/hashes** + **hash-wasm** (Argon2id) | Seed, KDF, AES-GCM in-browser |
| BIP39 / HD | **@scure/bip39**, **@scure/bip32** | Mnemonic + key derivation |
| EVM signatures | **viem** | WalletConnect / personal_sign verification |
| WalletConnect | **@walletconnect/ethereum-provider**, **@reown/appkit** | Link external EVM wallets |
| Telegram | **@telegram-apps/sdk-react** | Mini-app integration |
| Storage | **IndexedDB** | Encrypted seed vault, client-only |

## Backend

| Layer | Technology | Why |
|-------|-----------|-----|
| Framework | **NestJS 10** (Node.js + TypeScript) | Modular, dependency-injected, testable |
| Database | **PostgreSQL** via **Prisma** | Type-safe queries, migrations, no raw SQL |
| Cache / sessions | **Redis** (**ioredis**) | Refresh tokens, throttle counters, wallet-link nonces, price cache |
| Auth | **JWT** (access + refresh, separate secrets) + **Passport** | Stateless access, revocable refresh |
| Password hashing | **argon2** (argon2id) | Login password storage |
| Rate limiting | **@nestjs/throttler** (Redis-backed) | Brute-force protection |
| Security headers | **helmet** (CSP + HSTS) | Standard HTTP hardening |
| Signatures | **viem** (EVM recover), Node **crypto** (HMAC) | Wallet-link proofs, webhook verification |
| Telegram | **grammY** / bot API | Bot + mini-app auth |

## Shared / tooling

| Purpose | Technology |
|---------|-----------|
| Package manager | npm (frontend + backend); NuGet (desktop) |
| Lint | ESLint (web); analyzers (desktop) |
| Types | TypeScript strict (web + backend); nullable reference types (C#) |
| Containers | Docker + docker-compose (web + backend + Tor) |
| Deploy targets | Vercel (frontend), Render (backend), Inno Setup / tar.gz (desktop) |

## Version pins that matter

- **Node**: transitive dependency vulnerabilities are held down with `overrides` in the root
  `package.json` (axios ≥ 1.18.1, ws, valibot, brace-expansion, shell-quote). `npm audit` reports
  **0 vulnerabilities** on both frontend and backend.
- **Tor Expert Bundle**: 0.4.9.x — staged by `scripts/fetch-tor.ps1` (Windows) or
  `scripts/publish-linux.sh` (Linux); not committed to git.
- **monero-wallet-rpc**: v0.18.5.1 — staged by `scripts/fetch-monero.ps1` / `publish-linux.sh`.
