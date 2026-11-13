// The full-screen mini map like the desktop's (CMiniMap, mini_map.rml): Tab opens it with the world's
// picture turned 45 degrees and its NPC markers, hovering a marker shows its name, and Escape or the
// close button closes it.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

const MAP = '.rml-mini_map';

async function mapState(page: Page): Promise<{ loaded: boolean; markers: number; transform: string }> {
  return page.evaluate((selector) => {
    const shadow = document.querySelector(selector)?.shadowRoot;
    const img = shadow?.querySelector<HTMLImageElement>('img.map');
    return {
      loaded: Boolean(img && img.complete && img.naturalWidth > 0),
      markers: shadow?.querySelectorAll('.marker').length ?? 0,
      transform: img?.style.transform ?? '',
    };
  }, MAP);
}

test('Tab opens the mini map of Lorencia with its markers; Escape and the close button close it', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test2');
  expect(await page.evaluate(() => (window as unknown as { __game: { mapNumber: number } }).__game.mapNumber)).toBe(0);

  await page.keyboard.press('Tab');
  await expect(page.locator(MAP)).toBeAttached({ timeout: 60_000 });
  await expect.poll(async () => (await mapState(page)).loaded, { timeout: 60_000 }).toBe(true);
  const state = await mapState(page);
  // Lorencia's markers come from Minimap_World1_eng.bmd; each clip region holds a copy of them.
  expect(state.markers).toBeGreaterThan(0);
  expect(state.transform).toMatch(/^matrix\(/);

  // Hovering a marker shows its name (the location hint). An NPC's hint box starts at its quad's
  // first corner (the matrix's e, f) and is half its size, in reference units.
  const point = await page.evaluate((selector) => {
    const shadow = document.querySelector(selector)?.shadowRoot;
    const scaleX = window.innerWidth / 640, scaleY = window.innerHeight / 480;
    for (const marker of shadow?.querySelectorAll<HTMLElement>('.marker:not(.portal)') ?? []) {
      const match = /matrix\(([^)]*)\)/.exec(marker.style.transform);
      if (!match) continue;
      const [, , , , e, f] = match[1].split(',').map(Number);
      const x = e + 3.75 * scaleX, y = f + 3.75 * scaleY;
      if (x > 40 && x < window.innerWidth - 40 && y > 60 && y < window.innerHeight * 0.8) return { x, y };
    }
    return null;
  }, MAP);
  expect(point).not.toBeNull();
  await page.mouse.move(point!.x, point!.y, { steps: 4 });
  const hint = page.locator(`${MAP} .hint`).first();
  await expect(hint).toBeVisible();
  await expect(page.locator(`${MAP} .hint-text`).first()).not.toHaveText('');
  await page.screenshot({ path: 'test-results/minimap.png' });

  await page.keyboard.press('Escape');
  await expect(page.locator(MAP)).toHaveCount(0);

  // Open again and close with the close button.
  await page.keyboard.press('Tab');
  await expect.poll(async () => (await mapState(page)).loaded, { timeout: 60_000 }).toBe(true);
  const close = await page.evaluate((selector) => {
    const rect = document.querySelector(selector)?.shadowRoot?.querySelector('.close')?.getBoundingClientRect();
    return rect ? { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 } : null;
  }, MAP);
  expect(close).not.toBeNull();
  await page.mouse.click(close!.x, close!.y);
  await expect(page.locator(MAP)).toHaveCount(0);
  await expectNoPageErrors(errors);
});
