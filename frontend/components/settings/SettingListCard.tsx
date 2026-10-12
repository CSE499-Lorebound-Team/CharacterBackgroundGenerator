import Link from "next/link";
import {
  BookOpen,
  MoreHorizontal,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

type SettingListCardProps = {
  id: string;
  name: string;
  description: string;
  entryCount: number;
  updatedAt: string;
};

export function SettingListCard({
  id,
  name,
  description,
  entryCount,
  updatedAt,
}: SettingListCardProps) {
  const updatedText = new Date(updatedAt).toLocaleDateString();

  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-2">
            <BookOpen className="h-4 w-4 text-primary" />
            <CardTitle>{name}</CardTitle>
          </div>

          <Button
            variant="ghost"
            size="icon"
          >
            <MoreHorizontal className="h-4 w-4" />
            <span className="sr-only">
              Setting options
            </span>
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-3 text-sm text-muted-foreground">
        {description && (
          <p className="line-clamp-2">
            {description}
          </p>
        )}

        <div>
          <p>{entryCount} {entryCount === 1 ? "entry" : "entries"}</p>
          <p>Updated {updatedText}</p>
        </div>
      </CardContent>

      <CardFooter>
      <Button
        className="w-full"
        nativeButton={false}
        render={
          <Link href={`/settings/${id}`} />
        }
      >
        Open Setting
      </Button>
      </CardFooter>
    </Card>
  );
}