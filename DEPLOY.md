# Deploy Umbrella Wallet

Umbrella Wallet is distributed as a **desktop application**. Releases are published on GitHub — not via Vercel or other cloud frontends.

## Desktop releases (primary)

| Platform | Artifact | How |
|----------|----------|-----|
| Windows | `UmbrellaWallet-Setup-1.7.0.exe` | [Latest release](https://github.com/kiurakku/umbrella-wallet/releases/latest) |
| Windows | portable `Umbrella.Wallet.App.exe` | CI build attached to tag |
| Linux | `umbrella-wallet-linux-x64.tar.gz` | `./desktop/scripts/publish-linux.sh` |

### Build Windows installer locally

```powershell
cd desktop
./scripts/fetch-tor.ps1
./scripts/fetch-monero.ps1
dotnet publish src/Umbrella.Wallet.App/Umbrella.Wallet.App.csproj -c Release -r win-x64
# then run Inno Setup: desktop/installer/umbrella.iss
```

## Optional: local web + API (developers)

```bash
npm install && npm run dev
cd backend && npm install && npm run start:dev
```

Specs: `UMBRA_BACKEND_SPEC.md` · `UMBRA_AGGREGATOR_ADDENDUM.md` · `CHANGELOG.md`

**Owner:** [kiurakku](https://github.com/kiurakku)
