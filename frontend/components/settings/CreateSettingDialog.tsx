"use client";

import { type FormEvent, useState } from "react";

import { FormAlert } from "@/components/forms/FormAlert";
import { FormField } from "@/components/forms/FormField";
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
import { errorMessage } from "@/components/layout/PageStates";
import {
  type FieldErrors,
  hasErrors,
  readValidationErrors,
} from "@/lib/form-errors";
import type { CreateSettingRequest, SettingDetailDto } from "@/lib/types";
import { useApiFetch } from "@/lib/use-api-fetch";

// CreateSettingRequest limits in the API.
const NAME_MAX = 100;
const DESCRIPTION_MAX = 2000;

type CreateSettingDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (setting: SettingDetailDto) => void;
};

export function CreateSettingDialog({
  open,
  onOpenChange,
  onCreated,
}: CreateSettingDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        {/* Keyed so the fields reset each time the dialog opens. */}
        <CreateSettingForm
          key={open ? "open" : "closed"}
          onOpenChange={onOpenChange}
          onCreated={onCreated}
        />
      </DialogContent>
    </Dialog>
  );
}

function CreateSettingForm({
  onOpenChange,
  onCreated,
}: Omit<CreateSettingDialogProps, "open">) {
  const fetchApi = useApiFetch();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const errors: FieldErrors = {
      name: name.trim() ? undefined : "Enter a setting name.",
      description:
        description.length > DESCRIPTION_MAX
          ? `Use at most ${DESCRIPTION_MAX} characters.`
          : undefined,
    };

    setFieldErrors(errors);
    if (hasErrors(errors)) return;

    setIsSubmitting(true);

    try {
      const setting = await fetchApi<SettingDetailDto>("/api/settings", {
        method: "POST",
        json: {
          name: name.trim(),
          description: description.trim() || null,
        } satisfies CreateSettingRequest,
      });

      onCreated(setting);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, ["name", "description"]);
        setFieldErrors(result.fieldErrors);
        setFormError(result.general);
      } else if (error instanceof ApiError && error.status === 409) {
        // A duplicate among the caller's own settings.
        setFieldErrors({ name: error.detail ?? "You already have a setting with this name." });
      } else {
        setFormError(errorMessage(error));
      }

      setIsSubmitting(false);
    }
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>Create Setting</DialogTitle>
        <DialogDescription>
          Create a campaign setting that you can fill with locations, cultures,
          factions, people, and other lore. You will be its Game Master.
        </DialogDescription>
      </DialogHeader>

      <form onSubmit={handleSubmit} noValidate className="space-y-5">
        {formError && <FormAlert>{formError}</FormAlert>}

        <FormField
          id="setting-name"
          label="Setting Name"
          placeholder="The Shattered Realms"
          maxLength={NAME_MAX}
          value={name}
          onChange={(event) => setName(event.target.value)}
          error={fieldErrors.name}
        />

        <div className="space-y-2">
          <Label htmlFor="setting-description">Description</Label>
          <Textarea
            id="setting-description"
            placeholder="A short description of this campaign world..."
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
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
          >
            Cancel
          </Button>

          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Creating..." : "Create Setting"}
          </Button>
        </div>
      </form>
    </>
  );
}
