import type { SettingEntryType } from "@/lib/types";

type EntryTypeInfo = {
  type: SettingEntryType;
  label: string;
  plural: string;
};

// The API's SettingEntryType enum, in its order, with display names.
export const ENTRY_TYPES: EntryTypeInfo[] = [
  { type: "Location", label: "Location", plural: "Locations" },
  { type: "Culture", label: "Culture", plural: "Cultures" },
  { type: "Religion", label: "Religion", plural: "Religions" },
  { type: "Faction", label: "Faction", plural: "Factions" },
  { type: "Organization", label: "Organization", plural: "Organizations" },
  { type: "Profession", label: "Profession", plural: "Professions" },
  { type: "SocialClass", label: "Social Class", plural: "Social Classes" },
  { type: "HistoricalEvent", label: "Historical Event", plural: "Historical Events" },
  { type: "Person", label: "Person", plural: "People" },
  { type: "Other", label: "Other", plural: "Other" },
];

export function entryTypeLabel(type: SettingEntryType): string {
  return ENTRY_TYPES.find((info) => info.type === type)?.label ?? type;
}
