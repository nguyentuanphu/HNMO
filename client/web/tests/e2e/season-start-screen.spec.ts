// The start screen after logging in: Season 6.3 and Season 9 servers side by side (seasons plan, step 4).
// The website's worlds list is replaced with one Season 6.3 world and one Season 9 world, so the test needs no
// Season 9 server; nothing is entered, so the account stays free.
import { test, expect } from '@playwright/test';

const SHOTS = process.env.SHOTS_DIR;

for (const language of ['en', 'vi'] as const) {
  test(`servers stand in a Season 6.3 and a Season 9 column (${language})`, async ({ page }) => {
    await page.addInitScript((lang) => { try { localStorage.setItem('hnmo.language', lang); } catch { /* private window */ } }, language);
    // Even server ids play Season 6.3, odd ones Season 9.
    const ids = Array.from({ length: 20 }, (_, i) => i);
    await page.route('**/website/data', (route) => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        worlds: [
          { worldId: 'classic', season: 's6', nameEn: 'Classic', nameVi: 'Cổ điển', isOpen: true, servers: ids.filter((i) => i % 2 === 0).map((id) => ({ id })) },
          { worldId: 'new', season: 's9', nameEn: 'New world', nameVi: 'Thế giới mới', isOpen: true, servers: ids.filter((i) => i % 2 === 1).map((id) => ({ id })) },
        ],
        servers: [],
      }),
    }));
    await page.goto('/index.html');
    await page.fill('#username', 'test8');
    await page.fill('#password', 'test8');
    await page.click('button.primary');
    await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
    test.skip(!(await page.isVisible('#servers .choice')), 'The connect server lists one game server: the start screen is skipped.');

    const s6 = page.locator('#servers .season-column.season-s6');
    const s9 = page.locator('#servers .season-column.season-s9');
    await expect(s6.locator('h2')).toContainText('Season 6.3');
    await expect(s9.locator('h2')).toContainText('Season 9');
    await expect(page.locator('#servers .seasons-title')).toHaveText(language === 'vi' ? 'Chọn máy chủ' : 'Choose a server');
    // Every server is in its world's column, and the first button is still a Season 6.3 server.
    const total = await page.locator('#servers .choice').count();
    expect(await s6.locator('.choice').count() + await s9.locator('.choice').count()).toBe(total);
    await expect(s6.locator('.choice .season-badge')).toHaveCount(0); // the column says the season
    if (await s9.locator('.choice').count() > 0) await expect(s9.locator('.choice').first()).toContainText(language === 'vi' ? 'Thế giới mới' : 'New world');
    else await expect(s9.locator('.season-empty')).toBeVisible();
    await expect(page.locator('#servers .choice').first()).toContainText(language === 'vi' ? 'Cổ điển' : 'Classic');
    // Side by side on a wide screen.
    const a = await s6.boundingBox();
    const b = await s9.boundingBox();
    expect(a && b && b.x > a.x + a.width - 1 && Math.abs(b.y - a.y) < 2).toBeTruthy();
    if (SHOTS) await page.screenshot({ path: `${SHOTS}/season-start-screen-${language}.png` });

    // On a phone the columns stand one above the other, with no sideways scrolling.
    await page.setViewportSize({ width: 390, height: 844 });
    const c = await s6.boundingBox();
    const d = await s9.boundingBox();
    expect(c && d && d.y >= c.y + c.height - 1).toBeTruthy();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
  });
}
