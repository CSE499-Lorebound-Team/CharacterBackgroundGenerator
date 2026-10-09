import "server-only";

import { cookies } from "next/headers";

import {
  ApiError,
} from "@/lib/api";

import type {
  ProblemDetails,
} from "@/lib/types";

function getApiUrl(
  path: string
): string {
  const baseUrl =
    process.env.API_ORIGIN;

  if (!baseUrl) {
    throw new Error(
      "API origin is not configured."
    );
  }

  return new URL(
    path,
    baseUrl
  ).toString();
}

export async function apiFetchServer<T>(
  path: string,
  init: RequestInit = {}
): Promise<T> {
  const cookieStore =
    await cookies();

  const cookieHeader =
    cookieStore
      .getAll()
      .map(
        (cookie) =>
          `${cookie.name}=${cookie.value}`
      )
      .join("; ");

  const requestHeaders =
    new Headers(init.headers);

  requestHeaders.set(
    "X-Requested-With",
    "Lorebound"
  );

  if (cookieHeader) {
    requestHeaders.set(
      "Cookie",
      cookieHeader
    );
  }

  if (
    init.body &&
    !requestHeaders.has(
      "Content-Type"
    ) &&
    typeof init.body === "string"
  ) {
    requestHeaders.set(
      "Content-Type",
      "application/json"
    );
  }

  const response =
    await fetch(
      getApiUrl(path),
      {
        ...init,
        headers:
          requestHeaders,
        cache: "no-store",
      }
    );

  if (!response.ok) {
    let problem:
      | ProblemDetails
      | undefined;

    try {
      problem =
        (await response.json()) as ProblemDetails;
    } catch {
      problem = undefined;
    }

    throw new ApiError(
      response.status,
      problem
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}