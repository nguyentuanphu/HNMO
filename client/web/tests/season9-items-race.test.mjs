// The item catalog under season changes and slow answers (loadItemCatalog in src/game/items.ts): a catalog is published
// only when it is complete (base, then Season 9 overrides, then the admin panel's), and a load started before a season
// change can't publish or change the new season's catalog. The answers are held with deferred promises, so their order
// is the test's, not the network's.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const js = (file) => pathToFileURL(path.resolve(__dirname, '../public/js', file)).href;

const deferred = () => { let resolve; let reject; const promise = new Promise((res, rej) => { resolve = res; reject = rej; }); return { promise, resolve, reject }; };
const json = (body) => ({ ok: true, status: 200, json: async () => body });

let held = {}; // url -> deferred answer
let fail = new Set();
const fetchLog = [];
globalThis.fetch = async (url) => {
  fetchLog.push(String(url));
  if (fail.has(url)) throw new Error(`fetch failed: ${url}`);
  if (held[url]) return held[url].promise;
  const match = /\/data\/Items\/(Models\/)?Group(\d\d)_/.exec(url);
  if (match) {
    const group = Number(match[2]);
    return match[1] ? json({ group, models: [] }) : json({ group, items: group === 0 ? [{ number: 0, name: { en: 'Base' } }] : [] });
  }
  if (url.endsWith('SharedModels.json')) return json({ models: [] });
  if (url.endsWith('PartModels.json')) return json({ partModels: [] });
  if (url === '/website/items') return json({ items: [] });
  return { ok: false, status: 404, json: async () => null };
};

const items = await import(js('game/items.js'));
const data = await import(js('dataRoot.js'));
const nameOf = (catalog) => catalog.definitions.get('0:0')?.name?.en;
const activeName = () => items.itemDefinition(0, 0)?.name?.en;
const s9Overrides = (name) => json({ season: 's9', items: [{ group: 0, number: 0, name: { en: name } }] });
const admin = (name) => json({ items: [{ group: 0, number: 0, name: { en: name } }] });

function reset(season) {
  held = {}; fail = new Set();
  data.setSeason(season === 's6' ? 's9' : 's6');
  data.setSeason(season);
}

test('two callers of Season 9 while its overrides load get the same complete catalog', async () => {
  reset('s9');
  held['/data/Items/Season9Overrides.json'] = deferred();
  const first = items.loadItemCatalog();
  await new Promise((resolve) => setTimeout(resolve, 20)); // the base files are in, the overrides are not
  assert.equal(activeName(), undefined, 'the base catalog was published before the overrides came');
  const second = items.loadItemCatalog().then((catalog) => ({ catalog, name: nameOf(catalog) }));
  held['/data/Items/Season9Overrides.json'].resolve(s9Overrides('S9'));
  const [a, b] = await Promise.all([first, second]);
  assert.equal(b.name, 'S9', 'the second caller got the catalog without the Season 9 overrides');
  assert.equal(a, b.catalog, 'one load, one object');
  assert.equal(activeName(), 'S9');
});

test('S9 -> S6: a late Season 9 answer changes neither the Season 6 catalog nor what is active', async () => {
  reset('s9');
  held['/data/Items/Season9Overrides.json'] = deferred();
  const s9 = items.loadItemCatalog();
  data.setSeason('s6');
  const s6 = await items.loadItemCatalog();
  assert.equal(nameOf(s6), 'Base');
  held['/data/Items/Season9Overrides.json'].resolve(s9Overrides('S9 late'));
  const s9Catalog = await s9;
  assert.notEqual(s9Catalog, s6, 'the Season 9 promise must not hand out the Season 6 object');
  assert.equal(nameOf(s6), 'Base', 'the late answer changed the Season 6 catalog');
  assert.equal(activeName(), 'Base', 'the late answer was published in Season 6');
  assert.equal(await items.loadItemCatalog(), s6, 'Season 6 stays cached');
});

test('S9 -> S6 -> S9: the last Season 9 load is the one published', async () => {
  reset('s9');
  held['/data/Items/Season9Overrides.json'] = deferred();
  const old = items.loadItemCatalog();
  data.setSeason('s6');
  await items.loadItemCatalog();
  const oldAnswer = held['/data/Items/Season9Overrides.json'];
  delete held['/data/Items/Season9Overrides.json'];
  held['/data/Items/Season9Overrides.json'] = deferred();
  data.setSeason('s9');
  const fresh = items.loadItemCatalog();
  held['/data/Items/Season9Overrides.json'].resolve(s9Overrides('S9 new'));
  assert.equal(nameOf(await fresh), 'S9 new');
  oldAnswer.resolve(s9Overrides('S9 old'));
  await old;
  assert.equal(activeName(), 'S9 new', 'the old Season 9 load overwrote the new one');
});

test('precedence base -> Season 9 -> admin panel, with a slow admin answer', async () => {
  reset('s9');
  held['/website/items'] = deferred();
  const loading = items.loadItemCatalog();
  await new Promise((resolve) => setTimeout(resolve, 20));
  assert.equal(activeName(), undefined, 'published before the admin answer came');
  held['/website/items'].resolve(admin('Admin'));
  assert.equal(nameOf(await loading), 'Admin');
});

test('a failed or broken override answer still gives the base catalog, and a retry after a season change loads again', async () => {
  reset('s9');
  fail.add('/data/Items/Season9Overrides.json');
  held['/website/items'] = { promise: Promise.resolve({ ok: true, status: 200, json: async () => { throw new SyntaxError('bad json'); } }) };
  const catalog = await items.loadItemCatalog();
  assert.equal(nameOf(catalog), 'Base');
  reset('s9');
  held['/data/Items/Season9Overrides.json'] = { promise: Promise.resolve(s9Overrides('S9 retry')) };
  assert.equal(nameOf(await items.loadItemCatalog()), 'S9 retry');
});
