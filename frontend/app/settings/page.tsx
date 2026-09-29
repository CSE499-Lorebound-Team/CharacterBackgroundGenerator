"use client";

import { useEffect, useMemo, useState } from "react";
import { Plus } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { CreateSettingDialog } from "@/components/settings/CreateSettingDialog";
import { SettingListCard } from "@/components/settings/SettingListCard";
import { SettingSearch } from "@/components/settings/SettingSearch";
import { Button } from "@/components/ui/button";

import {
  createSetting,
  getSettings,
} from "@/lib/settings/settings-store";
import type { Setting } from "@/lib/settings/types";

export default function SettingsPage() {
  const [settings, setSettings] = useState<Setting[]>([]);
  const [search, setSearch] = useState("");
  const [createOpen, setCreateOpen] = useState(false);

  useEffect(() => {
    setSettings(getSettings());
  }, []);

  const filteredSettings = useMemo(() => {
    const query = search.trim().toLowerCase();

    if (!query) {
      return settings;
    }

    return settings.filter((setting) =>
      setting.name.toLowerCase().includes(query)
    );
  }, [settings, search]);

  function handleCreate(
    name: string,
    description: string
  ) {
    const setting = createSetting(name, description);

    setSettings((current) => [
      ...current,
      setting,
    ]);
  }

  return (
    <AppShell>
      <PageHeader
        title="Settings"
        description="Manage your campaign worlds and setting information."
        actions={
          <Button
            onClick={() => setCreateOpen(true)}
          >
            <Plus className="h-4 w-4" />
            Create Setting
          </Button>
        }
      />

      <div className="space-y-6">
        <SettingSearch
          value={search}
          onChange={setSearch}
        />

        {filteredSettings.length > 0 ? (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {filteredSettings.map((setting) => (
              <SettingListCard
                key={setting.id}
                id={setting.id}
                name={setting.name}
                description={setting.description}
                entryCount={setting.entryCount}
                updatedAt={setting.updatedAt}
              />
            ))}
          </div>
        ) : (
          <div className="border border-dashed border-border p-10 text-center">
            <p className="font-medium">
              No settings found
            </p>

            <p className="mt-2 text-sm text-muted-foreground">
              Create a setting to begin building your campaign world.
            </p>
          </div>
        )}
      </div>

      <CreateSettingDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreate={handleCreate}
      />
    </AppShell>
  );
}