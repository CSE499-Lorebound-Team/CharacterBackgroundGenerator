"use client";

import { use, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Plus } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { useToast } from "@/components/layout/Toaster";
import { PageHeader } from "@/components/layout/PageHeader";
import {
  EmptyState,
  ErrorState,
  LoadingCards,
  LoadMore,
} from "@/components/layout/PageStates";
import { EntrySearch } from "@/components/settings/EntrySearch";
import { RoleBadge } from "@/components/settings/RoleBadge";
import { SettingEntryDialog } from "@/components/settings/SettingEntryDialog";
import { SettingInvites } from "@/components/settings/SettingInvites";
import { SettingMembers } from "@/components/settings/SettingMembers";
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

function SettingDetail({ setting, onSettingChanged }: SettingDetailProps) {
  const isGm = setting.myRole === "GameMaster";
  const [addOpen, setAddOpen] = useState(false);
  const toast = useToast();
  const [section, setSection] = useState<"lore" | "members">("lore");

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

  // Only GameMasters may add entries (the API answers 403 to Players).
  const addButton = isGm ? (
    <Button onClick={() => setAddOpen(true)}>
      <Plus className="h-4 w-4" />
      Add Entry
    </Button>
  ) : undefined;

  return (
    <>
      <PageHeader
        title={setting.name}
        description={setting.description ?? undefined}
        actions={section === "lore" ? addButton : undefined}
      />

      <div className="-mt-3 mb-6 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-muted-foreground">
        <RoleBadge role={setting.myRole} isOwner={setting.isOwner} />
        <span>Run by {setting.ownerDisplayName}</span>
        <span>{plural(setting.memberCount, "member")}</span>
        <span>{plural(totalEntries, "entry", "entries")}</span>
        <span>Updated {formatRelativeTime(setting.updatedAt)}</span>
      </div>

      <div role="tablist" aria-label="Setting sections" className="mb-6 flex gap-1">
        {(["lore", "members"] as const).map((value) => (
          <Button
            key={value}
            role="tab"
            aria-selected={section === value}
            size="sm"
            variant={section === value ? "default" : "outline"}
            onClick={() => setSection(value)}
          >
            {value === "lore" ? "Lore" : "Members"}
          </Button>
        ))}
      </div>

      {section === "members" ? (
        <div className="space-y-10">
          <SettingMembers setting={setting} onChanged={onSettingChanged} />
          {/* Players get 403 on invites, so the section is not shown. */}
          {isGm && <SettingInvites settingId={setting.id} />}
        </div>
      ) : (
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
              action={query ? undefined : addButton}
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
      )}

      {isGm && (
        <SettingEntryDialog
          open={addOpen}
          onOpenChange={setAddOpen}
          settingId={setting.id}
          defaultType={activeTab === "Overview" ? undefined : activeTab}
          onSaved={(entry) => {
            entries.reload();
            onSettingChanged();
            toast(`Added ${entry.name}`);
          }}
        />
      )}
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
