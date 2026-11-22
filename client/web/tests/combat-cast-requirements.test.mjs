import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const { Combat } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/combat.js')).href);

test('Combat.cast refuses a skill without its requirements before selecting the monster target', () => {
  let targetChanged = 0;
  const connection = { onPacket: () => () => {} };
  const online = {
    hero: { id: 1, dead: false, cell: { x: 100, y: 100 } },
    world: { flags: new Uint16Array(256 * 256) },
  };
  const inventory = { items: new Map() };
  const combat = new Combat(connection, online, inventory, 3, {
    onTargetChanged: () => { targetChanged++; },
  });
  combat.skills = {
    current: () => 1,
    requirementsMet: (skill) => skill !== 1,
    canCast: () => true,
  };

  const monster = { id: 1000, dead: false, cell: { x: 101, y: 101 } };
  const accepted = combat.cast(monster, monster.cell, 1);

  assert.equal(accepted, false);
  assert.equal(combat.target, null);
  assert.equal(targetChanged, 0);
});
