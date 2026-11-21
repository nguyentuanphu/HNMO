// Copies a map check's screenshots into the docs as small JPEGs (960 wide): <out>/<map>-<name>.jpg.
// Usage: node shots.mjs <report json> <shots dir> <docs out dir>
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-maps/client/web/package.json');
const sharp = require('sharp');
const [reportFile, shotsDir, outDir] = process.argv.slice(2);
const report = JSON.parse(fs.readFileSync(reportFile, 'utf8'));
fs.mkdirSync(outDir, { recursive: true });
for (const m of report.maps) {
  const src = path.join(shotsDir, `${String(m.map).padStart(3, '0')}.png`);
  if (!fs.existsSync(src)) continue;
  const slug = (m.name ?? `map-${m.map}`).toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  const out = path.join(outDir, `${String(m.map).padStart(3, '0')}-${slug}.jpg`);
  await sharp(src).resize({ width: 960 }).jpeg({ quality: 72 }).toFile(out);
  m.shot = path.basename(out);
}
fs.writeFileSync(reportFile, JSON.stringify(report, null, 1));
console.log('shots', report.maps.filter((m) => m.shot).length);
