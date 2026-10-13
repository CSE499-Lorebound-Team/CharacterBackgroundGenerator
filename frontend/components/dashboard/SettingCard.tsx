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

export function SettingCard({ setting }: { setting: SettingListItemDto }) {
  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2">
            <BookOpen className="h-4 w-4 shrink-0 text-primary" />
            <CardTitle className="truncate">{setting.name}</CardTitle>
          </div>
          <RoleBadge role={setting.myRole} isOwner={setting.isOwner} />
        </div>
      </CardHeader>

      <CardContent className="space-y-1 text-sm text-muted-foreground">
        <p>{plural(setting.entryCount, "entry", "entries")}</p>
        <p>Updated {formatRelativeTime(setting.updatedAt)}</p>
      </CardContent>

      <CardFooter>
        <Button
          variant="outline"
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
