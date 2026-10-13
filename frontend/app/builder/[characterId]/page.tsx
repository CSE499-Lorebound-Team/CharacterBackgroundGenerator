"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AlertTriangle, ArrowLeft, ArrowRight, Check, Lock } from "lucide-react";

import { BuilderProgress } from "@/components/builder/BuilderProgress";
import { BuilderTextStep } from "@/components/builder/BuilderTextStep";
import { CharacterSummaryPanel } from "@/components/builder/CharacterSummaryPanel";
import { EntryChoiceStep } from "@/components/builder/EntryChoiceStep";
import { ChoiceSummary } from "@/components/characters/ChoiceSummary";
import { BackstoryField } from "@/components/characters/EditCharacterDialog";
import { FormAlert } from "@/components/forms/FormAlert";
import { AppShell } from "@/components/layout/AppShell";
import { useToast } from "@/components/layout/Toaster";
import {
  EmptyState,
  ErrorState,
  errorMessage,
  LoadingCards,
} from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { choiceRows, stepLabel, useBuilderSteps } from "@/lib/builder-steps";
import type {
  BuilderStepDto,
  CharacterDetailDto,
  SaveChoiceRequest,
  UpdateCharacterRequest,
} from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";

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
        <Builder key={character.data.id} steps={steps.data.steps} initial={character.data} />
      )}
    </AppShell>
  );
}

/** The answer being edited for each step, before it is saved. */
type Draft = { entryIds: string[]; freeText: string };
type Drafts = Record<string, Draft>;

const emptyDraft: Draft = { entryIds: [], freeText: "" };

function draftsFrom(character: CharacterDetailDto): Drafts {
  const drafts: Drafts = {};

  for (const choice of [...character.choices].sort((a, b) => a.ordinal - b.ordinal)) {
    const draft = (drafts[choice.stepKey] ??= { entryIds: [], freeText: "" });
    if (choice.entryId) draft.entryIds.push(choice.entryId);
    if (choice.freeText) draft.freeText = choice.freeText;
  }

  return drafts;
}

function sameDraft(a: Draft, b: Draft): boolean {
  return (
    a.freeText.trim() === b.freeText.trim() &&
    a.entryIds.length === b.entryIds.length &&
    a.entryIds.every((id, i) => id === b.entryIds[i])
  );
}

/** Resume a draft at its currentStep (the order of the first unsaved step). */
function startIndex(steps: BuilderStepDto[], character: CharacterDetailDto): number {
  const index = steps.findIndex((step) => step.order === character.currentStep);
  return index >= 0 ? index : steps.length - 1;
}

/** Messages from a refused save or completion, keyed by step or field. */
function problemLines(error: unknown): string[] {
  if (error instanceof ApiError && error.errors) {
    return Object.entries(error.errors).map(([key, messages]) => {
      const label = key && !/^(EntryIds|FreeText|stepKey)/i.test(key) ? `${stepLabel(key)}: ` : "";
      return label + messages.join(" ");
    });
  }
  return [errorMessage(error)];
}

type BuilderProps = {
  steps: BuilderStepDto[];
  initial: CharacterDetailDto;
};

function Builder({ steps, initial }: BuilderProps) {
  const [character, setCharacter] = useState(initial);
  const editable = character.isOwner && !character.isReadOnly;

  if (!editable) {
    return <ViewOnly character={character} steps={steps} />;
  }

  if (character.status === "Complete") {
    return <Completed character={character} steps={steps} onReopened={setCharacter} />;
  }

  return <Wizard steps={steps} character={character} onSaved={setCharacter} />;
}

type WizardProps = {
  steps: BuilderStepDto[];
  character: CharacterDetailDto;
  onSaved: (character: CharacterDetailDto) => void;
};

function Wizard({ steps, character, onSaved }: WizardProps) {
  const router = useRouter();
  const fetchApi = useApiFetch();
  const toast = useToast();

  const [drafts, setDrafts] = useState<Drafts>(() => draftsFrom(character));
  const [index, setIndex] = useState(() => startIndex(steps, character));
  const [problems, setProblems] = useState<string[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [backstory, setBackstory] = useState(character.backstory ?? "");
  const [backstoryError, setBackstoryError] = useState<string>();

  const step = steps[index];
  const draft = drafts[step.key] ?? emptyDraft;
  const saved = draftsFrom(character)[step.key] ?? emptyDraft;
  const rows = choiceRows(steps, character);
  const isStale = character.staleSteps.includes(step.key);

  function updateDraft(change: Partial<Draft>) {
    setDrafts((current) => ({ ...current, [step.key]: { ...draft, ...change } }));
  }

  const answered = step.allowFreeText
    ? draft.freeText.trim().length > 0
    : draft.entryIds.length > 0;
  const canContinue = !step.required || step.maxSelections === 0 || answered;

  /** Saves the open step if its answer changed; false if the API refused. */
  async function saveStep(): Promise<boolean> {
    if (step.maxSelections === 0 || sameDraft(draft, saved)) return true;

    setIsSaving(true);
    setProblems([]);

    try {
      const request: SaveChoiceRequest = step.allowFreeText
        ? { entryIds: null, freeText: draft.freeText.trim() || null }
        : { entryIds: draft.entryIds, freeText: null };

      // Returns the character with currentStep and staleSteps updated.
      const updated = await fetchApi<CharacterDetailDto>(
        `/api/characters/${character.id}/choices/${step.key}`,
        { method: "PUT", json: request }
      );
      onSaved(updated);
      return true;
    } catch (error) {
      setProblems(problemLines(error));
      return false;
    } finally {
      setIsSaving(false);
    }
  }

  async function goTo(target: number) {
    if (target === index) return;
    if (await saveStep()) {
      setProblems([]);
      setIndex(target);
    }
  }

  function goToKey(key: string) {
    const target = steps.findIndex((candidate) => candidate.key === key);
    if (target >= 0) void goTo(target);
  }

  async function saveBackstory() {
    setBackstoryError(undefined);
    try {
      const updated = await fetchApi<CharacterDetailDto>(`/api/characters/${character.id}`, {
        method: "PUT",
        json: {
          name: character.name,
          backstory: backstory.trim() || null,
        } satisfies UpdateCharacterRequest,
      });
      onSaved(updated);
      toast("Backstory saved");
    } catch (error) {
      setBackstoryError(problemLines(error).join(" "));
    }
  }

  async function complete() {
    setIsSaving(true);
    setProblems([]);

    try {
      // Keep an unsaved backstory edit too; it is not part of completion.
      if ((character.backstory ?? "") !== backstory.trim()) await saveBackstory();

      await fetchApi<CharacterDetailDto>(`/api/characters/${character.id}/complete`, {
        method: "POST",
      });
      toast(`${character.name} is complete`);
      router.push(`/characters/${character.id}`);
    } catch (error) {
      // 400: one error per failing step, keyed by step key (and Name).
      setProblems(problemLines(error));
      setIsSaving(false);
    }
  }

  return (
    <div className="mx-auto max-w-7xl space-y-8">
      <div>
        <p className="text-sm text-muted-foreground">{character.settingName}</p>
        <h1 className="font-heading text-3xl font-bold tracking-tight">{character.name}</h1>
      </div>

      <BuilderProgress position={index + 1} totalSteps={steps.length} stepTitle={step.title} />

      {character.staleSteps.length > 0 && (
        <FormAlert>
          <p className="flex items-center gap-2 font-medium">
            <AlertTriangle className="h-4 w-4" />
            Some answers no longer fit your earlier choices.
          </p>
          <div className="mt-2 flex flex-wrap gap-2">
            {character.staleSteps.map((key) => (
              <Button key={key} size="sm" variant="outline" onClick={() => goToKey(key)}>
                Fix {stepLabel(key)}
              </Button>
            ))}
          </div>
        </FormAlert>
      )}

      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_320px]">
        <section className="space-y-6">
          <p className="text-muted-foreground">{step.description}</p>

          {isStale && (
            <FormAlert>
              Your earlier choices changed, and this answer no longer fits them.
              Choose again.
            </FormAlert>
          )}

          {problems.length > 0 && (
            <FormAlert>
              <ul className="list-disc space-y-1 pl-4">
                {problems.map((problem) => (
                  <li key={problem}>{problem}</li>
                ))}
              </ul>
            </FormAlert>
          )}

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
              // Remount per step and per save, so options reflect the
              // latest earlier choices.
              key={`${step.key}:${character.updatedAt}`}
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

          {step.key === "review" && (
            <div className="space-y-6">
              <ChoiceSummary rows={rows} columns />

              <div className="space-y-2 border-t border-border pt-6">
                <BackstoryField value={backstory} onChange={setBackstory} />
                <div className="flex items-center gap-3">
                  <Button variant="outline" size="sm" onClick={saveBackstory}>
                    Save Backstory
                  </Button>
                  {backstoryError && (
                    <span className="text-sm text-destructive">{backstoryError}</span>
                  )}
                </div>
              </div>
            </div>
          )}

          <div className="flex flex-col-reverse gap-2 border-t border-border pt-6 sm:flex-row sm:justify-between">
            <Button
              variant="outline"
              disabled={index === 0 || isSaving}
              onClick={() => goTo(index - 1)}
            >
              <ArrowLeft className="h-4 w-4" />
              Back
            </Button>

            {step.key === "review" ? (
              <Button disabled={isSaving} onClick={complete}>
                <Check className="h-4 w-4" />
                {isSaving ? "Completing..." : "Complete Character"}
              </Button>
            ) : (
              <Button disabled={!canContinue || isSaving} onClick={() => goTo(index + 1)}>
                {isSaving ? "Saving..." : "Next"}
                <ArrowRight className="h-4 w-4" />
              </Button>
            )}
          </div>
        </section>

        <CharacterSummaryPanel rows={rows} activeKey={step.key} onSelect={goToKey} />
      </div>
    </div>
  );
}

type CompletedProps = {
  character: CharacterDetailDto;
  steps: BuilderStepDto[];
  onReopened: (character: CharacterDetailDto) => void;
};

/** A complete character's answers are locked until it is reopened. */
function Completed({ character, steps, onReopened }: CompletedProps) {
  const fetchApi = useApiFetch();
  const toast = useToast();
  const [error, setError] = useState<string>();
  const [isReopening, setIsReopening] = useState(false);

  async function reopen() {
    setError(undefined);
    setIsReopening(true);
    try {
      onReopened(
        await fetchApi<CharacterDetailDto>(`/api/characters/${character.id}/reopen`, {
          method: "POST",
        })
      );
      toast("Reopened as a draft");
    } catch (caught) {
      setError(errorMessage(caught));
      setIsReopening(false);
    }
  }

  return (
    <SummaryLayout
      character={character}
      steps={steps}
      notice={
        <>
          <p className="flex items-center gap-2 font-medium">
            <Check className="h-4 w-4" />
            This character is complete.
          </p>
          <p className="mt-1">Reopen it as a draft to change its answers.</p>
          {error && <p className="mt-1 text-destructive">{error}</p>}
          <Button className="mt-3" size="sm" disabled={isReopening} onClick={reopen}>
            {isReopening ? "Reopening..." : "Reopen to Edit"}
          </Button>
        </>
      }
    />
  );
}

/** Read-only (owner removed from the setting) or someone else's character. */
function ViewOnly({ character, steps }: { character: CharacterDetailDto; steps: BuilderStepDto[] }) {
  return (
    <SummaryLayout
      character={character}
      steps={steps}
      notice={
        <p className="flex items-center gap-2">
          <Lock className="h-4 w-4" />
          {character.isOwner
            ? `You are no longer a member of ${character.settingName}, so this character is read-only.`
            : `This is ${character.ownerDisplayName}'s character. You can view it, but only they can edit it.`}
        </p>
      }
    />
  );
}

type SummaryLayoutProps = {
  character: CharacterDetailDto;
  steps: BuilderStepDto[];
  notice: React.ReactNode;
};

function SummaryLayout({ character, steps, notice }: SummaryLayoutProps) {
  return (
    <div className="mx-auto max-w-5xl space-y-8">
      <div>
        <p className="text-sm text-muted-foreground">{character.settingName}</p>
        <h1 className="font-heading text-3xl font-bold tracking-tight">{character.name}</h1>
      </div>

      <FormAlert variant="success">{notice}</FormAlert>

      <section className="border border-border p-6">
        <ChoiceSummary rows={choiceRows(steps, character)} columns />
      </section>

      <Button variant="outline" nativeButton={false} render={<Link href={`/characters/${character.id}`} />}>
        View Character Page
      </Button>
    </div>
  );
}
