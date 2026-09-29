"use client";

import { use, useEffect, useState } from "react";
import { Plus } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { EntrySearch } from "@/components/settings/EntrySearch";
import { SettingEntryCard } from "@/components/settings/SettingEntryCard";
import { SettingTabs } from "@/components/settings/SettingTabs";
import { Button } from "@/components/ui/button";

import { getSetting } from "@/lib/settings/settings-store";
import type { Setting } from "@/lib/settings/types";

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

  useEffect(() => {
    setSetting(getSetting(settingId));
  }, [settingId]);

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
          <Button>
            <Plus className="h-4 w-4" />
            Add Entry
          </Button>
        }
      />

      <div className="space-y-6">
        <SettingTabs />

        <EntrySearch />

        <div className="grid gap-4 lg:grid-cols-2">
          <SettingEntryCard
            name="Nigallu"
            type="Location"
            description="A major city and important regional center within the setting."
            relationshipCount={4}
          />

          <SettingEntryCard
            name="Merchant Guild"
            type="Faction"
            description="A powerful trade organization with influence throughout the region."
            relationshipCount={7}
          />

          <SettingEntryCard
            name="Cult of Beléna"
            type="Religion"
            description="A major religious tradition practiced throughout parts of the setting."
            relationshipCount={5}
          />
        </div>
      </div>
    </AppShell>
  );
}