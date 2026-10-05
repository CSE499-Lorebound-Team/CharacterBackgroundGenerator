"use client";

import {
  FormEvent,
  useEffect,
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

import { getSettings } from "@/lib/settings/settings-store";
import type { Setting } from "@/lib/settings/types";

type CreateCharacterDialogProps = {
  open: boolean;
  onOpenChange:
    (open: boolean) => void;

  onCreate: (
    name: string,
    settingId: string,
    settingName: string
  ) => void;
};

export function CreateCharacterDialog({
  open,
  onOpenChange,
  onCreate,
}: CreateCharacterDialogProps) {
  const [name, setName] =
    useState("");

  const [
    selectedSettingId,
    setSelectedSettingId,
  ] = useState("");

  const [settings, setSettings] =
    useState<Setting[]>([]);

  const [error, setError] =
    useState<string>();

  useEffect(() => {
    if (!open) {
      return;
    }

    setSettings(getSettings());
    setError(undefined);
  }, [open]);

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

    if (!selectedSettingId) {
      setError(
        "Choose a campaign setting."
      );
      return;
    }

    const setting =
      settings.find(
        (item) =>
          item.id ===
          selectedSettingId
      );

    if (!setting) {
      setError(
        "The selected setting could not be found."
      );
      return;
    }

    onCreate(
      name.trim(),
      setting.id,
      setting.name
    );

    setName("");
    setSelectedSettingId("");
    setError(undefined);

    onOpenChange(false);
  }

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            Create Character
          </DialogTitle>

          <DialogDescription>
            Choose the campaign setting
            this character belongs to.
            You can build their
            background afterward.
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
              placeholder="Theron Vale"
            />
          </div>

          <div className="space-y-2">
            <Label>
              Campaign Setting
            </Label>

            <Select
              value={
                selectedSettingId
              }
              onValueChange={(value) =>
                setSelectedSettingId(
                  value ?? ""
                )
              }
            >
              <SelectTrigger className="w-full">
                <SelectValue>
                  {selectedSettingId
                    ? settings.find(
                        (setting) =>
                          setting.id ===
                          selectedSettingId
                      )?.name
                    : "Choose a setting"}
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

          {settings.length === 0 && (
            <p className="text-sm text-muted-foreground">
              You need a campaign
              setting before creating a
              character.
            </p>
          )}

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

            <Button
              type="submit"
              disabled={
                settings.length === 0
              }
            >
              Create Character
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}