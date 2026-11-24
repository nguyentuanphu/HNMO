// Comprehensive parsers for remaining Season 6 and Season 9 server suite gameplay rule files:
// drops, item options/380/harmony/socket/pentagram/muun/cashshop, character/skill/exp/item Lua scripts,
// monster-AI/monster-skills, EXP/Zen/PK/Mining/MuRummy/Marriage/OffLevelling configs.
// Non-rule infrastructure files (.sql, DataServer/ConnectServer/AllowedIP/ServerList/DB/passwords) are excluded.

import fs from 'node:fs';
import path from 'node:path';
import {
  findSuiteFile,
  parseIni,
  parseSectionedTxt,
  parseXmlTags,
} from './suite-files.mjs';

const SKIP_DIRS = new Set([
  'tools',
  'editor',
  '.git',
  'node_modules',
  'essentialtool',
  'toolkit silver',
  'database',
  '1. dataserver',
  '1. dataserver_battlecore',
  '2. connectserver',
]);

const SKIP_FILES_RE = /(?:^|\/)(?:suite_)?(?:AllowedIPList\.xml|ServerList\.xml|ConnectMember\.(?:txt|xml)|IpList\.dat|News\.dat|NewsSystem\.\w+|LangBase\.xml|MapServerInfo[^/]*\.xml|Configuration Files Editor\.txt|Reedme[^/]*\.txt|GameServer_Types\.txt|README\.txt|Dec2\.dat|Enc1\.dat|CheckSum\.dat|English\.xml|CashItem_Info\.xml)$/i;
const INFRA_INI_KEY_RE = /^(?:ServerName|ServerCode|.*IP$|.*Port$|.*User$|.*Pass$|.*DB$|.*DSN$|.*Path$|.*CRC$|LogCreate|MachineID|WAN|LAN|EventChipServer|Allow|EnableHitHack)/i;

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

export function isInfraIniKey(section, key) {
  if (/^(SQL|Database|Connection|AntiHack)$/i.test(section)) return true;
  return INFRA_INI_KEY_RE.test(key);
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
    const rawIni = parseIni(fullPath);
    const ini = {};
    for (const [sec, kv] of Object.entries(rawIni)) {
      const filtered = {};
      for (const [k, v] of Object.entries(kv)) {
        if (!isInfraIniKey(sec, k)) filtered[k] = v;
      }
      if (Object.keys(filtered).length > 0) ini[sec] = filtered;
    }
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
    return ['.xml', '.ini', '.txt', '.dat', '.cfg', '.lua'].includes(ext);
  });

  const parsed = [];
  for (const f of files) {
    const rel = cleanRel(rootDir, f);
    if (/\/(Shops|ItemBags|EventItemBags)\//i.test(rel)) continue;
    if (SKIP_FILES_RE.test(rel)) continue;
    const summary = summarizeFile(f);
    parsed.push({ rel, file: path.basename(f), ...summary });
  }

  // Deep-parse real values across all remaining rule domains
  const expXml = findSuiteFile(rootDir, 'ExpSystem.xml');
  const staticExp = parseXmlTags(expXml, 'StaticExp')[0] ?? {};
  const dynRanges = parseXmlTags(expXml, 'Range');

  const zenFile = findSuiteFile(rootDir, 'ZenDrop.xml');
  const zenRoot = parseXmlTags(zenFile, 'ZenDropSystem')[0] ?? {};
  const zenMaps = parseXmlTags(zenFile, 'Map');

  const appointFile = findSuiteFile(rootDir, 'AppointItemDrop.xml');
  const appointMonsters = parseXmlTags(appointFile, 'Monster');
  const appointItems = parseXmlTags(appointFile, 'Item');

  const setDropFile = findSuiteFile(rootDir, 'SetItemDropManager.xml');
  const setDropRoot = parseXmlTags(setDropFile, 'SetItemDropManager')[0] ?? {};
  const setDropRules = parseXmlTags(setDropFile, 'Rule');
  const setDropItems = parseXmlTags(setDropFile, 'ItemDrop');

  const monsterDropRate = parseXmlTags(findSuiteFile(rootDir, 'MonsterItemDropRate.xml'), 'Monster');
  const excXml = findSuiteFile(rootDir, 'ExcellentOptions.xml');
  const excCommon = parseXmlTags(excXml, 'Option');
  const excDrop = parseXmlTags(excXml, 'OptionDropRate')[0] ?? {};

  const i380Xml = findSuiteFile(rootDir, 'Item380Option.xml');
  const i380Mix = parseXmlTags(i380Xml, 'Mix')[0] ?? {};
  const i380Rate = parseXmlTags(i380Xml, 'SuccessRate')[0] ?? {};
  const i380Items = parseXmlTags(i380Xml, 'Item');

  const itemOptFile = findSuiteFile(rootDir, 'ItemOptionManager.xml');
  const itemOptSections = parseXmlTags(itemOptFile, 'Section');
  const itemOptItems = parseXmlTags(itemOptFile, 'Item');
  const itemSetTypeItems = parseXmlTags(findSuiteFile(rootDir, 'ItemSetType.xml'), 'Item');
  const cashItems = parseXmlTags(findSuiteFile(rootDir, 'CashItem_List.xml'), 'Item');
  const cashPackages = parseXmlTags(findSuiteFile(rootDir, 'CashItem_Package.xml'), 'Package');
  const monsterAiUnits = parseXmlTags(findSuiteFile(rootDir, 'Monster_AI_Unit.xml'), 'Unit')
    .concat(parseSectionedTxt(findSuiteFile(rootDir, 'MonsterAIUnit.txt')).get(0) ?? []);
  const monsterSkills = parseXmlTags(findSuiteFile(rootDir, 'Monster_Skill.xml'), 'Skill')
    .concat(parseSectionedTxt(findSuiteFile(rootDir, 'MonsterSkill.txt')).get(0) ?? []);
  const harmonyOpts = parseXmlTags(findSuiteFile(rootDir, 'HarmonyItem_Option.xml'), 'Option');
  const pkFile = findSuiteFile(rootDir, 'PlayerKillSystem.xml');
  const pkClear = parseXmlTags(pkFile, 'PKClearCommand')[0] ?? {};
  const pkLevels = parseXmlTags(pkFile, 'PK');
  const miningXml = findSuiteFile(rootDir, 'MiningSystem.xml');
  const miningTwinkles = parseXmlTags(miningXml, 'Twinkle');
  const buffCount = parseXmlTags(findSuiteFile(rootDir, 'BuffEffectManager.xml'), 'Buff').length;
  const muRummy = parseIni(findSuiteFile(rootDir, 'MuRummy.ini')).MuRummy ?? {};
  const marryXml = findSuiteFile(rootDir, 'MarriageSystem.xml');
  const marryCfg = parseXmlTags(marryXml, 'MarrySystem')[0] ?? {};
  const offLv = parseXmlTags(findSuiteFile(rootDir, 'OffLevelling.xml'), 'General')[0] ?? {};
  const chaosCards = parseXmlTags(findSuiteFile(rootDir, 'ChaosCard.xml'), 'Option');
  const chaosCardIni = parseSectionedTxt(findSuiteFile(rootDir, 'ChaosCard.txt'));

  return {
    files: parsed,
    deep: {
      expStaticNormal: staticExp.Normal ?? 'n/a',
      expStaticMaster: staticExp.Master ?? 'n/a',
      expDynamicRanges: dynRanges.length,
      zenEnabled: zenRoot.Enable ?? '0',
      zenMultiplyByLv: zenRoot.MultiplyByMonsterLevel ?? '0',
      zenMaps: zenMaps.length,
      appointMonsters: appointMonsters.length,
      appointItems: appointItems.length,
      setDropRate: setDropRoot.DropUseRate ?? 'n/a',
      setDropRules: setDropRules.length,
      setDropItems: setDropItems.length,
      monsterDropRateCount: monsterDropRate.length,
      excOptions: excCommon.length,
      excDropRateCommon: excDrop.Common ?? 'n/a',
      item380Count: i380Items.length,
      item380MixRates: Object.keys(i380Rate).length ? `${i380Rate.Grade1}/${i380Rate.Grade2}/${i380Rate.Grade3}/${i380Rate.Grade4}` : 'ini-only',
      item380MixZen: i380Mix.ReqMoney ?? 'n/a',
      itemOptSections: itemOptSections.length,
      itemOptItems: itemOptItems.length,
      itemSetTypeItems: itemSetTypeItems.length,
      cashItemsCount: cashItems.length,
      cashPackagesCount: cashPackages.length,
      monsterAiUnitsCount: monsterAiUnits.length,
      monsterSkillsCount: monsterSkills.length,
      harmonyOptXmlCount: harmonyOpts.length,
      pkClearEnable: pkClear.Enable ?? 'ini/cfg',
      pkLevelRules: pkLevels.length,
      miningTwinkles: miningTwinkles.length,
      buffCount,
      muRummyFirstScore: muRummy.RewardScore_First ?? 'absent',
      marryEnabled: marryCfg.Enable ?? 'ini',
      offLvEnable: offLv.Enable ?? '0',
      chaosCardCount: chaosCards.length || chaosCardIni.size,
    },
  };
}
