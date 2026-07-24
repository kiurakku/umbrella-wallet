# 10 · Extending the project

Safe, step-by-step recipes for the changes you'll actually make. Follow the existing patterns — they
exist for good reasons (usually a bug that was fixed).

## Add a coin (desktop)

⚠️ **The golden rule:** never ship a coin whose address derivation isn't verified against a
**published test vector**. A plausible-looking wrong address loses funds forever. This is why TON and
Cardano are disabled.

1. **Add to the chain enum** — `Core/Chains/ChainId.cs`.
2. **Derive the address** — add a case in `HdAddressDeriver`, using the coin's real derivation path
   and address format.
3. **Write a test first** — in `tests/`, assert the address derived from the canonical test mnemonic
   equals the value published by the coin's own reference implementation. If you can't find such a
   vector, **stop** — mark it `🚧` disabled like TON/Cardano.
4. **Map the ticker** — `SymbolFor()` and `ParseChain()` in `MainViewModel` (accept ticker,
   token-standard name, and full coin name; a missing map drops the coin silently).
5. **Read balances** — add to `PublicChainClients` / `PublicChainBalanceClient`.
6. **Add prices** — `CoinIds` and `BinancePairs` tables in `PublicChainClients`.
7. **Sending (optional, separate step)** — new `XxxTransactionSender`, gated by a test that proves the
   signing key matches the displayed address.

## Add a language (desktop)

1. `Localization.cs` → add a `new("xx", "Native name")` to `Languages`.
2. Add a `["xx"] = new() { … }` block with every key (missing keys fall back to English, so you can
   ship partial and fill in later).
3. That's it — the picker and live switching already work.

## Add a theme (desktop)

1. `Theming.cs` → add `new("id", "Display name")` to `Themes`.
2. Add an `["id"] = [ …hex values… ]` palette with **every** key in `Keys` (the count must match, or
   it throws at apply).
3. If it should have a gradient background, handle it in `GradientBackground(id)` and the
   `id is "gradient" or …` check in `Apply`.
4. Never theme the QR plate or danger-reds.

## Add an exchange (desktop, read-only)

1. `ExchangeConnectors.cs`:
   - add the name to `Supported`,
   - add a `FetchXxxAsync` that calls only the venue's **balance** endpoint with its signing scheme,
     and route it in `FetchBalancesAsync`,
   - set `RequiresPassphrase` / `RequiresSecret` and a `KeyHint`.
2. **Read-only only.** Do not add any trading or withdrawal call — the project's guarantee is that no
   such code exists.
3. A test asserts every `Supported` venue has a key hint and correct passphrase/secret flags.

## Add a page (web frontend)

1. Create `src/routes/yourpage.tsx` (TanStack Start file-based routing picks it up).
2. Use TanStack Query for any backend data; keep wallet crypto in `lib/wallet/` (never send the seed).
3. Add nav if it should be reachable from the shell.

## Add a backend endpoint

1. Create/extend a module under `backend/src/<feature>/` (controller + service + DTOs).
2. Import it in `app.module.ts`.
3. **Never** accept a private key or mnemonic. If you're tempted to, the design is wrong — the
   operation belongs in the client.
4. Throttle sensitive endpoints (`@Throttle`), validate DTOs, and use Prisma (no raw SQL).
5. Add a test if it has non-trivial logic (see `test/p2p-transitions.test.ts` as a model).

## Add / change a fee (financial)

Read [07-financial.md](07-financial.md) first. In brief:

- **Swap fee** → edit `rates.service.convert()` to apply a disclosed percentage (recommended path).
- **Send fee** → add an output in the relevant `*TransactionSender` (raises network fee, visible
  on-chain).
- The license requires the fee be **disclosed in the UI where it applies** — wire a review-screen
  line that shows it before the user confirms.

## Bump the version

Change all three in sync, or the footer/installer will disagree:

1. `desktop/src/Umbrella.Wallet.App/Umbrella.Wallet.App.csproj` → `<Version>`.
2. `desktop/installer/umbrella.iss` → `#define AppVersion`.
3. The status-bar text in `MainWindow.axaml` and the badge in `README.md`.

## Before you commit

- Desktop: `dotnet test` green.
- Web: `npx tsc --noEmit`, `npm run build`, `npm audit`.
- Backend: `npm test`, `npm run build`.
- Never commit `.env`, secrets, or the `tor/` / `monero/` binaries (all gitignored).
- Never `git push --force`.
