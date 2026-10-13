import { defineConfig, devices } from "@playwright/test";

// End-to-end smoke test (P9-17): `npm run e2e` boots the seeded API and the
// frontend, then drives the whole loop in Chromium. Postgres must be running
// and migrated (`docker compose up -d`, then `dotnet ef database update` in
// api/). Servers that are already up on 5110 and 3000 are reused locally.
const isCI = Boolean(process.env.CI);

export default defineConfig({
  testDir: "./e2e",
  // One scenario that builds on itself; the auth rate limit (10 a minute
  // per IP) also rules out parallel runs.
  fullyParallel: false,
  workers: 1,
  forbidOnly: isCI,
  retries: 0,
  // The dev server compiles each route on its first visit.
  timeout: 180_000,
  expect: { timeout: 20_000 },
  reporter: isCI ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    // The API's CSRF check only allows http://localhost:3000 in Development.
    baseURL: "http://localhost:3000",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      name: "api",
      command: "dotnet run --project ../api --launch-profile http -- --seed",
      url: "http://localhost:5110/api/health",
      reuseExistingServer: !isCI,
      timeout: 240_000,
      stdout: "ignore",
      stderr: "pipe",
    },
    {
      name: "frontend",
      command: "npm run dev",
      url: "http://localhost:3000/login",
      reuseExistingServer: !isCI,
      timeout: 180_000,
    },
  ],
});
