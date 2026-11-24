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
  const maxSlots = Math.floor(buf.length / REC);
  const out = [];
  for (let i = 0; i < maxSlots && (i + 1) * REC <= buf.length; i++) {
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
  const maxSlots = Math.floor(buf.length / REC);
  const out = [];
  for (let i = 0; i < maxSlots && (i + 1) * REC <= buf.length; i++) {
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
  const maxSlots = Math.floor(buf.length / REC);
  const out = [];
  for (let i = 0; i < maxSlots && (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const id = r.readInt32LE(0);
    const name = readCString(r, 4, 60);
    if (id > 0 && name) out.push({ slot: i, id, name });
  }
  return out;
}

function decodeItemSetType(filePath) {
  if (!filePath) return new Map();
  const buf = fs.readFileSync(filePath);
  const recSize = buf.length >= 8192 * 6 ? 6 : 4;
  const out = new Map();
  for (let i = 0; i < 8192 && (i + 1) * recSize <= buf.length; i++) {
    const r = unbux(buf, i * recSize, recSize);
    const sets = recSize === 6 ? [r.readUInt8(0), r.readUInt8(1), r.readUInt8(2)] : [r.readUInt8(0), r.readUInt8(1)];
    const mixLvl = recSize === 6 ? [r.readUInt8(3), r.readUInt8(4), r.readUInt8(5)] : [r.readUInt8(2), r.readUInt8(3)];
    if (sets.some((v) => v > 0)) {
      const grp = Math.floor(i / 512);
      const num = i % 512;
      out.set(`${grp}-${num}`, { sets: sets.filter((v) => v > 0), mixLvl });
    }
  }
  return out;
}

function decodeItemAddOption(filePath) {
  if (!filePath) return new Map();
  const buf = fs.readFileSync(filePath);
  const dec = unbux(buf, 0, buf.length);
  const out = new Map();
  for (let i = 0; i < 8192 && (i + 1) * 16 <= dec.length; i++) {
    const o = i * 16;
    const op1 = dec.readUInt8(o);
    const val1 = dec.readUInt16LE(o + 2);
    const op2 = dec.readUInt8(o + 4);
    const val2 = dec.readUInt16LE(o + 6);
    const type = dec.readUInt8(o + 8);
    const time = dec.readUInt32LE(o + 12);
    if (op1 || op2 || time) {
      out.set(`${Math.floor(i / 512)}-${i % 512}`, { op1, val1, op2, val2, type, time });
    }
  }
  return out;
}

function decodeMonsterSkill(filePath) {
  if (!filePath) return new Map();
  const buf = fs.readFileSync(filePath);
  const n = buf.readUInt32LE(0);
  const out = new Map();
  for (let i = 0; i < n && 4 + (i + 1) * 48 <= buf.length; i++) {
    const r = unbux(buf, 4 + i * 48, 48);
    const monster = r.readInt32LE(0);
    const skills = [];
    for (let j = 1; j <= 10; j++) {
      const s = r.readInt32LE(j * 4);
      if (s >= 0) skills.push(s);
    }
    out.set(monster, skills);
  }
  return out;
}

function decodeQuestEng(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 744;
  const out = [];
  for (let i = 0; (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const subQuests = r.readInt16LE(0);
    const reqCount = r.readInt16LE(2);
    const npcType = r.readInt16LE(4);
    const name = readCString(r, 6, 32);
    if (name || subQuests > 0) out.push({ index: i, subQuests, reqCount, npcType, name });
  }
  return out;
}

function decodeQuestProgress(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 41;
  const out = [];
  for (let i = 0; (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    out.push({
      key: r.readUInt32LE(0),
      uiType: r.readUInt8(4),
      npcWords: r.readInt32LE(5),
      playerWords: r.readInt32LE(9),
      subject: r.readInt32LE(33),
      summary: r.readInt32LE(37),
    });
  }
  return out;
}

function decodeNpcDialogue(filePath) {
  if (!filePath) return [];
  const buf = fs.readFileSync(filePath);
  const REC = 88;
  const out = [];
  for (let i = 0; (i + 1) * REC <= buf.length; i++) {
    const r = unbux(buf, i * REC, REC);
    const answers = [];
    for (let j = 0; j < 20; j++) {
      const a = r.readInt32LE(8 + j * 4);
      if (a > 0) answers.push(a);
    }
    out.push({ key: r.readUInt32LE(0), npcWords: r.readInt32LE(4), answerCount: answers.length });
  }
  return out;
}

function decodeItemBmdNames(filePath) {
  const items = new Map();
  if (!filePath) return { items, format: 'none' };
  const buf = fs.readFileSync(filePath);
  if (buf.length === 8192 * 84 + 4) {
    for (let i = 0; i < 8192; i++) {
      const r = unbux(buf, i * 84, 84);
      const name = readCString(r, 0, 30);
      if (name) items.set(`${Math.floor(i / 512)}-${i % 512}`, name);
    }
    return { items, format: '8192x84' };
  }
  const u32 = buf.length >= 4 ? buf.readUInt32LE(0) : 0;
  if (u32 > 0 && u32 < 10000 && buf.length === 4 + u32 * 628 + 4) {
    for (let i = 0; i < u32; i++) {
      const r = unbux(buf, 4 + i * 628, 628);
      const grp = r.readUInt16LE(4);
      const num = r.readUInt16LE(6);
      const name = readCString(r, 528, 64);
      if (name) items.set(`${grp}-${num}`, name);
    }
    return { items, format: `${u32}x628` };
  }
  return { items, format: `${buf.length}b` };
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
  const it6 = decodeItemBmdNames(findClientFile(S6_CLIENT, ['Data/Local/Eng/item_eng.bmd']));
  const it9 = decodeItemBmdNames(findClientFile(S9_CLIENT, ['Data/Local/Eng/Item_eng.bmd']));
  const sk6 = decodeSkillBmdNames(findClientFile(S6_CLIENT, ['Data/Local/Eng/skill_eng.bmd']));
  const sk9 = decodeSkillBmdNames(findClientFile(S9_CLIENT, ['Data/Local/Eng/skill_eng.bmd']));
  const s9OnlyItems = [...it9.items.entries()].filter(([k]) => !it6.items.has(k));
  const s9OnlySkills = [...sk9.entries()].filter(([id]) => !sk6.has(id));
  lines.push(
    '',
    `## Item_eng.bmd: S6=${it6.items.size} items (${it6.format}), S9=${it9.items.size} items (${it9.format}) (+${s9OnlyItems.length} S9-only)`,
  );
  for (const [k, name] of s9OnlyItems.slice(0, 20)) lines.push(`  S9-only item ${k}: ${name}`);
  if (s9OnlyItems.length > 20) lines.push(`  ... (${s9OnlyItems.length - 20} more)`);
  lines.push(
    `## skill_eng.bmd: S6=${sk6.size} skills, S9=${sk9.size} skills (+${s9OnlySkills.length} S9-only)`,
  );
  for (const [id, name] of s9OnlySkills.slice(0, 20)) lines.push(`  S9-only skill #${id}: ${name}`);
  if (s9OnlySkills.length > 20) lines.push(`  ... (${s9OnlySkills.length - 20} more)`);
}

// 8. itemsettype.bmd, ItemAddOption.bmd, MonsterSkill.bmd, Quest_eng.bmd, QuestProgress.bmd, NPCDialogue.bmd
{
  const st6 = decodeItemSetType(findClientFile(S6_CLIENT, ['Data/Local/itemsettype.bmd']));
  const st9 = decodeItemSetType(findClientFile(S9_CLIENT, ['Data/Local/itemsettype.bmd']));
  const s9OnlySetItems = [...st9.entries()].filter(([k]) => !st6.has(k));
  lines.push('', `## itemsettype.bmd: S6=${st6.size} ancient items, S9=${st9.size} ancient items (+${s9OnlySetItems.length} S9-only)`);
  for (const [k, v] of s9OnlySetItems.slice(0, 15)) lines.push(`  S9-only ancient item ${k}: sets=[${v.sets.join(',')}]`);
  if (s9OnlySetItems.length > 15) lines.push(`  ... (${s9OnlySetItems.length - 15} more)`);

  const add6 = decodeItemAddOption(findClientFile(S6_CLIENT, ['Data/Local/ItemAddOption.bmd']));
  const add9 = decodeItemAddOption(findClientFile(S9_CLIENT, ['Data/Local/ItemAddOption.bmd']));
  const s9OnlyAdd = [...add9.entries()].filter(([k]) => !add6.has(k));
  const diffAdd = [...add9.entries()].filter(([k, v]) => {
    const o = add6.get(k);
    return o && (o.op1 !== v.op1 || o.val1 !== v.val1 || o.op2 !== v.op2 || o.val2 !== v.val2 || o.time !== v.time);
  });
  lines.push('', `## ItemAddOption.bmd: S6=${add6.size} items, S9=${add9.size} items (+${s9OnlyAdd.length} S9-only, ${diffAdd.length} differing)`);
  for (const [k, v] of s9OnlyAdd.slice(0, 10)) lines.push(`  S9-only add-option ${k}: op1=${v.op1}:${v.val1} op2=${v.op2}:${v.val2} time=${v.time}`);

  const ms6 = decodeMonsterSkill(findClientFile(S6_CLIENT, ['Data/Local/MonsterSkill.bmd']));
  const ms9 = decodeMonsterSkill(findClientFile(S9_CLIENT, ['Data/Local/MonsterSkill.bmd']));
  const s9OnlyMs = [...ms9.entries()].filter(([k]) => !ms6.has(k));
  lines.push('', `## MonsterSkill.bmd: S6=${ms6.size} monsters, S9=${ms9.size} monsters (+${s9OnlyMs.length} S9-only)`);
  for (const [m, sk] of s9OnlyMs) lines.push(`  S9-only monster #${m}: skills=[${sk.join(',')}]`);

  const q6 = decodeQuestEng(findClientFile(S6_CLIENT, ['Data/Local/Eng/Quest_eng.bmd']));
  const q9 = decodeQuestEng(findClientFile(S9_CLIENT, ['Data/Local/Eng/Quest_eng.bmd']));
  lines.push('', `## Quest_eng.bmd: S6=${q6.length} quests, S9=${q9.length} quests`);

  const qp6 = decodeQuestProgress(findClientFile(S6_CLIENT, ['Data/Local/QuestProgress.bmd']));
  const qp9 = decodeQuestProgress(findClientFile(S9_CLIENT, ['Data/Local/QuestProgress.bmd']));
  lines.push(`## QuestProgress.bmd: S6=${qp6.length} steps, S9=${qp9.length} steps (+${qp9.length - qp6.length} S9)`);

  const nd6 = decodeNpcDialogue(findClientFile(S6_CLIENT, ['Data/Local/NPCDialogue.bmd']));
  const nd9 = decodeNpcDialogue(findClientFile(S9_CLIENT, ['Data/Local/NPCDialogue.bmd']));
  lines.push(`## NPCDialogue.bmd: S6=${nd6.length} entries, S9=${nd9.length} entries (+${nd9.length - nd6.length} S9)`);
}

// 9. Client text rule tables (InGameShop & Npcname)
{
  const parseIbs = (rel) => {
    const p6 = findClientFile(S6_CLIENT, [rel]);
    const p9 = findClientFile(S9_CLIENT, [rel]);
    const c6 = p6 ? fs.readFileSync(p6, 'utf8').trim().split(/\r?\n/).filter(Boolean).length : 0;
    const c9 = p9 ? fs.readFileSync(p9, 'utf8').trim().split(/\r?\n/).filter(Boolean).length : 0;
    return { c6, c9 };
  };
  lines.push('', '## Client Text Rule Tables (S6 vs S9)');
  for (const [label, rel] of [
    ['IBSCategory.txt', 'Data/InGameShopScript/512.2014.124/IBSCategory.txt'],
    ['IBSPackage.txt', 'Data/InGameShopScript/512.2014.124/IBSPackage.txt'],
    ['IBSProduct.txt', 'Data/InGameShopScript/512.2014.124/IBSProduct.txt'],
    ['IBSBanner.txt', 'Data/InGameShopBanner/583.2014.001/IBSBanner.txt'],
  ]) {
    const { c6, c9 } = parseIbs(rel);
    lines.push(`  ${label}: S6=${c6} rows | S9=${c9} rows`);
  }
  const npc6 = findClientFile(S6_CLIENT, ['Data/Local/Eng/Npcname(eng).txt']);
  const npc9 = findClientFile(S9_CLIENT, ['Data/Local/Eng/Npcname_eng.bmd']);
  const npc6Count = npc6 ? fs.readFileSync(npc6, 'utf8').trim().split(/\r?\n/).filter((l) => l.trim() && !l.trim().startsWith('//')).length : 0;
  const npc9Bytes = npc9 ? fs.statSync(npc9).size : 0;
  lines.push(`  Npcname(eng): S6 txt=${npc6Count} rows | S9 Npcname_eng.bmd=${npc9Bytes} bytes`);
}

function decodeQuestWords(filePath) {
  if (!filePath) return new Map();
  const buf = fs.readFileSync(filePath);
  const out = new Map();
  let off = 0;
  while (off + 6 <= buf.length) {
    const hdr = unbux(buf, off, 6);
    off += 6;
    const id = hdr.readInt32LE(0);
    const len = hdr.readInt16LE(4);
    if (len < 0 || len > 1024 || off + len > buf.length) break;
    const text = unbux(buf, off, len).toString('utf8');
    off += len;
    out.set(id, text);
  }
  return out;
}

function decodeHeaderTableSummary(filePath) {
  if (!filePath) return null;
  const buf = fs.readFileSync(filePath);
  const size = buf.length;
  const u32 = size >= 4 ? buf.readUInt32LE(0) : 0;
  const dec0 = size >= 4 ? ((buf[0] ^ 0xfc) | ((buf[1] ^ 0xcf) << 8) | ((buf[2] ^ 0xab) << 16) | ((buf[3] ^ 0xfc) << 24)) >>> 0 : 0;
  if (u32 > 0 && u32 < 20000 && ((size - 4) % u32 === 0 || (size - 8) % u32 === 0)) {
    const stride = (size - 4) % u32 === 0 ? (size - 4) / u32 : (size - 8) / u32;
    const sample = [];
    for (let i = 0; i < Math.min(3, u32) && 4 + (i + 1) * stride <= size; i++) {
      const r = unbux(buf, 4 + i * stride, stride);
      const s = readCString(r, 4, Math.min(48, Math.max(0, stride - 4))).replace(/[\x00-\x1f\ufffd]/g, '');
      if (s && /^[A-Za-z0-9]/.test(s)) sample.push(s);
    }
    return { count: u32, stride, size, sample };
  }
  if (dec0 > 0 && dec0 < 5000 && ((size - 4) % dec0 === 0 || (size - 8) % dec0 === 0)) {
    const stride = (size - 4) % dec0 === 0 ? (size - 4) / dec0 : (size - 8) / dec0;
    return { count: dec0, stride, size, sample: [] };
  }
  return { count: null, stride: null, size, sample: [] };
}

function decodeTextBmdCount(filePath) {
  if (!filePath) return 0;
  const buf = fs.readFileSync(filePath);
  if (buf.length < 6) return 0;
  // Header is either 2 bytes ('TG' etc) + count or count(u32) + variable BUX records
  let off = 6;
  let count = 0;
  if (buf.readUInt16LE(0) === 0x4754) { // 'TG'
    return buf.readUInt32LE(2);
  }
  while (off + 8 <= buf.length) {
    const idx = buf.readUInt32LE(off);
    const len = buf.readUInt32LE(off + 4);
    if (len > 4096 || off + 8 + len > buf.length) break;
    off += 8 + len;
    count++;
    if (idx > 50000) break;
  }
  return count;
}

// 10. All remaining S6, S9, and S9-only encoded .bmd tables (decoded headers, record counts, and samples)
{
  const qw6 = decodeQuestWords(findClientFile(S6_CLIENT, ['Data/Local/Eng/QuestWords_eng.bmd']));
  const qw9 = decodeQuestWords(findClientFile(S9_CLIENT, ['Data/Local/Eng/QuestWords_eng.bmd']));
  lines.push('', `## QuestWords_eng.bmd: S6=${qw6.size} strings, S9=${qw9.size} strings (+${qw9.size - qw6.size} S9)`);

  lines.push('', '## Remaining Shared Encoded .bmd Tables (S6 vs S9)');
  const shared = [
    ['Text_eng.bmd', ['Data/Local/Eng/Text_eng.bmd'], ['Data/Local/Eng/Text_eng.bmd']],
    ['Dialog_eng.bmd', ['Data/Local/Eng/Dialog_eng.bmd'], ['Data/Local/Eng/Dialog_eng.bmd']],
    ['ItemTooltipText_eng.bmd', ['Data/Local/Eng/ItemTooltipText_eng.bmd'], ['Data/Local/Eng/ItemTooltipText_eng.bmd']],
    ['ItemLevelTooltip_eng.bmd', ['Data/Local/Eng/ItemLevelTooltip_eng.bmd'], ['Data/Local/Eng/ItemLevelTooltip_eng.bmd']],
    ['MasterSkillTooltip_eng.bmd', ['Data/Local/Eng/MasterSkillTooltip_eng.bmd'], ['Data/Local/Eng/MasterSkillTooltip_eng.bmd']],
    ['JewelOfHarmonySmelt_eng.bmd', ['Data/Local/Eng/JewelOfHarmonySmelt_eng.bmd'], ['Data/Local/Eng/JewelOfHarmonySmelt_eng.bmd']],
    ['slide_eng.bmd', ['Data/Local/Eng/slide_eng.bmd'], ['Data/Local/Eng/slide_eng.bmd']],
    ['pet.bmd', ['Data/Local/pet.bmd'], ['Data/Local/pet.bmd']],
    ['PetData.bmd', ['Data/Local/PetData.bmd'], ['Data/Local/PetData.bmd']],
    ['MapCharacters.bmd', ['Data/Local/MapCharacters.bmd'], ['Data/Local/MapCharacters.bmd']],
    ['MasterSkillTree.bmd', ['Data/Local/MasterSkillTree.bmd'], ['Data/Local/MasterSkillTree.bmd']],
    ['Filter.bmd', ['Data/Local/Filter.bmd'], ['Data/Local/Filter.bmd']],
    ['FilterName.bmd', ['Data/Local/FilterName.bmd'], ['Data/Local/FilterName.bmd']],
    ['Glow.bmd', ['Data/Local/Glow.bmd'], ['Data/Local/Glow.bmd']],
    ['credit.bmd', ['Data/Local/credit.bmd'], ['Data/Local/credit.bmd']],
    ['ServerInfo.bmd', ['Data/Local/ServerInfo.bmd'], ['Data/Local/ServerInfo.bmd']],
    ['ServerList.bmd', ['Data/Local/ServerList.bmd'], ['Data/Local/ServerList.bmd']],
  ];
  for (const [label, c6, c9] of shared) {
    const p6 = findClientFile(S6_CLIENT, c6);
    const p9 = findClientFile(S9_CLIENT, c9);
    const s6 = decodeHeaderTableSummary(p6);
    const s9 = decodeHeaderTableSummary(p9);
    const fmt = (s) => (!s ? 'absent' : s.count ? `${s.count}x${s.stride} (${s.size}B)` : `${s.size}B`);
    const extra = label === 'Text_eng.bmd'
      ? ` [strings S6=${decodeTextBmdCount(p6)} S9=${decodeTextBmdCount(p9)}]`
      : '';
    lines.push(`  ${label}: S6=${fmt(s6)} | S9=${fmt(s9)}${extra}`);
  }

  lines.push('', '## Season 9-Only Encoded .bmd Rule & UI Tables');
  const s9OnlyBmds = [
    'Data/Local/ArcaBattleBootyMix.bmd',
    'Data/Local/Eng/ArcaBattleScript_eng.bmd',
    'Data/Local/AttributeVariation.bmd',
    'Data/Local/BonusExp.bmd',
    'Data/Local/EvolutionMonsterBox.bmd',
    'Data/Local/Eng/ExcellentCommonOption_eng.bmd',
    'Data/Local/Eng/ExcellentWingOption_eng.bmd',
    'Data/Local/Eng/HelpData_eng.bmd',
    'Data/Local/InfoTooltip.bmd',
    'Data/Local/Eng/InfoTooltipText_eng.bmd',
    'Data/Local/Eng/ItemSetOptionText_eng.bmd',
    'Data/Local/ItemTRSData.bmd',
    'Data/Local/Eng/Minimap_eng.bmd',
    'Data/Local/MuRummyCard.bmd',
    'Data/Local/MuRummyOption.bmd',
    'Data/Local/MuRummySlot.bmd',
    'Data/Local/MuunExchange.bmd',
    'Data/Local/MuunInfo.bmd',
    'Data/Local/MuunMeshInfo.bmd',
    'Data/Local/Eng/MuunOption_eng.bmd',
    'Data/Local/MuunRenderInfo.bmd',
    'Data/Local/MuunTooltipInfo.bmd',
    'Data/Local/Eng/Npcname_eng.bmd',
    'Data/Local/Eng/PentagramJewelOptionValue_eng.bmd',
    'Data/Local/PentagramMixNeedSource.bmd',
    'Data/Local/Eng/PentagramOption_eng.bmd',
    'Data/Local/StatOption.bmd',
    'Data/Local/Eng/StatOptionText_eng.bmd',
  ];
  for (const rel of s9OnlyBmds) {
    const p = findClientFile(S9_CLIENT, [rel]);
    const s = decodeHeaderTableSummary(p);
    const fmt = !s ? 'absent' : s.count ? `${s.count}x${s.stride} (${s.size}B)` : `${s.size}B`;
    const smp = s?.sample?.length ? ` sample=[${s.sample.join(', ')}]` : '';
    lines.push(`  ${path.basename(rel)}: S9=${fmt}${smp}`);
  }
}

fs.writeFileSync(OUT_FILE, lines.join('\n') + '\n', 'utf8');
console.log(`Wrote ${OUT_FILE} (${lines.length} lines)`);
