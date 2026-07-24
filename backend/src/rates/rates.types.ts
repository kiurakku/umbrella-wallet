/**
 * Pluggable rates layer — Umbrella must run with no commercial API keys.
 * Adapters are tried in order; failures fall back to the last-known cache.
 */

export type AssetPrice = {
  symbol: string;
  price: number;
  change24h: number;
};

export type MultiFiatPrice = {
  usd: number;
  uah: number;
  eur: number;
  change24h: number;
};

export type MarketSnapshot = {
  prices: Record<string, MultiFiatPrice>;
  updatedAt: number;
};

export type RateQuote = {
  base: string;
  quote: string;
  rate: number;
  at: number;
};

export type MarketChart = {
  symbol: string;
  days?: number;
  prices: number[];
  timestamps?: number[];
};

export type SparklineResult = {
  symbol: string;
  prices: number[];
  timestamps: number[];
};

export type ConvertResult = {
  /** What the user receives, AFTER the platform spread is applied. */
  result: number;
  /** Effective rate the user gets (already includes the spread). */
  rate: number;
  /** Platform fee, expressed in the destination asset (result units). */
  fee: number;
  /** The mid-market rate before any spread — shown next to `rate` for transparency. */
  marketRate: number;
  /** What the user would have received at the mid-market rate (before spread). */
  marketResult: number;
  /** Spread in basis points (50 = 0.5%). Surfaced so the UI can label it exactly. */
  spreadBps: number;
};

export interface IRatesProvider {
  /** Adapter id for logs/diagnostics. */
  readonly id: string;
  /** False when the adapter is not configured — orchestrator skips it. */
  isEnabled(): boolean;
  getMarketPrices(symbols: string[]): Promise<AssetPrice[]>;
  getRate(base: string, quote: string): Promise<RateQuote | null>;
  getMarketChart(symbol: string, days: number): Promise<MarketChart | null>;
}

/** CoinGecko ids for the symbols Umbrella prices. Used by adapters that need them. */
export const COIN_IDS: Record<string, string> = {
  BTC: "bitcoin",
  ETH: "ethereum",
  USDT: "tether",
  SOL: "solana",
  TON: "the-open-network",
  XMR: "monero",
  BNB: "binancecoin",
  MATIC: "matic-network",
  USDC: "usd-coin",
  TRX: "tron",
  AVAX: "avalanche-2",
  LTC: "litecoin",
  DOGE: "dogecoin",
  ADA: "cardano",
};

/** Core market set for /rates/market snapshot. */
export const MARKET_COINS: Array<{ id: string; symbol: string }> = [
  { id: "bitcoin", symbol: "BTC" },
  { id: "ethereum", symbol: "ETH" },
  { id: "tether", symbol: "USDT" },
  { id: "solana", symbol: "SOL" },
  { id: "the-open-network", symbol: "TON" },
  { id: "monero", symbol: "XMR" },
  { id: "binancecoin", symbol: "BNB" },
  { id: "matic-network", symbol: "MATIC" },
  { id: "usd-coin", symbol: "USDC" },
];

export const PRICED_SYMBOLS = Object.keys(COIN_IDS);

export const MARKET_SYMBOLS = MARKET_COINS.map((c) => c.symbol);
