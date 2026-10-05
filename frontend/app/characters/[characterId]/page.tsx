"use client";

import { use, useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Pencil,
  Trash2,
  WandSparkles,
} from "lucide-react";
import { useRouter } from "next/navigation";

import { AppShell } from "@/components/layout/AppShell";
import { EditCharacterDialog } from "@/components/characters/EditCharacterDialog";
import { Button } from "@/components/ui/button";

import {
  deleteCharacter,
  getCharacter,
  updateCharacter,
} from "@/lib/characters/characters-store";

import type { Character } from "@/lib/characters/types";

import { useIsClient } from "@/lib/use-is-client";

type CharacterDetailPageProps = {
  params: Promise<{
    characterId: string;
  }>;
};

export default function CharacterDetailPage({
  params,
}: CharacterDetailPageProps) {
  const { characterId } = use(params);
  const isClient = useIsClient();

  return (
    <CharacterDetailContent
      key={`${isClient ? "client" : "server"}:${characterId}`}
      characterId={characterId}
      loaded={isClient}
    />
  );
}

function CharacterDetailContent({
  characterId,
  loaded,
}: {
  characterId: string;
  loaded: boolean;
}) {
  const router = useRouter();

  const [character, setCharacter] =
    useState<Character | undefined>(() =>
      loaded
        ? getCharacter(characterId)
        : undefined
    );

  const [editOpen, setEditOpen] =
    useState(false);

  if (!loaded) {
    return (
      <AppShell>
        <div className="py-16 text-center text-muted-foreground">
          Loading character...
        </div>
      </AppShell>
    );
  }

  if (!character) {
    return (
      <AppShell>
        <div className="py-16 text-center">
          <h1 className="text-2xl font-semibold">
            Character not found
          </h1>

          <p className="mt-2 text-muted-foreground">
            This character could not be found.
          </p>

          <Button
            className="mt-6"
            variant="outline"
            nativeButton={false}
            render={<Link href="/characters" />}
          >
            Back to Characters
          </Button>
        </div>
      </AppShell>
    );
  }

  const currentCharacter = character;

  function handleSave(data: {
    name: string;
    settingId: string;
    settingName: string;
    status: Character["status"];
    backstory: string;
  }) {
    const updated =
      updateCharacter(
        currentCharacter.id,
        data
      );

    if (updated) {
      setCharacter(updated);
    }
  }

  function handleDelete() {
    const confirmed =
      window.confirm(
        `Delete "${currentCharacter.name}"? This cannot be undone.`
      );

    if (!confirmed) {
      return;
    }

    const deleted =
      deleteCharacter(
        currentCharacter.id
      );

    if (deleted) {
      router.push("/characters");
    }
  }

  return (
    <AppShell>
      <div className="mb-6">
        <Button
          variant="ghost"
          nativeButton={false}
          render={
            <Link href="/characters" />
          }
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Characters
        </Button>
      </div>

      <div className="mx-auto max-w-5xl space-y-8">
        <header className="border-b border-border pb-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div className="mb-3">
                <span
                  className={[
                    "inline-flex rounded-full px-2 py-1 text-xs font-medium",
                    currentCharacter.status ===
                    "Complete"
                      ? "bg-primary/10 text-primary"
                      : "bg-muted text-muted-foreground",
                  ].join(" ")}
                >
                  {
                    currentCharacter.status
                  }
                </span>
              </div>

              <h1 className="font-heading text-4xl font-bold tracking-tight">
                {
                  currentCharacter.name
                }
              </h1>

              <p className="mt-3 text-lg text-muted-foreground">
                {
                  currentCharacter.settingName
                }
              </p>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                nativeButton={false}
                render={
                  <Link
                    href={`/builder?characterId=${currentCharacter.id}`}
                  />
                }
              >
                <WandSparkles className="h-4 w-4" />

                {currentCharacter.status ===
                "Draft"
                  ? "Continue Builder"
                  : "View Builder"}
              </Button>

              <Button
                variant="outline"
                onClick={() =>
                  setEditOpen(true)
                }
              >
                <Pencil className="h-4 w-4" />
                Edit Character
              </Button>

              <Button
                variant="destructive"
                onClick={handleDelete}
              >
                <Trash2 className="h-4 w-4" />
                Delete
              </Button>
            </div>
          </div>
        </header>

        <section className="grid gap-6 md:grid-cols-2">
          <div className="border border-border p-6">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Campaign Setting
            </p>

            <p className="mt-2 text-lg font-medium">
              {
                currentCharacter.settingName
              }
            </p>
          </div>

          <div className="border border-border p-6">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Builder Progress
            </p>

            <p className="mt-2 text-lg font-medium">
              Step{" "}
              {
                currentCharacter.currentStep
              }
            </p>
          </div>
        </section>

        <section className="border border-border p-6">
          <div className="flex items-center justify-between gap-4">
            <h2 className="font-heading text-xl font-semibold">
              Backstory
            </h2>

            <Button
              variant="ghost"
              onClick={() =>
                setEditOpen(true)
              }
            >
              <Pencil className="h-4 w-4" />
              Edit
            </Button>
          </div>

          {currentCharacter.backstory ? (
            <p className="mt-4 whitespace-pre-wrap leading-8">
              {
                currentCharacter.backstory
              }
            </p>
          ) : (
            <p className="mt-4 text-sm italic text-muted-foreground">
              No backstory has been written yet.
            </p>
          )}
        </section>

        <section className="border border-border p-6">
          <h2 className="font-heading text-xl font-semibold">
            Background Choices
          </h2>

          <p className="mt-2 text-sm text-muted-foreground">
            Homeland, culture,
            relationships, profession,
            life events, and other builder
            choices will appear here.
          </p>
        </section>

        <footer className="border-t border-border pt-6 text-sm text-muted-foreground">
          <p>
            Last updated{" "}
            {new Date(
              currentCharacter.updatedAt
            ).toLocaleDateString()}
          </p>
        </footer>
      </div>

      <EditCharacterDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        character={currentCharacter}
        onSave={handleSave}
      />
    </AppShell>
  );
}