import Link from "next/link";

import { GmOnlyBadge, TypeBadge } from "@/components/settings/EntryBadges";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { plural } from "@/lib/format";
import type { EntryListItemDto } from "@/lib/types";

type SettingEntryCardProps = {
  settingId: string;
  entry: EntryListItemDto;
};

export function SettingEntryCard({ settingId, entry }: SettingEntryCardProps) {
  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader className="space-y-2">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <CardTitle>{entry.name}</CardTitle>

          <div className="flex flex-wrap items-center gap-2">
            <TypeBadge type={entry.entryType} />
            {/* The API only sends isGmOnly to GameMasters. */}
            {entry.isGmOnly && <GmOnlyBadge />}
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-3">
        {entry.description ? (
          <p className="line-clamp-3 text-sm text-muted-foreground">
            {entry.description}
          </p>
        ) : (
          <p className="text-sm italic text-muted-foreground">No description yet.</p>
        )}

        <p className="text-xs text-muted-foreground">
          {plural(entry.relationshipCount, "relationship")}
        </p>
      </CardContent>

      <CardFooter>
        <Button
          variant="outline"
          className="w-full"
          nativeButton={false}
          render={<Link href={`/settings/${settingId}/entries/${entry.id}`} />}
        >
          Open Entry
        </Button>
      </CardFooter>
    </Card>
  );
}
