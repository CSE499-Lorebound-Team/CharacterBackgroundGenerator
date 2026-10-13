import Link from "next/link";
import { UserRound } from "lucide-react";

import { ReadOnlyBadge, StatusBadge } from "@/components/characters/CharacterBadges";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import type { CharacterListItemDto } from "@/lib/types";

export function CharacterCard({ character }: { character: CharacterListItemDto }) {
  const canBuild = character.status === "Draft" && !character.isReadOnly;

  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2">
            <UserRound className="h-4 w-4 shrink-0 text-primary" />
            <CardTitle className="truncate">{character.name}</CardTitle>
          </div>
          <div className="flex shrink-0 gap-1">
            <StatusBadge status={character.status} />
            {character.isReadOnly && <ReadOnlyBadge />}
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-1 text-sm text-muted-foreground">
        <p className="truncate">{character.settingName}</p>
        <p className="truncate">
          {character.homelandName ? `From ${character.homelandName}` : "No homeland yet"}
        </p>
      </CardContent>

      <CardFooter>
        <Button
          variant="outline"
          className="w-full"
          nativeButton={false}
          render={
            <Link href={canBuild ? `/builder/${character.id}` : `/characters/${character.id}`} />
          }
        >
          {canBuild ? "Continue Building" : "View Character"}
        </Button>
      </CardFooter>
    </Card>
  );
}
