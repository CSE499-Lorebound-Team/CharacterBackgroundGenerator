"use client";

import { type FormEvent, useState } from "react";

import { FormAlert } from "@/components/forms/FormAlert";
import { FormField } from "@/components/forms/FormField";
import { errorMessage } from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import { ENTRY_TYPES } from "@/lib/entry-types";
import {
  type FieldErrors,
  hasErrors,
  readValidationErrors,
} from "@/lib/form-errors";
import type {
  CreateEntryRequest,
  SettingEntryDto,
  SettingEntryType,
} from "@/lib/types";
import { useApiFetch } from "@/lib/use-api-fetch";
import { cn } from "@/lib/utils";

// CreateEntryRequest limits in the API.
const NAME_MAX = 120;
const DESCRIPTION_MAX = 4000;

const typeItems = ENTRY_TYPES.map((info) => ({ value: info.type, label: info.label }));

/** The fields of an entry being edited. */
export type EditableEntry = {
  id: string;
  name: string;
  entryType: SettingEntryType;
  description: string | null;
  isGmOnly?: boolean;
};

type SettingEntryDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  settingId: string;
  /** The entry to edit; omit to create one. */
  entry?: EditableEntry;
  /** Type preselected when creating, e.g. the open tab. */
  defaultType?: SettingEntryType;
  onSaved: (entry: SettingEntryDto) => void;
};

/** Create and edit a lore entry (GameMasters only). */
export function SettingEntryDialog({
  open,
  onOpenChange,
  ...formProps
}: SettingEntryDialogProps) {
  // The Dialog stays mounted so its animation plays; the form is keyed so
  // its fields reset whenever the dialog opens or the entry changes.
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <SettingEntryForm
          key={`${open ? "open" : "closed"}:${formProps.entry?.id ?? "new"}`}
          onOpenChange={onOpenChange}
          {...formProps}
        />
      </DialogContent>
    </Dialog>
  );
}

function SettingEntryForm({
  onOpenChange,
  settingId,
  entry,
  defaultType,
  onSaved,
}: Omit<SettingEntryDialogProps, "open">) {
  const fetchApi = useApiFetch();
  const isEditing = Boolean(entry);

  const [name, setName] = useState(entry?.name ?? "");
  const [entryType, setEntryType] = useState<SettingEntryType>(
    entry?.entryType ?? defaultType ?? "Location"
  );
  const [description, setDescription] = useState(entry?.description ?? "");
  const [isGmOnly, setIsGmOnly] = useState(entry?.isGmOnly ?? false);

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const errors: FieldErrors = {
      name: name.trim() ? undefined : "Enter a name.",
      description:
        description.length > DESCRIPTION_MAX
          ? `Use at most ${DESCRIPTION_MAX} characters.`
          : undefined,
    };

    setFieldErrors(errors);
    if (hasErrors(errors)) return;

    setIsSubmitting(true);

    const request: CreateEntryRequest = {
      name: name.trim(),
      entryType,
      description: description.trim() || null,
      isGmOnly,
    };

    try {
      const saved = await fetchApi<SettingEntryDto>(
        entry
          ? `/api/settings/${settingId}/entries/${entry.id}`
          : `/api/settings/${settingId}/entries`,
        { method: entry ? "PUT" : "POST", json: request }
      );

      onSaved(saved);
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, [
          "name",
          "entryType",
          "description",
        ]);
        setFieldErrors(result.fieldErrors);
        setFormError(result.general);
      } else if (error instanceof ApiError && error.status === 409) {
        // Names are unique per type within a setting, ignoring case.
        setFieldErrors({
          name: error.detail ?? "An entry of this type already has this name.",
        });
      } else {
        setFormError(errorMessage(error));
      }

      setIsSubmitting(false);
    }
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>{isEditing ? "Edit Lore Entry" : "Add Lore Entry"}</DialogTitle>
        <DialogDescription>
          {isEditing
            ? "Update this entry's name, type, description or visibility."
            : "Add a location, culture, person or other piece of lore to this setting."}
        </DialogDescription>
      </DialogHeader>

      <form onSubmit={handleSubmit} noValidate className="space-y-5">
        {formError && <FormAlert>{formError}</FormAlert>}

        <FormField
          id="entry-name"
          label="Name"
          placeholder="Kingdom of Valmere"
          maxLength={NAME_MAX}
          value={name}
          onChange={(event) => setName(event.target.value)}
          error={fieldErrors.name}
        />

        <div className="space-y-2">
          <Label id="entry-type-label">Type</Label>
          <Select
            items={typeItems}
            value={entryType}
            onValueChange={(value) => {
              if (value) setEntryType(value as SettingEntryType);
            }}
          >
            <SelectTrigger className="w-full" aria-labelledby="entry-type-label">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {typeItems.map((item) => (
                <SelectItem key={item.value} value={item.value}>
                  {item.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {fieldErrors.entryType && (
            <p className="text-sm text-destructive">{fieldErrors.entryType}</p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="entry-description">Description</Label>
          <Textarea
            id="entry-description"
            placeholder="What players and the builder should know about this entry..."
            className="min-h-40"
            maxLength={DESCRIPTION_MAX}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            aria-invalid={fieldErrors.description ? true : undefined}
          />
          {fieldErrors.description && (
            <p className="text-sm text-destructive">{fieldErrors.description}</p>
          )}
        </div>

        <div className="flex items-start justify-between gap-4 border border-border p-4">
          <div>
            <Label htmlFor="entry-gm-only">GM Only</Label>
            <p className="mt-1 text-sm text-muted-foreground">
              Hide this entry from players. They cannot see it, search for it,
              or pick it in the builder.
            </p>
          </div>

          <button
            id="entry-gm-only"
            type="button"
            role="switch"
            aria-checked={isGmOnly}
            onClick={() => setIsGmOnly((value) => !value)}
            className={cn(
              "relative mt-1 inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors focus-visible:outline-2 focus-visible:outline-ring",
              isGmOnly ? "bg-primary" : "bg-muted-foreground/30"
            )}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 rounded-full bg-background shadow transition-transform",
                isGmOnly ? "translate-x-4.5" : "translate-x-0.5"
              )}
            />
          </button>
        </div>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Saving..." : isEditing ? "Save Changes" : "Add Entry"}
          </Button>
        </div>
      </form>
    </>
  );
}
