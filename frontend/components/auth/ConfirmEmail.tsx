"use client";

import { type FormEvent, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { CircleCheck, CircleX, LoaderCircle } from "lucide-react";

import { FormAlert } from "@/components/auth/FormAlert";
import { FormField } from "@/components/auth/FormField";
import { Button } from "@/components/ui/button";
import { apiFetch } from "@/lib/api";
import { authErrorMessage, validateEmail } from "@/lib/auth";
import { useSession } from "@/lib/session";
import type {
  ConfirmEmailRequest,
  ResendConfirmationRequest,
} from "@/lib/types";

type ConfirmEmailProps = {
  userId?: string;
  code?: string;
};

type Result = "confirming" | "confirmed" | "failed";

/** Posts the emailed link's userId and code once, then shows the outcome. */
export function ConfirmEmail({ userId, code }: ConfirmEmailProps) {
  const hasLink = Boolean(userId && code);
  const [result, setResult] = useState<Result>(
    hasLink ? "confirming" : "failed"
  );
  const posted = useRef(false);

  useEffect(() => {
    // A code works once; Strict Mode runs effects twice in development.
    if (!userId || !code || posted.current) return;
    posted.current = true;

    apiFetch<void>("/api/auth/confirm-email", {
      method: "POST",
      json: { userId, code } satisfies ConfirmEmailRequest,
    }).then(
      () => setResult("confirmed"),
      () => setResult("failed")
    );
  }, [userId, code]);

  if (result === "confirming") {
    return (
      <div className="flex flex-col items-center gap-3 py-4 text-center">
        <LoaderCircle className="h-8 w-8 animate-spin text-primary" />
        <p>Confirming your email...</p>
      </div>
    );
  }

  if (result === "confirmed") {
    return <Confirmed />;
  }

  return <Failed />;
}

function Confirmed() {
  const session = useSession();
  const signedIn = session.status === "authenticated";

  return (
    <div className="space-y-5 text-center">
      <CircleCheck className="mx-auto h-10 w-10 text-primary" />
      <p>Your email is confirmed.</p>
      <Link
        href={signedIn ? "/dashboard" : "/login"}
        className="inline-block font-medium text-primary hover:underline"
      >
        {signedIn ? "Go to your dashboard" : "Log in"}
      </Link>
    </div>
  );
}

function Failed() {
  const [email, setEmail] = useState("");
  const [emailError, setEmailError] = useState<string>();
  const [formError, setFormError] = useState<string>();
  const [sent, setSent] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleResend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const error = validateEmail(email);
    setEmailError(error);
    if (error) return;

    setIsSubmitting(true);

    try {
      await apiFetch<void>("/api/auth/resend-confirmation", {
        method: "POST",
        json: { email: email.trim() } satisfies ResendConfirmationRequest,
      });
      setSent(true);
    } catch (error) {
      setFormError(authErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col items-center gap-3 text-center">
        <CircleX className="h-10 w-10 text-destructive" />
        <p>This confirmation link is invalid or has already been used.</p>
      </div>

      {sent ? (
        <FormAlert variant="success">
          If that account still needs confirming, we sent a new link. Check
          your email.
        </FormAlert>
      ) : (
        <form onSubmit={handleResend} noValidate className="space-y-4">
          {formError && <FormAlert>{formError}</FormAlert>}

          <FormField
            id="email"
            label="Send a new link to"
            type="email"
            autoComplete="email"
            placeholder="you@example.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            error={emailError}
          />

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? "Sending..." : "Resend Confirmation"}
          </Button>
        </form>
      )}
    </div>
  );
}
