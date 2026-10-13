import type { Metadata } from "next";

import { AuthCard } from "@/components/auth/AuthCard";
import { JoinInvite } from "@/components/settings/JoinInvite";

export const metadata: Metadata = {
  title: "Join a Setting | Lorebound",
};

// The shareable invite link: /join/<code>. proxy.ts sends a signed-out user
// to /login?next=/join/<code>, so they come back here after signing in.
export default async function JoinPage({ params }: PageProps<"/join/[code]">) {
  const { code } = await params;

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/40 px-4 py-12 text-foreground">
      <AuthCard title="Join a Setting">
        <JoinInvite code={code} />
      </AuthCard>
    </div>
  );
}
