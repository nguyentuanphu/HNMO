// Regression tests for HeroState / outlaw warp gating and extended bag free-cell counting.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const statsMod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/stats.js')).href);
const invMod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/inventory.js')).href);
const warpsMod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/warps.js')).href);

test('CharacterStats tracks initial HeroState and HeroStateChanged packet for self and maps outlaw >= 5', () => {
  assert.equal(typeof statsMod.isOutlawHeroState, 'function', 'isOutlawHeroState must be exported');

  let packetListener = null;
  const fakeConnection = {
    onPacket(fn) {
      packetListener = fn;
      return () => {};
    },
  };
  let selfId = 0x1234;
  const stats = new statsMod.CharacterStats(fakeConnection, 200, { HeroState: 3 }, () => selfId);
  assert.equal(stats.heroState, 3);
  assert.equal(statsMod.isOutlawHeroState(stats.heroState), false);

  // Another player turning PK2 must not mark self as outlaw.
  packetListener(new Uint8Array([0xc1, 7, 0xf3, 0x08, 0x00, 0x09, 6]));
  assert.equal(stats.heroState, 3);

  // Self turning PlayerKiller1stStage (5, with masked new-object bit 0x80) updates heroState and blocks warp.
  packetListener(new Uint8Array([0xc1, 7, 0xf3, 0x08, 0x92, 0x34, 5]));
  assert.equal(stats.heroState, 5);
  assert.equal(statsMod.isOutlawHeroState(stats.heroState), true);

  const warp = { index: 1, mapName: 'Lorencia', subName: '', level: 10, maxLevel: 400, zen: 2000, gate: 17 };
  assert.equal(
    warpsMod.canWarp(warp, {
      classIndex: 1,
      level: 200,
      money: 1_000_000,
      outlaw: statsMod.isOutlawHeroState(stats.heroState),
      wings: undefined,
      helper: undefined,
      rings: [undefined, undefined],
    }),
    false,
  );
});

test('Inventory.countFreeBagCells counts base bag plus unlocked inventory extensions', () => {
  const fakeConnection = { onPacket: () => () => {} };
  const inv = new invMod.Inventory(fakeConnection);
  assert.equal(typeof inv.countFreeBagCells, 'function', 'Inventory.countFreeBagCells must be defined');

  // Fill every cell in the base 8x8 bag (slots 12..75).
  for (let slot = 12; slot < 76; slot++) {
    inv.items.set(slot, {
      group: 14, number: 0, level: 0, durability: 1, luck: false, skill: false, guardian: false,
      optionLevel: 0, optionType: 0, excellent: 0, ancientSet: 0, ancientBonus: 0, harmony: 0,
      sockets: [], socketBonus: 0, length: 0, width: 1, height: 1, raw: new Uint8Array(),
    });
  }

  assert.equal(inv.countFreeBagCells(0), 0);
  assert.equal(inv.countFreeBagCells(1), 32);

  // Occupy 2 cells in extension page 1 (slots 76..77) -> 30 free cells remain.
  inv.items.set(76, inv.items.get(12));
  inv.items.set(77, inv.items.get(12));
  assert.equal(inv.countFreeBagCells(1), 30);
});
