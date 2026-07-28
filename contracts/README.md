# FeeSplitter — one-transaction ETH fee batcher

[`FeeSplitter.sol`](./FeeSplitter.sol) makes the developer fee on an **ETH** send economical: instead
of a second transaction (extra ~21k+ gas, doubtful economics on a small fee), the recipient payment
and the fee are forwarded in **one** transaction. The contract is non-custodial — it holds nothing,
forwards both legs atomically, and reverts if either fails. There is no owner, withdraw, or upgrade.

This is the ETH counterpart to how BTC/LTC/XMR/SOL already collect the fee in a single transaction.

## Status

- **Fee recipient: baked in.** `feeRecipient` is hardcoded (immutable) to
  `0x01d1a1413F6b15f58906c804c261AFc12C3DCdBe` (EIP-55 checksum verified). There is no constructor
  arg and no setter — a deploy cannot point it elsewhere.
- **Remaining (requires you): a one-time deploy.** Deploying is a real, paid on-chain transaction
  from a funded key. I don't hold keys and won't move funds, so you (or your pipeline) run it.

## Deploy (one zero-argument command)

```bash
forge create contracts/FeeSplitter.sol:FeeSplitter \
  --rpc-url https://eth.llamarpc.com \
  --private-key <DEPLOY_KEY>
```

Then send me the deployed contract address (`0x…`).

## Integration (I wire this after deploy)

Once the address exists, the desktop ETH sender routes sends through it in one transaction:

- Instead of a plain transfer of `amount` to `recipient`, it calls
  `FeeSplitter.pay(recipient, amount)` with `msg.value = amount + fee` (fee = `amount * bps / 10000`).
- The recipient receives exactly `amount`; the developer receives `fee`; one network fee total.
- The deployed contract address is baked into the app (obfuscated), exactly like the SOL fee address
  in `DeveloperFeeConfig`; `RoutedChains` gains `ETH`, and the send review discloses the fee as it
  already does for BTC/LTC/XMR/SOL.

The contract's `maxFeeBps` guard means even a bug in the client can never skim more than the cap.
