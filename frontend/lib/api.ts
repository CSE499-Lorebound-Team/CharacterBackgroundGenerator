import type {
    ProblemDetails,
  } from "@/lib/types";
  
  export class ApiError extends Error {
    status: number;
    title?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  
    constructor(
      status: number,
      problem?: ProblemDetails
    ) {
      super(
        problem?.detail ||
          problem?.title ||
          `Request failed with status ${status}`
      );
  
      this.name = "ApiError";
      this.status = status;
      this.title = problem?.title;
      this.detail = problem?.detail;
      this.errors = problem?.errors;
    }
  }
  
  
  export async function apiFetch<T>(
    path: string,
    init: RequestInit = {}
  ): Promise<T> {
    const headers =
      new Headers(init.headers);
  
    headers.set(
      "X-Requested-With",
      "Lorebound"
    );
  
    if (
      init.body &&
      !headers.has("Content-Type") &&
      typeof init.body === "string"
    ) {
      headers.set(
        "Content-Type",
        "application/json"
      );
    }
  
    const response =
      await fetch(path, {
        ...init,
        headers,
        credentials: "include",
      });
  
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
  
      const error =
        new ApiError(
          response.status,
          problem
        );
  
        throw new ApiError(
            response.status,
            problem
          );
  
      throw error;
    }
  
    if (response.status === 204) {
      return undefined as T;
    }
  
    return (await response.json()) as T;
  }