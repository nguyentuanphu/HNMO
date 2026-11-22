import test from 'node:test';
import assert from 'node:assert/strict';
import { parseServerGuide } from '../public/js/websiteServerGuideModels.js';

function reply() {
  return { updatedAt: '2026-11-22T00:00:00Z', clientRules: { en: '', vi: '' }, notForSale: { en: '', vi: '' }, servers: [{ id: 7, facts: {
    maximumInventoryMoney: 100, maximumVaultMoney: 200, classes: [], helper: null, inventory: null, vault: null,
    partyExperience: [{ size: 2, percent: 160, differentClassesPercent: 0 }], vip: [], catalog: { products: null, truncated: false },
  } }] };
}

test('server guide keeps actual zero/off values without adding sample VIP or storage offers', () => {
  const value = reply(); value.servers[0].facts.maximumInventoryMoney = 0;
  const parsed = parseServerGuide(value);
  assert.equal(parsed.servers[0].facts.maximumInventoryMoney, 0);
  assert.equal(parsed.servers[0].facts.helper, null);
  assert.deepEqual(parsed.servers[0].facts.vip, []);
  assert.equal(parsed.servers[0].facts.catalog.products, null);
});

test('incompatible, non-finite and oversized snapshots are rejected instead of inventing defaults', () => {
  assert.throws(() => parseServerGuide({}));
  for (const bad of [NaN, Infinity, '100', null]) {
    const value = reply(); value.servers[0].facts.maximumInventoryMoney = bad;
    assert.throws(() => parseServerGuide(value));
  }
  const value = reply(); value.servers = Array.from({ length: 33 }, () => value.servers[0]);
  assert.throws(() => parseServerGuide(value));
});

test('only the selected public model survives parsing and untrusted policy remains plain text', () => {
  const value = reply(); value.adminPath = 'not-public'; value.clientRules.en = '<img src=x onerror=alert(1)>';
  const parsed = parseServerGuide(value);
  assert.equal(Object.hasOwn(parsed, 'adminPath'), false);
  assert.equal(parsed.clientRules.en, '<img src=x onerror=alert(1)>');
  value.updatedAt = 'not-a-time'; assert.throws(() => parseServerGuide(value));
});


test('the complete byte-sized party setting is supported without accepting unbounded arrays', () => {
  const value = reply(); value.servers[0].facts.partyExperience = Array.from({ length: 254 }, (_, index) => ({ size: index + 2, percent: 220, differentClassesPercent: 300 }));
  assert.equal(parseServerGuide(value).servers[0].facts.partyExperience.length, 254);
  value.servers[0].facts.partyExperience.push({}, {});
  assert.throws(() => parseServerGuide(value));
});

test('extra system metadata stays allowlisted and absent extras remain compatible', () => {
  const value = reply();
  value.servers[0].extras = { systems: { antiCheatEnabled: false, activityLogEnabled: true, onlyValuableDrops: true, fileName: 'private-state' }, quests: [], questsTruncated: false, crafts: [], craftsTruncated: false };
  const parsed = parseServerGuide(value);
  assert.equal(parsed.servers[0].extras.systems.onlyValuableDrops, true);
  assert.equal(JSON.stringify(parsed).includes('private-state'), false);
});
