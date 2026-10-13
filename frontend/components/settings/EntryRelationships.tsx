"use client";

import { useState } from "react";
import Link from "next/link";
import { ArrowLeft, ArrowRight, Plus, X } from "lucide-react";

import { FormAlert } from "@/components/forms/FormAlert";
import { errorMessage } from "@/components/layout/PageStates";
import { useToast } from "@/components/layout/Toaster";
import { AddRelationshipDialog } from "@/components/settings/AddRelationshipDialog";
import { TypeBadge } from "@/components/settings/EntryBadges";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import type { EntryDetailDto, EntryRelationshipDto } from "@/lib/types";
import { useApiFetch } from "@/lib/use-api-fetch";

type EntryRelationshipsProps = {
  settingId: string;
  entry: EntryDetailDto;
  /** GameMasters add and remove; Players only read. */
  isGm: boolean;
  onChanged: () => void;
};

type Removing = { relationship: EntryRelationshipDto; outgoing: boolean };

/** The entry's outgoing and incoming relationships, grouped by type. */
export function EntryRelationships({
  settingId,
  entry,
  isGm,
  onChanged,
}: EntryRelationshipsProps) {
  const [addOpen, setAddOpen] = useState(false);
  const toast = useToast();
  const [removing, setRemoving] = useState<Removing>();

  const isEmpty = entry.outgoing.length === 0 && entry.incoming.length === 0;

  return (
    <section className="border-t border-border py-8">
      <div className="flex items-center justify-between gap-4">
        <h2 className="font-heading text-xl font-semibold">Relationships</h2>
        {isGm && (
          <Button variant="outline" size="sm" onClick={() => setAddOpen(true)}>
            <Plus className="h-4 w-4" />
            Add Relationship
          </Button>
        )}
      </div>

      {isEmpty ? (
        <p className="mt-3 text-sm text-muted-foreground">
          {isGm
            ? "No relationships yet. Link this entry to the places, people and groups it is connected to."
            : "This entry has no relationships yet."}
        </p>
      ) : (
        <div className="mt-4 grid gap-6 md:grid-cols-2">
          <RelationshipList
            title={`${entry.name} →`}
            relationships={entry.outgoing}
            outgoing
            settingId={settingId}
            onRemove={isGm ? (relationship) => setRemoving({ relationship, outgoing: true }) : undefined}
          />
          <RelationshipList
            title={`→ ${entry.name}`}
            relationships={entry.incoming}
            outgoing={false}
            settingId={settingId}
            onRemove={isGm ? (relationship) => setRemoving({ relationship, outgoing: false }) : undefined}
          />
        </div>
      )}

      {isGm && (
        <>
          <AddRelationshipDialog
            open={addOpen}
            onOpenChange={setAddOpen}
            settingId={settingId}
            entry={entry}
            onAdded={() => {
              onChanged();
              toast("Relationship added");
            }}
          />
          <RemoveRelationshipDialog
            settingId={settingId}
            entryName={entry.name}
            removing={removing}
            onClose={() => setRemoving(undefined)}
            onRemoved={() => {
              onChanged();
              toast("Relationship removed");
            }}
          />
        </>
      )}
    </section>
  );
}

type RelationshipListProps = {
  title: string;
  relationships: EntryRelationshipDto[];
  outgoing: boolean;
  settingId: string;
  onRemove?: (relationship: EntryRelationshipDto) => void;
};

function RelationshipList({
  title,
  relationships,
  outgoing,
  settingId,
  onRemove,
}: RelationshipListProps) {
  // Group by type, keeping the API's order (sorted by the other entry's name).
  const groups = new Map<string, EntryRelationshipDto[]>();
  for (const relationship of relationships) {
    const group = groups.get(relationship.relationshipType) ?? [];
    group.push(relationship);
    groups.set(relationship.relationshipType, group);
  }
  const Arrow = outgoing ? ArrowRight : ArrowLeft;

  return (
    <div>
      <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
        {title}
      </h3>

      {relationships.length === 0 ? (
        <p className="mt-2 text-sm text-muted-foreground">None.</p>
      ) : (
        <div className="mt-2 space-y-4">
          {[...groups].map(([type, items]) => (
            <div key={type}>
              <p className="flex items-center gap-1.5 text-sm font-medium">
                <Arrow className="h-3.5 w-3.5 text-muted-foreground" />
                {type}
              </p>

              <ul className="mt-1 space-y-1">
                {items.map((relationship) => (
                  <li
                    key={relationship.id}
                    className="flex items-start justify-between gap-2 border border-border px-3 py-2 text-sm"
                  >
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <Link
                          href={`/settings/${settingId}/entries/${relationship.otherEntryId}`}
                          className="font-medium text-primary hover:underline"
                        >
                          {relationship.otherEntryName}
                        </Link>
                        <TypeBadge type={relationship.otherEntryType} />
                      </div>
                      {relationship.description && (
                        <p className="mt-1 text-muted-foreground">
                          {relationship.description}
                        </p>
                      )}
                    </div>

                    {onRemove && (
                      <button
                        type="button"
                        onClick={() => onRemove(relationship)}
                        className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-destructive"
                        aria-label={`Remove relationship with ${relationship.otherEntryName}`}
                        title="Remove"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

type RemoveRelationshipDialogProps = {
  settingId: string;
  entryName: string;
  removing?: Removing;
  onClose: () => void;
  onRemoved: () => void;
};

function RemoveRelationshipDialog({
  settingId,
  entryName,
  removing,
  onClose,
  onRemoved,
}: RemoveRelationshipDialogProps) {
  const fetchApi = useApiFetch();
  const [error, setError] = useState<string>();
  const [isRemoving, setIsRemoving] = useState(false);

  async function handleRemove() {
    if (!removing) return;
    setError(undefined);
    setIsRemoving(true);

    try {
      await fetchApi<void>(
        `/api/settings/${settingId}/relationships/${removing.relationship.id}`,
        { method: "DELETE" }
      );
      onClose();
      onRemoved();
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setIsRemoving(false);
    }
  }

  const relationship = removing?.relationship;
  const summary = relationship
    ? removing.outgoing
      ? `${entryName} ${relationship.relationshipType.toLowerCase()} ${relationship.otherEntryName}`
      : `${relationship.otherEntryName} ${relationship.relationshipType.toLowerCase()} ${entryName}`
    : "";

  return (
    <Dialog
      open={Boolean(removing)}
      onOpenChange={(open) => {
        if (!open) {
          setError(undefined);
          onClose();
        }
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Remove relationship?</DialogTitle>
          <DialogDescription>
            &ldquo;{summary}&rdquo; will be removed. Both entries stay.
          </DialogDescription>
        </DialogHeader>

        {error && <FormAlert>{error}</FormAlert>}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={isRemoving}
            onClick={handleRemove}
          >
            {isRemoving ? "Removing..." : "Remove"}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
