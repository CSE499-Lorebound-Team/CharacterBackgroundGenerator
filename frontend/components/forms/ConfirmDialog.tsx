"use client";

import { type ReactNode, useState } from "react";

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

type ConfirmDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: ReactNode;
  confirmLabel: string;
  /** Runs on confirm; a thrown error is shown and the dialog stays open. */
  onConfirm: () => Promise<void>;
  destructive?: boolean;
};

/** "Are you sure?" for an action that calls the API. */
export function ConfirmDialog({ open, onOpenChange, ...props }: ConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <ConfirmBody key={open ? "open" : "closed"} onOpenChange={onOpenChange} {...props} />
      </DialogContent>
    </Dialog>
  );
}

function ConfirmBody({
  onOpenChange,
  title,
  description,
  confirmLabel,
  onConfirm,
  destructive = true,
}: Omit<ConfirmDialogProps, "open">) {
  const [error, setError] = useState<string>();
  const [isWorking, setIsWorking] = useState(false);

  async function handleConfirm() {
    setError(undefined);
    setIsWorking(true);

    try {
      await onConfirm();
      onOpenChange(false);
    } catch (caught) {
      setError(errorMessage(caught));
      setIsWorking(false);
    }
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>{title}</DialogTitle>
        <DialogDescription>{description}</DialogDescription>
      </DialogHeader>

      {error && <FormAlert>{error}</FormAlert>}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
          Cancel
        </Button>
        <Button
          type="button"
          variant={destructive ? "destructive" : "default"}
          disabled={isWorking}
          onClick={handleConfirm}
        >
          {isWorking ? "Working..." : confirmLabel}
        </Button>
      </div>
    </>
  );
}
