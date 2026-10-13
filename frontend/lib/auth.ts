import { ApiError } from "@/lib/api";

// Mirrors the API's rules (RegisterRequest and the Identity options in
// api/Auth/AuthenticationSetup.cs); the API still checks everything.
export const PASSWORD_MIN_LENGTH = 10;
export const DISPLAY_NAME_MAX_LENGTH = 60;
export const EMAIL_MAX_LENGTH = 256;

export function validateEmail(email: string): string | undefined {
  const value = email.trim();

  if (!value) return "Enter your email address.";
  if (value.length > EMAIL_MAX_LENGTH) return "That email address is too long.";
  if (!/^[^\s@]+@[^\s@]+$/.test(value)) return "Enter a valid email address.";
  return undefined;
}

export function validateNewPassword(password: string): string | undefined {
  if (!password) return "Enter a password.";
  if (password.length < PASSWORD_MIN_LENGTH) {
    return `Use at least ${PASSWORD_MIN_LENGTH} characters.`;
  }
  return undefined;
}

export function validateDisplayName(displayName: string): string | undefined {
  const value = displayName.trim();

  if (!value) return "Enter a display name.";
  if (value.length > DISPLAY_NAME_MAX_LENGTH) {
    return `Use at most ${DISPLAY_NAME_MAX_LENGTH} characters.`;
  }
  return undefined;
}

/** Banner text for a failed auth call that is not a field validation error. */
export function authErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    switch (error.status) {
      case 401:
        return "Invalid email or password.";
      case 423:
        return "Too many failed sign-in attempts. Your account is locked for a few minutes; try again later.";
      case 429:
        return "Too many attempts. Wait a minute and try again.";
    }

    if (error.status < 500 && error.detail) return error.detail;
  }

  return "Something went wrong. Please try again.";
}

/**
 * The page to open after signing in. Only same-site paths are allowed, so a
 * crafted `next` cannot send the user to another site.
 */
export function safeNextPath(next: string | null | undefined): string {
  if (next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/\\")) {
    return next;
  }
  return "/dashboard";
}
