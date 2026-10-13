import Link from "next/link";
import { BookOpen, ScrollText, UserRound } from "lucide-react";

import { formatRelativeTime } from "@/lib/format";
import type { DashboardActivityDto, DashboardActivityKind } from "@/lib/types";

const icons: Record<DashboardActivityKind, typeof BookOpen> = {
  Setting: BookOpen,
  Entry: ScrollText,
  Character: UserRound,
};

function hrefFor(activity: DashboardActivityDto): string {
  switch (activity.kind) {
    case "Setting":
      return `/settings/${activity.id}`;
    case "Entry":
      return `/settings/${activity.settingId}/entries/${activity.id}`;
    case "Character":
      return `/characters/${activity.id}`;
  }
}

/** The latest changes the user can see, newest first (GET /api/dashboard). */
export function RecentActivity({ activity }: { activity: DashboardActivityDto[] }) {
  if (activity.length === 0) {
    return (
      <p className="border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
        Nothing yet. Changes to your settings, their lore and your characters show here.
      </p>
    );
  }

  return (
    <ul className="divide-y divide-border border border-border">
      {activity.map((item) => {
        const Icon = icons[item.kind];

        return (
          <li key={`${item.kind}:${item.id}`}>
            <Link
              href={hrefFor(item)}
              className="flex items-center gap-3 px-4 py-3 text-sm transition-colors hover:bg-muted"
            >
              <Icon className="h-4 w-4 shrink-0 text-primary" />
              <span className="min-w-0 flex-1 truncate">{item.text}</span>
              <span className="shrink-0 text-xs text-muted-foreground">
                {formatRelativeTime(item.at)}
              </span>
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
