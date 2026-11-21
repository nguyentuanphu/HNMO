#!/usr/bin/env node
// Prepares a Season 9 data pack for an HNMO folder.
//
//   node client/web/tools/build-s9-pack.mjs <extracted Season 9 Data> <base game Data> <pack folder> [--dry-run]
//        [--base-allowlist <file>]
//
// The web server answers /data/s9/... from the pack first and from the base game for the rest
// (client/web/bridge/SeasonData.cs). Only the world's looks and sounds come from a pack, so only those
// folders are taken, and a file that is byte for byte the base game's file of the same path is left out:
// the pack holds only what differs. Names are matched ignoring case, as the web server does.
// A packaged HNMO folder keeps only the base files its own allowlist names (tools/build-allowlist.mjs), so
// a Season 9 model can need a base file the folder doesn't have (a Season 9 model of Noria uses
// Object4/itp01, no base model does). Pass that allowlist, made from the same base Data:
//   node client/web/tools/build-allowlist.mjs client/src/bin/Data <scratch>/base-allowlist.txt
// and such a file is counted as "the base game doesn't ship it" and taken into the pack.
// The pack is the owner's own data: it is built on their computer and never goes into the repository.
// Point package-hnmo.sh's HNMO_S9_DATA_DIR at the pack folder.

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

/** The top folders a season's pack may answer (the same list as bridge/SeasonData.cs). */
export const SEASON_FOLDER = /^(World\d+|Object\d+|Monster|Item|Skill|Effect|Player|NPC|Sound|Music)$/i;

const FOLDER_NAMES = ['Monster', 'Item', 'Skill', 'Effect', 'Player', 'NPC', 'Sound', 'Music'];

/** The summary's name of a top folder: one spelling per folder, and all WorldN / ObjectN together. */
function canonicalFolder(top) {
  if (/^world\d+$/i.test(top)) return 'WorldN';
  if (/^object\d+$/i.test(top)) return 'ObjectN';
  return FOLDER_NAMES.find((name) => name.toLowerCase() === top.toLowerCase()) ?? top;
}

/** Folder notes Windows and file managers leave beside the files (thumbnail caches, comments): never game data. */
export const FOLDER_NOTES = /^(thumbs\.db|desktop\.ini|descript\.ion)$/i;

function walk(root, relative = '') {
  const files = [];
  const folder = path.join(root, relative);
  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    const rel = relative ? `${relative}/${entry.name}` : entry.name;
    if (entry.isDirectory()) files.push(...walk(root, rel));
    else if (entry.isFile() && !FOLDER_NOTES.test(entry.name)) files.push(rel);
  }
  return files;
}

function indexIgnoringCase(root) {
  const index = new Map();
  if (!fs.existsSync(root)) return index;
  for (const rel of walk(root)) index.set(rel.toLowerCase(), rel);
  return index;
}

function sameBytes(a, b) {
  const statA = fs.statSync(a);
  const statB = fs.statSync(b);
  if (statA.size !== statB.size) return false;
  const hash = (file) => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
  return hash(a) === hash(b);
}

/**
 * Builds the pack.
 * @param {{ season: string, base: string, out: string, dryRun?: boolean }} options
 * @returns {{ folders: Record<string, { taken: number, takenBytes: number, same: number, outside: number }>,
 *   taken: number, takenBytes: number, same: number, outside: number, outsideFolders: string[] }}
 */
export function buildSeasonPack({ season, base, out, dryRun = false, baseAllowlist = null }) {
  if (!fs.existsSync(season)) throw new Error(`No Season 9 Data folder at ${season}`);
  if (!fs.existsSync(base)) throw new Error(`No base game Data folder at ${base}`);
  const seasonRoot = path.resolve(season);
  const outRoot = path.resolve(out);
  if (outRoot === seasonRoot || outRoot.startsWith(seasonRoot + path.sep)) {
    throw new Error('The pack folder must not be inside the Season 9 Data folder.');
  }

  const baseIndex = indexIgnoringCase(base);
  const shipped = baseAllowlist ? new Set([...baseAllowlist].map((rel) => rel.replaceAll('\\', '/').toLowerCase())) : null;
  const summary = { folders: {}, taken: 0, takenBytes: 0, same: 0, notShipped: 0, outside: 0, outsideFolders: [] };
  const outside = new Set();
  for (const rel of walk(season)) {
    const top = rel.includes('/') ? rel.slice(0, rel.indexOf('/')) : '';
    if (!SEASON_FOLDER.test(top)) {
      summary.outside++;
      outside.add(top || '(top level)');
      continue;
    }

    const key = canonicalFolder(top);
    const folder = (summary.folders[key] ??= { taken: 0, takenBytes: 0, same: 0, outside: 0 });
    const source = path.join(season, rel);
    const baseRel = baseIndex.get(rel.toLowerCase());
    if (baseRel && sameBytes(source, path.join(base, baseRel))) {
      if (!shipped || shipped.has(baseRel.toLowerCase())) {
        folder.same++;
        summary.same++;
        continue;
      }

      // The base game's bytes, but a packaged HNMO folder leaves the base file out: the pack must carry it.
      summary.notShipped++;
    }

    const size = fs.statSync(source).size;
    folder.taken++;
    folder.takenBytes += size;
    summary.taken++;
    summary.takenBytes += size;
    if (!dryRun) {
      const target = path.join(out, rel);
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.copyFileSync(source, target);
    }
  }

  summary.outsideFolders = [...outside].sort();
  return summary;
}

function megabytes(bytes) {
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** The summary as text: one line per folder, then the totals. */
export function formatSummary(summary) {
  const lines = ['Folder        taken      size   same as base'];
  for (const [name, folder] of Object.entries(summary.folders).sort(([a], [b]) => a.localeCompare(b))) {
    lines.push(`${name.padEnd(12)} ${String(folder.taken).padStart(6)} ${megabytes(folder.takenBytes).padStart(10)} ${String(folder.same).padStart(8)}`);
  }
  lines.push(`Total        ${String(summary.taken).padStart(6)} ${megabytes(summary.takenBytes).padStart(10)} ${String(summary.same).padStart(8)}`);
  if (summary.notShipped) lines.push(`Taken although the same as base (the base allowlist leaves them out): ${summary.notShipped} files`);
  lines.push(`Left out (never served from a pack): ${summary.outside} files in ${summary.outsideFolders.join(', ') || 'no folder'}`);
  return lines.join('\n');
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  const dryRun = args.includes('--dry-run');
  const allowlistAt = args.indexOf('--base-allowlist');
  const allowlistPath = allowlistAt >= 0 ? args[allowlistAt + 1] : null;
  const [season, base, out] = args.filter((arg, i) => arg !== '--dry-run' && i !== allowlistAt && i !== allowlistAt + 1);
  if (!season || !base || !out || (allowlistAt >= 0 && !allowlistPath)) {
    console.error('Usage: node build-s9-pack.mjs <extracted Season 9 Data> <base game Data> <pack folder> [--dry-run] [--base-allowlist <file>]');
    process.exit(2);
  }

  const baseAllowlist = allowlistPath
    ? fs.readFileSync(allowlistPath, 'utf8').split(/\r?\n/).map((line) => line.trim()).filter(Boolean)
    : null;
  if (!baseAllowlist) {
    console.warn('No --base-allowlist: files equal to the base game are left out even where a packaged HNMO folder lacks them.');
  }

  if (!dryRun && fs.existsSync(out) && fs.readdirSync(out).length > 0) {
    console.error(`${out} isn't empty: pick a new folder (an old pack's files would stay in it).`);
    process.exit(2);
  }

  const summary = buildSeasonPack({ season, base, out, dryRun, baseAllowlist });
  console.log(formatSummary(summary));
  if (!dryRun) console.log(`Pack written to ${path.resolve(out)}. Package with HNMO_S9_DATA_DIR="${path.resolve(out)}".`);
}
