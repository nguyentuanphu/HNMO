// Unit tests of the 3D character stage (src/scenes/characterStage.ts): the desktop's CharacterScene values: its camera
// looks from the east, a little north and down at the five places; the vertical field of view of 40° horizontal on the
// 4:3 reference; the five places and their facing; the light of the picked character and the rings' glow.
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
let mod;
try {
  mod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/scenes/characterStage.js')).href);
} catch {
  // The module loads the world and models with the browser; if it can't load here, the test says so.
}
const skip = !mod && 'characterStage.js not loadable here';
const close = (actual, expected, epsilon = 1e-3) => assert.ok(Math.abs(actual - expected) < epsilon, `${actual} ≈ ${expected}`);

test('the camera looks west, a little north and 5.5° down, as DefaultCamera::UpdateFrustum builds it', { skip }, () => {
  const { cameraBasis, STAGE_CAMERA } = mod;
  const { forward, up } = cameraBasis(STAGE_CAMERA.angle);
  close(forward[0], -0.9616);
  close(forward[1], 0.2577);
  close(forward[2], -0.0958);
  assert.ok(up[2] > 0.99, 'up is nearly straight up');
  // A plain check of the formula: no angles look down the -z axis.
  assert.deepEqual(cameraBasis([0, 0, 0]).forward.map((v) => Math.round(v * 1000) / 1000 + 0), [0, 0, -1]);
});

test('every place is in front of the camera, within its field of view', { skip }, () => {
  const { cameraBasis, verticalFov, STAGE_CAMERA, STAGE_SLOTS, STAGE_HEIGHT } = mod;
  const { forward } = cameraBasis(STAGE_CAMERA.angle);
  const [cx, cy, cz] = STAGE_CAMERA.position;
  const half = (verticalFov(STAGE_CAMERA.hFov) / 2) * (4 / 3) * Math.PI / 180; // roughly the horizontal half angle
  for (const slot of STAGE_SLOTS) {
    const v = [slot.x - cx, slot.y - cy, STAGE_HEIGHT + 100 - cz];
    const length = Math.hypot(...v);
    const cos = (v[0] * forward[0] + v[1] * forward[1] + v[2] * forward[2]) / length;
    assert.ok(cos > Math.cos(half * 1.2), `slot at ${slot.x},${slot.y} is in view`);
    assert.ok(length < STAGE_CAMERA.far, 'nearer than the far plane');
  }
});

test('40° horizontal on the 4:3 reference is about 30.54° vertical; five places as the desktop list sets them', { skip }, () => {
  const { verticalFov, STAGE_SLOTS, STAGE_MAP } = mod;
  close(verticalFov(40), 30.54, 0.01);
  assert.equal(STAGE_MAP, 74);
  assert.equal(STAGE_SLOTS.length, 5);
  assert.deepEqual(STAGE_SLOTS.map((s) => s.angle), [115, 90, 75, 60, 35]);
});

test('the picked character is lit, the rings glow between 0.2 and 0.8', { skip }, () => {
  const { stageLight, ringBrightness } = mod;
  assert.ok(stageLight(true) > stageLight(false));
  for (let t = 0; t < 10_000; t += 137) {
    const b = ringBrightness(t);
    assert.ok(b >= 0.2 - 1e-9 && b <= 0.8 + 1e-9);
  }
});
