"use client";

import { type FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { MailCheck } from "lucide-react";

import { FormAlert } from "@/components/auth/FormAlert";
import { FormField } from "@/components/auth/FormField";
import { Button } from "@/components/ui/button";
import { ApiError, apiFetch } from "@/lib/api";
import {
  authErrorMessage,
  DISPLAY_NAME_MAX_LENGTH,
  type FieldErrors,
  hasErrors,
  PASSWORD_MIN_LENGTH,
  readValidationErrors,
  validateDisplayName,
  validateEmail,
  validateNewPassword,
} from "@/lib/auth";
import { useSession } from "@/lib/session";
import type {
  AuthUserDto,
  LoginRequest,
  RegisteredUserDto,
  RegisterRequest,
} from "@/lib/types";

type RegisterFormProps = {
  /** Where to go after signing in; already checked by safeNextPath. */
  next: string;
};

export function RegisterForm({ next }: RegisterFormProps) {
  const router = useRouter();

  const session = useSession();
  const isSignedIn = session.status === "authenticated";

  // Already signed in (or just signed in): leave the auth page.
  useEffect(() => {
    if (isSignedIn) router.replace(next);
  }, [isSignedIn, next, router]);

  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [checkEmailFor, setCheckEmailFor] = useState<string>();

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const errors: FieldErrors = {
      displayName: validateDisplayName(displayName),
      email: validateEmail(email),
      password: validateNewPassword(password),
      confirmPassword:
        password === confirmPassword ? undefined : "Passwords do not match.",
    };

    setFieldErrors(errors);
    if (hasErrors(errors)) return;

    setIsSubmitting(true);

    const request: RegisterRequest = {
      email: email.trim(),
      password,
      displayName: displayName.trim(),
    };

    try {
      await apiFetch<RegisteredUserDto>("/api/auth/register", {
        method: "POST",
        json: request,
      });
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, [
          "email",
          "password",
          "displayName",
        ]);
        setFieldErrors(result.fieldErrors);
        setFormError(result.general);
      } else {
        setFormError(authErrorMessage(error));
      }

      setIsSubmitting(false);
      return;
    }

    // The API only says whether login needs a confirmed email by refusing
    // it, so sign in straight away. Identity rejects an unconfirmed account
    // before checking the password, so this never counts toward lockout.
    try {
      await apiFetch<AuthUserDto>("/api/auth/login", {
        method: "POST",
        json: { email: request.email, password } satisfies LoginRequest,
      });

      await session.refresh();
      router.replace(next);
    } catch {
      setCheckEmailFor(request.email);
      setIsSubmitting(false);
    }
  }

  if (checkEmailFor) {
    return (
      <div className="space-y-5 text-center">
        <MailCheck className="mx-auto h-10 w-10 text-primary" />

        <p>
          We sent a confirmation link to{" "}
          <span className="font-medium text-foreground">{checkEmailFor}</span>.
          Open it to activate your account, then log in.
        </p>

        <Link
          href="/login"
          className="inline-block font-medium text-primary hover:underline"
        >
          Go to log in
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-5">
      {formError && <FormAlert>{formError}</FormAlert>}

      <FormField
        id="displayName"
        label="Display Name"
        autoComplete="nickname"
        placeholder="How other players see you"
        maxLength={DISPLAY_NAME_MAX_LENGTH}
        value={displayName}
        onChange={(event) => setDisplayName(event.target.value)}
        error={fieldErrors.displayName}
      />

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
        autoComplete="new-password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        error={fieldErrors.password}
        hint={`At least ${PASSWORD_MIN_LENGTH} characters.`}
      />

      <FormField
        id="confirmPassword"
        label="Confirm Password"
        type="password"
        autoComplete="new-password"
        value={confirmPassword}
        onChange={(event) => setConfirmPassword(event.target.value)}
        error={fieldErrors.confirmPassword}
      />

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? "Creating Account..." : "Create Account"}
      </Button>
    </form>
  );
}

export function RegisterFooter({ next }: RegisterFormProps) {
  const href =
    next === "/dashboard"
      ? "/login"
      : `/login?next=${encodeURIComponent(next)}`;

  return (
    <span>
      Already have an account?{" "}
      <Link href={href} className="font-medium text-primary hover:underline">
        Log in
      </Link>
    </span>
  );
}
