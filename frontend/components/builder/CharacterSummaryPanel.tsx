type CharacterSummaryPanelProps = {
  setting?: string;
  homeland?: string;
  culture?: string;
  religion?: string;
  faction?: string;
  motivation?: string;
};

export function CharacterSummaryPanel({
  setting,
  homeland,
  culture,
  religion,
  faction,
  motivation,
}: CharacterSummaryPanelProps) {
  const summaryItems = [
    {
      label: "Setting",
      value: setting,
    },
    {
      label: "Homeland",
      value: homeland,
    },
    {
      label: "Culture",
      value: culture,
    },
    {
      label: "Religion",
      value: religion,
    },
    {
      label: "Faction",
      value: faction,
    },
    {
      label: "Motivation",
      value: motivation,
    },
  ];

  return (
    <aside className="rounded-lg border border-border bg-card p-5">
      <h2 className="font-heading text-lg font-semibold">
        Character Summary
      </h2>

      <div className="mt-4 space-y-4">
        {summaryItems.map((item) => (
          <div key={item.label}>
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {item.label}
            </p>

            <p className="mt-1 text-sm text-foreground">
              {item.value ||
                "Not selected"}
            </p>
          </div>
        ))}
      </div>
    </aside>
  );
}