// The admin panel (ADMIN_URL, default the local server's panel on 127.0.0.1:8095): its main pages
// render without errors, the sidebar reaches every kind of game data, an edit page and the plugin
// settings dialog open, and the layout works on a phone and in dark mode. Run against a server
// whose panel needs no login (a local save without admin users). Nothing is saved.
import { test, expect, type Page } from '@playwright/test';

const adminUrl = (process.env.ADMIN_URL ?? 'http://127.0.0.1:8095').replace(/\/$/, '');
const shots = process.env.ADMIN_SHOTS ?? 'test-results';

test.use({ baseURL: adminUrl, viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 });

/** Collects page errors and failed Blazor circuits of a page. */
function watchErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error' && !/favicon/i.test(message.text())) errors.push(message.text());
  });
  return errors;
}

/** Opens a page of the panel and waits until the interactive circuit has rendered it. */
async function open(page: Page, path: string): Promise<void> {
  await page.goto(path, { waitUntil: 'networkidle' });
  await expect(page.locator('.sidebar .hn-brand')).toBeVisible();
}

/** Checks that the page shows no error: no Blazor error bar, no page errors. */
async function expectHealthy(page: Page, errors: string[]): Promise<void> {
  await expect(page.locator('#blazor-error-ui')).toBeHidden();
  expect(errors, errors.join('\n')).toEqual([]);
}

/** The visible data rows of the first table on the page (the grids keep empty rows for paging). */
function dataRows(page: Page) {
  return page.locator('article.content table tbody tr:visible');
}

test('home shows the dashboard with servers, players, accounts and shortcuts', async ({ page }) => {
  const errors = watchErrors(page);
  await open(page, '/');
  await expect(page.locator('h1')).toHaveText(/Dashboard/);
  await expect(page.locator('.dashboard-statistics .card')).toHaveCount(4);
  await expect(page.locator('.list-group-item').first()).toBeVisible();
  await expect(page.locator('.quick-link')).not.toHaveCount(0);
  for (const section of ['Overview', 'Players', 'Game', 'Administration']) {
    await expect(page.locator('.sidebar .nav-section', { hasText: section })).toBeVisible();
  }
  await page.screenshot({ path: `${shots}/admin-home.png` });
  await expectHealthy(page, errors);
});

test('accounts list and an account edit page', async ({ page }) => {
  const errors = watchErrors(page);
  await open(page, '/accounts');
  await expect(page.locator('h1')).toHaveText(/Accounts/);
  await expect(dataRows(page).nth(1)).toBeVisible({ timeout: 30_000 });
  await page.screenshot({ path: `${shots}/admin-accounts.png` });
  await page.locator('article.content a.btn-primary[href^="edit-account/"]').first().click();
  await expect(page.locator('h1')).toHaveText(/Edit/, { timeout: 30_000 });
  await expect(page.locator('article.content input.form-control:visible').nth(1)).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.form-actions button[type=submit]')).toBeVisible();
  await page.screenshot({ path: `${shots}/admin-account-edit.png` });
  await expectHealthy(page, errors);
});

test('game configuration editor shows its fields', async ({ page }) => {
  const errors = watchErrors(page);
  await open(page, '/edit-config/HNMO.DataModel.Configuration.GameConfiguration/');
  await expect(page.locator('h1')).toHaveText(/Game Configuration/, { timeout: 30_000 });
  await expect.poll(() => page.locator('article.content form input').count(), { timeout: 60_000 }).toBeGreaterThan(10);
  await page.screenshot({ path: `${shots}/admin-game-configuration.png` });
  await expectHealthy(page, errors);
});

test('plugins list and the settings dialog of a plugin', async ({ page }) => {
  const errors = watchErrors(page);
  await open(page, '/plugins');
  await expect(page.locator('h1')).toHaveText(/Plugins/);
  await expect(dataRows(page).nth(1)).toBeVisible({ timeout: 30_000 });
  await page.locator('article.content button:has(.oi-cog)').first().click();
  const dialog = page.locator('.modal.show .modal-content');
  await expect(dialog).toBeVisible();
  await page.screenshot({ path: `${shots}/admin-plugin-dialog.png` });
  await dialog.locator('.btn-close, button:has-text("Cancel")').first().click();
  await expect(dialog).toBeHidden();
  await expectHealthy(page, errors);
});

test('every link of the sidebar groups opens a working page', async ({ page }) => {
  test.setTimeout(15 * 60_000);
  const errors = watchErrors(page);
  await open(page, '/');
  // Open all collapsed groups and collect their links.
  const toggles = page.locator('.sidebar li.dropdown:not(.expanded) > a.dropdown-toggle');
  while (await toggles.count() > 0) {
    await toggles.first().click();
  }
  const links = await page.locator('.sidebar li.dropdown .dropdown-item').evaluateAll(
    (items) => items.map((item) => [item.textContent?.trim() ?? '', item.getAttribute('href') ?? '']));
  expect(links.length).toBeGreaterThan(30);
  const failures: string[] = [];
  for (const [caption, href] of links) {
    if (!href || href.startsWith('http') || href.startsWith('/grafana')) continue;
    await page.goto(href, { waitUntil: 'networkidle' });
    await page.waitForTimeout(500);
    const broken = await page.locator('#blazor-error-ui').isVisible();
    // Some pages (map and terrain editors, setup, logs) have no heading but a canvas, form or card.
    const content = page.locator('article.content').locator(':is(h1, h2, h3, h4, canvas, form, table, .card, .alert, select)').first();
    const rendered = await content.isVisible({ timeout: 30_000 }).catch(() => false)
      || await content.waitFor({ state: 'visible', timeout: 30_000 }).then(() => true, () => false);
    if (broken || !rendered) failures.push(`${caption} (${href}): ${broken ? 'error bar' : 'nothing rendered'}`);
    // The generic grids: the first entry opens in the editor.
    if (href.startsWith('edit-config-grid/')) {
      const edit = page.locator('article.content table tbody tr:visible a.btn-primary').first();
      if (await edit.count() > 0) {
        await edit.click();
        await expect(page.locator('article.content h1')).toHaveText(/Edit/, { timeout: 30_000 }).catch(() => {});
        const formOk = await page.locator('article.content form').first().isVisible({ timeout: 30_000 }).catch(() => false);
        if (!formOk || await page.locator('#blazor-error-ui').isVisible()) failures.push(`${caption}: edit page`);
      }
    }
  }
  expect(failures, failures.join('\n')).toEqual([]);
  await expectHealthy(page, errors);
});

test('phone layout: the menu opens from the top bar', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: adminUrl, viewport: { width: 390, height: 844 }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  const errors = watchErrors(page);
  await open(page, '/');
  const menu = page.locator('.sidebar .nav-scrollable');
  await expect(menu).toBeHidden();
  const width = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(width).toBeLessThanOrEqual(390);
  await page.screenshot({ path: `${shots}/admin-phone-home.png` });
  await page.locator('.sidebar button.navbar-toggler').click();
  await expect(menu).toBeVisible();
  await page.screenshot({ path: `${shots}/admin-phone-menu.png` });
  await menu.locator('a.nav-link[href="accounts"]').click();
  await expect(page.locator('h1')).toHaveText(/Accounts/);
  await expect(menu).toBeHidden();
  await expectHealthy(page, errors);
  await context.close();
});

test('dark mode switches with the theme button', async ({ page }) => {
  const errors = watchErrors(page);
  await open(page, '/');
  const before = await page.locator('html').getAttribute('data-bs-theme');
  await page.locator('.theme-selector').click();
  await expect(page.locator('html')).not.toHaveAttribute('data-bs-theme', before ?? '');
  await page.screenshot({ path: `${shots}/admin-home-other-theme.png` });
  await page.locator('.theme-selector').click();
  await expect(page.locator('html')).toHaveAttribute('data-bs-theme', before ?? 'light');
  await expectHealthy(page, errors);
});
