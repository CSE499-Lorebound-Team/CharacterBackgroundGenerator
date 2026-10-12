import type { BuilderStep } from "./types";

export const builderSteps: BuilderStep[] = [
  {
    key: "character",
    order: 1,
    title: "Your Character",
    description:
      "Review your character and campaign setting before beginning their background.",
  },
  {
    key: "homeland",
    order: 2,
    title: "Choose Your Homeland",
    description:
      "Choose the nation or city your character calls home.",
    entryTypes: ["Nation", "City"],
  },
  {
    key: "culture",
    order: 3,
    title: "Choose Your Culture",
    description:
      "Choose the culture that most strongly shaped your character.",
    entryTypes: ["Culture"],
  },
  {
    key: "religion",
    order: 4,
    title: "Faith and Religion",
    description:
      "Choose a religion important to your character, or continue without one.",
    entryTypes: ["Religion"],
    optional: true,
  },
  {
    key: "faction",
    order: 5,
    title: "Faction or Connection",
    description:
      "Choose a faction your character belongs to or has meaningful ties to.",
    entryTypes: ["Faction"],
    optional: true,
  },
  {
    key: "motivation",
    order: 6,
    title: "Motivation",
    description:
      "What drives your character forward?",
    freeText: true,
  },
  {
    key: "backstory",
    order: 7,
    title: "Backstory",
    description:
      "Write the important events and experiences that shaped your character.",
    freeText: true,
    optional: true,
  },
  {
    key: "review",
    order: 8,
    title: "Review Your Character",
    description:
      "Review your choices and finish your character background.",
  },
];

export function getBuilderStep(
  order: number
): BuilderStep | undefined {
  return builderSteps.find(
    (step) => step.order === order
  );
}