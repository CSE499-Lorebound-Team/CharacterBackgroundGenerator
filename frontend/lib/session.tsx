"use client";

import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";

import { ApiError, apiFetch } from "@/lib/api";
import type { UserProfileDto } from "@/lib/types";

type SessionState =
  | { status: "loading" }
  | { status: "authenticated"; user: UserProfileDto }
  // loggedOut: the user just pressed Log out (no `next` on the login page).
  | { status: "unauthenticated"; loggedOut?: boolean };

type Session = SessionState & {
  /** Reloads the user from the API, e.g. after logging in. */
  refresh: () => Promise<void>;
  /** Calls POST /api/auth/logout; the API clears the cookie. */
  logout: () => Promise<void>;
};

const SessionContext = createContext<Session | null>(null);

async function loadSession(): Promise<SessionState> {
  try {
    const user = await apiFetch<UserProfileDto>("/api/users/me");
    return { status: "authenticated", user };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return { status: "unauthenticated" };
    }
    throw error;
  }
}

/**
 * Holds the signed-in user for the whole app. The auth cookie is httpOnly,
 * so the only way to know who is signed in is to ask the API.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<SessionState>({ status: "loading" });

  const refresh = useCallback(async () => {
    try {
      setState(await loadSession());
    } catch {
      // The API is unreachable; treat it as signed out so pages can react.
      setState({ status: "unauthenticated" });
    }
  }, []);

  const logout = useCallback(async () => {
    try {
      await apiFetch<void>("/api/auth/logout", { method: "POST" });
    } catch {
      // Already signed out (401) or the API is down: the user still ends
      // up on the login page, and the API remains the authority.
    }
    setState({ status: "unauthenticated", loggedOut: true });
  }, []);

  useEffect(() => {
    let cancelled = false;

    loadSession().then(
      (next) => {
        if (!cancelled) setState(next);
      },
      () => {
        if (!cancelled) setState({ status: "unauthenticated" });
      }
    );

    return () => {
      cancelled = true;
    };
  }, []);

  const value = useMemo<Session>(
    () => ({ ...state, refresh, logout }),
    [state, refresh, logout]
  );

  return (
    <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
  );
}

export function useSession(): Session {
  const session = useContext(SessionContext);

  if (!session) {
    throw new Error("useSession must be used inside SessionProvider.");
  }

  return session;
}

/** Up to two initials from a display name, e.g. "Ada Byron" -> "AB". */
export function initialsOf(displayName: string): string {
  const initials = displayName
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]!.toUpperCase())
    .join("");

  return initials || "?";
}
