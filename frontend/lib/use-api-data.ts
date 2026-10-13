"use client";

import { useCallback, useEffect, useState } from "react";

import type { PagedResult } from "@/lib/types";
import { useApiFetch } from "@/lib/use-api-fetch";

// Both hooks key their state by request, so a new path shows as loading
// without a synchronous setState in the effect (react-hooks lint rule).

type DataState<T> = {
  key: string | null;
  data?: T;
  error?: unknown;
};

/**
 * GETs `path` (null: nothing yet) and reloads when it changes. A 401
 * redirects to /login (useApiFetch). `data` keeps the previous result while
 * a new path loads, so lists do not flash empty.
 */
export function useApiData<T>(path: string | null) {
  const fetchApi = useApiFetch();
  const [version, setVersion] = useState(0);
  const [state, setState] = useState<DataState<T>>({ key: null });

  const key = path === null ? null : `${version}|${path}`;

  useEffect(() => {
    if (key === null || path === null) return;
    let cancelled = false;

    fetchApi<T>(path).then(
      (data) => {
        if (!cancelled) setState({ key, data });
      },
      (error: unknown) => {
        if (!cancelled) setState((current) => ({ ...current, key, error }));
      }
    );

    return () => {
      cancelled = true;
    };
  }, [key, path, fetchApi]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);
  const settled = state.key === key;

  return {
    data: state.data,
    error: settled ? state.error : undefined,
    isLoading: key !== null && !settled,
    reload,
  };
}

type PagedState<T> = {
  key: string | null;
  items: T[];
  page: number;
  totalCount: number;
  error?: unknown;
};

function withPage(path: string, page: number, pageSize: number): string {
  const separator = path.includes("?") ? "&" : "?";
  return `${path}${separator}page=${page}&pageSize=${pageSize}`;
}

/**
 * A paged list (`PagedResult<T>`) shown with "Load more": page 1 loads when
 * `path` (without page parameters) changes, `loadMore` appends the next one.
 */
export function usePagedList<T>(path: string, pageSize = 24) {
  const fetchApi = useApiFetch();
  const [version, setVersion] = useState(0);
  const [state, setState] = useState<PagedState<T>>({
    key: null,
    items: [],
    page: 0,
    totalCount: 0,
  });
  const [loadingMore, setLoadingMore] = useState(false);

  const key = `${version}|${path}`;

  useEffect(() => {
    let cancelled = false;

    fetchApi<PagedResult<T>>(withPage(path, 1, pageSize)).then(
      (result) => {
        if (!cancelled) {
          setState({
            key,
            items: result.items,
            page: 1,
            totalCount: result.totalCount,
          });
        }
      },
      (error: unknown) => {
        if (!cancelled) setState((current) => ({ ...current, key, error }));
      }
    );

    return () => {
      cancelled = true;
    };
  }, [key, path, pageSize, fetchApi]);

  const settled = state.key === key;

  const loadMore = useCallback(async () => {
    if (state.key !== key) return;

    setLoadingMore(true);

    try {
      const result = await fetchApi<PagedResult<T>>(
        withPage(path, state.page + 1, pageSize)
      );

      setState((current) =>
        current.key === key
          ? {
              ...current,
              items: [...current.items, ...result.items],
              page: current.page + 1,
              totalCount: result.totalCount,
            }
          : current
      );
    } catch (error) {
      setState((current) =>
        current.key === key ? { ...current, error } : current
      );
    } finally {
      setLoadingMore(false);
    }
  }, [fetchApi, key, path, pageSize, state.key, state.page]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);

  return {
    items: state.items,
    totalCount: state.totalCount,
    hasMore: settled && state.items.length < state.totalCount,
    error: settled ? state.error : undefined,
    // True until page 1 of this path arrives; earlier items stay visible.
    isLoading: !settled,
    hasLoaded: state.key !== null,
    loadingMore,
    loadMore,
    reload,
  };
}
