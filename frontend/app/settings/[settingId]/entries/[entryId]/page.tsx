"use client";

import { use, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Pencil, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";

import { AppShell } from "@/components/layout/AppShell";
import { SettingEntryDialog } from "@/components/settings/SettingEntryDialog";
import { Button } from "@/components/ui/button";

import {
  deleteEntry,
  duplicateNameError,
  getEntry,
  updateEntry,
} from "@/lib/settings/entries-store";
import { getSetting } from "@/lib/settings/settings-store";
import { useIsClient } from "@/lib/use-is-client";

import type {
  Setting,
  SettingEntry,
} from "@/lib/settings/types";

type ArticlePageProps = {
  params: Promise<{
    settingId: string;
    entryId: string;
  }>;
};

export default function ArticlePage({
  params,
}: ArticlePageProps) {
  const { settingId, entryId } = use(params);
  const isClient = useIsClient();

  // Remount once in the browser (and per article) so the content can read
  // localStorage.
  return (
    <ArticleContent
      key={`${isClient ? "client" : "server"}:${settingId}:${entryId}`}
      settingId={settingId}
      entryId={entryId}
      loaded={isClient}
    />
  );
}

function ArticleContent({
  settingId,
  entryId,
  loaded,
}: {
  settingId: string;
  entryId: string;
  loaded: boolean;
}) {
  const router = useRouter();

  const [setting] = useState<Setting | undefined>(() =>
    loaded ? getSetting(settingId) : undefined
  );
  const [entry, setEntry] = useState<SettingEntry | undefined>(() => {
    if (!loaded) {
      return undefined;
    }

    const foundEntry = getEntry(entryId);

    return foundEntry && foundEntry.settingId === settingId
      ? foundEntry
      : undefined;
  });
  const [editOpen, setEditOpen] = useState(false);

  if (!loaded) {
    return (
      <AppShell>
        <div className="py-16 text-center text-muted-foreground">
          Loading article...
        </div>
      </AppShell>
    );
  }

  // A Player gets the same "not found" for a GM-only entry as for a missing
  // one, so they cannot tell that secret lore exists (the API answers 404).
  const hiddenFromPlayer =
    setting?.role === "Player" && entry?.isGmOnly;

  if (!setting || !entry || hiddenFromPlayer) {
    return (
      <AppShell>
        <div className="py-16 text-center">
          <h1 className="text-2xl font-semibold">
            Article not found
          </h1>
  
          <p className="mt-2 text-muted-foreground">
            This lore article could not be found.
          </p>
  
          <Button
            className="mt-6"
            variant="outline"
            nativeButton={false}
            render={
              <Link href={`/settings/${settingId}`} />
            }
          >
            Back to Setting
          </Button>
        </div>
      </AppShell>
    );
  }
  
  const currentSetting = setting;
  const currentEntry = entry;
  
  const canManageSetting =
    currentSetting.role === "GM";

  function handleSave(data: {
    name: string;
    type: SettingEntry["type"];
    summary: string;
    content: string;
    isGmOnly: boolean;
  }) {
    const duplicate = duplicateNameError(
      settingId,
      data.type,
      data.name,
      currentEntry.id
    );

    if (duplicate) {
      return duplicate;
    }

    const updated = updateEntry(
      currentEntry.id,
      data
    );
  
    if (updated) {
      setEntry(updated);
    }
  }

  function handleDelete() {
    const confirmed = window.confirm(
      `Delete "${currentEntry.name}"? This cannot be undone.`
    );
  
    if (!confirmed) {
      return;
    }
  
    const deleted = deleteEntry(
      currentEntry.id
    );
  
    if (deleted) {
      router.push(`/settings/${settingId}`);
    }
  }

  return (
    <AppShell>
      <div className="mb-6">
        <Button
          variant="ghost"
          nativeButton={false}
          render={
            <Link href={`/settings/${settingId}`} />
          }
        >
          <ArrowLeft className="h-4 w-4" />
          Back to {setting.name}
        </Button>
      </div>

      <article className="mx-auto max-w-4xl">
        <header className="border-b border-border pb-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div className="mb-3 flex flex-wrap gap-2">
                <span className="rounded-full bg-muted px-2 py-1 text-xs font-medium text-muted-foreground">
                  {entry.type}
                </span>

                {entry.isGmOnly && (
                  <span className="rounded-full bg-destructive/10 px-2 py-1 text-xs font-medium text-destructive">
                    GM Only
                  </span>
                )}
              </div>

              <h1 className="font-heading text-4xl font-bold tracking-tight">
                {entry.name}
              </h1>

              <p className="mt-4 text-lg leading-8 text-muted-foreground">
                {entry.summary}
              </p>
            </div>

            {canManageSetting && (
              <div className="flex shrink-0 gap-2">
                <Button
                  variant="outline"
                  onClick={() => setEditOpen(true)}
                >
                  <Pencil className="h-4 w-4" />
                  Edit
                </Button>

                <Button
                  variant="destructive"
                  onClick={handleDelete}
                >
                  <Trash2 className="h-4 w-4" />
                  Delete
                </Button>
              </div>
            )}
          </div>
        </header>

        <section className="py-8">
          {entry.content ? (
            <div className="whitespace-pre-wrap text-base leading-8">
              {entry.content}
            </div>
          ) : (
            <p className="italic text-muted-foreground">
              No additional lore has been written for this article yet.
            </p>
          )}
        </section>

        <section className="border-t border-border py-8">
          <h2 className="font-heading text-xl font-semibold">
            Relationships
          </h2>

          <p className="mt-2 text-sm text-muted-foreground">
            Relationships between lore entries will appear here.
          </p>
        </section>

        <footer className="border-t border-border py-6 text-sm text-muted-foreground">
          <p>
            Last updated{" "}
            {new Date(
              entry.updatedAt
            ).toLocaleDateString()}
          </p>
        </footer>
      </article>

      {canManageSetting && (
        <SettingEntryDialog
          open={editOpen}
          onOpenChange={setEditOpen}
          entry={entry}
          onSave={handleSave}
        />
      )}
    </AppShell>
  );
}