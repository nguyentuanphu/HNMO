// Draws the admin panel's missing item pictures (item_<group>_<number>_0.png) with the web game's own item renderer
// (src/ui/itemIcons.ts), so a picture shows the item as the game draws it.
// Usage: node client/web/tools/make-item-icons.mjs [web game URL, default http://127.0.0.1:8081]
// Needs the web game running (the development bridge) and `npm run build`. Items without a 3D model are listed.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const iconDir = path.join(repo, 'server/src/Web/ItemEditor/wwwroot/img/items');
const itemDir = path.join(repo, 'client/src/bin/Data/Items');
const base = process.argv[2] ?? 'http://127.0.0.1:8081';

const have = new Set(fs.readdirSync(iconDir).map((f) => /^item_(\d+)_(\d+)_0\.png$/.exec(f)).filter(Boolean).map((m) => `${m[1]}:${m[2]}`));
const missing = [];
for (const file of fs.readdirSync(itemDir).filter((f) => /^Group\d+_.*\.json$/.test(f)).sort()) {
  const data = JSON.parse(fs.readFileSync(path.join(itemDir, file), 'utf8'));
  for (const item of data.items) {
    if (!have.has(`${data.group}:${item.number}`)) missing.push({ g: data.group, n: item.number, w: item.width ?? 1, h: item.height ?? 1 });
  }
}
console.log(`${missing.length} items have no picture`);

const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 960 } });
await page.goto(`${base}/index.html`);
const results = await page.evaluate(async (list) => {
  const items = await import('/js/game/items.js');
  const glow = await import('/js/game/itemGlow.js');
  const icons = await import('/js/ui/itemIcons.js');
  await Promise.all([items.loadItemCatalog(), glow.loadGlowColors()]);
  const modelled = new Map();
  const out = [];
  for (const it of list) {
    if (!modelled.has(it.g)) modelled.set(it.g, new Set(items.itemsWithModels(it.g).map((x) => x.number)));
    if (!modelled.get(it.g).has(it.n)) { out.push({ ...it, skip: 'no model' }); continue; }
    // The item at twice the inventory's size, in a picture with the same margins as the others.
    const inner = document.createElement('canvas');
    inner.width = it.w * 40; inner.height = it.h * 40;
    const drawn = await icons.drawItem(inner, { group: it.g, number: it.n, level: 0, excellent: 0, ancientSet: 0, width: it.w, height: it.h }, { pixelsPerReference: 2 });
    const pixels = inner.getContext('2d').getImageData(0, 0, inner.width, inner.height).data;
    let opaque = 0;
    for (let i = 3; i < pixels.length; i += 4) if (pixels[i] > 16) opaque++;
    if (!drawn || opaque < 20) { out.push({ ...it, skip: 'nothing drawn' }); continue; }
    const canvas = document.createElement('canvas');
    canvas.width = it.w * 40 + 80; canvas.height = it.h * 42 + 84;
    canvas.getContext('2d').drawImage(inner, (canvas.width - inner.width) / 2, (canvas.height - inner.height) / 2);
    out.push({ ...it, png: canvas.toDataURL('image/png') });
  }
  return out;
}, missing);
await browser.close();

let made = 0;
for (const r of results) {
  if (!r.png) { console.log(`no picture for ${r.g}:${r.n} (${r.skip})`); continue; }
  fs.writeFileSync(path.join(iconDir, `item_${r.g}_${r.n}_0.png`), Buffer.from(r.png.split(',')[1], 'base64'));
  made++;
}
console.log(`made ${made} pictures`);
