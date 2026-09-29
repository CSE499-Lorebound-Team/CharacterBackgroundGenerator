import type { Setting } from "./types";

const STORAGE_KEY = "lorebound-settings";

const defaultSettings: Setting[] = [
  {
    id: "osepia",
    name: "Osepia",
    description:
      "A large campaign setting filled with competing nations, cultures, and ancient history.",
    entryCount: 24,
    updatedAt: new Date().toISOString(),
    role: "GM",
    isOwner: true,
  },
  {
    id: "campaign-setting-2",
    name: "Campaign Setting 2",
    description:
      "A second campaign setting ready to be expanded.",
    entryCount: 12,
    updatedAt: new Date().toISOString(),
    role: "GM",
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

    return defaultSettings;
  }

  const settings = JSON.parse(stored) as Setting[];

  return settings.map((setting) => ({
    ...setting,
    role: setting.role ?? "GM",
    isOwner: setting.isOwner ?? true,
  }));
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