# 9 · Build, run & deploy

Exact commands for every product. Copy-paste ready.

## Prerequisites

| For | Install |
|-----|---------|
| Desktop | .NET 8 SDK |
| Web frontend | Node.js 20+, npm |
| Backend | Node.js 20+, PostgreSQL, Redis |

## Desktop

### Run from source

```bash
cd desktop
dotnet run --project src/Umbrella.Wallet.App/Umbrella.Wallet.App.csproj
```

### Test (77 tests, pinned to published crypto vectors)

```bash
cd desktop
dotnet test
```

### Windows release (installer + portable)

```powershell
cd desktop
./scripts/fetch-tor.ps1        # stage tor.exe + geoip (once; ~35 MB, not committed)
./scripts/fetch-monero.ps1     # stage monero-wallet-rpc.exe (once; ~39 MB, not committed)

# portable single-file exe
dotnet publish src/Umbrella.Wallet.App/Umbrella.Wallet.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# installer (needs Inno Setup)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/umbrella.iss
# → UmbrellaWallet-Setup-<version>.exe
```

### Linux release (self-contained tar.gz)

```bash
cd desktop
./scripts/publish-linux.sh     # publishes, fetches Linux Tor/Monero helpers, packs tar.gz
# → dist/linux/umbrella-wallet-<version>-linux-x64.tar.gz
# run: ./Umbrella.Wallet.App
```

> The Tor and Monero helper binaries are **per-OS** and **not committed** (~50 MB of third-party
> build output). The fetch scripts stage them from the official Tor Project and getmonero.org
> distributions. The build copies whatever is in `Assets/tor/` and `Assets/monero/` into the output.

## Web frontend

### Run (dev)

```bash
npm install
npm run dev            # http://localhost:5173
```

For a standalone preview with no backend, set `VITE_DEMO_MODE=true` (in `.env.local`) — the app runs
against the in-memory demo API.

### Typecheck / lint / build

```bash
npx tsc --noEmit      # types
npx eslint .          # lint
npm run build         # production build
```

### Deploy (Vercel)

The frontend is Vercel-ready. Configure `VITE_API_URL` (backend URL) and any Telegram keys in the
Vercel project env. See `DEPLOY.md`.

## Backend

### Run (dev)

```bash
cd backend
npm install
# set DATABASE_URL, REDIS_URL, JWT_ACCESS_SECRET, JWT_REFRESH_SECRET, CORS_ORIGIN in .env
npx prisma migrate deploy      # apply schema
npm run start:dev              # watch mode
```

### Test

```bash
cd backend
npm test                       # p2p state-machine + telegram reliability
```

### Build

```bash
cd backend
npm run build                  # nest build
```

### Required environment variables

| Var | Purpose |
|-----|---------|
| `DATABASE_URL` | PostgreSQL connection |
| `REDIS_URL` | Redis connection |
| `JWT_ACCESS_SECRET` | ≥ 32 chars — **boot fails in prod without it** |
| `JWT_REFRESH_SECRET` | ≥ 32 chars, different from access |
| `CORS_ORIGIN` | comma-separated allowlist of frontend origins |
| `KYC_WEBHOOK_SECRET` | HMAC secret for KYC webhook (if used) |
| `OPEN_BANKING_WEBHOOK_SECRET` | HMAC secret for Open Banking webhook (if used) |
| `TELEGRAM_BOT_TOKEN` | bot + mini-app auth (if used) |
| `NODE_ENV=production` | enables strict secret checks |

## Docker (web + backend + Tor)

```bash
docker compose up              # see docker-compose.yml
docker compose -f docker-compose.tor.yml up   # with a Tor sidecar
```

## Release checklist (from `UMBRA_READINESS_CHECKLIST.md`)

1. `dotnet test` (desktop) green — 77 passing.
2. `npm run build` (frontend) + `npm test` (backend) green.
3. `npm audit` = 0 on both.
4. Bump `<Version>` in the desktop `.csproj`, `#define AppVersion` in `installer/umbrella.iss`, and
   the footer/README version.
5. Publish desktop (Windows installer + portable, Linux tar.gz).
6. Verify data lands in `data/` beside the exe and nothing writes to the system drive.
7. First **mainnet** transaction: test with a **small** amount — no send path has been broadcast on
   mainnet yet; signing is test-gated but untested against a live network.
