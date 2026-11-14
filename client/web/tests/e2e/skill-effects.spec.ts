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

const KNIGHT: Case[] = [
  { skill: 41, name: 'twisting-slash', expect: ['effect:skillWheel1', 'effect:skillWheel2', 'joint:jointSpark'], shotAt: 900 },
  { skill: 42, name: 'rageful-blow', expect: ['effect:skillFuryStrike', 'effect:earthQuake1', 'effect:earthQuake5', 'effect:earthQuake7', 'effect:wave'], shotAt: 1050 },
  { skill: 43, name: 'death-stab', expect: ['joint:spearSkill', 'effect:lightSpear', 'joint:flare'], shotAt: 420 },
  { skill: 47, name: 'impale', expect: ['effect:lightSpear', 'joint:flare', 'effect:spearSkillModel'], shotAt: 600 },
  { skill: 48, name: 'swell-life', expect: ['joint:jointSpirit', 'effect:magicGround2'], shotAt: 600 },
  { skill: 59, name: 'combo', expect: ['effect:combo', 'joint:lightJoint'], shotAt: 250 },
  { skill: 22, name: 'cyclone', expect: ['particle:spark', 'particle:spark3'], shotAt: 650 },
];

const ELF: Case[] = [
  { skill: 52, name: 'penetration', expect: ['effect:gathering', 'particle:spark3'], shotAt: 240 },
  { skill: 235, name: 'multi-shot', expect: ['effect:multiShot1', 'effect:multiShot2', 'effect:multiShot3'], shotAt: 200 },
  { skill: 77, name: 'infinity-arrow', expect: ['effect:infinityArrow', 'effect:infinityArrow1', 'effect:infinityArrow4'], shotAt: 300 },
  { skill: 30, name: 'summon', expect: ['effect:magicGround2', 'joint:jointHealing'], shotAt: 800 },
];

const GLADIATOR: Case[] = [
  { skill: 55, name: 'fire-slash', expect: ['effect:gathering', 'effect:swordForce', 'joint:jointForce', 'joint:jointThunder'], shotAt: 400 },
  { skill: 56, name: 'power-slash', expect: ['effect:magic2'], shotAt: 250 },
];

const LORD: Case[] = [
  { skill: 66, name: 'force-wave', expect: ['effect:waves', 'effect:piercing2', 'joint:piercing'], shotAt: 680 },
];

const CASES: Record<string, Case[]> = { wizard: WIZARD, knight: KNIGHT, elf: ELF, gladiator: GLADIATOR, lord: LORD };
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
    // Without a monster close by, a targeted skill is aimed at the hero itself.
    g.combat.events.onSkill(hero, skill, area ? null : best ?? hero, cell);
    return best !== null;
  }, { skill, area });
}

const groups = process.env.SKILL_GROUPS?.split(',');
for (const [group, cases] of Object.entries(CASES)) {
  if (groups && !groups.includes(group)) continue;
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
      // The effect system's tick is watched from the page: what each tick holds is gathered, and at
      // the picture's tick the effects stop moving (they are still drawn) until the picture is taken,
      // so the pictures don't depend on how fast the test machine draws.
      await page.evaluate((freezeAt) => {
        type Ticking = { step(): void; census(): Record<string, number>; __seen?: Set<string>; __frozen?: boolean; __ticks?: number; __step?: () => void };
        const e = (window as unknown as { __game: { effects: Ticking } }).__game.effects;
        e.__step ??= e.step;
        const step = e.__step;
        e.__seen = new Set<string>();
        e.__frozen = false;
        e.__ticks = 0;
        e.step = function (this: Ticking) {
          if (this.__frozen) return;
          step.call(this);
          for (const key of Object.keys(this.census())) this.__seen?.add(key);
          if (++this.__ticks! === freezeAt) this.__frozen = true;
        };
      }, Math.max(1, Math.round(c.shotAt / 40)));
      await cast(page, c.skill, c.area ?? false);
      await page.waitForFunction(() => (window as unknown as { __game: { effects: { __frozen?: boolean } } }).__game.effects.__frozen === true, null, { timeout: 30_000 });
      await page.waitForTimeout(400); // a frame or two drawn
      await page.screenshot({ path: `${SHOTS_DIR}/${group}-${c.name}.png` });
      await page.evaluate(() => { (window as unknown as { __game: { effects: { __frozen?: boolean } } }).__game.effects.__frozen = false; });
      await page.waitForTimeout(3_000);
      const seen = new Set(await page.evaluate(() => [...((window as unknown as { __game: { effects: { __seen?: Set<string> } } }).__game.effects.__seen ?? [])]));
      for (const key of c.expect) expect(seen, `${c.name} makes ${key}`).toContain(key);
      // Everything it made ends (Soul Barrier's lights last while the buff does).
      if (c.name !== 'soul-barrier') {
        await expect.poll(() => game(page, (g) => g.effects.counts().effects + g.effects.counts().joints), { timeout: 20_000 }).toBe(0);
      }
    }
    if (group === 'knight' && (!only || only.includes('weapon-trail'))) {
      // The weapon trail of a sword skill's swing (CreateWeaponBlur): Cyclone's action, PLAYER_ATTACK_SKILL_SWORD4,
      // played slowly (2 keys a second) so the test machine's few frames a second see it; the monsters'
      // hits don't interrupt it with the hero's shock meanwhile.
      await page.evaluate(() => {
        type Hero = { playOnce(action: number, keysPerSecond?: number): void };
        const hero = (window as unknown as { __game: { online: { hero: Hero } } }).__game.online.hero;
        const playOnce = hero.playOnce.bind(hero);
        playOnce(63, 2);
        hero.playOnce = () => { /* held for the test */ };
        setTimeout(() => { hero.playOnce = playOnce; }, 4_000);
      });
      await expect.poll(() => page.evaluate(() => (window as unknown as { __game: { weaponTrails?: { count: number } } }).__game.weaponTrails?.count ?? 0), { timeout: 5_000, intervals: [50] }).toBeGreaterThan(0);
      await page.screenshot({ path: `${SHOTS_DIR}/knight-weapon-trail.png` });
      await expect.poll(() => page.evaluate(() => (window as unknown as { __game: { weaponTrails?: { count: number } } }).__game.weaponTrails?.count ?? 0), { timeout: 10_000 }).toBe(0);
    }
    expect(errors).toEqual([]);
  });
}
