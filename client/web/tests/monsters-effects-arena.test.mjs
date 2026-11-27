// Unit tests for Part 8 Item 5 fixes: strict BMD texture-script parsing (missing body parts),
// out-of-order parent bones (BloodCastle01), 30-byte EncTerrain.obj parsing + expanded Arena World7/Terrain7,
// and QuadBatch/effect pooling behavior.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as THREE from 'three';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const web = path.resolve(__dirname, '..');
const baseData = path.resolve(web, '../src/bin/Data');
const serverResources = path.resolve(web, '../../server/src/Persistence/Initialization/Resources');

const { textureKind, computeBoneMatrices } = await import(pathToFileURL(path.join(web, 'public/js/bmd.js')).href);
const { parseBMD, parseWorldObjects, parseTerrainAttributes } = await import(pathToFileURL(path.join(web, 'public/js/formats.js')).href);
const { QuadBatches } = await import(pathToFileURL(path.join(web, 'public/js/render/effects/quadBatch.js')).href);
const { preloadEffectTextures } = await import(pathToFileURL(path.join(web, 'public/js/render/effects/core.js')).href);

test('textureKind follows strict C++ parsingTScriptA rules without hiding Head/Hair textures', () => {
  assert.deepEqual(textureKind('monster259_Head.jpg'), { isTga: false, additive: false, hidden: false });
  assert.deepEqual(textureKind('RFighter_Hair.tga'), { isTga: true, additive: false, hidden: false });
  assert.deepEqual(textureKind('TKnight_Hair.tga'), { isTga: true, additive: false, hidden: false });
  assert.deepEqual(textureKind('Item_Helm.jpg'), { isTga: false, additive: false, hidden: false });
  assert.deepEqual(textureKind('Monster81_R2.jpg'), { isTga: false, additive: false, hidden: false });
  assert.deepEqual(textureKind('Book_of_Rargle.jpg'), { isTga: false, additive: false, hidden: false });
  assert.deepEqual(textureKind('hair_R.jpg'), { isTga: false, additive: false, hidden: false });
  assert.deepEqual(textureKind('aura_R.jpg'), { isTga: false, additive: true, hidden: false });
  assert.deepEqual(textureKind('cloak_H.tga'), { isTga: true, additive: false, hidden: true });
  assert.deepEqual(textureKind('glow_RH.jpg'), { isTga: false, additive: true, hidden: true });
  assert.deepEqual(textureKind('solid_RN.jpg'), { isTga: false, additive: false, hidden: false });
  assert.deepEqual(textureKind('crop_DC.tga'), { isTga: true, additive: false, hidden: false });
});

test('computeBoneMatrices applies parent transform even when parent bone appears later in the array', () => {
  const still = new Float32Array([0, 0, 0]);
  const rootPos = new Float32Array([100, 200, 300]);
  const childPos = new Float32Array([10, 20, 30]);
  const model = {
    name: 'out-of-order',
    meshes: [],
    actions: [{ numKeys: 1, lockPositions: false }],
    bones: [
      { dummy: false, name: 'child_first', parent: 1, frames: [{ positions: childPos, rotations: still }] },
      { dummy: false, name: 'parent_later', parent: -1, frames: [{ positions: rootPos, rotations: still }] },
    ],
  };
  const matrices = computeBoneMatrices(model, 0, 0);
  const childWorld = new THREE.Vector3().setFromMatrixPosition(matrices[0]);
  assert.deepEqual({ x: Math.round(childWorld.x), y: Math.round(childWorld.y), z: Math.round(childWorld.z) }, { x: 110, y: 220, z: 330 });

  // Also verify real BloodCastle01.bmd resolves its forward-parented bones (43, 44 -> 45) without error.
  const bcBmd = parseBMD(new Uint8Array(fs.readFileSync(path.join(baseData, 'NPC/BloodCastle01.bmd'))));
  const bcMatrices = computeBoneMatrices(bcBmd, 0, 0);
  assert.equal(bcMatrices.length, bcBmd.bones.length);
  for (const m of bcMatrices) assert.ok(Number.isFinite(m.elements[12]) && Number.isFinite(m.elements[14]));
});

test('Arena World7 uses expanded 30-byte EncTerrain7.obj and matches server Terrain7.att across all 65,536 cells', () => {
  const objBytes = new Uint8Array(fs.readFileSync(path.join(baseData, 'World7/EncTerrain7.obj')));
  const worldObjects = parseWorldObjects(objBytes);
  assert.equal(worldObjects.objects.length, 5169);
  const usedTypes = new Set(worldObjects.objects.map((o) => o.type));
  assert.ok(usedTypes.has(75), 'expanded Arena objects include up to Object76 (type 75)');
  for (const t of usedTypes) {
    assert.ok(t >= 0 && t < 100, `unexpected object type ${t}`);
    const objFile = path.join(baseData, 'Object7', `Object${String(t + 1).padStart(2, '0')}.bmd`);
    assert.ok(fs.existsSync(objFile), `missing ${objFile} for type ${t}`);
  }

  const clientAtt = parseTerrainAttributes(new Uint8Array(fs.readFileSync(path.join(baseData, 'World7/EncTerrain7.att'))));
  const serverAttRaw = fs.readFileSync(path.join(serverResources, 'Terrain7.att'));
  assert.equal(serverAttRaw.length, 65539);
  const serverAtt = serverAttRaw.subarray(3);
  let diffs = 0;
  for (let i = 0; i < 65536; i++) {
    if ((clientAtt[i] & 0xff) !== (serverAtt[i] & 0xff)) diffs++;
  }
  assert.equal(diffs, 0, 'client EncTerrain7.att and server Terrain7.att must match byte-for-byte');
});

test('QuadBatches reuses scratch vectors without error on billboard and decal batches', async () => {
  const prevFetch = globalThis.fetch;
  globalThis.fetch = async () => new Response(null, { status: 404 });
  try {
    await preloadEffectTextures(['flareRed', 'inferno']);
    const root = new THREE.Group();
    const batches = new QuadBatches(root);
    batches.beginFrame(new THREE.PerspectiveCamera());
    assert.equal(root.children.length, 0);
    batches.billboard('flareRed', 'blend2', new THREE.Vector3(10, 20, 30), 64, 64, new THREE.Vector3(1, 1, 1), 45);
    batches.decal('inferno', 'glow', 500, 600, 2, new THREE.Vector3(1, 1, 1), 0, () => 10, 10, 2);
    batches.endFrame();
    batches.dispose();
  } finally {
    globalThis.fetch = prevFetch;
  }
});
