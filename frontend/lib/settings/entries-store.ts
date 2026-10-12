import type {
    SettingEntry,
    SettingEntryType,
  } from "./types";
  
  const STORAGE_KEY = "lorebound-setting-entries";
  
  function getAllEntries(): SettingEntry[] {
    if (typeof window === "undefined") {
      return [];
    }
  
    const stored =
      window.localStorage.getItem(STORAGE_KEY);
  
    if (!stored) {
      return [];
    }
  
    const entries =
      JSON.parse(stored) as SettingEntry[];
  
    return entries.map((entry) => ({
      ...entry,
      isGmOnly: entry.isGmOnly ?? false,
    }));
  }
  
  function saveEntries(
    entries: SettingEntry[]
  ) {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(entries)
    );
  }
  
  export function getEntriesForSetting(
    settingId: string
  ): SettingEntry[] {
    return getAllEntries().filter(
      (entry) => entry.settingId === settingId
    );
  }
  
  // Same rule as the API (409): a name may appear once per type in a
  // setting, ignoring case. Returns an error message, or undefined if free.
  export function duplicateNameError(
    settingId: string,
    type: SettingEntryType,
    name: string,
    exceptId?: string
  ): string | undefined {
    const taken = getAllEntries().some(
      (entry) =>
        entry.settingId === settingId &&
        entry.type === type &&
        entry.id !== exceptId &&
        entry.name.toLowerCase() === name.toLowerCase()
    );

    return taken
      ? `A ${type} with this name already exists in this setting.`
      : undefined;
  }

  export function createEntry(
    settingId: string,
    data: {
      name: string;
      type: SettingEntryType;
      summary: string;
      content: string;
      isGmOnly: boolean;
    }
  ): SettingEntry {
    const entries = getAllEntries();
  
    const now = new Date().toISOString();
  
    const entry: SettingEntry = {
      id: crypto.randomUUID(),
      settingId,
      name: data.name,
      type: data.type,
      summary: data.summary,
      content: data.content,
      relationshipCount: 0,
      createdAt: now,
      updatedAt: now,
      isGmOnly: data.isGmOnly,
    };
  
    saveEntries([...entries, entry]);
  
    return entry;
  }
  
  export function updateEntry(
    id: string,
    data: {
      name: string;
      type: SettingEntryType;
      summary: string;
      content: string;
      isGmOnly: boolean;
    }
  ): SettingEntry | undefined {
    const entries = getAllEntries();
  
    const existing = entries.find(
      (entry) => entry.id === id
    );
  
    if (!existing) {
      return undefined;
    }
  
    const updated: SettingEntry = {
      ...existing,
      ...data,
      updatedAt: new Date().toISOString(),
    };
  
    saveEntries(
      entries.map((entry) =>
        entry.id === id ? updated : entry
      )
    );
  
    return updated;
  }
  export function getEntry(
    id: string
  ): SettingEntry | undefined {
    return getAllEntries().find(
      (entry) => entry.id === id
    );
  }
  
  export function deleteEntry(
    id: string
  ): boolean {
    const entries = getAllEntries();
  
    const exists = entries.some(
      (entry) => entry.id === id
    );
  
    if (!exists) {
      return false;
    }
  
    saveEntries(
      entries.filter(
        (entry) => entry.id !== id
      )
    );
  
    return true;
  }