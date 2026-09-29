"use client";

import { use, useEffect, useState } from "react";
import { Plus } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { EntrySearch } from "@/components/settings/EntrySearch";
import { SettingEntryCard } from "@/components/settings/SettingEntryCard";
import { SettingTabs, type SettingTab, } from "@/components/settings/SettingTabs";
import { Button } from "@/components/ui/button";

import { SettingEntryDialog } from "@/components/settings/SettingEntryDialog";



import { getSetting } from "@/lib/settings/settings-store";
import type { Setting, SettingEntry } from "@/lib/settings/types";
import {
  createEntry,
  getEntriesForSetting,
  updateEntry,
} from "@/lib/settings/entries-store";

type SettingDetailPageProps = {
  params: Promise<{
    settingId: string;
  }>;
};

export default function SettingDetailPage({
  params,
}: SettingDetailPageProps) {
  const { settingId } = use(params);

  const [setting, setSetting] = useState<Setting>();
  const [activeTab, setActiveTab] = useState<SettingTab>("Overview");
  const tabTypeMap = {
    Nations: "Nation",
    Cities: "City",
    Cultures: "Culture",
    Religions: "Religion",
    Factions: "Faction",
  } as const;

  const canManageSetting =
  setting?.role === "GM";

  const [entries, setEntries] = useState<SettingEntry[]>([]);
  const visibleEntries =
  setting?.role === "GM"
    ? entries
    : entries.filter(
        (entry) => !entry.isGmOnly
      );

const filteredEntries =
  activeTab === "Overview"
    ? visibleEntries
    : visibleEntries.filter(
        (entry) =>
          entry.type === tabTypeMap[activeTab]
      );

  const [entryDialogOpen, setEntryDialogOpen] = useState(false);

  const [editingEntry, setEditingEntry] = useState<SettingEntry>();

    useEffect(() => {
      setSetting(getSetting(settingId));
      setEntries(
        getEntriesForSetting(settingId)
      );
    }, [settingId]);

    function handleAddEntry() {
      setEditingEntry(undefined);
      setEntryDialogOpen(true);
    }
    
    function handleEditEntry(
      entry: SettingEntry
    ) {
      setEditingEntry(entry);
      setEntryDialogOpen(true);
    }
    
    function handleSaveEntry(data: {
      name: string;
      type: SettingEntry["type"];
      summary: string;
      content: string;
    }) {
      if (editingEntry) {
        const updated = updateEntry(
          editingEntry.id,
          data
        );
    
        if (!updated) {
          return;
        }
    
        setEntries((current) =>
          current.map((entry) =>
            entry.id === updated.id
              ? updated
              : entry
          )
        );
    
        return;
      }
    
      const created = createEntry(
        settingId,
        data
      );
    
      setEntries((current) => [
        ...current,
        created,
      ]);
    }

  if (!setting) {
    return (
      <AppShell>
        <div className="py-16 text-center">
          <h1 className="text-2xl font-semibold">
            Setting not found
          </h1>

          <p className="mt-2 text-muted-foreground">
            This campaign setting could not be found.
          </p>
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <PageHeader
        title={setting.name}
        description={
          setting.description ||
          "Manage entries and relationships in this campaign setting."
        }
        actions={
          canManageSetting ? (
            <Button onClick={handleAddEntry}>
              <Plus className="h-4 w-4" />
              Add Entry
            </Button>
          ) : undefined
        }
      />

      <div className="space-y-6">
      <SettingTabs
        activeTab={activeTab}
        onTabChange={setActiveTab}
      />

        <EntrySearch />

        {filteredEntries.length > 0 ? (
          <div className="grid gap-4 lg:grid-cols-2">
            {filteredEntries.map((entry) => (
              <SettingEntryCard
                key={entry.id}
                name={entry.name}
                type={entry.type}
                description={entry.summary}
                relationshipCount={entry.relationshipCount}
                onEdit={() => handleEditEntry(entry)}
              />
            ))}
          </div>
        ) : (
          <div className="border border-dashed border-border p-10 text-center">
            <p className="font-medium">
              {activeTab === "Overview"
                ? "No lore entries yet"
                : `No ${activeTab.toLowerCase()} yet`}
            </p>

            <p className="mt-2 text-sm text-muted-foreground">
              {canManageSetting
                ? "Add a lore entry to start building this setting."
                : "There are no available lore entries in this category."}
            </p>

            {canManageSetting && (
              <Button
                className="mt-4"
                onClick={handleAddEntry}
              >
                <Plus className="h-4 w-4" />
                Add Entry
              </Button>
            )}
          </div>
        )}
      </div>
      <SettingEntryDialog
        open={entryDialogOpen}
        onOpenChange={setEntryDialogOpen}
        entry={editingEntry}
        onSave={handleSaveEntry}
      />
    </AppShell>
  );
}