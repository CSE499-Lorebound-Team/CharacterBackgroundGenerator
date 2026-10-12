import type { ComponentProps, ReactNode } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

type FormFieldProps = ComponentProps<typeof Input> & {
  id: string;
  label: string;
  error?: string;
  hint?: ReactNode;
};

/** A labelled input with its validation message underneath. */
export function FormField({ id, label, error, hint, ...inputProps }: FormFieldProps) {
  const messageId = `${id}-message`;

  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>

      <Input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? messageId : undefined}
        {...inputProps}
      />

      {error ? (
        <p id={messageId} className="text-sm text-destructive">
          {error}
        </p>
      ) : (
        hint && (
          <p id={messageId} className="text-sm text-muted-foreground">
            {hint}
          </p>
        )
      )}
    </div>
  );
}
