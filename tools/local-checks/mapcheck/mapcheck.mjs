// The map check (2026-11-20): every map of the web game in one season, read from the client's own state. A GM is
// warped (/move <hero> <map> <x> <y>) to the map's monster areas (from the server's map initializers, spawns.json);
// at each spot every monster and NPC in view is recorded: its number, the model slot it got, why it got the unknown
// look, player-shaped or not, its body files, meshes without a texture and see-through meshes. Per map also the
// world objects the client skips, the files that failed (HTTP 4xx) and the console warnings, and one screenshot.
// Usage: node mapcheck.mjs <s6|s9> <account> <out dir> [maps comma list] [base url]
import fs from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-maps/client/web/package.json');
const { chromium } = require('playwright');
const [SEASON, ACCOUNT, OUT, MAPS, BASE] = [process.argv[2], process.argv[3], process.argv[4], process.argv[5], process.argv[6] ?? 'http://127.0.0.1:8084'];
fs.mkdirSync(OUT, { recursive: true });
const spawns = JSON.parse(fs.readFileSync(new URL('./spawns.json', import.meta.url), 'utf8'));
const EXTRA_MAPS = [9, 32, 39, 40, 58, 64, 65, 66, 67, 68, 69, 70, 71, 72]; // maps with wave or no spawns: their gate only
const SEASON9_MAPS = [82, 91, 93, 94, 95, 98, 100, 102];
let maps = MAPS && MAPS !== 'all' ? MAPS.split(',').map(Number) : [...new Set([...Object.keys(spawns).map(Number), ...EXTRA_MAPS, ...(SEASON === 's9' ? SEASON9_MAPS : [])])].sort((a, b) => a - b);

const b = await chromium.launch({ headless: true, args: ['--use-angle=d3d11', '--ignore-gpu-blocklist'] });
const p = await b.newPage({ viewport: { width: 1280, height: 720 } });
let current = { http: new Set(), console: new Set(), pageErrors: [] };
p.on('response', (r) => { if (r.status() >= 400) current.http.add(`${r.status()} ${new URL(r.url()).pathname}`); });
p.on('console', (m) => { if (m.type() === 'warning' || m.type() === 'error') current.console.add(m.text().slice(0, 300)); });
p.on('pageerror', (e) => current.pageErrors.push(e.message.slice(0, 300)));

const report = { season: SEASON, account: ACCOUNT, started: new Date().toISOString(), maps: [] };
const save = () => fs.writeFileSync(`${OUT}/mapcheck-${SEASON}.json`, JSON.stringify(report, null, 1));

function spots(map) {
  const list = spawns[map]?.spawns ?? [];
  const out = [];
  for (const s of list) {
    if (s.x <= 0 || s.y <= 0 || s.x >= 255 || s.y >= 255) continue;
    if (!out.some((o) => Math.hypot(o.x - s.x, o.y - s.y) < 22)) out.push({ x: s.x, y: s.y });
  }
  return out.slice(0, 9);
}

const sample = () => p.evaluate(() => {
  const g = window.__game;
  const rows = [];
  for (const c of g.online.characters.values()) {
    if (c.monsterNumber === undefined || c.monsterNumber === null || c.monsterNumber < 0) continue;
    const parts = c.model?.parts ?? [];
    // Shown: the mesh and its parents up to the character are visible, or only put away while it is out of view (culled).
    const culled = new Set(c.culledChildren ?? []);
    const drawn = (object) => { for (let o = object; o && o !== c.group; o = o.parent) if (!o.visible && !culled.has(o)) return false; return true; };
    const shown = parts.filter((part) => drawn(part.mesh) && part.mesh.material.visible);
    const look = c.look;
    rows.push({
      number: c.monsterNumber, name: c.name, kind: c.kind, model: c.monsterModel, unknown: look?.unknown ?? null,
      playerShaped: Boolean(c.playerShaped), skin: look?.player?.skin?.fileName ?? null,
      body: look?.body ? `${look.body.dir}/${look.body.fileName}` : (look?.player ? 'player' : null),
      meshes: parts.length, shown: shown.length,
      untextured: shown.filter((part) => !part.mesh.material.map).length,
      seeThrough: shown.filter((part) => { const m = part.mesh.material; return m.transparent && m.opacity < 0.95; }).length,
      additiveOnly: shown.length > 0 && shown.every((part) => part.mesh.material.blending === 2),
      groupVisible: c.group.visible, culled: Boolean(c.culled), dead: Boolean(c.dead), cell: c.cell ? [c.cell.x, c.cell.y] : null,
    });
  }
  return rows;
});

try {
  await p.goto(`${BASE}/index.html`);
  await p.fill('#username', ACCOUNT); await p.fill('#password', ACCOUNT); await p.click('button.primary');
  await p.waitForSelector('.season-column .choice, #servers .choice', { timeout: 120000 });
  const column = SEASON === 's9' ? '.season-column.season-s9 .choice' : '.season-column.season-s6 .choice';
  await p.click(`${column} >> nth=0`).catch(() => p.click('#servers .choice >> nth=0'));
  await p.waitForSelector('#characters .choice', { timeout: 120000 });
  await p.click('#characters .choice >> nth=0');
  await p.waitForFunction(() => window.__viewerReady, null, { timeout: 240000 });
  await p.waitForTimeout(3000);
  await p.evaluate(() => window.__game.onboarding?.close?.());
  const hero = await p.evaluate(() => ({ name: window.__game.hero.name, map: window.__game.mapNumber, season: document.documentElement.dataset.season ?? null }));
  const season = await p.evaluate(async () => (await import('/js/dataRoot.js')).currentSeason());
  report.hero = hero; report.clientSeason = season;
  console.log('hero', JSON.stringify(hero), 'client season', season);
  for (const map of maps) {
    current = { http: new Set(), console: new Set(), pageErrors: [] };
    const entry = { map, name: null, arrived: false, spots: [], monsters: {}, objects: null };
    const list = spots(map);
    const first = list[0];
    await p.evaluate(([name, m, s]) => window.__game.online.say(s ? `/move ${name} ${m} ${s.x} ${s.y}` : `/move ${name} ${m}`), [hero.name, map, first ?? null]);
    try {
      await p.waitForFunction((m) => window.__game.mapNumber === m && !window.__game.online.changingMap, map, { timeout: 60000 });
      entry.arrived = true;
    } catch { entry.arrived = false; }
    if (entry.arrived) {
      await p.waitForTimeout(5000);
      entry.name = await p.evaluate(async (m) => (await import('/js/world.js')).mapName(m), map);
      entry.objects = await p.evaluate(async (m) => {
        const world = await import('/js/world.js');
        const data = await world.loadWorldData(m);
        const files = world.worldModelFiles(data.worldNumber);
        const types = new Map();
        for (const o of data.objects) types.set(o.type, (types.get(o.type) ?? 0) + 1);
        const skipped = [...types].filter(([t]) => !files.has(t));
        return { worldFolder: data.dir, placed: data.objects.length, types: types.size, skippedTypes: skipped.map(([t, n]) => `${t}x${n}`) };
      }, map);
      let best = { count: -1, index: 0 };
      const shotPath = `${OUT}/${String(map).padStart(3, '0')}.png`;
      for (const [index, spot] of (list.length ? list : [null]).entries()) {
        if (spot && index > 0) {
          await p.evaluate(([name, m, s]) => window.__game.online.say(`/move ${name} ${m} ${s.x} ${s.y}`), [hero.name, map, spot]);
          await p.waitForTimeout(3500);
        }
        const rows = await sample();
        const at = await p.evaluate(() => ({ ...window.__game.hero.cell }));
        entry.spots.push({ goal: spot, at: [at.x, at.y], seen: rows.length });
        for (const row of rows) {
          const key = String(row.number);
          const had = entry.monsters[key];
          // Keep the best-drawn sample of a number (a dying or fading one draws less).
          if (!had || (row.shown > had.shown && !row.dead)) entry.monsters[key] = { ...row, count: had?.count ?? 0 };
          entry.monsters[key].count++;
        }
        const monsters = rows.filter((r) => r.kind === 'monster').length;
        if (monsters > best.count) { best = { count: monsters, index }; await p.screenshot({ path: shotPath }); }
      }
    }
    // A .ktx2 the server lacks is tried before the original texture: only a miss of both counts.
    const http = [...current.http];
    entry.http = http.filter((line) => !/\.ktx2$/i.test(line));
    entry.ktx2Misses = http.length - entry.http.length; entry.console = [...current.console].filter((t) => !t.startsWith('Failed to load resource')).slice(0, 40);
    entry.pageErrors = current.pageErrors;
    report.maps.push(entry);
    const faults = Object.values(entry.monsters).filter((m) => m.unknown || m.untextured || m.seeThrough || (m.shown === 0 && m.kind !== 'trap'));
    console.log(`map ${map} ${entry.name ?? ''} arrived=${entry.arrived} types=${Object.keys(entry.monsters).length} faults=${faults.length} http=${entry.http.length} skippedObj=${entry.objects?.skippedTypes.length ?? '-'}`);
    save();
  }
  await p.evaluate((name) => window.__game.online.say(`/move ${name} 0 135 128`), hero.name);
  await p.waitForTimeout(3000);
} catch (e) { console.log('STOPPED', e.message.split('\n')[0]); report.stopped = e.message.slice(0, 500); }
save();
await b.close();
