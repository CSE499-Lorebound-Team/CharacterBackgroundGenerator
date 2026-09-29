"use client";

export type SettingTab =
  | "Overview"
  | "Nations"
  | "Cities"
  | "Cultures"
  | "Religions"
  | "Factions";

type SettingTabsProps = {
  activeTab: SettingTab;
  onTabChange: (tab: SettingTab) => void;
};

const tabs: SettingTab[] = [
  "Overview",
  "Nations",
  "Cities",
  "Cultures",
  "Religions",
  "Factions",
];

export function SettingTabs({
  activeTab,
  onTabChange,
}: SettingTabsProps) {
  return (
    <div className="overflow-x-auto border-b border-border">
      <div className="flex min-w-max gap-1">
        {tabs.map((tab) => {
          const isActive = tab === activeTab;

          return (
            <button
              key={tab}
              type="button"
              onClick={() => onTabChange(tab)}
              className={[
                "border-b-2 px-3 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "border-primary text-foreground"
                  : "border-transparent text-muted-foreground hover:text-foreground",
              ].join(" ")}
            >
              {tab}
            </button>
          );
        })}
      </div>
    </div>
  );
}