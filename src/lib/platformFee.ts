/**
 * Platform (developer) fee configuration — the web counterpart of the desktop
 * `DeveloperFeeConfig`. Stores the fee percentage and a receiving address per chain, entirely in
 * localStorage, so it works with no backend. A small PIN gate keeps the admin panel out of casual
 * reach; on a device-local app it is a soft gate, not real access control.
 *
 * The web wallet does not broadcast on-chain sends (trades settle over P2P), so nothing is
 * collected here today — the fee percentage only drives the *disclosed* quote on the Exchange
 * screen, and the addresses are stored for when a client-side swap exists. Real collection lives
 * in the desktop app. The fee is always shown before the user acts; there is no hidden charge.
 */

import { PLATFORM_SPREAD_BPS } from "@/lib/api/client";

const BPS_KEY = "umbra.fee.bps.v1";
const ADDR_KEY = "umbra.fee.addr.v1";
const PIN_KEY = "umbra.fee.pin.v1";

/** Hard ceiling so a fat-fingered value can never quote a wild fee. */
export const MAX_FEE_BPS = 200;

/** Chains offered in the admin panel (parity with the desktop wallet). */
export const FEE_CHAINS = ["BTC", "LTC", "ETH", "SOL", "TRX", "USDT", "XMR"] as const;
export type FeeChain = (typeof FEE_CHAINS)[number];

function read(key: string): string | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string | null) {
  try {
    if (value === null) window.localStorage.removeItem(key);
    else window.localStorage.setItem(key, value);
  } catch {
    /* ignore */
  }
}

/** Fee in basis points, clamped to [0, MAX_FEE_BPS]. Defaults to the baked-in spread. */
export function getFeeBps(): number {
  const raw = Number.parseInt(read(BPS_KEY) ?? "", 10);
  const bps = Number.isFinite(raw) ? raw : PLATFORM_SPREAD_BPS;
  return Math.min(Math.max(bps, 0), MAX_FEE_BPS);
}

export function setFeeBps(bps: number) {
  const clamped = Math.min(Math.max(Math.round(bps), 0), MAX_FEE_BPS);
  write(BPS_KEY, String(clamped));
}

export function getFeePercent(): number {
  return getFeeBps() / 100;
}

export function getFeeAddresses(): Record<string, string> {
  try {
    const parsed = JSON.parse(read(ADDR_KEY) ?? "{}");
    return parsed && typeof parsed === "object" ? (parsed as Record<string, string>) : {};
  } catch {
    return {};
  }
}

export function getFeeAddress(chain: string): string {
  return getFeeAddresses()[chain.toUpperCase()] ?? "";
}

export function setFeeAddresses(addresses: Record<string, string>) {
  const clean: Record<string, string> = {};
  for (const [k, v] of Object.entries(addresses)) {
    const t = (v ?? "").trim();
    if (t) clean[k.toUpperCase()] = t;
  }
  write(ADDR_KEY, JSON.stringify(clean));
}

// --- Soft PIN gate --------------------------------------------------------

async function sha256(text: string): Promise<string> {
  const bytes = new TextEncoder().encode(text);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

export function hasAdminPin(): boolean {
  return !!read(PIN_KEY);
}

export async function setAdminPin(pin: string): Promise<void> {
  write(PIN_KEY, await sha256(pin));
}

export async function verifyAdminPin(pin: string): Promise<boolean> {
  const stored = read(PIN_KEY);
  return !!stored && stored === (await sha256(pin));
}
