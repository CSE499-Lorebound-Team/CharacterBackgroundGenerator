"use client";

import { type FormEvent, useState } from "react";
import Link from "next/link";

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
import { ApiError } from "@/lib/api";
import { readValidationErrors, type FieldErrors } from "@/lib/form-errors";
import type {
  CharacterDetailDto,
  CreateCharacterRequest,
  PagedResult,
  SettingListItemDto,
} from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";

// CreateCharacterRequest limit in the API.
const NAME_MAX = 100;

type CreateCharacterDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (character: CharacterDetailDto) => void;
};

/** Starts a draft in one of the user's settings. */
export function CreateCharacterDialog({
  open,
  onOpenChange,
  onCreated,
}: CreateCharacterDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        {open && (
          <CreateCharacterForm onOpenChange={onOpenChange} onCreated={onCreated} />
        )}
      </DialogContent>
    </Dialog>
  );
}

function CreateCharacterForm({
  onOpenChange,
  onCreated,
}: Omit<CreateCharacterDialogProps, "open">) {
  const fetchApi = useApiFetch();

  // Any member may create a character, GameMasters included. 100 is the
  // API's page size cap; nobody belongs to that many settings.
  const settings = useApiData<PagedResult<SettingListItemDto>>(
    "/api/settings?pageSize=100"
  );

  const [name, setName] = useState("");
  const [settingId, setSettingId] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const items = (settings.data?.items ?? []).map((setting) => ({
    value: setting.id,
    label: setting.name,
  }));

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    if (!settingId) {
      setFieldErrors({ settingId: "Choose a setting." });
      return;
    }
    setFieldErrors({});
    setIsSubmitting(true);

    try {
      const character = await fetchApi<CharacterDetailDto>("/api/characters", {
        method: "POST",
        // A blank name becomes "Unnamed Character" in the API.
        json: { settingId, name: name.trim() } satisfies CreateCharacterRequest,
      });
      onCreated(character);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, ["name", "settingId"]);
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
        <DialogTitle>Create Character</DialogTitle>
        <DialogDescription>
          Choose the campaign setting your character belongs to. The builder
          then walks you through their background.
        </DialogDescription>
      </DialogHeader>

      {settings.error ? (
        <FormAlert>{errorMessage(settings.error)}</FormAlert>
      ) : !settings.data ? (
        <p className="text-sm text-muted-foreground">Loading your settings...</p>
      ) : items.length === 0 ? (
        <div className="space-y-3 text-sm">
          <p>You need to belong to a setting first.</p>
          <p className="text-muted-foreground">
            Create one, or ask your Game Master for an invite link.
          </p>
          <Link href="/settings" className="font-medium text-primary hover:underline">
            Go to Settings
          </Link>
        </div>
      ) : (
        <form onSubmit={handleSubmit} noValidate className="space-y-5">
          {formError && <FormAlert>{formError}</FormAlert>}

          <div className="space-y-2">
            <Label id="character-setting-label">Setting</Label>
            <Select
              items={items}
              value={settingId ?? null}
              onValueChange={(value) => setSettingId(value ?? undefined)}
            >
              <SelectTrigger
                className="w-full"
                aria-labelledby="character-setting-label"
                aria-invalid={fieldErrors.settingId ? true : undefined}
              >
                <SelectValue placeholder="Choose a setting" />
              </SelectTrigger>
              <SelectContent>
                {items.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {item.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {fieldErrors.settingId && (
              <p className="text-sm text-destructive">{fieldErrors.settingId}</p>
            )}
          </div>

          <FormField
            id="character-name"
            label="Character Name"
            placeholder="Unnamed Character"
            maxLength={NAME_MAX}
            value={name}
            onChange={(event) => setName(event.target.value)}
            error={fieldErrors.name}
            hint="You can change it later."
          />

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Creating..." : "Start Building"}
            </Button>
          </div>
        </form>
      )}
    </>
  );
}
