import type { Metadata } from "next";

import { AuthCard } from "@/components/auth/AuthCard";
import { ConfirmEmail } from "@/components/auth/ConfirmEmail";

export const metadata: Metadata = {
  title: "Confirm Email | Lorebound",
};

// The target of the emailed link: /confirm-email?userId=...&code=...
export default async function ConfirmEmailPage({
  searchParams,
}: PageProps<"/confirm-email">) {
  const { userId, code } = await searchParams;

  return (
    <AuthCard title="Confirm Email">
      <ConfirmEmail
        userId={typeof userId === "string" ? userId : undefined}
        code={typeof code === "string" ? code : undefined}
      />
    </AuthCard>
  );
}
