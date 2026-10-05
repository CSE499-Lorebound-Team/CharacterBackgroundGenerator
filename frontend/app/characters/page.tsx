"use client";

import { useMemo, useState } from "react";
import { Plus } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { CharacterSearch } from "@/components/characters/CharacterSearch";
import { CharacterListCard } from "@/components/characters/CharacterListCard";
import { CreateCharacterDialog } from "@/components/characters/CreateCharacterDialog";
import { Button } from "@/components/ui/button";

import {
  createCharacter,
  getCharacters,
} from "@/lib/characters/characters-store";

import type {
  Character,
  CharacterStatus,
} from "@/lib/characters/types";

import { useIsClient } from "@/lib/use-is-client";

type CharacterFilter =
  | "All"
  | CharacterStatus;

export default function CharactersPage() {
  const isClient = useIsClient();

  return (
    <CharactersPageContent
      key={isClient ? "client" : "server"}
      isClient={isClient}
    />
  );
}

function CharactersPageContent({
  isClient,
}: {
  isClient: boolean;
}) {
  const [characters, setCharacters] =
    useState<Character[]>(() =>
      isClient ? getCharacters() : []
    );

  const [search, setSearch] =
    useState("");

  const [filter, setFilter] =
    useState<CharacterFilter>("All");

  const [createOpen, setCreateOpen] =
    useState(false);

  const filteredCharacters =
    useMemo(() => {
      const query =
        search.trim().toLowerCase();

      return characters.filter(
        (character) => {
          const matchesSearch =
            !query ||
            character.name
              .toLowerCase()
              .includes(query) ||
            character.settingName
              .toLowerCase()
              .includes(query);

          const matchesFilter =
            filter === "All" ||
            character.status === filter;

          return (
            matchesSearch &&
            matchesFilter
          );
        }
      );
    }, [
      characters,
      search,
      filter,
    ]);

  function handleCreate(
    name: string,
    settingId: string,
    settingName: string
  ) {
    const character =
      createCharacter(
        name,
        settingId,
        settingName
      );

    setCharacters((current) => [
      ...current,
      character,
    ]);
  }

  return (
    <AppShell>
      <PageHeader
        title="Characters"
        description="Manage your saved character backgrounds."
        actions={
          <Button
            onClick={() =>
              setCreateOpen(true)
            }
          >
            <Plus className="h-4 w-4" />
            Create Character
          </Button>
        }
      />

      <div className="space-y-6">
        <CharacterSearch
          value={search}
          onChange={setSearch}
        />

        <div className="flex flex-wrap gap-2">
          {(
            [
              "All",
              "Draft",
              "Complete",
            ] as CharacterFilter[]
          ).map((option) => (
            <Button
              key={option}
              variant={
                filter === option
                  ? "secondary"
                  : "ghost"
              }
              onClick={() =>
                setFilter(option)
              }
            >
              {option === "Draft"
                ? "Drafts"
                : option}
            </Button>
          ))}
        </div>

        {filteredCharacters.length > 0 ? (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {filteredCharacters.map(
              (character) => (
                <CharacterListCard
                  key={character.id}
                  id={character.id}
                  name={character.name}
                  settingName={
                    character.settingName
                  }
                  status={character.status}
                />
              )
            )}
          </div>
        ) : (
          <div className="border border-dashed border-border p-10 text-center">
            <p className="font-medium">
              No characters found
            </p>

            <p className="mt-2 text-sm text-muted-foreground">
              Create a character to begin
              building their background.
            </p>

            <Button
              className="mt-4"
              onClick={() =>
                setCreateOpen(true)
              }
            >
              <Plus className="h-4 w-4" />
              Create Character
            </Button>
          </div>
        )}
      </div>

      <CreateCharacterDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreate={handleCreate}
      />
    </AppShell>
  );
}