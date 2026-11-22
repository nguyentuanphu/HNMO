import { expect, test } from '@playwright/test';

for (const skin of ['classic', 'ember', 'azure', 'jade', 'violet', 'parchment']) {
  test(`${skin} keeps the same feature nodes in its own page structure`, async ({ page }) => {
    await page.route('**/website/**', route => route.fulfill({ status: 503 }));
    await page.route('**/website/settings', route => route.fulfill({ json: { theme: skin } }));
    await page.route('**/game-name.json', route => route.fulfill({ json: { name: 'HNMO' } }));
    for (const width of [390, 1440]) {
      await page.setViewportSize({ width, height: 1000 });
      await page.goto('/home.html?theme=ignored');
      await expect(page.locator('body')).toHaveAttribute('data-website-layout', skin);
      for (const id of ['realm', 'news', 'events', 'schedule', 'newplayers', 'missions', 'bosses', 'lucky-number', 'marriage', 'guilds', 'social', 'races', 'gifts', 'resets', 'market', 'arena', 'jewel-bank', 'catchup', 'season-plan', 'leftovers', 'mounts', 'party-spots', 'rankings', 'drops', 'world', 'phone', 'start']) await expect(page.locator(`section#${id}`)).toHaveCount(1);
      if (width === 1440) {
        if (skin === 'ember' || skin === 'violet') {
          const rail = await page.locator('.theme-navigation-rail').boundingBox(); const main = await page.locator('main').boundingBox();
          expect(rail).not.toBeNull(); expect(main).not.toBeNull();
          if (skin === 'ember') expect(rail!.x).toBeLessThan(main!.x); else expect(rail!.x).toBeGreaterThan(main!.x + main!.width);
        }
        if (skin === 'azure') {
          const hero = await page.locator('.theme-bento-intro .hero').boundingBox(); const board = await page.locator('.theme-server-board').boundingBox();
          expect(hero!.x + hero!.width).toBeLessThan(board!.x);
        }
        if (skin === 'jade') await expect(page.locator('.theme-sanctuary-column')).toHaveCount(3);
        if (skin === 'parchment') await expect(page.locator('.theme-book-pages')).toHaveCSS('overflow-y', 'auto');
      }
      const proof = await page.evaluate(async () => {
        const before = [...document.querySelectorAll('main section[id]')].map(node => node.id).sort();
        let calls = 0; const feature = document.createElement('section'); feature.id = 'late-feature';
        const action = document.createElement('button'); action.textContent = 'Fixture action'; action.onclick = () => calls++;
        feature.append(action); document.querySelector('main')!.append(feature);
        const path = '/js/websiteTheme.js'; const { applyWebsiteTheme } = await import(path) as typeof import('../../src/websiteTheme.js');
        const theme = document.documentElement.dataset.websiteTheme;
        applyWebsiteTheme('classic'); await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
        applyWebsiteTheme(theme); await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
        const same = document.getElementById('late-feature') === feature; action.click();
        return { same, calls, before, after: [...document.querySelectorAll('main section[id]')].filter(node => node.id !== 'late-feature').map(node => node.id).sort() };
      });
      expect(proof.same).toBe(true); expect(proof.calls).toBe(1); expect(proof.after).toEqual(proof.before);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    }
  });
}
