"use client";

import { use, useState } from "react";
import Link from "next/link";
import { ArrowLeft, ArrowRight } from "lucide-react";

import { BuilderProgress } from "@/components/builder/BuilderProgress";
import { BuilderTextStep } from "@/components/builder/BuilderTextStep";
import { CharacterSummaryPanel } from "@/components/builder/CharacterSummaryPanel";
import { EntryChoiceStep } from "@/components/builder/EntryChoiceStep";
import { ChoiceSummary } from "@/components/characters/ChoiceSummary";
import { AppShell } from "@/components/layout/AppShell";
import { EmptyState, ErrorState, LoadingCards } from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { choiceRows, useBuilderSteps } from "@/lib/builder-steps";
import type { BuilderStepDto, CharacterDetailDto } from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";

export default function BuilderPage({ params }: PageProps<"/builder/[characterId]">) {
  const { characterId } = use(params);

  const steps = useBuilderSteps();
  const character = useApiData<CharacterDetailDto>(`/api/characters/${characterId}`);

  if (character.error instanceof ApiError && character.error.status === 404) {
    return (
      <AppShell>
        <EmptyState
          title="Character not found"
          description="This character does not exist, or you cannot see it."
          action={
            <Button variant="outline" nativeButton={false} render={<Link href="/characters" />}>
              Back to Characters
            </Button>
          }
        />
      </AppShell>
    );
  }

  const error = character.error ?? steps.error;

  return (
    <AppShell>
      {error ? (
        <ErrorState
          error={error}
          onRetry={() => {
            character.reload();
            steps.reload();
          }}
        />
      ) : !character.data || !steps.data ? (
        <LoadingCards count={4} />
      ) : (
        <Builder
          key={character.data.id}
          steps={steps.data.steps}
          initial={character.data}
        />
      )}
    </AppShell>
  );
}

/** The answer being edited for each step, before it is saved. */
type Drafts = Record<string, { entryIds: string[]; freeText: string }>;

function draftsFrom(character: CharacterDetailDto): Drafts {
  const drafts: Drafts = {};

  for (const choice of [...character.choices].sort((a, b) => a.ordinal - b.ordinal)) {
    const draft = (drafts[choice.stepKey] ??= { entryIds: [], freeText: "" });
    if (choice.entryId) draft.entryIds.push(choice.entryId);
    if (choice.freeText) draft.freeText = choice.freeText;
  }

  return drafts;
}

type BuilderProps = {
  steps: BuilderStepDto[];
  initial: CharacterDetailDto;
};

function Builder({ steps, initial }: BuilderProps) {
  const [character] = useState(initial);
  const [drafts, setDrafts] = useState<Drafts>(() => draftsFrom(initial));
  const [index, setIndex] = useState(0);

  const step = steps[index];
  const draft = drafts[step.key] ?? { entryIds: [], freeText: "" };
  const rows = choiceRows(steps, character);

  function updateDraft(change: Partial<Drafts[string]>) {
    setDrafts((current) => ({
      ...current,
      [step.key]: { ...draft, ...change },
    }));
  }

  // A required step needs an answer before moving on.
  const answered = step.allowFreeText
    ? draft.freeText.trim().length > 0
    : draft.entryIds.length > 0;
  const canContinue = !step.required || step.maxSelections === 0 || answered;

  function goTo(key: string) {
    const target = steps.findIndex((candidate) => candidate.key === key);
    if (target >= 0) setIndex(target);
  }

  return (
    <div className="mx-auto max-w-7xl space-y-8">
      <div>
        <p className="text-sm text-muted-foreground">{character.settingName}</p>
        <h1 className="font-heading text-3xl font-bold tracking-tight">{character.name}</h1>
      </div>

      <BuilderProgress position={index + 1} totalSteps={steps.length} stepTitle={step.title} />

      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_320px]">
        <section className="space-y-6">
          <p className="text-muted-foreground">{step.description}</p>

          {step.key === "setting" && (
            <div className="border border-border bg-card p-5">
              <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                Setting
              </p>
              <p className="mt-1 text-lg font-medium">{character.settingName}</p>
              <p className="mt-2 text-sm text-muted-foreground">
                Every answer comes from this setting&apos;s lore.
              </p>
            </div>
          )}

          {step.entryType && (
            <EntryChoiceStep
              key={step.key}
              characterId={character.id}
              step={step}
              selectedIds={draft.entryIds}
              onChange={(entryIds) => updateDraft({ entryIds })}
            />
          )}

          {step.allowFreeText && (
            <BuilderTextStep
              id={`step-${step.key}`}
              label={step.title}
              value={draft.freeText}
              onChange={(freeText) => updateDraft({ freeText })}
              placeholder="What does your character want, fear, protect, or hope to accomplish?"
              maxLength={step.maxFreeTextLength ?? 2000}
            />
          )}

          {step.key === "review" && <ChoiceSummary rows={rows} columns />}

          <div className="flex flex-col-reverse gap-2 border-t border-border pt-6 sm:flex-row sm:justify-between">
            <Button
              variant="outline"
              disabled={index === 0}
              onClick={() => setIndex(index - 1)}
            >
              <ArrowLeft className="h-4 w-4" />
              Back
            </Button>

            {index < steps.length - 1 && (
              <Button disabled={!canContinue} onClick={() => setIndex(index + 1)}>
                Next
                <ArrowRight className="h-4 w-4" />
              </Button>
            )}
          </div>
        </section>

        <CharacterSummaryPanel rows={rows} activeKey={step.key} onSelect={goTo} />
      </div>
    </div>
  );
}
