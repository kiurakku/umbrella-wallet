import { scrubSensitive } from "./scrubSecrets";
import { isPrivacyMode } from "./privacyMode";
import { isRunningViaTor } from "./wallet/tor";

type ErrorReportOptions = {
  mechanism?: "manual" | "onerror" | "unhandledrejection" | "react_error_boundary";
  handled?: boolean;
  severity?: "error" | "warning" | "info";
};

/** Best-effort client-side error hook. No telemetry is sent in privacy/Tor mode. */
export function reportClientError(error: unknown, context: Record<string, unknown> = {}) {
  if (typeof window === "undefined") return;
  if (isPrivacyMode() || isRunningViaTor()) return;

  const payload = scrubSensitive({ ...context, error });
  if (import.meta.env.DEV) {
    console.error("[umbrella-wallet]", payload);
  }
}

export type { ErrorReportOptions };
