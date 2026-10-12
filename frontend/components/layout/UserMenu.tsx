"use client";

import { useState } from "react";
import { LogOut } from "lucide-react";

import { initialsOf, useSession } from "@/lib/session";

/** The signed-in user and a Log out action, at the foot of the navigation. */
export function UserMenu() {
  const session = useSession();
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  const user = session.status === "authenticated" ? session.user : null;

  async function handleLogout() {
    setIsLoggingOut(true);
    // AppShell sends the user to /login once the session is cleared.
    await session.logout();
  }

  return (
    <div className="flex items-center gap-3 px-3 py-2 text-sm">
      <div
        aria-hidden="true"
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-sidebar-border bg-sidebar-primary text-xs font-semibold text-sidebar-primary-foreground"
      >
        {user ? initialsOf(user.displayName) : ""}
      </div>

      <div className="min-w-0 flex-1">
        {user ? (
          <>
            <div className="truncate font-medium text-sidebar-foreground">
              {user.displayName}
            </div>
            <div className="truncate text-xs text-sidebar-foreground/70">
              {user.email}
            </div>
          </>
        ) : (
          <div className="space-y-1.5" aria-label="Loading user">
            <div className="h-3 w-24 animate-pulse rounded bg-sidebar-accent" />
            <div className="h-2.5 w-32 animate-pulse rounded bg-sidebar-accent" />
          </div>
        )}
      </div>

      <button
        type="button"
        onClick={handleLogout}
        disabled={!user || isLoggingOut}
        className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-sidebar-foreground/70 transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground disabled:opacity-50"
        aria-label="Log out"
        title="Log out"
      >
        <LogOut className="h-4 w-4" />
      </button>
    </div>
  );
}
