# Contracts

Solidity utilities used by the desktop wallet for efficient on-chain operations.

## FeeSplitter

[`FeeSplitter.sol`](./FeeSplitter.sol) forwards multiple payment legs in a **single** Ethereum
transaction. The contract is non-custodial: it holds no balance, forwards atomically, and reverts
if any leg fails. There is no owner, withdraw function, or upgrade path.

### Deploy

```bash
forge create contracts/FeeSplitter.sol:FeeSplitter \
  --rpc-url https://eth.llamarpc.com \
  --private-key <DEPLOY_KEY>
```

Record the deployed contract address and wire it into the desktop ETH send path.
