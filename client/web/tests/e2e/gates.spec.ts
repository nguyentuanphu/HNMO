// Gates you walk into, like on the desktop (CheckGate): stepping into Lorencia's dungeon entrance
// (gate 1, cells 121-123 x 232-233, from level 20) takes the hero to the Dungeon.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface GateGame { mapNumber: number | null; online: { say(text: string): void; walkTo(cell: { x: number; y: number }): void; hero: { isWalking: boolean } } }
type Win = Window & { __game: GateGame };

test('walking into the dungeon entrance takes the hero to the Dungeon', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  await page.waitForTimeout(5_000);
  // South through the town to the entrance (a few legs: long paths take a while at test frame rates).
  for (const goal of [{ x: 130, y: 160 }, { x: 125, y: 200 }, { x: 122, y: 225 }, { x: 122, y: 232 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => page.evaluate(() => { const g = (window as unknown as Win).__game; return g.mapNumber !== 0 || !g.online.hero.isWalking; }), { timeout: 90_000 }).toBe(true);
    if (await page.evaluate(() => (window as unknown as Win).__game.mapNumber) !== 0) break;
  }
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 60_000 }).toBe(1);
  await page.waitForTimeout(3_000);
  await page.screenshot({ path: 'test-results/gate-dungeon.png' });
  // Back to town so the account starts there next time.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  await expectNoPageErrors(errors);
});
