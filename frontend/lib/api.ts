import type { ProblemDetails } from "@/lib/types";

// The API's CSRF check (P1-10) rejects unsafe requests without this header.
const CSRF_HEADER = "X-Requested-With";
const CSRF_VALUE = "Lorebound";

/** Error thrown for any non-2xx response, parsed from problem JSON. */
export class ApiError extends Error {
  status: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
  /** The whole problem JSON, including extensions such as counts. */
  problem?: ProblemDetails;

  constructor(status: number, problem?: ProblemDetails) {
    super(
      problem?.detail ||
        problem?.title ||
        `Request failed with status ${status}`
    );

    this.name = "ApiError";
    this.status = status;
    this.title = problem?.title ?? undefined;
    this.detail = problem?.detail ?? undefined;
    this.errors = problem?.errors;
    this.problem = problem;
  }
}

/** `fetch` options, plus `json`: a value sent as the JSON request body. */
export type ApiRequestInit = RequestInit & {
  json?: unknown;
};

/** Adds the CSRF header and encodes `json`; shared by client and server. */
export function buildRequest(init: ApiRequestInit): RequestInit {
  const { json, ...rest } = init;
  const headers = new Headers(rest.headers);

  headers.set(CSRF_HEADER, CSRF_VALUE);

  if (json !== undefined) {
    headers.set("Content-Type", "application/json");
    return { ...rest, headers, body: JSON.stringify(json) };
  }

  return { ...rest, headers };
}

/** Returns the JSON body (undefined when empty) or throws `ApiError`. */
export async function readResponse<T>(response: Response): Promise<T> {
  const text = await response.text();

  if (!response.ok) {
    let problem: ProblemDetails | undefined;

    try {
      problem = text ? (JSON.parse(text) as ProblemDetails) : undefined;
    } catch {
      problem = undefined;
    }

    throw new ApiError(response.status, problem);
  }

  return (text ? JSON.parse(text) : undefined) as T;
}

/**
 * Calls the API from the browser. `path` is same-origin (`/api/...`);
 * next.config.ts rewrites it to API_ORIGIN (ADR 0001), so the auth cookie
 * stays on the frontend's origin. No token is ever read or stored in JS.
 */
export async function apiFetch<T>(
  path: string,
  init: ApiRequestInit = {}
): Promise<T> {
  const response = await fetch(path, {
    ...buildRequest(init),
    credentials: "include",
  });

  return readResponse<T>(response);
}
