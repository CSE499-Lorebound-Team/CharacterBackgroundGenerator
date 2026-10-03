"use client";

import { useState } from "react";
import { AppShell } from "@/components/layout/AppShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { BuilderProgress } from "@/components/builder/BuilderProgress";
import { BuilderOptionCard } from "@/components/builder/BuilderOptionCard";
import { CharacterSummaryPanel } from "@/components/builder/CharacterSummaryPanel";
import { Button } from "@/components/ui/button";

const homelandOptions = [
  {
    title: "Sasymon",
    description:
      "A major cultural and political region with strong traditions and influential cities.",
  },
  {
    title: "Ymenite Region",
    description:
      "A culturally distinct region with deep historical roots and powerful local identities.",
  },
  {
    title: "Northern Marches",
    description:
      "A frontier region shaped by trade, conflict, and contact with neighboring peoples.",
  },
];

export default function BuilderPage() {
  const [selectedHomeland, setSelectedHomeland] = useState<string>();

  return (
    <AppShell>
      <PageHeader
        title="Character Background Builder"
        description="Build your character's history, relationships, and connections to the setting."
      />

      <div className="space-y-8">
        <BuilderProgress
          currentStep={2}
          totalSteps={8}
          stepName="Choose Your Homeland"
        />

        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_320px]">
          <section className="space-y-6">
            <div>
              <h2 className="font-heading text-2xl font-semibold">
                Where is your character from?
              </h2>

              <p className="mt-1 text-muted-foreground">
                Choose the homeland that best fits your character&apos;s origin.
              </p>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              {homelandOptions.map((option) => (
                <BuilderOptionCard
                  key={option.title}
                  title={option.title}
                  description={option.description}
                  selected={selectedHomeland === option.title}
                  onSelect={() => setSelectedHomeland(option.title)}
                />
              ))}
            </div>

            <div className="flex flex-col-reverse gap-2 border-t border-border pt-6 sm:flex-row sm:justify-between">
              <Button variant="outline">
                Back
              </Button>

              <Button disabled={!selectedHomeland}>
                Next
              </Button>
            </div>
          </section>

          <CharacterSummaryPanel
            setting="Osepia"
            homeland={selectedHomeland}
          />
        </div>
      </div>
    </AppShell>
  );
}