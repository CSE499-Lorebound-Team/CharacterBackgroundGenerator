import { NextResponse, type NextRequest } from "next/server";

// The API's auth cookie (api/Auth/AuthenticationSetup.cs). httpOnly, so only
// its presence is checked here.
const AUTH_COOKIE = "lorebound.auth";

/**
 * Optimistic route protection: with no auth cookie at all, app pages (and
 * invite links) go straight to /login, keeping the page in `next`. A cookie that is present but expired gets through,
 * and the page's first API call answers 401 (see useApiFetch and AppShell).
 * The API is the authority on every request.
 *
 * /login is not redirected the other way here: a stale cookie would bounce
 * between /login and /dashboard. LoginForm redirects once the session is
 * confirmed instead.
 */
export function proxy(request: NextRequest) {
  if (request.cookies.has(AUTH_COOKIE)) {
    return NextResponse.next();
  }

  const { pathname, search } = request.nextUrl;
  const login = new URL("/login", request.url);
  login.searchParams.set("next", pathname + search);

  return NextResponse.redirect(login);
}

export const config = {
  matcher: [
    "/dashboard/:path*",
    "/settings/:path*",
    "/characters/:path*",
    "/builder/:path*",
    "/join/:path*",
  ],
};
