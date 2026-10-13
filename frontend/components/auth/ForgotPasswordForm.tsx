"use client";

import { type FormEvent, useState } from "react";

import { FormAlert } from "@/components/forms/FormAlert";
import { FormField } from "@/components/forms/FormField";
import { Button } from "@/components/ui/button";
import { apiFetch } from "@/lib/api";
import { authErrorMessage, validateEmail } from "@/lib/auth";
import type { ForgotPasswordRequest } from "@/lib/types";

export function ForgotPasswordForm() {
  const [email, setEmail] = useState("");
  const [emailError, setEmailError] = useState<string>();
  const [formError, setFormError] = useState<string>();
  const [sent, setSent] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const error = validateEmail(email);
    setEmailError(error);
    if (error) return;

    setIsSubmitting(true);

    try {
      await apiFetch<void>("/api/auth/forgot-password", {
        method: "POST",
        json: { email: email.trim() } satisfies ForgotPasswordRequest,
      });
      setSent(true);
    } catch (error) {
      // Only rate limiting or an outage gets here; the API answers 204
      // whether or not the account exists.
      setFormError(authErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (sent) {
    // The same message for every address, so the page never reveals
    // which emails have accounts.
    return (
      <FormAlert variant="success">
        If an account with a confirmed email exists for that address, we sent
        a link to reset the password. Check your email.
      </FormAlert>
    );
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-5">
      {formError && <FormAlert>{formError}</FormAlert>}

      <FormField
        id="email"
        label="Email"
        type="email"
        autoComplete="email"
        placeholder="you@example.com"
        value={email}
        onChange={(event) => setEmail(event.target.value)}
        error={emailError}
      />

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? "Sending..." : "Send Reset Link"}
      </Button>
    </form>
  );
}
