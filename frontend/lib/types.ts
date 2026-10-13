// DTOs returned and accepted by the Lorebound API. They mirror the C# records
// in api/Dtos (see docs/api/openapi.json); dates are ISO 8601 strings and enums
// are sent as their names.

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

export type SettingRole = "GameMaster" | "Player";

export type CharacterStatus = "Draft" | "Complete";

export type InviteStatus = "Active" | "Expired" | "Revoked" | "Exhausted";

export type DashboardActivityKind = "Setting" | "Entry" | "Character";

export type ProblemDetails = {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  // Validation problems (400) list messages per field.
  errors?: Record<string, string[]>;
  // Extensions sit at the top level, e.g. relationshipCount on a 409.
  [extension: string]: unknown;
};

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type HealthResponse = {
  status: string;
  application: string;
  database: string;
};

// Auth and account

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

export type UserProfileDto = {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  createdAt: string;
};

export type LoginRequest = {
  email: string;
  password: string;
  rememberMe?: boolean;
};

export type RegisterRequest = {
  email: string;
  password: string;
  displayName: string;
};

export type ConfirmEmailRequest = {
  userId: string;
  code: string;
};

export type ResendConfirmationRequest = {
  email: string;
};

export type ForgotPasswordRequest = {
  email: string;
};

export type ResetPasswordRequest = {
  email: string;
  code: string;
  newPassword: string;
};

export type UpdateProfileRequest = {
  displayName: string;
};

export type DeleteAccountRequest = {
  password: string;
};

// Settings, members and invites

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
  entryCountsByType: Partial<Record<SettingEntryType, number>>;
  createdAt: string;
  updatedAt: string;
};

export type CreateSettingRequest = {
  name: string;
  description: string | null;
};

export type UpdateSettingRequest = CreateSettingRequest;

export type MemberDto = {
  userId: string;
  displayName: string;
  role: SettingRole;
  isOwner: boolean;
  joinedAt: string;
};

export type UpdateMemberRoleRequest = {
  role: SettingRole;
};

export type InviteDto = {
  id: string;
  code: string;
  joinUrl: string;
  status: InviteStatus;
  expiresAt: string | null;
  maxUses: number | null;
  useCount: number;
  revokedAt: string | null;
  createdAt: string;
};

export type CreateInviteRequest = {
  expiresInDays: number | null;
  maxUses: number | null;
};

export type InvitePreviewDto = {
  settingName: string;
  gmDisplayName: string;
  alreadyMember: boolean;
};

export type AcceptInviteResponse = {
  settingId: string;
  myRole: SettingRole;
};

// Entries and relationships. The API omits isGmOnly for players.

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

export type EntryListItemDto = {
  id: string;
  name: string;
  entryType: SettingEntryType;
  description: string | null;
  isGmOnly?: boolean;
  relationshipCount: number;
  updatedAt: string;
};

export type EntryRelationshipDto = {
  id: string;
  otherEntryId: string;
  otherEntryName: string;
  otherEntryType: SettingEntryType;
  relationshipType: string;
  description: string | null;
};

export type EntryDetailDto = {
  id: string;
  campaignSettingId: string;
  name: string;
  entryType: SettingEntryType;
  description: string | null;
  isGmOnly?: boolean;
  outgoing: EntryRelationshipDto[];
  incoming: EntryRelationshipDto[];
  createdAt: string;
  updatedAt: string;
};

export type CreateEntryRequest = {
  name: string;
  entryType: SettingEntryType;
  description: string | null;
  isGmOnly?: boolean;
};

export type UpdateEntryRequest = CreateEntryRequest;

export type RelationshipEndpointDto = {
  id: string;
  name: string;
  entryType: SettingEntryType;
};

export type RelationshipDto = {
  id: string;
  source: RelationshipEndpointDto;
  target: RelationshipEndpointDto;
  relationshipType: string;
  description: string | null;
  createdAt: string;
  updatedAt: string;
};

export type RelationshipTypesDto = {
  suggested: string[];
};

export type CreateRelationshipRequest = {
  sourceEntryId: string;
  targetEntryId: string;
  relationshipType: string;
  description: string | null;
};

export type UpdateRelationshipRequest = {
  relationshipType: string;
  description: string | null;
  sourceEntryId?: string | null;
  targetEntryId?: string | null;
};

// Characters and the builder

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
  // Keys of steps whose choices no longer fit earlier ones.
  staleSteps: string[];
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

export type SettingCharacterListItemDto = {
  id: string;
  name: string;
  status: CharacterStatus;
  ownerUserId: string;
  ownerDisplayName: string;
  ownerIsMember: boolean;
  homelandName: string | null;
  currentStep: number;
  updatedAt: string;
};

export type CreateCharacterRequest = {
  settingId: string;
  name: string;
};

export type UpdateCharacterRequest = {
  name: string;
  backstory: string | null;
};

export type BuilderStepDto = {
  key: string;
  order: number;
  title: string;
  description: string;
  entryType: SettingEntryType | null;
  allowFreeText: boolean;
  maxFreeTextLength: number | null;
  required: boolean;
  maxSelections: number;
};

export type BuilderStepsDto = {
  steps: BuilderStepDto[];
};

export type StepOptionDto = {
  entryId: string;
  name: string;
  description: string | null;
};

export type StepOptionsDto = {
  stepKey: string;
  narrowed: boolean;
  options: StepOptionDto[];
};

export type SaveChoiceRequest = {
  entryIds: string[] | null;
  freeText: string | null;
};

// Dashboard

export type DashboardCountsDto = {
  settingsAsGm: number;
  settingsAsPlayer: number;
  charactersDraft: number;
  charactersComplete: number;
};

export type DashboardActivityDto = {
  kind: DashboardActivityKind;
  text: string;
  at: string;
  id: string;
  settingId: string;
};

export type DashboardDto = {
  counts: DashboardCountsDto;
  recentSettings: SettingListItemDto[];
  recentCharacters: CharacterListItemDto[];
  recentActivity: DashboardActivityDto[];
};
