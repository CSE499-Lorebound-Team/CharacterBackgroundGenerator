import { Search } from "lucide-react";

import { Input } from "@/components/ui/input";

type SettingSearchProps = {
  value: string;
  onChange: (value: string) => void;
};

export function SettingSearch({
  value,
  onChange,
}: SettingSearchProps) {
  return (
    <div className="relative max-w-md">
      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />

      <Input
        value={value}
        onChange={(event) =>
          onChange(event.target.value)
        }
        placeholder="Search settings..."
        className="pl-9"
      />
    </div>
  );
}