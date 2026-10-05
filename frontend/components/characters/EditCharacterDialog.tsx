"use client";

import {
  FormEvent,
  useState,
} from "react";

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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";

import { getSettings } from "@/lib/settings/settings-store";
import type { Setting } from "@/lib/settings/types";

import type {
  Character,
  CharacterStatus,
} from "@/lib/characters/types";

import { useIsClient } from "@/lib/use-is-client";

type EditCharacterDialogProps = {
  open: boolean;
  onOpenChange:
    (open: boolean) => void;

  character: Character;

  onSave: (data: {
    name: string;
    settingId: string;
    settingName: string;
    status: CharacterStatus;
    backstory: string;
  }) => void;
};

export function EditCharacterDialog({
  open,
  onOpenChange,
  character,
  onSave,
}: EditCharacterDialogProps) {
  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
    >
      <DialogContent className="sm:max-w-2xl">
        <EditCharacterForm
          key={`${open ? "open" : "closed"}:${character.id}`}
          character={character}
          onOpenChange={onOpenChange}
          onSave={onSave}
        />
      </DialogContent>
    </Dialog>
  );
}

function EditCharacterForm({
  character,
  onOpenChange,
  onSave,
}: Omit<
  EditCharacterDialogProps,
  "open"
>) {
  const isClient = useIsClient();

  const settings: Setting[] =
    isClient
      ? getSettings()
      : [];

  const [name, setName] =
    useState(character.name);

  const [settingId, setSettingId] =
    useState(character.settingId);

  const [status, setStatus] =
    useState<CharacterStatus>(
      character.status
    );

  const [backstory, setBackstory] =
    useState(character.backstory);

  const [error, setError] =
    useState<string>();

  function handleSubmit(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();

    if (!name.trim()) {
      setError(
        "Enter a character name."
      );
      return;
    }

    const setting =
      settings.find(
        (item) =>
          item.id === settingId
      );

    if (!setting) {
      setError(
        "Choose a campaign setting."
      );
      return;
    }

    onSave({
      name: name.trim(),
      settingId: setting.id,
      settingName: setting.name,
      status,
      backstory: backstory.trim(),
    });

    onOpenChange(false);
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>
          Edit Character
        </DialogTitle>

        <DialogDescription>
          Update this character&apos;s
          basic information and backstory.
        </DialogDescription>
      </DialogHeader>

      <form
        onSubmit={handleSubmit}
        className="space-y-5"
      >
        <div className="space-y-2">
          <Label htmlFor="character-name">
            Character Name
          </Label>

          <Input
            id="character-name"
            value={name}
            onChange={(event) =>
              setName(
                event.target.value
              )
            }
          />
        </div>

        <div className="space-y-2">
          <Label>
            Campaign Setting
          </Label>

          <Select
            value={settingId}
            onValueChange={(value) =>
              setSettingId(
                value ?? ""
              )
            }
          >
            <SelectTrigger className="w-full">
              <SelectValue>
                {settings.find(
                  (setting) =>
                    setting.id ===
                    settingId
                )?.name ??
                  "Choose a setting"}
              </SelectValue>
            </SelectTrigger>

            <SelectContent>
              {settings.map(
                (setting) => (
                  <SelectItem
                    key={setting.id}
                    value={setting.id}
                  >
                    {setting.name}
                  </SelectItem>
                )
              )}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label>
            Status
          </Label>

          <Select
            value={status}
            onValueChange={(value) =>
              setStatus(
                value as CharacterStatus
              )
            }
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>

            <SelectContent>
              <SelectItem value="Draft">
                Draft
              </SelectItem>

              <SelectItem value="Complete">
                Complete
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="backstory">
            Backstory
          </Label>

          <Textarea
            id="backstory"
            value={backstory}
            onChange={(event) =>
              setBackstory(
                event.target.value
              )
            }
            placeholder="Write this character's backstory..."
            className="min-h-52"
          />
        </div>

        {error && (
          <p className="text-sm text-destructive">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() =>
              onOpenChange(false)
            }
          >
            Cancel
          </Button>

          <Button type="submit">
            Save Changes
          </Button>
        </div>
      </form>
    </>
  );
}