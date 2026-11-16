import { test, expect } from '@playwright/test';
import { enterWorld } from './helpers.js';
import type { MovementSummary } from '../../src/game/movementRecorder.js';

interface DebugWin {
  __game: {
    mapNumber: number;
    connection: { dispatch(bytes: Uint8Array): void };
    online: {
      hero: {
        cell: { x: number; y: number };
        position: { x: number; y: number };
        angle: number;
        action: number;
        isWalking: boolean;
        walk(cells: { x: number; y: number }[]): void;
        update(dt: number): void;
      };
      characters: Map<number, {
        id: number;
        name: string;
        kind: string;
        cell: { x: number; y: number };
        position: { x: number; y: number };
        angle: number;
        action: number;
        isWalking: boolean;
      }>;
      walkTo(cell: { x: number; y: number }): boolean;
      say(text: string): void;
    };
    world: { flags: Uint8Array; heights: Float32Array; light: Float32Array; mapNumber?: number };
    debug: {
      movementRecorder: {
        start(): void;
        stop(): MovementSummary;
      };
    };
  };
}

test('measure movement in Lorencia and Devias', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  await enterWorld(page, 'test8');
  await page.evaluate(() => (window as unknown as DebugWin).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as DebugWin).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  await page.waitForTimeout(2000);

  await page.evaluate(() => (window as unknown as DebugWin).__game.debug.movementRecorder.start());

  // Walk around Lorencia town and out to the monster area (including mid-walk click redirects).
  const lorenciaWaypoints = [
    { x: 140, y: 128, waitMs: 1200 },
    { x: 148, y: 118, waitMs: 800 }, // redirect mid-walk
    { x: 155, y: 100, waitMs: 2500 },
    { x: 162, y: 85, waitMs: 800 },  // redirect mid-walk
    { x: 168, y: 72, waitMs: 3500 },
    { x: 175, y: 60, waitMs: 4000 },
    { x: 165, y: 65, waitMs: 800 },  // redirect mid-walk
    { x: 182, y: 52, waitMs: 4000 },
    { x: 170, y: 58, waitMs: 4000 },
  ];
  for (const wp of lorenciaWaypoints) {
    await page.evaluate((cell) => (window as unknown as DebugWin).__game.online.walkTo(cell), { x: wp.x, y: wp.y });
    await page.waitForTimeout(wp.waitMs);
  }

  // Warp to Devias and walk out to the monster hunting area.
  await page.evaluate(() => (window as unknown as DebugWin).__game.online.say('/move devias'));
  await expect.poll(() => page.evaluate(() => (window as unknown as DebugWin).__game.mapNumber), { timeout: 60_000 }).toBe(2);
  await page.waitForTimeout(1500);

  const deviasWaypoints = [
    { x: 215, y: 45, waitMs: 1500 },
    { x: 218, y: 58, waitMs: 800 },  // redirect mid-walk
    { x: 222, y: 72, waitMs: 3500 },
    { x: 215, y: 80, waitMs: 800 },  // redirect mid-walk
    { x: 210, y: 88, waitMs: 4000 },
    { x: 200, y: 82, waitMs: 4000 },
  ];
  for (const wp of deviasWaypoints) {
    await page.evaluate((cell) => (window as unknown as DebugWin).__game.online.walkTo(cell), { x: wp.x, y: wp.y });
    await page.waitForTimeout(wp.waitMs);
  }

  const summary = await page.evaluate(() => (window as unknown as DebugWin).__game.debug.movementRecorder.stop());
  console.log('MOVEMENT_SUMMARY_AFTER:', JSON.stringify({
    frames: summary.frames,
    durationSeconds: summary.durationSeconds,
    angleSnapsOver20Deg: summary.angleSnapsOver20Deg,
    stopWalkFlips: summary.stopWalkFlips,
    positionJumps: summary.positionJumps,
    perMinute: summary.perMinute,
    sampleAngleSnaps: summary.events.angleSnaps.slice(0, 8),
    sampleStopWalkFlips: summary.events.stopWalkFlips.slice(0, 8),
    samplePositionJumps: summary.events.positionJumps.slice(0, 8),
  }, null, 2));

  expect(summary.frames).toBeGreaterThan(100);
  // Zero backward-reversal jumps on mid-walk redirects.
  expect(summary.events.positionJumps.filter((j) => j.reversal)).toEqual([]);

  // Return to Lorencia for clean state.
  await page.evaluate(() => (window as unknown as DebugWin).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as DebugWin).__game.mapNumber), { timeout: 60_000 }).toBe(0);
});

test('fake move packets verify gradual 45-deg turn, no stop frame between steps, and no jump on new path', async ({ page }) => {
  await enterWorld(page, 'test8');

  // 1. Gradual 45-degree turn: hero walking straight (north) then 45 deg diagonal takes > 1 frame
  //    and never snaps > 20 degrees in a single frame.
  const turnResult = await page.evaluate(() => {
    const g = (window as unknown as DebugWin).__game;
    const hero = g.online.hero as unknown as {
      cell: { x: number; y: number };
      position: { x: number; y: number };
      angle: number;
      placeAt(x: number, y: number): void;
      walk(cells: { x: number; y: number }[]): void;
      update(dt: number, world: unknown): void;
    };
    hero.placeAt(130, 130);
    hero.angle = 0; // facing -Y (north)
    hero.walk([{ x: 131, y: 129 }]); // 45-deg NE step
    const deltasDeg: number[] = [];
    let prevAngle = hero.angle;
    for (let i = 0; i < 12; i++) {
      hero.update(1 / 60, g.world);
      let d = (hero.angle - prevAngle) % (Math.PI * 2);
      if (d > Math.PI) d -= Math.PI * 2;
      else if (d < -Math.PI) d += Math.PI * 2;
      deltasDeg.push(Math.abs(d * 180 / Math.PI));
      prevAngle = hero.angle;
    }
    return {
      maxSingleFrameDeg: Math.max(...deltasDeg),
      framesTurning: deltasDeg.filter((d) => d > 0.5).length,
      finalAngleDeg: (hero.angle * 180 / Math.PI),
    };
  });
  expect(turnResult.framesTurning).toBeGreaterThan(1);
  expect(turnResult.maxSingleFrameDeg).toBeLessThan(20);
  expect(turnResult.finalAngleDeg).toBeGreaterThan(35);

  // 2. No position jump / backward reversal when a new path arrives mid-step.
  const noJumpResult = await page.evaluate(() => {
    const g = (window as unknown as DebugWin).__game;
    const hero = g.online.hero as unknown as {
      position: { x: number; y: number };
      placeAt(x: number, y: number): void;
      walk(cells: { x: number; y: number }[]): void;
      update(dt: number, world: unknown): void;
    };
    hero.placeAt(130, 130);
    hero.walk([{ x: 131, y: 130 }, { x: 132, y: 130 }]);
    // Advance ~40% into the first cell (130 -> 131).
    for (let i = 0; i < 8; i++) hero.update(1 / 60, g.world);
    const beforeX = hero.position.x;
    // New path starts at (130, 130) -> (131, 130) -> (132, 130) while hero is already past 130.
    hero.walk([{ x: 130, y: 130 }, { x: 131, y: 130 }, { x: 132, y: 130 }]);
    hero.update(1 / 60, g.world);
    const afterX = hero.position.x;
    return { beforeX, afterX, movedForward: afterX > beforeX, jumpDistance: Math.abs(afterX - beforeX) };
  });
  expect(noJumpResult.movedForward).toBe(true);
  expect(noJumpResult.jumpDistance).toBeLessThan(20);

  // 3. Consecutive ObjectWalkedExtended packets to a character keep walk animation with 0 stop frames.
  await page.evaluate(() => (window as unknown as DebugWin).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as DebugWin).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  await expect.poll(() => page.evaluate(() => (window as unknown as DebugWin).__game.online.characters.size), { timeout: 60_000 }).toBeGreaterThan(0);
  const npcId = await page.evaluate(() => {
    const g = (window as unknown as DebugWin).__game;
    const first = g.online.characters.values().next().value;
    return first ? first.id : null;
  });
  expect(npcId).not.toBeNull();
  const stopWalkResult = await page.evaluate((id: number) => {
    const g = (window as unknown as DebugWin).__game;
    const npc = g.online.characters.get(id)! as unknown as {
      cell: { x: number; y: number };
      action: number;
      actions: { stop: number; walk: number };
      update(dt: number, world: unknown): void;
    };
    const sendWalk = (sx: number, sy: number, tx: number, ty: number) => {
      const bytes = new Uint8Array([0xc1, 10, 0xd4, (id >> 8) & 0xff, id & 0xff, sx, sy, tx, ty, 0]);
      g.connection.dispatch(bytes);
    };
    const startX = npc.cell.x, startY = npc.cell.y;
    sendWalk(startX, startY, startX + 1, startY);
    const actions: number[] = [];
    for (let i = 0; i < 20; i++) {
      npc.update(1 / 60, g.world);
      actions.push(npc.action);
    }
    // Wait ~4 frames after finishing the 1-cell walk before next packet arrives.
    for (let i = 0; i < 4; i++) {
      npc.update(1 / 60, g.world);
      actions.push(npc.action);
    }
    sendWalk(startX + 1, startY, startX + 2, startY);
    for (let i = 0; i < 10; i++) {
      npc.update(1 / 60, g.world);
      actions.push(npc.action);
    }
    return {
      walkAction: npc.actions.walk,
      stopAction: npc.actions.stop,
      sawStopBetweenPackets: actions.slice(1).includes(npc.actions.stop),
    };
  }, npcId!);
  expect(stopWalkResult.sawStopBetweenPackets).toBe(false);
});
