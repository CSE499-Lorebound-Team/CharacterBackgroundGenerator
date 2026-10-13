"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { LogOut } from "lucide-react";

import { ConfirmDialog } from "@/components/forms/ConfirmDialog";
import { EmptyState, ErrorState, LoadingCards } from "@/components/layout/PageStates";
import { useToast } from "@/components/layout/Toaster";
import { RoleBadge } from "@/components/settings/RoleBadge";
import { Button } from "@/components/ui/button";
import { formatRelativeTime } from "@/lib/format";
import { initialsOf, useSession } from "@/lib/session";
import type {
  MemberDto,
  SettingDetailDto,
  SettingRole,
  UpdateMemberRoleRequest,
} from "@/lib/types";
import { usePagedList } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";

type Pending =
  | { kind: "remove"; member: MemberDto }
  | { kind: "role"; member: MemberDto; role: SettingRole }
  | { kind: "leave" };

type SettingMembersProps = {
  setting: SettingDetailDto;
  /** Reloads the setting header (member count). */
  onChanged: () => void;
};

/**
 * Who belongs to the setting. The owner changes roles; GameMasters remove
 * Players (the owner removes anyone); everyone but the owner may leave.
 */
export function SettingMembers({ setting, onChanged }: SettingMembersProps) {
  const router = useRouter();
  const fetchApi = useApiFetch();
  const session = useSession();
  const toast = useToast();
  const myUserId = session.status === "authenticated" ? session.user.id : null;

  // A setting has few members; 100 is the API's page size cap.
  const members = usePagedList<MemberDto>(`/api/settings/${setting.id}/members`, 100);
  // The last action asked about; kept while the dialog animates closed.
  const [pending, setPending] = useState<Pending>();
  const [confirmOpen, setConfirmOpen] = useState(false);

  function ask(action: Pending) {
    setPending(action);
    setConfirmOpen(true);
  }

  const isGm = setting.myRole === "GameMaster";

  function canRemove(member: MemberDto): boolean {
    if (member.isOwner || member.userId === myUserId) return false;
    if (setting.isOwner) return true;
    return isGm && member.role === "Player";
  }

  async function confirmPending() {
    if (!pending) return;

    if (pending.kind === "role") {
      await fetchApi<MemberDto>(
        `/api/settings/${setting.id}/members/${pending.member.userId}`,
        {
          method: "PATCH",
          json: { role: pending.role } satisfies UpdateMemberRoleRequest,
        }
      );
      members.reload();
      toast(`${pending.member.displayName} is now ${pending.role === "GameMaster" ? "a Game Master" : "a Player"}`);
      return;
    }

    const userId = pending.kind === "leave" ? myUserId : pending.member.userId;
    await fetchApi<void>(`/api/settings/${setting.id}/members/${userId}`, {
      method: "DELETE",
    });

    if (pending.kind === "leave") {
      // Takes effect at once: the setting now answers 404 for this user.
      toast(`You left ${setting.name}`);
      router.push("/settings");
      return;
    }

    members.reload();
    onChanged();
    toast(`Removed ${pending.member.displayName}`);
  }

  const dialog = describe(pending, setting.name);

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-heading text-xl font-semibold">Members</h2>

        {!setting.isOwner && (
          <Button variant="outline" onClick={() => ask({ kind: "leave" })}>
            <LogOut className="h-4 w-4" />
            Leave Setting
          </Button>
        )}
      </div>

      {members.error ? (
        <ErrorState error={members.error} onRetry={members.reload} />
      ) : !members.hasLoaded ? (
        <LoadingCards count={2} />
      ) : members.items.length === 0 ? (
        <EmptyState title="No members" />
      ) : (
        <ul className="divide-y divide-border border border-border">
          {members.items.map((member) => (
            <li
              key={member.userId}
              className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center sm:justify-between"
            >
              <div className="flex min-w-0 items-center gap-3">
                <div
                  aria-hidden="true"
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold"
                >
                  {initialsOf(member.displayName)}
                </div>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="truncate font-medium">
                      {member.displayName}
                      {member.userId === myUserId && (
                        <span className="text-muted-foreground"> (you)</span>
                      )}
                    </span>
                    <RoleBadge role={member.role} isOwner={member.isOwner} />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    Joined {formatRelativeTime(member.joinedAt)}
                  </p>
                </div>
              </div>

              <div className="flex flex-wrap gap-2">
                {/* Only the owner changes roles; the owner stays a GameMaster. */}
                {setting.isOwner && !member.isOwner && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      ask({
                        kind: "role",
                        member,
                        role: member.role === "GameMaster" ? "Player" : "GameMaster",
                      })
                    }
                  >
                    {member.role === "GameMaster" ? "Make Player" : "Make GM"}
                  </Button>
                )}
                {canRemove(member) && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => ask({ kind: "remove", member })}
                  >
                    Remove
                  </Button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={dialog.title}
        description={dialog.description}
        confirmLabel={dialog.confirmLabel}
        destructive={pending?.kind !== "role"}
        onConfirm={confirmPending}
      />
    </section>
  );
}

function describe(pending: Pending | undefined, settingName: string) {
  switch (pending?.kind) {
    case "remove":
      return {
        title: `Remove ${pending.member.displayName}?`,
        description: `They lose access to ${settingName} at once. Their characters are kept, read-only.`,
        confirmLabel: "Remove",
      };
    case "role":
      return pending.role === "GameMaster"
        ? {
            title: `Make ${pending.member.displayName} a Game Master?`,
            description: "Game Masters see GM-only lore, edit entries, manage invites and remove Players.",
            confirmLabel: "Make GM",
          }
        : {
            title: `Make ${pending.member.displayName} a Player?`,
            description: "They will no longer see GM-only lore or be able to edit the setting.",
            confirmLabel: "Make Player",
          };
    case "leave":
      return {
        title: `Leave ${settingName}?`,
        description: "You lose access at once. Your characters in this setting are kept, read-only. You need a new invite to come back.",
        confirmLabel: "Leave Setting",
      };
    default:
      return { title: "", description: "", confirmLabel: "" };
  }
}
