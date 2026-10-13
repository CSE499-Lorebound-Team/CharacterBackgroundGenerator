"use client";

import { useState } from "react";

import { FormAlert } from "@/components/forms/FormAlert";
import { errorMessage } from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ApiError } from "@/lib/api";
import { plural } from "@/lib/format";
import { useApiFetch } from "@/lib/use-api-fetch";

type DeleteEntryDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  settingId: string;
  entry: { id: string; name: string };
  onDeleted: () => void;
};

/** What a 409 said is still attached to the entry. */
type Blockers = { relationshipCount: number; characterCount: number };

export function DeleteEntryDialog({
  open,
  onOpenChange,
  ...props
}: DeleteEntryDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DeleteEntryForm
          key={open ? "open" : "closed"}
          onOpenChange={onOpenChange}
          {...props}
        />
      </DialogContent>
    </Dialog>
  );
}

function DeleteEntryForm({
  onOpenChange,
  settingId,
  entry,
  onDeleted,
}: Omit<DeleteEntryDialogProps, "open">) {
  const fetchApi = useApiFetch();

  const [blockers, setBlockers] = useState<Blockers>();
  const [error, setError] = useState<string>();
  const [isDeleting, setIsDeleting] = useState(false);

  async function handleDelete(force: boolean) {
    setError(undefined);
    setIsDeleting(true);

    try {
      await fetchApi<void>(
        `/api/settings/${settingId}/entries/${entry.id}${force ? "?force=true" : ""}`,
        { method: "DELETE" }
      );
      onOpenChange(false);
      onDeleted();
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 409 && !force) {
        // Relationships or characters still use it; nothing was deleted.
        setBlockers({
          relationshipCount: Number(caught.problem?.relationshipCount ?? 0),
          characterCount: Number(caught.problem?.characterCount ?? 0),
        });
      } else {
        setError(errorMessage(caught));
      }
      setIsDeleting(false);
    }
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>Delete &ldquo;{entry.name}&rdquo;?</DialogTitle>
        <DialogDescription>
          {blockers
            ? "This entry is still in use."
            : "This cannot be undone."}
        </DialogDescription>
      </DialogHeader>

      <div className="space-y-4">
        {error && <FormAlert>{error}</FormAlert>}

        {blockers && (
          <FormAlert>
            It has {plural(blockers.relationshipCount, "relationship")} and is
            chosen by {plural(blockers.characterCount, "character")}. Deleting it
            anyway also deletes its relationships; those characters keep their
            other choices, and this one shows as removed.
          </FormAlert>
        )}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={isDeleting}
            onClick={() => handleDelete(Boolean(blockers))}
          >
            {isDeleting ? "Deleting..." : blockers ? "Delete Anyway" : "Delete Entry"}
          </Button>
        </div>
      </div>
    </>
  );
}
