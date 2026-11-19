// A character that walks or moves while its model is still loading shows where the server says once it is loaded.
// On a slow machine a player who walked right after coming into view stayed for good, on the other players' screens,
// where it came into view (the walk packet came while its model was loading and was dropped).
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Cell { x: number; y: number }
interface Shown { cell: Cell; isWalking: boolean }
interface Online {
  hero: { cell: Cell };
  characters: Map<number, Shown>;
  pending: Set<number>;
  spawn(id: number, create: () => Promise<unknown>, placement: { x: number; y: number; targetX: number; targetY: number; rotation: number }): Promise<void>;
  walked(packet: { ObjectId: number; TargetX: number; TargetY: number }): void;
  moved(packet: { ObjectId: number; PositionX: number; PositionY: number }): void;
  remove(packet: { Objects: { Id: number }[] }): void;
}
type Win = Window & {
  __game: { online: Online; mapNumber: number; debug: { createMonsterCharacter(options: { id: number; type: number; map: number }): Promise<unknown> } };
  __release?: () => void;
  __spawned?: Promise<void>;
};

const SPIDER = 3;

/** Starts showing a spider at `from` (relative to the hero) whose model load waits until `release` is called. */
async function spawnHeld(page: Page, id: number, from: Cell): Promise<Cell> {
  return page.evaluate(({ id, from, type }) => {
    const w = window as unknown as Win, g = w.__game, o = g.online;
    const at = { x: o.hero.cell.x + from.x, y: o.hero.cell.y + from.y };
    let release!: () => void;
    const gate = new Promise<void>((resolve) => { release = resolve; });
    w.__release = release;
    w.__spawned = o.spawn(id, async () => { await gate; return g.debug.createMonsterCharacter({ id, type, map: g.mapNumber }); },
      { x: at.x, y: at.y, targetX: at.x, targetY: at.y, rotation: 0 });
    return at;
  }, { id, from, type: SPIDER });
}

async function release(page: Page): Promise<void> {
  await page.evaluate(async () => { const w = window as unknown as Win; w.__release!(); await w.__spawned; });
}

const shown = (page: Page, id: number) => page.evaluate((i) => {
  const c = (window as unknown as Win).__game.online.characters.get(i);
  return c ? { x: c.cell.x, y: c.cell.y, walking: c.isWalking } : null;
}, id);

test('a character walking or moving while its model loads shows where the server says', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test8');
  // Entering may still bring a map change, which clears the characters in view (and those loading).
  await page.waitForTimeout(5_000);

  // A walk while loading: it shows at the cell it came into view at and walks to the walk's target.
  const walker = 0x7f01;
  const start = await spawnHeld(page, walker, { x: 2, y: 0 });
  await expect.poll(() => page.evaluate((i) => (window as unknown as Win).__game.online.pending.has(i), walker)).toBe(true);
  const target = { x: start.x, y: start.y + 3 };
  await page.evaluate(({ i, t }) => (window as unknown as Win).__game.online.walked({ ObjectId: i, TargetX: t.x, TargetY: t.y }), { i: walker, t: target });
  await release(page);
  await expect.poll(() => shown(page, walker), { timeout: 30_000 }).toEqual({ ...target, walking: false });

  // An instant move while loading: it shows at the new cell, standing.
  const mover = 0x7f02;
  const before = await spawnHeld(page, mover, { x: -2, y: 0 });
  const moved = { x: before.x - 2, y: before.y + 1 };
  await page.evaluate(({ i, m }) => (window as unknown as Win).__game.online.moved({ ObjectId: i, PositionX: m.x, PositionY: m.y }), { i: mover, m: moved });
  await release(page);
  await expect.poll(() => shown(page, mover), { timeout: 30_000 }).toEqual({ ...moved, walking: false });

  // Out of view while loading: it never shows, and a walk for it after that changes nothing.
  const leaver = 0x7f03;
  await spawnHeld(page, leaver, { x: 0, y: 2 });
  await page.evaluate((i) => (window as unknown as Win).__game.online.remove({ Objects: [{ Id: i }] }), leaver);
  await page.evaluate((i) => (window as unknown as Win).__game.online.walked({ ObjectId: i, TargetX: 1, TargetY: 1 }), leaver);
  await release(page);
  expect(await shown(page, leaver)).toBeNull();

  await page.evaluate((ids) => (window as unknown as Win).__game.online.remove({ Objects: ids.map((Id) => ({ Id })) }), [walker, mover]);
  await expectNoPageErrors(errors);
});
