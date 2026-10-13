import type { ReactNode } from "react";
import { AlertTriangle } from "lucide-react";

import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";

/** Placeholder cards while a list loads. */
export function LoadingCards({ count = 6 }: { count?: number }) {
  return (
    <div
      className="grid gap-4 md:grid-cols-2 xl:grid-cols-3"
      aria-busy="true"
      aria-label="Loading"
    >
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          className="h-40 animate-pulse border border-border bg-muted/50"
        />
      ))}
    </div>
  );
}

/** A friendly message for a failed request. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404) return "This page could not be found, or you no longer have access to it.";
    if (error.status === 403) return "You do not have permission to do that.";
    if (error.status === 429) return "Too many requests. Wait a minute and try again.";
    if (error.status < 500 && error.detail) return error.detail;
  }

  return "Something went wrong while loading. Check your connection and try again.";
}

type ErrorStateProps = {
  error: unknown;
  onRetry?: () => void;
};

export function ErrorState({ error, onRetry }: ErrorStateProps) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center gap-3 border border-destructive/30 bg-destructive/5 p-10 text-center"
    >
      <AlertTriangle className="h-6 w-6 text-destructive" />
      <p className="text-sm">{errorMessage(error)}</p>
      {onRetry && (
        <Button variant="outline" onClick={onRetry}>
          Try Again
        </Button>
      )}
    </div>
  );
}

type EmptyStateProps = {
  title: string;
  description?: string;
  action?: ReactNode;
};

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="border border-dashed border-border p-10 text-center">
      <p className="font-medium">{title}</p>
      {description && (
        <p className="mt-2 text-sm text-muted-foreground">{description}</p>
      )}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

type LoadMoreProps = {
  shown: number;
  total: number;
  loading: boolean;
  onLoadMore: () => void;
};

/** "Showing 24 of 60" with a Load more button while pages remain. */
export function LoadMore({ shown, total, loading, onLoadMore }: LoadMoreProps) {
  return (
    <div className="flex flex-col items-center gap-2 pt-2 text-sm text-muted-foreground">
      <p>
        Showing {shown} of {total}
      </p>
      <Button variant="outline" onClick={onLoadMore} disabled={loading}>
        {loading ? "Loading..." : "Load More"}
      </Button>
    </div>
  );
}
