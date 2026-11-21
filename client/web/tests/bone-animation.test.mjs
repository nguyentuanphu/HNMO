// Unit tests of the bone animation in bmd.ts (computeBoneMatrices, loopKeyCount) with a small made-up model:
// a locked action (LockPositions, the walks and runs) is drawn in place and loops over one key less, as the
// desktop's ZzzBMD.cpp does. Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const { computeBoneMatrices, loopKeyCount } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/bmd.js')).href);

// Root bone 0 and a child bone 1, 4 keys in two actions: 0 is free, 1 is locked. The root walks 100 units
// forward per key and bobs up by 10; the child sits 50 above the root.
function model() {
  const keys = 4;
  const rootPositions = new Float32Array(keys * 3);
  for (let k = 0; k < keys; k++) rootPositions.set([k * 100, 5, k * 10], k * 3);
  const childPositions = new Float32Array(keys * 3);
  for (let k = 0; k < keys; k++) childPositions.set([0, 0, 50], k * 3);
  const still = new Float32Array(keys * 3);
  const frames = (positions) => [{ positions, rotations: still }, { positions, rotations: still }];
  return {
    name: 'made-up',
    meshes: [],
    actions: [{ numKeys: keys, lockPositions: false }, { numKeys: keys, lockPositions: true }],
    bones: [
      { dummy: false, name: 'root', parent: -1, frames: frames(rootPositions) },
      { dummy: false, name: 'child', parent: 0, frames: frames(childPositions) },
    ],
  };
}

const position = (matrix) => ({ x: Math.round(matrix.elements[12]), y: Math.round(matrix.elements[13]), z: Math.round(matrix.elements[14]) });

test('a locked action keeps the root on key 0\'s x and y, only its height moves', () => {
  const m = model();
  for (const time of [0, 1, 1.5, 2.25]) {
    const [root, child] = computeBoneMatrices(m, 1, time);
    assert.deepEqual({ x: position(root).x, y: position(root).y }, { x: 0, y: 5 }, `root at key ${time}`);
    assert.equal(position(root).z, Math.round(time * 10), 'the bob follows the keys');
    assert.deepEqual(position(child), { x: 0, y: 5, z: Math.round(time * 10) + 50 }, 'children move with the locked root');
  }
});

test('a free action still follows its root keys', () => {
  const [root] = computeBoneMatrices(model(), 0, 1.5);
  assert.deepEqual(position(root), { x: 150, y: 5, z: 15 });
});

test('a locked action loops over one key less, a free one over all its keys', () => {
  const m = model();
  assert.equal(loopKeyCount(m, 1), 3);
  assert.equal(loopKeyCount(m, 0), 4);
  // Time 3 is key 0 again for the locked action (no pose held on the last key), key 3 for the free one.
  assert.equal(position(computeBoneMatrices(m, 1, 3)[0]).z, 0);
  assert.equal(position(computeBoneMatrices(m, 0, 3)[0]).z, 30);
  // Between its last two looped keys the locked action blends key 2 into key 3.
  assert.equal(position(computeBoneMatrices(m, 1, 2.5)[0]).z, 25);
});

test('an action missing from the model falls back to action 0, and a one-key action loops over its one key', () => {
  const m = model();
  assert.deepEqual(position(computeBoneMatrices(m, 9, 1)[0]), { x: 100, y: 5, z: 10 });
  const one = { ...m, actions: [{ numKeys: 1, lockPositions: true }, m.actions[1]] };
  assert.equal(loopKeyCount(one, 0), 1);
});
