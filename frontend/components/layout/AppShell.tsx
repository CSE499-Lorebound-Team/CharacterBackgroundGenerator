"use client";

import { type ReactNode, useEffect } from "react";
import { useRouter } from "next/navigation";

import { useSession } from "@/lib/session";
import { MobileNav } from "./MobileNav";
import { Sidebar } from "./Sidebar";

type AppShellProps = {
  children: ReactNode;
};

export function AppShell({ children }: AppShellProps) {
  const router = useRouter();
  const session = useSession();

  // proxy.ts only checks that a cookie exists; this catches an expired one
  // and a logout. After Log out the login page opens without `next`.
  const loggedOut =
    session.status === "unauthenticated" ? Boolean(session.loggedOut) : null;

  useEffect(() => {
    if (loggedOut === null) return;

    const next = window.location.pathname + window.location.search;
    router.replace(
      loggedOut ? "/login" : `/login?next=${encodeURIComponent(next)}`
    );
  }, [loggedOut, router]);

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="flex min-h-screen">
        <Sidebar />

        <div className="min-w-0 flex-1">
          <MobileNav />

          <main>
            <div className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
              {children}
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}
