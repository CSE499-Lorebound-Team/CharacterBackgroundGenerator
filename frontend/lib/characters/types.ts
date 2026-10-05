export type CharacterStatus =
  | "Draft"
  | "Complete";

export type Character = {
  id: string;

  name: string;

  settingId: string;
  settingName: string;

  status: CharacterStatus;
  currentStep: number;

  backstory: string;

  createdAt: string;
  updatedAt: string;
};