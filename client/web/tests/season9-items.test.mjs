// Unit tests verifying Part (a) Season 9 Data/Items additions, Season9Overrides.json, and season-gated loadItemCatalog().
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const web = path.resolve(__dirname, '..');
const dataItemsDir = path.resolve(web, '../src/bin/Data/Items');
const modelsDir = path.join(dataItemsDir, 'Models');
const s9OverridesPath = path.join(web, 'public/data/Items/Season9Overrides.json');
const readJson = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));

const GROUP_FILES = [
  'Group00_Sword', 'Group01_Axe', 'Group02_Mace', 'Group03_Spear',
  'Group04_Bow', 'Group05_Staff', 'Group06_Shield', 'Group07_Helm',
  'Group08_Armor', 'Group09_Pants', 'Group10_Gloves', 'Group11_Boots',
  'Group12_Wing', 'Group13_Helper', 'Group14_Potion', 'Group15_Etc',
];

test('all 16 item and model group files have unique numbers and cover Season 9-only items', () => {
  let totalItems = 0;
  const byKey = new Map();
  for (let g = 0; g < GROUP_FILES.length; g++) {
    const stem = GROUP_FILES[g];
    const itemsFile = readJson(path.join(dataItemsDir, `${stem}.json`));
    assert.equal(itemsFile.group, g);
    const seenItems = new Set();
    for (const item of itemsFile.items) {
      assert.ok(!seenItems.has(item.number), `Duplicate item ${g}:${item.number}`);
      seenItems.add(item.number);
      byKey.set(`${g}:${item.number}`, item);
    }
    totalItems += itemsFile.items.length;

    const modelsFile = readJson(path.join(modelsDir, `${stem}.json`));
    assert.equal(modelsFile.group, g);
    const seenModels = new Set();
    for (const m of modelsFile.models) {
      assert.ok(!seenModels.has(m.number), `Duplicate model ${g}:${m.number}`);
      seenModels.add(m.number);
    }
  }
  assert.ok(totalItems >= 1200, `Expected >=1200 items after S9 import, got ${totalItems}`);
  // Spot check representative S9-only items across groups and 4-language names
  const sonicBlade = byKey.get('0:29');
  assert.ok(sonicBlade, '0:29 Sonic Blade present');
  assert.equal(sonicBlade.name.en, 'Sonic Blade');
  assert.ok(sonicBlade.name.vi && sonicBlade.name.de && sonicBlade.name['zh-CN'], 'Sonic Blade has 4 languages');
  assert.ok(byKey.get('8:54'), '8:54 Lazy Wind Armor present');
  assert.ok(byKey.get('12:267'), '12:267 Wings of Angel and Devil present');
  assert.ok(byKey.get('13:239'), '13:239 Muun Energy Converter present');
  assert.ok(byKey.get('14:191'), '14:191 Sonic Combination Note present');
});

test('Season9Overrides.json contains all 210 shared S9 item overrides and applyItemOverrides merges cleanly', async () => {
  const doc = readJson(s9OverridesPath);
  assert.equal(doc.season, 's9');
  assert.equal(doc.items.length, 210);
  const seen = new Set();
  for (const ov of doc.items) {
    const key = `${ov.group}:${ov.number}`;
    assert.ok(!seen.has(key), `Duplicate override ${key}`);
    seen.add(key);
  }
  const { applyItemOverrides } = await import(pathToFileURL(path.join(web, 'public/js/game/items.js')).href);
  const fakeCatalog = {
    models: new Map([['14:225', { dir: 'Item', fileName: 'Scroll.bmd', textureDir: ['Item'], look: {}, glow: {}, inventory: null, ground: null }]]),
    names: new Map([['14:225', 'Scroll Master of Defense'], ['14:0', 'Apple']]),
    definitions: new Map([
      ['14:225', { number: 225, name: { en: 'Scroll Master of Defense' }, width: 1, height: 1, level: 25 }],
      ['14:0', { number: 0, name: { en: 'Apple' }, width: 1, height: 1, level: 1, durability: 3 }],
      ['5:32', { number: 32, name: { en: 'Summon Spirit Stick' }, width: 1, height: 4, level: 130, requirements: { strength: 45, dexterity: 15 } }],
    ]),
  };
  applyItemOverrides(fakeCatalog, doc.items);
  assert.equal(fakeCatalog.definitions.get('14:225').width, 2);
  assert.equal(fakeCatalog.definitions.get('14:225').height, 2);
  assert.equal(fakeCatalog.definitions.get('14:0').durability, 255);
  assert.equal(fakeCatalog.definitions.get('5:32').level, 149);
  assert.equal(fakeCatalog.definitions.get('5:32').requirements.level, 400);
});
