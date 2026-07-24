# 1 · Overview

## What Umbrella is

Umbrella Wallet is an **anonymous-first, non-custodial cryptocurrency wallet**. The defining rule —
the one every design decision bends to — is:

> **The seed phrase is generated on the user's device, encrypted on the user's device, and never
> sent anywhere.** The developers cannot see it, cannot recover it, and cannot move the user's funds.

Everything else follows from that. There is no sign-up because there is no account to identify. There
is no "forgot password" recovery because there is no server holding your secret. This is a wallet in
the original sense: *you* are the only custodian.

## The three products

The repository contains three separate but related products. They share branding, cryptographic
approach and philosophy, but they are **different codebases with different capabilities** — it is
important not to confuse them.

### 1. Desktop app — the flagship (`desktop/`)

A standalone wallet written in **C# / .NET 8** with the **Avalonia** UI framework, running on
Windows and Linux. This is the most capable product:

- Real, locally-signed sending for **8 coins** (BTC, ETH, LTC, DOGE, TRX, USDT-TRC20, SOL, XMR).
- **Monero's own wallet engine** (`monero-wallet-rpc`) bundled inside, so XMR is a full private
  coin, not a read-only address.
- **Tor** bundled inside, so all wallet traffic can be routed anonymously with one switch — no
  separate Tor install.
- Screenshot protection, 10 themes, 6 languages, encrypted backups, 9 read-only exchange links.

A browser fundamentally cannot spawn a Tor process or a Monero daemon, so these two capabilities are
**desktop-only** — the web product achieves privacy differently (see below).

### 2. Web frontend (`src/`)

A **React** application (TanStack Start + Vite) that is two things at once:

- A **browser wallet**: the seed is created with the Web Crypto API, encrypted with Argon2id +
  AES-GCM, and stored in the browser's **IndexedDB**. It never reaches the server. This mirrors the
  desktop vault's cryptography exactly.
- An **aggregator**: link external wallets (via WalletConnect signatures) and bank accounts (Open
  Banking), browse **P2P** offers, and check live rates. This half *does* talk to the backend, but
  only ever with public data.

### 3. Backend (`backend/`)

A **NestJS** (Node.js) API with **PostgreSQL** + **Redis**. Its job is deliberately narrow:

- Store **public** data only: linked-wallet *addresses* (never keys), P2P offers, KYC status flags,
  a price cache.
- Authenticate users (nickname + password, or Telegram) so their linked-account list is theirs.
- Run the **Telegram bot** and mini-app.

The backend is architected so that **a full database dump would not expose a single private key or
mnemonic** — because it never receives one.

## Core philosophy, in five principles

1. **Keys never leave the device.** Enforced by architecture, not policy. The server has no endpoint
   that accepts a seed.
2. **No Big-Tech identity, analytics or SaaS.** No Google/Apple sign-in, no tracking pixels, no
   third-party analytics. Balances come straight from public block explorers and nodes.
3. **Anonymity is a feature, not a setting you have to find.** Tor is built in; the app makes no
   third-party requests unless you act.
4. **Fail honestly.** If a coin's address derivation isn't verified against published test vectors
   (TON, Cardano), it is *disabled* rather than shipped — a plausible-looking wrong address loses
   funds forever.
5. **The user is responsible, and told so plainly.** Non-custodial means unrecoverable. The UI and
   the license both say this without softening it.

## Who this documentation is for

- **A new user** deciding whether to trust it → start with the [main README](../README.md), then
  [08-security.md](08-security.md).
- **A new engineer** joining the project → read this file, then [02-architecture.md](02-architecture.md),
  then the doc for whichever product you'll work on.
- **The owner (kiurakku) / future-you** → [07-financial.md](07-financial.md) is where the money
  logic and fee decisions live.
- **An AI assistant** picking up the project → everything needed to act correctly is in these docs;
  [10-extending.md](10-extending.md) shows the safe way to change things.
