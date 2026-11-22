import { test, expect } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'Configured game name' } }));
  await page.goto('/account-profile-preview.html?view=profile&lang=en');
  await expect(page.locator('.portal-header .preview-brand')).toHaveText('Configured game name');
});

test('one example session serves game and account, without a second login form', async ({ page }) => {
  await page.goto('/account-profile-preview.html?view=portal&lang=en');
  await expect(page.getByRole('heading', { name: 'Sign in once', exact: true })).toBeVisible();
  await expect(page.getByLabel('Password', { exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Enter with the example account', exact: true }).click();
  await expect(page.locator('body')).toHaveAttribute('data-view', 'profile');
  await page.locator('.portal-actions').getByRole('button', { name: 'Enter game', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Choose a character', exact: true })).toBeVisible();
  await page.locator('.portal-actions').getByRole('button', { name: 'My account', exact: true }).click();
  await expect(page.locator('body')).toHaveAttribute('data-view', 'profile');
  await page.reload();
  await page.locator('.portal-header .preview-brand').click();
  await expect(page.getByRole('heading', { name: 'Signed in as Explorer', exact: true })).toBeVisible();
  await expect(page.getByLabel('Password', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Sign in once', exact: true })).toBeVisible();
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
  await expect(page.getByRole('button', { name: 'VIP plans', exact: true })).toBeDisabled();
  expect(calls).toEqual([]);
});

test('five host-selected kits have portal and profile previews, never a player theme picker', async ({ page }) => {
  await page.goto('/account-profile-preview.html');
  await expect(page.locator('html')).toHaveAttribute('lang', 'vi');
  await expect(page.locator('.theme-tile')).toHaveCount(5);
  await expect(page.locator('.tile-actions a')).toHaveCount(10);
  for (const theme of ['ember', 'azure', 'jade', 'violet', 'parchment']) {
    await expect(page.locator(`.theme-tile[data-theme=${theme}] a`).first()).toHaveAttribute('href', `?view=portal&theme=${theme}&lang=vi`);
    await expect(page.locator(`.theme-tile[data-theme=${theme}] a`).last()).toHaveAttribute('href', `?view=profile&theme=${theme}&lang=vi`);
  }
  await page.goto('/account-profile-preview.html?view=admin&lang=en');
  await page.getByRole('button', { name: 'Devias · Frost', exact: true }).click();
  await page.getByRole('button', { name: 'Apply example host theme', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Example host setting saved');
  await page.goto('/account-profile-preview.html?view=profile&lang=en');
  await expect(page.locator('body')).toHaveAttribute('data-preview-theme', 'azure');
  await expect(page.locator('select')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /appearance|theme|website sign-in|game login/i })).toHaveCount(0);
});

test('every section works in both languages with five equal mobile targets and no page overflow', async ({ page }) => {
  test.setTimeout(180000);
  for (const lang of ['en', 'vi']) {
    for (const width of [320, 375, 430, 1280]) {
      await page.setViewportSize({ width, height: 950 });
      for (const theme of ['ember', 'azure', 'jade', 'violet', 'parchment']) {
        await page.goto(`/account-profile-preview.html?view=profile&lang=${lang}&theme=${theme}`);
        await expect(page.locator('html')).toHaveAttribute('lang', lang);
        await expect(page.locator('body')).toHaveAttribute('data-preview-theme', theme);
        await page.evaluate(async () => { await document.fonts.ready; });
        if (width < 760) {
          const targets = await page.locator('.mobile-account-nav button').evaluateAll(nodes => nodes.map(node => ({ width: node.getBoundingClientRect().width, height: node.getBoundingClientRect().height, labelFits: node.lastElementChild!.scrollWidth <= node.lastElementChild!.clientWidth })));
          expect(targets).toHaveLength(5);
          expect(Math.max(...targets.map(t => t.width)) - Math.min(...targets.map(t => t.width))).toBeLessThan(1);
          expect(targets.every(t => t.height >= 44)).toBe(true);
          expect(targets.every(t => t.labelFits)).toBe(true);
          await page.locator('.mobile-account-nav button').last().click();
          await expect(page.getByRole('dialog')).toBeVisible();
          await page.keyboard.press('Escape');
          await expect(page.getByRole('dialog')).toHaveCount(0);
          await expect(page.locator('.mobile-account-nav button').last()).toBeFocused();
        }
        for (const section of ['home','characters','items','wallet','activity','journal','compare','security','rewards','support']) {
          if (width < 760) {
            await page.locator('.mobile-account-nav button').last().click();
            await page.locator(`dialog [data-section=${section}]`).click();
            await expect(page.getByRole('dialog')).toHaveCount(0);
          } else await page.locator(`.preview-sidebar [data-section=${section}]`).click();
          await expect(page.locator(`.preview-sidebar [data-section=${section}]`)).toHaveAttribute('aria-current', 'page');
          expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
        }
      }
    }
  }
});

test('fantasy kits vary portal and profile composition as well as color', async ({ page }) => {
  const styles: Record<string, string[]> = {};
  for (const theme of ['ember', 'azure', 'jade', 'violet', 'parchment']) {
    await page.goto(`/account-profile-preview.html?view=portal&lang=en&theme=${theme}`);
    const portal = await page.locator('.portal-hero').evaluate(el => [getComputedStyle(el).gridColumn, getComputedStyle(el).position, getComputedStyle(el).borderRadius, getComputedStyle(el).clipPath].join('|'));
    await page.goto(`/account-profile-preview.html?view=profile&lang=en&theme=${theme}`);
    styles[theme] = await page.evaluate(() => [getComputedStyle(document.querySelector('.preview-shell')!).gridTemplateColumns, getComputedStyle(document.querySelector('.preview-sidebar')!).display, getComputedStyle(document.querySelector('.preview-card')!).borderRadius, getComputedStyle(document.querySelector('.profile-banner')!).minHeight]);
    styles[theme].push(portal);
  }
  expect(new Set(Object.values(styles).map(value => JSON.stringify(value))).size).toBe(5);
});

test('event log filters by kind, character and Sydney date, then exports exactly those rows', async ({ page }) => {
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Item event log', exact: true }).click();
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

test('global search, reminders, journal, density and session simulation work', async ({ page }) => {
  await page.getByRole('searchbox', { name: 'Search items, trace IDs, characters or receipts', exact: true }).fill('0005');
  await page.locator('.account-hub-search-results button').click();
  await expect(page.locator('.preview-main code')).toHaveText('DEMO-ITEM-0005');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Overview', exact: true }).click();
  await expect(page.locator('.preview-main')).toContainText('Since your last visit');
  await expect(page.locator('.preview-main')).toContainText('Expiring soon');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Adventure journal', exact: true }).click();
  await expect(page.locator('.preview-main')).toContainText('Example boss defeated with the party.');
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Security', exact: true }).click();
  await page.getByRole('button', { name: 'End example session', exact: true }).click();
  await expect(page.locator('.preview-main')).toContainText('Example session ended locally');
  await expect(page.getByRole('button', { name: 'End example session', exact: true })).toBeDisabled();
  await page.getByLabel('Display density', { exact: true }).selectOption('compact');
  await expect(page.locator('body')).toHaveAttribute('data-density', 'compact');
  await page.reload();
  await page.locator('.preview-sidebar').getByRole('button', { name: 'Security', exact: true }).click();
  await expect(page.getByLabel('Display density', { exact: true })).toHaveValue('compact');
});

test('all five fantasy entrances load their artwork and fit phone, tablet and desktop in EN/VI', async ({ page }) => {
  test.setTimeout(120000);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  for (const language of ['vi', 'en']) {
    for (const width of [320, 390, 820, 1440]) {
      await page.setViewportSize({ width, height: 950 });
      for (const theme of ['ember', 'azure', 'jade', 'violet', 'parchment']) {
        await page.goto(`/account-profile-preview.html?view=portal&lang=${language}&theme=${theme}`);
        await expect(page.getByRole('heading', { name: language === 'vi' ? 'Đăng nhập một lần' : 'Sign in once', exact: true })).toBeVisible();
        await expect(page.locator('.portal-hero img')).toHaveJSProperty('complete', true);
        expect(await page.locator('.portal-hero img').evaluate(el => (el as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${theme}/${width}/${language}`).toBe(true);
        await expect(page.locator('.portal-actions button')).toHaveCount(2);
        await expect(page.locator('.login-gate input')).toHaveCount(2);
      }
    }
  }
  expect(errors).toEqual([]);
});
