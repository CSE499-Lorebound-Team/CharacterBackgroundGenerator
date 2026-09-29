import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

type SettingEntryCardProps = {
  name: string;
  type: string;
  description: string;
  relationshipCount: number;

  isGmOnly?: boolean;
  canEdit?: boolean;

  onEdit?: () => void;
};

export function SettingEntryCard({
  name,
  type,
  description,
  relationshipCount,
  isGmOnly = false,
  canEdit = false,
  onEdit,
}: SettingEntryCardProps) {
  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardHeader className="space-y-2">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <CardTitle>{name}</CardTitle>

          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded-full bg-muted px-2 py-1 text-xs font-medium text-muted-foreground">
              {type}
            </span>

            {isGmOnly && (
              <span className="rounded-full bg-destructive/10 px-2 py-1 text-xs font-medium text-destructive">
                GM Only
              </span>
            )}
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-3">
        <p className="text-sm text-muted-foreground">
          {description}
        </p>

        <p className="text-xs text-muted-foreground">
          {relationshipCount} relationships
        </p>
      </CardContent>

      <CardFooter>
        {canEdit && (
          <Button
            variant="outline"
            className="w-full"
            onClick={onEdit}
          >
            Edit Entry
          </Button>
        )}
      </CardFooter>
    </Card>
  );
}