import type { SettingRole } from "@/lib/types";
import { cn } from "@/lib/utils";

type RoleBadgeProps = {
  role: SettingRole;
  isOwner?: boolean;
};

/** The caller's role in a setting: "Owner", "GM" or "Player". */
export function RoleBadge({ role, isOwner = false }: RoleBadgeProps) {
  const label = isOwner ? "Owner" : role === "GameMaster" ? "GM" : "Player";

  return (
    <span
      className={cn(
        "rounded-full px-2 py-0.5 text-xs font-medium",
        role === "GameMaster"
          ? "bg-primary/10 text-primary"
          : "bg-muted text-muted-foreground"
      )}
    >
      {label}
    </span>
  );
}
