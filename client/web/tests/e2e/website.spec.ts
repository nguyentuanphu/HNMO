import { expect, test } from "@playwright/test";

test("website localizes server content and clears stale counts after disconnection", async ({
  page,
}) => {
  let offline = false;
  await page.route("**/game-name.json", (route) =>
    route.fulfill({ json: { name: "Website test realm" } }),
  );
  await page.route("**/website/data", (route) =>
    offline
      ? route.fulfill({ status: 503 })
      : route.fulfill({
          json: {
            updatedAt: new Date().toISOString(),
            servers: [
              {
                id: 0,
                players: 12,
                capacity: 100,
                experienceRate: 5,
                masterExperienceRate: 2,
                maximumLevel: 400,
                maximumMasterLevel: 200,
                maximumCharactersPerAccount: 5,
                maximumPartySize: 5,
                classes: [
                  { id: 0, name: "Wizard||vi=Pháp sư", requiredLevel: 0 },
                ],
                maps: [{ id: 0, name: "Forest||vi=Rừng", expMultiplier: 1 }],
                events: [],
              },
            ],
          },
        }),
  );
  await page.goto("/home.html");
  await expect(page).toHaveTitle("Website test realm");
  await expect(page.getByText("12 / 100", { exact: true })).toBeVisible();
  await page.locator("#vi").click();
  await expect(page.getByText("Pháp sư", { exact: true })).toBeVisible();
  await page.locator("#search").fill("no matching map");
  await expect(page.locator(".map-row")).toHaveCount(0);
  await page.reload();
  await expect(page.locator("#vi")).toHaveAttribute("aria-pressed", "true");
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  offline = true;
  await page.locator("#refresh").click();
  await expect(page.getByText("Chưa kết nối", { exact: true })).toBeVisible();
  await expect(page.getByText("12 / 100", { exact: true })).toHaveCount(0);
});
