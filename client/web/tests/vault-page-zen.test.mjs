// Each vault page has its own zen (owner, 26 Nov 2026): VaultPages tells the item windows when the page in use changes
// (a switch was sent, or the server answered with another page), so a zen box asked on the old page does nothing.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const element = () => new Proxy({ style: {}, dataset: {}, classList: { add() {}, remove() {}, toggle() {} } }, {
  get(target, key) {
    if (key in target) return target[key];
    if (key === 'getBoundingClientRect') return () => ({ left: 0, top: 0, width: 0, height: 0, right: 0, bottom: 0 });
    return () => element();
  },
  set(target, key, value) { target[key] = value; return true; },
});
globalThis.window ??= { addEventListener() {}, innerWidth: 800, innerHeight: 600 };
globalThis.document ??= { addEventListener() {}, documentElement: { lang: 'en' }, body: element(), createElement: () => element() };
globalThis.localStorage ??= { getItem() { return null; }, setItem() {} };

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const { VaultPages } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/vaultPages.js')).href);
const { write } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/net/packets.js')).href);

const statePacket = (active) => write('ServerToClient', 'HnmoVaultPageState', {
  Json: JSON.stringify({ active, unlocked: 3, max: 5, price: 200, coinType: 'WCoinC', balance: 1000 }),
});

function setUp() {
  let changes = 0;
  const sent = [];
  const pages = new VaultPages({
    send: (name, fields) => sent.push({ name, fields }), message() {}, anchor: () => null,
    pageChanged: () => { changes++; },
  });
  return { pages, sent, changes: () => changes };
}

test('a switch tells of a page change at once, before the server answers', () => {
  const { pages, changes } = setUp();
  pages.show();
  assert.equal(pages.handle(statePacket(0)), true);
  assert.equal(changes(), 0, 'the first state is no change');
  pages.switchTo(1);
  assert.equal(changes(), 1, 'a box asked before the switch must not act on page 2');
  pages.handle(statePacket(1));
  assert.equal(changes(), 2, 'the answer with the new page is a change too');
});

test('a state with the same page is no change', () => {
  const { pages, changes } = setUp();
  pages.show();
  pages.handle(statePacket(2));
  pages.handle(statePacket(2));
  assert.equal(changes(), 0);
});
