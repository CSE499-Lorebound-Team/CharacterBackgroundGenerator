import Link from "next/link";
import { UserRound } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

type CharacterListCardProps = {
  id: string;
  name: string;
  settingName: string;

  status:
    | "Draft"
    | "Complete";
};

export function CharacterListCard({
  id,
  name,
  settingName,
  status,
}: CharacterListCardProps) {
  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-2">
            <UserRound className="h-4 w-4 text-primary" />

            <CardTitle>
              {name}
            </CardTitle>
          </div>

          <span
            className={[
              "rounded-full px-2 py-1 text-xs font-medium",
              status === "Complete"
                ? "bg-primary/10 text-primary"
                : "bg-muted text-muted-foreground",
            ].join(" ")}
          >
            {status}
          </span>
        </div>
      </CardHeader>

      <CardContent className="space-y-2 text-sm">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Setting
          </p>

          <p>
            {settingName}
          </p>
        </div>
      </CardContent>

      <CardFooter>
        <Button
          className="w-full"
          nativeButton={false}
          render={
            <Link
              href={`/characters/${id}`}
            />
          }
        >
          {status === "Draft"
            ? "Open Character"
            : "View Character"}
        </Button>
      </CardFooter>
    </Card>
  );
}