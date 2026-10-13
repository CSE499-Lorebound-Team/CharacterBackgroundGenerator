import type {
  BuilderStepDto,
  BuilderStepsDto,
  CharacterDetailDto,
} from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";

// The builder's steps come only from GET /api/builder/steps (the API's
// BuilderSteps catalog); the frontend keeps no copy of its own.

export function useBuilderSteps() {
  return useApiData<BuilderStepsDto>("/api/builder/steps");
}

/** A short label from a step key: "social_class" -> "Social Class". */
export function stepLabel(key: string): string {
  return key
    .split("_")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
}

/** Steps that store answers (not `setting` or `review`). */
export function isAnswerStep(step: BuilderStepDto): boolean {
  return step.maxSelections > 0;
}

export type ChoiceRow = {
  key: string;
  label: string;
  /** The chosen entry names or free text; null when unanswered. */
  value: string | null;
  /** The chosen entry was deleted from the setting since. */
  removed: boolean;
  required: boolean;
  stale: boolean;
};

/**
 * One row per catalog step except Review: Setting from the character, the
 * rest from its choices.
 */
export function choiceRows(
  steps: BuilderStepDto[],
  character: CharacterDetailDto
): ChoiceRow[] {
  return steps
    .filter((step) => step.key !== "review")
    .map((step) => {
      if (step.key === "setting") {
        return {
          key: step.key,
          label: stepLabel(step.key),
          value: character.settingName,
          removed: false,
          required: true,
          stale: false,
        };
      }

      const choices = character.choices
        .filter((choice) => choice.stepKey === step.key)
        .sort((a, b) => a.ordinal - b.ordinal);

      const values = choices
        .map((choice) => choice.entryName ?? choice.freeText)
        .filter((value): value is string => Boolean(value));

      return {
        key: step.key,
        label: stepLabel(step.key),
        value: values.length > 0 ? values.join(", ") : null,
        // A deleted entry leaves the choice with no id, name or text.
        removed: choices.some(
          (choice) => !choice.entryId && !choice.entryName && !choice.freeText
        ),
        required: step.required,
        stale: character.staleSteps.includes(step.key),
      };
    });
}
