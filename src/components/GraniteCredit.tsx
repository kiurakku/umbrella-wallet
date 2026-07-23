type Props = {
  compact?: boolean;
  className?: string;
};

/** Attribution badge — "the fear". Theme-aligned (monochrome + teal). */
export function GraniteCredit({ compact, className = "" }: Props) {
  if (compact) {
    return (
      <div
        className={`flex items-center justify-center gap-1.5 text-[11px] text-muted-foreground ${className}`}
      >
        <img src="/thefear-logo.png" alt="" aria-hidden className="h-4 w-4 rounded-[5px]" />
        <span>
          by <span className="text-foreground/80 font-medium">the fear</span>
        </span>
      </div>
    );
  }

  return (
    <div
      className={`flex items-center gap-3 rounded-xl border border-border/60 bg-secondary/30 px-3.5 py-2.5 ${className}`}
    >
      <img
        src="/thefear-logo.png"
        alt="the fear"
        className="h-8 w-8 rounded-lg ring-1 ring-border/60"
      />
      <div className="min-w-0 leading-tight">
        <p className="text-[13px] font-medium text-foreground">
          Built by <span className="text-primary">the fear</span>
        </p>
        <p className="text-[10.5px] uppercase tracking-[0.12em] text-muted-foreground mt-0.5">
          Privacy · Self-custody · No middlemen
        </p>
      </div>
    </div>
  );
}
