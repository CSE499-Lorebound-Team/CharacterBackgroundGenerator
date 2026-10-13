import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

type FormAlertProps = {
  variant?: "error" | "success";
  children: ReactNode;
};

/** A banner for messages that belong to the whole form, not one field. */
export function FormAlert({ variant = "error", children }: FormAlertProps) {
  return (
    <div
      role={variant === "error" ? "alert" : "status"}
      className={cn(
        "border px-3 py-2 text-sm",
        variant === "error"
          ? "border-destructive/30 bg-destructive/10 text-destructive"
          : "border-primary/30 bg-primary/10 text-foreground"
      )}
    >
      {children}
    </div>
  );
}
