"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, Pencil, Trash2 } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { EmptyState, ErrorState, LoadingCards } from "@/components/layout/PageStates";
import { DeleteEntryDialog } from "@/components/settings/DeleteEntryDialog";
import { GmOnlyBadge, TypeBadge } from "@/components/settings/EntryBadges";
import { SettingEntryDialog } from "@/components/settings/SettingEntryDialog";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { formatRelativeTime } from "@/lib/format";
import type { EntryDetailDto, SettingDetailDto } from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";

export default function EntryPage({
  params,
}: PageProps<"/settings/[settingId]/entries/[entryId]">) {
  const { settingId, entryId } = use(params);

  const setting = useApiData<SettingDetailDto>(`/api/settings/${settingId}`);
  const entry = useApiData<EntryDetailDto>(
    `/api/settings/${settingId}/entries/${entryId}`
  );

  const backLink = (
    <Button
      variant="ghost"
      nativeButton={false}
      render={<Link href={`/settings/${settingId}`} />}
    >
      <ArrowLeft className="h-4 w-4" />
      Back to {setting.data?.name ?? "Setting"}
    </Button>
  );

  // A Player gets the same 404 for a GM-only entry as for a missing one.
  const notFound = [entry.error, setting.error].some(
    (error) => error instanceof ApiError && error.status === 404
  );

  if (notFound) {
    return (
      <AppShell>
        <EmptyState
          title="Entry not found"
          description="This lore entry does not exist, or you cannot see it."
          action={backLink}
        />
      </AppShell>
    );
  }

  const error = entry.error ?? setting.error;

  return (
    <AppShell>
      <div className="mb-6">{backLink}</div>

      {error ? (
        <ErrorState
          error={error}
          onRetry={() => {
            entry.reload();
            setting.reload();
          }}
        />
      ) : !entry.data || !setting.data ? (
        <LoadingCards count={2} />
      ) : (
        <EntryArticle
          settingId={settingId}
          entry={entry.data}
          isGm={setting.data.myRole === "GameMaster"}
          onChanged={entry.reload}
        />
      )}
    </AppShell>
  );
}

type EntryArticleProps = {
  settingId: string;
  entry: EntryDetailDto;
  isGm: boolean;
  onChanged: () => void;
};

function EntryArticle({ settingId, entry, isGm, onChanged }: EntryArticleProps) {
  const router = useRouter();
  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  return (
    <article className="mx-auto max-w-4xl">
      <header className="flex flex-col gap-4 border-b border-border pb-6 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="mb-3 flex flex-wrap gap-2">
            <TypeBadge type={entry.entryType} />
            {entry.isGmOnly && <GmOnlyBadge />}
          </div>

          <h1 className="font-heading text-4xl font-bold tracking-tight">
            {entry.name}
          </h1>
        </div>

        {/* Players never see edit or delete; the API refuses them anyway. */}
        {isGm && (
          <div className="flex shrink-0 gap-2">
            <Button variant="outline" onClick={() => setEditOpen(true)}>
              <Pencil className="h-4 w-4" />
              Edit
            </Button>
            <Button variant="destructive" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="h-4 w-4" />
              Delete
            </Button>
          </div>
        )}
      </header>

      <section className="py-8">
        {entry.description ? (
          <div className="whitespace-pre-wrap text-base leading-8">
            {entry.description}
          </div>
        ) : (
          <p className="italic text-muted-foreground">
            No description has been written for this entry yet.
          </p>
        )}
      </section>

      <footer className="border-t border-border py-6 text-sm text-muted-foreground">
        Updated {formatRelativeTime(entry.updatedAt)}
      </footer>

      {isGm && (
        <>
          <SettingEntryDialog
            open={editOpen}
            onOpenChange={setEditOpen}
            settingId={settingId}
            entry={entry}
            onSaved={onChanged}
          />
          <DeleteEntryDialog
            open={deleteOpen}
            onOpenChange={setDeleteOpen}
            settingId={settingId}
            entry={entry}
            onDeleted={() => router.push(`/settings/${settingId}`)}
          />
        </>
      )}
    </article>
  );
}
