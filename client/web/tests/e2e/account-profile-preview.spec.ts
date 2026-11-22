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
  await page.locator('.preview-main .preview-content > input[type=search]').fill('0002');
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
        for (const label of vi ? ['Tổng quan', 'Nhân vật', 'Vật phẩm & rương', 'Hoạt động & giao dịch', 'Ví & VIP', 'Bảo mật tài khoản', 'Phần thưởng & thư', 'Giao diện', 'Nhật ký hành trình', 'So sánh trang bị', 'Hỗ trợ'] : ['Overview', 'Characters', 'Items & vaults', 'Activity & trades', 'Wallet & VIP', 'Account security', 'Rewards & mail', 'Appearance', 'Adventure journal', 'Compare equipment', 'Support']) {
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

test('event log filters by kind, character and Sydney date, then exports exactly those rows', async ({ page }) => {
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Activity & trades', exact: true }).click();
  await page.getByLabel('Event', { exact: true }).selectOption('trade');
  await expect(page.locator('.preview-main')).toContainText('1 matching records');
  await expect(page.getByRole('cell', { name: 'Traded', exact: true })).toBeVisible();
  await page.getByLabel('From date (Sydney)', { exact: true }).fill('2026-11-21');
  await page.getByLabel('To date (Sydney)', { exact: true }).fill('2026-11-21');
  await page.getByLabel('Character', { exact: true }).selectOption('Explorer');
  const exported = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export filtered CSV', exact: true }).click();
  const download = await exported;
  expect(download.suggestedFilename()).toBe('hnmo-sample-events.csv');
  const path = await download.path();
  expect(path).not.toBeNull();
  const { readFile } = await import('node:fs/promises');
  const csv = await readFile(path!, 'utf8');
  expect(csv).toContain('DEMO-ITEM-0002');
  expect(csv).not.toContain('DEMO-ITEM-0001');
  await page.getByLabel('Character', { exact: true }).selectOption('Starlight');
  await expect(page.locator('.preview-main')).toContainText('No matching records');
});

test('item passports retain watch state and expose crafting inputs and before/after history', async ({ page }) => {
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Items & vaults', exact: true }).click();
  await page.getByRole('button', { name: 'Watch this item', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Watching this item', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.preview-main')).toContainText('Before: +8 / After: +9');
  await page.getByRole('button', { name: 'DEMO-ITEM-0003', exact: true }).click();
  await expect(page.locator('.preview-main')).toContainText('DEMO-INPUT-0101');
  await expect(page.locator('.preview-main')).toContainText('Crafting inputs (consumed)');
  await page.getByRole('button', { name: 'DEMO-ITEM-0001', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Watching this item', exact: true })).toBeVisible();
});

test('equipment comparison calculates deltas and support export omits account secrets', async ({ page }) => {
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Compare equipment', exact: true }).click();
  await expect(page.locator('.preview-main table')).toContainText('-22');
  await expect(page.locator('.preview-main table')).toContainText('+8');
  await page.getByLabel('Alternative', { exact: true }).selectOption('DEMO-ITEM-0001');
  const rows = page.locator('.preview-main tbody tr');
  for (const row of await rows.all()) await expect(row.locator('td').last()).toHaveText('0');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Support', exact: true }).click();
  const exported = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download support report', exact: true }).click();
  const download = await exported;
  const { readFile } = await import('node:fs/promises');
  const report = JSON.parse(await readFile((await download.path())!, 'utf8')) as Record<string, unknown>;
  expect(report.sample).toBe(true);
  expect(Object.keys(report)).toEqual(['sample', 'account', 'item', 'events']);
  expect(JSON.stringify(report)).not.toMatch(/password|pin|email|secret/i);
});

test('global search, reminders, journal, preferences and session simulation work', async ({ page }) => {
  await page.getByRole('searchbox', { name: 'Search items, trace IDs, characters or receipts', exact: true }).fill('0005');
  await page.locator('.account-hub-search-results button').click();
  await expect(page.locator('.preview-main code')).toHaveText('DEMO-ITEM-0005');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Overview', exact: true }).click();
  await expect(page.locator('.preview-main')).toContainText('Since your last visit');
  await expect(page.locator('.preview-main')).toContainText('Expiring soon');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Adventure journal', exact: true }).click();
  await expect(page.locator('.preview-main')).toContainText('Example boss defeated with the party.');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Account security', exact: true }).click();
  await page.getByRole('button', { name: 'End example session', exact: true }).click();
  await expect(page.locator('.preview-main')).toContainText('Example session ended locally');
  await expect(page.getByRole('button', { name: 'End example session', exact: true })).toBeDisabled();
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Appearance', exact: true }).click();
  await page.getByLabel('Display density', { exact: true }).selectOption('compact');
  await page.locator('.preview-theme-control select').selectOption('jade');
  await page.reload();
  await expect(page.locator('body')).toHaveAttribute('data-preview-theme', 'jade');
  await expect(page.locator('body')).toHaveAttribute('data-density', 'compact');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Appearance', exact: true }).click();
  await expect(page.getByLabel('Display density', { exact: true })).toHaveValue('compact');
});
