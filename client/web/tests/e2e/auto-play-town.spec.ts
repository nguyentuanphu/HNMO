// HNMO extra of auto play: a trip to town — walk to the potion seller, buy healing potions, walk
// back to the hunting spot and carry on hunting.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface TownGame {
  online: { hero: { cell: { x: number; y: number }; isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  inventory: { items: Map<number, { group: number; number: number; durability: number }> };
  autoPlay: { active: boolean; setConfig(config: unknown): void };
  autoPlayExtras: { onTrip: boolean; goToTown(): void; settings: { potionsToBuy: number } };
}
type Win = Window & { __game: TownGame };

const potions = (page: Page) => page.evaluate(() => {
  let n = 0;
  for (const [slot, item] of (window as unknown as Win).__game.inventory.items) if (slot >= 12 && item.group === 14 && item.number <= 3) n += item.durability;
  return n;
});

test('auto play goes to town for potions and comes back to hunt', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test8');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 88 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
  }
  const spot = await page.evaluate(() => ({ ...(window as unknown as Win).__game.online.hero.cell }));
  const before = await potions(page);
  await page.evaluate((target) => {
    const g = (window as unknown as Win).__game;
    g.autoPlayExtras.settings.potionsToBuy = target;
    g.autoPlayExtras.goToTown();
  }, before + 3);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.autoPlayExtras.onTrip)).toBe(true);
  // It buys healing potions from the potion seller, walks back and starts hunting again.
  await expect.poll(() => potions(page), { timeout: 180_000 }).toBeGreaterThan(before);
  await page.screenshot({ path: 'test-results/auto-play-town.png' });
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.autoPlayExtras.onTrip), { timeout: 180_000 }).toBe(false);
  const back = await page.evaluate(() => ({ ...(window as unknown as Win).__game.online.hero.cell }));
  expect(Math.hypot(back.x - spot.x, back.y - spot.y)).toBeLessThanOrEqual(2);
  await expectNoPageErrors(errors);
});
