import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'public', 'data', 'Season9');

test('Season 9 minimap.json covers 31 maps including Acheron, Debenter, and Uruk Mountain', () => {
  const data = JSON.parse(fs.readFileSync(path.join(root, 'minimap.json'), 'utf8'));
  const mapIds = Object.keys(data).map(Number);
  assert.ok(mapIds.length >= 30, `expected >= 30 maps, got ${mapIds.length}`);
  assert.ok(data['91']?.length >= 10, 'Acheron minimap markers present');
  assert.ok(data['100']?.length >= 5, 'Uruk Mountain minimap markers present');
});

test('Season 9 master-tree.json exports 480 suite nodes and 330 max master level', () => {
  const tree = JSON.parse(fs.readFileSync(path.join(root, 'master-tree.json'), 'utf8'));
  assert.equal(tree.maxMasterLevel, 330);
  assert.equal(tree.nodes.length, 480);
});

test('Season 9 s9-skills.json defines skills 618-621', () => {
  const skills = JSON.parse(fs.readFileSync(path.join(root, 's9-skills.json'), 'utf8'));
  assert.deepEqual(skills.map((s) => s.number), [618, 619, 620, 621]);
  assert.equal(skills[0].name, 'Spell of Protection');
  assert.equal(skills[3].name, 'Shield-Burn');
});
