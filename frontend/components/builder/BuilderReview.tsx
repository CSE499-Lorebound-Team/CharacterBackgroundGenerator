type BuilderReviewProps = {
    characterName: string;
    setting: string;
    homeland?: string;
    culture?: string;
    religion?: string;
    faction?: string;
    motivation?: string;
    backstory?: string;
  };
  
  export function BuilderReview({
    characterName,
    setting,
    homeland,
    culture,
    religion,
    faction,
    motivation,
    backstory,
  }: BuilderReviewProps) {
    const items = [
      {
        label: "Character",
        value: characterName,
      },
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
      <div className="space-y-6">
        <div className="grid gap-4 md:grid-cols-2">
          {items.map((item) => (
            <div
              key={item.label}
              className="rounded-lg border border-border bg-card p-5"
            >
              <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {item.label}
              </p>
  
              <p className="mt-2 whitespace-pre-wrap">
                {item.value || "Not selected"}
              </p>
            </div>
          ))}
        </div>
  
        <div className="rounded-lg border border-border bg-card p-5">
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Backstory
          </p>
  
          {backstory ? (
            <p className="mt-3 whitespace-pre-wrap leading-7">
              {backstory}
            </p>
          ) : (
            <p className="mt-3 text-sm italic text-muted-foreground">
              No backstory provided.
            </p>
          )}
        </div>
      </div>
    );
  }