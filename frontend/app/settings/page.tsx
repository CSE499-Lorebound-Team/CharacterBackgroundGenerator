"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import {
  EmptyState,
  ErrorState,
  LoadingCards,
  LoadMore,
} from "@/components/layout/PageStates";
import { CreateSettingDialog } from "@/components/settings/CreateSettingDialog";
import { SettingListCard } from "@/components/settings/SettingListCard";
import { SettingSearch } from "@/components/settings/SettingSearch";
import { Button } from "@/components/ui/button";
import type { SettingListItemDto } from "@/lib/types";
import { usePagedList } from "@/lib/use-api-data";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { cn } from "@/lib/utils";

type RoleFilter = "all" | "gm" | "player";

const roleFilters: { value: RoleFilter; label: string }[] = [
  { value: "all", label: "All" },
  { value: "gm", label: "GM" },
  { value: "player", label: "Player" },
];

export default function SettingsPage() {
  const router = useRouter();

  const [search, setSearch] = useState("");
  const [role, setRole] = useState<RoleFilter>("all");
  const [createOpen, setCreateOpen] = useState(false);

  const query = useDebouncedValue(search.trim());

  const params = new URLSearchParams();
  if (query) params.set("search", query);
  if (role !== "all") params.set("role", role);
  const path = `/api/settings${params.size > 0 ? `?${params}` : ""}`;

  const settings = usePagedList<SettingListItemDto>(path);
  const filtered = Boolean(query) || role !== "all";

  const createButton = (
    <Button onClick={() => setCreateOpen(true)}>
      <Plus className="h-4 w-4" />
      Create Setting
    </Button>
  );

  return (
    <AppShell>
      <PageHeader
        title="Settings"
        description="The campaign worlds you run or play in."
        actions={createButton}
      />

      <div className="space-y-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <SettingSearch value={search} onChange={setSearch} />

          <div
            role="group"
            aria-label="Filter by your role"
            className="flex gap-1"
          >
            {roleFilters.map((filter) => (
              <Button
                key={filter.value}
                size="sm"
                variant={role === filter.value ? "default" : "outline"}
                aria-pressed={role === filter.value}
                onClick={() => setRole(filter.value)}
              >
                {filter.label}
              </Button>
            ))}
          </div>
        </div>

        {settings.error ? (
          <ErrorState error={settings.error} onRetry={settings.reload} />
        ) : !settings.hasLoaded ? (
          <LoadingCards />
        ) : settings.items.length === 0 ? (
          filtered ? (
            <EmptyState
              title="No settings match"
              description="Try another search or role filter."
            />
          ) : (
            <EmptyState
              title="No settings yet"
              description="Create a setting to begin building your campaign world, or join one with an invite link from your Game Master."
              action={createButton}
            />
          )
        ) : (
          <div
            className={cn(
              "space-y-4 transition-opacity",
              settings.isLoading && "opacity-60"
            )}
            aria-busy={settings.isLoading}
          >
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {settings.items.map((setting) => (
                <SettingListCard key={setting.id} setting={setting} />
              ))}
            </div>

            {settings.hasMore && (
              <LoadMore
                shown={settings.items.length}
                total={settings.totalCount}
                loading={settings.loadingMore}
                onLoadMore={settings.loadMore}
              />
            )}
          </div>
        )}
      </div>

      <CreateSettingDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={(setting) => {
          setCreateOpen(false);
          settings.reload();
          router.push(`/settings/${setting.id}`);
        }}
      />
    </AppShell>
  );
}
