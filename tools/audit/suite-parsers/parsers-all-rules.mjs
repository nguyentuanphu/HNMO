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

function cleanRel(rootDir, fullPath) {
  const rel = path.relative(rootDir, fullPath).replace(/\\/g, '/');
  return rel
    .replace(/FileServerS9ep2\//gi, 'Files/')
    .replace(/(^|\/)[A-Z]{3}(?=_|DS\.|CS\.|Data\/|\()/g, '$1suite');
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
  const dropMgr = parseXmlTags(findSuiteFile(rootDir, 'DropManager.xml'), 'DropItem');
  const appointDrop = parseXmlTags(findSuiteFile(rootDir, 'AppointItemDrop.xml'), 'DropItem');
  const setDrop = parseXmlTags(findSuiteFile(rootDir, 'SetItemDropManager.xml'), 'SetItem');
  const monsterDropRate = parseXmlTags(findSuiteFile(rootDir, 'MonsterItemDropRate.xml'), 'Monster');
  const zenDrop = parseXmlTags(findSuiteFile(rootDir, 'ZenDrop.xml'), 'ZenDrop');
  const itemOptMgr = parseXmlTags(findSuiteFile(rootDir, 'ItemOptionManager.xml'), 'Option');
  const cashItems = parseXmlTags(findSuiteFile(rootDir, 'CashItem_List.xml'), 'Item');
  const cashPackages = parseXmlTags(findSuiteFile(rootDir, 'CashItem_Package.xml'), 'Package');
  const monsterAiUnits = parseXmlTags(findSuiteFile(rootDir, 'Monster_AI_Unit.xml'), 'Unit')
    .concat(parseXmlTags(findSuiteFile(rootDir, 'Monster_AI_Unit.xml'), 'AI'));
  const monsterSkills = parseXmlTags(findSuiteFile(rootDir, 'Monster_Skill.xml'), 'Skill')
    .concat(parseXmlTags(findSuiteFile(rootDir, 'Monster_Skill_Unit.xml'), 'Skill'));

  return {
    files: parsed,
    deep: {
      dropMgrCount: dropMgr.length,
      appointDropCount: appointDrop.length,
      setDropCount: setDrop.length,
      monsterDropRateCount: monsterDropRate.length,
      zenDropCount: zenDrop.length,
      itemOptMgrCount: itemOptMgr.length,
      cashItemsCount: cashItems.length,
      cashPackagesCount: cashPackages.length,
      monsterAiUnitsCount: monsterAiUnits.length,
      monsterSkillsCount: monsterSkills.length,
    },
  };
}
