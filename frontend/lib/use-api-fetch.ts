"use client";

import { useCallback } from "react";
import { useRouter } from "next/navigation";

import {
  ApiError,
  apiFetch,
  type ApiRequestInit,
} from "@/lib/api";

// Calls that answer 401 as part of their normal flow; no login redirect.
function isPublicPath(path: string): boolean {
  return path.startsWith("/api/auth/") || path === "/api/health";
}

/**
 * `apiFetch` for client components. A 401 from a protected call sends the
 * user to /login with `next` set to the current page.
 */
export function useApiFetch() {
  const router = useRouter();

  return useCallback(
    async function fetchApi<T>(
      path: string,
      init: ApiRequestInit = {}
    ): Promise<T> {
      try {
        return await apiFetch<T>(path, init);
      } catch (error) {
        if (
          error instanceof ApiError &&
          error.status === 401 &&
          !isPublicPath(path)
        ) {
          const next = window.location.pathname + window.location.search;
          router.push(`/login?next=${encodeURIComponent(next)}`);
        }

        throw error;
      }
    },
    [router]
  );
}
