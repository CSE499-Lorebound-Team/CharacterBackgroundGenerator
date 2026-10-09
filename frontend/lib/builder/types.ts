import type { SettingEntryType } from "@/lib/settings/types";

export type BuilderStepKey =
  | "character"
  | "homeland"
  | "culture"
  | "religion"
  | "faction"
  | "motivation"
  | "backstory"
  | "review";

export type BuilderStep = {
  key: BuilderStepKey;
  order: number;
  title: string;
  description: string;
  entryTypes?: SettingEntryType[];
  optional?: boolean;
  freeText?: boolean;
};

export type BuilderChoices = {
  homelandEntryId?: string;
  cultureEntryId?: string;
  religionEntryId?: string;
  factionEntryId?: string;
  motivation?: string;
  backstory?: string;
};

export type CharacterBuilderState = {
  characterId: string;
  currentStep: number;
  choices: BuilderChoices;
  updatedAt: string;
};