import type { CharacterStatus } from "@/lib/types";
import { cn } from "@/lib/utils";

export function StatusBadge({ status }: { status: CharacterStatus }) {
  return (
    <span
      className={cn(
        "rounded-full px-2 py-1 text-xs font-medium",
        status === "Complete"
          ? "bg-primary/10 text-primary"
          : "bg-muted text-muted-foreground"
      )}
    >
      {status === "Draft" ? "Draft" : "Complete"}
    </span>
  );
}

/** The owner left or was removed from the setting; the API refuses edits. */
export function ReadOnlyBadge() {
  return (
    <span className="rounded-full bg-destructive/10 px-2 py-1 text-xs font-medium text-destructive">
      Read-only
    </span>
  );
}
