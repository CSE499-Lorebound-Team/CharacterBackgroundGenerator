"use client";

import { Textarea } from "@/components/ui/textarea";

type BuilderTextStepProps = {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  maxLength?: number;
};

export function BuilderTextStep({
  value,
  onChange,
  placeholder,
  maxLength = 2000,
}: BuilderTextStepProps) {
  return (
    <div className="space-y-2">
      <Textarea
        value={value}
        onChange={(event) =>
          onChange(event.target.value)
        }
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