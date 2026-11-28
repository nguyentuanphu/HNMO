// The host sets up bank top-ups on the admin panel (Bank top-ups), a player asks for a transfer code on the top-up
// page, the host presses Received, and the player's page shows the coins as added. Demo server and bridge.
import { test, expect } from '@playwright/test';

const ADMIN = process.env.ADMIN_URL ?? 'http://127.0.0.1:8095';

test('a player gets a VietQR transfer code and the host confirms it once', async ({ page, browser }) => {
  test.setTimeout(4 * 60_000);
  const admin = await browser.newPage();
  await admin.goto(`${ADMIN}/bank-topups`);
  await expect(admin.locator('#bank-settings')).toBeVisible({ timeout: 60_000 });
  // The page is server-rendered first: click until the live circuit answers with an account row.
  const rows = admin.locator('#bank-accounts tr.bank-account');
  await expect(async () => {
    if (await rows.count() === 0) await admin.locator('#bank-add').click();
    await expect(rows.first()).toBeVisible({ timeout: 1_000 });
  }).toPass({ timeout: 60_000 });
  const row = rows.first();
  await row.locator('.bank-pick').selectOption('970422');
  await row.locator('.bank-number').fill('0909123456');
  await row.locator('.bank-number').press('Tab');
  await admin.locator('#bank-holder-default').fill('NGUYEN VAN A');
  await admin.locator('#bank-holder-default').press('Tab');
  await admin.locator('#bank-active').check();
  await expect(admin.locator('#bank-preview svg')).toBeVisible();
  await admin.locator('#bank-save').click();
  await expect(admin.getByText(/Saved\.|Đã lưu\./)).toBeVisible({ timeout: 20_000 });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/topup.html');
  await expect(page.locator('#topup')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('#banks button')).toHaveText(['MB Bank']);
  await page.fill('#account', 'test2');
  await page.fill('#amount', '50000');
  await page.locator('#create').click();
  const code = page.locator('#message');
  await expect(code).toHaveText(/^HNMO[A-Z2-9]{6}$/, { timeout: 20_000 });
  const transferCode = await code.textContent() ?? '';
  await expect(page.locator('#qr')).toBeVisible();
  expect(await page.locator('#qr').getAttribute('data-vietqr')).toContain(transferCode);
  await expect(page.locator('#statusText')).toHaveClass(/pending/);
  await expect(page.locator('#history .history-row')).toHaveCount(1);
  const width = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(width).toBeLessThanOrEqual(390);

  await admin.locator('#bank-refresh').click();
  const topUp = admin.locator(`#bank-topups tr.bank-topup[data-code="${transferCode}"]`);
  await expect(topUp).toBeVisible();
  await topUp.locator('.bank-received').click();
  await admin.locator('#bank-filter').selectOption('credited');
  await expect(admin.locator(`#bank-topups tr.bank-topup[data-code="${transferCode}"]`)).toBeVisible({ timeout: 20_000 });

  await page.reload();
  await expect(page.locator('#history .state.credited')).toHaveCount(1, { timeout: 30_000 });
  await admin.close();
});
