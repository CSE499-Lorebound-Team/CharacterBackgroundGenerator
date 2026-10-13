"use client";

import { Filter } from "lucide-react";

import { BuilderOptionCard } from "@/components/builder/BuilderOptionCard";
import { EmptyState, ErrorState, LoadingCards } from "@/components/layout/PageStates";
import type { BuilderStepDto, StepOptionsDto } from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";

type EntryChoiceStepProps = {
  characterId: string;
  step: BuilderStepDto;
  selectedIds: string[];
  onChange: (entryIds: string[]) => void;
};

/** The step's options from the API, narrowed by earlier choices (ADR 0002). */
export function EntryChoiceStep({ characterId, step, selectedIds, onChange }: EntryChoiceStepProps) {
  const options = useApiData<StepOptionsDto>(
    `/api/characters/${characterId}/steps/${step.key}/options`
  );

  if (options.error) {
    return <ErrorState error={options.error} onRetry={options.reload} />;
  }

  // Keep the old options visible only while the same step reloads.
  if (!options.data || options.data.stepKey !== step.key) {
    return <LoadingCards count={4} />;
  }

  if (options.data.options.length === 0) {
    return (
      <EmptyState
        title="No options yet"
        description={
          step.required
            ? "This setting has no entries for this step yet. Ask your Game Master to add some."
            : "This setting has no entries for this step yet. It is optional, so you can continue."
        }
      />
    );
  }

  function toggle(entryId: string) {
    if (selectedIds.includes(entryId)) {
      // Clicking the chosen option again clears it on optional steps.
      if (!step.required) onChange(selectedIds.filter((id) => id !== entryId));
      return;
    }

    onChange(
      step.maxSelections === 1 ? [entryId] : [...selectedIds, entryId].slice(-step.maxSelections)
    );
  }

  return (
    <div className="space-y-4">
      {options.data.narrowed && (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <Filter className="h-4 w-4" />
          Narrowed by your earlier choices: only options connected to them are shown.
        </p>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        {options.data.options.map((option) => (
          <BuilderOptionCard
            key={option.entryId}
            title={option.name}
            description={option.description ?? undefined}
            selected={selectedIds.includes(option.entryId)}
            onSelect={() => toggle(option.entryId)}
          />
        ))}
      </div>
    </div>
  );
}
