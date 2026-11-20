// Unit tests of the players' moving speed (src/game/combatLook.ts playerMoveSpeed, the desktop's CharacterMoveSpeed):
// the web game walked everyone at the walking speed, so a winged or running character fell behind the server, which
// times a walk by the same speeds, and got speed-check warnings. Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const { playerMoveSpeed, isRunning } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/combatLook.js')).href);

const base = { classIndex: 0, mount: 'none', wings: null, bootsLevel: 0, glovesLevel: 0, safeZone: false, mapNumber: 0, walkedSeconds: 0 };

test('a walk, a run after 40 frames for the runners, everyone walks in a safe zone', () => {
  assert.equal(playerMoveSpeed(base), 12, 'a Dark Wizard walks');
  assert.equal(playerMoveSpeed({ ...base, walkedSeconds: 5 }), 12, 'and keeps walking');
  const knight = { ...base, classIndex: 1 };
  assert.equal(playerMoveSpeed(knight), 12, 'a Dark Knight starts walking');
  assert.equal(playerMoveSpeed({ ...knight, walkedSeconds: 1.6 }), 15, 'and runs after 40 frames');
  assert.equal(isRunning({ ...knight, walkedSeconds: 1.6 }), true);
  assert.equal(playerMoveSpeed({ ...base, bootsLevel: 5, walkedSeconds: 2 }), 15, '+5 boots');
  assert.equal(playerMoveSpeed({ ...base, bootsLevel: 5, mapNumber: 7, walkedSeconds: 2 }), 12, 'boots don\'t count in Atlans');
  assert.equal(playerMoveSpeed({ ...base, glovesLevel: 5, mapNumber: 7, walkedSeconds: 2 }), 15, 'gloves do');
  assert.equal(playerMoveSpeed({ ...knight, safeZone: true, walkedSeconds: 3 }), 12, 'a safe zone');
});

test('wings, mounts and Chaos Castle', () => {
  assert.equal(playerMoveSpeed({ ...base, wings: 1 }), 15, 'wings');
  assert.equal(playerMoveSpeed({ ...base, wings: 5 }), 16, 'Wings of Dragon');
  assert.equal(playerMoveSpeed({ ...base, wings: 36 }), 16, 'Wing of Storm');
  assert.equal(playerMoveSpeed({ ...base, wings: 1, safeZone: true }), 12, 'wings in town');
  assert.equal(playerMoveSpeed({ ...base, mount: 'uniria' }), 15);
  assert.equal(playerMoveSpeed({ ...base, mount: 'darkHorse' }), 17);
  assert.equal(playerMoveSpeed({ ...base, mount: 'fenrir' }), 15, 'a Fenrir builds up');
  assert.equal(playerMoveSpeed({ ...base, mount: 'fenrir', walkedSeconds: 0.5 }), 16);
  assert.equal(playerMoveSpeed({ ...base, mount: 'fenrir', walkedSeconds: 1 }), 17);
  assert.equal(playerMoveSpeed({ ...base, mount: 'fenrir', fenrirExcellent: true, walkedSeconds: 1 }), 19);
  assert.equal(playerMoveSpeed({ ...base, mapNumber: 18 }), 15, 'Chaos Castle');
});
