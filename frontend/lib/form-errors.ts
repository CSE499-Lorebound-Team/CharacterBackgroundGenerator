import { ApiError } from "@/lib/api";

/** Validation messages by camelCase field name; undefined means valid. */
export type FieldErrors = Record<string, string | undefined>;

export function hasErrors(errors: FieldErrors): boolean {
  return Object.values(errors).some(Boolean);
}

/**
 * Splits a 400 validation problem into per-field messages (keyed by the
 * camelCase field name) and a general message for errors without a field.
 * The API keys fields in PascalCase ("Email") and uses "" for the rest.
 */
export function readValidationErrors(
  error: ApiError,
  fields: string[]
): { fieldErrors: FieldErrors; general?: string } {
  const fieldErrors: FieldErrors = {};
  const general: string[] = [];

  for (const [key, messages] of Object.entries(error.errors ?? {})) {
    const field = fields.find(
      (name) => name.toLowerCase() === key.toLowerCase()
    );

    if (field) {
      fieldErrors[field] = messages.join(" ");
    } else {
      general.push(...messages);
    }
  }

  if (!error.errors && error.detail) {
    general.push(error.detail);
  }

  return {
    fieldErrors,
    general: general.length > 0 ? general.join(" ") : undefined,
  };
}
