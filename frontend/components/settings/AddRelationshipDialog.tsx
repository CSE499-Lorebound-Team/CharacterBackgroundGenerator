"use client";

import { type FormEvent, useState } from "react";
import { Search } from "lucide-react";

import { FormAlert } from "@/components/forms/FormAlert";
import { FormField } from "@/components/forms/FormField";
import { errorMessage } from "@/components/layout/PageStates";
import { TypeBadge } from "@/components/settings/EntryBadges";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import {
  type FieldErrors,
  hasErrors,
  readValidationErrors,
} from "@/lib/form-errors";
import type {
  CreateRelationshipRequest,
  EntryListItemDto,
  PagedResult,
  RelationshipDto,
  RelationshipTypesDto,
} from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { cn } from "@/lib/utils";

// CreateRelationshipRequest limits in the API.
const TYPE_MAX = 60;
const DESCRIPTION_MAX = 1000;

type Direction = "outgoing" | "incoming";

type AddRelationshipDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  settingId: string;
  /** The entry whose page this is. */
  entry: { id: string; name: string };
  onAdded: () => void;
};

/** Links the current entry to another one (GameMasters only). */
export function AddRelationshipDialog({
  open,
  onOpenChange,
  ...props
}: AddRelationshipDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <AddRelationshipForm
          key={open ? "open" : "closed"}
          onOpenChange={onOpenChange}
          {...props}
        />
      </DialogContent>
    </Dialog>
  );
}

function AddRelationshipForm({
  onOpenChange,
  settingId,
  entry,
  onAdded,
}: Omit<AddRelationshipDialogProps, "open">) {
  const fetchApi = useApiFetch();

  const [direction, setDirection] = useState<Direction>("outgoing");
  const [other, setOther] = useState<EntryListItemDto>();
  const [search, setSearch] = useState("");
  const [relationshipType, setRelationshipType] = useState("");
  const [description, setDescription] = useState("");

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const query = useDebouncedValue(search.trim());
  const candidates = useApiData<PagedResult<EntryListItemDto>>(
    `/api/settings/${settingId}/entries?pageSize=20${query ? `&search=${encodeURIComponent(query)}` : ""}`
  );
  const suggestions = useApiData<RelationshipTypesDto>("/api/relationship-types");

  // An entry cannot be linked to itself (the API answers 400).
  const options = (candidates.data?.items ?? []).filter(
    (candidate) => candidate.id !== entry.id
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const errors: FieldErrors = {
      other: other ? undefined : "Choose the entry to link to.",
      relationshipType: relationshipType.trim()
        ? undefined
        : "Enter or pick a relationship type.",
    };

    setFieldErrors(errors);
    if (hasErrors(errors) || !other) return;

    setIsSubmitting(true);

    const request: CreateRelationshipRequest = {
      sourceEntryId: direction === "outgoing" ? entry.id : other.id,
      targetEntryId: direction === "outgoing" ? other.id : entry.id,
      relationshipType: relationshipType.trim(),
      description: description.trim() || null,
    };

    try {
      await fetchApi<RelationshipDto>(`/api/settings/${settingId}/relationships`, {
        method: "POST",
        json: request,
      });
      onOpenChange(false);
      onAdded();
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, [
          "relationshipType",
          "description",
        ]);
        setFieldErrors(result.fieldErrors);
        setFormError(result.general);
      } else if (error instanceof ApiError && error.status === 409) {
        setFieldErrors({
          relationshipType:
            error.detail ?? "These entries already have this relationship.",
        });
      } else {
        setFormError(errorMessage(error));
      }
      setIsSubmitting(false);
    }
  }

  const otherName = other?.name ?? "the other entry";

  return (
    <>
      <DialogHeader>
        <DialogTitle>Add Relationship</DialogTitle>
        <DialogDescription>
          Link &ldquo;{entry.name}&rdquo; to another entry in this setting.
        </DialogDescription>
      </DialogHeader>

      <form onSubmit={handleSubmit} noValidate className="space-y-5">
        {formError && <FormAlert>{formError}</FormAlert>}

        <div className="space-y-2">
          <Label htmlFor="relationship-search">Other entry</Label>
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="relationship-search"
              className="pl-9"
              placeholder="Search entries by name or description..."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>

          <div
            role="listbox"
            aria-label="Entries"
            className="max-h-56 overflow-y-auto border border-border"
          >
            {candidates.error ? (
              <p className="p-3 text-sm text-destructive">
                {errorMessage(candidates.error)}
              </p>
            ) : !candidates.data ? (
              <p className="p-3 text-sm text-muted-foreground">Loading entries...</p>
            ) : options.length === 0 ? (
              <p className="p-3 text-sm text-muted-foreground">
                {query ? "No entries match." : "There are no other entries yet."}
              </p>
            ) : (
              options.map((candidate) => (
                <button
                  key={candidate.id}
                  type="button"
                  role="option"
                  aria-selected={other?.id === candidate.id}
                  onClick={() => setOther(candidate)}
                  className={cn(
                    "flex w-full items-center justify-between gap-3 border-b border-border px-3 py-2 text-left text-sm last:border-b-0 hover:bg-muted",
                    other?.id === candidate.id && "bg-primary/10"
                  )}
                >
                  <span className="truncate font-medium">{candidate.name}</span>
                  <TypeBadge type={candidate.entryType} />
                </button>
              ))
            )}
          </div>
          {fieldErrors.other && (
            <p className="text-sm text-destructive">{fieldErrors.other}</p>
          )}
        </div>

        <fieldset className="space-y-2">
          <legend className="text-sm font-medium">Direction</legend>
          <div className="flex flex-col gap-2 text-sm">
            {(["outgoing", "incoming"] as const).map((value) => (
              <label key={value} className="flex items-center gap-2">
                <input
                  type="radio"
                  name="direction"
                  value={value}
                  checked={direction === value}
                  onChange={() => setDirection(value)}
                  className="accent-primary"
                />
                {value === "outgoing"
                  ? `${entry.name} → ${otherName}`
                  : `${otherName} → ${entry.name}`}
              </label>
            ))}
          </div>
        </fieldset>

        <FormField
          id="relationship-type"
          label="Relationship type"
          placeholder="e.g. allied with, located in, worships"
          list="relationship-type-suggestions"
          maxLength={TYPE_MAX}
          value={relationshipType}
          onChange={(event) => setRelationshipType(event.target.value)}
          error={fieldErrors.relationshipType}
          hint="Pick a suggestion or type your own."
        />
        <datalist id="relationship-type-suggestions">
          {suggestions.data?.suggested.map((type) => (
            <option key={type} value={type} />
          ))}
        </datalist>

        <div className="space-y-2">
          <Label htmlFor="relationship-description">Description (optional)</Label>
          <Textarea
            id="relationship-description"
            maxLength={DESCRIPTION_MAX}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            aria-invalid={fieldErrors.description ? true : undefined}
          />
          {fieldErrors.description && (
            <p className="text-sm text-destructive">{fieldErrors.description}</p>
          )}
        </div>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Adding..." : "Add Relationship"}
          </Button>
        </div>
      </form>
    </>
  );
}
