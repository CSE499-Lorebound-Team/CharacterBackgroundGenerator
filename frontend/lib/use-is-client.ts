import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/**
 * false during server rendering and hydration,
 * true once running in the browser.
 */
export function useIsClient(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false
  );
}