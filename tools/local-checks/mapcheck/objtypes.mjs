// Object types a map's EncTerrainN.obj places, and which model files its object folder lacks. Usage: node objtypes.mjs <obj file> <object folder>
import fs from 'node:fs';
import { pathToFileURL } from 'node:url';
const { parseWorldObjects } = await import(pathToFileURL('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-maps/client/web/public/js/formats.js').href);
const [objFile, folder] = process.argv.slice(2);
const { objects } = parseWorldObjects(new Uint8Array(fs.readFileSync(objFile)));
const types = new Map();
for (const o of objects) types.set(o.type, (types.get(o.type) ?? 0) + 1);
const lower = new Set(fs.readdirSync(folder).map((f) => f.toLowerCase()));
const missing = [...types].filter(([t]) => !lower.has(`object${String(t + 1).padStart(2, '0')}.bmd`));
console.log(`objects ${objects.length}, types ${types.size}, max type ${Math.max(...types.keys())}, missing models ${missing.length}: ${missing.map(([t, n]) => `${t}(Object${String(t + 1).padStart(2, '0')})x${n}`).join(' ')}`);
