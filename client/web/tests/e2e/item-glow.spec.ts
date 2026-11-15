// Items glow as the desktop draws them (RenderPartObjectEffect, game/itemGlow.ts): a row of players next to the hero
// wear the same sword and armour at +0, +7, +9, +11, +13 and +15, excellent and ancient; their glow passes come
// from shared materials (a second row adds none), a debuff takes the level's glow away, the cloak turns a player
// into a chrome shell, a +15 weapon sparkles, and the same items are drawn as window icons (with the Jewel of
// Harmony's shine below +3). Pictures go to SHOTS_DIR (default test-results/item-glow).
import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { enterWorld } from './helpers.js';

const SHOTS_DIR = process.env.SHOTS_DIR ?? 'test-results/item-glow';

interface Variant { label: string; level: number; excellent: boolean; ancient: boolean }
const VARIANTS: Variant[] = [
  { label: '+0', level: 0, excellent: false, ancient: false },
  { label: '+7', level: 7, excellent: false, ancient: false },
  { label: '+9', level: 9, excellent: false, ancient: false },
  { label: '+11', level: 11, excellent: false, ancient: false },
  { label: '+13', level: 13, excellent: false, ancient: false },
  { label: '+15', level: 15, excellent: false, ancient: false },
  { label: 'excellent', level: 0, excellent: true, ancient: false },
  { label: 'ancient', level: 0, excellent: false, ancient: true },
];
const SWORD = { group: 0, number: 5 };
const ARMOR = 5;

/** Builds a row of players (ids from `firstId`) wearing the variants, beside the hero; returns their glow mesh counts. */
function buildRow(page: Page, firstId: number, row: number): Promise<number[]> {
  return page.evaluate(async ({ variants, sword, armor, firstId, row }) => {
    type Glow = { meshes: unknown[] };
    type Char = {
      id: number; group: { parent: unknown }; holdWeapons: boolean; angle: number; model: { glows: Glow[] };
      attachments: { model: { glows: Glow[] } }[]; placeAt(x: number, y: number): void;
    };
    type G = { online: { hero: Char & { cell: { x: number; y: number }; group: { parent: { add(o: unknown): void } } }; characters: Map<number, Char> } };
    const g = (window as unknown as { __game: G }).__game;
    // window.__game.debug: the same in the dev build (modules) and the packaged bundle.
    const { createPlayerCharacter } = (window as unknown as { __game: { debug: {
      createPlayerCharacter(o: { id: number; name: string; bodySet: number; equipment: Record<string, unknown> }): Promise<Char>;
    } } }).__game.debug;
    const hero = g.online.hero;
    const counts: number[] = [];
    for (let i = 0; i < variants.length; i++) {
      const v = variants[i];
      const options = { glowLevel: v.level, excellent: v.excellent, ancient: v.ancient };
      const equipment: Record<string, unknown> = { rightHand: { ...sword, ...options } };
      for (const [slot, group] of [['helm', 7], ['armor', 8], ['pants', 9], ['gloves', 10], ['boots', 11]] as const) equipment[slot] = { group, number: armor, ...options };
      const c = await createPlayerCharacter({ id: firstId + i, name: v.label, bodySet: 2, equipment });
      c.holdWeapons = true;
      c.placeAt(hero.cell.x - 4 + i, hero.cell.y + 2 + row);
      c.angle = 0;
      hero.group.parent.add(c.group);
      g.online.characters.set(c.id, c);
      counts.push(c.model.glows.reduce((n, glow) => n + glow.meshes.length, 0) + c.attachments.reduce((n, a) => n + a.model.glows.reduce((m, glow) => m + glow.meshes.length, 0), 0));
    }
    return counts;
  }, { variants: VARIANTS, sword: SWORD, armor: ARMOR, firstId, row });
}

const materialCount = (page: Page) => page.evaluate(() => (window as unknown as { __game: { debug: { glowMaterialCount(): number } } }).__game.debug.glowMaterialCount());

test('items glow by level, excellent and ancient, worn and as icons', async ({ page }) => {
  test.setTimeout(10 * 60_000);
  mkdirSync(SHOTS_DIR, { recursive: true });
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400');
  await page.evaluate(() => (window as unknown as { __game: { online: { say(t: string): void } } }).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);

  const counts = await buildRow(page, 0x7e00, 0);
  // +0 has no glow; every other variant has passes (the level's chromes and metal, the excellent or ancient one).
  expect(counts[0]).toBe(0);
  for (let i = 1; i < counts.length; i++) expect(counts[i], VARIANTS[i].label).toBeGreaterThan(0);
  // +7 one pass, +9 two, +11 and up three per mesh.
  expect(counts[2]).toBeGreaterThan(counts[1]);
  expect(counts[3]).toBeGreaterThan(counts[2]);
  // The background warm-up (prewarmItemGlow) makes the cloak's material too: wait until the count settles.
  let materials = -1;
  await expect.poll(async () => { const before = materials; materials = await materialCount(page); await page.waitForTimeout(4_000); return before === materials && materials === await materialCount(page); }, { timeout: 120_000 }).toBe(true);
  await buildRow(page, 0x7e10, 1);
  expect(await materialCount(page), 'a second row of the same looks makes no new material').toBe(materials);

  // +15 weapons sparkle in the hand (SpawnNextGradeWeaponEffects).
  await expect.poll(() => page.evaluate(() => (window as unknown as { __game: { effects: { census(): Record<string, number> } } }).__game.effects.census()['sprite:flareRed'] ?? 0), { timeout: 20_000 }).toBeGreaterThan(0);
  await page.waitForTimeout(3_000); // textures in, a few frames drawn
  await page.screenshot({ path: `${SHOTS_DIR}/worn.png` });
  // The +15 armour wears its nine decorations (ForEachNextGradeObject); a closer look.
  const ornaments = await page.evaluate(() => {
    type Char = { attachments: { slot: string }[]; cell: { x: number; y: number }; group: { visible: boolean }; placeAt(x: number, y: number): void };
    const g = (window as unknown as { __game: { online: { characters: Map<number, Char>; hero: Char }; view: { distance: number } } }).__game;
    g.view.distance *= 0.45;
    const plus15 = g.online.characters.get(0x7e05)!;
    plus15.placeAt(g.online.hero.cell.x, g.online.hero.cell.y);
    g.online.hero.group.visible = false;
    return plus15.attachments.filter((a) => a.slot === 'ornament').length;
  });
  expect(ornaments).toBe(9);
  await page.waitForTimeout(2_000);
  await page.screenshot({ path: `${SHOTS_DIR}/worn-close.png` });
  await page.evaluate(() => {
    const g = (window as unknown as { __game: { view: { distance: number }; online: { hero: { group: { visible: boolean } } } } }).__game;
    g.view.distance /= 0.45;
    g.online.hero.group.visible = true;
  });

  // A debuff takes the +15's level glow away (its excellent / ancient glow stays); the cloak leaves a chrome shell.
  const state = await page.evaluate(() => {
    type Mesh = { visible: boolean };
    type Char = { setItemLooks(l: { debuffed: boolean; hardened: boolean; cloaked: boolean }): void; update(dt: number, w: unknown): void; model: { glows: { meshes: Mesh[] }[]; parts: { mesh: Mesh }[] } };
    const g = (window as unknown as { __game: { online: { characters: Map<number, Char> } } }).__game;
    const plus15 = g.online.characters.get(0x7e05)!;
    const plus13 = g.online.characters.get(0x7e04)!;
    plus15.setItemLooks({ debuffed: true, hardened: false, cloaked: false });
    plus13.setItemLooks({ debuffed: false, hardened: false, cloaked: true });
    return true;
  });
  expect(state).toBe(true);
  await page.waitForTimeout(1_500);
  const after = await page.evaluate(() => {
    type Mesh = { visible: boolean };
    type Char = { model: { glows: { meshes: Mesh[] }[]; parts: { mesh: Mesh }[]; group: { children: Mesh[] } } };
    const g = (window as unknown as { __game: { online: { characters: Map<number, Char> } } }).__game;
    const shown = (c: Char) => c.model.glows.flatMap((glow) => glow.meshes).filter((m) => m.visible).length;
    const cloaked = g.online.characters.get(0x7e04)!;
    return {
      debuffedGlow: shown(g.online.characters.get(0x7e05)!),
      cloakedBody: cloaked.model.parts.filter((p) => p.mesh.visible).length,
      cloakedShell: cloaked.model.group.children.filter((m) => m.visible).length,
    };
  });
  expect(after.debuffedGlow).toBe(0);
  expect(after.cloakedBody).toBe(0);
  expect(after.cloakedShell).toBeGreaterThan(0);
  await page.screenshot({ path: `${SHOTS_DIR}/debuff-cloak.png` });

  // The same items as window icons, and the Jewel of Harmony (shines below +3).
  const drawn = await page.evaluate(async ({ variants, sword }) => {
    const { drawItem } = (window as unknown as { __game: { debug: { drawItem(c: HTMLCanvasElement, item: unknown, o?: unknown): Promise<boolean> } } }).__game.debug;
    const strip = document.createElement('div');
    strip.id = 'glow-icons';
    Object.assign(strip.style, { position: 'fixed', left: '0', top: '0', zIndex: '99999', background: '#202020', display: 'flex', gap: '4px', padding: '4px' });
    document.body.append(strip);
    const items = [
      ...variants.map((v) => ({ ...sword, level: v.level, excellent: v.excellent ? 1 : 0, ancientSet: v.ancient ? 1 : 0 })),
      { group: 14, number: 42, level: 0, excellent: 0, ancientSet: 0 },
    ];
    const results: boolean[] = [];
    for (const item of items) {
      const canvas = document.createElement('canvas');
      canvas.width = 80; canvas.height = 160;
      strip.append(canvas);
      results.push(await drawItem(canvas, item, { time: 1 }));
    }
    return results;
  }, { variants: VARIANTS, sword: SWORD });
  expect(drawn.every(Boolean)).toBe(true);
  await page.locator('#glow-icons').screenshot({ path: `${SHOTS_DIR}/icons.png` });

  // The test players leave again.
  await page.evaluate(() => {
    type Char = { group: { parent: { remove(o: unknown): void } | null }; release(): void };
    const g = (window as unknown as { __game: { online: { characters: Map<number, Char> } } }).__game;
    for (const [id, c] of [...g.online.characters]) if (id >= 0x7e00 && id < 0x7e20) { c.group.parent?.remove(c.group); c.release(); g.online.characters.delete(id); }
  });
  expect(errors).toEqual([]);
});

// The moving parts of item looks (Data/Effects/RenderStyles.json): the Dark Reign Blade's textures scroll on the
// world's clock and the Divine Sword of the Archangel draws its reflection (stillShine) as a chrome pass.
test('item looks move: scrolling textures and reflection layers', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  mkdirSync(SHOTS_DIR, { recursive: true });
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400');
  await page.evaluate(() => (window as unknown as { __game: { online: { say(t: string): void } } }).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  type TextureMap = { offset: { x: number; y: number } } | null;
  type Mesh = { material: { map?: TextureMap; uniforms?: unknown } };
  type Char = { id: number; holdWeapons: boolean; angle: number; group: unknown; attachments: { slot: string; model: { glows: { meshes: Mesh[] }[] } }[]; placeAt(x: number, y: number): void; release(): void };
  const built = await page.evaluate(async () => {
    type G = { online: { hero: { cell: { x: number; y: number }; group: { parent: { add(o: unknown): void } } }; characters: Map<number, unknown> };
      debug: { createPlayerCharacter(o: { id: number; name: string; bodySet: number; equipment: Record<string, unknown> }): Promise<Char> } };
    const g = (window as unknown as { __game: G }).__game;
    const hero = g.online.hero;
    const out: { overlays: number; chrome: number }[] = [];
    for (const [i, number] of [21, 19].entries()) {
      const c = await g.debug.createPlayerCharacter({ id: 0x7e40 + i, name: `look${number}`, bodySet: 2, equipment: { rightHand: { group: 0, number, glowLevel: 0 } } });
      c.holdWeapons = true;
      c.placeAt(hero.cell.x - 1 + i * 2, hero.cell.y + 2);
      c.angle = 0;
      hero.group.parent.add(c.group);
      g.online.characters.set(c.id, c);
      const meshes = (c.attachments.find((a) => a.slot === 'rightHand')?.model.glows ?? []).flatMap((glow) => glow.meshes);
      out.push({ overlays: meshes.length, chrome: meshes.filter((m) => m.material.uniforms !== undefined).length });
    }
    return out;
  });
  expect(built[0].overlays, 'the Dark Reign Blade draws its scrolling layers over its meshes').toBeGreaterThan(0);
  expect(built[1].chrome, 'the Divine Sword draws its reflection as a chrome pass').toBeGreaterThan(0);
  const offsets = () => page.evaluate(() => {
    const g = (window as unknown as { __game: { online: { characters: Map<number, Char> } } }).__game;
    const blade = g.online.characters.get(0x7e40)!;
    return (blade.attachments.find((a) => a.slot === 'rightHand')?.model.glows ?? []).flatMap((glow) => glow.meshes)
      .map((m) => (m.material.map ? m.material.map.offset.x + m.material.map.offset.y : 0));
  });
  const first = await offsets();
  await page.waitForTimeout(1_500);
  const later = await offsets();
  expect(later.some((value, i) => Math.abs(value - first[i]) > 0.01), 'the textures scroll').toBe(true);
  await page.screenshot({ path: `${SHOTS_DIR}/looks-moving.png` });
  await page.evaluate(() => {
    const g = (window as unknown as { __game: { online: { characters: Map<number, Char & { group: { parent: { remove(o: unknown): void } | null } }> } } }).__game;
    for (const id of [0x7e40, 0x7e41]) { const c = g.online.characters.get(id); if (c) { c.group.parent?.remove(c.group); c.release(); g.online.characters.delete(id); } }
  });
  expect(errors).toEqual([]);
});
