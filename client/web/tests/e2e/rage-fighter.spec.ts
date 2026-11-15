// The Rage Fighter casts like on the desktop (AttackRagefighter / UseSkillRagefighter): each skill at a monster
// sends the desktop's requests (TargetedSkill, AreaSkill, the fighter attack C3 4A, Dark Side's C1 4B) and makes its
// effects; Dark Side dashes through the server's target list with its afterimages. testgm2's second character is a
// Rage Fighter that knows every skill (it rides a Fenrir, which the desktop doesn't allow for the close skills: the
// test first checks that refusal, then takes the horn off on the client side only and puts it back at the end).
import { test, expect, type Page } from '@playwright/test';

interface Fighter { id: number; kind: string; dead: boolean; cell: { x: number; y: number } }
interface RageGame {
  online: {
    characters: Map<number, Fighter>; playerId: number;
    hero: { cell: { x: number; y: number }; isWalking: boolean; isPlayingOnce: boolean; oneShot: { action: number } | null; dead: boolean; angle: number };
    walkTo(cell: { x: number; y: number }): boolean; say(text: string): void;
  };
  combat: { cast(target: unknown, cell: { x: number; y: number } | null, skill?: number): boolean; casting: boolean; cancel(): void };
  inventory: { items: Map<number, unknown> };
  effects: { census(): Record<string, number> };
  connection: { dispatch(bytes: Uint8Array): void };
  darkSide: () => { dash: { targetCount: number; dashedTargets: number; placements: unknown[]; clearDummies(): void } | null; ghosts: number; made: number };
}
type Win = Window & { __game: RageGame };

async function enterAsFighter(page: Page): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', 'testgm2');
  await page.fill('#password', 'testgm2');
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('#servers .choice')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await page.click('#characters .choice:has-text("testgm2Rf")');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 180_000 });
}

const nearestMonster = (page: Page) => page.evaluate(() => {
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

test('the Rage Fighter casts each skill at a monster as the desktop does', async ({ page }) => {
  test.setTimeout(12 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const sent: number[][] = [];
  const received: number[][] = [];
  page.on('websocket', (socket) => {
    socket.on('framesent', (frame) => { if (typeof frame.payload !== 'string') sent.push([...frame.payload.subarray(0, 16)]); });
    socket.on('framereceived', (frame) => { if (typeof frame.payload !== 'string') received.push([...frame.payload.subarray(0, 16)]); });
  });
  await enterAsFighter(page);
  await page.waitForFunction(() => (window as unknown as Win).__game.combat !== undefined, null, { timeout: 120_000 });
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 70 }, { x: 170, y: 58 }, { x: 185, y: 45 }, { x: 200, y: 35 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
    if (goal.y <= 70 && await nearestMonster(page)) break;
  }
  await expect.poll(() => nearestMonster(page), { timeout: 60_000 }).not.toBeNull();

  // IsRideNotUseSkill: with the Fenrir on, Killing Blow walks up but sends nothing.
  const fenrirMark = sent.length;
  const first = (await nearestMonster(page))!;
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.combat.cast(g.online.characters.get(id), null, 260); }, first.id);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.combat.casting), { timeout: 60_000 }).toBe(false);
  expect(sent.slice(fenrirMark).some((b) => b[0] === 0xc3 && b[2] === 0x19 && ((b[3] << 8) | b[4]) === 260)).toBe(false);
  const horn = await page.evaluate(() => { const g = (window as unknown as Win).__game; const item = g.inventory.items.get(8); g.inventory.items.delete(8); return item !== undefined; });

  const isTargeted = (b: number[], skill: number, id?: number) => b[0] === 0xc3 && b[2] === 0x19 && ((b[3] << 8) | b[4]) === skill && (id === undefined || ((b[5] << 8) | b[6]) === id);
  const isRage = (b: number[], skill: number) => b[0] === 0xc3 && b[2] === 0x4a && ((b[3] << 8) | b[4]) === skill;
  const isRange = (b: number[], skill: number) => b[0] === 0xc1 && b[2] === 0x4b && ((b[3] << 8) | b[4]) === skill;
  const isArea = (b: number[], skill: number) => b[0] === 0xc3 && b[2] === 0x1e && ((b[3] << 8) | b[4]) === skill;
  const isInstantMove = (b: number[]) => b[0] === 0xc1 && b[2] === 0x15;
  const playerId = await page.evaluate(() => (window as unknown as Win).__game.online.playerId);
  const cases: { skill: number; name: string; sends: (b: number[][], target: number) => boolean; effects: string[] }[] = [
    { skill: 260, name: 'Killing Blow', sends: (b, t) => b.some((x) => isTargeted(x, 260, t)) && b.some(isInstantMove), effects: ['effect:wolfHeadEffect'] },
    { skill: 261, name: 'Beast Uppercut', sends: (b, t) => b.some((x) => isRage(x, 261)) && b.filter((x) => isTargeted(x, 261, t)).length >= 2, effects: ['effect:downAttackDummyR'] },
    { skill: 262, name: 'Chain Drive', sends: (b, t) => b.some((x) => isRage(x, 262)) && b.filter((x) => isTargeted(x, 262, t)).length >= 2, effects: [] },
    { skill: 265, name: 'Dragon Slasher', sends: (b, t) => b.some((x) => isRage(x, 265)) && b.some((x) => isTargeted(x, 265, t)), effects: ['effect:dragonKickDummy'] },
    { skill: 264, name: 'Dragon Roar', sends: (b) => b.some((x) => isArea(x, 264)), effects: [] },
    { skill: 269, name: 'Charge', sends: (b, t) => b.some((x) => isTargeted(x, 269, t)), effects: [] },
    { skill: 266, name: 'Ignore Defense', sends: (b) => b.some((x) => isTargeted(x, 266, playerId)), effects: ['effect:windForce'] },
    { skill: 267, name: 'Increase Health', sends: (b) => b.some((x) => isTargeted(x, 267, playerId)), effects: ['effect:windForce'] },
    { skill: 268, name: 'Increase Block', sends: (b) => b.some((x) => isTargeted(x, 268, playerId)), effects: ['effect:windForce'] },
    { skill: 263, name: 'Dark Side', sends: (b) => b.some((x) => isRange(x, 263)) && b.some((x) => isRage(x, 263)), effects: ['effect:eventCloud'] },
  ];
  const results: string[] = [];
  for (const c of cases) {
    await expect.poll(() => page.evaluate(() => { const h = (window as unknown as Win).__game.online.hero; return !h.isPlayingOnce && !h.isWalking; }), { timeout: 30_000 }).toBe(true);
    const mark = sent.length;
    const census: Record<string, number>[] = [];
    const sampler = setInterval(() => { void page.evaluate(() => (window as unknown as Win).__game.effects.census()).then((x) => census.push(x)).catch(() => undefined); }, 150);
    // The nearest living monster (another player may kill it first: then the next one).
    let target = { id: 0, distance: 0 };
    let roam = 0;
    await expect(async () => {
      let nearest = await nearestMonster(page);
      if (!nearest) {
        // None left near: walk on to the next hunting spot.
        const spots = [{ x: 170, y: 58 }, { x: 185, y: 45 }, { x: 200, y: 35 }, { x: 160, y: 70 }, { x: 140, y: 60 }];
        await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), spots[roam++ % spots.length]);
        await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
        nearest = await nearestMonster(page);
      }
      expect(nearest).not.toBeNull();
      target = nearest!;
      const cast = await page.evaluate(([id, skill]) => { const g = (window as unknown as Win).__game; return g.combat.cast(g.online.characters.get(id), null, skill); }, [target.id, c.skill] as const);
      expect(cast, c.name).toBe(true);
    }).toPass({ timeout: 240_000 });
    await expect.poll(() => c.sends(sent.slice(mark), target.id), { timeout: 60_000, message: `${c.name} sends its requests` }).toBe(true);
    if (c.skill === 263) {
      // The server's target list (C1 4B, skill little endian at 4) and the dash with its afterimages.
      await expect.poll(() => received.some((b) => b[0] === 0xc1 && b[2] === 0x4b && (b[4] | (b[5] << 8)) === 263), { timeout: 30_000 }).toBe(true);
      await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.darkSide().dash?.targetCount ?? 0), { timeout: 10_000 }).toBeGreaterThan(0);
      await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.darkSide().ghosts), { timeout: 10_000, intervals: [50] }).toBeGreaterThan(0);
      await page.screenshot({ path: 'test-results/rage-fighter-dark-side.png' });
    }
    await page.waitForTimeout(1_500);
    clearInterval(sampler);
    for (const effect of c.effects) expect(census.some((x) => (x[effect] ?? 0) > 0), `${c.name} makes ${effect}`).toBe(true);
    console.log(`${c.name}: done`);
    results.push(`${c.name}: ${sent.slice(mark).map((b) => b.slice(0, 3).map((n) => n.toString(16)).join(' ')).join(', ')}`);
    await page.evaluate(() => (window as unknown as Win).__game.combat.cancel());
  }
  // Phoenix Shot: AttackRagefighter walks up, UseSkillRagefighter sends nothing.
  const target = (await nearestMonster(page))!;
  const mark = sent.length;
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.combat.cast(g.online.characters.get(id), null, 270); }, target.id);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.combat.casting), { timeout: 60_000 }).toBe(false);
  expect(sent.slice(mark).some((b) => (b[0] === 0xc3 || b[0] === 0xc1) && ((b[3] << 8) | b[4]) === 270)).toBe(false);
  console.log(results.join('\n'));
  if (horn) await page.reload(); // the horn comes back with the inventory the server sends
  expect(errors).toEqual([]);
});

test('Dark Side dashes through a fed target list with its afterimages', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterAsFighter(page);
  await page.waitForFunction(() => (window as unknown as Win).__game.combat !== undefined, null, { timeout: 120_000 });
  await expect.poll(() => page.evaluate(() => [...(window as unknown as Win).__game.online.characters.values()].filter((c) => c.kind !== 'player').length), { timeout: 60_000 }).toBeGreaterThan(0);
  // The ids of two monsters or NPCs near the hero, and the fed packets of a Dark Side from the hero at them.
  const feed = () => page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    const ids = [...g.online.characters.values()].filter((c) => c.kind !== 'player').slice(0, 2).map((c) => c.id);
    const hero = g.online.playerId;
    g.darkSide().dash?.clearDummies();
    // RageAttack (C3 4A): Dark Side (263) from the hero at the first; the fighter readies.
    g.connection.dispatch(new Uint8Array([0xc3, 9, 0x4a, 0x01, 0x07, hero >> 8, hero & 0xff, 0x80 | (ids[0] >> 8), ids[0] & 0xff]));
    // RageAttackRangeResponse (C1 4B): the skill and the targets, little endian, 10000 for none.
    const bytes = new Uint8Array(16);
    bytes.set([0xc1, 16, 0x4b, 0, 0x07, 0x01]);
    [...ids, 10000, 10000, 10000, 10000, 10000].slice(0, 5).forEach((id, i) => { bytes[6 + i * 2] = id & 0xff; bytes[7 + i * 2] = id >> 8; });
    g.connection.dispatch(bytes);
    // Headless Chromium draws a few frames a second: the readying is held at key 9 so the dash plays out.
    g.online.hero.oneShot = { action: 250, keysPerSecond: 0, hold: true };
    g.online.hero.time = 9;
  });
  await feed(); // the first Dark Side makes the copies of the fighter
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.darkSide().made), { timeout: 120_000 }).toBe(6);
  await page.evaluate(() => { (window as unknown as Win).__game.online.hero.oneShot = null; });
  await page.waitForTimeout(1_000);
  // Each effect tick is watched (the dash's state and copies move with it); the ticks stop once three copies show with
  // the dash's shock wave or strike flash made, for the picture.
  await page.evaluate(() => {
    type Ticking = { step(): void; __step?: () => void; __frozen?: boolean; __most?: number; __dashed?: number; __seen?: Record<string, number> };
    const g = (window as unknown as Win).__game;
    const e = g.effects as unknown as Ticking & { census(): Record<string, number> };
    e.__step ??= e.step;
    const step = e.__step;
    e.__most = 0; e.__dashed = 0; e.__seen = {}; e.__frozen = false;
    e.step = function (this: Ticking) {
      if (this.__frozen) return;
      step.call(this);
      const state = g.darkSide();
      this.__most = Math.max(this.__most ?? 0, state.ghosts);
      this.__dashed = Math.max(this.__dashed ?? 0, state.dash?.dashedTargets ?? 0);
      for (const [k, v] of Object.entries(e.census())) this.__seen![k] = Math.max(this.__seen![k] ?? 0, v);
      if (state.ghosts >= 3 && ((this.__seen!['effect:shockWave1'] ?? 0) > 0 || (this.__seen!['particle:damage2'] ?? 0) > 0)) this.__frozen = true;
    };
  });
  await feed();
  await page.waitForFunction(() => (window as unknown as { __game: { effects: { __frozen?: boolean } } }).__game.effects.__frozen === true, null, { timeout: 60_000 }).catch(() => undefined);
  const result = await page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    const e = g.effects as unknown as { __most: number; __dashed: number; __seen: Record<string, number> };
    return { targets: g.darkSide().dash?.targetCount ?? 0, most: e.__most, dashed: e.__dashed, census: e.__seen };
  });
  await page.screenshot({ path: 'test-results/rage-fighter-dark-side-fed.png' });
  await page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    const e = g.effects as unknown as { step(): void; __step?: () => void };
    if (e.__step) e.step = e.__step;
    g.online.hero.oneShot = null;
  });
  console.log(JSON.stringify(result));
  expect(result.targets).toBeGreaterThan(0);
  expect(result.most).toBeGreaterThan(1);
  expect(result.dashed).toBeGreaterThan(0);
  expect((result.census['effect:shockWave1'] ?? 0) + (result.census['particle:damage2'] ?? 0)).toBeGreaterThan(0);
  expect(errors).toEqual([]);
});
