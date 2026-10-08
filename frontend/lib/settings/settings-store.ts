import { getEntriesForSetting } from "./entries-store";
import type { Setting, SettingRole } from "./types";

const STORAGE_KEY = "lorebound-settings";

const defaultSettings: Setting[] = [
  {
    id: "osepia",
    name: "Osepia",
    description:
      "A large campaign setting filled with competing nations, cultures, and ancient history.",
    entryCount: 0,
    updatedAt: new Date().toISOString(),
    role: "GM",
    isOwner: true,
  },
  {
    id: "campaign-setting-2",
    name: "Campaign Setting 2",
    description:
      "A second campaign setting ready to be expanded.",
    entryCount: 0,
    updatedAt: new Date().toISOString(),
    role: "Player",
    isOwner: false,
  },
];

export function getSettings(): Setting[] {
  if (typeof window === "undefined") {
    return defaultSettings;
  }

  const stored = window.localStorage.getItem(STORAGE_KEY);

  if (!stored) {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(defaultSettings)
    );
  }

  const settings = stored
    ? (JSON.parse(stored) as Setting[])
    : defaultSettings;

  return settings.map((setting) => {
    const role = setting.role ?? "GM";

    return {
      ...setting,
      role,
      isOwner: setting.isOwner ?? true,
      entryCount: countVisibleEntries(setting.id, role),
    };
  });
}

// Counted from the stored entries rather than kept on the setting, so it is
// always current. Players do not count GM-only entries, like the API.
function countVisibleEntries(
  settingId: string,
  role: SettingRole
): number {
  return getEntriesForSetting(settingId).filter(
    (entry) => role === "GM" || !entry.isGmOnly
  ).length;
}

export function getSetting(id: string): Setting | undefined {
  return getSettings().find((setting) => setting.id === id);
}

export function createSetting(
  name: string,
  description: string
): Setting {
  const settings = getSettings();

  const setting: Setting = {
    id: crypto.randomUUID(),
    name,
    description,
    entryCount: 0,
    updatedAt: new Date().toISOString(),
  
    role: "GM",
    isOwner: true,
  };

  window.localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify([...settings, setting])
  );

  return setting;
}