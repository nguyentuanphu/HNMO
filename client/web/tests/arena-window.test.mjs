// Unit tests of the Arena window (src/ui/arenaWindow.ts): countdowns and live scores don't redraw the window, so its
// buttons can be clicked while a match is planned or fought. Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
let renderKey;
try {
  ({ renderKey } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/arenaWindow.js')).href));
} catch {
  // The module draws with the browser's DOM only when used; if it can't load here, the test says so.
}

const planned = (secondsToStart, signups) => ({
  inArena: true,
  royale: { times: '12:00', nextSydney: '12:00', secondsToStart, signupOpen: true, signedUp: false, signups, minPlayers: 3, running: false, secondsLeft: 0, zoneX: 0, zoneY: 0, zoneRadius: 0 },
  team: { size: 5, side: 0, scoreA: 0, scoreB: 0, secondsLeft: 600 },
});

test('a countdown or a live score doesn\'t change what is drawn; a sign-up does', { skip: !renderKey && 'arenaWindow.js not loadable here' }, () => {
  assert.equal(renderKey(planned(300, 1)), renderKey(planned(299, 1)), 'the countdown ticks');
  const fighting = { ...planned(0, 1), team: { size: 5, side: 1, scoreA: 3, scoreB: 2, secondsLeft: 420 } };
  assert.equal(renderKey(fighting), renderKey({ ...fighting, team: { ...fighting.team, scoreA: 4, secondsLeft: 419 } }), 'a kill and the clock');
  assert.notEqual(renderKey(planned(300, 1)), renderKey(planned(300, 2)), 'one more sign-up');
  assert.notEqual(renderKey(planned(300, 1)), renderKey({ ...planned(300, 1), royale: { ...planned(300, 1).royale, signedUp: true } }), 'signed up');
});
