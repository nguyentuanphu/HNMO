// HNMO's own skills (700-727; NEW_SKILLS.md): a level-400 Dark Knight has learnt its class's skills A, B
// and C by level; they show on the skill list with their recoloured icons; outside town Iron Guard
// puts its buff (180) on the hero and Earth Splitter plays its effect.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface NewSkillsGame {
  skills: { count: number; skillAt(index: number): number };
  combat: { cast(target: unknown, cell: { x: number; y: number } | null, skill?: number): boolean };
  buffs: { has(id: number, buff: number): boolean };
  online: { playerId: number; say(text: string): void; walkTo(cell: { x: number; y: number }): void; hero: { isWalking: boolean; cell: { x: number; y: number } } };
  effects?: { counts(): { effects: number; particles: number; joints: number; sprites: number } };
  mapNumber: number | null;
}
type Win = Window & { __game: NewSkillsGame };
const game = <T>(page: import('@playwright/test').Page, read: (g: NewSkillsGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);

test('a Dark Knight has its new skills by level and casts them', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400');
  // Learnt on entering (levels 80, 150, 220).
  const known = () => game(page, (g) => { const list: number[] = []; for (let i = 0; i < 80; i++) { const s = g.skills.skillAt(i); if (s) list.push(s); } return list; });
  await expect.poll(async () => (await known()).filter((s) => s >= 700 && s <= 702).sort(), { timeout: 60_000 }).toEqual([700, 701, 702]);
  // Out of town (no skills in a safe zone).
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => game(page, (g) => g.mapNumber), { timeout: 60_000 }).toBe(0);
  await page.waitForTimeout(5_000);
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => game(page, (g) => g.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
  }
  // Iron Guard: the buff on the hero and on the strip.
  expect(await game(page, (g) => g.combat.cast(null, { ...g.online.hero.cell }, 702))).toBe(true);
  await expect.poll(() => game(page, (g) => g.buffs.has(g.online.playerId, 180)), { timeout: 30_000 }).toBe(true);
  await expect(page.locator('.rml-buff_strip .buff-icon, .rml-buff_strip [data-decorator]').first()).toBeVisible({ timeout: 30_000 });
  // Earth Splitter around the hero: its effect plays.
  await page.waitForTimeout(3_500); // Iron Guard's cooldown doesn't block it, but its animation does
  const before = await game(page, (g) => g.effects?.counts() ?? null);
  expect(await game(page, (g) => g.combat.cast(null, { ...g.online.hero.cell }, 700))).toBe(true);
  await expect.poll(async () => { const now = await game(page, (g) => g.effects?.counts() ?? null); return (now?.particles ?? 0) + (now?.effects ?? 0) > (before?.particles ?? 0) + (before?.effects ?? 0); }, { timeout: 20_000 }).toBe(true);
  await page.screenshot({ path: 'test-results/new-skill.png' });
  // Their own sounds are there (made for them, one per skill).
  const sounds = await page.evaluate(async () => (await Promise.all([700, 713, 727].map((n) => fetch(`/data/Sound/hnmo/${n}.mp3`).then((r) => r.ok && (r.headers.get('content-type') ?? '').includes('audio'))))));
  expect(sounds).toEqual([true, true, true]);
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expectNoPageErrors(errors);
});
