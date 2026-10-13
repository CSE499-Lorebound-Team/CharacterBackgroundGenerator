import "server-only";

import { headers } from "next/headers";

import {
  buildRequest,
  readResponse,
  type ApiRequestInit,
} from "@/lib/api";

// Same default as the rewrite in next.config.ts.
const apiOrigin = process.env.API_ORIGIN ?? "http://localhost:5110";

/**
 * Calls the API from server components and server actions. A relative URL
 * does not work on the server, so this calls API_ORIGIN directly and forwards
 * the incoming request's cookie header (ADR 0001).
 */
export async function apiFetchServer<T>(
  path: string,
  init: ApiRequestInit = {}
): Promise<T> {
  const request = buildRequest(init);
  const requestHeaders = new Headers(request.headers);
  const cookie = (await headers()).get("cookie");

  if (cookie) {
    requestHeaders.set("Cookie", cookie);
  }

  const response = await fetch(new URL(path, apiOrigin), {
    ...request,
    headers: requestHeaders,
    cache: "no-store",
  });

  return readResponse<T>(response);
}
