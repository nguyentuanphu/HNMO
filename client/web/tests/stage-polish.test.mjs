// Unit tests of the stage polish (C step 3): the loading meter (src/loadProgress.ts), the reconnect tries
// (src/net/reconnect.ts), the scene windows' rem on a narrow screen (src/ui/rml.ts sceneRemPx) and the stage's field of
// view on a phone held upright (src/scenes/characterStage.ts stageVerticalFov).
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const load = async (file) => {
  try { return await import(pathToFileURL(path.resolve(__dirname, '../public/js', file)).href); } catch { return null; }
};
const progress = await load('loadProgress.js');
const reconnect = await load('net/reconnect.js');
const rml = await load('ui/rml.js');
const stage = await load('scenes/characterStage.js');

test('the meter goes by stage shares, follows the files of a stage and never goes back', { skip: !progress && 'loadProgress.js not loadable' }, async () => {
  const { LoadMeter, trackLoad } = progress;
  const seen = [];
  const meter = new LoadMeter((reading) => seen.push(reading.fraction));
  meter.stage(0.5);
  const files = [0, 1, 2, 3].map(() => { let done; const p = new Promise((r) => { done = r; }); trackLoad(p); return done; });
  files[0](); files[1]();
  await new Promise((r) => setTimeout(r, 0));
  assert.ok(Math.abs(meter.fraction - 0.25) < 1e-9, `half the files of a half stage: ${meter.fraction}`);
  // More files asked for: the share of done ones falls, the bar doesn't.
  trackLoad(new Promise(() => {}));
  assert.ok(meter.fraction >= 0.25);
  meter.progress(0.9);
  assert.ok(Math.abs(meter.fraction - 0.45) < 1e-9, 'the stage says 90 %');
  meter.progress(1);
  assert.ok(meter.fraction < 0.5, 'a stage never fills before it ends');
  meter.stage(0.5);
  assert.ok(meter.fraction >= 0.475);
  meter.finish();
  assert.equal(meter.fraction, 1);
  for (let i = 1; i < seen.length; i++) assert.ok(seen[i] >= seen[i - 1], 'forward only');
  files[2](); files[3]();
});

test('the reconnect tries wait longer each time, resume with a ticket, and give up after the last', { skip: !reconnect && 'reconnect.js not loadable' }, async () => {
  const { Reconnector, RETRY_DELAYS_S } = reconnect;
  assert.deepEqual(RETRY_DELAYS_S, [0, 2, 4, 8, 16]);
  const plan = { account: 'test2', relay: 'ws://x/relay', connect: { host: '127.0.0.1', port: 44406 }, column: 's6', serverId: 0, character: 'Hero', attempt: 0, lostAt: 0 };
  const waits = [];
  let probes = 0;
  let resumed = null;
  let gaveUp = null;
  const run = (driver) => new Reconnector({ show: () => {}, sleep: async (ms) => { waits.push(ms); }, giveUp: (r) => { gaveUp = r; }, resume: (p, t) => { resumed = { p, t }; }, ...driver });
  // The server comes back at the third try.
  await run({ probe: async () => { if (++probes < 3) throw new Error('down'); }, ticket: async () => ({ ticket: 't', loginName: 'test2' }) }).run(plan);
  assert.equal(probes, 3);
  assert.equal(waits.length, 2 + 4, 'two seconds, then four, one by one');
  assert.equal(resumed?.p.attempt, 2);
  assert.equal(resumed?.t.ticket, 't');
  assert.equal(gaveUp, null);
  // Never back: every try, then 'failed'.
  probes = 0; gaveUp = null; resumed = null;
  await run({ probe: async () => { probes++; throw new Error('down'); }, ticket: async () => ({ ticket: 't', loginName: 'test2' }) }).run(plan);
  assert.equal(probes, RETRY_DELAYS_S.length);
  assert.equal(gaveUp, 'failed');
  // No website session: no try can work.
  gaveUp = null;
  await run({ probe: async () => {}, ticket: async () => 'signed-out' }).run(plan);
  assert.equal(gaveUp, 'signed-out');
  // A failed reload goes on at the next try.
  probes = 0; resumed = null;
  await run({ probe: async () => { probes++; }, ticket: async () => ({ ticket: 'u', loginName: 'test2' }) }).run({ ...plan, attempt: 3 });
  assert.equal(resumed?.p.attempt, 3);
});

test('a scene window grows on a phone held upright, as far as its width allows', { skip: !rml && 'rml.js not loadable' }, () => {
  const { sceneRemPx } = rml;
  assert.equal(sceneRemPx(1280, 720), 1.5, 'the 640x480 layout');
  assert.equal(sceneRemPx(1280, 720, 280), 1.5, 'a wide screen keeps it');
  assert.equal(sceneRemPx(320, 568), 0.5, 'without a width of its own');
  assert.ok(Math.abs(sceneRemPx(320, 568, 280) - 320 / 280) < 1e-9, 'the plates: as wide as the screen');
  assert.equal(sceneRemPx(390, 844, 256), 1.25, 'never more than 1.25');
  assert.ok(Math.abs(sceneRemPx(844, 390, 280) - 390 / 480) < 1e-9, 'a phone held sideways: the layout');
});

test('the stage keeps its 40° across on a screen narrower than 4:3', { skip: !stage && 'characterStage.js not loadable' }, () => {
  const { stageVerticalFov, verticalFov, STAGE_CAMERA } = stage;
  assert.equal(stageVerticalFov(16 / 9), verticalFov(STAGE_CAMERA.hFov));
  assert.ok(Math.abs(stageVerticalFov(4 / 3) - verticalFov(STAGE_CAMERA.hFov)) < 1e-9, 'the same at 4:3');
  const aspect = 320 / 568;
  const across = 2 * Math.atan(Math.tan((stageVerticalFov(aspect) * Math.PI) / 360) * aspect) * 180 / Math.PI;
  assert.ok(Math.abs(across - STAGE_CAMERA.hFov) < 1e-9, `40° across at 320 x 568: ${across}`);
});
