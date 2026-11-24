import { expect, test } from '@playwright/test';

test.use({ baseURL: process.env.ADMIN_URL ?? 'http://127.0.0.1:8095' });

for (const width of [390, 1440]) {
  test(`activity log categories and retention are available at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 930 });
    await page.goto('/activity-log');
    await expect(page.getByRole('heading', { name: 'Player activity log' })).toBeVisible();
    await expect(page.locator('#al-retention')).toHaveValue(/^[1-9][0-9]*$/);
    const kind = page.locator('#al-kind');
    for (const [key, label] of [
      ['Loot', 'Loot'], ['Upgrade', 'Upgrade'], ['Consume', 'Item use'],
      ['Grant', 'Item grant'], ['PvpKill', 'PvP defeat'], ['ChaosMix', 'Chaos mix'],
    ]) {
      await expect(kind.locator(`option[value="${key}"]`)).toHaveText(label);
    }
    await kind.selectOption('Upgrade');
    await page.locator('#al-search').click();
    await expect(page.locator('#al-results')).toBeVisible();
    await expect(page.locator('#blazor-error-ui')).toBeHidden();
    await page.locator('#al-filters').screenshot({ path: `docs/screenshots/activity-log/filters-${width}.png` });
  });
}
