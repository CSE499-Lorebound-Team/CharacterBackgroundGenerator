"use client";

import { type FormEvent, useState } from "react";
import Link from "next/link";
import { CircleCheck } from "lucide-react";

import { FormAlert } from "@/components/auth/FormAlert";
import { FormField } from "@/components/auth/FormField";
import { Button } from "@/components/ui/button";
import { ApiError, apiFetch } from "@/lib/api";
import {
  authErrorMessage,
  type FieldErrors,
  hasErrors,
  PASSWORD_MIN_LENGTH,
  readValidationErrors,
  validateNewPassword,
} from "@/lib/auth";
import type { ResetPasswordRequest } from "@/lib/types";

type ResetPasswordFormProps = {
  email?: string;
  code?: string;
};

export function ResetPasswordForm({ email, code }: ResetPasswordFormProps) {
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  // Set when the API rejects the link itself, not the new password.
  const [linkInvalid, setLinkInvalid] = useState(!email || !code);
  const [done, setDone] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!email || !code) return;

    setFormError(undefined);

    const errors: FieldErrors = {
      newPassword: validateNewPassword(newPassword),
      confirmPassword:
        newPassword === confirmPassword ? undefined : "Passwords do not match.",
    };

    setFieldErrors(errors);
    if (hasErrors(errors)) return;

    setIsSubmitting(true);

    try {
      await apiFetch<void>("/api/auth/reset-password", {
        method: "POST",
        json: { email, code, newPassword } satisfies ResetPasswordRequest,
      });
      setDone(true);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        if (error.errors) {
          // Password policy errors come back under "NewPassword".
          const result = readValidationErrors(error, ["newPassword"]);
          setFieldErrors(result.fieldErrors);
          setFormError(result.general);
        } else {
          setLinkInvalid(true);
        }
      } else {
        setFormError(authErrorMessage(error));
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  if (done) {
    return (
      <div className="space-y-5 text-center">
        <CircleCheck className="mx-auto h-10 w-10 text-primary" />
        <p>Your password has been reset.</p>
        <Link
          href="/login"
          className="inline-block font-medium text-primary hover:underline"
        >
          Log in with your new password
        </Link>
      </div>
    );
  }

  if (linkInvalid) {
    return (
      <div className="space-y-5 text-center">
        <FormAlert>This reset link is invalid or has expired.</FormAlert>
        <Link
          href="/forgot-password"
          className="inline-block font-medium text-primary hover:underline"
        >
          Request a new link
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-5">
      {formError && <FormAlert>{formError}</FormAlert>}

      <p className="text-sm text-muted-foreground">
        Resetting the password for{" "}
        <span className="font-medium text-foreground">{email}</span>.
      </p>

      <FormField
        id="newPassword"
        label="New Password"
        type="password"
        autoComplete="new-password"
        value={newPassword}
        onChange={(event) => setNewPassword(event.target.value)}
        error={fieldErrors.newPassword}
        hint={`At least ${PASSWORD_MIN_LENGTH} characters.`}
      />

      <FormField
        id="confirmPassword"
        label="Confirm New Password"
        type="password"
        autoComplete="new-password"
        value={confirmPassword}
        onChange={(event) => setConfirmPassword(event.target.value)}
        error={fieldErrors.confirmPassword}
      />

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? "Saving..." : "Reset Password"}
      </Button>
    </form>
  );
}
