"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";

import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { QuickActions } from "@/components/dashboard/QuickActions";
import { SettingCard } from "@/components/dashboard/SettingCard";
import { CharacterCard } from "@/components/dashboard/CharacterCard";
import { RecentActivity } from "@/components/dashboard/RecentActivity";
import { Button } from "@/components/ui/button";

import { getSettings } from "@/lib/settings/settings-store";
import type { Setting } from "@/lib/settings/types";

import { getCharacters } from "@/lib/characters/characters-store";
import type { Character } from "@/lib/characters/types";

export default function DashboardPage() {
  const [settings, setSettings] = useState<Setting[]>([]);

  useEffect(() => {
    setSettings(getSettings());
  }, []);

  const recentSettings = useMemo(() => {
    return [...settings]
      .sort(
        (a, b) =>
          new Date(b.updatedAt).getTime() -
          new Date(a.updatedAt).getTime()
      )
      .slice(0, 3);
  }, [settings]);

  const [characters, setCharacters] = useState<Character[]>([]);

  useEffect(() => {
    setCharacters(getCharacters());
  }, []);
  
  const recentCharacters = useMemo(() => {
    return [...characters]
      .sort(
        (a, b) =>
          new Date(b.updatedAt).getTime() -
          new Date(a.updatedAt).getTime()
      )
      .slice(0, 3);
  }, [characters]);

  return (
    <AppShell>
      <PageHeader
        title="Dashboard"
        description="Manage your settings and characters."
        actions={<QuickActions />}
      />

      <div className="space-y-10">
        <section className="space-y-4">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <h2 className="font-heading text-xl font-semibold">
              My Settings
            </h2>

            <Button
              variant="ghost"
              className="self-start sm:self-auto"
              nativeButton={false}
              render={<Link href="/settings" />}
            >
              View All Settings
            </Button>
          </div>

          {recentSettings.length > 0 ? (
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {recentSettings.map((setting) => (
                <SettingCard
                  key={setting.id}
                  id={setting.id}
                  name={setting.name}
                  entryCount={setting.entryCount}
                  updatedAt={setting.updatedAt}
                />
              ))}
            </div>
          ) : (
            <div className="border border-dashed border-border p-8 text-center">
              <p className="font-medium">
                No settings yet
              </p>

              <p className="mt-2 text-sm text-muted-foreground">
                Create your first campaign setting to start building lore.
              </p>

              <Button
                className="mt-4"
                nativeButton={false}
                render={<Link href="/settings" />}
              >
                Create Setting
              </Button>
            </div>
          )}
        </section>

        <section className="space-y-4">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <h2 className="font-heading text-xl font-semibold">
              My Characters
            </h2>

            <Button
              variant="ghost"
              className="self-start sm:self-auto"
              nativeButton={false}
              render={<Link href="/characters" />}
            >
              View All Characters
            </Button>
          </div>

          {recentCharacters.length > 0 ? (
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {recentCharacters.map((character) => (
                <CharacterCard
                  key={character.id}
                  id={character.id}
                  name={character.name}
                  settingName={character.settingName}
                  status={character.status}
                />
              ))}
            </div>
          ) : (
            <div className="border border-dashed border-border p-8 text-center">
              <p className="font-medium">
                No characters yet
              </p>

              <p className="mt-2 text-sm text-muted-foreground">
                Create your first character to begin building their background.
              </p>

              <Button
                className="mt-4"
                nativeButton={false}
                render={<Link href="/characters" />}
              >
                Create Character
              </Button>
            </div>
          )}
        </section>

        <RecentActivity
          items={[
            "Edited Osepia setting",
            "Added faction: Merchant Guild",
            "Updated Theron Vale",
          ]}
        />
      </div>
    </AppShell>
  );
}