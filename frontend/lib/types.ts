export type SettingEntryType =
  | "Location"
  | "Culture"
  | "Religion"
  | "Faction"
  | "Organization"
  | "Profession"
  | "SocialClass"
  | "HistoricalEvent"
  | "Person"
  | "Other";

export type SettingRole =
  | "GameMaster"
  | "Player";

export type CharacterStatus =
  | "Draft"
  | "Complete";

export type AuthUserDto = {
  id: string;
  email: string;
  displayName: string;
};

export type RegisteredUserDto = {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
};

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type SettingListItemDto = {
  id: string;
  name: string;
  description: string | null;
  entryCount: number;
  myRole: SettingRole;
  isOwner: boolean;
  ownerDisplayName: string;
  updatedAt: string;
};

export type SettingDetailDto = {
  id: string;
  name: string;
  description: string | null;
  myRole: SettingRole;
  isOwner: boolean;
  ownerDisplayName: string;
  memberCount: number;
  entryCountsByType: Partial<
    Record<SettingEntryType, number>
  >;
  createdAt: string;
  updatedAt: string;
};

export type SettingEntryDto = {
  id: string;
  campaignSettingId: string;
  name: string;
  description: string | null;
  entryType: SettingEntryType;
  isGmOnly?: boolean;
  createdAt: string;
  updatedAt: string;
};

export type CharacterChoiceDto = {
  stepKey: string;
  ordinal: number;
  entryId: string | null;
  entryName: string | null;
  entryType: SettingEntryType | null;
  freeText: string | null;
};

export type CharacterDetailDto = {
  id: string;
  settingId: string;
  settingName: string;
  ownerUserId: string;
  ownerDisplayName: string;
  name: string;
  status: CharacterStatus;
  currentStep: number;
  backstory: string | null;
  isOwner: boolean;
  isReadOnly: boolean;
  choices: CharacterChoiceDto[];
  createdAt: string;
  updatedAt: string;
};

export type CharacterListItemDto = {
  id: string;
  name: string;
  status: CharacterStatus;
  settingId: string;
  settingName: string;
  homelandName: string | null;
  currentStep: number;
  isReadOnly: boolean;
  updatedAt: string;
};

export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
};