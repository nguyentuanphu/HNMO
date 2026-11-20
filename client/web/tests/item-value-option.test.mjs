// Unit tests of the additional option's share of an item's price in itemValue.ts: up to +16 (option level 4) as
// classic MU, and above it, now that the Jewel of Life goes up to +28 (level 7), HNMO's server formula
// (ItemPriceCalculator.ApplyOptionModifiers):
//   level 1:      price + (long)(price * 0.6)
//   level n >= 2: price + (long)(price * 0.7 * 2^(n - 1))
// Run: node --test client/web/tests/*.test.mjs (after npm run build). The data comes from client/src/bin/Data, as
// the bridge serves it under /data.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDirectory = path.resolve(__dirname, '../../src/bin/Data');

globalThis.fetch = async (url) => {
  const match = /^\/data\/(.+)$/.exec(String(url));
  if (!match) return { ok: false, status: 404, json: async () => null };
  try {
    const text = await readFile(path.join(dataDirectory, match[1]), 'utf8');
    return { ok: true, status: 200, json: async () => JSON.parse(text.replace(/^﻿/, '')) };
  } catch {
    return { ok: false, status: 404, json: async () => null };
  }
};

const { addAdditionalOptionPrice, itemValue, loadItemValueData } =
  await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/itemValue.js')).href);

// The server's formula, for the expected numbers.
const serverPrice = (price, level) => level === 0 ? price
  : level === 1 ? price + Math.trunc(price * 0.6)
  : price + Math.trunc(price * 0.7 * 2 ** (level - 1));

test('the additional option adds what the server adds at every option level up to 7', () => {
  // 1,000,000 zen: +60 %, +140 %, +280 %, +560 %, then +1120 %, +2240 %, +4480 % (0.7 * 16, 32, 64).
  assert.deepEqual([0, 1, 2, 3, 4, 5, 6, 7].map((level) => addAdditionalOptionPrice(1000000, level)),
    [1000000, 1600000, 2400000, 3800000, 6600000, 12200000, 23400000, 45800000]);
  for (const price of [7654321, 123457, 245]) {
    for (let level = 0; level <= 7; level++) {
      assert.equal(addAdditionalOptionPrice(price, level), serverPrice(price, level), `${price} at level ${level}`);
    }
  }
});

const item = (group, number, level, optionLevel) => ({
  group, number, level, durability: 255, luck: false, skill: false, guardian: false, optionLevel, optionType: 0,
  excellent: 0, ancientSet: 0, ancientBonus: 0, harmony: 0, sockets: [], socketBonus: 0, length: 0, width: 1, height: 1,
});

// The shop's price (rounded to hundreds) of a +15 Kris (0/0, +4 damage a level) and Small Shield (6/0, +5 defense
// rate a level), one-handed, so 80 % of 100 + (40 + L) * L * L / 8 with L = the drop level + 15 * 3 + 365:
// Kris 7,891,433 zen, Small Shield 7,726,855 zen; then the server's formula at the option levels 0 to 7.
test('a weapon and a shield cost more at +20, +24 and +28 as the server prices them', async () => {
  await loadItemValueData();
  const expected = {
    kris: [7891400, 12626200, 18939400, 29987400, 52083400, 96275400, 184659500, 361427600],
    smallShield: [7726800, 12362900, 18544400, 29362000, 50997200, 94267600, 180808400, 353889900],
  };
  for (let level = 0; level <= 7; level++) {
    assert.equal(itemValue(item(0, 0, 15, level), 0), expected.kris[level], `Kris, option level ${level}`);
    assert.equal(itemValue(item(6, 0, 15, level), 0), expected.smallShield[level], `Small Shield, option level ${level}`);
  }
});
