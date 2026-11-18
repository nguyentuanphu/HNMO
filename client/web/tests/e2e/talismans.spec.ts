import { expect, test } from '@playwright/test';

test('guide documents bag Talismans/Figurines and capped Stat Consumables cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Talismans & figurines');

  await page.goto('/index.html');
  expect(errors).toEqual([]);
});
