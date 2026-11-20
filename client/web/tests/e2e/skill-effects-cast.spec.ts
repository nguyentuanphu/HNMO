// Skills cast for real (Combat.cast at a monster next to the hero, as the right mouse button does) draw their effects:
// the cast plan lets the skill out, the cast's effect hook runs, and the effect objects it makes are counted (every
// createEffect / createParticle / createSprite / createJoint call during the cast, less what the fields around make
// with no cast over the same time). skill-effects.spec.ts plays each effect through the hook directly; this one
// catches a skill whose cast never reaches it (Spiral Slash had no cast plan, a Rage Fighter on a horn mount
// can't use Chain Drive, Dragon Slasher or Charge) or whose effect makes nothing (Phoenix Shot).
//
// It needs level 400 test characters of each class (the demo server's testgm: Dark Knight, Dark Wizard, -, Dark Lord,
// Magic Gladiator; testgm2: -, Rage Fighter). FX_ACCOUNTS=testgm:0,testgm2:1 picks some; SHOTS_DIR keeps the pictures.
import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';

interface Case { skill: number; name: string; expect: string[] }
interface Hero { account: string; slot: number; class: string; cases: Case[]; unmount?: boolean }

const HEROES: Hero[] = [
  { account: 'testgm', slot: 0, class: 'knight', cases: [{ skill: 701, name: 'blood-slash', expect: ['particle:damage2', 'particle:damage1', 'particle:blood2'] }] },
  { account: 'testgm', slot: 1, class: 'wizard', cases: [{ skill: 17, name: 'energy-ball', expect: ['effect:energy', 'particle:energy'] }] },
  { account: 'testgm', slot: 3, class: 'lord', cases: [{ skill: 65, name: 'electric-spike', expect: ['joint:flareForceJoint'] }] },
  { account: 'testgm', slot: 4, class: 'gladiator', cases: [{ skill: 57, name: 'spiral-slash', expect: ['joint:flare'] }] },
  // On foot: the test fighter rides a horn mount, which keeps Chain Drive, Dragon Slasher and Charge from casting.
  { account: 'testgm2', slot: 1, class: 'fighter', unmount: true, cases: [
    { skill: 262, name: 'chain-drive', expect: ['effect:swordEffect', 'effect:shockWave1'] },
    { skill: 265, name: 'dragon-slasher', expect: ['effect:dragonKickDummy', 'effect:multiShot1'] },
    { skill: 269, name: 'charge', expect: ['effect:multiShot1', 'joint:jointSpark'] },
    { skill: 270, name: 'phoenix-shot', expect: ['effect:phoenixShot', 'particle:fireHik1'] },
  ] },
];
const WAYPOINTS = [{ x: 176, y: 126 }, { x: 190, y: 128 }, { x: 205, y: 140 }, { x: 200, y: 160 }, { x: 185, y: 175 }, { x: 165, y: 185 }, { x: 150, y: 200 }];
const SHOTS_DIR = process.env.SHOTS_DIR ?? 'test-results/skill-cast-shots';
const picked = process.env.FX_ACCOUNTS?.split(',');

// The page's game, loosely typed (the tests read it through page.evaluate).
type G = any; // eslint-disable-line @typescript-eslint/no-explicit-any
const living = (page: Page) => page.evaluate(() => [...(window as unknown as { __game: G }).__game.online.characters.values()].filter((c: G) => c.kind === 'monster' && !c.dead).length);
const idle = (page: Page) => page.waitForFunction(() => { const o = (window as unknown as { __game: G }).__game.online; return !o.goal && !o.hero.isWalking; }, null, { timeout: 60_000 }).catch(() => {});

async function enter(page: Page, account: string, slot: number): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.waitForSelector('.season-column .choice, #servers .choice', { timeout: 120_000 });
  await page.click('.season-column .choice >> nth=0').catch(() => page.click('#servers .choice >> nth=0'));
  await page.waitForSelector('#characters .choice', { timeout: 120_000 });
  await page.click(`#characters .choice >> nth=${slot}`);
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 240_000 });
  await page.evaluate(() => (window as unknown as { __game: G }).__game.onboarding?.close());
}

for (const hero of HEROES) {
  if (picked && !picked.includes(`${hero.account}:${hero.slot}`)) continue;
  test(`the ${hero.class}'s skills cast at a monster draw their effects`, async ({ page }) => {
    test.setTimeout(10 * 60_000);
    mkdirSync(SHOTS_DIR, { recursive: true });
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await enter(page, hero.account, hero.slot);
    await page.evaluate(() => (window as unknown as { __game: G }).__game.effects.ready);
    if (await page.evaluate(() => (window as unknown as { __game: G }).__game.mapNumber) !== 0) {
      await page.evaluate(() => (window as unknown as { __game: G }).__game.online.say('/move lorencia'));
      await page.waitForFunction(() => { const g = (window as unknown as { __game: G }).__game; return g.mapNumber === 0 && !g.online.changingMap; }, null, { timeout: 90_000 });
    }
    let way = 0;
    for (; way < WAYPOINTS.length && await living(page) < 3; way++) {
      await page.evaluate((cell) => (window as unknown as { __game: G }).__game.online.walkTo(cell), WAYPOINTS[way]);
      await idle(page);
    }
    // Count what is made, by kind; and the cast's effect hook.
    await page.evaluate(() => {
      const w = window as unknown as { __game: G; __made: Record<string, number>; __skills: number[] };
      const g = w.__game;
      w.__made = {}; w.__skills = [];
      const onSkill = g.combat.events.onSkill;
      g.combat.events.onSkill = (c: G, s: number, t: G, cell: G) => { if (c === g.online.hero) w.__skills.push(s); onSkill?.(c, s, t, cell); };
      for (const m of ['createEffect', 'createParticle', 'createSprite', 'createJoint']) {
        const f = g.effects[m].bind(g.effects);
        g.effects[m] = (kind: string, ...rest: unknown[]) => { const k = `${m.slice(6).toLowerCase()}:${kind}`; w.__made[k] = (w.__made[k] ?? 0) + 1; return f(kind, ...rest); };
      }
    });
    let helperSlot: number | null = null;
    if (hero.unmount) {
      helperSlot = await page.evaluate(() => { const inv = (window as unknown as { __game: G }).__game.inventory; const h = inv.items.get(8); if (!h) return null; const s = inv.firstEmptySlot(inv.items, { firstSlot: 12, columns: 8, rows: 8 }, h); if (s === null) return null; inv.move(0, 8, 0, s); return s; });
      await expect.poll(() => page.evaluate(() => (window as unknown as { __game: G }).__game.inventory.items.has(8)), { timeout: 15_000 }).toBe(false);
    }
    try {
      for (const c of hero.cases) {
        for (let tries = 0; tries < 12 && await living(page) === 0; tries++) {
          await page.evaluate((cell) => (window as unknown as { __game: G }).__game.online.walkTo(cell), WAYPOINTS[way++ % WAYPOINTS.length]);
          await idle(page);
          await page.waitForTimeout(1_500);
        }
        await page.evaluate(() => {
          const g = (window as unknown as { __game: G }).__game; const me = g.online.hero;
          const m = [...g.online.characters.values()].filter((x: G) => x.kind === 'monster' && !x.dead)
            .sort((a: G, b: G) => Math.hypot(a.cell.x - me.cell.x, a.cell.y - me.cell.y) - Math.hypot(b.cell.x - me.cell.x, b.cell.y - me.cell.y))[0];
          if (m && Math.hypot(m.cell.x - me.cell.x, m.cell.y - me.cell.y) > 2) { const near = g.online.cellNextTo(m.cell); if (near) g.online.walkTo(near); }
        });
        await idle(page);
        // The fields' own effects (torches, the hero's wings and items) over 1.5 s without a cast.
        await page.evaluate(() => { const w = window as unknown as { __made: Record<string, number>; __from: number }; w.__made = {}; w.__from = performance.now(); });
        await page.waitForTimeout(1_500);
        const cast = await page.evaluate((skill) => {
          const w = window as unknown as { __game: G; __made: Record<string, number>; __ambient: Record<string, number>; __ambientMs: number; __from: number; __skills: number[] };
          const g = w.__game; const me = g.online.hero;
          w.__ambient = { ...w.__made }; w.__ambientMs = performance.now() - w.__from; w.__made = {}; w.__from = performance.now(); w.__skills = [];
          const target = [...g.online.characters.values()].filter((x: G) => x.kind === 'monster' && !x.dead)
            .sort((a: G, b: G) => Math.hypot(a.cell.x - me.cell.x, a.cell.y - me.cell.y) - Math.hypot(b.cell.x - me.cell.x, b.cell.y - me.cell.y))[0] ?? null;
          return g.combat.cast(target, target ? { ...target.cell } : null, skill);
        }, c.skill);
        expect(cast, `${c.name} is castable`).toBe(true);
        await expect.poll(() => page.evaluate(() => (window as unknown as { __skills: number[] }).__skills), { timeout: 15_000 }).toContain(c.skill);
        await page.waitForTimeout(700);
        await page.screenshot({ path: `${SHOTS_DIR}/${hero.class}-${c.name}.png` });
        await page.waitForTimeout(2_000);
        const made = await page.evaluate(() => {
          const w = window as unknown as { __made: Record<string, number>; __ambient: Record<string, number>; __ambientMs: number; __from: number };
          const scale = (performance.now() - w.__from) / Math.max(1, w.__ambientMs);
          const net: Record<string, number> = {};
          for (const [k, v] of Object.entries(w.__made)) { const n = Math.round(v - (w.__ambient[k] ?? 0) * scale * 1.5); if (n > 0) net[k] = n; }
          return net;
        });
        const total = Object.values(made).reduce((a, b) => a + b, 0);
        console.log(`${c.name}: ${total} effect objects ${JSON.stringify(made)}`);
        expect(total, `${c.name} makes effects`).toBeGreaterThan(0);
        for (const key of c.expect) expect(Object.keys(made), `${c.name} makes ${key}`).toContain(key);
        await page.evaluate(() => (window as unknown as { __game: G }).__game.combat.cancel());
      }
    } finally {
      if (helperSlot !== null) await page.evaluate((s) => (window as unknown as { __game: G }).__game.inventory.move(0, s, 0, 8), helperSlot);
      await page.waitForTimeout(2_000);
    }
    expect(errors).toEqual([]);
  });
}
