"use client";

import { useEffect } from "react";
import Link from "next/link";
import { AlertTriangle } from "lucide-react";

import { AppShell } from "@/components/layout/AppShell";
import { LoadingCards } from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";

/** The skeleton each app segment's loading.tsx shows while it loads. */
export function RouteLoading() {
  return (
    <AppShell>
      <div className="mb-6 space-y-3 border-b border-border pb-6" aria-hidden="true">
        <div className="h-8 w-56 animate-pulse bg-muted" />
        <div className="h-4 w-80 max-w-full animate-pulse bg-muted" />
      </div>
      <LoadingCards />
    </AppShell>
  );
}

type RouteErrorProps = {
  error: Error & { digest?: string };
  retry: () => void;
};

/**
 * What each app segment's error.tsx shows when rendering throws. Errors
 * from server components arrive with a generic message and a digest, so
 * the page never shows internal details.
 */
export function RouteError({ error, retry }: RouteErrorProps) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <AppShell>
      <div
        role="alert"
        className="mx-auto flex max-w-lg flex-col items-center gap-4 border border-destructive/30 bg-destructive/5 p-10 text-center"
      >
        <AlertTriangle className="h-8 w-8 text-destructive" />
        <div>
          <p className="font-medium">Something went wrong loading this page.</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Check your connection and try again. If it keeps happening, the API
            may be down.
          </p>
          {error.digest && (
            <p className="mt-2 text-xs text-muted-foreground">Reference: {error.digest}</p>
          )}
        </div>
        <div className="flex gap-2">
          <Button onClick={() => retry()}>Try Again</Button>
          <Button variant="outline" nativeButton={false} render={<Link href="/dashboard" />}>
            Go to Dashboard
          </Button>
        </div>
      </div>
    </AppShell>
  );
}
