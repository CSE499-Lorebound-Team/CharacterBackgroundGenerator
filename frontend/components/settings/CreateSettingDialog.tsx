"use client";

import { FormEvent, useState } from "react";

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

type CreateSettingDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreate: (name: string, description: string) => void;
};

export function CreateSettingDialog({
  open,
  onOpenChange,
  onCreate,
}: CreateSettingDialogProps) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string>();

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!name.trim()) {
      setError("Enter a setting name.");
      return;
    }

    onCreate(name.trim(), description.trim());

    setName("");
    setDescription("");
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
          <DialogTitle>Create Setting</DialogTitle>

          <DialogDescription>
            Create a campaign setting that you can fill with locations,
            cultures, factions, people, and other lore.
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit}
          className="space-y-5"
        >
          <div className="space-y-2">
            <Label htmlFor="setting-name">
              Setting Name
            </Label>

            <Input
              id="setting-name"
              value={name}
              onChange={(event) =>
                setName(event.target.value)
              }
              placeholder="The Shattered Realms"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="setting-description">
              Description
            </Label>

            <Textarea
              id="setting-description"
              value={description}
              onChange={(event) =>
                setDescription(event.target.value)
              }
              placeholder="A short description of this campaign world..."
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
              onClick={() => onOpenChange(false)}
            >
              Cancel
            </Button>

            <Button type="submit">
              Create Setting
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}