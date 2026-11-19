import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const formatsUrl = pathToFileURL(path.resolve(__dirname, '../public/js/formats.js')).href;
const {
  buxConvert,
  parseBMD,
  parseTerrainAttributes,
  parseTerrainMapping,
  parseWorldObjects,
  TERRAIN_SIZE,
} = await import(formatsUrl);

const FIRST_STAGE_KEY = new Uint8Array([
  0x77, 0x65, 0x62, 0x7a, 0x65, 0x6e, 0x23, 0x40, 0x21, 0x30, 0x31, 0x77, 0x65, 0x62, 0x7a, 0x65,
  0x6e, 0x23, 0x40, 0x21, 0x30, 0x31, 0x77, 0x65, 0x62, 0x7a, 0x65, 0x6e, 0x23, 0x40, 0x21, 0x30,
]);
const MAP_XOR_KEY = [0xd1, 0x73, 0x52, 0xf6, 0xd2, 0x9a, 0xcb, 0x27, 0x3e, 0xaf, 0x59, 0x31, 0x37, 0xb3, 0xe7, 0xa2];

function encryptMapFile(plain) {
  const out = new Uint8Array(plain.length);
  let key = 0x5e;
  for (let i = 0; i < plain.length; i++) {
    const enc = ((plain[i] + key) & 0xff) ^ MAP_XOR_KEY[i % 16];
    out[i] = enc;
    key = (enc + 0x3d) & 0xff;
  }
  return out;
}

function getU32BE(b, o) {
  return ((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]) >>> 0;
}
function putU32BE(b, o, v) {
  b[o] = (v >>> 24) & 0xff;
  b[o + 1] = (v >>> 16) & 0xff;
  b[o + 2] = (v >>> 8) & 0xff;
  b[o + 3] = v & 0xff;
}

function encryptTeaBlocks(buf, offset, length, key) {
  const k0 = getU32BE(key, 0);
  const k1 = getU32BE(key, 4);
  const k2 = getU32BE(key, 8);
  const k3 = getU32BE(key, 12);
  const whole = length - (length % 8);
  for (let p = 0; p < whole; p += 8) {
    let y = getU32BE(buf, offset + p);
    let z = getU32BE(buf, offset + p + 4);
    let sum = 0;
    const delta = 0x9e3779b9 >>> 0;
    for (let i = 0; i < 32; i++) {
      sum = (sum + delta) >>> 0;
      y = (y + ((((z << 4) + k0) ^ (z + sum) ^ ((z >>> 5) + k1)) >>> 0)) >>> 0;
      z = (z + ((((y << 4) + k2) ^ (y + sum) ^ ((y >>> 5) + k3)) >>> 0)) >>> 0;
    }
    putU32BE(buf, offset + p, y);
    putU32BE(buf, offset + p + 4, z);
  }
}

// Wraps a plain body in a two-stage TEA ModulusCipher payload (<1024 data bytes so stage1 skips chunking cleanly,
// or >=1024 data bytes exercising first-stage chunking as well).
function packModulusTea(body) {
  const payload = new Uint8Array(34 + body.length);
  payload[0] = 0x08; // selector 0 = TEA
  payload[1] = 0x00; // selector 0 = TEA
  for (let i = 0; i < 32; i++) payload[2 + i] = (i * 17 + 5) & 0xff;
  const stage2Key = payload.slice(2, 34);
  payload.set(body, 34);
  encryptTeaBlocks(payload, 34, body.length, stage2Key);
  if (body.length > 1024) {
    if (body.length > 4 * 1024) {
      encryptTeaBlocks(payload, 2 + Math.floor(body.length / 2), 1024, FIRST_STAGE_KEY);
    }
    encryptTeaBlocks(payload, 2, 1024, FIRST_STAGE_KEY);
    encryptTeaBlocks(payload, payload.length - 1024, 1024, FIRST_STAGE_KEY);
  }
  return payload;
}

test('parseBMD decodes version 0x0E ModulusCipher payload', () => {
  // Minimal valid BMD v0x0A body: 32-byte name + 3 int16 counts (0 meshes, 0 bones, 0 actions) + padding >= 8 bytes
  const body = new Uint8Array(64);
  body.set(Buffer.from('SyntheticS9.smd\0', 'ascii'), 0);
  const payload = packModulusTea(body);
  const file = new Uint8Array(8 + payload.length);
  file[0] = 0x42; file[1] = 0x4d; file[2] = 0x44; file[3] = 0x0e;
  new DataView(file.buffer).setInt32(4, payload.length, true);
  file.set(payload, 8);

  const parsed = parseBMD(file);
  assert.equal(parsed.name, 'SyntheticS9.smd');
  assert.equal(parsed.meshes.length, 0);
  assert.equal(parsed.bones.length, 0);
  assert.equal(parsed.actions.length, 0);
});

test('parseTerrainMapping decodes S9 MAP\\x01 payload', () => {
  const cells = TERRAIN_SIZE * TERRAIN_SIZE;
  const plain = new Uint8Array(2 + cells * 3);
  plain[0] = 0;
  plain[1] = 92;
  plain[2] = 7;
  plain[2 + cells] = 9;
  plain[2 + cells * 2] = 128;

  const payload = packModulusTea(plain);
  const file = new Uint8Array(4 + payload.length);
  file[0] = 0x4d; file[1] = 0x41; file[2] = 0x50; file[3] = 0x01; // "MAP\x01"
  file.set(payload, 4);

  const mapping = parseTerrainMapping(file);
  assert.equal(mapping.mapNumber, 92);
  assert.equal(mapping.layer1[0], 7);
  assert.equal(mapping.layer2[0], 9);
  assert.equal(mapping.alpha[0], 128);
});

test('parseTerrainAttributes decodes S9 ATT\\x01 wide WORD payload', () => {
  const cells = TERRAIN_SIZE * TERRAIN_SIZE;
  const plain = new Uint8Array(4 + cells * 2);
  plain[0] = 0; plain[1] = 100; plain[2] = 255; plain[3] = 255;
  new DataView(plain.buffer).setUint16(4, 0x0004, true); // NOMOVE on cell 0
  new DataView(plain.buffer).setUint16(6, 0x0010, true); // WATER on cell 1
  buxConvert(plain);

  const payload = packModulusTea(plain);
  const file = new Uint8Array(4 + payload.length);
  file[0] = 0x41; file[1] = 0x54; file[2] = 0x54; file[3] = 0x01; // "ATT\x01"
  file.set(payload, 4);

  const flags = parseTerrainAttributes(file);
  assert.equal(flags[0], 0x0004);
  assert.equal(flags[1], 0x0010);
});

test('parseWorldObjects handles version 1 (32-byte object records)', () => {
  const plain = new Uint8Array(4 + 32 * 2);
  const dv = new DataView(plain.buffer);
  plain[0] = 1; // v1
  plain[1] = 101;
  dv.setInt16(2, 2, true);
  // record 0 at offset 4 (32 bytes)
  dv.setInt16(4, 36, true);
  dv.setFloat32(6, 100.5, true);
  dv.setFloat32(30, 1.25, true);
  plain[34] = 0x00; plain[35] = 0x01;
  // record 1 at offset 36 (32 bytes)
  dv.setInt16(36, 90, true);
  dv.setFloat32(38, 250.25, true);
  dv.setFloat32(62, 1.5, true);
  plain[66] = 0x01; plain[67] = 0x01;

  const res = parseWorldObjects(encryptMapFile(plain));
  assert.equal(res.mapNumber, 101);
  assert.equal(res.objects.length, 2);
  assert.equal(res.objects[0].type, 36);
  assert.equal(res.objects[0].position[0], 100.5);
  assert.equal(res.objects[0].scale, 1.25);
  assert.equal(res.objects[1].type, 90);
  assert.equal(res.objects[1].position[0], 250.25);
  assert.equal(res.objects[1].scale, 1.5);
});
