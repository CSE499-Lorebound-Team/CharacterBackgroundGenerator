import Link from "next/link";
import { BookOpen } from "lucide-react";

import { RoleBadge } from "@/components/settings/RoleBadge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { formatRelativeTime, plural } from "@/lib/format";
import type { SettingListItemDto } from "@/lib/types";

type SettingListCardProps = {
  setting: SettingListItemDto;
};

export function SettingListCard({ setting }: SettingListCardProps) {
  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="flex min-w-0 items-center gap-2">
            <BookOpen className="h-4 w-4 shrink-0 text-primary" />
            <CardTitle className="truncate">{setting.name}</CardTitle>
          </div>

          <RoleBadge role={setting.myRole} isOwner={setting.isOwner} />
        </div>
      </CardHeader>

      <CardContent className="space-y-3 text-sm text-muted-foreground">
        {setting.description && (
          <p className="line-clamp-2">{setting.description}</p>
        )}

        <div>
          <p>{plural(setting.entryCount, "entry", "entries")}</p>
          {!setting.isOwner && <p>Run by {setting.ownerDisplayName}</p>}
          <p>Updated {formatRelativeTime(setting.updatedAt)}</p>
        </div>
      </CardContent>

      <CardFooter>
        <Button
          className="w-full"
          nativeButton={false}
          render={<Link href={`/settings/${setting.id}`} />}
        >
          Open Setting
        </Button>
      </CardFooter>
    </Card>
  );
}
