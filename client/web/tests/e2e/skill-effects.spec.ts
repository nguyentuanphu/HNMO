// The skills' effects as the desktop draws them (render/effects): each ported skill is played
// through the game's own cast hook (Combat's onSkill, as when the server shows a cast) by the hero
// at the nearest monster; its effect objects appear and are gone again once it has played.
// Screenshots of each skill mid-effect go to SHOTS_DIR (default test-results/skill-shots).
import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { enterWorld } from './helpers.js';

interface Fighter { id: number; kind: string; dead: boolean; cell: { x: number; y: number } }
interface EffectsGame {
  online: { characters: Map<number, Fighter>; hero: Fighter & { isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  combat: { events: { onSkill(caster: unknown, skill: number, target: unknown, cell: { x: number; y: number } | null): void } };
  effects: { ready: Promise<void>; counts(): { effects: number; particles: number; joints: number; sprites: number }; census(): Record<string, number> };
}
type Win = Window & { __game: EffectsGame };

/** A skill, the objects its effect must make (census keys), and when to take its picture (ms after the cast). */
interface Case { skill: number; name: string; expect: string[]; shotAt: number; area?: boolean }

const WIZARD: Case[] = [
  { skill: 2, name: 'meteorite', expect: ['effect:fireModel'], shotAt: 1100 },
  { skill: 7, name: 'ice', expect: ['effect:ice', 'effect:iceSmall'], shotAt: 900 },
  { skill: 1, name: 'poison', expect: ['effect:poison', 'particle:smoke'], shotAt: 900 },
  { skill: 5, name: 'flame', expect: ['effect:flame', 'particle:flame'], shotAt: 1100, area: true },
  { skill: 8, name: 'twister', expect: ['effect:storm', 'joint:jointThunder'], shotAt: 1300 },
  { skill: 9, name: 'evil-spirit', expect: ['joint:jointSpirit', 'effect:laser'], shotAt: 1200 },
  { skill: 10, name: 'hellfire', expect: ['effect:circle', 'effect:circleLight'], shotAt: 1000 },
  { skill: 12, name: 'aqua-beam', expect: ['effect:bossLaser', 'sprite:spark3'], shotAt: 800 },
  { skill: 14, name: 'inferno', expect: ['effect:skillInferno', 'particle:explosion'], shotAt: 800 },
  { skill: 39, name: 'ice-storm', expect: ['effect:blizzardModel'], shotAt: 1000, area: true },
  { skill: 6, name: 'teleport', expect: ['effect:spark3', 'particle:spark3'], shotAt: 150 },
  { skill: 38, name: 'decay', expect: ['effect:fireModel', 'joint:smokeTrail'], shotAt: 1000, area: true },
  { skill: 40, name: 'nova', expect: ['joint:jointSpirit'], shotAt: 750 },
  // Last: its lights circle the hero while the buff lasts (here, until the page closes).
  { skill: 16, name: 'soul-barrier', expect: ['joint:spearSkill'], shotAt: 1200 },
];

const CASES: Record<string, Case[]> = { wizard: WIZARD };
const SHOTS_DIR = process.env.SHOTS_DIR ?? 'test-results/skill-shots';

const game = <T>(page: Page, read: (g: EffectsGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);

async function goHunting(page: Page): Promise<void> {
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(6_000);
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 72 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => game(page, (g) => g.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
  }
}

/** Plays the skill as the server shows it: the hero casts at the nearest monster (or a tile ahead). */
function cast(page: Page, skill: number, area: boolean): Promise<boolean> {
  return page.evaluate(({ skill, area }) => {
    const g = (window as unknown as Win).__game;
    const hero = g.online.hero;
    let best: Fighter | null = null, bestDistance = 8;
    for (const c of g.online.characters.values()) {
      if (c.kind !== 'monster' || c.dead) continue;
      const d = Math.hypot(c.cell.x - hero.cell.x, c.cell.y - hero.cell.y);
      if (d < bestDistance) { best = c; bestDistance = d; }
    }
    const cell = best ? { ...best.cell } : { x: hero.cell.x + 3, y: hero.cell.y - 3 };
    g.combat.events.onSkill(hero, skill, area ? null : best, area || !best ? cell : cell);
    return best !== null;
  }, { skill, area });
}

for (const [group, cases] of Object.entries(CASES)) {
  test(`the ${group}'s skills draw their effects`, async ({ page }) => {
    test.setTimeout(10 * 60_000);
    mkdirSync(SHOTS_DIR, { recursive: true });
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await enterWorld(page, 'test400');
    await page.evaluate(() => (window as unknown as Win).__game.effects.ready);
    await goHunting(page);
    const only = process.env.SKILLS?.split(',');
    for (const c of cases) {
      if (only && !only.includes(c.name)) continue;
      await expect.poll(() => game(page, (g) => g.effects.counts().effects + g.effects.counts().joints), { timeout: 30_000 }).toBe(0);
      // What it makes is gathered in the page every 40 ms (one tick), so short-lived objects count too.
      await page.evaluate(() => {
        const w = window as unknown as Win & { __seen?: Set<string>; __seenTimer?: number };
        w.__seen = new Set<string>();
        clearInterval(w.__seenTimer);
        w.__seenTimer = window.setInterval(() => { for (const key of Object.keys(w.__game.effects.census())) w.__seen?.add(key); }, 40);
      });
      await cast(page, c.skill, c.area ?? false);
      await page.waitForTimeout(c.shotAt);
      await page.screenshot({ path: `${SHOTS_DIR}/${group}-${c.name}.png` });
      await page.waitForTimeout(Math.max(0, 3_000 - c.shotAt));
      const seen = new Set(await page.evaluate(() => [...((window as unknown as { __seen?: Set<string> }).__seen ?? [])]));
      for (const key of c.expect) expect(seen, `${c.name} makes ${key}`).toContain(key);
      // Everything it made ends (Soul Barrier's lights last while the buff does).
      if (c.name !== 'soul-barrier') {
        await expect.poll(() => game(page, (g) => g.effects.counts().effects + g.effects.counts().joints), { timeout: 20_000 }).toBe(0);
      }
    }
    expect(errors).toEqual([]);
  });
}
