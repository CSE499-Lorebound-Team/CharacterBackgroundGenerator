import Link from "next/link";

import type { DashboardCountsDto } from "@/lib/types";

/** Four headline numbers, each linking to the list it counts. */
export function DashboardCounts({ counts }: { counts: DashboardCountsDto }) {
  const tiles = [
    { label: "Settings you run", value: counts.settingsAsGm, href: "/settings" },
    { label: "Settings you play in", value: counts.settingsAsPlayer, href: "/settings" },
    { label: "Draft characters", value: counts.charactersDraft, href: "/characters" },
    { label: "Complete characters", value: counts.charactersComplete, href: "/characters" },
  ];

  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {tiles.map((tile) => (
        <Link
          key={tile.label}
          href={tile.href}
          className="border border-border bg-card p-4 transition-colors hover:border-primary/40"
        >
          <p className="text-3xl font-semibold">{tile.value}</p>
          <p className="mt-1 text-sm text-muted-foreground">{tile.label}</p>
        </Link>
      ))}
    </div>
  );
}
