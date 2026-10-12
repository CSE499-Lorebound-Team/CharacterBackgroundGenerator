import type { ReactNode } from "react";
import Link from "next/link";
import { ScrollText } from "lucide-react";

import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

type AuthCardProps = {
  title: string;
  description?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
};

/** The card every auth page (login, register, email links) sits in. */
export function AuthCard({
  title,
  description,
  children,
  footer,
}: AuthCardProps) {
  return (
    <div className="w-full max-w-md">
      <Link
        href="/"
        className="mb-6 flex items-center justify-center gap-2 text-primary"
      >
        <ScrollText className="h-6 w-6" />
        <span className="font-heading text-xl font-semibold text-foreground">
          Lorebound
        </span>
      </Link>

      <Card>
        <CardHeader>
          <CardTitle className="font-heading text-xl">{title}</CardTitle>
          {description && <CardDescription>{description}</CardDescription>}
        </CardHeader>

        <CardContent>{children}</CardContent>

        {footer && (
          <CardFooter className="justify-center border-t border-border pt-4 text-sm text-muted-foreground">
            {footer}
          </CardFooter>
        )}
      </Card>
    </div>
  );
}
