import { AlertTriangle } from "lucide-react";

import type { ChoiceRow } from "@/lib/builder-steps";
import { cn } from "@/lib/utils";

type CharacterSummaryPanelProps = {
  rows: ChoiceRow[];
  /** The open step's key, highlighted. */
  activeKey?: string;
  /** Jumps to a step; omit for a read-only summary. */
  onSelect?: (key: string) => void;
};

/** The character's answers so far, one row per catalog step. */
export function CharacterSummaryPanel({ rows, activeKey, onSelect }: CharacterSummaryPanelProps) {
  return (
    <aside className="border border-border bg-card p-5">
      <h2 className="font-heading text-lg font-semibold">Character Summary</h2>

      <ul className="mt-4 space-y-1">
        {rows.map((row) => {
          const content = (
            <>
              <span className="flex items-center gap-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {row.label}
                {row.stale && <AlertTriangle className="h-3 w-3 text-destructive" aria-label="needs attention" />}
              </span>
              <span className="mt-0.5 block truncate text-sm">
                {row.removed
                  ? "Removed from the setting"
                  : row.value ?? <span className="italic text-muted-foreground">Not chosen</span>}
              </span>
            </>
          );

          return (
            <li key={row.key}>
              {onSelect ? (
                <button
                  type="button"
                  onClick={() => onSelect(row.key)}
                  aria-current={row.key === activeKey ? "step" : undefined}
                  className={cn(
                    "w-full px-2 py-1.5 text-left transition-colors hover:bg-muted",
                    row.key === activeKey && "bg-primary/10"
                  )}
                >
                  {content}
                </button>
              ) : (
                <div className="px-2 py-1.5">{content}</div>
              )}
            </li>
          );
        })}
      </ul>
    </aside>
  );
}
