export type SettingRole = "GM" | "Player";

export type Setting = {
  id: string;
  name: string;
  description: string;
  entryCount: number;
  updatedAt: string;

  role: SettingRole;
  isOwner: boolean;
};

export type SettingEntryType =
  | "Nation"
  | "City"
  | "Culture"
  | "Religion"
  | "Faction";

export type SettingEntry = {
  id: string;
  settingId: string;

  name: string;
  type: SettingEntryType;

  summary: string;
  content: string;

  isGmOnly: boolean;

  relationshipCount: number;

  createdAt: string;
  updatedAt: string;
};