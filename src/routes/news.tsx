import { createFileRoute } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { AppShell } from "@/components/AppShell";
import { useT } from "@/lib/i18n";

export const Route = createFileRoute("/news")({
  component: NewsPage,
});

type NewsItem = {
  id: string;
  tag: "update" | "market" | "security" | "guide";
  title: string;
  body: string;
  date: string; // ISO
  link?: string;
};

/**
 * Product and market news. When a `/news` backend endpoint exists this hook reads it; until then it
 * falls back to a curated, static feed so the section is never empty. Everything is public content —
 * no wallet data is involved.
 */
function useNews() {
  return useQuery<NewsItem[]>({
    queryKey: ["news"],
    queryFn: async () => {
      try {
        const base = import.meta.env.VITE_API_URL as string | undefined;
        if (base) {
          const res = await fetch(`${base.replace(/\/$/, "")}/news`, {
            headers: { accept: "application/json" },
          });
          if (res.ok) {
            const data = (await res.json()) as NewsItem[];
            if (Array.isArray(data) && data.length) return data;
          }
        }
      } catch {
        // fall through to the curated feed
      }
      return FALLBACK_NEWS;
    },
    staleTime: 5 * 60_000,
  });
}

const FALLBACK_NEWS: NewsItem[] = [
  {
    id: "spread",
    tag: "update",
    title: "Exchange now shows a transparent 0.5% service fee",
    body: "Currency conversions include a small, clearly-labelled spread — always shown before you confirm. No separate on-chain fee, so your network cost is unchanged.",
    date: "2026-07-24",
  },
  {
    id: "tor",
    tag: "security",
    title: "Tor is built into the desktop wallet",
    body: "One switch routes all wallet traffic through the Tor network — no separate install. Your IP stays out of your finances.",
    date: "2026-07-20",
  },
  {
    id: "monero",
    tag: "update",
    title: "Full Monero wallet on desktop",
    body: "XMR is a first-class coin: real private balance and sending, powered by Monero's own engine running locally. Keys never leave your device.",
    date: "2026-07-18",
  },
  {
    id: "trc20",
    tag: "guide",
    title: "Why USDT (TRC-20) transfers cost several dollars",
    body: "That fee is TRON's energy cost, not ours. A fresh, unstaked TRON account burns TRX for every USDT transfer. Stake TRX for energy, or use USDT on a cheaper network.",
    date: "2026-07-15",
  },
];

const TAG_STYLE: Record<NewsItem["tag"], string> = {
  update: "bg-primary/15 text-primary",
  market: "bg-emerald-500/15 text-emerald-400",
  security: "bg-amber-500/15 text-amber-400",
  guide: "bg-sky-500/15 text-sky-400",
};

function NewsPage() {
  const t = useT();
  const { data, isLoading } = useNews();

  return (
    <AppShell>
      <section className="max-w-2xl mx-auto w-full">
        <div className="mb-4">
          <h1 className="text-2xl font-bold">{t.news}</h1>
          <p className="text-sm text-muted-foreground">Product updates, market notes and guides</p>
        </div>

        {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}

        <div className="space-y-3">
          {(data ?? []).map((item) => (
            <article
              key={item.id}
              className="rounded-2xl border border-border/60 bg-card p-4"
            >
              <div className="flex items-center gap-2 mb-2">
                <span
                  className={`text-[10px] uppercase tracking-wide font-semibold px-2 py-0.5 rounded-full ${TAG_STYLE[item.tag]}`}
                >
                  {item.tag}
                </span>
                <time className="text-[11px] text-muted-foreground">
                  {new Date(item.date).toLocaleDateString("en-US", {
                    year: "numeric",
                    month: "short",
                    day: "numeric",
                  })}
                </time>
              </div>
              <h2 className="font-semibold text-[15px]">{item.title}</h2>
              <p className="text-sm text-muted-foreground mt-1">{item.body}</p>
              {item.link && (
                <a
                  href={item.link}
                  target="_blank"
                  rel="noreferrer"
                  className="text-primary text-xs mt-2 inline-block"
                >
                  Read more →
                </a>
              )}
            </article>
          ))}
        </div>
      </section>
    </AppShell>
  );
}
