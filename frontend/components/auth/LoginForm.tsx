"use client";

import { type FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { FormAlert } from "@/components/auth/FormAlert";
import { FormField } from "@/components/auth/FormField";
import { Button } from "@/components/ui/button";
import { ApiError, apiFetch } from "@/lib/api";
import {
  authErrorMessage,
  type FieldErrors,
  hasErrors,
  readValidationErrors,
  validateEmail,
} from "@/lib/auth";
import type { AuthUserDto, LoginRequest } from "@/lib/types";

type LoginFormProps = {
  /** Where to go after signing in; already checked by safeNextPath. */
  next: string;
};

export function LoginForm({ next }: LoginFormProps) {
  const router = useRouter();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  // A 401 is also what an unconfirmed account gets.
  const [mayNeedConfirmation, setMayNeedConfirmation] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);
    setMayNeedConfirmation(false);

    const errors: FieldErrors = {
      email: validateEmail(email),
      password: password ? undefined : "Enter your password.",
    };

    setFieldErrors(errors);
    if (hasErrors(errors)) return;

    setIsSubmitting(true);

    try {
      await apiFetch<AuthUserDto>("/api/auth/login", {
        method: "POST",
        json: { email: email.trim(), password, rememberMe } satisfies LoginRequest,
      });

      // The API set the httpOnly auth cookie; nothing is stored here.
      router.replace(next);
      router.refresh();
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, ["email", "password"]);
        setFieldErrors(result.fieldErrors);
        setFormError(result.general);
      } else {
        setFormError(authErrorMessage(error));
        setMayNeedConfirmation(
          error instanceof ApiError && error.status === 401
        );
      }

      setPassword("");
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-5">
      {formError && (
        <FormAlert>
          {formError}
          {mayNeedConfirmation && (
            <> If you just created an account, confirm your email first.</>
          )}
        </FormAlert>
      )}

      <FormField
        id="email"
        label="Email"
        type="email"
        autoComplete="email"
        placeholder="you@example.com"
        value={email}
        onChange={(event) => setEmail(event.target.value)}
        error={fieldErrors.email}
      />

      <FormField
        id="password"
        label="Password"
        type="password"
        autoComplete="current-password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        error={fieldErrors.password}
      />

      <label className="flex items-center gap-2 text-sm text-muted-foreground">
        <input
          type="checkbox"
          checked={rememberMe}
          onChange={(event) => setRememberMe(event.target.checked)}
          className="h-4 w-4 accent-primary"
        />
        Keep me signed in for 14 days
      </label>

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? "Signing In..." : "Log In"}
      </Button>
    </form>
  );
}

export function LoginFooter({ next }: LoginFormProps) {
  const href =
    next === "/dashboard"
      ? "/register"
      : `/register?next=${encodeURIComponent(next)}`;

  return (
    <span>
      Don&apos;t have an account?{" "}
      <Link href={href} className="font-medium text-primary hover:underline">
        Create one
      </Link>
    </span>
  );
}
