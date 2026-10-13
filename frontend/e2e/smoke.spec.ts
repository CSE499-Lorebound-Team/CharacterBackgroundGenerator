import { expect, type Page, test } from "@playwright/test";

// The whole loop (P9-17): a GameMaster builds a setting and invites a
// player, who joins and completes a character. Fresh accounts every run;
// both are deleted at the end, which deletes their setting and character.

const run = Date.now();
const gm = { name: "Smoke GM", email: `smoke-gm-${run}@example.com`, password: "smoke-gm-password" };
const player = { name: "Smoke Player", email: `smoke-player-${run}@example.com`, password: "smoke-player-password" };
const settingName = `Smoke Realm ${run}`;

type Account = typeof gm;

async function register(page: Page, account: Account) {
  await page.getByLabel("Display Name").fill(account.name);
  await page.getByLabel("Email").fill(account.email);
  await page.getByLabel("Password", { exact: true }).fill(account.password);
  await page.getByLabel("Confirm Password").fill(account.password);
  await page.getByRole("button", { name: "Create Account" }).click();
}

async function addEntry(page: Page, name: string, type: string, description: string) {
  await page.getByRole("button", { name: "Add Entry" }).first().click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name").fill(name);
  await dialog.getByRole("combobox", { name: "Type" }).click();
  await page.getByRole("option", { name: type, exact: true }).click();
  await dialog.getByLabel("Description").fill(description);
  await dialog.getByRole("button", { name: "Add Entry" }).click();
  await expect(page.getByRole("status").getByText(`Added ${name}`)).toBeVisible();
}

/** Deletes the signed-in user's account (the API needs the password). */
async function deleteAccount(page: Page, password: string) {
  const response = await page.request.delete("/api/users/me", {
    headers: { "X-Requested-With": "Lorebound" },
    data: { password },
  });
  expect(response.status(), "delete account").toBe(204);
}

test("GM builds a setting, a player joins and completes a character", async ({ browser }) => {
  const gmContext = await browser.newContext();
  const playerContext = await browser.newContext();
  const gmPage = await gmContext.newPage();
  const playerPage = await playerContext.newPage();
  let joinUrl = "";

  try {
    await test.step("GM registers and lands on the dashboard", async () => {
      await gmPage.goto("/register");
      await register(gmPage, gm);
      await expect(gmPage).toHaveURL(/\/dashboard$/);
      await expect(gmPage.getByText("No settings yet")).toBeVisible();
    });

    await test.step("GM creates a setting", async () => {
      await gmPage.goto("/settings");
      await gmPage.getByRole("button", { name: "Create Setting" }).first().click();
      const dialog = gmPage.getByRole("dialog");
      await dialog.getByLabel("Setting Name").fill(settingName);
      await dialog.getByRole("button", { name: "Create Setting" }).click();
      await expect(gmPage).toHaveURL(/\/settings\/[0-9a-f-]{36}$/);
      await expect(gmPage.getByRole("heading", { name: settingName })).toBeVisible();
    });

    await test.step("GM adds the entries the builder needs", async () => {
      await addEntry(gmPage, "Riverhold", "Location", "A ferry town on the wide river.");
      await addEntry(gmPage, "River Folk", "Culture", "Boat people who trust the current.");
      await addEntry(gmPage, "Ferryman", "Profession", "Carries people and goods across.");
      await expect(gmPage.getByRole("tab", { name: /Locations\s*1/ })).toBeVisible();
    });

    await test.step("GM creates an invite link", async () => {
      await gmPage.getByRole("tab", { name: "Members" }).click();
      await gmPage.getByRole("button", { name: "Create Invite Link" }).click();
      const link = gmPage.locator("code", { hasText: "/join/" });
      await expect(link).toBeVisible();
      joinUrl = (await link.textContent()) ?? "";
      expect(joinUrl).toMatch(/^http:\/\/localhost:3000\/join\/\S+$/);
    });

    await test.step("player opens the link signed out, registers, and comes back", async () => {
      await playerPage.goto(joinUrl);
      await expect(playerPage).toHaveURL(/\/login\?next=/);
      await playerPage.getByRole("link", { name: "Create one" }).click();
      await register(playerPage, player);
      await expect(playerPage).toHaveURL(new URL(joinUrl).pathname);
      await expect(playerPage.getByText(settingName)).toBeVisible();
      await expect(playerPage.getByText(`Run by ${gm.name}`)).toBeVisible();
    });

    await test.step("player joins and sees the setting as a Player", async () => {
      await playerPage.getByRole("button", { name: "Join Setting" }).click();
      await expect(playerPage).toHaveURL(/\/settings\/[0-9a-f-]{36}$/);
      await expect(playerPage.getByRole("heading", { name: settingName })).toBeVisible();
      await expect(playerPage.getByText("Player", { exact: true }).first()).toBeVisible();
      await expect(playerPage.getByRole("button", { name: "Add Entry" })).toHaveCount(0);
    });

    await test.step("player creates a character and opens the builder", async () => {
      await playerPage.goto("/characters?create=1");
      const dialog = playerPage.getByRole("dialog");
      await dialog.getByRole("combobox", { name: "Setting" }).click();
      await playerPage.getByRole("option", { name: settingName }).click();
      await dialog.getByLabel("Character Name").fill("Mira of Riverhold");
      await dialog.getByRole("button", { name: "Start Building" }).click();
      await expect(playerPage).toHaveURL(/\/builder\/[0-9a-f-]{36}$/);
      await expect(playerPage.getByText("Step 1 of 8")).toBeVisible();
    });

    await test.step("player answers every step", async () => {
      const next = playerPage.getByRole("button", { name: "Next", exact: true });

      await next.click();
      await expect(playerPage.getByText("Step 2 of 8")).toBeVisible();
      await expect(next).toBeDisabled();
      await playerPage.getByRole("button", { name: /Riverhold/ }).click();

      await next.click();
      await expect(playerPage.getByText("Step 3 of 8")).toBeVisible();
      await playerPage.getByRole("button", { name: /River Folk/ }).click();

      await next.click(); // Religion: optional, nothing to choose
      await expect(playerPage.getByText("Step 4 of 8")).toBeVisible();
      await next.click(); // Social class: optional
      await expect(playerPage.getByText("Step 5 of 8")).toBeVisible();
      await next.click();

      await expect(playerPage.getByText("Step 6 of 8")).toBeVisible();
      await playerPage.getByRole("button", { name: /Ferryman/ }).click();
      await next.click();

      await expect(playerPage.getByText("Step 7 of 8")).toBeVisible();
      await playerPage.getByLabel("Motivation").fill("To find the source of the river.");
      await next.click();

      await expect(playerPage.getByText("Step 8 of 8")).toBeVisible();
      await expect(playerPage.getByText("To find the source of the river.").first()).toBeVisible();
    });

    await test.step("player completes the character", async () => {
      await playerPage.getByRole("button", { name: "Complete Character" }).click();
      await expect(playerPage).toHaveURL(/\/characters\/[0-9a-f-]{36}$/);
      await expect(playerPage.getByRole("heading", { name: "Mira of Riverhold" })).toBeVisible();
      await expect(playerPage.getByText("Complete", { exact: true }).first()).toBeVisible();
      await expect(playerPage.getByText("Ferryman")).toBeVisible();
    });

    await test.step("the GM's dashboard lists the setting", async () => {
      await gmPage.goto("/dashboard");
      await expect(gmPage.getByText(settingName).first()).toBeVisible();
    });
  } finally {
    // The player first: the GM cannot delete an account whose setting
    // still has other members (409).
    await deleteAccount(playerPage, player.password).catch(() => {});
    await deleteAccount(gmPage, gm.password).catch(() => {});
    await gmContext.close();
    await playerContext.close();
  }
});
