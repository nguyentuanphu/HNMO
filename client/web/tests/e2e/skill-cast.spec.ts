// Casting like on the desktop: the right mouse button uses the current skill on the monster under
// the mouse — the hero walks into the skill's range, plays the skill's action and sends it
// (TargetedSkill), and the server shows the cast and hits the monster.
import { test, expect } from '@playwright/test';
import { enterWorld } from './helpers.js';

interface Fighter { id: number; kind: string; dead: boolean; healthStatus: number; cell: { x: number; y: number } }
interface CastGame {
  online: { characters: Map<number, Fighter>; hero: { cell: { x: number; y: number }; isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  combat: { cast(target: unknown, cell: { x: number; y: number } | null): boolean };
  skills: { skills: Map<number, number>; current: number; select(index: number): void };
  stats: { mana: number };
}
type Win = Window & { __game: CastGame };

test('the right mouse button casts the current skill at a monster', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const sent: number[][] = [];
  const received: string[] = [];
  page.on('websocket', (socket) => {
    socket.on('framesent', (frame) => { if (typeof frame.payload !== 'string') sent.push([...frame.payload.subarray(0, 9)]); });
    socket.on('framereceived', (frame) => { if (typeof frame.payload !== 'string') received.push(Buffer.from(frame.payload.subarray(0, 12)).toString('hex')); });
  });
  await enterWorld(page, 'test400'); // the knight knows Cyclone (22)
  await expect.poll(() => page.evaluate(() => [...(window as unknown as Win).__game.skills.skills.values()].includes(22))).toBe(true);
  await page.evaluate(() => {
    const s = (window as unknown as Win).__game.skills;
    for (const [index, skill] of s.skills) if (skill === 22) s.select(index);
  });
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  const nearest = () => page.evaluate(() => {
    const game = (window as unknown as Win).__game;
    const hero = game.online.hero.cell;
    let best: Fighter | null = null, bestDistance = Infinity;
    for (const c of game.online.characters.values()) {
      if (c.kind !== 'monster' || c.dead) continue;
      const d = Math.hypot(c.cell.x - hero.x, c.cell.y - hero.y);
      if (d < bestDistance) { best = c; bestDistance = d; }
    }
    return best ? { id: best.id, distance: bestDistance } : null;
  });
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 70 }, { x: 170, y: 58 }, { x: 185, y: 45 }, { x: 200, y: 35 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
    if (goal.y <= 70 && await nearest()) break;
  }
  await expect.poll(nearest, { timeout: 60_000 }).not.toBeNull();
  const target = (await nearest())!;
  const mark = received.length;
  expect(await page.evaluate((id) => { const g = (window as unknown as Win).__game; return g.combat.cast(g.online.characters.get(id), null); }, target.id)).toBe(true);
  // TargetedSkill (C3 07 19, skill 22, the monster's id) goes out, the server takes the mana.
  await expect.poll(() => sent.some((b) => b[0] === 0xc3 && b[2] === 0x19 && ((b[3] << 8) | b[4]) === 22), { timeout: 120_000 }).toBe(true);
  // The server shows the cast to everyone around (SkillAnimation C3 09 19: skill 22 from the hero), and the monster is hit.
  await expect.poll(() => received.slice(mark).some((h) => h.startsWith('c3091900160200')), { timeout: 30_000 }).toBe(true);
  await expect.poll(() => page.evaluate((id) => { const c = (window as unknown as Win).__game.online.characters.get(id); return !c || c.dead || c.healthStatus < 1; }, target.id), { timeout: 120_000 }).toBe(true);
  await page.screenshot({ path: 'test-results/skill-cast.png' });
  expect(errors).toEqual([]);
});
