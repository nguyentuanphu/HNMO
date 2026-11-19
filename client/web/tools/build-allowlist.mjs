#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const args = process.argv.slice(2);
if (args.length < 2) {
  console.error('Usage: node build-allowlist.mjs <dataDir> <outFile> [--extra <logFile>]');
  process.exit(1);
}

const dataDir = path.resolve(args[0]);
const outFile = path.resolve(args[1]);
const extraFiles = [];
for (let i = 2; i < args.length - 1; i++) {
  if (args[i] === '--extra') extraFiles.push(path.resolve(args[i + 1]));
}

const webDir = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '..');
const jsDir = path.join(webDir, 'public', 'js');

const { parseBMD, parseWorldObjects } = await import(pathToFileURL(path.join(jsDir, 'formats.js')).href);
const { KNOWN_MAPS, filesFolder, worldModelFiles } = await import(pathToFileURL(path.join(jsDir, 'world.js')).href);
const { tileFileNames } = await import(pathToFileURL(path.join(jsDir, 'terrain.js')).href);

// Index all files in dataDir (lowerRel -> actualRel)
const allFiles = new Map();
const allFilesList = [];
function walk(dir, prefix = '') {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const rel = prefix ? `${prefix}/${entry.name}` : entry.name;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (rel.toLowerCase() === 'packs') continue;
      walk(full, rel);
    } else if (entry.isFile()) {
      allFiles.set(rel.toLowerCase(), rel);
      allFilesList.push({ full, rel, lower: rel.toLowerCase(), size: fs.statSync(full).size });
    }
  }
}
walk(dataDir);

const allowlist = new Set();
const bmdQueue = [];
const parsedBmds = new Set();

function resolvePath(relPath) {
  let clean = relPath.replace(/\\/g, '/').replace(/^\/+/, '');
  if (clean.toLowerCase().startsWith('data/')) clean = clean.slice(5);
  const lower = clean.toLowerCase();
  if (allFiles.has(lower)) return allFiles.get(lower);

  const ext = path.extname(lower);
  const stem = ext ? lower.slice(0, -ext.length) : lower;
  if (ext === '.jpg' || ext === '.jpeg') {
    if (allFiles.has(`${stem}.ozj`)) return allFiles.get(`${stem}.ozj`);
  } else if (ext === '.tga') {
    if (allFiles.has(`${stem}.ozt`)) return allFiles.get(`${stem}.ozt`);
  } else if (ext === '.wav' || ext === '.mp3' || ext === '.ogg') {
    if (allFiles.has(`${stem}.opus`)) return allFiles.get(`${stem}.opus`);
  }
  return null;
}

function addLogical(relPath) {
  const actual = resolvePath(relPath);
  if (!actual) return false;
  if (!allowlist.has(actual)) {
    allowlist.add(actual);
    if (actual.toLowerCase().endsWith('.bmd')) {
      bmdQueue.push(actual);
    }
  }
  return true;
}

function addFolder(relFolder, predicate = () => true) {
  const prefix = `${relFolder.toLowerCase()}/`;
  for (const f of allFilesList) {
    if (f.lower.startsWith(prefix) && predicate(f.rel, f.lower)) {
      addLogical(f.rel);
    }
  }
}

function addBmdAndTextures() {
  while (bmdQueue.length > 0) {
    const rel = bmdQueue.pop();
    if (parsedBmds.has(rel)) continue;
    parsedBmds.add(rel);
    const full = path.join(dataDir, rel);
    const dir = path.posix.dirname(rel);
    let model;
    try {
      const bytes = fs.readFileSync(full);
      model = parseBMD(new Uint8Array(bytes.buffer, bytes.byteOffset, bytes.byteLength));
    } catch {
      continue;
    }
    for (const mesh of model.meshes ?? []) {
      if (!mesh.textureName) continue;
      const texName = path.posix.basename(mesh.textureName.replace(/\\/g, '/'));
      if (!texName) continue;
      // Try same directory first, then sibling/common effect texture directories
      if (!addLogical(`${dir}/${texName}`)) {
        if (dir.toLowerCase().startsWith('object')) {
          const num = dir.slice(6);
          addLogical(`World${num}/${texName}`);
        } else if (dir.toLowerCase() === 'skill' || dir.toLowerCase() === 'effect') {
          addLogical(`Effect/${texName}`) || addLogical(`Skill/${texName}`);
        } else if (dir.toLowerCase() === 'item' || dir.toLowerCase() === 'player') {
          addLogical(`Player/${texName}`) || addLogical(`Item/${texName}`);
        }
      }
    }
  }
}

// 1. Root files & small manifest/JSON folders
addLogical('gate.bmd');
addLogical('Audio/Opus.json');
for (const folder of ['Audio', 'Buffs', 'Effects', 'Grading', 'Items', 'Maps', 'Models', 'Monsters', 'Render']) {
  addFolder(folder);
}

// The cash shop's script and banner (game/cashShopScript.ts asks for them by the version the server sends,
// so no fixed path names them): without these the shop window can't open in a packed folder.
addFolder('InGameShopScript', (_rel, lower) => lower.endsWith('.txt'));
addFolder('InGameShopBanner', (_rel, lower) => /\.(txt|jpg|jpeg|png|tga|ozj|ozt)$/.test(lower));

// 2. Scan web/src and web/public for /data/... and effect/skill/interface paths
function scanSourceFiles(dir, exts) {
  const results = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'node_modules' || entry.name === 'js' || entry.name === 'test-results') continue;
      results.push(...scanSourceFiles(full, exts));
    } else if (exts.some((e) => entry.name.endsWith(e))) {
      results.push(full);
    }
  }
  return results;
}

const sourceFiles = [
  ...scanSourceFiles(path.join(webDir, 'src'), ['.ts']),
  ...scanSourceFiles(path.join(webDir, 'public'), ['.html', '.json', '.rml', '.rcss']),
];

for (const file of sourceFiles) {
  const text = fs.readFileSync(file, 'utf8');
  for (const m of text.matchAll(/['"`]\/data\/([^'"`${}\r\n]+)['"`]/g)) {
    addLogical(m[1]);
  }
  for (const m of text.matchAll(/['"`](Effect|Skill|Player|Monster|NPC|Item|Interface|Local)\/([^'"`${}\r\n]+)['"`]/g)) {
    addLogical(`${m[1]}/${m[2]}`);
  }
  // Tuple patterns like ['Effect', 'wind_foce.bmd']
  for (const m of text.matchAll(/\[\s*['"`](Effect|Skill|Player|Monster|NPC|Item)['"`]\s*,\s*['"`]([^'"`${}\r\n]+)['"`]\s*\]/g)) {
    addLogical(`${m[1]}/${m[2]}`);
  }
  // Audio filenames referenced in TS (e.g. monsterSounds.ts)
  for (const m of text.matchAll(/['"`]([A-Za-z0-9_()-]+\.(?:wav|mp3|ogg|opus))['"`]/gi)) {
    addLogical(`Sound/${m[1]}`) || addLogical(`Music/${m[1]}`);
  }
}

// BITMAPS in render/effects/core.ts
const corePath = path.join(webDir, 'src', 'render', 'effects', 'core.ts');
if (fs.existsSync(corePath)) {
  const coreText = fs.readFileSync(corePath, 'utf8');
  for (const m of coreText.matchAll(/:\s*['"`]([^'"`]+\.(?:jpg|tga|ozj|ozt))['"`]/gi)) {
    addLogical(`Effect/${m[1]}`) || addLogical(`Skill/${m[1]}`) || addLogical(`Item/${m[1]}`);
  }
}

// 3. EffectTypes.json
const effectTypesRel = resolvePath('Effects/EffectTypes.json');
if (effectTypesRel) {
  const json = JSON.parse(fs.readFileSync(path.join(dataDir, effectTypesRel), 'utf8'));
  for (const t of json.types ?? []) {
    if (t.model) {
      addLogical(`Effect/${t.model}`) || addLogical(`Skill/${t.model}`);
    }
  }
}

// 4. Monsters.json and Models.json
for (const mRel of ['Monsters/Monsters.json', 'Monsters/Models.json']) {
  const actual = resolvePath(mRel);
  if (!actual) continue;
  const json = JSON.parse(fs.readFileSync(path.join(dataDir, actual), 'utf8'));
  const scanObj = (obj) => {
    if (!obj || typeof obj !== 'object') return;
    if (typeof obj.file === 'string') {
      addLogical(obj.file);
    }
    if (typeof obj.model === 'string') {
      const dir = typeof obj.dir === 'string' ? obj.dir : 'Monster';
      addLogical(`${dir}/${obj.model}`) || addLogical(`Monster/${obj.model}`) || addLogical(`NPC/${obj.model}`);
    }
    for (const v of Object.values(obj)) {
      if (Array.isArray(v)) v.forEach(scanObj);
      else if (v && typeof v === 'object') scanObj(v);
    }
  };
  scanObj(json);
}

// Include Effect/ and Skill/ assets (glows, skill models, particles, Fenrir, etc.)
addFolder('Effect', (_rel, lower) => /\.(bmd|ozj|ozt|jpg|tga|png)$/.test(lower));
addFolder('Skill', (_rel, lower) => /\.(bmd|ozj|ozt|jpg|tga|png)$/.test(lower));

// 5. Items/Models/PartModels.json & SharedModels.json & Player/player.bmd
addLogical('Player/player.bmd');
for (const iRel of ['Items/Models/PartModels.json', 'Items/Models/SharedModels.json']) {
  const actual = resolvePath(iRel);
  if (!actual) continue;
  const json = JSON.parse(fs.readFileSync(path.join(dataDir, actual), 'utf8'));
  const scanItem = (obj) => {
    if (!obj || typeof obj !== 'object') return;
    if (typeof obj.file === 'string') {
      const dir = typeof obj.dir === 'string' ? obj.dir : 'Item';
      addLogical(`${dir}/${obj.file}`);
    }
    if (typeof obj.model === 'string') {
      const dir = typeof obj.dir === 'string' ? obj.dir : 'Item';
      addLogical(`${dir}/${obj.model}`);
    }
    for (const v of Object.values(obj)) {
      if (Array.isArray(v)) v.forEach(scanItem);
      else if (v && typeof v === 'object') scanItem(v);
    }
  };
  scanItem(json);
}
// Player skin & body textures + Item textures that might be referenced by PartModels
addFolder('Player', (_rel, lower) => /\.(bmd|ozj|ozt|jpg|tga)$/.test(lower));
addFolder('Item', (_rel, lower) => /\.(bmd|ozj|ozt|jpg|tga)$/.test(lower));

// 6. Interface (RmlUi, MapNameOrnament, MacroUI, partCharge1, InGameShop, and top-level HUD textures)
addFolder('Interface/RmlUi');
addFolder('Interface/MapNameOrnament');
addFolder('Interface/MacroUI');
addFolder('Interface/partCharge1');
addFolder('Interface/InGameShop');
addFolder('Interface', (rel, lower) => {
  const withoutRoot = rel.slice('Interface/'.length);
  return !withoutRoot.includes('/') && /\.(tga|jpg|jpeg|png|ozj|ozt|json|rml|rcss)$/.test(lower);
});

// 7. Worlds & Objects for KNOWN_MAPS
const worldNumbers = new Set(KNOWN_MAPS.map((m) => filesFolder(m)));
for (const wn of worldNumbers) {
  const worldDir = `World${wn}`;
  const objDir = `Object${wn}`;
  // EncTerrain files (including siege/crywolf variants like EncTerrain312.att, TerrainLight1/2.jpg)
  addFolder(worldDir, (_rel, lower) => {
    const base = path.posix.basename(lower);
    return base.startsWith('encterrain') || base.startsWith('terrainlight') || base.startsWith('terrainheight') || base.startsWith('mini_map');
  });
  addLogical(`Local/Eng/Minimap/Minimap_World${wn}_eng.bmd`);
  for (const tile of tileFileNames(worldDir)) {
    addLogical(tile);
  }

  const objFile = resolvePath(`${worldDir}/EncTerrain${wn}.obj`);
  if (objFile) {
    try {
      const bytes = fs.readFileSync(path.join(dataDir, objFile));
      const parsed = parseWorldObjects(new Uint8Array(bytes.buffer, bytes.byteOffset, bytes.byteLength));
      const modelTable = worldModelFiles(wn);
      const usedTypes = new Set(parsed.objects.map((o) => o.type));
      for (const t of usedTypes) {
        const modelName = modelTable.get(t);
        if (modelName) {
          addLogical(`${objDir}/${modelName}`);
        }
      }
    } catch {
      // Fallback: keep all bmds in ObjectN if .obj couldn't be parsed
      addFolder(objDir);
    }
  }
}

// 8. Audio: SoundFiles.json + SoundFolders.json + Music
const soundFilesRel = resolvePath('Audio/SoundFiles.json');
if (soundFilesRel) {
  const json = JSON.parse(fs.readFileSync(path.join(dataDir, soundFilesRel), 'utf8'));
  for (const s of json.sounds ?? []) {
    if (s.file) addLogical(s.file);
  }
}
const soundFoldersRel = resolvePath('Audio/SoundFolders.json');
if (soundFoldersRel) {
  const json = JSON.parse(fs.readFileSync(path.join(dataDir, soundFoldersRel), 'utf8'));
  const scanAudio = (val) => {
    if (typeof val === 'string' && (val.includes('/') || /\.(wav|mp3|ogg|opus)$/i.test(val))) {
      addLogical(val) || addLogical(`Sound/${val}`) || addLogical(`Music/${val}`);
    } else if (Array.isArray(val)) {
      val.forEach(scanAudio);
    } else if (val && typeof val === 'object') {
      Object.values(val).forEach(scanAudio);
    }
  };
  scanAudio(json);
}
addFolder('Music');
addLogical('Sound/mWizardAttack1.wav');
addLogical('Sound/mWizardAttack2.wav');

// 9. Merge any runtime-observed paths
for (const extra of extraFiles) {
  if (!fs.existsSync(extra)) continue;
  for (const line of fs.readFileSync(extra, 'utf8').split(/\r?\n/)) {
    const trimmed = line.trim();
    if (trimmed) addLogical(trimmed);
  }
}

// 10. Extract textures from all allowlisted .bmd files
addBmdAndTextures();

const sorted = [...allowlist].sort((a, b) => a.localeCompare(b));
fs.mkdirSync(path.dirname(outFile), { recursive: true });
fs.writeFileSync(outFile, `${sorted.join('\n')}\n`, 'utf8');

// Report stats
let keptBytes = 0;
for (const f of allFilesList) {
  if (allowlist.has(f.rel)) keptBytes += f.size;
}
const totalBytes = allFilesList.reduce((acc, f) => acc + f.size, 0);
console.log(`Allowlist: ${sorted.length}/${allFilesList.length} files, ${(keptBytes / 1048576).toFixed(2)}/${(totalBytes / 1048576).toFixed(2)} MB`);
