"use client";

import { use, useState } from "react";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import {
  EmptyState,
  ErrorState,
  LoadingCards,
  LoadMore,
} from "@/components/layout/PageStates";
import { EntrySearch } from "@/components/settings/EntrySearch";
import { RoleBadge } from "@/components/settings/RoleBadge";
import { SettingEntryCard } from "@/components/settings/SettingEntryCard";
import { SettingTabs, type SettingTab } from "@/components/settings/SettingTabs";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { ENTRY_TYPES } from "@/lib/entry-types";
import { formatRelativeTime, plural } from "@/lib/format";
import type { EntryListItemDto, SettingDetailDto } from "@/lib/types";
import { useApiData, usePagedList } from "@/lib/use-api-data";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { cn } from "@/lib/utils";

export default function SettingDetailPage({
  params,
}: PageProps<"/settings/[settingId]">) {
  const { settingId } = use(params);

  const setting = useApiData<SettingDetailDto>(`/api/settings/${settingId}`);

  if (setting.error instanceof ApiError && setting.error.status === 404) {
    return (
      <AppShell>
        <EmptyState
          title="Setting not found"
          description="This setting does not exist, or you are not a member of it."
          action={<BackToSettings />}
        />
      </AppShell>
    );
  }

  return (
    <AppShell>
      {setting.error ? (
        <ErrorState error={setting.error} onRetry={setting.reload} />
      ) : !setting.data ? (
        <LoadingCards count={4} />
      ) : (
        <SettingDetail
          setting={setting.data}
          onSettingChanged={setting.reload}
        />
      )}
    </AppShell>
  );
}

function BackToSettings() {
  return (
    <Button variant="outline" nativeButton={false} render={<Link href="/settings" />}>
      <ArrowLeft className="h-4 w-4" />
      Back to Settings
    </Button>
  );
}

type SettingDetailProps = {
  setting: SettingDetailDto;
  /** Reloads the header counts, e.g. after an entry is added. */
  onSettingChanged: () => void;
};

function SettingDetail({ setting }: SettingDetailProps) {
  const isGm = setting.myRole === "GameMaster";

  const [activeTab, setActiveTab] = useState<SettingTab>("Overview");
  const [search, setSearch] = useState("");
  const query = useDebouncedValue(search.trim());

  const params = new URLSearchParams();
  if (activeTab !== "Overview") params.set("type", activeTab);
  if (query) params.set("search", query);
  const entriesPath = `/api/settings/${setting.id}/entries${params.size > 0 ? `?${params}` : ""}`;

  const entries = usePagedList<EntryListItemDto>(entriesPath);
  const totalEntries = Object.values(setting.entryCountsByType).reduce(
    (sum, count) => sum + (count ?? 0),
    0
  );
  const tabLabel =
    ENTRY_TYPES.find((info) => info.type === activeTab)?.plural ?? "entries";

  return (
    <>
      <PageHeader
        title={setting.name}
        description={setting.description ?? undefined}
      />

      <div className="-mt-3 mb-6 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-muted-foreground">
        <RoleBadge role={setting.myRole} isOwner={setting.isOwner} />
        <span>Run by {setting.ownerDisplayName}</span>
        <span>{plural(setting.memberCount, "member")}</span>
        <span>{plural(totalEntries, "entry", "entries")}</span>
        <span>Updated {formatRelativeTime(setting.updatedAt)}</span>
      </div>

      <div className="space-y-6">
        <SettingTabs
          activeTab={activeTab}
          onTabChange={setActiveTab}
          counts={setting.entryCountsByType}
        />

        {activeTab === "Overview" && (
          <TypeOverview
            counts={setting.entryCountsByType}
            onSelect={setActiveTab}
          />
        )}

        <EntrySearch value={search} onChange={setSearch} />

        {entries.error ? (
          <ErrorState error={entries.error} onRetry={entries.reload} />
        ) : !entries.hasLoaded ? (
          <LoadingCards />
        ) : entries.items.length === 0 ? (
          <EmptyState
            title={
              query
                ? "No entries match your search"
                : activeTab === "Overview"
                  ? "No lore entries yet"
                  : `No ${tabLabel.toLowerCase()} yet`
            }
            description={
              query
                ? "Search looks at entry names and descriptions."
                : isGm
                  ? "Add a lore entry to start building this setting."
                  : "Your Game Master has not shared any entries here yet."
            }
          />
        ) : (
          <div
            className={cn(
              "space-y-4 transition-opacity",
              entries.isLoading && "opacity-60"
            )}
            aria-busy={entries.isLoading}
          >
            <div className="grid gap-4 lg:grid-cols-2">
              {entries.items.map((entry) => (
                <SettingEntryCard
                  key={entry.id}
                  settingId={setting.id}
                  entry={entry}
                />
              ))}
            </div>

            {entries.hasMore && (
              <LoadMore
                shown={entries.items.length}
                total={entries.totalCount}
                loading={entries.loadingMore}
                onLoadMore={entries.loadMore}
              />
            )}
          </div>
        )}
      </div>
    </>
  );
}

type TypeOverviewProps = {
  counts: SettingDetailDto["entryCountsByType"];
  onSelect: (tab: SettingTab) => void;
};

/** Entry counts by type; each opens its tab. */
function TypeOverview({ counts, onSelect }: TypeOverviewProps) {
  return (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
      {ENTRY_TYPES.map((info) => (
        <button
          key={info.type}
          type="button"
          onClick={() => onSelect(info.type)}
          className="border border-border bg-card p-3 text-left transition-colors hover:border-primary/40"
        >
          <div className="text-2xl font-semibold">{counts[info.type] ?? 0}</div>
          <div className="text-xs text-muted-foreground">{info.plural}</div>
        </button>
      ))}
    </div>
  );
}
