"use client";

import { ENTRY_TYPES } from "@/lib/entry-types";
import type { SettingEntryType } from "@/lib/types";

export type SettingTab = "Overview" | SettingEntryType;

type SettingTabsProps = {
  activeTab: SettingTab;
  onTabChange: (tab: SettingTab) => void;
  /** Entry counts by type from the setting (GM-only left out for Players). */
  counts: Partial<Record<SettingEntryType, number>>;
};

/** Overview plus one tab per API entry type, each with its count. */
export function SettingTabs({ activeTab, onTabChange, counts }: SettingTabsProps) {
  const tabs: { tab: SettingTab; label: string; count?: number }[] = [
    { tab: "Overview", label: "Overview" },
    ...ENTRY_TYPES.map((info) => ({
      tab: info.type,
      label: info.plural,
      count: counts[info.type] ?? 0,
    })),
  ];

  return (
    <div className="overflow-x-auto border-b border-border">
      <div role="tablist" aria-label="Entry types" className="flex min-w-max gap-1">
        {tabs.map(({ tab, label, count }) => {
          const isActive = tab === activeTab;

          return (
            <button
              key={tab}
              type="button"
              role="tab"
              aria-selected={isActive}
              onClick={() => onTabChange(tab)}
              className={[
                "flex items-center gap-1.5 border-b-2 px-3 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "border-primary text-foreground"
                  : "border-transparent text-muted-foreground hover:text-foreground",
              ].join(" ")}
            >
              {label}
              {count !== undefined && (
                <span className="rounded-full bg-muted px-1.5 text-xs text-muted-foreground">
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
