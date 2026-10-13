"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, Pencil, Trash2, WandSparkles } from "lucide-react";

import { ReadOnlyBadge, StatusBadge } from "@/components/characters/CharacterBadges";
import { ChoiceSummary } from "@/components/characters/ChoiceSummary";
import { EditCharacterDialog } from "@/components/characters/EditCharacterDialog";
import { ConfirmDialog } from "@/components/forms/ConfirmDialog";
import { FormAlert } from "@/components/forms/FormAlert";
import { AppShell } from "@/components/layout/AppShell";
import { useToast } from "@/components/layout/Toaster";
import { EmptyState, ErrorState, LoadingCards } from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { choiceRows, useBuilderSteps } from "@/lib/builder-steps";
import { formatRelativeTime } from "@/lib/format";
import type { BuilderStepDto, CharacterDetailDto } from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";

export default function CharacterPage({
  params,
}: PageProps<"/characters/[characterId]">) {
  const { characterId } = use(params);

  const character = useApiData<CharacterDetailDto>(`/api/characters/${characterId}`);
  const steps = useBuilderSteps();

  const backLink = (
    <Button variant="ghost" nativeButton={false} render={<Link href="/characters" />}>
      <ArrowLeft className="h-4 w-4" />
      Back to Characters
    </Button>
  );

  if (character.error instanceof ApiError && character.error.status === 404) {
    return (
      <AppShell>
        <EmptyState
          title="Character not found"
          description="This character does not exist, or you cannot see it."
          action={backLink}
        />
      </AppShell>
    );
  }

  const error = character.error ?? steps.error;

  return (
    <AppShell>
      <div className="mb-6">{backLink}</div>

      {error ? (
        <ErrorState
          error={error}
          onRetry={() => {
            character.reload();
            steps.reload();
          }}
        />
      ) : !character.data || !steps.data ? (
        <LoadingCards count={2} />
      ) : (
        <CharacterSheet
          character={character.data}
          steps={steps.data.steps}
          onChanged={character.reload}
        />
      )}
    </AppShell>
  );
}

type CharacterSheetProps = {
  character: CharacterDetailDto;
  steps: BuilderStepDto[];
  onChanged: () => void;
};

function CharacterSheet({ character, steps, onChanged }: CharacterSheetProps) {
  const router = useRouter();
  const fetchApi = useApiFetch();
  const toast = useToast();
  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  // Only the owner, while still a member, edits or builds; GameMasters
  // reading a Player's character see it read-only too.
  const canEdit = character.isOwner && !character.isReadOnly;

  return (
    <div className="mx-auto max-w-5xl space-y-8">
      <header className="flex flex-col gap-4 border-b border-border pb-6 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="mb-3 flex flex-wrap gap-2">
            <StatusBadge status={character.status} />
            {character.isReadOnly && <ReadOnlyBadge />}
          </div>
          <h1 className="font-heading text-4xl font-bold tracking-tight">
            {character.name}
          </h1>
          <p className="mt-3 text-lg text-muted-foreground">
            <Link href={`/settings/${character.settingId}`} className="hover:underline">
              {character.settingName}
            </Link>
            {!character.isOwner && ` · played by ${character.ownerDisplayName}`}
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          {canEdit && (
            <>
              <Button
                variant="outline"
                nativeButton={false}
                render={<Link href={`/builder/${character.id}`} />}
              >
                <WandSparkles className="h-4 w-4" />
                {character.status === "Draft" ? "Continue Building" : "Open Builder"}
              </Button>
              <Button variant="outline" onClick={() => setEditOpen(true)}>
                <Pencil className="h-4 w-4" />
                Edit
              </Button>
            </>
          )}
          {character.isOwner && (
            <Button variant="destructive" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="h-4 w-4" />
              Delete
            </Button>
          )}
        </div>
      </header>

      {character.isReadOnly && character.isOwner && (
        <FormAlert>
          You are no longer a member of {character.settingName}, so this
          character is read-only. You can still delete it.
        </FormAlert>
      )}

      <section className="border border-border p-6">
        <h2 className="font-heading text-xl font-semibold">Background</h2>
        <div className="mt-6">
          <ChoiceSummary rows={choiceRows(steps, character)} columns />
        </div>
      </section>

      <section className="border border-border p-6">
        <div className="flex items-center justify-between gap-4">
          <h2 className="font-heading text-xl font-semibold">Backstory</h2>
          {canEdit && (
            <Button variant="ghost" onClick={() => setEditOpen(true)}>
              <Pencil className="h-4 w-4" />
              Edit
            </Button>
          )}
        </div>

        {/* Plain text from the API; rendered as text, never as HTML. */}
        {character.backstory ? (
          <p className="mt-4 whitespace-pre-wrap leading-8">{character.backstory}</p>
        ) : (
          <p className="mt-4 text-sm italic text-muted-foreground">
            No backstory has been written yet.
          </p>
        )}
      </section>

      <footer className="border-t border-border pt-6 text-sm text-muted-foreground">
        Updated {formatRelativeTime(character.updatedAt)}
      </footer>

      {canEdit && (
        <EditCharacterDialog
          open={editOpen}
          onOpenChange={setEditOpen}
          character={character}
          onSaved={() => {
            onChanged();
            toast("Character saved");
          }}
        />
      )}

      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title={`Delete ${character.name}?`}
        description="Their answers and backstory are deleted too. This cannot be undone."
        confirmLabel="Delete Character"
        onConfirm={async () => {
          await fetchApi<void>(`/api/characters/${character.id}`, { method: "DELETE" });
          toast(`Deleted ${character.name}`);
          router.push("/characters");
        }}
      />
    </div>
  );
}
