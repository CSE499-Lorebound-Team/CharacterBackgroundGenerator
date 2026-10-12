import type { Metadata } from "next";

import { AuthCard } from "@/components/auth/AuthCard";
import { ResetPasswordForm } from "@/components/auth/ResetPasswordForm";

export const metadata: Metadata = {
  title: "Reset Password | Lorebound",
};

// The target of the emailed link: /reset-password?email=...&code=...
export default async function ResetPasswordPage({
  searchParams,
}: PageProps<"/reset-password">) {
  const { email, code } = await searchParams;

  return (
    <AuthCard title="Reset Password" description="Choose a new password.">
      <ResetPasswordForm
        email={typeof email === "string" ? email : undefined}
        code={typeof code === "string" ? code : undefined}
      />
    </AuthCard>
  );
}
