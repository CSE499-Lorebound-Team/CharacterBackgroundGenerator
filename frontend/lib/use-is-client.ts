import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/**
<<<<<<< HEAD
 * Returns false during server rendering/hydration
 * and true once running in the browser.
=======
 * false during server rendering and hydration, true once running in the
 * browser. Pages that read localStorage use it as a `key` so their content
 * remounts once on the client, without calling setState inside an effect.
>>>>>>> origin/dev
 */
export function useIsClient(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false
  );
<<<<<<< HEAD
}
=======
}
>>>>>>> origin/dev
