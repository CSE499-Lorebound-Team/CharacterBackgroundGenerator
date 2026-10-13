"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CircleX, Users } from "lucide-react";

import { FormAlert } from "@/components/forms/FormAlert";
import { errorMessage } from "@/components/layout/PageStates";
import { useToast } from "@/components/layout/Toaster";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import type { AcceptInviteResponse, InvitePreviewDto } from "@/lib/types";
import { useApiData } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";

/** Previews an invite code and joins its setting as a Player. */
export function JoinInvite({ code }: { code: string }) {
  const router = useRouter();
  const fetchApi = useApiFetch();
  const toast = useToast();
  const path = `/api/invites/${encodeURIComponent(code)}`;

  // A 401 sends the user to /login?next=/join/<code> (useApiFetch).
  const preview = useApiData<InvitePreviewDto>(path);
  const [joinError, setJoinError] = useState<string>();
  const [isJoining, setIsJoining] = useState(false);

  async function handleJoin() {
    setJoinError(undefined);
    setIsJoining(true);

    try {
      // Safe to repeat: a member gets 200 with their role and uses nothing.
      const result = await fetchApi<AcceptInviteResponse>(`${path}/accept`, {
        method: "POST",
      });
      if (!preview.data?.alreadyMember) toast(`Joined ${preview.data?.settingName ?? "the setting"}`);
      router.push(`/settings/${result.settingId}`);
    } catch (error) {
      setJoinError(
        error instanceof ApiError && error.status === 404
          ? invalidMessage
          : errorMessage(error)
      );
      setIsJoining(false);
    }
  }

  if (preview.error) {
    const invalid = preview.error instanceof ApiError && preview.error.status === 404;

    return (
      <div className="space-y-5 text-center">
        <CircleX className="mx-auto h-10 w-10 text-destructive" />
        {/* The API answers the same 404 for unknown, expired, revoked and used-up codes. */}
        <p>{invalid ? invalidMessage : errorMessage(preview.error)}</p>
        <Link href="/settings" className="inline-block font-medium text-primary hover:underline">
          Go to your settings
        </Link>
      </div>
    );
  }

  if (!preview.data) {
    return <p className="py-4 text-center text-muted-foreground">Checking the invite...</p>;
  }

  const { settingName, gmDisplayName, alreadyMember } = preview.data;

  return (
    <div className="space-y-5 text-center">
      <Users className="mx-auto h-10 w-10 text-primary" />

      <div>
        <p className="text-lg font-semibold">{settingName}</p>
        <p className="text-sm text-muted-foreground">Run by {gmDisplayName}</p>
      </div>

      <p className="text-sm">
        {alreadyMember
          ? "You are already a member of this setting."
          : "You are invited to join this setting as a Player. You will see its shared lore and can build characters in it."}
      </p>

      {joinError && <FormAlert>{joinError}</FormAlert>}

      <Button className="w-full" disabled={isJoining} onClick={handleJoin}>
        {isJoining ? "Joining..." : alreadyMember ? "Open Setting" : "Join Setting"}
      </Button>
    </div>
  );
}

const invalidMessage =
  "This invite link is invalid or has expired. Ask your Game Master for a new one.";
