"use client";

import {
  usePathname,
  useRouter,
  useSearchParams,
} from "next/navigation";

import {
  ApiError,
  apiFetch,
} from "@/lib/api";

export function useApiFetch() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams =
    useSearchParams();

  return async function fetchApi<T>(
    path: string,
    init: RequestInit = {}
  ): Promise<T> {
    try {
      return await apiFetch<T>(
        path,
        init
      );
    } catch (error) {
      if (
        error instanceof ApiError &&
        error.status === 401 &&
        !path.startsWith("/api/auth/") &&
        path !== "/api/health"
      ) {
        const query =
          searchParams.toString();

        const next =
          query
            ? `${pathname}?${query}`
            : pathname;

        router.push(
          `/login?next=${encodeURIComponent(next)}`
        );
      }

      throw error;
    }
  };
}