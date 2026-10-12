import type { Metadata } from "next";

import { AuthCard } from "@/components/auth/AuthCard";
import { LoginFooter, LoginForm } from "@/components/auth/LoginForm";
import { safeNextPath } from "@/lib/auth";

export const metadata: Metadata = {
  title: "Log In | Lorebound",
};

export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const { next } = await searchParams;
  const nextPath = safeNextPath(typeof next === "string" ? next : undefined);

  return (
    <AuthCard
      title="Welcome Back"
      description="Log in to continue building your settings and characters."
      footer={<LoginFooter next={nextPath} />}
    >
      <LoginForm next={nextPath} />
    </AuthCard>
  );
}
