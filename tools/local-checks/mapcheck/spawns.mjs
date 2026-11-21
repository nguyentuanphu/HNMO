// The monster spawn spots of every Season 6 map, read from the server's map initializers (Persistence/Initialization),
// for the map check's probe: map number -> [{ npc, x, y }] (an area's centre). Usage: node spawns.mjs <repo> > spawns.json
import fs from 'node:fs';
import path from 'node:path';

const repo = process.argv[2];
const root = path.join(repo, 'server/src/Persistence/Initialization');
const files = new Map();
for (const version of ['Version075', 'Version095d', 'VersionSeasonSix']) {
  const dir = path.join(root, version, 'Maps');
  for (const name of fs.readdirSync(dir)) if (name.endsWith('.cs')) files.set(`${version}.${name.slice(0, -3)}`, fs.readFileSync(path.join(dir, name), 'utf8'));
}
function info(key) {
  const text = files.get(key);
  if (!text) return null;
  const version = key.split('.')[0];
  const base = /class \w+ : ([\w.]+)/.exec(text)?.[1];
  const baseKey = base && base !== 'BaseMapInitializer' ? (base.includes('.') ? base.replace(/\.Maps\./, '.') : `${version}.${base}`) : null;
  const number = /(?:const|static readonly) byte Number = (\d+)/.exec(text)?.[1] ?? /MapNumber => (\d+)/.exec(text)?.[1];
  const spawns = [];
  for (const m of text.matchAll(/^\s*yield return this\.CreateMonsterSpawn\(\d+, this\.NpcDictionary\[(\d+)\], (\d+), (\d+)(?:, (\d+), (\d+))?/gm)) {
    const [npc, a, b, c, d] = [m[1], m[2], m[3], m[4], m[5]].map((v) => (v === undefined ? undefined : Number(v)));
    // (x, y) or (x1, x2, y1, y2)
    spawns.push(c === undefined ? { npc, x: a, y: b } : { npc, x: Math.round((a + b) / 2), y: Math.round((c + d) / 2) });
  }
  // Field layouts: new(number, monster, x1, x2, y1, y2, quantity)
  for (const m of text.matchAll(/^\s*new\(\d+, (\d+), (\d+), (\d+), (\d+), (\d+), \d+\)/gm)) {
    const [npc, x1, x2, y1, y2] = m.slice(1).map(Number);
    spawns.push({ npc, x: Math.round((x1 + x2) / 2), y: Math.round((y1 + y2) / 2) });
  }
  const parent = baseKey ? info(baseKey) : null;
  return { number: number !== undefined ? Number(number) : parent?.number, spawns: [...spawns, ...(parent?.spawns ?? [])] };
}
const byMap = {};
const seasonSix = [...files.keys()].filter((k) => k.startsWith('VersionSeasonSix.'));
const all = [...seasonSix, ...[...files.keys()].filter((k) => !k.startsWith('VersionSeasonSix.'))];
for (const key of all) {
  const i = info(key);
  if (!i || i.number === undefined || byMap[i.number]) continue; // Season 6's own file first
  if (!i.spawns.length) continue;
  byMap[i.number] = { file: key, spawns: i.spawns };
}
console.log(JSON.stringify(byMap));
