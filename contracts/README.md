# FeeSplitter — one-transaction ETH fee batcher

[`FeeSplitter.sol`](./FeeSplitter.sol) makes the developer fee on an **ETH** send economical: instead
of a second transaction (extra ~21k+ gas, doubtful economics on a small fee), the recipient payment
and the fee are forwarded in **one** transaction. The contract is non-custodial — it holds nothing,
forwards both legs atomically, and reverts if either fails. There is no owner, withdraw, or upgrade.

This is the ETH counterpart to how BTC/LTC/XMR/SOL already collect the fee in a single transaction.

## What's needed to activate it (not done yet — requires you)

1. **An Ethereum fee address.** The address you provided (`DRkL…46oCs`) is **Solana** — it can only
   receive SOL. The batcher needs a mainnet **ETH** address (`0x…`) as `feeRecipient`. Give me that
   and I'll bake it in (obfuscated) like the SOL one.
2. **Deploy access + gas.** Deploying a contract is a real, paid on-chain transaction from a funded
   key. I don't hold keys and won't move funds, so you (or your deploy pipeline) run the deploy. It
   is a one-time cost.

## Deploy (once you have the above)

```bash
# Foundry
forge create contracts/FeeSplitter.sol:FeeSplitter \
  --rpc-url https://eth.llamarpc.com \
  --private-key <DEPLOY_KEY> \
  --constructor-args <FEE_RECIPIENT_0x> 50    # 50 = 0.5% max-fee guard (cap 200 = 2%)
```

Record the deployed contract address.

## Integration (I wire this after deploy)

Once the address exists, the desktop ETH sender routes sends through it in one transaction:

- Instead of a plain transfer of `amount` to `recipient`, it calls
  `FeeSplitter.pay(recipient, amount)` with `msg.value = amount + fee` (fee = `amount * bps / 10000`).
- The recipient receives exactly `amount`; the developer receives `fee`; one network fee total.
- The deployed contract address is baked into the app (obfuscated), exactly like the SOL fee address
  in `DeveloperFeeConfig`; `RoutedChains` gains `ETH`, and the send review discloses the fee as it
  already does for BTC/LTC/XMR/SOL.

The contract's `maxFeeBps` guard means even a bug in the client can never skim more than the cap.
