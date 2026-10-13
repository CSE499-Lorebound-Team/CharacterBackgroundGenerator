import { AlertTriangle } from "lucide-react";

import type { ChoiceRow } from "@/lib/builder-steps";
import { cn } from "@/lib/utils";

type ChoiceSummaryProps = {
  rows: ChoiceRow[];
  /** Lays rows out in two columns, with free text spanning both. */
  columns?: boolean;
};

/** The character's answers, one row per builder step. */
export function ChoiceSummary({ rows, columns = false }: ChoiceSummaryProps) {
  return (
    <dl className={cn(columns ? "grid gap-5 sm:grid-cols-2" : "space-y-4")}>
      {rows.map((row) => (
        <div
          key={row.key}
          className={cn(columns && row.key === "motivation" && "sm:col-span-2")}
        >
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {row.label}
            {!row.required && " (optional)"}
          </dt>
          <dd className="mt-1 whitespace-pre-wrap text-sm">
            {row.removed ? (
              <span className="italic text-muted-foreground">
                Removed from the setting
              </span>
            ) : (
              row.value ?? (
                <span className="italic text-muted-foreground">Not chosen</span>
              )
            )}
            {row.stale && (
              <span className="mt-1 flex items-center gap-1 text-xs text-destructive">
                <AlertTriangle className="h-3 w-3" />
                No longer fits earlier choices
              </span>
            )}
          </dd>
        </div>
      ))}
    </dl>
  );
}
