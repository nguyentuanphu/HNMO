import { test, expect } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'Configured game name' } }));
  await page.goto('/account-profile-preview.html');
  await expect(page.locator('.preview-brand')).toHaveText('Configured game name');
});

test('both sign-in layouts lead into a clearly marked private sample dashboard', async ({ page }) => {
  for (const label of ['Game login', 'Website sign-in']) {
    await page.getByRole('button', { name: label, exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
    await expect(page.getByLabel('Password', { exact: true })).toBeDisabled();
    await expect(page.locator('#profilePreview')).toContainText('Design preview · fictional account data');
    await page.getByRole('button', { name: 'Explore the account preview', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Welcome back, Explorer.' })).toBeVisible();
  }
});

test('owned item search and trace detail work without making account or payment requests', async ({ page }) => {
  const calls: string[] = [];
  page.on('request', request => { if (/\/portal\/|\/api\//.test(request.url())) calls.push(request.url()); });
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Items & vaults', exact: true }).click();
  await page.getByRole('searchbox').fill('0002');
  await expect(page.getByRole('cell', { name: 'Example jewel', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Example sword +9', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'DEMO-ITEM-0002', exact: true }).click();
  await expect(page.locator('code')).toHaveText('DEMO-ITEM-0002');
  await expect(page.locator('#profilePreview')).toContainText('Earlier history unavailable.');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Wallet & VIP', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Top up coins', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Review VIP plans', exact: true })).toBeDisabled();
  expect(calls).toEqual([]);
});

test('English and Vietnamese cover all sections and every theme fits desktop and phone', async ({ page }) => {
  for (const language of ['English', 'Tiếng Việt']) {
    await page.getByRole('button', { name: language, exact: true }).click();
    const vi = language === 'Tiếng Việt';
    await expect(page.locator('html')).toHaveAttribute('lang', vi ? 'vi' : 'en');
    for (const width of [375, 1280]) {
      await page.setViewportSize({ width, height: 1000 });
      for (const theme of ['obsidian', 'parchment', 'jade', 'cyber', 'royal']) {
        await page.locator('.preview-theme-control select').selectOption(theme);
        await expect(page.locator('body')).toHaveAttribute('data-preview-theme', theme);
        const nav = page.locator('.preview-sidebar');
        for (const label of vi ? ['Tổng quan', 'Nhân vật', 'Vật phẩm & rương', 'Hoạt động & giao dịch', 'Ví & VIP', 'Bảo mật tài khoản', 'Phần thưởng & thư', 'Giao diện'] : ['Overview', 'Characters', 'Items & vaults', 'Activity & trades', 'Wallet & VIP', 'Account security', 'Rewards & mail', 'Appearance']) {
          await nav.getByRole('button', { name: label, exact: true }).click();
          await expect(nav.getByRole('button', { name: label, exact: true })).toHaveAttribute('aria-current', 'page');
          expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
        }
      }
    }
  }
});

test('themes change layout and typography as well as palette', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const styles: Record<string, { columns: string; font: string; radius: string }> = {};
  for (const theme of ['obsidian', 'parchment', 'jade', 'cyber', 'royal']) {
    await page.locator('.preview-theme-control select').selectOption(theme);
    styles[theme] = await page.evaluate(() => ({ columns: getComputedStyle(document.querySelector('.preview-shell')!).gridTemplateColumns, font: getComputedStyle(document.body).fontFamily, radius: getComputedStyle(document.querySelector('.preview-card')!).borderRadius }));
  }
  expect(styles.parchment.columns).not.toEqual(styles.obsidian.columns);
  expect(styles.parchment.font).not.toEqual(styles.obsidian.font);
  expect(styles.cyber.font).not.toEqual(styles.obsidian.font);
  expect(styles.jade.radius).not.toEqual(styles.obsidian.radius);
  expect(styles.royal.columns).not.toEqual(styles.obsidian.columns);
});
