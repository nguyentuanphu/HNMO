// Lists settings (public properties of configuration classes) that no game code reads: such a setting does nothing
// when an owner changes it. Usage: node unused-settings.mjs <server/src>
import fs from 'fs';
import path from 'path';

const root = process.argv[2];
const walk = (d, out = []) => {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name);
    if (e.isDirectory()) {
      if (!['bin', 'obj', 'node_modules'].includes(e.name)) walk(p, out);
    } else out.push(p);
  }
  return out;
};
const norm = (f) => f.split(path.sep).join('/');
const all = walk(root).filter((f) => /\.(cs|razor)$/.test(f)).map(norm);
const isDef = (f) => /(Configuration|Settings)\.cs$/.test(f) || /(^|\/)DataModel\/Configuration\//.test(f);
const skipCode = (f) => /(^|\/)(Persistence|AdminPanel|DataModel|ServerImporter|Tests?)\//.test(f);
const code = all.filter((f) => !skipCode(f)).map((f) => [f, fs.readFileSync(path.join(root, f), 'utf8')]);
const prop = /public\s+(?:virtual\s+|required\s+)?[\w<>\[\]?,. ]+?\s+(\w+)\s*\{\s*get;/g;
const skip = new Set(['Id', 'Name', 'Description', 'Enabled', 'IsActive', 'Number', 'Designation', 'FileName', 'NameEn', 'NameVi']);
for (const f of all.filter(isDef)) {
  const txt = fs.readFileSync(path.join(root, f), 'utf8');
  for (const m of txt.matchAll(prop)) {
    const n = m[1];
    if (skip.has(n)) continue;
    const re = new RegExp('\\.' + n + '\\b');
    if (!code.some(([p, t]) => p !== f && re.test(t))) console.log(f + '\t' + n);
  }
}
