#!/usr/bin/env node
// Decodes Season 6 and Season 9 client encoded data tables (.bmd) in memory using LEGACY_S6_CLIENT_DIR and
// LEGACY_S9_CLIENT_DIR, and compares their counts/values against HNMO and each other.
// Never writes decoded table dumps to disk; outputs only counts and differences to docs/audit/data/diff-client-tables.txt.

import fs from 'node:fs';
import path from 'node:path';
import { parseCsv } from './suite-files.mjs';

const BUX = [0xfc, 0xcf, 0xab];
const ROOT = process.cwd();
const S6_CLIENT = process.env.LEGACY_S6_CLIENT_DIR;
const S9_CLIENT = process.env.LEGACY_S9_CLIENT_DIR;
const HNMO_DIR = process.env.HNMO_CSV_DIR ?? path.join(ROOT, 'docs', 'audit', 'data', 'hnmo');
const OUT_FILE = path.join(ROOT, 'docs', 'audit', 'data', 'diff-client-tables.txt');

if (!S6_CLIENT || !S9_CLIENT) {
  console.error('Error: set LEGACY_S6_CLIENT_DIR and LEGACY_S9_CLIENT_DIR environment variables.');
  process.exit(1);
}

function unbux(buf, offset, length) {
  const out = Buffer.allocUnsafe(length);
  for (let i = 0; i < length; i++) out[i] = buf[offset + i] ^ BUX[i % 3];
  return out;
}

function readCString(buf, offset, maxLen) {
  const slice = buf.subarray(offset, offset + maxLen);
  const zero = slice.indexOf(0);
  return slice.subarray(0, zero >= 0 ? zero : maxLen).toString('utf8');
}

function findClientFile(clientRoot, relCandidates) {
  for (const rel of relCandidates) {
    const p = path.join(clientRoot, rel);
    if (fs.existsSync(p)) return p;
  }
  return null;
}

function decodeMasterTree(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 24;
  const nodes = [];
  for (let i = 0; i < 512 && (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const index = r.readUInt16LE(0);
    if (index === 0) break;
    nodes.push({
      index,
      classCode: r.readUInt16LE(2),
      group: r.readUInt8(4),
      reqPoints: r.readUInt8(5),
      maxLevel: r.readUInt8(6),
      skill: r.readInt32LE(16),
    });
  }
  return nodes;
}

function decodeMixBmd(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const MIX_TYPES = 14;
  const REC = 656;
  if (buf.length < MIX_TYPES * 4) return [];
  const hdr = unbux(buf, 0, MIX_TYPES * 4);
  const counts = [];
  for (let i = 0; i < MIX_TYPES; i++) counts.push(hdr.getInt32 ? hdr.getInt32(i * 4, true) : hdr.readInt32LE(i * 4));
  let offset = MIX_TYPES * 4;
  const recipes = [];
  for (let t = 0; t < MIX_TYPES; t++) {
    const c = counts[t];
    if (c < 0 || c > 500) continue;
    for (let i = 0; i < c && offset + REC <= buf.length; i++) {
      const r = unbux(buf, offset, REC);
      offset += REC;
      recipes.push({
        type: t,
        index: r.readInt32LE(0),
        id: r.readInt32LE(4),
        reqLevel: r.readInt32LE(44),
        zen: r.readInt32LE(52),
        successRate: r.readInt32LE(468),
      });
    }
  }
  return recipes;
}

function decodeMoveReq(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  if (buf.length < 4) return [];
  const count = buf.readInt32LE(0);
  const bodyLen = buf.length - 4;
  const recSize = bodyLen % 84 === 0 ? 84 : bodyLen % 80 === 0 ? 80 : Math.floor(bodyLen / Math.max(1, count));
  const entries = [];
  if (recSize < 80) return entries;
  for (let i = 0; i < count && 4 + (i + 1) * recSize <= buf.length; i++) {
    const r = unbux(buf, 4 + i * recSize, recSize);
    const index = r.readInt32LE(0);
    const mainName = readCString(r, 4, 32);
    const subName = readCString(r, 36, 32);
    const zen = r.readInt32LE(68);
    const reqLevel = r.readInt32LE(72);
    const gate = r.readInt32LE(76);
    if (index > 0 || mainName) entries.push({ index, mainName, subName, zen, reqLevel, gate });
  }
  return entries;
}

function decodeQuestProgress(filePath) {
  if (!filePath) return 0;
  const buf = fs.readFileSync(filePath);
  // 4-byte index + SQuestProgress
  const REC = 76;
  return Math.floor(buf.length / REC);
}

function decodeGateBmd(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 14;
  const gates = [];
  for (let i = 0; (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const flag = r.readUInt8(0);
    const map = r.readUInt8(1);
    const x1 = r.readUInt8(2);
    const y1 = r.readUInt8(3);
    const x2 = r.readUInt8(4);
    const y2 = r.readUInt8(5);
    const target = r.readUInt16LE(6);
    const level = r.readUInt16LE(10);
    if (flag !== 0 || map !== 0 || target !== 0 || level !== 0) {
      gates.push({ index: i, flag, map, x1, y1, x2, y2, target, level });
    }
  }
  return gates;
}

function decodeBuffEffect(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  if (buf.length < 4) return [];
  const count = buf.readUInt32LE(0);
  const recSize = Math.floor((buf.length - 4) / Math.max(1, count));
  const out = [];
  for (let i = 0; i < count && 4 + (i + 1) * recSize <= buf.length; i++) {
    const r = unbux(buf, 4 + i * recSize, recSize);
    const index = r.readInt16LE(0);
    const name = readCString(r, 4, 50);
    if (index > 0 || name) out.push({ index, name });
  }
  return out;
}

function decodeItemSetOptions(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 110;
  const out = [];
  for (let i = 0; i < 64 && (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const name = readCString(r, 0, 64);
    if (name) out.push({ index: i, name, optionCount: r.readUInt8(92) });
  }
  return out;
}

function decodeSocketOptions(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 104;
  const out = [];
  for (let i = 0; i < 150 && (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const id = r.readInt32LE(0);
    const name = readCString(r, 8, 64);
    if (name) out.push({ slot: i, id, name, category: r.readInt32LE(4) });
  }
  return out;
}

function decodeHarmonyOptions(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 180;
  const out = [];
  for (let i = 0; i < 30 && (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const name = readCString(r, 4, 60);
    if (name) out.push({ index: i, name });
  }
  return out;
}

function decodeItemBmdCount(filePath) {
  if (!filePath) return { count: 0, format: 'none' };
  const buf = fs.readFileSync(filePath);
  if (buf.length === 8192 * 84 + 4) {
    let populated = 0;
    for (let i = 0; i < 8192; i++) {
      const r = unbux(buf, i * 84, 84);
      if (readCString(r, 0, 30)) populated++;
    }
    return { count: populated, format: '8192x84' };
  }
  const u32 = buf.length >= 4 ? buf.readUInt32LE(0) : 0;
  if (u32 > 0 && u32 < 10000 && ((buf.length - 4) % u32 === 0 || (buf.length - 8) % u32 === 0)) {
    const stride = (buf.length - 4) % u32 === 0 ? (buf.length - 4) / u32 : (buf.length - 8) / u32;
    return { count: u32, format: `${u32}x${stride}` };
  }
  return { count: 0, format: `${buf.length}b` };
}

function decodeSkillBmdNames(filePath) {
  if (!filePath) return new Map();
  const buf = fs.readFileSync(filePath);
  const recSize = buf.length === 650 * 88 + 4 ? 88 : buf.length === 650 * 112 + 4 ? 112 : 0;
  const out = new Map();
  if (!recSize) return out;
  for (let i = 0; i < 650; i++) {
    const r = unbux(buf, i * recSize, recSize);
    const name = readCString(r, 0, 32);
    if (name) out.set(i, name);
  }
  return out;
}

const lines = [
  '# Client Encoded Data Tables Comparison (Season 6 vs Season 9 vs HNMO)',
  '',
];

// 1. MasterSkillTreeData.bmd
{
  const f6 = findClientFile(S6_CLIENT, ['Data/Local/MasterSkillTreeData.bmd']);
  const f9 = findClientFile(S9_CLIENT, ['Data/Local/MasterSkillTreeData.bmd']);
  const m6 = decodeMasterTree(f6);
  const m9 = decodeMasterTree(f9);
  lines.push(`## MasterSkillTreeData.bmd: S6=${m6.length} nodes, S9=${m9.length} nodes`);
  const map6 = new Map(m6.map((n) => [`${n.classCode}:${n.skill}`, n]));
  for (const n of m9) {
    const o = map6.get(`${n.classCode}:${n.skill}`);
    if (!o) lines.push(`  S9-only node: class=${n.classCode} skill=${n.skill} max=${n.maxLevel}`);
    else if (o.maxLevel !== n.maxLevel || o.reqPoints !== n.reqPoints) {
      lines.push(`  Diff node class=${n.classCode} skill=${n.skill}: S6(max=${o.maxLevel},req=${o.reqPoints}) -> S9(max=${n.maxLevel},req=${n.reqPoints})`);
    }
  }
}

// 2. mix.bmd / Mix.bmd
{
  const f6 = findClientFile(S6_CLIENT, ['Data/Local/mix.bmd']);
  const f9 = findClientFile(S9_CLIENT, ['Data/Local/Mix.bmd', 'Data/Local/mix.bmd']);
  const r6 = decodeMixBmd(f6);
  const r9 = decodeMixBmd(f9);
  lines.push('', `## mix.bmd: S6=${r6.length} recipes, S9=${r9.length} recipes (S9 uses an expanded Mix.bmd record layout)`);
  if (r6.length > 0 && r9.length > 0 && fs.statSync(f9).size === 56 + r9.length * 656) {
    const map6 = new Map(r6.map((r) => [`${r.type}:${r.id}`, r]));
    for (const r of r9) {
      const o = map6.get(`${r.type}:${r.id}`);
      if (!o) lines.push(`  S9-only mix: type=${r.type} id=${r.id} zen=${r.zen}`);
      else if (o.zen !== r.zen || o.reqLevel !== r.reqLevel) {
        lines.push(`  Diff mix type=${r.type} id=${r.id}: S6(zen=${o.zen},lvl=${o.reqLevel}) -> S9(zen=${r.zen},lvl=${r.reqLevel})`);
      }
    }
  }
}

// 3. movereq_eng.bmd vs HNMO warps.csv
{
  const f6 = findClientFile(S6_CLIENT, ['Data/Local/Eng/movereq_eng.bmd', 'Data/Local/Movereq.bmd']);
  const f9 = findClientFile(S9_CLIENT, ['Data/Local/Eng/movereq_eng.bmd', 'Data/Local/Movereq.bmd']);
  const w6 = decodeMoveReq(f6);
  const w9 = decodeMoveReq(f9);
  const hWarps = parseCsv(path.join(HNMO_DIR, 'warps.csv'));
  lines.push('', `## movereq_eng.bmd: S6=${w6.length} warps, S9=${w9.length} warps, HNMO warps.csv=${hWarps.length}`);
  const map6 = new Map(w6.map((w) => [w.index, w]));
  for (const w of w9) {
    const o = map6.get(w.index);
    if (!o) lines.push(`  S9-only warp #${w.index}: ${w.mainName} (Lv=${w.reqLevel}, Zen=${w.zen}, Gate=${w.gate})`);
    else if (o.zen !== w.zen || o.reqLevel !== w.reqLevel) {
      lines.push(`  Diff warp #${w.index} (${w.mainName}): S6(Lv=${o.reqLevel},Zen=${o.zen}) -> S9(Lv=${w.reqLevel},Zen=${w.zen})`);
    }
  }
}

// 4. Gate.bmd vs HNMO enter_gates.csv / exit_gates.csv
{
  const g6 = decodeGateBmd(findClientFile(S6_CLIENT, ['Data/Gate.bmd', 'Data/gate.bmd']));
  const g9 = decodeGateBmd(findClientFile(S9_CLIENT, ['Data/Local/Gate.bmd', 'Data/Gate.bmd']));
  const hEnter = parseCsv(path.join(HNMO_DIR, 'enter_gates.csv'));
  const hExit = parseCsv(path.join(HNMO_DIR, 'exit_gates.csv'));
  lines.push('', `## Gate.bmd: S6=${g6.length} non-zero gates, S9=${g9.length} non-zero gates, HNMO enter=${hEnter.length} exit=${hExit.length}`);
  const m6 = new Map(g6.map((g) => [g.index, g]));
  for (const g of g9) {
    const o = m6.get(g.index);
    if (!o) lines.push(`  S9-only gate #${g.index}: map=${g.map} flag=${g.flag} (${g.x1},${g.y1})-(${g.x2},${g.y2}) target=${g.target} lvl=${g.level}`);
    else if (o.map !== g.map || o.target !== g.target || o.level !== g.level) {
      lines.push(`  Diff gate #${g.index}: S6(map=${o.map},target=${o.target},lvl=${o.level}) -> S9(map=${g.map},target=${g.target},lvl=${g.level})`);
    }
  }
}

// 5. BuffEffect_eng.bmd (S6 vs S9)
{
  const b6 = decodeBuffEffect(findClientFile(S6_CLIENT, ['Data/Local/Eng/BuffEffect_eng.bmd']));
  const b9 = decodeBuffEffect(findClientFile(S9_CLIENT, ['Data/Local/Eng/BuffEffect_eng.bmd']));
  const s6Ids = new Set(b6.map((b) => b.index));
  const s9Only = b9.filter((b) => !s6Ids.has(b.index));
  lines.push('', `## BuffEffect_eng.bmd: S6=${b6.length} buffs, S9=${b9.length} buffs (+${s9Only.length} S9-only)`);
  for (const b of s9Only.slice(0, 25)) lines.push(`  S9-only buff #${b.index}: ${b.name}`);
  if (s9Only.length > 25) lines.push(`  ... (${s9Only.length - 25} more)`);
}

// 6. ItemSetOption_eng.bmd, SocketItem_eng.bmd, JewelOfHarmonyOption_eng.bmd
{
  const sets6 = decodeItemSetOptions(findClientFile(S6_CLIENT, ['Data/Local/Eng/itemsetoption_eng.bmd']));
  const sets9 = decodeItemSetOptions(findClientFile(S9_CLIENT, ['Data/Local/Eng/itemsetoption_eng.bmd']));
  const hSets = parseCsv(path.join(HNMO_DIR, 'ancient_sets.csv'));
  const sock6 = decodeSocketOptions(findClientFile(S6_CLIENT, ['Data/Local/Eng/socketitem_eng.bmd']));
  const sock9 = decodeSocketOptions(findClientFile(S9_CLIENT, ['Data/Local/Eng/SocketItem_eng.bmd']));
  const harm6 = decodeHarmonyOptions(findClientFile(S6_CLIENT, ['Data/Local/Eng/JewelOfHarmonyOption_eng.bmd']));
  const harm9 = decodeHarmonyOptions(findClientFile(S9_CLIENT, ['Data/Local/Eng/JewelOfHarmonyOption_eng.bmd']));
  lines.push(
    '',
    `## Ancient Sets (itemsetoption_eng.bmd): S6=${sets6.length}, S9=${sets9.length}, HNMO ancient_sets.csv=${hSets.length}`,
    `## Socket Options (socketitem_eng.bmd): S6=${sock6.length}, S9=${sock9.length}`,
    `## Harmony Options (JewelOfHarmonyOption_eng.bmd): S6=${harm6.length}, S9=${harm9.length}`,
  );
}

// 7. Item_eng.bmd & skill_eng.bmd
{
  const it6 = decodeItemBmdCount(findClientFile(S6_CLIENT, ['Data/Local/Eng/item_eng.bmd']));
  const it9 = decodeItemBmdCount(findClientFile(S9_CLIENT, ['Data/Local/Eng/Item_eng.bmd']));
  const sk6 = decodeSkillBmdNames(findClientFile(S6_CLIENT, ['Data/Local/Eng/skill_eng.bmd']));
  const sk9 = decodeSkillBmdNames(findClientFile(S9_CLIENT, ['Data/Local/Eng/skill_eng.bmd']));
  const s9OnlySkills = [...sk9.entries()].filter(([id]) => !sk6.has(id));
  lines.push(
    '',
    `## Item_eng.bmd: S6=${it6.count} items (${it6.format}), S9=${it9.count} items (${it9.format})`,
    `## skill_eng.bmd: S6=${sk6.size} skills, S9=${sk9.size} skills (+${s9OnlySkills.length} S9-only)`,
  );
  for (const [id, name] of s9OnlySkills.slice(0, 20)) lines.push(`  S9-only skill #${id}: ${name}`);
  if (s9OnlySkills.length > 20) lines.push(`  ... (${s9OnlySkills.length - 20} more)`);
}

// 8. Remaining encoded .bmd table byte sizes (S6 vs S9)
lines.push('', '## Encoded .bmd table presence & byte sizes (S6 vs S9)');
const tables = [
  ['Item_eng.bmd', ['Data/Local/Eng/item_eng.bmd', 'Data/Local/Item.bmd'], ['Data/Local/Eng/Item_eng.bmd']],
  ['Skill_eng.bmd', ['Data/Local/Eng/skill_eng.bmd', 'Data/Local/Skill.bmd'], ['Data/Local/Eng/skill_eng.bmd']],
  ['BuffEffect_eng.bmd', ['Data/Local/Eng/BuffEffect_eng.bmd', 'Data/Local/BuffEffect.bmd'], ['Data/Local/Eng/BuffEffect_eng.bmd']],
  ['Quest_eng.bmd', ['Data/Local/Eng/Quest_eng.bmd', 'Data/Local/Quest.bmd'], ['Data/Local/Eng/Quest_eng.bmd']],
  ['QuestProgress.bmd', ['Data/Local/QuestProgress.bmd'], ['Data/Local/QuestProgress.bmd']],
  ['NPCDialogue.bmd', ['Data/Local/NPCDialogue.bmd'], ['Data/Local/NPCDialogue.bmd']],
  ['SocketItem_eng.bmd', ['Data/Local/Eng/socketitem_eng.bmd'], ['Data/Local/Eng/SocketItem_eng.bmd']],
  ['JewelOfHarmonyOption_eng.bmd', ['Data/Local/Eng/JewelOfHarmonyOption_eng.bmd'], ['Data/Local/Eng/JewelOfHarmonyOption_eng.bmd']],
  ['ItemSetOption_eng.bmd', ['Data/Local/Eng/itemsetoption_eng.bmd'], ['Data/Local/Eng/itemsetoption_eng.bmd']],
  ['ItemSetType.bmd', ['Data/Local/Eng/itemsettype.bmd', 'Data/Local/itemsettype.bmd'], ['Data/Local/itemsettype.bmd']],
  ['ItemAddOption.bmd', ['Data/Local/ItemAddOption.bmd'], ['Data/Local/ItemAddOption.bmd']],
  ['MonsterSkill.bmd', ['Data/Local/MonsterSkill.bmd'], ['Data/Local/MonsterSkill.bmd']],
];
for (const [label, c6, c9] of tables) {
  const p6 = findClientFile(S6_CLIENT, c6);
  const p9 = findClientFile(S9_CLIENT, c9);
  const s6 = p6 ? fs.statSync(p6).size : 0;
  const s9 = p9 ? fs.statSync(p9).size : 0;
  lines.push(`  ${label}: S6=${s6} bytes | S9=${s9} bytes`);
}

fs.writeFileSync(OUT_FILE, lines.join('\n') + '\n', 'utf8');
console.log(`Wrote ${OUT_FILE} (${lines.length} lines)`);
