# 07 — Financial Model & Fees

## Current state: zero platform fee by default

Umbra ships with **zero markup** by default. Users pay only network (miner/validator) fees. There are no subscriptions and no hidden charges. A developer fee **can** be switched on per install (see below); until it is, nothing is added.

This is intentional for launch — build trust first. But the platform must be sustainable.

---

## Implemented: developer fee on send + admin panel

Both clients ship a **disclosed developer fee on sends**, configurable with no backend:

- **Config** — fee percentage (basis points, capped at **2%**) plus a receiving address **per chain**, stored in a local file (`developer-fee.json` on desktop) / `localStorage` on web. Default is **0% / no address** → the wallet behaves as zero-fee until a developer sets it. To ship one fixed fee to every install, bake the defaults in before building (`DeveloperFeeConfig` on desktop, `PLATFORM_SPREAD_BPS` + client defaults on web).
- **Admin panel** — desktop: *Settings → Developer*; web: the `/admin` route behind a local PIN. Sets the percentage and per-chain address.
- **Disclosure (required)** — the fee is **always shown in the send review before the user confirms**, exactly like the network fee. `07-financial.md` (below) and consumer-protection law require this; a hidden skim is not built or supported.
- **Routing** — the fee is only taken on chains where the send path actually routes it, so the disclosure can never claim a fee the wallet does not collect:
  - **Routed today (desktop):** BTC, LTC (extra output in the same transaction — one network fee), and XMR (second `destinations` entry in the same RingCT transfer). A fee address that fails validation is dropped, so a misconfiguration never blocks the user's send.
  - **Configurable, routing pending:** ETH, SOL, TRX, USDT-TRC20 — deferred because a `%` fee via a second transaction is uneconomic (ETH gas, TRON energy) or needs a change to pinned transaction-serialization (SOL). Web has no on-chain send at all, so on web the percentage only drives the disclosed Exchange quote.

**Trade-off (see "Anonymity and fee collection" below):** an on-chain fee address is chain-analysis visible, unlike the swap-spread option. This was a deliberate product choice to make the fee collectible with **no server**; the spread model needs a market-maker/backend to realise the revenue.

---

## Why TRC-20 (USDT on Tron) is expensive

This is **not** Umbra's fee and **not** CryptoBot's fee. It is the Tron network's economics:

### How Tron fees work

Tron smart contract calls (like USDT TRC-20 transfers) consume **Energy**. Energy is obtained by staking TRX. If the wallet has no staked TRX, Tron **burns TRX** instead.

| Scenario | Energy needed | TRX burned | USD cost (at $0.28/TRX) |
|----------|--------------|------------|--------------------------|
| Send USDT to account that already has USDT | ~13,000 Energy | ~13 TRX | ~$3.6 |
| Send USDT to fresh account (no USDT history) | ~27,000-30,000 Energy | ~27-30 TRX | ~$7.5-8.4 |
| Send USDT if wallet has enough staked TRX | 0 TRX burned | 0 | Free |

**This is how Tron works for everyone** — MetaMask, Trust Wallet, Binance, any wallet. Not Umbra-specific.

### Mitigation strategies for users

1. **Stake TRX** — stake ~10,000+ TRX to get enough Energy for free USDT transfers
2. **Use ERC-20 USDT** (Ethereum) — gas ~$0.50-3 instead
3. **Use USDC on Solana** — fees <$0.001
4. **Use P2P** — no on-chain transaction during fiat↔crypto trade

---

## Revenue model options

### Option A: Swap spread (RECOMMENDED)

**What:** When user exchanges coin A → coin B, the displayed rate includes a small spread (e.g., 0.5%). The difference between market rate and shown rate is the platform's revenue.

**Example:**
- Market rate: 1 ETH = $3,000.00
- Umbra shows: 1 ETH = $2,985.00 (0.5% spread)
- User agrees and swaps
- Umbra earns $15 per ETH swapped (kept in the rate, not a separate on-chain transaction)

**Why this is ideal:**

| Property | Swap spread | On-chain fee address |
|----------|-------------|----------------------|
| Chain-analysis visible | **No** — rate is internal | **Yes** — fee address is public |
| Adds to network fee | No | No |
| Legal compliance | Simple (disclose in UI) | Complex (MSB license risk) |
| User-friendly | Transparent in UI | Annoying (extra gas) |
| Implementation | 1 constant in rates.service.ts | Smart contract or tx splitting |

**Implementation (1 change):**

```typescript
// backend/src/rates/rates.service.ts

const PLATFORM_SPREAD_BPS = 50; // 0.5% = 50 basis points
// Change to 100 for 1%, 25 for 0.25%, etc.

// When returning exchange rate for quote A→B:
const marketRate = rawCoinGeckoRate;
const umbраRate = marketRate * (1 - PLATFORM_SPREAD_BPS / 10_000);
return { rate: umbраRate, spread: PLATFORM_SPREAD_BPS / 100 + "%" };
```

And in the frontend exchange UI, show:
```
You pay: 1 ETH
You receive: 2,985 USDT
Rate: 2,985 USDT/ETH (incl. 0.5% service fee)  ← ALWAYS SHOW THIS
```

### Option B: P2P matchmaking fee

**What:** Charge a small % on completed P2P orders.

**Example:** 0.1% of deal amount deducted from seller's crypto proof requirement.

**How to implement:**
- Add `platformFeePercent Decimal` to `p2p_offers` table (set globally or per-offer type)
- On order completion: record `platformFeeAmount` in `p2p_orders`
- Fee is virtual (tracked in DB), not on-chain — no separate transaction

**Concern:** This works only if you have a way to enforce it. In a pure non-custodial P2P (no escrow), you can't withhold crypto from the seller. So this works best if:
- You add soft enforcement: orders marked "completed" only after seller confirms fee was deducted
- Or: you run a hybrid escrow for TRC-20 (smart contract)

**Recommended %:** 0.1–0.2% per completed deal — low enough that users don't defect to other platforms, high enough to be meaningful at volume.

### Option C: Subscription / Pro tier

**What:** Free tier has rate-limited P2P (e.g., 5 deals/month). Pro tier ($5/month) unlimited.

**Cons:**
- Adds friction, may suppress growth
- Requires payment processing (Stripe integration)
- More compliance overhead

**Not recommended** for privacy-first wallet. Users who want anonymity distrust recurring payments.

### Option D: NFT/collectible minting fee (future)

When NFT functionality is added, charge 1-2% on minting or marketplace transactions.

---

## Recommended setup for launch

| Phase | Revenue model | Expected rate |
|-------|--------------|---------------|
| Launch | 0% (no fee) | Trust building |
| Month 2-3 | 0.25% swap spread | Low, barely noticeable |
| Month 4+ | 0.5% swap spread | Standard market rate |
| Scale | 0.5% swap + 0.1% P2P | Dual stream |

**Never go above 1% swap spread** — users will notice and compare to DEX aggregators (0.3% on Uniswap).

---

## Anonymity and fee collection

**IMPORTANT — architectural constraint:**

You CANNOT make on-chain fee collection anonymous. Here's why:

1. Blockchain is a public ledger.
2. If every transaction sends 0.5% to address `0xFEE...`, chain analysis tools (Chainalysis, Elliptic) will cluster that address and link it to Umbra within weeks.
3. This **creates legal exposure** — regulators can subpoena exchange to identify the wallet owner.

**Safe approaches:**

1. **Swap spread** (recommended above) — no on-chain fee transaction. Revenue is the rate difference, tracked only in your backend DB.
2. **Address rotation** — if you must collect on-chain, use a new HD wallet address for every 100 transactions. Much harder to cluster.
3. **Monero for fee collection** — if fee goes to your Monero wallet, it's genuinely untraceable. But converting crypto→XMR→you is complex.

**Legal note:** In all cases, the fee must be **shown to users before they confirm** the transaction. This is required for consumer protection laws in virtually all jurisdictions, and also required for maintaining the "non-custodial aggregator" legal model.

---

## Fee display in UI (required)

Before any swap or P2P creation:

```jsx
// In exchange.tsx rate display:
<div className="text-xs text-muted-foreground">
  Market rate: {marketRate} USDT/ETH
</div>
<div className="text-xs text-primary">
  Applied rate: {umbраRate} USDT/ETH
</div>
<div className="text-xs text-muted-foreground">
  Service fee: 0.5% (included in rate)
</div>
```

This is both legally required and builds user trust (they see it's small and consistent).

---

## Financial projections (rough)

At 0.5% swap spread:

| Monthly swap volume | Platform revenue |
|--------------------|-----------------|
| $100,000 | $500/month |
| $500,000 | $2,500/month |
| $1,000,000 | $5,000/month |
| $5,000,000 | $25,000/month |

**What drives volume:** P2P is the main acquisition channel. Users come for P2P → discover swap → swap becomes recurring revenue. Grow P2P DAU first.

---

## No fees we will never add

1. ~~Withdrawal fee~~ — contradicts non-custodial model (we don't hold funds)
2. ~~Account subscription~~ — wrong model for privacy-first wallet
3. ~~Inactivity fee~~ — hostile to users
4. ~~Forced KYC to use wallet~~ — defeats the anonymity purpose; KYC only required for P2P above thresholds
