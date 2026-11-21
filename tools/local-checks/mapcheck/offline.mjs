// Offline world check for maps the GM could not enter (event maps, Season 9-only maps): builds each with the game's
// own code (loadWorldData + createWorld) in a season and lists missing models, untextured object meshes and HTTP misses.
// Usage: node offline.mjs <s6|s9> <maps comma list> <out json>
import fs from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-maps/client/web/package.json');
const { chromium } = require('playwright');
const [SEASON, MAPS, OUT] = process.argv.slice(2);
const b = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const p = await b.newPage();
let http = new Set(); let warn = new Set();
p.on('response', (r) => { if (r.status() >= 400 && !/\.ktx2$/i.test(r.url())) http.add(new URL(r.url()).pathname); });
p.on('console', (m) => { if (m.type() === 'warning') warn.add(m.text().slice(0, 200)); });
await p.route('**/offline.html', (route) => route.fulfill({ contentType: 'text/html', body: '<!doctype html><html><head><script type="importmap">{"imports":{"three":"/vendor/three/build/three.module.js","three/":"/vendor/three/"}}</script></head><body></body></html>' }));
await p.goto('http://127.0.0.1:8084/offline.html');
const out = [];
for (const map of MAPS.split(',').map(Number)) {
  http = new Set(); warn = new Set();
  const row = await p.evaluate(async ([season, m]) => {
    try {
      (await import('/js/dataRoot.js')).setSeason(season);
      const THREE = await import('three');
      const world = await import('/js/world.js');
      const data = await world.loadWorldData(m);
      const built = await world.createWorld(data, { color: new THREE.Color(0), near: 2600, far: 4200 });
      let meshes = 0; const untextured = {};
      for (const c of built.group.children) { const model = c.userData.model; if (!model) continue; meshes++; if (c.material.visible && !c.material.map) untextured[model] = (untextured[model] ?? 0) + 1; }
      return { map: m, name: world.mapName(m), folder: data.dir, objects: data.objects.length, meshes, untextured };
    } catch (e) { return { map: m, error: String(e.message ?? e) }; }
  }, [SEASON, map]);
  row.http = [...http]; row.warnings = [...warn].slice(0, 20);
  out.push(row);
  console.log(map, row.name ?? '', row.error ?? `objects ${row.objects} meshes ${row.meshes} untextured ${Object.keys(row.untextured).length} http ${row.http.length}`);
}
fs.writeFileSync(OUT, JSON.stringify(out, null, 1));
await b.close();
