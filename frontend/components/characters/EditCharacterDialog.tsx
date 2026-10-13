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
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import {
  type FieldErrors,
  hasErrors,
  readValidationErrors,
} from "@/lib/form-errors";
import type { CharacterDetailDto, UpdateCharacterRequest } from "@/lib/types";
import { useApiFetch } from "@/lib/use-api-fetch";

// UpdateCharacterRequest limits in the API.
const NAME_MAX = 100;
export const BACKSTORY_MAX = 10000;

type EditCharacterDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  character: CharacterDetailDto;
  onSaved: (character: CharacterDetailDto) => void;
};

/** The character's name and free-text backstory (PUT /api/characters/{id}). */
export function EditCharacterDialog({
  open,
  onOpenChange,
  ...props
}: EditCharacterDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <EditCharacterForm
          key={open ? "open" : "closed"}
          onOpenChange={onOpenChange}
          {...props}
        />
      </DialogContent>
    </Dialog>
  );
}

function EditCharacterForm({
  onOpenChange,
  character,
  onSaved,
}: Omit<EditCharacterDialogProps, "open">) {
  const fetchApi = useApiFetch();

  const [name, setName] = useState(character.name);
  const [backstory, setBackstory] = useState(character.backstory ?? "");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const errors: FieldErrors = {
      name: name.trim() ? undefined : "Enter a name.",
    };
    setFieldErrors(errors);
    if (hasErrors(errors)) return;

    setIsSubmitting(true);

    try {
      const saved = await fetchApi<CharacterDetailDto>(`/api/characters/${character.id}`, {
        method: "PUT",
        json: {
          name: name.trim(),
          backstory: backstory.trim() || null,
        } satisfies UpdateCharacterRequest,
      });
      onSaved(saved);
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, ["name", "backstory"]);
        setFieldErrors(result.fieldErrors);
        setFormError(result.general);
      } else {
        setFormError(errorMessage(error));
      }
      setIsSubmitting(false);
    }
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>Edit Character</DialogTitle>
        <DialogDescription>
          The backstory is optional free text, separate from the builder&apos;s
          answers.
        </DialogDescription>
      </DialogHeader>

      <form onSubmit={handleSubmit} noValidate className="space-y-5">
        {formError && <FormAlert>{formError}</FormAlert>}

        <FormField
          id="character-name"
          label="Name"
          maxLength={NAME_MAX}
          value={name}
          onChange={(event) => setName(event.target.value)}
          error={fieldErrors.name}
        />

        <BackstoryField
          value={backstory}
          onChange={setBackstory}
          error={fieldErrors.backstory}
        />

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Saving..." : "Save"}
          </Button>
        </div>
      </form>
    </>
  );
}

type BackstoryFieldProps = {
  value: string;
  onChange: (value: string) => void;
  error?: string;
};

/** Plain text: the API stores it as sent and the page shows it as text. */
export function BackstoryField({ value, onChange, error }: BackstoryFieldProps) {
  return (
    <div className="space-y-2">
      <Label htmlFor="character-backstory">Backstory (optional)</Label>
      <Textarea
        id="character-backstory"
        className="min-h-48"
        placeholder="The people, events, losses and victories that shaped your character..."
        maxLength={BACKSTORY_MAX}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={error ? true : undefined}
      />
      <div className="flex justify-between gap-2 text-xs">
        <span className="text-destructive">{error}</span>
        <span className="text-muted-foreground">
          {value.length} / {BACKSTORY_MAX}
        </span>
      </div>
    </div>
  );
}
