"use client";

import {
  use,
  useMemo,
  useState,
} from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { AppShell } from "@/components/layout/AppShell";
import { BuilderProgress } from "@/components/builder/BuilderProgress";
import { BuilderReview } from "@/components/builder/BuilderReview";
import { BuilderTextStep } from "@/components/builder/BuilderTextStep";
import { CharacterSummaryPanel } from "@/components/builder/CharacterSummaryPanel";
import { EntryChoiceStep } from "@/components/builder/EntryChoiceStep";
import { Button } from "@/components/ui/button";

import {
  getBuilderState,
  saveBuilderState,
} from "@/lib/builder/builder-store";
import {
  builderSteps,
  getBuilderStep,
} from "@/lib/builder/steps";
import {
  getCharacter,
  updateCharacter,
  updateCharacterProgress,
} from "@/lib/characters/characters-store";
import {
  getEntriesForSetting,
} from "@/lib/settings/entries-store";
import {
  getSetting,
} from "@/lib/settings/settings-store";
import { useIsClient } from "@/lib/use-is-client";

import type {
  BuilderStepKey,
  CharacterBuilderState,
} from "@/lib/builder/types";
import type {
  Character,
} from "@/lib/characters/types";
import type {
  SettingEntry,
} from "@/lib/settings/types";


type BuilderPageProps = {
  params: Promise<{
    characterId: string;
  }>;
};

type EntryChoiceKey =
  | "homelandEntryId"
  | "cultureEntryId"
  | "religionEntryId"
  | "factionEntryId";

export default function BuilderPage({
  params,
}: BuilderPageProps) {
  const { characterId } =
    use(params);

  const isClient =
    useIsClient();

  return (
    <BuilderContent
      key={`${isClient ? "client" : "server"}:${characterId}`}
      characterId={characterId}
      loaded={isClient}
    />
  );
}

function BuilderContent({
  characterId,
  loaded,
}: {
  characterId: string;
  loaded: boolean;
}) {
  const router =
    useRouter();

  const [
    character,
    setCharacter,
  ] =
    useState<Character | undefined>(
      () =>
        loaded
          ? getCharacter(
              characterId
            )
          : undefined
    );

  const [
    builderState,
    setBuilderState,
  ] =
    useState<
      CharacterBuilderState | undefined
    >(() =>
      loaded
        ? getBuilderState(
            characterId
          )
        : undefined
    );

  const setting =
    useMemo(
      () =>
        loaded &&
        character
          ? getSetting(
              character.settingId
            )
          : undefined,
      [
        loaded,
        character,
      ]
    );

  const entries =
    useMemo<SettingEntry[]>(
      () => {
        if (
          !loaded ||
          !character
        ) {
          return [];
        }

        const allEntries =
          getEntriesForSetting(
            character.settingId
          );

        if (
          setting?.role === "Player"
        ) {
          return allEntries.filter(
            (entry) =>
              !entry.isGmOnly
          );
        }

        return allEntries;
      },
      [
        loaded,
        character,
        setting,
      ]
    );

  if (!loaded) {
    return (
      <AppShell>
        <div className="py-16 text-center text-muted-foreground">
          Loading builder...
        </div>
      </AppShell>
    );
  }

  if (
    !character ||
    !builderState
  ) {
    return (
      <AppShell>
        <div className="py-16 text-center">
          <h1 className="text-2xl font-semibold">
            Character not found
          </h1>

          <p className="mt-2 text-muted-foreground">
            This character could not
            be loaded.
          </p>

          <Button
            className="mt-6"
            variant="outline"
            nativeButton={false}
            render={
              <Link href="/characters" />
            }
          >
            Back to Characters
          </Button>
        </div>
      </AppShell>
    );
  }

  const currentCharacter =
    character;

  const currentBuilderState =
    builderState;

  const step =
    getBuilderStep(
      currentBuilderState.currentStep
    );

  if (!step) {
    return null;
  }

  const entryChoiceKey =
    getEntryChoiceKey(
      step.key
    );

  const entryOptions =
    step.entryTypes
      ? entries.filter(
          (entry) =>
            step.entryTypes?.includes(
              entry.type
            )
        )
      : [];

  const selectedEntryId =
    entryChoiceKey
      ? currentBuilderState
          .choices[
          entryChoiceKey
        ]
      : undefined;

  const selectedHomeland =
    findChoiceEntry(
      entries,
      currentBuilderState
        .choices
        .homelandEntryId
    );

  const selectedCulture =
    findChoiceEntry(
      entries,
      currentBuilderState
        .choices
        .cultureEntryId
    );

  const selectedReligion =
    findChoiceEntry(
      entries,
      currentBuilderState
        .choices
        .religionEntryId
    );

  const selectedFaction =
    findChoiceEntry(
      entries,
      currentBuilderState
        .choices
        .factionEntryId
    );

  function persistState(
    state:
      CharacterBuilderState
  ) {
    const saved =
      saveBuilderState(
        state.characterId,
        {
          currentStep:
            state.currentStep,
          choices:
            state.choices,
        }
      );

    setBuilderState(
      saved
    );

    updateCharacterProgress(
      characterId,
      saved.currentStep
    );
  }

  function selectEntry(
    choiceKey:
      EntryChoiceKey,
    entryId: string
  ) {
    persistState({
      ...currentBuilderState,

      choices: {
        ...currentBuilderState
          .choices,

        [choiceKey]:
          entryId,
      },

      updatedAt:
        new Date()
          .toISOString(),
    });
  }

  function updateMotivation(
    motivation: string
  ) {
    persistState({
      ...currentBuilderState,

      choices: {
        ...currentBuilderState
          .choices,

        motivation,
      },

      updatedAt:
        new Date()
          .toISOString(),
    });
  }

  function updateBackstoryChoice(
    backstory: string
  ) {
    persistState({
      ...currentBuilderState,

      choices: {
        ...currentBuilderState
          .choices,

        backstory,
      },

      updatedAt:
        new Date()
          .toISOString(),
    });
  }

  function handleBack() {
    if (
      currentBuilderState
        .currentStep <= 1
    ) {
      return;
    }

    persistState({
      ...currentBuilderState,

      currentStep:
        currentBuilderState
          .currentStep - 1,

      updatedAt:
        new Date()
          .toISOString(),
    });
  }

  function handleNext() {
    if (
      currentBuilderState
        .currentStep >=
      builderSteps.length
    ) {
      return;
    }

    persistState({
      ...currentBuilderState,

      currentStep:
        currentBuilderState
          .currentStep + 1,

      updatedAt:
        new Date()
          .toISOString(),
    });
  }

  function handleComplete() {
    const backstory =
      currentBuilderState
        .choices
        .backstory ?? "";

    const updated =
      updateCharacter(
        currentCharacter.id,
        {
          name:
            currentCharacter.name,

          settingId:
            currentCharacter.settingId,

          settingName:
            currentCharacter.settingName,

          status:
            "Complete",

          backstory,
        }
      );

    if (!updated) {
      return;
    }

    setCharacter(
      updated
    );

    router.push(
      `/characters/${updated.id}`
    );
  }

  const nextDisabled =
    Boolean(
      entryChoiceKey &&
      !step.optional &&
      !selectedEntryId
    ) ||
    (
      step.key ===
        "motivation" &&
      !currentBuilderState
        .choices
        .motivation
        ?.trim()
    );

  const isReview =
    step.key === "review";

  return (
    <AppShell>
      <div className="mx-auto max-w-7xl space-y-8">
        <div>
          <p className="text-sm text-muted-foreground">
            {
              currentCharacter.name
            }
          </p>

          <h1 className="font-heading text-3xl font-bold tracking-tight">
            Character Background
            Builder
          </h1>
        </div>

        <BuilderProgress
          currentStep={
            step.order
          }
          totalSteps={
            builderSteps.length
          }
          stepName={
            step.title
          }
        />

        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_320px]">
          <section className="space-y-6">
            <div>
              <h2 className="font-heading text-2xl font-semibold">
                {
                  step.title
                }
              </h2>

              <p className="mt-1 text-muted-foreground">
                {
                  step.description
                }
              </p>
            </div>

            {step.key ===
              "character" && (
              <CharacterIntro
                character={
                  currentCharacter
                }
              />
            )}

            {entryChoiceKey && (
              <EntryChoiceStep
                options={
                  entryOptions
                }
                selectedId={
                  selectedEntryId
                }
                onSelect={(
                  entryId
                ) =>
                  selectEntry(
                    entryChoiceKey,
                    entryId
                  )
                }
              />
            )}

            {step.key ===
              "motivation" && (
              <BuilderTextStep
                value={
                  currentBuilderState
                    .choices
                    .motivation ?? ""
                }
                onChange={
                  updateMotivation
                }
                placeholder="What does your character want, fear, protect, or hope to accomplish?"
              />
            )}

            {step.key ===
              "backstory" && (
              <BuilderTextStep
                value={
                  currentBuilderState
                    .choices
                    .backstory ?? ""
                }
                onChange={
                  updateBackstoryChoice
                }
                placeholder="Describe the important people, events, losses, victories, and experiences that shaped your character."
              />
            )}

            {step.key ===
              "review" && (
              <BuilderReview
                characterName={
                  currentCharacter.name
                }
                setting={
                  currentCharacter
                    .settingName
                }
                homeland={
                  selectedHomeland?.name
                }
                culture={
                  selectedCulture?.name
                }
                religion={
                  selectedReligion?.name
                }
                faction={
                  selectedFaction?.name
                }
                motivation={
                  currentBuilderState
                    .choices
                    .motivation
                }
                backstory={
                  currentBuilderState
                    .choices
                    .backstory
                }
              />
            )}

            <div className="flex flex-col-reverse gap-2 border-t border-border pt-6 sm:flex-row sm:justify-between">
              <Button
                variant="outline"
                onClick={
                  handleBack
                }
                disabled={
                  currentBuilderState
                    .currentStep <=
                  1
                }
              >
                Back
              </Button>

              {isReview ? (
                <Button
                  onClick={
                    handleComplete
                  }
                >
                  Complete Character
                </Button>
              ) : (
                <Button
                  onClick={
                    handleNext
                  }
                  disabled={
                    nextDisabled
                  }
                >
                  Next
                </Button>
              )}
            </div>
          </section>

          <CharacterSummaryPanel
            setting={
              currentCharacter
                .settingName
            }
            homeland={
              selectedHomeland
                ?.name
            }
            culture={
              selectedCulture
                ?.name
            }
            religion={
              selectedReligion
                ?.name
            }
            faction={
              selectedFaction
                ?.name
            }
            motivation={
              currentBuilderState
                .choices
                .motivation
            }
          />
        </div>
      </div>
    </AppShell>
  );
}

function CharacterIntro({
  character,
}: {
  character: Character;
}) {
  return (
    <div className="rounded-lg border border-border bg-card p-6">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        Character
      </p>

      <h3 className="mt-2 font-heading text-2xl font-semibold">
        {
          character.name
        }
      </h3>

      <p className="mt-2 text-sm text-muted-foreground">
        Campaign Setting
      </p>

      <p className="mt-1 font-medium">
        {
          character.settingName
        }
      </p>

      <p className="mt-6 text-sm leading-6 text-muted-foreground">
        Your choices in the next
        steps will connect this
        character to the lore of
        their campaign setting.
      </p>
    </div>
  );
}

function getEntryChoiceKey(
  stepKey:
    BuilderStepKey
):
  | EntryChoiceKey
  | undefined {
  switch (stepKey) {
    case "homeland":
      return "homelandEntryId";

    case "culture":
      return "cultureEntryId";

    case "religion":
      return "religionEntryId";

    case "faction":
      return "factionEntryId";

    default:
      return undefined;
  }
}

function findChoiceEntry(
  entries: SettingEntry[],
  entryId?: string
):
  | SettingEntry
  | undefined {
  if (!entryId) {
    return undefined;
  }

  return entries.find(
    (entry) =>
      entry.id === entryId
  );
}