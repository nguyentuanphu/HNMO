// Unit tests of the server choice's window (src/ui/serverSelectWindow.ts): the desktop's load states (100 % full,
// 128 % overloaded), its ten-segment load bar and the button's "({vip} {pvp})".
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const mod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/serverSelectWindow.js')).href);

test('load states as the desktop: open below 100, full from 100, overloaded from 128', () => {
  assert.equal(mod.loadState(0), 'open');
  assert.equal(mod.loadState(99), 'open');
  assert.equal(mod.loadState(100), 'full');
  assert.equal(mod.loadState(127), 'full');
  assert.equal(mod.loadState(128), 'overloaded');
  assert.equal(mod.loadText(50, false), '');
  assert.equal(mod.loadText(100, false), '(Full)');
  assert.equal(mod.loadText(130, false), 'Overloaded');
});

test('the load bar: ten segments of 10 %, lit by the load, all full color once full', () => {
  const bar = mod.loadBarSegments(35, '#00bd8b', '#e0483e');
  assert.equal(bar.length, 10);
  assert.deepEqual(bar.map((s) => s.lit), ['100%', '100%', '100%', '50%', '0%', '0%', '0%', '0%', '0%', '0%']);
  assert.equal(bar[0].color, '#00bd8b');
  assert.equal(bar[3].color, 'color-mix(in oklch, #00bd8b, #e0483e 30%)');
  const full = mod.loadBarSegments(100, '#00bd8b', '#e0483e');
  assert.ok(full.every((s) => s.lit === '100%' && s.color === '#e0483e'));
});

test('the button names a VIP or non-PvP server like the server list', () => {
  assert.equal(mod.serverButtonName('Server 1', false, false, false), 'Server 1');
  assert.equal(mod.serverButtonName('Server 1', true, true, false), 'Server 1 (VIP Non-PvP)');
  assert.equal(mod.serverButtonName('Server 2', false, true, false), 'Server 2 (Non-PvP)');
});
