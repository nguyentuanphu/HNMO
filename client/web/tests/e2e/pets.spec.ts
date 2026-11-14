// Pets and mounts: the helper slot's item is drawn next to its owner (src/game/pets.ts).
// The "ancient" account's knight wears a red Horn of Fenrir: hidden in town (the rider stands on
// foot there), ridden outside it, standing and running with the rider's poses. Every pet and mount
// model of the game's data loads and steps.
import { test, expect, type Page } from '@playwright/test';
import { expectNoPageErrors } from './helpers.js';

interface CompanionView {
  kind: string; visible: boolean; action: number; scale: number;
  position: { x: number; y: number; z: number };
  model: { parts: unknown[] };
}
interface HeroView {
  cell: { x: number; y: number }; position: { x: number; y: number }; action: number; isWalking: boolean;
  companions: CompanionView[];
}
interface Game {
  hero: HeroView;
  mapNumber: number;
  world: { flags: ArrayLike<number> };
  online: { say(text: string): void; walkTo(cell: { x: number; y: number }): boolean };
}
type Win = Window & { __game: Game };

const SAFEZONE = 0x01;
const BLOCKING = 0x04 | 0x08; // NOMOVE, NOGROUND (pathfinding.ts)

async function enterAs(page: Page, account: string, character: string): Promise<void> {
  await page.goto('/');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('#servers .choice')) await page.click('#servers .choice >> nth=0');
  await page.locator('#characters .choice', { hasText: character }).first().click({ timeout: 60_000 });
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 180_000 });
}

function mount(page: Page): Promise<CompanionView | null> {
  return page.evaluate(() => {
    const companion = (window as unknown as Win).__game.hero.companions[0];
    return companion ? { kind: companion.kind, visible: companion.visible, action: companion.action, scale: companion.scale,
      position: { x: companion.position.x, y: companion.position.y, z: companion.position.z }, model: { parts: [] } } : null;
  });
}

// The nearest walkable cell outside the safe zone, a few cells into the field.
function fieldCell(page: Page): Promise<{ x: number; y: number }> {
  return page.evaluate(([safe, blocking]) => {
    const game = (window as unknown as Win).__game;
    const flags = game.world.flags, start = game.hero.cell;
    const seen = new Set<number>([start.y * 256 + start.x]);
    const queue = [{ x: start.x, y: start.y, outside: 0 }];
    while (queue.length) {
      const cell = queue.shift()!;
      const outside = (flags[cell.y * 256 + cell.x] & safe) === 0 ? cell.outside + 1 : 0;
      if (outside >= 4) return { x: cell.x, y: cell.y };
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const x = cell.x + dx, y = cell.y + dy;
        if (x < 0 || y < 0 || x > 255 || y > 255 || seen.has(y * 256 + x) || (flags[y * 256 + x] & blocking)) continue;
        seen.add(y * 256 + x);
        queue.push({ x, y, outside });
      }
    }
    throw new Error('no cell outside the safe zone');
  }, [SAFEZONE, BLOCKING] as const);
}

async function walkAndWait(page: Page, cell: { x: number; y: number }): Promise<void> {
  await page.evaluate((target) => (window as unknown as Win).__game.online.walkTo(target), cell);
  await expect.poll(() => page.evaluate(() => {
    const hero = (window as unknown as Win).__game.hero;
    return !hero.isWalking ? `${hero.cell.x},${hero.cell.y}` : 'walking';
  }), { timeout: 90_000 }).toBe(`${cell.x},${cell.y}`);
}

test('every pet and mount model loads and steps next to an owner', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterAs(page, 'ancient', 'ancientDk');
  const result = await page.evaluate(async () => {
    const pets = await import('/js/game/pets.js' as string) as {
      createCompanion(kind: string): Promise<{ kind: string; visible: boolean; position: { x: number; y: number; z: number }; model: { parts: unknown[] };
        update(dt: number, owner: unknown, light: Float32Array): void; detach(): void } | null>;
    };
    const kinds = ['uniria', 'dinorant', 'darkHorse', 'fenrirRed', 'fenrirBlack', 'fenrirBlue', 'fenrirGold', 'guardianAngel', 'imp',
      'darkRaven', 'demon', 'spiritOfGuardian', 'rudolph', 'panda', 'unicorn', 'skeleton'];
    const hero = (window as unknown as Win & { __game: { hero: { model: { matrices: unknown[] } } } }).__game.hero;
    const owner = { position: { x: 13000, y: 13000 }, height: 0, angle: 0, action: 1, scale: 1, safeZone: false,
      boneMatrices: (hero as unknown as { model: { matrices: unknown[] } }).model.matrices, mapNumber: 0, ridesDarkHorse: false };
    const light = new Float32Array(256 * 256 * 3).fill(1);
    const out: Record<string, { parts: number; x: number; y: number; z: number }> = {};
    for (const kind of kinds) {
      const companion = await pets.createCompanion(kind);
      if (!companion) { out[kind] = { parts: 0, x: 0, y: 0, z: 0 }; continue; }
      for (let i = 0; i < 50; i++) companion.update(0.04, owner, light);
      out[kind] = { parts: companion.model.parts.length, x: companion.position.x, y: companion.position.y, z: companion.position.z };
      companion.detach();
    }
    return out;
  });
  for (const [kind, companion] of Object.entries(result)) {
    expect(companion.parts, `${kind} has meshes`).toBeGreaterThan(0);
    // Every companion stays near its owner (the angel flies within a few hundred units).
    expect(Math.hypot(companion.x - 13000, companion.y - 13000), `${kind} near its owner`).toBeLessThan(700);
  }
  // Mounts stand where the rider stands; the flying ones are above it.
  expect(result.fenrirRed.x).toBe(13000);
  expect(result.darkRaven.z).toBeGreaterThan(150);
  expect(result.spiritOfGuardian.z).toBeCloseTo(230, 0);
  await expectNoPageErrors(errors);
});

test('a knight rides its Fenrir outside the town', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterAs(page, 'ancient', 'ancientDk');
  if (await page.evaluate(() => (window as unknown as Win).__game.mapNumber) !== 0) {
    await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  }
  await expect.poll(async () => (await mount(page))?.kind ?? 'none', { timeout: 60_000 }).toBe('fenrirRed');

  // In the safe zone the Fenrir isn't drawn and the knight stands on foot.
  const inTown = await page.evaluate(([safe]) => {
    const game = (window as unknown as Win).__game;
    return (game.world.flags[game.hero.cell.y * 256 + game.hero.cell.x] & safe) !== 0;
  }, [SAFEZONE] as const);
  if (inTown) {
    await page.waitForTimeout(1500);
    expect((await mount(page))!.visible).toBe(false);
    await page.screenshot({ path: 'test-results/pets-town.png' });
  }

  // Outside: ridden, standing (Fenrir stand action 0, the rider's PLAYER_FENRIR_STAND poses 122-125).
  const field = await fieldCell(page);
  await walkAndWait(page, field);
  await page.waitForTimeout(2000);
  const standing = await mount(page);
  const hero = await page.evaluate(() => { const h = (window as unknown as Win).__game.hero; return { x: h.position.x, y: h.position.y, action: h.action }; });
  expect(standing!.visible).toBe(true);
  expect(standing!.action).toBe(0);
  expect(standing!.scale).toBeCloseTo(0.9);
  expect(standing!.position.x).toBeCloseTo(hero.x, 0);
  expect(standing!.position.y).toBeCloseTo(hero.y, 0);
  expect(hero.action).toBeGreaterThanOrEqual(122);
  expect(hero.action).toBeLessThanOrEqual(125);
  await page.screenshot({ path: 'test-results/pets-fenrir-standing.png' });

  // Walking: the Fenrir walks or runs with the rider (actions 1 and 2).
  const back = await page.evaluate(() => { const c = (window as unknown as Win).__game.hero.cell; return { x: c.x, y: c.y }; });
  const further = { x: back.x + (back.x > 128 ? -3 : 3), y: back.y };
  await page.evaluate((target) => (window as unknown as Win).__game.online.walkTo(target), further);
  await expect.poll(async () => (await mount(page))!.action, { timeout: 30_000 }).toBeGreaterThanOrEqual(1);
  const walking = await mount(page);
  expect([1, 2]).toContain(walking!.action);
  await page.screenshot({ path: 'test-results/pets-fenrir-walking.png' });
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.hero.isWalking), { timeout: 60_000 }).toBe(false);

  // Back to town, so the account starts there next time.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(3000);
  expect(await page.evaluate(() => (window as unknown as Win).__game.mapNumber)).toBe(0);
  await expectNoPageErrors(errors);
});
