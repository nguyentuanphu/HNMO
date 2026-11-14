// The class change quests' reward (ReceiveQuestPrize, C1 A3): level-up points for the hero, the
// light around the player and the sound. Fed as a recorded packet: a real class change needs the
// whole quest chain.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface RewardGame {
  connection: { dispatch(bytes: Uint8Array): void };
  online: { playerId: number };
  stats: { levelUpPoints: number };
  effects?: { counts(): { effects: number; particles: number; joints: number; sprites: number } };
}
type Win = Window & { __game: RewardGame };

test('a quest reward of level-up points adds them and lights up the hero', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test3');
  const before = await page.evaluate(() => (window as unknown as Win).__game.stats.levelUpPoints);
  const sprites = await page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    const sprites0 = g.effects?.counts().sprites ?? 0;
    const id = g.online.playerId;
    // C1 07 A3, the player id (big endian), reward 200 (level-up points), 3 points.
    g.connection.dispatch(new Uint8Array([0xc1, 0x07, 0xa3, (id >> 8) & 0xff, id & 0xff, 200, 3]));
    return (g.effects?.counts().sprites ?? 0) - sprites0;
  });
  expect(sprites).toBeGreaterThan(0);
  expect(await page.evaluate(() => (window as unknown as Win).__game.stats.levelUpPoints)).toBe(before + 3);
  await page.screenshot({ path: 'test-results/quest-reward.png' });
  await expectNoPageErrors(errors);
});
