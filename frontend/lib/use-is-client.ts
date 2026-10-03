import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/**
 * false during server rendering and hydration, true once running in the
 * browser. Pages that read localStorage use it as a `key` so their content
 * remounts once on the client, without calling setState inside an effect.
 */
export function useIsClient(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false
  );
}
