// Unit tests of the web's Season 9 monster list (public/data/Monsters/Season9.json), the map check of 2026-11-20:
// Season 9's monsters and NPCs had no entry and were all drawn as the unknown monsters' ghost. Each number now has an
// entry and a model of its own, from Season 9's files. With HNMO_S9_DATA_DIR (a Season 9 data pack, e.g.
// C:\hnmo-work\s9-pack) every model file must be in the pack, open, and find each texture it names.
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const web = path.resolve(__dirname, '..');
const baseData = path.resolve(web, '../src/bin/Data');
const readJson = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));
const season9 = readJson(path.join(web, 'public/data/Monsters/Season9.json'));
const monsters = readJson(path.join(baseData, 'Monsters/Monsters.json'));
const models = readJson(path.join(baseData, 'Monsters/Models.json'));

const numbers = season9.monsters.flatMap((monster) => monster.numbers.map((own) => own.number));
const slots = new Map(season9.models.map((model) => [model.slot, model]));

test('the Season 9 numbers and slots are new: the game\'s own data is never replaced', () => {
  const known = new Set(monsters.monsters.flatMap((monster) => monster.numbers.map((own) => own.number)));
  assert.deepEqual(numbers.filter((n) => known.has(n)), []);
  assert.equal(new Set(numbers).size, numbers.length, 'a number twice');
  const knownSlots = new Set(models.models.map((model) => model.slot));
  assert.deepEqual([...slots.keys()].filter((slot) => knownSlots.has(slot)), []);
  assert.equal(slots.size, season9.models.length, 'a slot twice');
});

test('the field monsters of Nars, Ferea and Uruk Mountain and the Season 9 NPCs have an entry', () => {
  const known = new Set(monsters.monsters.flatMap((monster) => monster.numbers.map((own) => own.number)));
  for (const n of [616, 617]) {
    assert.ok(known.has(n), `${n} has no base entry`);
  }
  // The numbers the Season 9 server suite's spawn list places that the base game lacks, except the five whose
  // models are not known yet (580 Captain Slough, 581 Deruvish, 584 Sir Lesnar, 643 Mait, 689 Flame Trap).
  for (const n of [582, 586, 587, 588, 589, 590, 591, 592, 593, 594, 595, 596, 608, 609, 610, 611, 612, 613, 614, 615, 651, 669, 670, 671, 672, 673, 682]) {
    assert.ok(numbers.includes(n), `${n} has no entry`);
  }
});

test('every entry names a model of the list, of its kind, from Season 9\'s files', () => {
  for (const monster of season9.monsters) {
    const model = slots.get(monster.model);
    assert.ok(model, `${monster.name.en}: no model ${monster.model}`);
    assert.match(monster.model, monster.kind === 'npc' ? /^MODEL_S9_/ : /^MONSTER_MODEL_S9_/, monster.name.en);
    assert.ok(monster.name.en, 'an English name');
    assert.match(model.file, /^Data\/s9\/(Monster|NPC)\/[^/]+\.bmd$/i, model.slot);
    assert.deepEqual(model.textureFolders, [path.posix.dirname(model.file.slice('Data/'.length))], model.slot);
  }
});

const packDir = process.env.HNMO_S9_DATA_DIR;
test('with a Season 9 pack: each model opens and finds its textures', { skip: !packDir && 'HNMO_S9_DATA_DIR is not set' }, async () => {
  const { parseBMD } = await import(pathToFileURL(path.join(web, 'public/js/formats.js')).href);
  const listing = new Map();
  const find = (root, folder, name) => {
    const key = path.join(root, folder);
    if (!listing.has(key)) listing.set(key, fs.existsSync(key) ? new Set(fs.readdirSync(key).map((f) => f.toLowerCase())) : new Set());
    return listing.get(key).has(name.toLowerCase());
  };
  for (const model of season9.models) {
    const relative = model.file.slice('Data/s9/'.length);
    const [folder, fileName] = [path.posix.dirname(relative), path.posix.basename(relative)];
    assert.ok(find(packDir, folder, fileName), `${model.slot}: ${relative} is not in the pack`);
    const actual = fs.readdirSync(path.join(packDir, folder)).find((f) => f.toLowerCase() === fileName.toLowerCase());
    const bmd = parseBMD(new Uint8Array(fs.readFileSync(path.join(packDir, folder, actual))));
    assert.ok(bmd.meshes.length > 0 && bmd.actions.length > 0, `${model.slot}: no meshes or actions`);
    for (const mesh of bmd.meshes) {
      if (!mesh.textureName) continue;
      const stem = mesh.textureName.replace(/\.[^.]+$/, '');
      const names = [mesh.textureName, `${stem}.OZJ`, `${stem}.OZT`, `${stem}.jpg`, `${stem}.tga`];
      const found = names.some((name) => find(packDir, folder, name) || find(baseData, folder, name));
      assert.ok(found, `${model.slot}: texture ${mesh.textureName} not found in ${folder}`);
    }
  }
});
