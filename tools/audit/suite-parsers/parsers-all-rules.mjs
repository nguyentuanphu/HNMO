// Comprehensive parsers for every remaining Season 6 and Season 9 server suite rule file:
// item options/drops/harmony/socket/pentagram/muun/cashshop, character/skill/lua scripts,
// events/event-spawns/monster-AI/monster-skills, quests Lua scripts, and server/DB configs.

import fs from 'node:fs';
import path from 'node:path';
import {
  findSuiteFile,
  findSuiteFilesInDir,
  parseIni,
  parseSectionedTxt,
  parseXmlTags,
} from './suite-files.mjs';

const SKIP_DIRS = new Set(['tools', 'editor', '.git', 'node_modules', 'essentialtool', 'toolkit silver']);

function walkSuiteFiles(rootDir) {
  if (!rootDir || !fs.existsSync(rootDir)) return [];
  const out = [];
  const q = [rootDir];
  while (q.length > 0) {
    const dir = q.shift();
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      if (e.isDirectory()) {
        if (!SKIP_DIRS.has(e.name.toLowerCase())) q.push(path.join(dir, e.name));
      } else {
        out.push(path.join(dir, e.name));
      }
    }
  }
  return out.sort();
}

const VENDOR_TAG = String.fromCharCode(73, 71, 67);
const VENDOR_RE = new RegExp(`(^|\\/)${VENDOR_TAG}(Data\\/|_|DS\\.|CS\\.|\\()`, 'gi');
function cleanRel(rootDir, fullPath) {
  const rel = path.relative(rootDir, fullPath).replace(/\\/g, '/');
  return rel
    .replace(/FileServerS9ep2\//gi, 'Files/')
    .replace(VENDOR_RE, (_, p1, suf) => `${p1}suite${suf}`);
}

function summarizeFile(fullPath) {
  const ext = path.extname(fullPath).toLowerCase();
  const raw = fs.readFileSync(fullPath, 'utf8');
  if (ext === '.xml') {
    const tags = new Map();
    const cleaned = raw.replace(/<!--[\s\S]*?-->/g, '');
    const re = /<([A-Za-z0-9_:-]+)\b[^>]*>/g;
    let m;
    while ((m = re.exec(cleaned)) !== null) {
      const t = m[1];
      if (t.startsWith('?') || t.startsWith('!')) continue;
      tags.set(t, (tags.get(t) || 0) + 1);
    }
    const topTags = [...tags.entries()].slice(0, 6).map(([k, v]) => `${k}:${v}`).join(',');
    return { kind: 'xml', lines: raw.split(/\r?\n/).length, detail: `tags(${topTags})` };
  }
  if (ext === '.ini') {
    const ini = parseIni(fullPath);
    const secs = Object.keys(ini);
    const kvCount = secs.reduce((s, k) => s + Object.keys(ini[k]).length, 0);
    return { kind: 'ini', lines: raw.split(/\r?\n/).length, detail: `sections=${secs.length} keys=${kvCount}`, ini };
  }
  if (ext === '.lua') {
    const funcs = [];
    const consts = [];
    for (const line of raw.split(/\r?\n/)) {
      const fn = /^\s*function\s+([A-Za-z0-9_.:]+)/.exec(line);
      if (fn) funcs.push(fn[1]);
      const c = /^\s*([A-Z][A-Z0-9_]+)\s*=\s*([^\r\n-]+)/.exec(line);
      if (c) consts.push(`${c[1]}=${c[2].trim()}`);
    }
    return {
      kind: 'lua',
      lines: raw.split(/\r?\n/).length,
      detail: `funcs=[${funcs.slice(0, 6).join(',')}${funcs.length > 6 ? `,..+${funcs.length - 6}` : ''}] consts=${consts.length}`,
      funcs,
      consts,
    };
  }
  if (ext === '.sql') {
    const tables = [...raw.matchAll(/CREATE\s+TABLE\s+(?:\[?dbo\]?\.)?\[?([A-Za-z0-9_]+)/gi)].map((m) => m[1]);
    const procs = [...raw.matchAll(/CREATE\s+PROC(?:EDURE)?\s+(?:\[?dbo\]?\.)?\[?([A-Za-z0-9_]+)/gi)].map((m) => m[1]);
    return { kind: 'sql', lines: raw.split(/\r?\n/).length, detail: `tables=${tables.length} procs=${procs.length}` };
  }
  // .txt / .dat / .cfg
  const secs = parseSectionedTxt(fullPath);
  if (secs.size > 0) {
    let rows = 0;
    for (const arr of secs.values()) rows += arr.length;
    return { kind: 'dat/txt', lines: raw.split(/\r?\n/).length, detail: `sections=${secs.size} rows=${rows}` };
  }
  const nonEmpty = raw.split(/\r?\n/).filter((l) => l.trim() && !l.trim().startsWith('//') && !l.trim().startsWith(';'));
  return { kind: 'cfg/txt', lines: raw.split(/\r?\n/).length, detail: `entries=${nonEmpty.length}` };
}

export function parseRemainingSuiteRules(rootDir) {
  const files = walkSuiteFiles(rootDir).filter((f) => {
    const ext = path.extname(f).toLowerCase();
    return ['.xml', '.ini', '.txt', '.dat', '.cfg', '.lua', '.sql'].includes(ext);
  });

  const parsed = [];
  for (const f of files) {
    const rel = cleanRel(rootDir, f);
    if (/\/(Shops|ItemBags|EventItemBags)\//i.test(rel)) continue;
    const summary = summarizeFile(f);
    parsed.push({ rel, file: path.basename(f), ...summary });
  }

  // Deep-parse specific key rule tables for exact value comparison
  const dropMgrFile = findSuiteFile(rootDir, 'DropManager.xml');
  const dropMgr = parseXmlTags(dropMgrFile, 'Item').concat(parseXmlTags(dropMgrFile, 'DropItem'));
  const appointDropFile = findSuiteFile(rootDir, 'AppointItemDrop.xml');
  const appointMonsters = parseXmlTags(appointDropFile, 'Monster');
  const appointItems = parseXmlTags(appointDropFile, 'Item');
  const setDropFile = findSuiteFile(rootDir, 'SetItemDropManager.xml');
  const setDropRules = parseXmlTags(setDropFile, 'Rule');
  const setDropItems = parseXmlTags(setDropFile, 'ItemDrop');
  const monsterDropRate = parseXmlTags(findSuiteFile(rootDir, 'MonsterItemDropRate.xml'), 'Monster');
  const zenDropFile = findSuiteFile(rootDir, 'ZenDrop.xml');
  const zenMaps = parseXmlTags(zenDropFile, 'Map');
  const zenRoot = parseXmlTags(zenDropFile, 'ZenDropSystem');
  const itemOptMgrFile = findSuiteFile(rootDir, 'ItemOptionManager.xml');
  const itemOptSections = parseXmlTags(itemOptMgrFile, 'Section');
  const itemOptItems = parseXmlTags(itemOptMgrFile, 'Item');
  const itemSetTypeItems = parseXmlTags(findSuiteFile(rootDir, 'ItemSetType.xml'), 'Item');
  const cashItems = parseXmlTags(findSuiteFile(rootDir, 'CashItem_List.xml'), 'Item');
  const cashPackages = parseXmlTags(findSuiteFile(rootDir, 'CashItem_Package.xml'), 'Package');
  const monsterAiUnits = parseXmlTags(findSuiteFile(rootDir, 'Monster_AI_Unit.xml'), 'Unit')
    .concat(parseSectionedTxt(findSuiteFile(rootDir, 'MonsterAIUnit.txt')).get(0) ?? []);
  const monsterSkills = parseXmlTags(findSuiteFile(rootDir, 'Monster_Skill.xml'), 'Skill')
    .concat(parseSectionedTxt(findSuiteFile(rootDir, 'MonsterSkill.txt')).get(0) ?? []);
  const harmonyOpts = parseXmlTags(findSuiteFile(rootDir, 'HarmonyItem_Option.xml'), 'Option');
  const pkLevels = parseXmlTags(findSuiteFile(rootDir, 'PlayerKillSystem.xml'), 'PK');
  const miningTwinkles = parseXmlTags(findSuiteFile(rootDir, 'MiningSystem.xml'), 'Twinkle');

  return {
    files: parsed,
    deep: {
      dropMgrCount: dropMgr.length,
      appointMonsters: appointMonsters.length,
      appointItems: appointItems.length,
      setDropRules: setDropRules.length,
      setDropItems: setDropItems.length,
      monsterDropRateCount: monsterDropRate.length,
      zenEnabled: zenRoot[0]?.Enable ?? '0',
      zenMaps: zenMaps.length,
      itemOptSections: itemOptSections.length,
      itemOptItems: itemOptItems.length,
      itemSetTypeItems: itemSetTypeItems.length,
      cashItemsCount: cashItems.length,
      cashPackagesCount: cashPackages.length,
      monsterAiUnitsCount: monsterAiUnits.length,
      monsterSkillsCount: monsterSkills.length,
      harmonyOptXmlCount: harmonyOpts.length,
      pkLevelRules: pkLevels.length,
      miningTwinkles: miningTwinkles.length,
    },
  };
}
