import { expect, test } from '@playwright/test';

test('guide documents Fixed item options cleanly without page errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Fixed item options');
  expect(errors).toEqual([]);
});
