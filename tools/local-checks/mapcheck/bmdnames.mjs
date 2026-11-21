// The name, mesh textures, bone and action counts of BMD files (to tell which Season 9 model is which monster).
// Usage: node bmdnames.mjs <file or folder>...
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
const { parseBMD } = await import(pathToFileURL('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-maps/client/web/public/js/formats.js').href);
const files = process.argv.slice(2).flatMap((p) => (fs.statSync(p).isDirectory() ? fs.readdirSync(p).filter((f) => /\.bmd$/i.test(f)).map((f) => path.join(p, f)) : [p]));
for (const file of files) {
  try {
    const m = parseBMD(new Uint8Array(fs.readFileSync(file)));
    console.log(`${path.basename(file)}\t${m.name ?? ''}\tbones ${m.bones?.length ?? '?'}\tactions ${m.actions?.length ?? '?'}\t${(m.meshes ?? []).map((x) => x.textureName).join(',')}`);
  } catch (e) { console.log(`${path.basename(file)}\tERROR ${e.message}`); }
}
