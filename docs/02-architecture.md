# 2 · Architecture

## The big picture

```mermaid
graph TB
    subgraph User Devices
        D[Desktop app<br/>.NET + Avalonia]
        W[Web app<br/>React in browser]
        T[Telegram<br/>mini-app]
    end

    subgraph "Desktop-only bundled processes"
        TOR[tor.exe<br/>SOCKS5 :9250]
        XMR[monero-wallet-rpc<br/>JSON-RPC :18099]
    end

    subgraph "Public blockchain data (no account needed)"
        EXP[Block explorers<br/>esplora, blockstream]
        RPC[Public RPC nodes<br/>ETH, SOL, TRON, XMR]
        CG[CoinGecko / Binance<br/>prices & charts]
    end

    subgraph "Backend (public data only)"
        API[NestJS API]
        PG[(PostgreSQL)]
        RD[(Redis)]
        BOT[Telegram bot]
    end

    subgraph Exchanges
        EX[Binance, Bybit, OKX,<br/>Kraken, ... CryptoBot]
    end

    D --> TOR
    D --> XMR
    D --> EXP
    D --> RPC
    D --> CG
    D -.read-only keys.-> EX

    W --> API
    W --> EXP
    W --> CG
    T --> API
    T --> BOT

    API --> PG
    API --> RD
    XMR -.through Tor when on.-> RPC
    TOR -.wraps traffic.-> RPC
```

**Key reading:** the desktop talks *directly* to public blockchain data (optionally wrapped in Tor)
and never needs the backend. The web app uses the backend only for the *aggregator* half (auth, P2P,
linked-account list) — its wallet half also talks to public data directly from the browser.

## Where private keys live

This is the most important diagram in the whole project.

```mermaid
graph LR
    subgraph "Desktop device"
        DS[Seed 24 words] --> DV[Vault file<br/>Argon2id + AES-256-GCM]
        DV --> DD[(data/vault.json<br/>beside the .exe)]
    end
    subgraph "Web browser"
        WS[Seed 24 words] --> WV[Vault blob<br/>Argon2id + AES-GCM]
        WV --> WI[(IndexedDB<br/>seed:userId)]
    end
    subgraph "Backend server"
        SRV[Stores: addresses, offers,<br/>price cache, auth hashes]
        NK[❌ NEVER: seeds, keys, passwords in clear]
    end

    DS -.never leaves.-> DS
    WS -.never leaves.-> WS
```

The seed exists in exactly two places, both on the user's own machine: the desktop vault file, or
the browser's IndexedDB. **No arrow crosses into the server.** The backend's user table stores an
Argon2id *hash* of the login password (not the encryption password, and never the seed).

## Repository layout

```
Umbrella Wallet/
├── desktop/                         # Product 1 — .NET/Avalonia wallet
│   ├── src/
│   │   ├── Umbrella.Wallet.Core/            # pure crypto: BIP39, derivation, Monero keys
│   │   ├── Umbrella.Wallet.Infrastructure/  # network: senders, Tor, Monero RPC, vault I/O
│   │   └── Umbrella.Wallet.App/             # UI: Avalonia views + view-models
│   ├── tests/                               # 77 tests, pinned to published test vectors
│   ├── scripts/                             # fetch-tor.ps1, fetch-monero.ps1, publish-linux.sh
│   └── installer/umbrella.iss               # Inno Setup Windows installer
│
├── src/                             # Product 2 — web frontend (React)
│   ├── routes/                              # pages: index, exchange, p2p, stats, settings, legal
│   ├── components/                          # UI + wallet sheets
│   ├── lib/
│   │   ├── wallet/                          # seedManager, vault, walletCore, monero, tor, coinjoin
│   │   ├── api/                             # REST client + demo-mode implementation
│   │   └── market/                          # price/chart helpers
│   └── content/legal/                       # terms, privacy, agreement, rules
│
├── backend/                        # Product 3 — NestJS API
│   ├── src/
│   │   ├── auth/                            # JWT access+refresh, register/login
│   │   ├── users/  linked-wallets/  linked-bank-accounts/
│   │   ├── p2p/                             # offers, orders, state machine
│   │   ├── rates/                           # price aggregation + conversion
│   │   ├── kyc/    webhooks/                # optional KYC + signed webhooks
│   │   ├── telegram/                        # bot + mini-app auth
│   │   └── common/                          # crypto, env, throttler, webhook util
│   └── prisma/schema.prisma                 # database schema
│
├── docs/                           # ← you are here
└── README.md  LICENSE  DEPLOY.md
```

## Data-flow examples

### Desktop: "send 0.01 BTC"

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant S as BitcoinTransactionSender
    participant E as Esplora explorer
    U->>VM: enter address + amount, Confirm
    VM->>S: PrepareSendAsync(from, to, amount)
    S->>E: GET /address/{from}/utxo  (via Tor if on)
    E-->>S: unspent outputs
    S->>E: GET /fee-estimates
    S->>S: build tx, sign locally with NBitcoin
    S->>S: builder.Verify() must pass
    S-->>VM: quote (amount, fee)
    U->>VM: ConfirmSendAsync
    VM->>S: SignAndBroadcast
    S->>E: POST /tx  (signed bytes only)
    E-->>S: txid
```

The private key is used only inside `BitcoinTransactionSender` on the device; only the finished
signed transaction leaves.

### Web: "link my MetaMask address"

```mermaid
sequenceDiagram
    participant U as User
    participant W as Web app
    participant API as Backend
    participant MM as MetaMask
    W->>API: POST /linked-wallets/challenge
    API-->>W: { nonce, message }  (stored in Redis, per-user, TTL, one-time)
    W->>MM: personal_sign(message)
    MM-->>W: signature
    W->>API: POST /linked-wallets { chain, address, message, signature }
    API->>API: verifyEvmPersonalSign — recover signer == address?
    API->>API: message contains nonce? delete nonce (no replay)
    API-->>W: linked (address stored — never a key)
```

Only a signature and a public address are involved. The backend proves the user controls the address
without ever seeing its key.

## Why the two "wallet" halves are separate code

The desktop and web wallets implement the **same cryptography** (BIP39, Argon2id, AES-GCM) but in
**different languages** (C# vs TypeScript) because they run in different environments. They are kept
deliberately in sync at the spec level — same word count, same KDF parameters, same derivation paths
— so a seed created in one can be imported into the other. They are not a shared library; each is
tested independently against the published BIP/SLIP test vectors.
