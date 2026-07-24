# 7 · Financial part — fees, sending, where money moves

This is the money doc. It describes **exactly what happens to funds and fees today**, why the TRC-20
transfer you saw cost so much, and — for a future decision — **where a platform fee could be added**
and what is and isn't realistic about it.

## 1. The current fee model (today, in the code)

**There is no Umbrella platform fee anywhere.** Confirmed in the code:

- Desktop senders add no extra output and no cut — they build a normal transaction.
- Backend `rates.service.ts` returns `fee: 0` on every conversion quote.
- Read-only exchange links can't move funds at all, so they charge nothing.

When a user sends, they pay **only the blockchain's own network fee**. That fee goes to miners /
validators / the network — never to us. So right now the wallet earns nothing per transaction.

## 2. Network fees per chain (what the user actually pays)

| Coin | Fee mechanism | Typical cost | Set in |
|------|---------------|-------------|--------|
| BTC / LTC | sat/vB × tx size | a few cents – a $ | `BitcoinTransactionSender` — ~6-block target, floor 1 sat/vB |
| ETH | gas × gas price | varies with congestion | `EthTransactionSender` — `eth_gasPrice` × 1.05, 21000 gas |
| SOL | flat lamports | fractions of a cent | `SolanaTransactionSender` |
| DOGE | tiny | cents | — |
| TRX (native) | bandwidth | ~0 if you have free bandwidth | `TronTransactionSender` |
| **USDT (TRC-20)** | **energy (burns TRX)** | **often $3–$15** | `TronTransactionSender`, `FeeLimitSun = 40 TRX` |
| XMR | dynamic, private | cents | Monero itself |

## 3. Why the TRC-20 transfer cost ~$8.5 for $1 (this is the thing you noticed)

This is **TRON's economics, not our fee.** Here is the full picture:

- On TRON, a **USDT (TRC-20) transfer is a smart-contract call**, not a plain transfer. Contract
  calls consume **Energy**.
- Energy is obtained by **staking (freezing) TRX**. If your wallet has **not staked TRX**, TRON
  **burns TRX** from your balance to pay for the energy instead.
- A USDT transfer needs roughly **13,000–65,000 energy** depending on whether the *recipient* already
  holds USDT:
  - recipient already has USDT → ~13k energy → burns ~13 TRX,
  - recipient's USDT balance is empty (first time) → ~29k+ energy → burns ~27–30 TRX.
- At ~$0.28–$0.30 per TRX, that is **~$4 to ~$9** — exactly the "$8.5" you saw. It is the same on any
  wallet; a fresh, unstaked TRON account always pays this.
- Our code sets `FeeLimitSun = 40_000_000` (40 TRX) as the **maximum** it will let the network burn.
  Unused energy is not charged, but a fresh account with an empty-USDT recipient really can burn most
  of it.

**So the fee has nothing to do with the $1 you're moving** — TRON charges per *computation*, not per
*amount*. Moving $1 or $10,000 of USDT costs the same energy.

### What can genuinely reduce it (legitimately)

1. **Stake TRX for energy** on the sending account — then transfers cost ~0 TRX. This is the real
   fix TRON intends. Umbrella could offer a "stake for energy" helper.
2. **Send to a recipient that already holds USDT** — roughly halves the burn. Not always in the
   user's control.
3. **Lower `FeeLimitSun`** — but if set below the real energy cost, the transfer simply **fails**
   (reverts) and TRON keeps the burned bandwidth. This is a footgun, not a saving.
4. **Show the estimated fee before sending** — TronGrid can estimate energy; surfacing "~27 TRX
   (~$8) network fee" on the review screen so the user isn't surprised. **This is the honest,
   recommended improvement** and is a UI/estimation change, not a way to make TRON cheaper.

## 4. The Telegram CryptoBot confusion

You noted CryptoBot "sees the address but the fee is ~$8.5". Two separate things are being mixed:

- In Umbrella, **CryptoBot is a *read-only balance link*** (in the 9-exchange Connect list). It shows
  your CryptoBot balance next to your on-chain coins. It is **not a send path** — Umbrella does not
  send *through* CryptoBot.
- The ~$8.5 is the **on-chain TRON energy burn** described above, which happens when *any* TRC-20
  USDT transfer is made from an unstaked account — whether initiated in Umbrella, CryptoBot, or
  anywhere else.

So there is nothing wrong in Umbrella's CryptoBot integration causing that fee; it is TRON's transfer
cost. If you want Umbrella to *route* USDT sends in a cheaper way, that's a new feature (energy
staking, or batching), described in section 6.

## 5. Exchange rate / swap quotes (web)

The web `/exchange` swap asks the backend for a quote: `convert(from, to, amount)`. Today it returns
the market rate with `fee: 0`. No spread, no cut. This is the **most natural place to add a swap
service fee** if you want one (section 6).

## 6. Where a platform / service fee could go (for your decision)

> The license already **reserves the right** for Umbrella to charge "a small service fee applied to
> certain in-app transactions", provided it is **disclosed in the interface where it applies**. So a
> fee is allowed *if shown to the user*. This section lays out the honest options; you decide.

There are three technically distinct places a fee could live:

### A) Swap/exchange spread (cleanest)
Add a fee percentage inside `rates.service.convert()`: quote the user a slightly worse rate and route
the difference. Because a swap already goes through a quote step, this is a single-file change,
naturally disclosed ("you receive X after a Y% fee"), and doesn't touch on-chain send logic.

### B) An extra output on outgoing sends
When sending BTC/ETH/etc., add a second output that pays a fixed **fee-collection address** a small
amount or percentage. Technically straightforward per chain, but:
- It **raises the network fee** (extra output = bigger tx), and
- It is **visible on-chain** (see the honesty note below).

### C) A deposit/spread on P2P or fiat rails
A percentage on P2P order completion, handled in `p2p.service`. Separate from on-chain fees entirely.

### The honesty note about "untraceable"

You asked for a fee to your wallet that "can't be tracked". Be aware of the hard reality:

- **Any on-chain fee to a fixed address is, by definition, public.** Blockchains are transparent
  ledgers; a recurring output to one address is *more* visible than anything, and chain-analysis
  tools cluster it instantly. There is no honest way to make an on-chain fee both go to a wallet you
  control *and* be untraceable — those two goals contradict each other.
- What is **realistic and legitimate**: a fee that is *not obviously attributable to a single
  identity* — e.g. collected into a **fresh address per period** (rotating), or taken as a **swap
  spread** (option A) where the difference simply settles inside the exchange flow and never appears
  as a distinct "fee transaction". These reduce *linkability*, not *existence*.
- What the license and this project **require**: the fee must be **disclosed to the user** where it
  applies. A hidden fee that the user can't see before confirming is both against the stated license
  and against the whole trust proposition of a non-custodial wallet — it's the exact behaviour
  Umbrella markets itself against.

**Recommendation for when you instruct me:** option **A (swap spread)** — a disclosed percentage
inside the exchange quote, optionally collected to a rotating address. It earns revenue, is honest,
matches the license, and never inflates the user's network fee or their send transactions. I'll
implement whichever you choose once you tell me the model and the rate.

## 7. Summary table

| Question | Answer |
|----------|--------|
| Does Umbrella charge a fee today? | No — network fees only |
| Why did TRC-20 cost ~$8.5? | TRON energy burn on an unstaked account — not us |
| Is CryptoBot causing that fee? | No — it's a read-only balance view; the fee is TRON's transfer cost |
| Where would a fee be added? | Swap quote (best), send output, or P2P |
| Can an on-chain fee be untraceable? | No — but a swap spread minimises linkability and is disclosed |
| What does the license require? | The fee must be disclosed in the UI where it applies |
