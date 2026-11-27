// Automatic error reports (HNMO extra; owner 2026-11-27: they go to the server's owner only): an error of the page
// becomes one report on the admin panel (Bug reports, Automatic); the same error again in the same page load sends
// nothing more, and after a reload it adds to the same row instead of a new one.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld } from './helpers.js';

const ADMIN = process.env.ADMIN_URL ?? 'http://127.0.0.1:8095';

async function throwInPage(page: Page, message: string): Promise<void> {
  // From a timer, so it is an uncaught error of the page (window "error"), not of the test's evaluate.
  await page.evaluate((text) => { setTimeout(() => { throw new Error(text); }, 0); }, message);
  await page.waitForTimeout(500);
}

async function autoRow(admin: Page, message: string) {
  await admin.goto(`${ADMIN}/bug-reports`);
  // The page answers clicks once its live connection is up: click until the tab opens.
  await expect(async () => {
    await admin.locator('#bug-tab-auto').click();
    await expect(admin.locator('#auto-settings')).toBeVisible({ timeout: 1_000 });
  }).toPass({ timeout: 30_000 });
  await admin.locator('#auto-filter').selectOption('all');
  return admin.locator('#auto-table tr.auto-group', { hasText: message });
}

test('an error of the page reaches the admin panel once, grouped by its fingerprint', async ({ page, browser }) => {
  test.setTimeout(5 * 60_000);
  await enterWorld(page, 'test2');
  const message = `HNMO e2e automatic report ${Date.now()}`;

  await throwInPage(page, message);
  await throwInPage(page, message); // the same error again in this page load: not sent again

  const admin = await browser.newPage();
  await expect.poll(async () => (await autoRow(admin, message)).count(), { timeout: 30_000 }).toBe(1);
  let row = await autoRow(admin, message);
  await expect(row.locator('.auto-count')).toHaveText('1');
  await expect(row.locator('.auto-accounts')).toHaveText('1');
  await admin.locator('#auto-table').screenshot({ path: 'test-results/auto-error-reports-admin.png' });

  // A new page load: the same error adds to its row, never a second row.
  await enterWorld(page, 'test2');
  await throwInPage(page, message);
  await expect.poll(async () => (await (await autoRow(admin, message)).locator('.auto-count').textContent())?.trim(), { timeout: 30_000 }).toBe('2');
  row = await autoRow(admin, message);
  await expect(row).toHaveCount(1);

  // The details never show the login (the character, test2Dk, is shown to the owner); the owner can mark it fixed.
  await row.locator('summary').click();
  await expect(row.locator('details')).toContainText('test2Dk');
  await expect(row.locator('details')).not.toContainText(/\btest2\b/);
  await row.locator('.auto-fixed').click();
  await admin.locator('#auto-filter').selectOption('Fixed');
  await expect(admin.locator('#auto-table tr.auto-group', { hasText: message })).toHaveCount(1);
  await expect(admin.locator('#blazor-error-ui')).toBeHidden();
  await admin.close();
});
