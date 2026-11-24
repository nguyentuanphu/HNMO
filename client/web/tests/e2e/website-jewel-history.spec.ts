import { mkdir } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

for (const [language, viewport] of [['en', {width:1440,height:900}], ['vi', {width:390,height:844}]] as const) {
  test(`shows captured holdings and permitted hunters in ${language}`, async ({ page }) => {
    const vi = language === 'vi';
    await page.setViewportSize(viewport);
    await page.addInitScript(language => localStorage.setItem('hnmo.language', language), language);
    await page.route('**/website/tracker/jewels', route => route.fulfill({ json: {
      enabled: true, available: true, dateSydney: '2026-11-24', capturedAt: '2026-11-24T08:00:00+11:00',
      inventoryAndVaultPieces: 25, bankPieces: vi ? 11 : null, logWindowCapped: false, showHunters: true, hunterWindowAvailable: true, hunters: [{character:'FixtureHunter', pieces:20}],
    } }));
    await page.goto('/jewel-history.html');
    const panel = page.locator('#item-history');
    await expect(panel.getByRole('heading', { name: vi ? 'Ngọc trong túi và kho' : 'Jewels in inventories and vaults' })).toBeVisible();
    await expect(panel.locator('#jewel-total')).toContainText(vi ? '25 viên' : '25 pieces');
    await expect(panel.locator('#jewel-date')).toContainText('Sydney');
    if (vi) {
      await expect(panel.locator('#jewel-bank-total')).toHaveText('Số dư ngân hàng ngọc · 11 viên');
    } else {
      await expect(panel.locator('#jewel-bank-total')).toBeHidden();
    }
    await expect(panel.locator('#item-history-results')).toContainText('FixtureHunter');
    expect(await panel.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
    await mkdir('docs/screenshots/jewel-tracker', { recursive:true });
    await panel.screenshot({ path:`docs/screenshots/jewel-tracker/tracker-${language}-${viewport.width}.png` });
  });
}

test('shows an unavailable snapshot without placeholder counts or hunters', async ({ page }) => {
  await page.route('**/website/tracker/jewels', route => route.fulfill({json:{enabled:true,available:false}}));
  await page.goto('/jewel-history.html');
  await expect(page.locator('#jewel-status')).toHaveText('The host has no verified jewel snapshot yet.');
  await expect(page.locator('#jewel-summary')).toBeHidden();
});

test('does not show named hunters outside the current retention window', async ({ page }) => {
  await page.route('**/website/tracker/jewels', route => route.fulfill({json:{
    enabled:true, available:true, capturedAt:'2026-11-01T08:00:00+11:00', inventoryAndVaultPieces:25,
    showHunters:true, hunterWindowAvailable:false, logWindowCapped:false, hunters:[{character:'FixtureHunter',pieces:20}],
  }}));
  await page.goto('/jewel-history.html');
  await expect(page.locator('#jewel-total')).toContainText('25 pieces');
  await expect(page.locator('#jewel-hunter-note')).toHaveText('The hunter records are outside the current retention window.');
  await expect(page.locator('#item-history-results li')).toHaveCount(0);
});
