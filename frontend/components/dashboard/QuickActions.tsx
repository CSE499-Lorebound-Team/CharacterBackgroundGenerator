import Link from "next/link";
import { Plus } from "lucide-react";

import { Button } from "@/components/ui/button";

export function QuickActions() {
  return (
    <div className="flex flex-col gap-2 sm:flex-row">
      <Button
        nativeButton={false}
        render={<Link href="/settings" />}
      >
        <Plus className="h-4 w-4" />
        Create Setting
      </Button>

      <Button
        variant="outline"
        nativeButton={false}
        render={<Link href="/builder" />}
      >
        <Plus className="h-4 w-4" />
        Create Character
      </Button>
    </div>
  );
}