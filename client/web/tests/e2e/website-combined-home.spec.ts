import { expect, test } from '@playwright/test';
import { websitePreviewReply } from '../../src/websiteThemePreviewData.js';

for (const skin of ['classic', 'ember', 'azure', 'jade', 'violet', 'parchment']) {
  for (const width of [390, 1440]) for (const language of ['en', 'vi']) {
    test(`${skin}: additive server facts remain usable at ${width}px (${language})`, async ({ page }) => {
      await page.setViewportSize({ width, height: 1000 });
      await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
      await page.route('**/website/**', route => {
        const path = new URL(route.request().url()).pathname;
        if (path === '/website/settings') return route.fulfill({ json: { theme: skin, googleClientId: '', requireWebsiteLogin: true } });
        const data = websitePreviewReply(path);
        return route.fulfill(data === undefined ? { status: 503 } : { json: data });
      });
      await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO' } }));
      await page.goto('/home.html');
      await expect(page.locator('body')).toHaveAttribute('data-website-layout', skin);
      const guide = page.locator('#server-guide');
      await expect(guide).toContainText(language === 'vi' ? 'Thông tin máy chủ' : 'Server rules at a glance');
      await expect(guide).toContainText(language === 'vi' ? 'Nhiệm vụ thăng lớp mẫu' : 'Example promotion');
      await expect(guide).toContainText('100 WCoinC');
      await expect(page.locator('#server-guide-season')).toHaveText('S6.3');
      await expect(page.locator('main section[id]')).toHaveCount(29);
      if (skin === 'jade') await expect(page.locator('.theme-sanctuary-column').first().locator('#server-guide')).toHaveCount(1);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      const scrolls = guide.locator('.server-guide-scroll');
      for (const table of await scrolls.all()) {
        expect(await table.evaluate(element => element.getBoundingClientRect().right <= innerWidth + 1)).toBe(true);
      }
      await expect(page.locator('#signup')).toHaveCount(0);
      await expect(page.locator('.website-navigation a[href="/account.html"]')).toHaveCount(1);
    });
  }
}
