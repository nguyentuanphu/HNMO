// Unit tests of the automatic error reports (src/autoErrors.ts): the fingerprint (message + file + line, without the
// address's query string, hash or the numbers of a build in its paths), the hourly limit of a browser (5 an hour, kept
// in its storage, and working without one), and the scrubbing of tickets and passwords.
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
let mod;
try {
  mod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/autoErrors.js')).href);
} catch {
  // The module uses the browser only when an error is reported; if it can't load here, the test says so.
}
const skip = !mod && 'autoErrors.js not loadable here';

test('one error gives one fingerprint whatever the query string, hash or build numbers', { skip }, () => {
  const { fingerprintOf, cleanPath } = mod;
  const a = fingerprintOf('x is undefined', 'https://game.example/js/main.js?v=123#account=a&ticket=t', 1200);
  const b = fingerprintOf('x is undefined', 'http://127.0.0.1:8081/js/main.js', '1200');
  assert.equal(a, b);
  assert.equal(a, 'x is undefined|/js/main.js|1200');
  assert.equal(cleanPath('/js/chunk-4f3a91.js?x=1'), '/js/chunk-fa.js');
  assert.notEqual(a, fingerprintOf('x is undefined', '/js/main.js', 1201), 'another line is another error');
  assert.notEqual(a, fingerprintOf('y is undefined', '/js/main.js', 1200), 'another message is another error');
  assert.ok(!a.includes('ticket'));
});

test('at most 5 reports an hour per browser, kept in its storage', { skip }, () => {
  const { HourlyLimit } = mod;
  const items = new Map();
  const storage = { getItem: (k) => items.get(k) ?? null, setItem: (k, v) => items.set(k, v), removeItem: (k) => items.delete(k) };
  let now = 1_000_000;
  const limit = new HourlyLimit(storage, () => now);
  for (let i = 0; i < 5; i++) { assert.equal(limit.take(), true); now += 1000; }
  assert.equal(limit.take(), false, 'the sixth in the hour is dropped');
  assert.equal(new HourlyLimit(storage, () => now).take(), false, 'another page load of the browser shares the count');
  now += 60 * 60 * 1000;
  assert.equal(limit.take(), true, 'an hour later reports go again');
});

test('the limit works without storage and with a broken or blocked one', { skip }, () => {
  const { HourlyLimit } = mod;
  let now = 5_000_000;
  const none = new HourlyLimit(null, () => now);
  for (let i = 0; i < 5; i++) assert.equal(none.take(), true);
  assert.equal(none.take(), false);
  const blocked = { getItem: () => { throw new Error('blocked'); }, setItem: () => { throw new Error('blocked'); }, removeItem: () => {} };
  const limit = new HourlyLimit(blocked, () => now);
  for (let i = 0; i < 5; i++) assert.equal(limit.take(), true);
  assert.equal(limit.take(), false);
  const broken = { getItem: () => 'not json', setItem: () => {}, removeItem: () => {} };
  assert.equal(new HourlyLimit(broken, () => now).take(), true);
});

test('tickets and passwords never go with a report', { skip }, () => {
  const { scrub, firstFrame } = mod;
  const text = 'Error at https://game.example/index.html#account=bob&ticket=SECRET\nlogin password=hunter2\nat f (https://game.example/js/a.js?x=1:10:5)';
  const clean = scrub(text);
  assert.ok(!clean.includes('SECRET'));
  assert.ok(!clean.includes('hunter2'));
  assert.ok(!clean.includes('?x=1'));
  assert.deepEqual(firstFrame('TypeError: x\n    at f (https://game.example/js/main.js:1200:5)\n    at g (https://game.example/js/b.js:3:1)'), { file: 'https://game.example/js/main.js', line: 1200 });
});

test('diagnostics use allowlisted source coordinates instead of raw error text', { skip }, () => {
  const { diagnosticSignature } = mod;
  assert.equal(diagnosticSignature('error', 'https://example.invalid/private/main.js?key=secret', 12), 'error|main.js|12');
  assert.equal(diagnosticSignature('packet', '/home/private-name.js', 12), 'packet');
  assert.equal(diagnosticSignature('error', '/js/main.js', 50001), 'error');
});

test('an unavailable storage write still limits reports in the tab', { skip }, () => {
  const storage = { getItem: () => '[]', setItem: () => { throw Error('blocked'); } };
  const limit = new mod.HourlyLimit(storage);
  for (let i = 0; i < 5; i++) assert.equal(limit.take(), true);
  assert.equal(limit.take(), false);
});

test('a queued disconnect only drains after a matching authenticated reconnect', { skip }, async () => {
  const items = new Map();
  const storage = {
    getItem: key => items.get(key) ?? null,
    setItem: (key, value) => items.set(key, value),
    removeItem: key => items.delete(key),
  };
  const previousWindow = globalThis.window;
  globalThis.window = { localStorage: storage, sessionStorage: storage };
  try {
    const scope6 = await mod.autoErrorScope('fixture.invalid', 55901, 'fixture');
    const scope9 = await mod.autoErrorScope('fixture.invalid', 56001, 'fixture');
    const other = await mod.autoErrorScope('fixture.invalid', 55901, 'other');
    assert.ok(scope6);
    assert.notEqual(scope6, scope9);
    assert.notEqual(scope6, other);

    const sent = [];
    const disconnect = mod.setAutoErrorSender(json => sent.push(JSON.parse(json)), scope6);
    disconnect();
    assert.equal(sent.length, 0);
    mod.setAutoErrorSender(json => sent.push(JSON.parse(json)), scope9);
    assert.equal(sent.length, 0);
    mod.setAutoErrorSender(json => sent.push(JSON.parse(json)), other);
    assert.equal(sent.length, 0, 'another account on the same endpoint gets nothing');
    mod.setAutoErrorSender(json => sent.push(JSON.parse(json)), null);
    assert.equal(sent.length, 0, 'no scope (no Web Crypto) gets nothing');
    mod.setAutoErrorSender(() => { throw Error('socket not ready'); }, scope6);
    mod.setAutoErrorSender(json => sent.push(JSON.parse(json)), scope6);
    assert.deepEqual(sent.map(report => report.kind), ['disconnect']);
    mod.setAutoErrorSender(json => sent.push(JSON.parse(json)), scope6);
    assert.equal(sent.length, 1);
  } finally {
    if (previousWindow === undefined) delete globalThis.window;
    else globalThis.window = previousWindow;
  }
});

test('the web writer sends C2 F5 60 with the JSON from offset 5 and no terminator', async () => {
  const { write } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/net/packets.js')).href);
  const json = '{"fingerprint":"error|main.js|12","kind":"error","version":"web 20261127","browser":"Chrome 131","os":"Linux"}';
  const bytes = write('ClientToServer', 'HnmoAutoErrorReport', { Json: json });
  assert.equal(bytes[0], 0xc2);
  assert.equal((bytes[1] << 8) | bytes[2], bytes.length);
  assert.deepEqual([bytes[3], bytes[4]], [0xf5, 0x60]);
  assert.equal(bytes.length, 5 + json.length);
  assert.equal(new TextDecoder().decode(bytes.subarray(5)), json);
  assert.ok(!bytes.subarray(5).includes(0), 'no NUL after the header');
});
