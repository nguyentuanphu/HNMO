import fs from 'node:fs';

function parseCsv(file) {
  const content = fs.readFileSync(file, 'utf8');
  const lines = content.trim().split('\n');
  const headers = lines[0].split(',');
  return lines.slice(1).map(line => {
    const values = line.split(',');
    const obj = {};
    headers.forEach((h, i) => obj[h] = values[i]);
    return obj;
  });
}

const hnmo = parseCsv('docs/audit/data/hnmo-s9/items.csv');
const hnmoMap = new Map(hnmo.map(r => [`${r.Group}-${r.Number}`, r]));

const s9Content = fs.readFileSync('docs/audit/data/s9/diff-items.txt', 'utf8');

// Find items in HNMO but not in S9
const s9Items = new Set();
const lines = s9Content.split('\n');
let inS9Only = false;
for (const line of lines) {
  if (line.startsWith('## Items present in Season 9 suite but absent from Season 6 suite')) {
    inS9Only = true;
    continue;
  }
  if (line.startsWith('## Items present in Season 6 suite but absent from HNMO')) {
    inS9Only = false;
    continue;
  }
  if (inS9Only && line.match(/^\d+-\d+/)) {
    const key = line.split(' | ')[0].trim();
    s9Items.add(key);
  }
}

// Also get shared items from S9
let inSharedS9 = false;
for (const line of lines) {
  if (line.startsWith('## Shared items where HNMO differs from Season 9 suite')) {
    inSharedS9 = true;
    continue;
  }
  if (line.startsWith('## Shared items where Season 9 suite differs from Season 6 suite')) {
    inSharedS9 = false;
    continue;
  }
  if (inSharedS9 && line.match(/^\d+-\d+/)) {
    const key = line.split(' ')[0].trim();
    s9Items.add(key);
  }
}

console.log('S9 items from diff:', s9Items.size);

// Find HNMO items not in S9
const hnmoOnly = [];
for (const [key, item] of hnmoMap) {
  if (!s9Items.has(key)) {
    hnmoOnly.push(key + ' | ' + item.Name);
  }
}

console.log('HNMO items not in S9:', hnmoOnly.length);
for (const item of hnmoOnly) {
  console.log(item);
}