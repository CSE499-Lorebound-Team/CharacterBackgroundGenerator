import Link from "next/link";
import { BookOpen } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

type SettingCardProps = {
  id: string;
  name: string;
  entryCount: number;
  updatedAt: string;
};

export function SettingCard({
  id,
  name,
  entryCount,
  updatedAt,
}: SettingCardProps) {
  const updatedText = new Date(updatedAt).toLocaleDateString();

  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader className="space-y-2">
        <div className="flex items-center gap-2">
          <BookOpen className="h-4 w-4 text-primary" />
          <CardTitle>{name}</CardTitle>
        </div>
      </CardHeader>

      <CardContent className="space-y-1 text-sm text-muted-foreground">
        <p>{entryCount} entries</p>
        <p>Updated {updatedText}</p>
      </CardContent>

      <CardFooter>
        <Button
          variant="outline"
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