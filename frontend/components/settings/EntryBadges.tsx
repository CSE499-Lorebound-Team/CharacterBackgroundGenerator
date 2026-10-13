import { entryTypeLabel } from "@/lib/entry-types";
import type { SettingEntryType } from "@/lib/types";

export function TypeBadge({ type }: { type: SettingEntryType }) {
  return (
    <span className="rounded-full bg-muted px-2 py-1 text-xs font-medium text-muted-foreground">
      {entryTypeLabel(type)}
    </span>
  );
}

/** Shown to GameMasters on entries hidden from Players. */
export function GmOnlyBadge() {
  return (
    <span className="rounded-full bg-destructive/10 px-2 py-1 text-xs font-medium text-destructive">
      GM Only
    </span>
  );
}
