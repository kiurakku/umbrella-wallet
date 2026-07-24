<div align="center">
<img src="assets/umbrella-256.png" width="80" alt="Umbrella"/>

# Umbrella Wallet — Full Documentation

Everything about how the project is built and how it works, in one place.
Written so a newcomer, a new engineer, an AI assistant, or future-you can pick it up
without hunting through the code.

</div>

---

## 📚 Read in this order

| # | Document | What it covers |
|---|----------|----------------|
| 1 | [Overview](01-overview.md) | What Umbrella is, the three products, the core philosophy |
| 2 | [Architecture](02-architecture.md) | How every piece connects — diagrams and data flow |
| 3 | [Tech stack](03-tech-stack.md) | Every language, framework and library, and why |
| 4 | [Desktop app](04-desktop.md) | The .NET/Avalonia wallet in depth: vault, coins, Tor, Monero, sending |
| 5 | [Web frontend](05-web-frontend.md) | The React app: routes, wallet-in-browser, state |
| 6 | [Backend](06-backend.md) | The NestJS server: modules, database, auth, P2P |
| 7 | [Financial part](07-financial.md) | **Fees, sending mechanics, network costs, exchange, where money moves** |
| 8 | [Security model](08-security.md) | Threat model and every defence, across all three products |
| 9 | [Build, run & deploy](09-build-run-deploy.md) | Exact commands for every platform |
| 10 | [Extending](10-extending.md) | How to add a coin, language, theme or exchange |
| 11 | [Glossary](11-glossary.md) | Every term explained in one line |

## 🗺️ The 30-second version

Umbrella is **three products sharing one philosophy — your keys never leave your device**:

- **Desktop** (`desktop/`) — a standalone, non-custodial wallet in C#/.NET + Avalonia. Real
  sending for 8 coins, a Monero full-node engine and Tor both bundled inside the app. This is
  the flagship.
- **Web frontend** (`src/`) — a React browser wallet + a P2P/exchange aggregator. The seed is
  generated and encrypted **in the browser**; the server never sees it.
- **Backend** (`backend/`) — a NestJS API that stores only *public* data (linked-wallet
  addresses, P2P offers, price cache) and drives the Telegram bot. It never touches a private key.

## 💸 The one thing to know about money

There is currently **no platform fee**. When you send, you pay only the **blockchain's own network
fee** — nothing to us. The one surprise is TRON: a USDT (TRC-20) transfer can cost several dollars
in TRX even for $1, because TRON charges "energy" that a fresh wallet hasn't staked. That is
TRON's economics, not our charge. Full detail — and where a future service fee *would* live — is in
[07-financial.md](07-financial.md).
