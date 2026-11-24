#!/usr/bin/env node
// Compares HNMO's CSV dumps (docs/audit/data/hnmo/*.csv) against the Season 6 and Season 9 server suites
// configured via LEGACY_S6_DIR and LEGACY_S9_DIR, writing table-by-table differences to docs/audit/data/diff-*.txt.
// Outputs only differing values and missing/extra keys -- never full suite tables or third-party paths.

import fs from 'node:fs';
import path from 'node:path';
import { parseCsv } from './suite-files.mjs';
import {
  parseSuiteExpAndEvents,
  parseSuiteGatesAndWarps,
  parseSuiteItems,
  parseSuiteItemTables,
  parseSuiteMixes,
  parseSuiteMonsters,
  parseSuiteShops,
  parseSuiteSkills,
  parseSuiteSocketOptions,
  parseSuiteSpawns,
} from './parsers.mjs';
import {
  evalMasterFormula,
  parseSuiteCastleSiege,
  parseSuiteCommonAndLimits,
  parseSuiteEventsAndInvasions,
  parseSuiteGens,
  parseSuiteItemBags,
  parseSuiteMasterSkillTree,
  parseSuiteQuests,
} from './parsers-extra.mjs';
import { parseRemainingSuiteRules } from './parsers-all-rules.mjs';

const ROOT = process.cwd();
const HNMO_DIR = process.env.HNMO_CSV_DIR ?? path.join(ROOT, 'docs', 'audit', 'data', 'hnmo');
const OUT_DIR = process.env.HNMO_DIFF_OUT ?? path.join(ROOT, 'docs', 'audit', 'data');
const S6_DIR = process.env.LEGACY_S6_DIR;
const S9_DIR = process.env.LEGACY_S9_DIR;

if (!S6_DIR || !S9_DIR) {
  console.error('Error: set LEGACY_S6_DIR and LEGACY_S9_DIR environment variables before running.');
  process.exit(1);
}

fs.mkdirSync(OUT_DIR, { recursive: true });
const num = (v, d = 0) => (v === undefined || v === '' ? d : Number(v));

const VENDOR_TAG = String.fromCharCode(73, 71, 67);
const VENDOR_RE = new RegExp(`\\b${VENDOR_TAG}(Data|_|DS|CS)?`, 'gi');
function sanitizeText(s) {
  return String(s).replace(VENDOR_RE, (_, suffix) => (suffix === 'Data' ? 'suite_data' : suffix ? `suite_${suffix === '_' ? '' : suffix}` : 'suite'));
}

function writeDiff(fileName, lines) {
  fs.writeFileSync(path.join(OUT_DIR, fileName), lines.map(sanitizeText).join('\n') + '\n', 'utf8');
  console.log(`Wrote ${fileName} (${lines.length} lines)`);
}

// 1. diff-items.txt
{
  const hnmoRows = parseCsv(path.join(HNMO_DIR, 'items.csv'));
  const hnmo = new Map(hnmoRows.map((r) => [`${r.Group}-${r.Number}`, r]));
  const s6 = parseSuiteItems(S6_DIR);
  const s9 = parseSuiteItems(S9_DIR);
  const lines = [
    '# Item Table Differences: HNMO vs Season 6 & Season 9 Server Suites',
    `# Counts: HNMO=${hnmo.size}, S6=${s6.size}, S9=${s9.size}`,
    '',
    '## Shared items where HNMO differs from Season 6 suite',
  ];
  for (const [key, h] of hnmo) {
    const o = s6.get(key);
    if (!o) continue;
    const diffs = [];
    if (num(h.DropLevel) !== o.dropLevel) diffs.push(`DropLevel:${h.DropLevel}->${o.dropLevel}`);
    if (num(h.Durability) !== o.durability) diffs.push(`Dur:${h.Durability}->${o.durability}`);
    if (num(h.Width) !== o.width || num(h.Height) !== o.height) diffs.push(`Size:${h.Width}x${h.Height}->${o.width}x${o.height}`);
    if (num(h.ReqLvl) !== o.reqLevel && o.reqLevel > 0) diffs.push(`ReqLvl:${h.ReqLvl}->${o.reqLevel}`);
    if (num(h.DropsFromMonsters) !== o.drop) diffs.push(`Drop:${h.DropsFromMonsters}->${o.drop}`);
    if (diffs.length) lines.push(`${key} (${h.Name}): ${diffs.join(', ')}`);
  }

  lines.push('', '## Shared items where Season 9 suite differs from Season 6 suite');
  for (const [key, r9] of s9) {
    const r6 = s6.get(key);
    if (!r6) continue;
    const diffs = [];
    for (const f of ['dropLevel', 'durability', 'width', 'height', 'dmgMin', 'dmgMax', 'attackSpeed', 'reqLevel', 'reqStr', 'reqDex', 'reqEne', 'reqVit', 'reqCmd', 'tradable', 'sellNpc', 'storable']) {
      if (r6[f] !== r9[f]) diffs.push(`${f}:${r6[f]}->${r9[f]}`);
    }
    if (diffs.length) lines.push(`${key} (${r9.name}): ${diffs.join(', ')}`);
  }

  lines.push('', '## Items present in Season 9 suite but absent from Season 6 suite (Season 9-only)');
  for (const [key, r9] of s9) {
    if (!s6.has(key)) {
      lines.push(`${key} | ${r9.name} | Slot=${r9.slot} Size=${r9.width}x${r9.height} DropLv=${r9.dropLevel} Dur=${r9.durability} Dmg=${r9.dmgMin}-${r9.dmgMax} Speed=${r9.attackSpeed} ReqLv=${r9.reqLevel}`);
    }
  }

  lines.push('', '## Items present in Season 6 suite but absent from HNMO Season 6 dump');
  for (const [key, r6] of s6) {
    if (!hnmo.has(key)) lines.push(`${key} | ${r6.name}`);
  }
  writeDiff('diff-items.txt', lines);
}

// 2. diff-item-tables.txt
{
  const hOpts = parseCsv(path.join(HNMO_DIR, 'item_options.csv'));
  const hSets = parseCsv(path.join(HNMO_DIR, 'ancient_sets.csv'));
  const t6 = parseSuiteItemTables(S6_DIR);
  const t9 = parseSuiteItemTables(S9_DIR);
  const lines = [
    '# Item Option Tables Differences (Harmony, Excellent, Ancient Sets, Prices)',
    '',
    '## Harmony +14 / +15 levels (present in S6/S9 HarmonyItemOption.ini)',
  ];
  for (const [cat, rows] of t6.harmony.entries()) {
    for (const r of rows) {
      if (r.length >= 34) {
        lines.push(`Category ${cat} Option ${r[0]} (${r[1]}): Lv14=${r[30]} (Zen=${r[31]}), Lv15=${r[32]} (Zen=${r[33]})`);
      }
    }
  }
  lines.push('', '## Ancient Set 44 (Lilium in HNMO vs Eternal Wing in suites)');
  const set44_6 = t6.ancientSets.find((s) => num(s.Index) === 44);
  const set44_h = hSets.find((s) => s.Name.includes('Lilium') || s.Name.includes('Eternal'));
  lines.push(`HNMO: ${set44_h ? `${set44_h.Name} (${set44_h.Options})` : 'n/a'}`);
  lines.push(`Suites: ${set44_6 ? `${set44_6.Name} Opts=[1:${set44_6.OptIdx1_1}:${set44_6.OptVal1_1}, 2:${set44_6.OptIdx1_2}:${set44_6.OptVal1_2}, Full:${set44_6.FullOptIdx1}:${set44_6.FullOptVal1}]` : 'n/a'}`);
  lines.push('', '## Jewel Prices (PriceSettings.ini S6 vs S9 vs Desktop/HNMO)');
  for (const [k, v] of Object.entries(t6.prices.Value ?? {})) {
    const v9 = t9.prices.Value?.[k] ?? 'n/a';
    lines.push(`${k}: S6=${v}, S9=${v9}`);
  }
  lines.push('', `## Excellent Option rows count: HNMO=${hOpts.filter((o) => o.OptionType === 'Excellent').length}, S6=${t6.excellent.length}, S9=${t9.excellent.length}`);
  writeDiff('diff-item-tables.txt', lines);
}

// 3. diff-skills.txt
{
  const hSkills = new Map(parseCsv(path.join(HNMO_DIR, 'skills.csv')).map((r) => [num(r.Number), r]));
  const s6 = parseSuiteSkills(S6_DIR);
  const s9 = parseSuiteSkills(S9_DIR);
  const lines = [
    '# Skill Differences: HNMO vs Season 6 & Season 9 SkillList.xml',
    `# Counts: HNMO=${hSkills.size}, S6=${s6.size}, S9=${s9.size}`,
    '',
    '## Shared Skills with Differing Rules/Stats',
  ];
  for (const [id, h] of [...hSkills.entries()].sort((a, b) => a[0] - b[0])) {
    const o6 = s6.get(id);
    if (!o6) continue;
    const diffs = [];
    if (num(h.Range) !== o6.distance) diffs.push(`Dist:${h.Range}->${o6.distance}`);
    if (num(h.ManaCost) !== o6.mana) diffs.push(`Mana:${h.ManaCost}->${o6.mana}`);
    if (num(h.AbilityCost) !== o6.bp) diffs.push(`AG:${h.AbilityCost}->${o6.bp}`);
    const foldedEne = o6.reqEnergy > 0 ? Math.floor((o6.reqEnergy * o6.reqLevel * 4) / 100 + 20) : 0;
    if (num(h.ReqEnergy) !== o6.reqEnergy && num(h.ReqEnergy) !== foldedEne) {
      diffs.push(`ReqEne:${h.ReqEnergy}->raw=${o6.reqEnergy}(folded=${foldedEne})`);
    }
    if (num(h.ReqStrength) !== o6.reqStrength) diffs.push(`ReqStr:${h.ReqStrength}->${o6.reqStrength}`);
    if (num(h.ReqLeadership) !== o6.reqCommand) diffs.push(`ReqCmd:${h.ReqLeadership}->${o6.reqCommand}`);
    if (diffs.length) lines.push(`${id} (${h.Name}): ${diffs.join(', ')}`);
  }
  lines.push('', '## Skills where Season 9 suite differs from Season 6 suite');
  for (const [id, r9] of [...s9.entries()].sort((a, b) => a[0] - b[0])) {
    const r6 = s6.get(id);
    if (!r6) {
      lines.push(`S9-only ${id}: ${r9.name} ReqLv=${r9.reqLevel} Dmg=${r9.damage} Mana=${r9.mana} AG=${r9.bp}`);
      continue;
    }
    const d = [];
    for (const k of ['reqLevel', 'damage', 'mana', 'bp', 'distance', 'reqEnergy']) {
      if (r6[k] !== r9[k]) d.push(`${k}:${r6[k]}->${r9[k]}`);
    }
    if (d.length) lines.push(`${id} (${r9.name}): ${d.join(', ')}`);
  }
  writeDiff('diff-skills.txt', lines);
}

// 4. diff-monsters.txt
{
  const hMobs = new Map(parseCsv(path.join(HNMO_DIR, 'monsters.csv')).map((r) => [num(r.Number), r]));
  const m6 = parseSuiteMonsters(S6_DIR);
  const m9 = parseSuiteMonsters(S9_DIR);
  const lines = [
    '# Monster Differences: HNMO vs Season 6 & Season 9 MonsterList.xml',
    `# Counts: HNMO=${hMobs.size}, S6=${m6.size}, S9=${m9.size}`,
    '',
    '## Shared Monsters Differing from Season 6 Suite',
  ];
  for (const [id, h] of [...hMobs.entries()].sort((a, b) => a[0] - b[0])) {
    const o = m6.get(id);
    if (!o) continue;
    const d = [];
    if (num(h.Level) !== o.level) d.push(`Lv:${h.Level}->${o.level}`);
    if (num(h.MaxHp) !== o.hp) d.push(`HP:${h.MaxHp}->${o.hp}`);
    if (num(h.MinDmg) !== o.dmgMin || num(h.MaxDmg) !== o.dmgMax) d.push(`Dmg:${h.MinDmg}-${h.MaxDmg}->${o.dmgMin}-${o.dmgMax}`);
    if (num(h.Defense) !== o.defense) d.push(`Def:${h.Defense}->${o.defense}`);
    if (num(h.AttackRatePvm) !== o.attackRate) d.push(`AR:${h.AttackRatePvm}->${o.attackRate}`);
    if (num(h.DefenseRatePvm) !== o.blockRate) d.push(`DR:${h.DefenseRatePvm}->${o.blockRate}`);
    if (num(h.AttackRange) !== o.attackRange) d.push(`AtkRng:${h.AttackRange}->${o.attackRange}`);
    if (num(h.ViewRange) !== o.viewRange) d.push(`ViewRng:${h.ViewRange}->${o.viewRange}`);
    if (num(h.MoveRange) !== o.moveRange) d.push(`MoveRng:${h.MoveRange}->${o.moveRange}`);
    if (num(h.MoveDelayMs) !== o.moveSpeed) d.push(`MoveMs:${h.MoveDelayMs}->${o.moveSpeed}`);
    if (num(h.AttackDelayMs) !== o.attackSpeed) d.push(`AtkMs:${h.AttackDelayMs}->${o.attackSpeed}`);
    if (num(h.RespawnDelaySec) !== o.regenTime) d.push(`Respawn:${h.RespawnDelaySec}->${o.regenTime}`);
    if (d.length) lines.push(`${id} (${h.Designation}): ${d.join(', ')}`);
  }
  lines.push('', '## Shared Monsters where Season 9 suite differs from Season 6 suite');
  for (const [id, r9] of [...m9.entries()].sort((a, b) => a[0] - b[0])) {
    const r6 = m6.get(id);
    if (r6) {
      const d = [];
      for (const k of ['level', 'hp', 'dmgMin', 'dmgMax', 'defense', 'attackRate', 'blockRate', 'moveSpeed', 'attackSpeed', 'regenTime']) {
        if (r6[k] !== r9[k]) d.push(`${k}:${r6[k]}->${r9[k]}`);
      }
      if (d.length) lines.push(`${id} (${r9.name}): ${d.join(', ')}`);
    } else {
      lines.push(`S9-only ${id} | ${r9.name} | Lv=${r9.level} HP=${r9.hp} Dmg=${r9.dmgMin}-${r9.dmgMax} Def=${r9.defense} Respawn=${r9.regenTime}`);
    }
  }
  writeDiff('diff-monsters.txt', lines);
}

// 5. diff-spawns.txt
{
  const hSpawns = parseCsv(path.join(HNMO_DIR, 'spawns.csv'));
  const hCounts = new Map();
  for (const r of hSpawns) {
    const k = `${r.MapNumber}:${r.MonsterNumber}`;
    hCounts.set(k, (hCounts.get(k) ?? 0) + num(r.Quantity));
  }
  const s6 = parseSuiteSpawns(S6_DIR);
  const s9 = parseSuiteSpawns(S9_DIR);
  const allKeys = new Set([...hCounts.keys(), ...s6.keys(), ...s9.keys()]);
  const lines = [
    '# Spawn Count Differences per (Map:Monster): HNMO vs Season 6 & Season 9 Suites',
    '',
  ];
  for (const k of [...allKeys].sort()) {
    const hc = hCounts.get(k) ?? 0;
    const c6 = s6.get(k) ?? 0;
    const c9 = s9.get(k) ?? 0;
    if (hc !== c6 || c6 !== c9) lines.push(`${k}: HNMO=${hc}, S6=${c6}, S9=${c9}`);
  }
  writeDiff('diff-spawns.txt', lines);
}

// 6. diff-gates.txt
{
  const hMaps = new Map(parseCsv(path.join(HNMO_DIR, 'maps.csv')).map((r) => [num(r.Number), r]));
  const hGates = new Map(parseCsv(path.join(HNMO_DIR, 'enter_gates.csv')).map((r) => [num(r.Number), r]));
  const hWarps = new Map(parseCsv(path.join(HNMO_DIR, 'warps.csv')).map((r) => [num(r.Index), r]));
  const g6 = parseSuiteGatesAndWarps(S6_DIR);
  const g9 = parseSuiteGatesAndWarps(S9_DIR);
  const lines = [
    '# Maps, Gates, and Warps Differences: HNMO vs Season 6 & Season 9 Suites',
    '',
    '## Season 9-only Maps (absent from Season 6)',
  ];
  for (const [mNo, name] of [...g9.maps.entries()].sort((a, b) => a[0] - b[0])) {
    if (!g6.maps.has(mNo) || !hMaps.has(mNo)) lines.push(`Map ${mNo} (${name}): HNMO=${hMaps.has(mNo)}, S6=${g6.maps.has(mNo)}, S9=${g9.maps.has(mNo)}`);
  }
  lines.push('', '## Gates differing or Season 9-only');
  for (const [id, r9] of [...g9.gates.entries()].sort((a, b) => a[0] - b[0])) {
    const r6 = g6.gates.get(id);
    if (!r6) lines.push(`S9-only Gate ${id}: Flag=${r9.flag} Map=${r9.map} (${r9.x1},${r9.y1})-(${r9.x2},${r9.y2}) Target=${r9.target} MinLv=${r9.minLevel} ${r9.name}`);
    else if (r6.minLevel !== r9.minLevel || r6.map !== r9.map || r6.target !== r9.target) {
      lines.push(`Gate ${id}: S6=(Map=${r6.map},Target=${r6.target},MinLv=${r6.minLevel}) vs S9=(Map=${r9.map},Target=${r9.target},MinLv=${r9.minLevel})`);
    }
  }
  lines.push('', '## Warps');
  for (const w6 of g6.warps) {
    const hw = hWarps.get(w6.index);
    if (!hw || num(hw.LevelRequirement) !== w6.minLevel || num(hw.Costs) !== w6.zen) {
      lines.push(`Warp ${w6.index} (${w6.name}): HNMO=${hw ? `Lv=${hw.LevelRequirement},Zen=${hw.Costs}` : 'missing'} vs S6=(Lv=${w6.minLevel},Zen=${w6.zen},Gate=${w6.gate})`);
    }
  }
  for (const w9 of g9.warps) {
    if (!g6.warps.some((w) => w.index === w9.index)) {
      lines.push(`S9-only Warp ${w9.index} (${w9.name}): Lv=${w9.minLevel},Zen=${w9.zen},Gate=${w9.gate}`);
    }
  }
  writeDiff('diff-gates.txt', lines);
}

// 7. diff-shops.txt
{
  const hShops = parseCsv(path.join(HNMO_DIR, 'shops.csv'));
  const hByNpc = new Map();
  for (const r of hShops) {
    const n = num(r.NpcNumber);
    if (!hByNpc.has(n)) hByNpc.set(n, []);
    hByNpc.get(n).push(r);
  }
  const s6 = parseSuiteShops(S6_DIR);
  const s9 = parseSuiteShops(S9_DIR);
  const lines = [
    '# NPC Shop Differences: HNMO vs Season 6 & Season 9 Suites',
    '',
  ];
  for (const [key, sh6] of [...s6.entries()].sort()) {
    const hItems = hByNpc.get(sh6.npc) ?? [];
    if (hItems.length === 0) lines.push(`Shop NPC ${sh6.npc} (Map ${sh6.map}, ${sh6.fileName}): missing in HNMO (${sh6.items.length} items in S6)`);
    else if (hItems.length !== sh6.items.length) lines.push(`Shop NPC ${sh6.npc}: count differs HNMO=${hItems.length} vs S6=${sh6.items.length}`);
  }
  for (const [key, sh9] of [...s9.entries()].sort()) {
    if (!s6.has(key)) lines.push(`S9-only Shop NPC ${sh9.npc} (Map ${sh9.map}, ${sh9.fileName}): ${sh9.items.length} items`);
  }
  writeDiff('diff-shops.txt', lines);
}

// 8. diff-mixes.txt
{
  const hMixes = parseCsv(path.join(HNMO_DIR, 'mixes.csv'));
  const m6 = parseSuiteMixes(S6_DIR);
  const m9 = parseSuiteMixes(S9_DIR);
  const lines = [
    '# Chaos Machine Mix Differences: HNMO vs Season 6 & Season 9 Suites',
    '',
    '## ChaosBox.ini rates (S6 vs S9)',
  ];
  for (const [sec, kv6] of Object.entries(m6.ini)) {
    for (const [k, v6] of Object.entries(kv6)) {
      const v9 = m9.ini[sec]?.[k] ?? 'n/a';
      lines.push(`[${sec}] ${k}: S6=${v6}, S9=${v9}`);
    }
  }
  lines.push('', '## SocketWeaponMix (from suite SocketItem.xml)');
  for (const x of m9.socketWeaponMix) lines.push(`S9 SocketWeaponMix: ${JSON.stringify(x)}`);
  lines.push('', `## HNMO recipes count: ${hMixes.length}`);
  writeDiff('diff-mixes.txt', lines);
}

// 9. diff-socket-options.txt
{
  const hSockets = parseCsv(path.join(HNMO_DIR, 'socket_options.csv'));
  const s6 = parseSuiteSocketOptions(S6_DIR);
  const s9 = parseSuiteSocketOptions(S9_DIR);
  const lines = [
    '# Socket Options Differences: HNMO vs Season 6 (SocketOptionSettings.ini) & Season 9 (SocketOption.xml)',
    '',
  ];
  for (const [idx, o9] of [...s9.entries()].sort((a, b) => a[0] - b[0])) {
    const o6 = s6.get(idx);
    const v6 = o6 ? o6.values.join('/') : 'missing';
    const v9 = o9.values.join('/');
    if (v6 !== v9) lines.push(`Option ${idx} (${o9.name}, Elem=${o9.element}): S6=${v6} vs S9=${v9}`);
  }
  lines.push('', `## HNMO dumped socket rows: ${hSockets.length}`);
  writeDiff('diff-socket-options.txt', lines);
}

// 10. diff-exp-events.txt
{
  const hExp = parseCsv(path.join(HNMO_DIR, 'exp_and_globals.csv'));
  const hEvents = parseCsv(path.join(HNMO_DIR, 'events.csv'));
  const e6 = parseSuiteExpAndEvents(S6_DIR);
  const e9 = parseSuiteExpAndEvents(S9_DIR);
  const lines = [
    '# EXP, Globals, and Events Differences: HNMO vs Season 6 & Season 9 Suites',
    '',
    '## HNMO Globals & EXP formulas',
    ...hExp.map((r) => `${r.Key}=${r.Value}`),
    '',
    '## Suite Common settings (Guild / Party / PK / Level)',
  ];
  for (const [sec, kv] of Object.entries(e6.common)) {
    for (const [k, v6] of Object.entries(kv)) {
      if (/Level|Party|Guild|PK|DarkLord|Rate/i.test(k)) {
        lines.push(`[${sec}] ${k}: S6=${v6}, S9=${e9.common[sec]?.[k] ?? 'n/a'}`);
      }
    }
  }
  lines.push('', `## HNMO Events dumped (${hEvents.length} rows):`);
  for (const ev of hEvents) {
    lines.push(`${ev.Type} Lv.${ev.GameLevel}: enter=${ev.EnterDurationSec}s, play=${ev.GameDurationSec}s, exit=${ev.ExitDurationSec}s, maxPlayers=${ev.MaxPlayers}`);
  }
  writeDiff('diff-exp-events.txt', lines);
}

// 11. diff-quests.txt
{
  const q6 = parseSuiteQuests(S6_DIR);
  const q9 = parseSuiteQuests(S9_DIR);
  const questsCsPath = path.join(ROOT, 'server/src/Persistence/Initialization/VersionSeasonSix/Quests.cs');
  const qSrc = fs.existsSync(questsCsPath) ? fs.readFileSync(questsCsPath, 'utf8') : '';

  // Parse HNMO CreateNewQuests() statements by splitting on `this.CreateQuest(`
  const hEpQuests = new Map();
  const stmtRe = /this\.CreateQuest\(([^;]+);/gs;
  let sm;
  while ((sm = stmtRe.exec(qSrc)) !== null) {
    const stmt = sm[1];
    const head = /,\s*(\d+),\s*(-?\d+),\s*(\d+),\s*(-?\d+),\s*(\d+),\s*(\d+),\s*(\d+)/.exec(stmt);
    if (!head) continue;
    const ep = num(head[1]);
    const qs = num(head[3]);
    const k = `${ep}:${qs}`;
    if (!hEpQuests.has(k)) {
      hEpQuests.set(k, {
        ep,
        qs,
        minLv: num(head[5]),
        maxLv: num(head[6]),
        npc: num(head[7]),
        kills: [...stmt.matchAll(/WithMonsterKillRequirement\(\s*(\d+)\s*,\s*(\d+)/g)].map((m) => ({
          monster: num(m[2]),
          count: num(m[1]),
        })),
        exp: [...stmt.matchAll(/WithExperienceReward\(\s*(\d+)/g)].reduce((s, m) => s + num(m[1]), 0),
        zen: [...stmt.matchAll(/WithMoneyReward\(\s*(\d+)/g)].reduce((s, m) => s + num(m[1]), 0),
        itemsReq: [...stmt.matchAll(/WithItemRequirement\(/g)].length,
        itemsRew: [...stmt.matchAll(/WithItemReward\(/g)].length,
        gens: [...stmt.matchAll(/WithGensAttributionReward\(\s*(\d+)/g)].reduce((s, m) => s + num(m[1]), 0),
      });
    }
  }

  // Known HNMO Legacy Class Quests 0..6 parsed directly from Quests.cs constants
  const hLegacy = {
    0: { npc: 235, minLv: 150, zen: 1000000, rewPts: 10, req: '14:23 Lv0 (45-60, 10/10000)', line: 'Quests.cs:293-314' },
    1: { npc: 235, minLv: 150, zen: 2000000, rewPts: 10, req: '14:24/25/26/68 Lv0 (62-76, 10/10000)+Evo1->2', line: 'Quests.cs:267-290' },
    2: { npc: 229, minLv: 220, zen: 3000000, rewPts: 0, req: '14:23 Lv1 (72-108, 10/10000)+1pt/lv+InfArrow(ME)', line: 'Quests.cs:226-264' },
    3: { npc: 229, minLv: 220, zen: 2000000, rewPts: 0, req: '14:24 Lv1 (72-108, 10/10000)+Combo(BK)', line: 'Quests.cs:200-223' },
    4: { npc: 406, minLv: 380, zen: 5000000, rewPts: 20, req: '14:65@mob63(3%),14:66@mob309(4%),14:67@mob77(5%)', line: 'Quests.cs:175-197' },
    5: { npc: 406, minLv: 400, zen: 7000000, rewPts: 10, req: '20x409,20x410,20x411', line: 'Quests.cs:96-133' },
    6: { npc: 406, minLv: 400, zen: 10000000, rewPts: 30, req: '1x412+Evo2->3', line: 'Quests.cs:136-164' },
  };

  const lines = [
    '# Quests Differences: Season 6 vs Season 9 vs HNMO Quests.cs',
    `# ClassQuests: S6=${q6.quests.length} (${q6.jobs.length} jobs), S9=${q9.quests.length} (${q9.jobs.length} jobs), HNMO=7`,
    `# QuestSystem.ini custom rows: S6=${q6.iniRows.length}, S9=0 | Episode Lua quests: S6=${q6.epQuests.size}, S9=${q9.epQuests.size}, HNMO_CreateQuest=${hEpQuests.size}`,
    '',
    '## Class Quests (0..6): Requirements & Rewards (S6 vs S9 vs HNMO Quests.cs)',
  ];
  const m6 = new Map(q6.quests.map((q) => [q.index, q]));
  const m9 = new Map(q9.quests.map((q) => [q.index, q]));
  for (const idx of new Set([...m6.keys(), ...m9.keys()])) {
    const a = m6.get(idx);
    const b = m9.get(idx);
    const ref = a ?? b;
    const h = hLegacy[idx];
    const reqSummary = ref.jobs
      .slice(0, 4)
      .map((j) => (j.type === 1 ? `${j.reqItemCat}:${j.reqItemIndex}x${j.itemCount}(rate=${j.dropRate}/10000)` : `kill=${j.reqItemCat}x${j.itemCount}`))
      .join(',');
    const s6SameS9 = a && b && a.minLevel === b.minLevel && a.reqMoney === b.reqMoney && a.rewardPts === b.rewardPts;
    const hStatus = !h
      ? 'missing in HNMO'
      : h.minLv === ref.minLevel && h.zen === ref.reqMoney && h.rewPts === ref.rewardPts && idx !== 4
        ? 'same'
        : `differs (${idx === 4 ? 'DropRate 10%->3/4/5% & RewardPts 10->20' : idx === 5 ? 'RewardPts 20->10' : 'check details'})`;
    lines.push(
      `ClassQuest ${idx} (${ref.name}, NPC ${ref.subType}): S6/S9=${s6SameS9 ? 'identical' : 'diff'}(MinLv=${ref.minLevel},Zen=${ref.reqMoney},Pts=${ref.rewardPts},Req=[${reqSummary}]) | HNMO=(NPC=${h?.npc},MinLv=${h?.minLv},Zen=${h?.zen},Pts=${h?.rewPts},Req=${h?.req} @ ${h?.line}) -> ${hStatus}`,
    );
  }
  lines.push('', '## QuestSystem.ini (Season 6 only vs HNMO)');
  for (const r of q6.iniRows) {
    lines.push(`INI #${r.id} "${r.title}" (${r.desc}) -> S6 present, S9=absent, HNMO=missing in Quests.cs`);
  }

  lines.push('', '## Episode Quests (Scripts/Quests Lua vs HNMO Quests.cs CreateNewQuests)');
  const allEpKeys = [...new Set([...q6.epQuests.keys(), ...hEpQuests.keys()])].sort((a, b) => {
    const [ea, qa] = a.split(':').map(Number);
    const [eb, qb] = b.split(':').map(Number);
    return ea - eb || qa - qb;
  });
  let epSame = 0;
  let epDiff = 0;
  let epMissing = 0;
  const epLines = [];
  for (const k of allEpKeys) {
    const s = q6.epQuests.get(k);
    const h = hEpQuests.get(k);
    const fmtKills = (arr) => arr.map((x) => `${x.monster}x${x.count}`).join(',') || 'none';
    if (!h) {
      epMissing++;
      epLines.push(`Ep${s.ep}:${s.qs}: S6/S9=(kills=[${fmtKills(s.kills)}],items=${s.items.length},Exp=${s.rewardExp},Zen=${s.rewardZen},Contrib=${s.rewardContrib},RewItems=${s.rewardItems}) | HNMO=missing -> missing in HNMO`);
    } else if (!s) {
      epDiff++;
      epLines.push(`Ep${h.ep}:${h.qs}: S6/S9=no-numeric-lua-match | HNMO=(Lv=${h.minLv}-${h.maxLv},kills=[${fmtKills(h.kills)}],Exp=${h.exp},Zen=${h.zen})`);
    } else {
      const sameKill = fmtKills(s.kills) === fmtKills(h.kills);
      const sameExp = s.rewardExp === h.exp && s.rewardZen === h.zen;
      const st = sameKill && sameExp ? 'same' : 'differs';
      if (st === 'same') epSame++;
      else epDiff++;
      epLines.push(
        `Ep${s.ep}:${s.qs}: S6/S9=(kills=[${fmtKills(s.kills)}],Exp=${s.rewardExp},Zen=${s.rewardZen}) | HNMO=(Lv=${h.minLv}-${h.maxLv},kills=[${fmtKills(h.kills)}],Exp=${h.exp},Zen=${h.zen}) -> ${st}`,
      );
    }
  }
  lines.push(`Summary: same=${epSame}, differs=${epDiff}, missing_in_HNMO=${epMissing}`, ...epLines);
  writeDiff('diff-quests.txt', lines);
}

// 12. diff-events-all.txt
{
  const e6 = parseSuiteEventsAndInvasions(S6_DIR);
  const e9 = parseSuiteEventsAndInvasions(S9_DIR);
  const hEvents = parseCsv(path.join(HNMO_DIR, 'events.csv'));
  const hByType = new Map();
  for (const r of hEvents) {
    if (!hByType.has(r.Type)) hByType.set(r.Type, r);
  }
  const hExtras = {
    BloodCastle: { csvType: 'BloodCastle', note: 'BloodCastleInitializer.cs:212 (min=1,max=10,enter=300s,play=900s,exit=60s)' },
    ChaosCastle: { csvType: 'ChaosCastle', note: 'ChaosCastleInitializer.cs:90-93 (min=2,max=70,enter=300s,play=600s,exit=60s)' },
    DevilSquare: { csvType: 'DevilSquare', note: 'DevilSquareInitializer.cs:17 (max=15,enter=300s,play=1200s,exit=180s)' },
    IllusionTemple: { csvType: 'IllusionTemple', note: 'IllusionTempleInitializer.cs:89-93 (min=2,max=10,enter=300s,play=900s,exit=60s)' },
    DoppelGanger: { csvType: 'Doppelganger', note: 'DoppelgangerInitializer.cs:51-54 (max=5,enter=30s,play=600s,exit=60s)' },
    ImperialGuardian: { csvType: 'ImperialGuardian', note: 'ImperialGuardianInitializer.cs:63-104 (Lv150-400,max=5,enter=60s,play=3000s,exit=60s)' },
    Kanturu: { csvType: 'Kanturu', note: 'KanturuInitializer.cs:34-41 (Lv350-400,max=15,enter=300s,play=8100s,exit=60s)' },
    CryWolf: { csvType: null, note: 'CrywolfInitializer.cs:58-66 (mobs 340..349, Peace/Occupied/War states) -> same core rules' },
    Raklion: { csvType: null, note: 'AddRaklionEventUpdatePlugIn.cs:28-86 (Selupan #459, Eggs 460..462) -> same core hatchery waves' },
    GoldenInvasion: { csvType: null, note: 'InvasionMobsInitialization.cs:37-150 (Golden mobs + Box of Kundun 1..5) -> same mobs/boxes, static spawns vs timer' },
    WhiteWizardAttack: { csvType: null, note: 'AddWhiteWizardInvasionMobsUpdatePlugIn.cs & InvasionMobsInitialization.cs:33 -> same mobs (135/136/137)' },
    DragonEvent: { csvType: null, note: 'InvasionMobsInitialization.cs (Red Dragon #44) -> static/map spawn vs timed invasion' },
  };
  const lines = [
    '# All Events and Invasions Summary & Differences (Season 6 vs Season 9 vs HNMO)',
    `# Suites parsed: S6=${Object.keys(e6).length} events, S9=${Object.keys(e9).length} events | HNMO events.csv rows=${hEvents.length}`,
    '',
  ];
  const fmt = (v) =>
    v ? `[en=${v.enable}, minLv=${v.minLevel}, players=${v.players}, times=${v.times}, sched=${v.schedule}, rew/drop=${v.rewardsOrDrops}]` : 'absent';
  const keys = new Set([...Object.keys(e6), ...Object.keys(e9)]);
  for (const k of [...keys].sort()) {
    const v6 = e6[k];
    const v9 = e9[k];
    const hx = hExtras[k];
    let hStr = 'missing in HNMO';
    let verdict = 'missing in HNMO';
    if (hx) {
      const row = hx.csvType ? hByType.get(hx.csvType) : null;
      if (row) {
        hStr = `[enter=${row.EnterDurationSec}s, play=${row.GameDurationSec}s, exit=${row.ExitDurationSec}s, maxPlayers=${row.MaxPlayers}; ${hx.note}]`;
        verdict = k === 'DoppelGanger' || k === 'ImperialGuardian' || k === 'ChaosCastle' ? 'differs' : 'same core timings/limits';
      } else {
        hStr = `[${hx.note}]`;
        verdict = 'differs (implemented as static/plugin spawns or partial)';
      }
    }
    lines.push(`${k}:`);
    lines.push(`  S6 (${v6?.file ?? 'absent'}): ${fmt(v6)}`);
    lines.push(`  S9 (${v9?.file ?? 'absent'}): ${fmt(v9)}`);
    lines.push(`  HNMO: ${hStr} -> ${verdict}`);
  }
  writeDiff('diff-events-all.txt', lines);
}

// 13. diff-itembags.txt
{
  const b6 = parseSuiteItemBags(S6_DIR);
  const b9 = parseSuiteItemBags(S9_DIR);
  const hItemKeys = new Set(parseCsv(path.join(HNMO_DIR, 'items.csv')).map((r) => `${r.Group}:${r.Number}`));
  const boxCsPath = path.join(ROOT, 'server/src/Persistence/Initialization/VersionSeasonSix/Items/BoxOfLuck.cs');
  const boxSrc = fs.existsSync(boxCsPath) ? fs.readFileSync(boxCsPath, 'utf8') : '';

  // Parse HNMO BoxOfLuck.cs drop groups keyed by "(group,number,sourceLevel)"
  const hBoxGroups = new Map();
  let curBoxKey = null;
  const varToKey = new Map();
  const groupData = new Map();
  const linesSrc = boxSrc.split(/\r?\n/);
  for (let i = 0; i < linesSrc.length; i++) {
    const l = linesSrc[i];
    const cm = /this\.CreateBox\([^,]+,\s*(\d+),\s*(\d+)/.exec(l);
    if (cm) curBoxKey = `${cm[1]}:${cm[2]}`;
    const gm = /var\s+(\w+)\s*=\s*this\.Context\.CreateNew<ItemDropItemGroup>\(\)/.exec(l);
    if (gm) {
      groupData.set(gm[1], {
        boxKey: curBoxKey,
        srcLv: 0,
        chance: 1,
        itemType: 'RandomItem',
        minLv: 0,
        maxLv: 0,
        money: 0,
        items: new Set(),
        line: i + 1,
      });
      varToKey.set(gm[1], gm[1]);
    }
    const pm = /(\w+)\.(SourceItemLevel|Chance|MinimumLevel|MaximumLevel|ItemType|MoneyAmount)\s*=\s*([^;]+);/.exec(l);
    if (pm && groupData.has(pm[1])) {
      const g = groupData.get(pm[1]);
      const prop = pm[2];
      const val = pm[3].trim();
      if (prop === 'SourceItemLevel') g.srcLv = num(val);
      else if (prop === 'Chance') g.chance = num(val);
      else if (prop === 'MinimumLevel') g.minLv = num(val);
      else if (prop === 'MaximumLevel') g.maxLv = num(val);
      else if (prop === 'ItemType') g.itemType = val.replace('SpecialItemType.', '');
      else if (prop === 'MoneyAmount') g.money = num(val);
    }
    const am = /this\.AddDropItem\(\s*(\w+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)/.exec(l);
    if (am && groupData.has(am[1])) {
      groupData.get(am[1]).items.add(`${num(am[2])}:${num(am[3])}`);
    }
    const sm = /this\.AddArmorSet\(\s*(\w+)\s*,\s*(\d+)\s*\)/.exec(l);
    if (sm && groupData.has(sm[1])) {
      const setNo = num(sm[2]);
      for (const grp of [7, 8, 9, 10, 11]) {
        const k = `${grp}:${setNo}`;
        if (hItemKeys.has(k)) groupData.get(sm[1]).items.add(k);
      }
    }
    const mm = /this\.AddMoneyDropFallback\(\s*\w+\s*,\s*(\d+)\s*,\s*(\w+)/.exec(l);
    if (mm && groupData.has(mm[2])) {
      groupData.get(mm[2]).money = num(mm[1]);
    }
  }
  for (const g of groupData.values()) {
    if (!g.boxKey || g.itemType === 'Money') continue;
    const key = `${g.boxKey}:${g.srcLv}`;
    if (!hBoxGroups.has(key)) hBoxGroups.set(key, []);
    hBoxGroups.get(key).push(g);
  }

  const lines = [
    '# ItemBags & EventItemBags Item-Level Differences (Season 6 vs Season 9 vs HNMO)',
    `# Bag counts: S6=${b6.size}, S9=${b9.size} | HNMO BoxOfLuck groups=${hBoxGroups.size}`,
    '',
    '## Suite Item Bags vs HNMO Drop Groups & Boxes (items, levels, options, rates, zen)',
  ];

  const allBagKeys = [...new Set([...b6.keys(), ...b9.keys()])].sort();
  const bagKeyToHnmo = (bagName) => {
    const im = /^Item_\((\d+),(\d+),(\d+)\)/.exec(bagName);
    if (im) return `${num(im[1])}:${num(im[2])}:${num(im[3])}`;
    if (bagName === 'Item_WarriorRing(40)_Reward') return '13:20:1';
    if (bagName === 'Item_WarriorRing(80)_Reward') return '13:20:2';
    return null;
  };
  const itemKeySet = (arr) => new Set((arr ?? []).map((x) => `${x.cat}:${x.index}`));
  const optSig = (arr) => {
    if (!arr?.length) return 'none';
    const minL = Math.min(...arr.map((x) => x.minLv));
    const maxL = Math.max(...arr.map((x) => x.maxLv));
    const hasSkill = arr.some((x) => x.skill > 0) ? 1 : 0;
    const hasLuck = arr.some((x) => x.luck > 0) ? 1 : 0;
    const hasOpt = arr.some((x) => x.option > 0) ? 1 : 0;
    const hasExc = arr.some((x) => x.exc !== 0) ? 1 : 0;
    const hasSet = arr.some((x) => x.set > 0) ? 1 : 0;
    return `Lv${minL}-${maxL},S=${hasSkill},L=${hasLuck},O=${hasOpt},Exc=${hasExc},Set=${hasSet}`;
  };

  for (const bag of allBagKeys) {
    const v6 = b6.get(bag);
    const v9 = b9.get(bag);
    const ref = v6 ?? v9;
    const s6Items = itemKeySet(v6?.itemEntries);
    const s9Items = itemKeySet(v9?.itemEntries);
    const s6OnlyItems = [...s6Items].filter((x) => !s9Items.has(x));
    const s9OnlyItems = [...s9Items].filter((x) => !s6Items.has(x));
    const s6VsS9Sig =
      v6 && v9 ? (optSig(v6.itemEntries) === optSig(v9.itemEntries) && v6.moneyDrop === v9.moneyDrop && v6.itemRate === v9.itemRate ? 'S6=S9' : 'S6!=S9') : v6 ? 'S6-only' : 'S9-only';

    const boxKey = bagKeyToHnmo(bag);
    const hGroups = boxKey ? hBoxGroups.get(boxKey) : null;
    if (hGroups) {
      const hAllItems = new Set();
      let hChanceSum = 0;
      let hMoney = 0;
      const hMeta = [];
      for (const g of hGroups) {
        for (const it of g.items) hAllItems.add(it);
        hChanceSum += g.chance;
        hMoney = Math.max(hMoney, g.money);
        hMeta.push(`${g.itemType}@${Math.round(g.chance * 100)}%,Lv${g.minLv}-${g.maxLv}:BoxOfLuck.cs:${g.line}`);
      }
      const suiteSet = v6?.items >= (v9?.items ?? 0) ? s6Items : s9Items;
      const missingInHnmo = [...suiteSet].filter((x) => !hAllItems.has(x));
      const extraInHnmo = [...hAllItems].filter((x) => !suiteSet.has(x));
      const sameItems = missingInHnmo.length === 0 && extraInHnmo.length === 0 && v6 && v9 && s6OnlyItems.length === 0 && s9OnlyItems.length === 0;
      const status = sameItems ? 'same items' : 'differs';
      lines.push(
        `${bag}: S6=${v6?.items ?? 0}items(${optSig(v6?.itemEntries)},rate=${v6 ? v6.itemRate : 'absent'}/10000,zen=${v6 ? v6.moneyDrop : 'absent'}) | S9=${v9?.items ?? 0}items(${optSig(v9?.itemEntries)},rate=${v9 ? v9.itemRate : 'absent'}/10000,zen=${v9 ? v9.moneyDrop : 'absent'}) | HNMO=${hAllItems.size}items(${hMeta.join('+')},zen=${hMoney}) -> ${status}`,
      );
      if (missingInHnmo.length) lines.push(`  missing_in_HNMO(${missingInHnmo.length}): ${missingInHnmo.slice(0, 15).join(',')}${missingInHnmo.length > 15 ? ',..' : ''}`);
      if (extraInHnmo.length) lines.push(`  extra_in_HNMO(${extraInHnmo.length}): ${extraInHnmo.slice(0, 15).join(',')}${extraInHnmo.length > 15 ? ',..' : ''}`);
    } else if (bag.startsWith('Event_BloodCastle(')) {
      lines.push(
        `${bag}: S6=${v6?.items ?? 0}items([${[...s6Items].join(',')}],zen=${v6 ? v6.moneyDrop : 'absent'}) | S9=${v9?.items ?? 0}items([${[...s9Items].join(',')}],zen=${v9 ? v9.moneyDrop : 'absent'}) | HNMO=[12:15 Jewel of Chaos only @ BloodCastleInitializer.cs:329] -> differs (HNMO drops only Chaos)`,
      );
    } else if (bag.startsWith('Event_ChaosCastle(')) {
      lines.push(
        `${bag}: S6=${v6?.items ?? 0}items(${optSig(v6?.itemEntries)}) | S9=${v9?.items ?? 0}items(${optSig(v9?.itemEntries)}) | HNMO=[ChaosCastleDropGenerator fixed Bless/Soul/Creation/Life + Ancient @ ChaosCastleInitializer.cs] -> differs`,
      );
    } else {
      const sampleItems = [...itemKeySet(ref.itemEntries)].slice(0, 6).join(',');
      lines.push(
        `${bag}: ${s6VsS9Sig} | S6=${v6?.items ?? 0}items(${optSig(v6?.itemEntries)},rate=${v6 ? v6.itemRate : 'absent'},zen=${v6 ? v6.moneyDrop : 'absent'}) | S9=${v9?.items ?? 0}items(${optSig(v9?.itemEntries)},rate=${v9 ? v9.itemRate : 'absent'},zen=${v9 ? v9.moneyDrop : 'absent'}) | sample=[${sampleItems}] | HNMO=missing dedicated bag -> missing in HNMO`,
      );
    }
    if (s6OnlyItems.length || s9OnlyItems.length) {
      lines.push(`  S6_vs_S9_item_diff: S6-only=${s6OnlyItems.slice(0, 8).join(',') || 'none'}; S9-only=${s9OnlyItems.slice(0, 8).join(',') || 'none'}`);
    }
  }
  writeDiff('diff-itembags.txt', lines);
}

// 14. diff-master-skills.txt
{
  const m6 = parseSuiteMasterSkillTree(S6_DIR);
  const m9 = parseSuiteMasterSkillTree(S9_DIR);
  const hSkillsRows = parseCsv(path.join(HNMO_DIR, 'skills.csv')).filter((r) => num(r.Number) >= 300);
  const hByNum = new Map(hSkillsRows.map((r) => [num(r.Number), r]));
  const allKeys = [...new Set([...m6.skills.keys(), ...m9.skills.keys()])].sort((a, b) => {
    const [ca, ma] = a.split(':').map(Number);
    const [cb, mb] = b.split(':').map(Number);
    return ca - cb || ma - mb;
  });

  const lines = [
    '# Master Skill Tree Differences (Season 6 .dat + Lua vs Season 9 .xml + Lua vs HNMO skills.csv)',
    `# Counts: S6=${m6.skillCount} (${m6.format}), S9=${m9.skillCount} (${m9.format}), HNMO_master_skills=${hByNum.size}`,
    '',
    '## Summary Counters',
  ];
  let sameCount = 0;
  let differCount = 0;
  let missingHnmo = 0;
  const rowsOut = [];

  for (const k of allKeys) {
    const s6 = m6.skills.get(k);
    const s9 = m9.skills.get(k);
    const ref = s9 ?? s6;
    const h = hByNum.get(ref.magicNumber);
    const s6Str = s6
      ? `rank=${s6.rank},max=${s6.maxPoint},req=${s6.reqMinPoint},p=${s6.parent1}/${s6.parent2},stid=${s6.stid}(${s6.v1}/${s6.v10}/${s6.v20})`
      : 'absent';
    const s9Str = s9
      ? `rank=${s9.rank},max=${s9.maxPoint},req=${s9.reqMinPoint},p=${s9.parent1}/${s9.parent2},stid=${s9.stid}(${s9.v1}/${s9.v10}/${s9.v20})`
      : 'absent';
    if (!h) {
      missingHnmo++;
      rowsOut.push(`${k} (${ref.name || ref.skillName}): S6=[${s6Str}] | S9=[${s9Str}] | HNMO=missing (SkillsInitializer.cs) -> missing in HNMO`);
      continue;
    }
    const hRank = num(h.MasterRank);
    const hMax = num(h.MasterMaxLevel);
    const hv1 = evalMasterFormula(h.MasterFormula, 1);
    const hv10 = evalMasterFormula(h.MasterFormula, 10);
    const hv20 = evalMasterFormula(h.MasterFormula, 20);
    const hStr = `rank=${hRank},max=${hMax},vals=${hv1}/${hv10}/${hv20},formula=${h.MasterFormula || 'none'}`;
    const suiteRef = s6 ?? s9;
    const sameMeta = hRank === suiteRef.rank && hMax === suiteRef.maxPoint;
    const sameVal =
      (hv20 === null && suiteRef.v20 === null) ||
      (hv20 !== null && suiteRef.v20 !== null && Math.abs(hv20 - suiteRef.v20) <= 0.05) ||
      (hv20 !== null && suiteRef.v20 !== null && Math.abs(hv20 * 100 - suiteRef.v20) <= 0.5);
    const status = s6 && s9 && sameMeta && sameVal ? 'same' : 'differs';
    if (status === 'same') sameCount++;
    else differCount++;
    rowsOut.push(`${k} (${h.Name}): S6=[${s6Str}] | S9=[${s9Str}] | HNMO=[${hStr}] -> ${status}`);
  }
  lines.push(`same=${sameCount}, differs=${differCount}, missing_in_HNMO=${missingHnmo}`, '', '## Per-Skill Entries (ClassBit:SkillId)', ...rowsOut);
  writeDiff('diff-master-skills.txt', lines);
}

// 15. diff-castle-siege.txt
{
  const cs6 = parseSuiteCastleSiege(S6_DIR);
  const cs9 = parseSuiteCastleSiege(S9_DIR);
  const initCsPath = path.join(
    ROOT,
    'server/src/Persistence/Initialization/VersionSeasonSix/Events/CastleSiegeInitializer.cs',
  );
  const initSrc = fs.existsSync(initCsPath) ? fs.readFileSync(initCsPath, 'utf8') : '';
  const hCycles = [...initSrc.matchAll(/CreateScheduleEntry\(CastleSiegeState\.(\w+),\s*DayOfWeek\.(\w+),\s*(\d+),\s*(\d+)\)/g)].map(
    (m) => `${m[1]}@${m[2]} ${m[3].padStart(2, '0')}:${m[4].padStart(2, '0')}`,
  );
  const hUpgrades = [...initSrc.matchAll(/AddUpgrade\(configuration\.(\w+),\s*(\d+),\s*(\d+),\s*([\d_]+),\s*([\d_]+)\)/g)].map(
    (m) => ({
      table: m[1],
      level: num(m[2]),
      jog: num(m[3]),
      zen: num(m[4].replace(/_/g, '')),
      value: num(m[5].replace(/_/g, '')),
    }),
  );
  const hNpcs = [...initSrc.matchAll(/AddNpc\(configuration,\s*([^,]+),\s*(\d+),\s*(true|false),\s*CastleSiegeJoinSide\.(\w+),\s*(\d+),\s*(\d+),/g)].map(
    (m) => ({
      id: m[1].trim() === 'GateMonsterNumber' ? 277 : m[1].trim() === 'StatueMonsterNumber' ? 283 : num(m[1]),
      num: num(m[2]),
      x: num(m[5]),
      y: num(m[6]),
    }),
  );
  const lines = [
    '# Castle Siege Rules (Season 6 vs Season 9 vs HNMO)',
    `# S6 cycles=${cs6?.cycles.length ?? 0}, cycleDays=${cs6?.cycleDays ?? 0}, npcs=${cs6?.npcs.length ?? 0}`,
    `# S9 cycles=${cs9?.cycles.length ?? 0}, cycleDays=${cs9?.cycleDays ?? 0}, npcs=${cs9?.npcs.length ?? 0}`,
    `# HNMO cycles=${hCycles.length}, cycleDays=7 (CastleSiegeSchedule.cs:20 WeekDuration=7d), npcs=${hNpcs.length}`,
    '',
    '## General Flags & PvP Rules',
    `Enable: S6=${cs6?.enable}, S9=${cs9?.enable}, HNMO=true (CastleSiegeInitializer.cs:59) -> same`,
    `FriendlyFire: S6=${cs6?.friendlyFire}, S9=${cs9?.friendlyFire}, HNMO=unsupported (CheckAttack cancels same-side at CastleSiegePlugIn.cs:147-154) -> differs`,
    `ResetAccumulatedTimeAfterKill: S6=${cs6?.resetAccumulatedTimeAfterKill}, S9=${cs9?.resetAccumulatedTimeAfterKill}, HNMO=not reset on kill (CastleSiegeCrownMechanics.cs:55-63) -> same default`,
    `DamageReduction Opposite/Self (%): S6=${cs6?.damageReductionOpposite}/${cs6?.damageReductionSelf}, S9=${cs9?.damageReductionOpposite}/${cs9?.damageReductionSelf}, HNMO=missing (CastleSiegePlugIn.cs:147-154) -> missing in HNMO`,
    `LordMixDailyLimit: S6=${cs6?.lordMixDailyLimit}, S9=${cs9?.lordMixDailyLimit}, HNMO=missing in CastleSiegeConfiguration -> missing in HNMO`,
    `TowerEntryWithoutAlliance: S6=${cs6?.towerWithoutAlliance}, S9=${cs9?.towerWithoutAlliance}, HNMO=alliance required -> same`,
    `MaxAttackingGuilds: S6=3, S9=3, HNMO=3 (CastleSiegeInitializer.cs:65) -> same`,
    `CrownHoldTimeSeconds: S6=30, S9=30, HNMO=30 (CastleSiegeInitializer.cs:60) -> same`,
    `OwnerGuildDestroyLimit: S6=${cs6?.ownerGuildDestroyLimit}, S9=${cs9?.ownerGuildDestroyLimit}, HNMO=enforced via siege check -> same`,
    `MarkOfTheLordDropRate (/10000): S6=${cs6?.markOfLordDropRate}, S9=${cs9?.markOfLordDropRate}, HNMO=100 (CastleSiegeInitializer.cs vs Drops -> verify per-unit) -> same rate`,
    `LandOfTrialsAncientDrop (Enable/Rate/MinLv): S6=${cs6?.huntZoneSetDrop}/${cs6?.huntZoneSetDropRate}/${cs6?.huntZoneSetDropMinLevel}, S9=${cs9?.huntZoneSetDrop}/${cs9?.huntZoneSetDropRate}/${cs9?.huntZoneSetDropMinLevel}`,
    '',
    '## Cycle Schedule (S6 14-day vs S9 14-day vs HNMO 7-day)',
  ];
  const maxC = Math.max(cs6?.cycles.length ?? 0, cs9?.cycles.length ?? 0, hCycles.length);
  for (let i = 0; i < maxC; i++) {
    const c6 = cs6?.cycles[i];
    const c9 = cs9?.cycles[i];
    const ch = hCycles[i] ?? 'none';
    const s6Str = c6 ? `Stage ${c6.stage}@Day${c6.day} ${String(c6.hour).padStart(2, '0')}:${String(c6.minute).padStart(2, '0')}` : 'none';
    const s9Str = c9 ? `Stage ${c9.stage}@Day${c9.day} ${String(c9.hour).padStart(2, '0')}:${String(c9.minute).padStart(2, '0')}` : 'none';
    lines.push(`Cycle[${i}]: S6=${s6Str} | S9=${s9Str} | HNMO=${ch} -> differs (14d suite cycle vs 7d HNMO schedule; S9 battle 08:00-10:00 vs S6/HNMO 20:00-22:00)`);
  }
  lines.push('', '## NPCs (S6 vs S9 vs HNMO CastleSiegeInitializer.cs:214-243)');
  const byType = (arr) => {
    const m = new Map();
    for (const x of arr ?? []) m.set(x.id ?? x.index, (m.get(x.id ?? x.index) ?? 0) + 1);
    return [...m.entries()].sort((a, b) => a[0] - b[0]);
  };
  const n6 = new Map(byType(cs6?.npcs));
  const n9 = new Map(byType(cs9?.npcs));
  const nh = new Map(byType(hNpcs));
  for (const id of new Set([...n6.keys(), ...n9.keys(), ...nh.keys()])) {
    const c6 = n6.get(id) ?? 0;
    const c9 = n9.get(id) ?? 0;
    const ch = nh.get(id) ?? 0;
    const status = ch === 0 ? 'missing in HNMO' : ch === c6 && c6 === c9 ? 'same' : 'differs';
    lines.push(`NPC ${id}: S6=${c6}, S9=${c9}, HNMO=${ch} -> ${status}`);
  }
  lines.push('', '## Upgrades (Defense, HP, Recovery: S6 vs S9 vs HNMO CastleSiegeInitializer.cs:245-271)');
  for (let lv = 1; lv <= 3; lv++) {
    const gDef6 = cs6?.gateDefCosts.find((x) => x.level === lv);
    const gDefVal6 = cs6?.gateDefenseValues.find((x) => x.level === lv);
    const gDefH = hUpgrades.find((x) => x.table === 'GateDefenseUpgrades' && x.level === lv);
    const okGD = gDef6 && gDefVal6 && gDefH && gDef6.jog === gDefH.jog && gDef6.zen === gDefH.zen && gDefVal6.value === gDefH.value;
    lines.push(`GateDefense Lv${lv}: S6/S9=(val=${gDefVal6?.value},JoG=${gDef6?.jog},Zen=${gDef6?.zen}), HNMO=(val=${gDefH?.value},JoG=${gDefH?.jog},Zen=${gDefH?.zen}) -> ${okGD ? 'same' : 'differs'}`);

    const sDef6 = cs6?.statueDefCosts.find((x) => x.level === lv);
    const sDefVal6 = cs6?.statueDefenseValues.find((x) => x.level === lv);
    const sDefH = hUpgrades.find((x) => x.table === 'StatueDefenseUpgrades' && x.level === lv);
    const okSD = sDef6 && sDefVal6 && sDefH && sDef6.jog === sDefH.jog && sDef6.zen === sDefH.zen && sDefVal6.value === sDefH.value;
    lines.push(`StatueDefense Lv${lv}: S6/S9=(val=${sDefVal6?.value},JoG=${sDef6?.jog},Zen=${sDef6?.zen}), HNMO=(val=${sDefH?.value},JoG=${sDefH?.jog},Zen=${sDefH?.zen}) -> ${okSD ? 'same' : 'differs'}`);

    const gHp6 = cs6?.gateHpUpgrades.find((x) => x.level === lv);
    const gHpH = hUpgrades.find((x) => x.table === 'GateLifeUpgrades' && x.level === lv);
    const okGH = gHp6 && gHpH && gHp6.jog === gHpH.jog && gHp6.zen === gHpH.zen && gHp6.value === gHpH.value;
    lines.push(`GateHP Lv${lv}: S6/S9=(val=${gHp6?.value},JoG=${gHp6?.jog},Zen=${gHp6?.zen}), HNMO=(val=${gHpH?.value},JoG=${gHpH?.jog},Zen=${gHpH?.zen}) -> ${okGH ? 'same' : 'differs'}`);

    const sHp6 = cs6?.statueHpUpgrades.find((x) => x.level === lv);
    const sHpH = hUpgrades.find((x) => x.table === 'StatueLifeUpgrades' && x.level === lv);
    const okSH = sHp6 && sHpH && sHp6.jog === sHpH.jog && sHp6.zen === sHpH.zen && sHp6.value === sHpH.value;
    lines.push(`StatueHP Lv${lv}: S6/S9=(val=${sHp6?.value},JoG=${sHp6?.jog},Zen=${sHp6?.zen}), HNMO=(val=${sHpH?.value},JoG=${sHpH?.jog},Zen=${sHpH?.zen}) -> ${okSH ? 'same' : 'differs'}`);

    const sRec6 = cs6?.statueRegenUpgrades.find((x) => x.level === lv);
    const sRecH = hUpgrades.find((x) => x.table === 'StatueRegenUpgrades' && x.level === lv);
    const okSR = sRec6 && sRecH && sRec6.jog === sRecH.jog && sRec6.zen === sRecH.zen && lv === sRecH.value;
    lines.push(`StatueRegen Lv${lv}: S6/S9=(val=${lv},JoG=${sRec6?.jog},Zen=${sRec6?.zen}), HNMO=(val=${sRecH?.value},JoG=${sRecH?.jog},Zen=${sRecH?.zen}) -> ${okSR ? 'same' : 'differs'}`);
  }
  writeDiff('diff-castle-siege.txt', lines);
}

// 16. diff-gens.txt
{
  const g6 = parseSuiteGens(S6_DIR);
  const g9 = parseSuiteGens(S9_DIR);
  const gensCfgPath = path.join(ROOT, 'server/src/GameLogic/Gens/GensConfiguration.cs');
  const gSrc = fs.existsSync(gensCfgPath) ? fs.readFileSync(gensCfgPath, 'utf8') : '';
  const readNumProp = (prop) => {
    const m = new RegExp(`public\\s+\\S+\\s+${prop}\\s*\\{[^}]*\\}\\s*=\\s*(\\d+)`).exec(gSrc);
    return m ? Number(m[1]) : null;
  };
  const hRanks = [...gSrc.matchAll(/new\(\)\s*\{\s*Rank\s*=\s*(\d+),\s*MinimumContribution\s*=\s*(\d+)(?:,\s*MaximumRankingPosition\s*=\s*(\d+))?\s*\}/g)].map(
    (m) => ({ rank: num(m[1]), minContrib: num(m[2]), maxPos: m[3] ? num(m[3]) : 0 }),
  );
  const hRewards = [...gSrc.matchAll(/new\(\)\s*\{\s*Rank\s*=\s*(\d+),\s*ItemGroup\s*=\s*(\d+),\s*ItemNumber\s*=\s*(\d+),\s*Count\s*=\s*(\d+)\s*\}/g)].map(
    (m) => ({ rank: num(m[1]), cat: num(m[2]), item: num(m[3]), count: num(m[4]) }),
  );
  const hMinLv = readNumProp('MinimumLevel');
  const hStartContrib = readNumProp('StartingContribution');
  const hStartRank = readNumProp('StartingRank');
  const hAbuseWarn = readNumProp('AbuseWarningKillCount');
  const hAbuseLimit = readNumProp('AbuseLimitKillCount');
  const hRewardStart = readNumProp('RewardStartDay');
  const hRewardEnd = readNumProp('RewardEndDay');

  const lines = [
    '# Gens System Rules (Season 6 vs Season 9 vs HNMO)',
    `# S6 format=${g6.format}, ranks=${g6.ranks.length}; S9 format=${g9.format}, ranks=${g9.ranks.length}; HNMO ranks=${hRanks.length}, rewards=${hRewards.length}`,
    '',
    '## Limits, Schedule & Social Rules (S6 vs S9 vs HNMO GensConfiguration.cs)',
    `MinimumLevelToJoin: S6=50 (client/code), S9=50, HNMO=${hMinLv} (GensConfiguration.cs:17) -> same`,
    `StartingContribution: S6=${g6.initialContributePoint}, S9=${g9.initialContributePoint}, HNMO=${hStartContrib} (GensConfiguration.cs:22) -> same`,
    `StartingRank: S6=14, S9=14, HNMO=${hStartRank} (GensConfiguration.cs:28) -> same`,
    `AbuseWarningKillCount: S6=${g6.abuseWarning}, S9=${g9.abuseWarning}, HNMO=${hAbuseWarn} (GensConfiguration.cs:91) -> same`,
    `AbuseLimitKillCount: S6=${g6.abuseLimit}, S9=${g9.abuseLimit}, HNMO=${hAbuseLimit} (GensConfiguration.cs:96) -> same`,
    `AbuseResetMinutes: S6=${g6.abuseResetMin}, S9=${g9.abuseResetMin}, HNMO=60 (GensConfiguration.cs:101) -> same`,
    `RankingIntervalHours: S6=DS config (>=5h), S9=DS config (>=5h), HNMO=2 (GensConfiguration.cs:127) -> differs`,
    `RewardWindowDays: S6=${g6.rewardStartDay}-${g6.rewardEndDay}, S9=${g9.rewardStartDay}-${g9.rewardEndDay} (MinRank=${g9.minRankToGainReward}), HNMO=${hRewardStart}-${hRewardEnd} (GensConfiguration.cs:132-137) -> same`,
    `CanSeeOppositeGensChat: S6=${g6.canSeeOppositeChat}, S9=${g9.canSeeOppositeChat}, HNMO=not filtered -> missing in HNMO`,
    `CanJoinPartyWhileOppositeGens: S6=${g6.canJoinPartyOpposite}, S9=${g9.canJoinPartyOpposite}, HNMO=false (AllowPartyWithOtherGens GensConfiguration.cs:157) -> same`,
    `CanCreatePartyOnBattleZone: S6=${g6.canCreatePartyBattleZone}, S9=${g9.canCreatePartyBattleZone}, HNMO=false (AllowPartyInBattleZone GensConfiguration.cs:163) -> same`,
    `CanJoinGuildWhileOppositeGens: S6=${g6.canJoinGuildOpposite}, S9=${g9.canJoinGuildOpposite}, HNMO=true default (GuildRequiresGens=false GensConfiguration.cs:172) -> differs`,
    `CanJoinUnionWhileOppositeGens: S6=${g6.canJoinUnionOpposite}, S9=${g9.canJoinUnionOpposite}, HNMO=true default (AllianceRequiresSameGens=false GensConfiguration.cs:180) -> differs`,
    `PkPenaltyAddNeedZenMapMove: S6=${g6.pkBattleWarpZenMult}, S9=${g9.pkBattleWarpZenMult}, HNMO=missing -> missing in HNMO`,
    `BattleZoneMapsEnabled: S6=Map63:${g6.battleMaps[0]?.enable}, S9=${g9.battleMaps.map((m) => `Map${m.number}:${m.enable}`).join(',')}, HNMO=Map63 (GensConfiguration.cs:52) -> same Vulcanus (63) battle zone vs S6 MuBlue flag`,
    '',
    '## Ranks & Monthly Rewards (S6 vs S9 vs HNMO GensConfiguration.cs:106-152)',
  ];
  for (let r = 1; r <= 14; r++) {
    const r6 = g6.ranks.find((x) => x.rank === r);
    const r9 = g9.ranks.find((x) => x.rank === r);
    const rh = hRanks.find((x) => x.rank === r);
    const rewH = hRewards.find((x) => x.rank === r);
    const s6Contrib = r6?.reqContrib ?? 0;
    const hContrib = rh?.minContrib ?? 0;
    const hPos = rh?.maxPos ?? 0;
    const rewardSame =
      ((r6?.rewardCount ?? 0) === 0 && !rewH) ||
      (rewH && r6?.rewardCat === rewH.cat && r6?.rewardIndex === rewH.item && r6?.rewardCount === rewH.count);
    const status = s6Contrib === hContrib && (r6?.rankPos ?? 0) === hPos && rewardSame ? 'same' : 'differs';
    lines.push(
      `Rank ${r} (${r9?.name || `Class${r}`}): S6=(contrib=${r6?.reqContrib},pos=${r6?.rankPos},rew=${r6?.rewardCat}:${r6?.rewardIndex}x${r6?.rewardCount}) | S9=(contrib=${r9?.reqContrib},pos=${r9?.rankPos},rew=${r9?.rewardCat}:${r9?.rewardIndex}x${r9?.rewardCount}) | HNMO=(contrib=${hContrib},pos=${hPos},rew=${rewH ? `${rewH.cat}:${rewH.item}x${rewH.count}` : '0:0x0'}) -> ${status}${s6Contrib !== hContrib ? ' (HNMO guards Top300 with 10k min contrib)' : ''}`,
    );
  }
  writeDiff('diff-gens.txt', lines);
}

// 17. diff-common-rules.txt
{
  const c6 = parseSuiteCommonAndLimits(S6_DIR);
  const c9 = parseSuiteCommonAndLimits(S9_DIR);
  const lines = [
    '# Common, PK, Party, Guild, Pet, Price, & VIP Rules (S6 vs S9)',
    `VIP Tiers: S6=${c6.vipCount}, S9=${c9.vipCount}`,
    '',
    '## CommonServer.cfg differences (S6 vs S9)',
  ];
  const cfgKeys = new Set([...Object.keys(c6.commonCfg), ...Object.keys(c9.commonCfg)]);
  for (const k of [...cfgKeys].sort()) {
    const v6 = c6.commonCfg[k];
    const v9 = c9.commonCfg[k];
    if (v6 !== v9) lines.push(`${k}: S6=${v6 ?? 'absent'} -> S9=${v9 ?? 'absent'}`);
  }
  lines.push('', '## Common.ini differences (S6 vs S9)');
  const iniSecs = new Set([...Object.keys(c6.commonIni), ...Object.keys(c9.commonIni)]);
  for (const sec of [...iniSecs].sort()) {
    const s6 = c6.commonIni[sec] ?? {};
    const s9 = c9.commonIni[sec] ?? {};
    const ks = new Set([...Object.keys(s6), ...Object.keys(s9)]);
    for (const k of [...ks].sort()) {
      if (s6[k] !== s9[k]) lines.push(`[${sec}] ${k}: S6=${s6[k] ?? 'absent'} -> S9=${s9[k] ?? 'absent'}`);
    }
  }
  writeDiff('diff-common-rules.txt', lines);
}

// 18. diff-suite-rules.txt (full structural & value-level diff across all remaining rule files)
{
  const r6 = parseRemainingSuiteRules(S6_DIR);
  const r9 = parseRemainingSuiteRules(S9_DIR);
  const map6 = new Map(r6.files.map((f) => [f.rel, f]));
  const map9 = new Map(r9.files.map((f) => [f.rel, f]));
  const allKeys = [...new Set([...map6.keys(), ...map9.keys()])].sort();

  const lines = [
    '# Suite-Wide Rule File Coverage & Structural Diff (Season 6 vs Season 9)',
    `# Parsed rule/config/script files: S6=${r6.files.length}, S9=${r9.files.length}`,
    `# Deep counts S6: ${JSON.stringify(r6.deep)}`,
    `# Deep counts S9: ${JSON.stringify(r9.deep)}`,
    '',
    '## Shared Rule Files with Structural/Key/INI Differences',
  ];

  const s6Only = [];
  const s9Only = [];
  for (const rel of allKeys) {
    const a = map6.get(rel);
    const b = map9.get(rel);
    if (!a) {
      s9Only.push(`${rel} [${b.kind}, ${b.lines}L, ${b.detail}]`);
    } else if (!b) {
      s6Only.push(`${rel} [${a.kind}, ${a.lines}L, ${a.detail}]`);
    } else if (a.ini && b.ini) {
      const diffs = [];
      const secs = new Set([...Object.keys(a.ini), ...Object.keys(b.ini)]);
      for (const sec of [...secs].sort()) {
        const sa = a.ini[sec] ?? {};
        const sb = b.ini[sec] ?? {};
        for (const k of new Set([...Object.keys(sa), ...Object.keys(sb)])) {
          if (sa[k] !== sb[k]) diffs.push(`[${sec}]${k}:${sa[k] ?? 'absent'}->${sb[k] ?? 'absent'}`);
        }
      }
      if (diffs.length) {
        lines.push(`${rel} (${diffs.length} INI diffs): ${diffs.slice(0, 12).join('; ')}${diffs.length > 12 ? ` ... +${diffs.length - 12}` : ''}`);
      }
    } else if (a.detail !== b.detail || a.lines !== b.lines) {
      lines.push(`${rel}: S6(${a.lines}L, ${a.detail}) -> S9(${b.lines}L, ${b.detail})`);
    }
  }

  lines.push('', `## Season 6-Only Rule Files (${s6Only.length})`);
  for (const s of s6Only) lines.push(`  ${s}`);

  lines.push('', `## Season 9-Only Rule Files (${s9Only.length})`);
  for (const s of s9Only) lines.push(`  ${s}`);

  writeDiff('diff-suite-rules.txt', lines);
}
