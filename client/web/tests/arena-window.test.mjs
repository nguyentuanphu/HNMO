// Unit tests of the Arena window (src/ui/arenaWindow.ts): countdowns, live scores and the numbers of players waiting or
// signed up don't redraw the window, so its buttons can be clicked while a match is planned, fought or others queue.
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
let renderKey;
let teamQueueText;
let rankedQueueText;
try {
  ({ renderKey, teamQueueText, rankedQueueText } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/arenaWindow.js')).href));
} catch {
  // The module draws with the browser's DOM only when used; if it can't load here, the test says so.
}

const skip = !renderKey && 'arenaWindow.js not loadable here';

const planned = (secondsToStart, signups) => ({
  inArena: true,
  royale: { times: '12:00', nextSydney: '12:00', secondsToStart, signupOpen: true, signedUp: false, signups, minPlayers: 3, running: false, secondsLeft: 0, zoneX: 0, zoneY: 0, zoneRadius: 0 },
  team: { queued: false, waiting: 0, size: 5, side: 0, scoreA: 0, scoreB: 0, secondsLeft: 600, running: false },
  ranked: { queued: false, inDuel: false, waiting: 0, rating: 1000, wins: 0, losses: 0, weekWins: 0, weekLosses: 0, top: [], week: [] },
});

test('a countdown or a live score doesn\'t change what is drawn; the player\'s own sign-up does', { skip }, () => {
  assert.equal(renderKey(planned(300, 1)), renderKey(planned(299, 1)), 'the countdown ticks');
  const fighting = { ...planned(0, 1), team: { ...planned(0, 1).team, side: 1, scoreA: 3, scoreB: 2, secondsLeft: 420, running: true } };
  assert.equal(renderKey(fighting), renderKey({ ...fighting, team: { ...fighting.team, scoreA: 4, secondsLeft: 419 } }), 'a kill and the clock');
  assert.notEqual(renderKey(planned(300, 1)), renderKey({ ...planned(300, 1), royale: { ...planned(300, 1).royale, signedUp: true } }), 'signed up');
});

test('others joining a queue or the sign-up don\'t redraw the window, so its buttons stay under the pointer', { skip }, () => {
  const state = planned(300, 1);
  assert.equal(renderKey(state), renderKey(planned(300, 4)), 'three more sign up for the battle royale');
  assert.equal(renderKey(state), renderKey({ ...state, team: { ...state.team, waiting: 5 } }), 'five wait for a team battle');
  assert.equal(renderKey(state), renderKey({ ...state, ranked: { ...state.ranked, waiting: 2 } }), 'two wait for a 1v1');
  assert.notEqual(renderKey(state), renderKey({ ...state, team: { ...state.team, queued: true } }), 'the player queues');
});

test('the queue buttons\' texts follow the numbers waiting (written in place)', { skip }, () => {
  const team = planned(300, 1).team;
  assert.match(teamQueueText({ ...team, queued: true, waiting: 5 }), /5/);
  assert.notEqual(teamQueueText({ ...team, queued: true, waiting: 5 }), teamQueueText({ ...team, queued: true, waiting: 6 }));
  assert.equal(teamQueueText({ ...team, waiting: 5 }), teamQueueText({ ...team, waiting: 6 }), 'not queued: no number shown');
  const ranked = planned(300, 1).ranked;
  assert.match(rankedQueueText({ ...ranked, queued: true, waiting: 2 }), /2/);
});
