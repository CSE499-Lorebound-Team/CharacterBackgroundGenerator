import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/**
 * Returns false during server rendering/hydration
 * and true once running in the browser.
 */
export function useIsClient(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false
  );
}