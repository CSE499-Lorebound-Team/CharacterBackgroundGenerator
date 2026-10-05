import { Search } from "lucide-react";

import { Input } from "@/components/ui/input";

type CharacterSearchProps = {
  value: string;
  onChange:
    (value: string) => void;
};

export function CharacterSearch({
  value,
  onChange,
}: CharacterSearchProps) {
  return (
    <div className="relative max-w-md">
      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />

      <Input
        value={value}
        onChange={(event) =>
          onChange(
            event.target.value
          )
        }
        placeholder="Search characters..."
        className="pl-9"
      />
    </div>
  );
}