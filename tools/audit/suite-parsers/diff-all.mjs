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

function writeDiff(fileName, lines) {
  fs.writeFileSync(path.join(OUT_DIR, fileName), lines.join('\n') + '\n', 'utf8');
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
