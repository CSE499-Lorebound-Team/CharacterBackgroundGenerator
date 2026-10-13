"use client";

import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

type BuilderTextStepProps = {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  maxLength: number;
};

export function BuilderTextStep({
  id,
  label,
  value,
  onChange,
  placeholder,
  maxLength,
}: BuilderTextStepProps) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id} className="sr-only">
        {label}
      </Label>
      <Textarea
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        maxLength={maxLength}
        className="min-h-48"
      />
      <p className="text-right text-xs text-muted-foreground">
        {value.length} / {maxLength}
      </p>
    </div>
  );
}
