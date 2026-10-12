import type { Metadata } from "next";

import { AuthCard } from "@/components/auth/AuthCard";
import { RegisterFooter, RegisterForm } from "@/components/auth/RegisterForm";
import { safeNextPath } from "@/lib/auth";

export const metadata: Metadata = {
  title: "Create Account | Lorebound",
};

export default async function RegisterPage({
  searchParams,
}: PageProps<"/register">) {
  const { next } = await searchParams;
  const nextPath = safeNextPath(typeof next === "string" ? next : undefined);

  return (
    <AuthCard
      title="Create Account"
      description="Create your Lorebound account to begin building worlds and characters."
      footer={<RegisterFooter next={nextPath} />}
    >
      <RegisterForm next={nextPath} />
    </AuthCard>
  );
}
