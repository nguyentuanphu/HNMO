// Playing on a touch screen (HNMO extra): the button bar shows, its buttons open what the keys open,
// a tap walks, two fingers pinch to zoom and turn the camera.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface TouchGame { view: { yaw: number; distance: number }; online: { hero: { isWalking: boolean; cell: { x: number; y: number } } } }
type Win = Window & { __game: TouchGame };
const game = <T>(page: import('@playwright/test').Page, read: (g: TouchGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);

test.use({ hasTouch: true });

test('touch: the button bar, a tap walks, two fingers zoom and turn', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test6');
  const bar = page.locator('#touchBar');
  await expect(bar).toBeVisible();
  // The I button opens the inventory, again closes it.
  await page.tap('#touchBar button[data-key="i"]');
  await expect(page.locator('.rml-my_inventory #panel')).toBeVisible({ timeout: 90_000 });
  await page.tap('#touchBar button[data-key="i"]');
  await expect(page.locator('.rml-my_inventory')).toHaveCount(0, { timeout: 30_000 });
  // A tap on the ground a little away walks there.
  const before = await game(page, (g) => ({ ...g.online.hero.cell }));
  await page.touchscreen.tap(760, 420);
  await expect.poll(async () => { const now = await game(page, (g) => ({ ...g.online.hero.cell })); return now.x !== before.x || now.y !== before.y; }, { timeout: 30_000 }).toBe(true);
  // Two fingers moving apart zoom in; sliding sideways turns.
  const cdp = await page.context().newCDPSession(page);
  const touch = (type: 'touchStart' | 'touchMove' | 'touchEnd', points: { x: number; y: number }[]) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: points.map((p, id) => ({ x: p.x, y: p.y, id })) });
  const view0 = await game(page, (g) => ({ ...g.view }));
  await touch('touchStart', [{ x: 560, y: 300 }, { x: 720, y: 300 }]);
  for (let i = 1; i <= 5; i++) await touch('touchMove', [{ x: 560 - i * 20 + i * 15, y: 300 }, { x: 720 + i * 20 + i * 15, y: 300 }]);
  await touch('touchEnd', []);
  const view1 = await game(page, (g) => ({ ...g.view }));
  expect(view1.distance).toBeLessThan(view0.distance);
  expect(view1.yaw).not.toBe(view0.yaw);
  await page.screenshot({ path: 'test-results/touch.png' });
  await expectNoPageErrors(errors);
});
