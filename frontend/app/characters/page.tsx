"use client";

import { use, useState } from "react";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";

import { CharacterListCard } from "@/components/characters/CharacterListCard";
import { CharacterSearch } from "@/components/characters/CharacterSearch";
import { CreateCharacterDialog } from "@/components/characters/CreateCharacterDialog";
import { ConfirmDialog } from "@/components/forms/ConfirmDialog";
import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import {
  EmptyState,
  ErrorState,
  LoadingCards,
  LoadMore,
} from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import type { CharacterListItemDto, CharacterStatus } from "@/lib/types";
import { usePagedList } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { cn } from "@/lib/utils";

type StatusFilter = "All" | CharacterStatus;

const statusFilters: { value: StatusFilter; label: string }[] = [
  { value: "All", label: "All" },
  { value: "Draft", label: "Drafts" },
  { value: "Complete", label: "Complete" },
];

export default function CharactersPage({
  searchParams,
}: PageProps<"/characters">) {
  const router = useRouter();
  const fetchApi = useApiFetch();
  // ?create=1 (the dashboard's Create Character) opens the dialog at once.
  const { create } = use(searchParams);

  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<StatusFilter>("All");
  const [createOpen, setCreateOpen] = useState(create === "1");
  const [deleting, setDeleting] = useState<CharacterListItemDto>();
  const [deleteOpen, setDeleteOpen] = useState(false);

  const query = useDebouncedValue(search.trim());

  const params = new URLSearchParams();
  if (query) params.set("search", query);
  if (status !== "All") params.set("status", status);
  const path = `/api/characters${params.size > 0 ? `?${params}` : ""}`;

  const characters = usePagedList<CharacterListItemDto>(path);
  const filtered = Boolean(query) || status !== "All";

  const createButton = (
    <Button onClick={() => setCreateOpen(true)}>
      <Plus className="h-4 w-4" />
      Create Character
    </Button>
  );

  return (
    <AppShell>
      <PageHeader
        title="Characters"
        description="Your character backgrounds, across all your settings."
        actions={createButton}
      />

      <div className="space-y-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <CharacterSearch value={search} onChange={setSearch} />

          <div role="group" aria-label="Filter by status" className="flex gap-1">
            {statusFilters.map((filter) => (
              <Button
                key={filter.value}
                size="sm"
                variant={status === filter.value ? "default" : "outline"}
                aria-pressed={status === filter.value}
                onClick={() => setStatus(filter.value)}
              >
                {filter.label}
              </Button>
            ))}
          </div>
        </div>

        {characters.error ? (
          <ErrorState error={characters.error} onRetry={characters.reload} />
        ) : !characters.hasLoaded ? (
          <LoadingCards />
        ) : characters.items.length === 0 ? (
          filtered ? (
            <EmptyState
              title="No characters match"
              description="Try another search or status."
            />
          ) : (
            <EmptyState
              title="No characters yet"
              description="Create a character to begin building their background."
              action={createButton}
            />
          )
        ) : (
          <div
            className={cn("space-y-4 transition-opacity", characters.isLoading && "opacity-60")}
            aria-busy={characters.isLoading}
          >
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {characters.items.map((character) => (
                <CharacterListCard
                  key={character.id}
                  character={character}
                  onDelete={() => {
                    setDeleting(character);
                    setDeleteOpen(true);
                  }}
                />
              ))}
            </div>

            {characters.hasMore && (
              <LoadMore
                shown={characters.items.length}
                total={characters.totalCount}
                loading={characters.loadingMore}
                onLoadMore={characters.loadMore}
              />
            )}
          </div>
        )}
      </div>

      <CreateCharacterDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={(character) => {
          setCreateOpen(false);
          router.push(`/builder/${character.id}`);
        }}
      />

      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title={`Delete ${deleting?.name ?? "this character"}?`}
        description="Their answers and backstory are deleted too. This cannot be undone."
        confirmLabel="Delete Character"
        onConfirm={async () => {
          if (!deleting) return;
          await fetchApi<void>(`/api/characters/${deleting.id}`, { method: "DELETE" });
          characters.reload();
        }}
      />
    </AppShell>
  );
}
