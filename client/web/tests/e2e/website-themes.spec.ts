import { expect, test } from '@playwright/test';

const backgrounds: Record<string, string> = {
  classic: 'rgb(16, 18, 15)', azure: 'rgb(9, 22, 38)', ember: 'rgb(33, 16, 13)',
  jade: 'rgb(8, 29, 24)', violet: 'rgb(23, 15, 39)', parchment: 'rgb(243, 235, 216)',
};

for (const width of [375, 820, 1280]) {
  for (const [theme, background] of Object.entries(backgrounds)) {
    test(`${theme} public theme at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await page.route('**/website/**', (route) => route.fulfill({ status: 503 }));
      await page.route('**/website/settings', (route) => route.fulfill({ json: { theme, googleClientId: '' } }));
      await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'HNMO theme test' } }));
      await page.goto('/home.html');
      await expect(page.locator('html')).toHaveAttribute('data-website-theme', theme);
      await expect(page.locator('body')).toHaveCSS('background-color', background);
      await expect(page.locator('.brand [data-game-name]')).toHaveText('HNMO theme test');
      await page.locator('#en').click();
      await expect(page.locator('#en')).toHaveAttribute('aria-pressed', 'true');
      await page.locator('#vi').click();
      await expect(page.locator('#vi')).toHaveAttribute('aria-pressed', 'true');
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      await page.screenshot({ path: `test-results/themes/${theme}-${width}.png`, fullPage: true });
    });
  }
}

test('unknown or missing theme preserves Classic', async ({ page }) => {
  await page.goto('/home.html');
  for (const value of ['unsupported', null, 4]) {
    await page.evaluate(async (theme) => {
      const url = '/js/websiteTheme.js';
      const module = await import(url);
      module.applyWebsiteTheme(theme);
    }, value);
    await expect(page.locator('html')).toHaveAttribute('data-website-theme', 'classic');
  }
});
