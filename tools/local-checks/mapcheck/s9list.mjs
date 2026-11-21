// Season 9's monster numbers (IGC_MonsterList.xml) and spawns (IGC_MonsterSpawn*.xml) against the web game's
// Monsters.json: which Season 9 numbers have no entry, and on which maps they spawn. Usage: node s9list.mjs <repo>
import fs from 'node:fs';
const repo = process.argv[2];
const dir = process.env.LEGACY_S9_MONSTERS_DIR; // the Season 9 server suite's monster folder (not in the repository)
const list = fs.readFileSync(`${dir}/IGC_MonsterList.xml`, 'utf8');
const names = new Map([...list.matchAll(/<Monster Index="(\d+)"\s+ExpType="\d+"\s+Name="([^"]*)"/g)].map((m) => [Number(m[1]), m[2]]));
const ours = JSON.parse(fs.readFileSync(`${repo}/client/src/bin/Data/Monsters/Monsters.json`, 'utf8'));
const known = new Map();
for (const m of ours.monsters) for (const n of m.numbers) known.set(n.number, { model: n.model ?? m.model, name: typeof m.name === 'string' ? m.name : m.name?.en });
const spawnsByNumber = new Map();
for (const file of ['IGC_MonsterSpawn.xml', 'IGC_MonsterSpawn_Event.xml']) {
  const text = fs.readFileSync(`${dir}/${file}`, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
  for (const map of text.matchAll(/<Map Number="(\d+)"[^>]*>([\s\S]*?)<\/Map>/g)) {
    for (const spot of map[2].matchAll(/<Spawn Index="(\d+)"[^>]*?StartX="(\d+)"\s+StartY="(\d+)"/g)) {
      const n = Number(spot[1]);
      if (!spawnsByNumber.has(n)) spawnsByNumber.set(n, new Map());
      const maps = spawnsByNumber.get(n);
      if (!maps.has(Number(map[1]))) maps.set(Number(map[1]), [Number(spot[2]), Number(spot[3])]);
    }
  }
}
const missing = [...names].filter(([n]) => !known.has(n));
const spawnedMissing = missing.filter(([n]) => spawnsByNumber.has(n));
const nameDiffer = [...names].filter(([n, name]) => known.has(n) && known.get(n).name && known.get(n).name.toLowerCase() !== name.toLowerCase());
console.log(JSON.stringify({
  s9Numbers: names.size, ours: known.size, missing: missing.length, spawnedMissing: spawnedMissing.length,
  spawnedMissingList: spawnedMissing.map(([n, name]) => ({ n, name, maps: Object.fromEntries(spawnsByNumber.get(n)) })),
  missingNotSpawned: missing.filter(([n]) => !spawnsByNumber.has(n)).map(([n, name]) => `${n} ${name}`),
  nameDiffer: nameDiffer.map(([n, name]) => `${n}: S9 "${name}" / ours "${known.get(n).name}"`),
}, null, 1));
