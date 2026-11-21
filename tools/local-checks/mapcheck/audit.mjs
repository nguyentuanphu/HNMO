// Offline monster/NPC audit with the game's own code (dev/monsterViewer.js) in one season, plus what the viewer does
// not report: transparent meshes, texture loads that failed, and the HTTP errors seen. Usage:
//   node audit.mjs <s6|s9> <out.json> [base url]
import fs from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-maps/client/web/package.json');
const { chromium } = require('playwright');
const [SEASON, OUT, BASE] = [process.argv[2] ?? 's6', process.argv[3] ?? 'audit.json', process.argv[4] ?? 'http://127.0.0.1:8084'];
const b = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const p = await b.newPage();
const failed = [];
p.on('response', (r) => { if (r.status() >= 400) failed.push(`${r.status()} ${new URL(r.url()).pathname}`); });
const consoleLines = [];
p.on('console', (m) => { if (m.type() === 'warning' || m.type() === 'error') consoleLines.push(m.text()); });
await p.route('**/monster-viewer.html', (route) => route.fulfill({
  contentType: 'text/html',
  body: '<!doctype html><html><head><script type="importmap">{"imports":{"three":"/vendor/three/build/three.module.js","three/":"/vendor/three/"}}</script></head><body><canvas id="c" width="64" height="64"></canvas></body></html>',
}));
await p.goto(`${BASE}/monster-viewer.html`);
const rows = await p.evaluate(async (season) => {
  const dataRoot = await import('/js/dataRoot.js');
  dataRoot.setSeason(season);
  const viewer = await import('/js/dev/monsterViewer.js');
  const { monsterCatalog } = await import('/js/game/monsterData.js');
  await viewer.ready();
  const out = [];
  for (const [number, entry] of monsterCatalog().byNumber) {
    const looks = (entry.randomVariants?.length ?? 0) + 1;
    for (let variant = 0; variant < looks; variant++) {
      const { row, character } = await viewer.auditOne(number, variant);
      const parts = character?.model.parts ?? [];
      const shown = parts.filter((part) => part.mesh.visible && part.mesh.material.visible);
      row.transparent = shown.filter((part) => { const m = part.mesh.material; return m.transparent && (m.opacity < 1 || m.blending !== 1); }).length;
      row.files = shown.map((part) => part.mesh.material.map?.name || part.mesh.material.userData?.texture || '').filter(Boolean).slice(0, 8);
      row.maps = entry.maps ?? [];
      character?.release();
      out.push(row);
    }
  }
  return out;
}, SEASON);
fs.writeFileSync(OUT, JSON.stringify({ season: SEASON, rows, failed: [...new Set(failed)], console: [...new Set(consoleLines)].slice(0, 400) }, null, 1));
console.log(SEASON, 'rows', rows.length, 'errors', rows.filter((r) => r.error).length, 'unknown', rows.filter((r) => r.unknown).length,
  'untextured', rows.filter((r) => r.untextured).length, 'transparent', rows.filter((r) => r.transparent).length, 'http errors', new Set(failed).size);
await b.close();
