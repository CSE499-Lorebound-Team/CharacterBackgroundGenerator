"use client";

import { FormEvent, useEffect, useState } from "react";

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

import type {
  SettingEntry,
  SettingEntryType,
} from "@/lib/settings/types";

type SettingEntryDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;

  entry?: SettingEntry;

  onSave: (entry: {
    name: string;
    type: SettingEntryType;
    summary: string;
    content: string;
    isGmOnly: boolean;
  }) => void;
};

const entryTypes: SettingEntryType[] = [
  "Nation",
  "City",
  "Culture",
  "Religion",
  "Faction",
];

export function SettingEntryDialog({
  open,
  onOpenChange,
  entry,
  onSave,
}: SettingEntryDialogProps) {
  const isEditing = Boolean(entry);

  const [name, setName] = useState("");
  const [type, setType] =
    useState<SettingEntryType>("Nation");
  const [summary, setSummary] = useState("");
  const [content, setContent] = useState("");
  const [error, setError] = useState<string>();
  const [isGmOnly, setIsGmOnly] = useState(false);

  useEffect(() => {
    if (entry) {
        setName(entry.name);
        setType(entry.type);
        setSummary(entry.summary);
        setContent(entry.content);
        setIsGmOnly(entry.isGmOnly);
      } else {
        setName("");
        setType("Nation");
        setSummary("");
        setContent("");
        setIsGmOnly(false);
      }

    setError(undefined);
  }, [entry, open]);

  function handleSubmit(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();

    if (!name.trim()) {
      setError("Enter a name.");
      return;
    }

    if (!summary.trim()) {
      setError("Enter a short summary.");
      return;
    }

    onSave({
        name: name.trim(),
        type,
        summary: summary.trim(),
        content: content.trim(),
        isGmOnly,
      });

    onOpenChange(false);
  }

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
    >
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {isEditing
              ? "Edit Lore Entry"
              : "Add Lore Entry"}
          </DialogTitle>

          <DialogDescription>
            {isEditing
              ? "Update this lore article."
              : "Add a new article to this campaign setting."}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit}
          className="space-y-5"
        >
          <div className="space-y-2">
            <Label htmlFor="entry-name">
              Name
            </Label>

            <Input
              id="entry-name"
              value={name}
              onChange={(event) =>
                setName(event.target.value)
              }
              placeholder="Kingdom of Valmere"
            />
          </div>

          <div className="space-y-2">
            <Label>
              Type
            </Label>

            <Select
              value={type}
              onValueChange={(value) =>
                setType(
                  value as SettingEntryType
                )
              }
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>

              <SelectContent>
                {entryTypes.map((entryType) => (
                  <SelectItem
                    key={entryType}
                    value={entryType}
                  >
                    {entryType}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="entry-summary">
              Summary
            </Label>

            <Textarea
              id="entry-summary"
              value={summary}
              onChange={(event) =>
                setSummary(event.target.value)
              }
              placeholder="A short description shown in lists and cards."
              className="min-h-20"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="entry-content">
              Lore Article
            </Label>

            <Textarea
              id="entry-content"
              value={content}
              onChange={(event) =>
                setContent(event.target.value)
              }
              placeholder="Write the full lore article here..."
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
              {isEditing
                ? "Save Changes"
                : "Add Entry"}
            </Button>
          </div>
          <div className="flex items-start gap-3 border border-border p-4">
            <input
                id="gm-only"
                type="checkbox"
                checked={isGmOnly}
                onChange={(event) =>
                setIsGmOnly(event.target.checked)
                }
                className="mt-1 h-4 w-4"
            />

            <div>
                <Label htmlFor="gm-only">
                GM Only
                </Label>

                <p className="mt-1 text-sm text-muted-foreground">
                Hide this lore entry from players.
                </p>
            </div>
            </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}