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