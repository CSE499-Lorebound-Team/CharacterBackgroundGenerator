"use client";

import { BuilderOptionCard } from "@/components/builder/BuilderOptionCard";

import type { SettingEntry } from "@/lib/settings/types";

type EntryChoiceStepProps = {
  options: SettingEntry[];
  selectedId?: string;
  onSelect: (entryId: string) => void;
};

export function EntryChoiceStep({
  options,
  selectedId,
  onSelect,
}: EntryChoiceStepProps) {
  if (options.length === 0) {
    return (
      <div className="border border-dashed border-border p-8 text-center">
        <p className="font-medium">
          No options available
        </p>

        <p className="mt-2 text-sm text-muted-foreground">
          This campaign setting does
          not have any available lore
          entries for this step yet.
        </p>
      </div>
    );
  }

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {options.map((entry) => (
        <BuilderOptionCard
          key={entry.id}
          title={entry.name}
          description={entry.summary}
          selected={
            selectedId === entry.id
          }
          onSelect={() =>
            onSelect(entry.id)
          }
        />
      ))}
    </div>
  );
}