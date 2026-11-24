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

// 4. Encoded table inventory & byte sizes
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
