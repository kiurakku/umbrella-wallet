/**
 * Platform (developer) fee — baked in, no user configuration.
 *
 * The web wallet does not broadcast on-chain sends (trades settle over P2P), so nothing is
 * collected here — this only drives the *disclosed* fee percentage on the Exchange quote. The
 * recipient address is baked, obfuscated, into the desktop app (see DeveloperFeeConfig); it is
 * never surfaced in any UI. The fee percentage is always shown before the user acts.
 */

import { PLATFORM_SPREAD_BPS } from "@/lib/api/client";

/** Fee in basis points (50 = 0.5%). */
export function getFeeBps(): number {
  return PLATFORM_SPREAD_BPS;
}

export function getFeePercent(): number {
  return PLATFORM_SPREAD_BPS / 100;
}
