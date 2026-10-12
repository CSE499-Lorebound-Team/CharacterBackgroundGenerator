import Link from "next/link";
import {
  BookOpen,
  Link2,
  ScrollText,
  Sparkles,
  Users,
} from "lucide-react";

import { buttonVariants } from "@/components/ui/button";

export default function Home() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b border-border">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
          <div className="flex items-center gap-2">
            <ScrollText className="h-6 w-6 text-primary" />

            <span className="font-heading text-xl font-semibold">
              Lorebound
            </span>
          </div>

          <div className="flex items-center gap-2">
          <Link
            href="/login"
            className={buttonVariants({ variant: "ghost" })}
          >
            Log In
          </Link>

          <Link href="/register" className={buttonVariants()}>
            Create Account
          </Link>
          </div>
        </div>
      </header>

      <main>
        <section className="border-b border-border">
          <div className="mx-auto grid max-w-7xl gap-12 px-4 py-20 sm:px-6 lg:grid-cols-2 lg:px-8 lg:py-28">
            <div className="flex flex-col justify-center">
              <div className="mb-4 flex items-center gap-2 text-sm font-medium text-primary">
                <Sparkles className="h-4 w-4" />
                Narrative character creation
              </div>

              <h1 className="font-heading text-4xl font-bold tracking-tight sm:text-5xl lg:text-6xl">
                Build characters that belong in the world.
              </h1>

              <p className="mt-6 max-w-2xl text-lg leading-8 text-muted-foreground">
                Lorebound helps Game Masters build rich campaign settings and
                gives players a guided way to create character histories,
                relationships, motivations, and connections to those worlds.
              </p>

              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Link
                href="/register"
                className={buttonVariants({ size: "lg" })}
              >
                Create Account
              </Link>

              <Link
                href="/login"
                className={buttonVariants({ variant: "outline", size: "lg" })}
              >
                Log In
              </Link>
              </div>
            </div>

            <div className="flex items-center justify-center">
              <div className="w-full max-w-lg rounded-xl border border-border bg-card p-6 shadow-sm">
                <div className="mb-6 flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10">
                    <BookOpen className="h-5 w-5 text-primary" />
                  </div>

                  <div>
                    <p className="font-semibold">
                      Your character&apos;s story
                    </p>

                    <p className="text-sm text-muted-foreground">
                      Shaped by the campaign setting
                    </p>
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="rounded-lg border border-border bg-background p-4">
                    <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Homeland
                    </p>

                    <p className="mt-1 font-medium">
                      Sasymon
                    </p>
                  </div>

                  <div className="rounded-lg border border-border bg-background p-4">
                    <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Culture
                    </p>

                    <p className="mt-1 font-medium">
                      River Cities
                    </p>
                  </div>

                  <div className="rounded-lg border border-border bg-background p-4">
                    <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Motivation
                    </p>

                    <p className="mt-1 font-medium">
                      Restore the honor of a fallen household
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-7xl px-4 py-20 sm:px-6 lg:px-8">
          <div className="mx-auto max-w-2xl text-center">
            <h2 className="font-heading text-3xl font-bold tracking-tight">
              Build more than a character sheet
            </h2>

            <p className="mt-4 text-muted-foreground">
              Lorebound connects character creation directly to the people,
              places, cultures, and history of a campaign setting.
            </p>
          </div>

          <div className="mt-12 grid gap-6 md:grid-cols-3">
            <FeatureCard
              icon={<BookOpen className="h-5 w-5" />}
              title="Build the World"
              description="Game Masters organize locations, cultures, factions, religions, professions, events, and other setting lore."
            />

            <FeatureCard
              icon={<Users className="h-5 w-5" />}
              title="Create Your Character"
              description="Players move through a guided background process focused on history, relationships, beliefs, and motivations."
            />

            <FeatureCard
              icon={<Link2 className="h-5 w-5" />}
              title="Connect the Story"
              description="Character choices are tied directly to the setting, helping every background feel like part of the same world."
            />
          </div>
        </section>

        <section className="border-t border-border bg-muted/40">
          <div className="mx-auto flex max-w-7xl flex-col items-center px-4 py-16 text-center sm:px-6 lg:px-8">
            <h2 className="font-heading text-3xl font-bold">
              Your story starts with the world around you.
            </h2>

            <p className="mt-4 max-w-xl text-muted-foreground">
              Create a Lorebound account and start building campaign settings
              and character backgrounds together.
            </p>

            <Link
              href="/register"
              className={buttonVariants({ size: "lg", className: "mt-8" })}
            >
              Get Started
            </Link>
          </div>
        </section>
      </main>

      <footer className="border-t border-border">
        <div className="mx-auto flex max-w-7xl flex-col gap-2 px-4 py-6 text-sm text-muted-foreground sm:px-6 md:flex-row md:items-center md:justify-between lg:px-8">
          <div className="flex items-center gap-2">
            <ScrollText className="h-4 w-4 text-primary" />
            <span>Lorebound</span>
          </div>

          <span>
            Narrative character building for tabletop RPGs.
          </span>
        </div>
      </footer>
    </div>
  );
}

type FeatureCardProps = {
  icon: React.ReactNode;
  title: string;
  description: string;
};

function FeatureCard({
  icon,
  title,
  description,
}: FeatureCardProps) {
  return (
    <div className="rounded-xl border border-border bg-card p-6">
      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
        {icon}
      </div>

      <h3 className="mt-5 font-heading text-lg font-semibold">
        {title}
      </h3>

      <p className="mt-2 text-sm leading-6 text-muted-foreground">
        {description}
      </p>
    </div>
  );
}