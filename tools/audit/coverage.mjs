#!/usr/bin/env node
// Audit coverage ledger for HNMO.
//
// Splits the code into review units (folders of at most MAX_LINES lines), keeps one row per unit in
// docs/audit/coverage.json (status, commit it was checked at, references, evidence, PRs) and renders
// docs/audit/coverage.md. Rows that were checked keep their data; when a unit's files changed after the
// commit it was checked at, the row is marked "Changed since check" so the next audit only rereads that.
//
// Usage (from the repository root):
//   node tools/audit/coverage.mjs            refresh units, mark changed rows, render the markdown
//   node tools/audit/coverage.mjs --check    exit 1 when a unit is not checked or changed since its check
//
// Update a row by editing docs/audit/coverage.json (status, checkedAt, references, evidence, prs) and run
// the script again. Status values: "Not checked", "Checked", "Changed since check", "No game rules".

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const MAX_LINES = 6000;
const ROOT = process.cwd();
const JSON_PATH = path.join(ROOT, 'docs', 'audit', 'coverage.json');
const MD_PATH = path.join(ROOT, 'docs', 'audit', 'coverage.md');

// Areas that hold game rules: split into units and read in full.
const RULE_AREAS = [
  { root: 'server/src/GameLogic', ext: ['.cs'], kind: 'Server rules' },
  { root: 'server/src/GameServer', ext: ['.cs'], kind: 'Server packet handlers' },
  { root: 'server/src/Persistence/Initialization', ext: ['.cs'], kind: 'Game data (Season 6/9)' },
  { root: 'server/src/DataModel', ext: ['.cs'], kind: 'Data model' },
  { root: 'server/src/AttributeSystem', ext: ['.cs'], kind: 'Server rules' },
  { root: 'server/src/GuildServer', ext: ['.cs'], kind: 'Server rules' },
  { root: 'server/src/FriendServer', ext: ['.cs'], kind: 'Server rules' },
  { root: 'server/src/PlugIns', ext: ['.cs'], kind: 'Server rules' },
  { root: 'server/src/Interfaces', ext: ['.cs'], kind: 'Server rules' },
  { root: 'client/web/src', ext: ['.ts'], kind: 'Web client' },
  { root: 'client/web/bridge', ext: ['.cs'], kind: 'Web client' },
  // Owner, 24 Nov: the desktop client after the server, its rule parts only (stats, skills, items, trade ...);
  // drawing, sound and network units are marked "No game rules" with one line why.
  { root: 'client/src/source', ext: ['.cpp', '.h'], kind: 'Desktop client' },
];

// Areas without game rules: one row each, only to show they were looked at and why they are skipped.
const OTHER_AREAS = [
  { root: 'server/src/Network', ext: ['.cs'], kind: 'Packet structures (client-server contract)' },
  { root: 'server/src/Web', ext: ['.cs', '.razor'], kind: 'Admin panel / website' },
  { root: 'server/src/Persistence/EntityFramework', ext: ['.cs'], kind: 'Database' },
  { root: 'server/src/Discord', ext: ['.cs'], kind: 'Discord' },
  { root: 'server/src/Dapr', ext: ['.cs'], kind: 'Hosting' },
  { root: 'server/src/ChatServer', ext: ['.cs'], kind: 'Chat server' },
  { root: 'server/src/Startup', ext: ['.cs'], kind: 'Hosting' },
  { root: 'server/src/ConnectServer', ext: ['.cs'], kind: 'Server list' },
  { root: 'server/src/LoginServer', ext: ['.cs'], kind: 'Login registry' },
  { root: 'server/src/Pathfinding', ext: ['.cs'], kind: 'Path finding' },
  { root: 'client/ClientLibrary', ext: ['.cs'], kind: 'Client library' },
];

const SKIP_DIRS = new Set(['bin', 'obj', 'node_modules', 'Migrations', 'CompiledModels', 'Properties']);

function walk(dir, ext) {
  const files = [];
  const dirs = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!SKIP_DIRS.has(entry.name)) {
        dirs.push(path.join(dir, entry.name));
      }
    } else if (ext.includes(path.extname(entry.name))) {
      files.push(path.join(dir, entry.name));
    }
  }

  return { files, dirs };
}

function countLines(file) {
  const text = fs.readFileSync(file, 'utf8');
  return text.length === 0 ? 0 : text.split('\n').length;
}

function allFiles(dir, ext) {
  const { files, dirs } = walk(dir, ext);
  return files.concat(...dirs.map(d => allFiles(d, ext)));
}

function rel(p) {
  return path.relative(ROOT, p).split(path.sep).join('/');
}

// A folder over MAX_LINES becomes chunks of its own files ("(files n)") plus one unit per subfolder.
// A single file over MAX_LINES stays one unit.
function split(dir, ext, kind, units) {
  const files = allFiles(dir, ext);
  const lines = files.reduce((sum, f) => sum + countLines(f), 0);
  const { files: own, dirs } = walk(dir, ext);
  if (lines <= MAX_LINES || (dirs.length === 0 && own.length === 1)) {
    if (files.length > 0) {
      units.push({ id: rel(dir), kind, paths: [rel(dir)], files: files.length, lines });
    }

    return;
  }

  if (own.length > 0) {
    // Own files of a big folder: chunk them by name so each chunk stays under MAX_LINES.
    own.sort();
    let chunk = [];
    let chunkLines = 0;
    let part = 1;
    const flush = () => {
      if (chunk.length === 0) {
        return;
      }

      units.push({ id: `${rel(dir)} (files ${part})`, kind, paths: chunk.map(rel), files: chunk.length, lines: chunkLines });
      part++;
      chunk = [];
      chunkLines = 0;
    };
    for (const f of own) {
      const n = countLines(f);
      if (chunkLines + n > MAX_LINES) {
        flush();
      }

      chunk.push(f);
      chunkLines += n;
    }

    flush();
  }

  for (const d of dirs.sort()) {
    split(d, ext, kind, units);
  }
}

function git(args) {
  return execFileSync('git', args, { cwd: ROOT, encoding: 'utf8' }).trim();
}

function changedSince(commit, paths) {
  try {
    return git(['diff', '--name-only', `${commit}..HEAD`, '--', ...paths]).split('\n').filter(Boolean);
  } catch {
    return ['(commit not found)'];
  }
}

const units = [];
for (const area of RULE_AREAS) {
  if (fs.existsSync(path.join(ROOT, area.root))) {
    split(path.join(ROOT, area.root), area.ext, area.kind, units);
  }
}

for (const area of OTHER_AREAS) {
  const dir = path.join(ROOT, area.root);
  if (fs.existsSync(dir)) {
    const files = allFiles(dir, area.ext);
    units.push({ id: area.root, kind: area.kind, paths: [area.root], files: files.length, lines: files.reduce((s, f) => s + countLines(f), 0), noRules: true });
  }
}

const old = fs.existsSync(JSON_PATH) ? JSON.parse(fs.readFileSync(JSON_PATH, 'utf8')) : { units: [] };
const oldById = new Map(old.units.map(u => [u.id, u]));
const head = git(['rev-parse', '--short', 'HEAD']);

const rows = units.map(u => {
  const prev = oldById.get(u.id) ?? {};
  const row = {
    id: u.id,
    kind: u.kind,
    paths: u.paths,
    files: u.files,
    lines: u.lines,
    status: prev.status ?? (u.noRules ? 'No game rules' : 'Not checked'),
    checkedAt: prev.checkedAt ?? '',
    checkedOn: prev.checkedOn ?? '',
    references: prev.references ?? '',
    evidence: prev.evidence ?? '',
    prs: prev.prs ?? '',
  };
  if ((row.status === 'Checked' || row.status === 'Changed since check') && row.checkedAt) {
    const changed = changedSince(row.checkedAt, row.paths);
    row.status = changed.length > 0 ? 'Changed since check' : 'Checked';
    row.changedFiles = changed.length > 0 ? changed : undefined;
  }

  return row;
});

const dropped = old.units.filter(u => !units.some(n => n.id === u.id) && u.status === 'Checked').map(u => u.id);

fs.mkdirSync(path.dirname(JSON_PATH), { recursive: true });
fs.writeFileSync(JSON_PATH, JSON.stringify({ generatedAt: head, maxLines: MAX_LINES, units: rows, removedCheckedUnits: dropped }, null, 2) + '\n');

const count = s => rows.filter(r => r.status === s);
const sum = list => list.reduce((s, r) => s + r.lines, 0);
const ruleRows = rows.filter(r => r.status !== 'No game rules');
let md = '# Audit coverage ledger\n\n';
md += 'Generated by `node tools/audit/coverage.mjs` at commit `' + head + '`. Edit `docs/audit/coverage.json`, never this file. ';
md += 'How to use it: [audit-method.md](audit-method.md).\n\n';
md += `| Status | Units | Lines |\n|---|---|---|\n`;
for (const s of ['Checked', 'Changed since check', 'Not checked', 'No game rules']) {
  md += `| ${s} | ${count(s).length} | ${sum(count(s))} |\n`;
}

md += `\nRule-bearing code: ${ruleRows.length} units, ${sum(ruleRows)} lines.\n\n`;
md += '| Unit | Kind | Files | Lines | Status | Checked at | References | Evidence | PRs |\n|---|---|---|---|---|---|---|---|---|\n';
const cell = s => String(s ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ');
for (const r of rows) {
  const status = r.changedFiles ? `${r.status} (${r.changedFiles.length} files)` : r.status;
  md += `| ${cell(r.id)} | ${cell(r.kind)} | ${r.files} | ${r.lines} | ${status} | ${cell(r.checkedAt)} ${cell(r.checkedOn)} | ${cell(r.references)} | ${cell(r.evidence)} | ${cell(r.prs)} |\n`;
}

if (dropped.length > 0) {
  md += `\nChecked units that no longer exist (folders moved or split; recheck their new units): ${dropped.join(', ')}\n`;
}

fs.writeFileSync(MD_PATH, md);
console.log(`${rows.length} units; checked ${count('Checked').length}, changed ${count('Changed since check').length}, not checked ${count('Not checked').length}.`);

if (process.argv.includes('--check') && (count('Not checked').length > 0 || count('Changed since check').length > 0)) {
  process.exit(1);
}
