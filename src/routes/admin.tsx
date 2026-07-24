import { createFileRoute } from "@tanstack/react-router";
import { AppShell } from "@/components/AppShell";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { ShieldCheck, Lock } from "lucide-react";
import {
  FEE_CHAINS,
  MAX_FEE_BPS,
  getFeeBps,
  setFeeBps,
  getFeeAddresses,
  setFeeAddresses,
  hasAdminPin,
  setAdminPin,
  verifyAdminPin,
} from "@/lib/platformFee";

export const Route = createFileRoute("/admin")({
  head: () => ({
    meta: [
      { title: "Developer — Umbrella Wallet" },
      { name: "description", content: "Platform fee configuration." },
      { name: "robots", content: "noindex" },
    ],
  }),
  component: AdminPage,
});

const ROUTED = new Set<string>(); // web has no on-chain send yet — nothing routes here

const PLACEHOLDER: Record<string, string> = {
  BTC: "bc1…",
  LTC: "ltc1…",
  ETH: "0x…",
  SOL: "Solana address",
  TRX: "T…",
  USDT: "T… (TRC-20)",
  XMR: "4…",
};

function AdminPage() {
  const [unlocked, setUnlocked] = useState(false);
  const [needsPin] = useState(() => hasAdminPin());
  const [pin, setPin] = useState("");
  const [pin2, setPin2] = useState("");

  const [percent, setPercent] = useState("0.5");
  const [addresses, setAddresses] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!unlocked) return;
    setPercent((getFeeBps() / 100).toString());
    setAddresses(getFeeAddresses());
  }, [unlocked]);

  const unlock = async () => {
    if (!needsPin) {
      // First run: set a PIN so the panel is not wide open.
      if (pin.length < 4) return toast.error("Choose a PIN of at least 4 digits");
      if (pin !== pin2) return toast.error("PINs do not match");
      await setAdminPin(pin);
      setUnlocked(true);
      toast.success("Admin PIN set");
      return;
    }
    if (await verifyAdminPin(pin)) {
      setUnlocked(true);
    } else {
      toast.error("Wrong PIN");
    }
  };

  const save = () => {
    const raw = percent.trim().replace(",", ".");
    const pct = Number.parseFloat(raw);
    if (!Number.isFinite(pct) || pct < 0) return toast.error("Enter the fee as a percentage, e.g. 0.5");
    let bps = Math.round(pct * 100);
    if (bps > MAX_FEE_BPS) {
      bps = MAX_FEE_BPS;
      toast.info(`Capped at ${MAX_FEE_BPS / 100}%`);
    }
    setFeeBps(bps);
    setFeeAddresses(addresses);
    toast.success(bps === 0 ? "Saved — fee is off (0%)" : `Saved — ${bps / 100}% platform fee`);
  };

  if (!unlocked) {
    return (
      <AppShell>
        <header className="px-5 pt-6 pb-4">
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Lock className="h-5 w-5" /> Developer
          </h1>
          <p className="text-sm text-muted-foreground">
            {needsPin ? "Enter the admin PIN." : "Set an admin PIN to protect this panel."}
          </p>
        </header>
        <section className="px-5 space-y-3 max-w-sm">
          <input
            type="password"
            inputMode="numeric"
            value={pin}
            onChange={(e) => setPin(e.target.value)}
            placeholder="Admin PIN"
            className="w-full bg-secondary rounded-xl px-3 py-2.5 text-sm outline-none"
          />
          {!needsPin && (
            <input
              type="password"
              inputMode="numeric"
              value={pin2}
              onChange={(e) => setPin2(e.target.value)}
              placeholder="Confirm PIN"
              className="w-full bg-secondary rounded-xl px-3 py-2.5 text-sm outline-none"
            />
          )}
          <button
            onClick={() => void unlock()}
            className="w-full py-3 rounded-xl bg-primary text-primary-foreground text-sm font-semibold"
          >
            {needsPin ? "Unlock" : "Set PIN & open"}
          </button>
        </section>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <header className="px-5 pt-6 pb-4">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <ShieldCheck className="h-5 w-5" /> Platform fee
        </h1>
        <p className="text-sm text-muted-foreground">
          Fee percentage and the address that receives it, per chain. Works with no server — this
          config lives on the device. The fee is always shown to the user before they confirm.
        </p>
      </header>

      <section className="px-5 space-y-5 max-w-lg pb-10">
        <div className="rounded-2xl bg-card p-4 space-y-3">
          <label className="text-xs uppercase tracking-wide text-muted-foreground">
            Fee percent · 0 turns it off · max {MAX_FEE_BPS / 100}%
          </label>
          <input
            value={percent}
            onChange={(e) => setPercent(e.target.value)}
            placeholder="0.5"
            inputMode="decimal"
            className="w-32 bg-secondary rounded-xl px-3 py-2.5 text-sm outline-none"
          />
          <p className="text-xs text-muted-foreground">
            On the web this drives the disclosed spread on the Exchange quote. On-chain collection
            happens in the desktop app.
          </p>
        </div>

        <div className="rounded-2xl bg-card p-4 space-y-3">
          <label className="text-xs uppercase tracking-wide text-muted-foreground">
            Receiving addresses
          </label>
          {FEE_CHAINS.map((chain) => (
            <div key={chain} className="space-y-1">
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <span className="font-medium text-foreground">{chain}</span>
                {!ROUTED.has(chain) && (
                  <span className="text-[10px] px-1.5 py-0.5 rounded bg-secondary">
                    routing pending on web
                  </span>
                )}
              </div>
              <input
                value={addresses[chain] ?? ""}
                onChange={(e) => setAddresses((a) => ({ ...a, [chain]: e.target.value }))}
                placeholder={PLACEHOLDER[chain] ?? "address"}
                className="w-full bg-secondary rounded-xl px-3 py-2 text-xs font-mono outline-none"
              />
            </div>
          ))}
        </div>

        <button
          onClick={save}
          className="w-full py-3 rounded-2xl font-semibold text-primary-foreground"
          style={{ background: "var(--gradient-primary)" }}
        >
          Save fee settings
        </button>

        <p className="text-[11px] text-muted-foreground leading-relaxed">
          Note: on a device-local wallet these values can be changed by whoever holds the device.
          To ship one fixed fee/address to every visitor, bake it into the backend
          (PLATFORM_SPREAD_BPS) and the client defaults before deploying.
        </p>
      </section>
    </AppShell>
  );
}
