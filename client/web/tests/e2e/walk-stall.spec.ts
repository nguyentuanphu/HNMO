// A walk keeps up with the clock when the page stands still for a while (a hidden tab, a slow phone, many pages on one
// computer). The server walks the character in real time; the page capped each frame at half a second, so after a
// stall the hero stood behind the server, and its next walk was refused as a speed hack (seen in the Arena's team
// battles: 7 to 9 cells apart). Here the page is held for 3 seconds in the middle of a walk across Lorencia's field:
// afterwards the hero is as far as the time says (3 cells a second), not half a second further.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface WalkGame {
  mapNumber: number;
  hero: { cell: { x: number; y: number }; isWalking: boolean };
  online: { say(text: string): void; walkTo(cell: { x: number; y: number }): boolean; goal?: unknown };
}
type Win = Window & { __game: WalkGame };
const game = <T>(page: Page, f: (g: WalkGame) => T) => page.evaluate(`(${f.toString()})(window.__game)`) as Promise<T>;

// The field east of Lorencia's town: 30 walkable cells in a row.
const START = { x: 176, y: 126 }, END = { x: 206, y: 126 };

async function walkAndWait(page: Page, cell: { x: number; y: number }): Promise<void> {
  await page.evaluate((target) => (window as unknown as Win).__game.online.walkTo(target), cell);
  await expect.poll(() => game(page, (g) => (!g.hero.isWalking && !g.online.goal ? `${g.hero.cell.x},${g.hero.cell.y}` : 'walking')), { timeout: 90_000 }).toBe(`${cell.x},${cell.y}`);
}

test('a walk catches up with the clock after the page stood still', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test8');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => game(page, (g) => g.mapNumber), { timeout: 60_000 }).toBe(0);
  await walkAndWait(page, START);

  // Start, hold and measure inside one page call: on a slow machine (frames of 150 ms) the round trips of a poll let the
  // hero walk most of the field before the hold began, leaving fewer cells than the hold is worth.
  const { before, after } = await page.evaluate(async ({ end, startX }) => {
    const g = (window as unknown as Win).__game;
    const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));
    g.online.walkTo(end);
    while (g.hero.cell.x < startX + 2) await frame();
    const beforeX = g.hero.cell.x;
    // The page stands still for 3 seconds: no frame is drawn.
    const until = performance.now() + 3000;
    while (performance.now() < until) { /* held */ }
    // One frame later the hero has walked the 3 seconds (9 cells at 3 a second; a run is faster), not half a second.
    await frame(); await frame();
    return { before: beforeX, after: g.hero.cell.x };
  }, { end: END, startX: START.x });
  expect(after - before, `from ${before} to ${after}`).toBeGreaterThanOrEqual(6);

  await walkAndWait(page, END);
  await expectNoPageErrors(errors);
});
