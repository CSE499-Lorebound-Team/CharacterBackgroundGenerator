import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { CharacterCard } from "@/components/dashboard/CharacterCard";
import { DashboardCounts } from "@/components/dashboard/DashboardCounts";
import { QuickActions } from "@/components/dashboard/QuickActions";
import { RecentActivity } from "@/components/dashboard/RecentActivity";
import { SettingCard } from "@/components/dashboard/SettingCard";
import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { EmptyState } from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { apiFetchServer } from "@/lib/api-server";
import type { DashboardDto } from "@/lib/types";

export const metadata: Metadata = {
  title: "Dashboard | Lorebound",
};

// Rendered on the server with the user's cookie forwarded (lib/api-server).
// Other failures reach app/dashboard/error.tsx.
export default async function DashboardPage() {
  let dashboard: DashboardDto;

  try {
    dashboard = await apiFetchServer<DashboardDto>("/api/dashboard");
  } catch (error) {
    // proxy.ts only checks that a cookie exists; this catches an expired one.
    if (error instanceof ApiError && error.status === 401) {
      redirect("/login?next=%2Fdashboard");
    }
    throw error;
  }

  return (
    <AppShell>
      <PageHeader
        title="Dashboard"
        description="Your settings, characters and the latest changes."
        actions={<QuickActions />}
      />

      <div className="space-y-10">
        <DashboardCounts counts={dashboard.counts} />

        <section className="space-y-4">
          <SectionHeader title="My Settings" href="/settings" linkLabel="View All Settings" />
          {dashboard.recentSettings.length > 0 ? (
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {dashboard.recentSettings.map((setting) => (
                <SettingCard key={setting.id} setting={setting} />
              ))}
            </div>
          ) : (
            <EmptyState
              title="No settings yet"
              description="Create your first campaign setting, or join one with an invite link from your Game Master."
              action={
                <Button nativeButton={false} render={<Link href="/settings" />}>
                  Create Setting
                </Button>
              }
            />
          )}
        </section>

        <section className="space-y-4">
          <SectionHeader title="My Characters" href="/characters" linkLabel="View All Characters" />
          {dashboard.recentCharacters.length > 0 ? (
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {dashboard.recentCharacters.map((character) => (
                <CharacterCard key={character.id} character={character} />
              ))}
            </div>
          ) : (
            <EmptyState
              title="No characters yet"
              description="Create your first character to begin building their background."
              action={
                <Button nativeButton={false} render={<Link href="/characters?create=1" />}>
                  Create Character
                </Button>
              }
            />
          )}
        </section>

        <section className="space-y-4">
          <h2 className="font-heading text-xl font-semibold">Recent Activity</h2>
          <RecentActivity activity={dashboard.recentActivity} />
        </section>
      </div>
    </AppShell>
  );
}

type SectionHeaderProps = {
  title: string;
  href: string;
  linkLabel: string;
};

function SectionHeader({ title, href, linkLabel }: SectionHeaderProps) {
  return (
    <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
      <h2 className="font-heading text-xl font-semibold">{title}</h2>
      <Button
        variant="ghost"
        className="self-start sm:self-auto"
        nativeButton={false}
        render={<Link href={href} />}
      >
        {linkLabel}
      </Button>
    </div>
  );
}
