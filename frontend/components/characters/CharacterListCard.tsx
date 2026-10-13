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
import { formatRelativeTime } from "@/lib/format";
import type { CharacterListItemDto } from "@/lib/types";

type CharacterListCardProps = {
  character: CharacterListItemDto;
  onDelete: () => void;
};

export function CharacterListCard({ character, onDelete }: CharacterListCardProps) {
  const canBuild = character.status === "Draft" && !character.isReadOnly;

  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="flex min-w-0 items-center gap-2">
            <UserRound className="h-4 w-4 shrink-0 text-primary" />
            <CardTitle className="truncate">{character.name}</CardTitle>
          </div>

          <div className="flex shrink-0 flex-wrap justify-end gap-1">
            <StatusBadge status={character.status} />
            {character.isReadOnly && <ReadOnlyBadge />}
          </div>
        </div>
      </CardHeader>

      <CardContent className="grid grid-cols-2 gap-3 text-sm">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Setting
          </p>
          <p className="truncate">{character.settingName}</p>
        </div>
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Homeland
          </p>
          <p className="truncate">
            {character.homelandName ?? (
              <span className="italic text-muted-foreground">Not chosen</span>
            )}
          </p>
        </div>
        <p className="col-span-2 text-xs text-muted-foreground">
          Updated {formatRelativeTime(character.updatedAt)}
        </p>
      </CardContent>

      <CardFooter className="flex gap-2">
        <Button
          className="flex-1"
          nativeButton={false}
          render={
            <Link href={canBuild ? `/builder/${character.id}` : `/characters/${character.id}`} />
          }
        >
          {canBuild ? "Continue Building" : "View Character"}
        </Button>
        {/* The owner may delete, even a read-only character. */}
        <Button variant="outline" onClick={onDelete}>
          Delete
        </Button>
      </CardFooter>
    </Card>
  );
}
