"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  BookOpen,
  LayoutDashboard,
  ScrollText,
  Users,
} from "lucide-react";

const navigationItems = [
  {
    label: "Dashboard",
    href: "/dashboard",
    icon: LayoutDashboard,
  },
  {
    label: "Settings",
    href: "/settings",
    icon: BookOpen,
  },
  {
    label: "Characters",
    href: "/characters",
    icon: Users,
  },
];

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className="hidden w-64 shrink-0 border-r border-sidebar-border bg-sidebar text-sidebar-foreground md:flex md:flex-col">
      <div className="flex h-16 items-center gap-2 border-b border-sidebar-border px-6">
        <ScrollText className="h-6 w-6 text-primary" />

        <span className="text-lg font-semibold">
          Lorebound
        </span>
      </div>

      <nav className="flex flex-1 flex-col gap-1 p-4">
        {navigationItems.map((item) => {
          const Icon = item.icon;

          const isActive =
            pathname === item.href ||
            pathname.startsWith(`${item.href}/`);

          return (
            <Link
              key={item.href}
              href={item.href}
              className={[
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
              ].join(" ")}
            >
              <Icon className="h-4 w-4" />

              {item.label}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-sidebar-border p-4">
        <button
          type="button"
          className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-left text-sm text-sidebar-foreground/70 transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
        >
          <div className="flex h-8 w-8 items-center justify-center rounded-full border border-sidebar-border bg-sidebar-primary text-sidebar-primary-foreground">
            JM
          </div>

          <div className="min-w-0">
            <div className="truncate font-medium text-sidebar-foreground">
              Joseph Marlow
            </div>

            <div className="truncate text-xs">
              Profile
            </div>
          </div>
        </button>
      </div>
    </aside>
  );
}