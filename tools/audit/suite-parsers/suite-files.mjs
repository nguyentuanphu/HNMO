// Helpers for reading HNMO CSV dumps and resolving/parsing Season 6 and Season 9 server suite tables
// from LEGACY_S6_DIR and LEGACY_S9_DIR without hardcoding local paths or vendor names.

import fs from 'node:fs';
import path from 'node:path';

const SKIP_DIRS = new Set(['tools', 'editor', '.git', 'node_modules']);

/**
 * Recursively walks a directory and finds a file whose basename (stripped of any vendor prefix before '_')
 * matches `targetName` (case-insensitive), skipping Tools/Editor folders.
 */
export function findSuiteFile(rootDir, targetName, preferDir = null) {
  if (!rootDir || !fs.existsSync(rootDir)) return null;
  const target = targetName.toLowerCase();
  const dirHint = preferDir ? preferDir.toLowerCase() : null;
  const exactMatches = [];
  const suffixMatches = [];
  const queue = [rootDir];
  while (queue.length > 0) {
    const dir = queue.shift();
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (!SKIP_DIRS.has(entry.name.toLowerCase())) queue.push(path.join(dir, entry.name));
        continue;
      }
      const lower = entry.name.toLowerCase();
      const full = path.join(dir, entry.name);
      const stripped = lower.includes('_') ? lower.slice(lower.indexOf('_') + 1) : lower;
      if (lower === target || stripped === target) {
        exactMatches.push(full);
      } else if (lower.endsWith(target)) {
        suffixMatches.push(full);
      }
    }
  }
  const pool = exactMatches.length > 0 ? exactMatches : suffixMatches;
  if (pool.length === 0) return null;
  if (dirHint) {
    const preferred = pool.find((p) => p.toLowerCase().includes(dirHint));
    if (preferred) return preferred;
  }
  return pool[0];
}

/** Finds all files under a suite folder whose relative directory contains `dirHint` and extension matches `ext`. */
export function findSuiteFilesInDir(rootDir, dirHint, ext) {
  if (!rootDir || !fs.existsSync(rootDir)) return [];
  const results = [];
  const hint = dirHint.toLowerCase();
  const targetExt = ext.toLowerCase();
  const queue = [rootDir];
  while (queue.length > 0) {
    const dir = queue.shift();
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (!SKIP_DIRS.has(entry.name.toLowerCase())) queue.push(path.join(dir, entry.name));
      } else if (entry.name.toLowerCase().endsWith(targetExt) && dir.toLowerCase().includes(hint)) {
        results.push(path.join(dir, entry.name));
      }
    }
  }
  return results.sort();
}

/** Minimal RFC-4180 CSV parser returning an array of keyed objects. */
export function parseCsv(filePath) {
  const text = fs.readFileSync(filePath, 'utf8').replace(/^\uFEFF/, '');
  const rows = [];
  let row = [];
  let field = '';
  let inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inQuotes) {
      if (c === '"' && text[i + 1] === '"') {
        field += '"';
        i++;
      } else if (c === '"') {
        inQuotes = false;
      } else {
        field += c;
      }
    } else if (c === '"') {
      inQuotes = true;
    } else if (c === ',') {
      row.push(field);
      field = '';
    } else if (c === '\n' || c === '\r') {
      if (c === '\r' && text[i + 1] === '\n') i++;
      row.push(field);
      if (row.length > 1 || row[0] !== '') rows.push(row);
      row = [];
      field = '';
    } else {
      field += c;
    }
  }
  if (field !== '' || row.length > 0) {
    row.push(field);
    rows.push(row);
  }
  if (rows.length === 0) return [];
  const header = rows[0];
  return rows.slice(1).map((cols) => {
    const obj = {};
    for (let i = 0; i < header.length; i++) obj[header[i]] = cols[i] ?? '';
    return obj;
  });
}

function stripXmlComments(xml) {
  return xml.replace(/<!--[\s\S]*?-->/g, '');
}

/** Parses attributes of all uncommented XML elements with tag `tagName`. */
export function parseXmlTags(filePath, tagName) {
  if (!filePath || !fs.existsSync(filePath)) return [];
  const xml = stripXmlComments(fs.readFileSync(filePath, 'utf8'));
  const re = new RegExp(`<${tagName}\\s+([^>]*?)\\s*/?>`, 'g');
  const out = [];
  for (const m of xml.matchAll(re)) {
    const attrs = {};
    for (const a of m[1].matchAll(/([A-Za-z0-9_:-]+)="([^"]*)"/g)) {
      attrs[a[1]] = a[2];
    }
    out.push(attrs);
  }
  return out;
}

/** Parses uncommented hierarchical XML sections like <Section Index="0"><Item .../></Section>. */
export function parseXmlSections(filePath, sectionTag, sectionAttr, itemTag) {
  if (!filePath || !fs.existsSync(filePath)) return [];
  const xml = stripXmlComments(fs.readFileSync(filePath, 'utf8'));
  const secRe = new RegExp(`<${sectionTag}\\b([^>]*)>([\\s\\S]*?)</${sectionTag}>`, 'g');
  const itemRe = new RegExp(`<${itemTag}\\s+([^>]*?)\\s*/?>`, 'g');
  const rows = [];
  for (const sm of xml.matchAll(secRe)) {
    const secAttrs = {};
    for (const a of sm[1].matchAll(/([A-Za-z0-9_:-]+)="([^"]*)"/g)) secAttrs[a[1]] = a[2];
    const secId = sectionAttr ? secAttrs[sectionAttr] : undefined;
    for (const im of sm[2].matchAll(itemRe)) {
      const attrs = {};
      for (const a of im[1].matchAll(/([A-Za-z0-9_:-]+)="([^"]*)"/g)) attrs[a[1]] = a[2];
      rows.push({ section: secId, sectionAttrs: secAttrs, attrs });
    }
  }
  return rows;
}

/** Parses INI files into { sectionName: { key: value } }. */
export function parseIni(filePath) {
  if (!filePath || !fs.existsSync(filePath)) return {};
  const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/);
  const result = {};
  let current = '';
  for (const raw of lines) {
    const line = raw.replace(/\/\/.*$/, '').replace(/;.*$/, '').trim();
    if (!line) continue;
    const sec = /^\[([^\]]+)\]$/.exec(line);
    if (sec) {
      current = sec[1].trim();
      result[current] ??= {};
      continue;
    }
    const kv = /^([^=]+?)=(.*)$/.exec(line);
    if (kv) {
      result[current] ??= {};
      result[current][kv[1].trim()] = kv[2].trim();
    }
  }
  return result;
}

/** Parses classic sectioned TXT/INI tables (`0 ... end`). */
export function parseSectionedTxt(filePath) {
  if (!filePath || !fs.existsSync(filePath)) return new Map();
  const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/);
  const sections = new Map();
  let curSec = null;
  for (const raw of lines) {
    const line = raw.replace(/\/\/.*$/, '').trim();
    if (!line) continue;
    if (curSec === null) {
      const firstToken = line.split(/\s+/)[0];
      if (/^\d+$/.test(firstToken)) {
        curSec = Number(firstToken);
        if (!sections.has(curSec)) sections.set(curSec, []);
      }
      continue;
    }
    if (line.toLowerCase() === 'end') {
      curSec = null;
      continue;
    }
    const tokens = [];
    for (const tm of line.matchAll(/"([^"]*)"|(\S+)/g)) {
      tokens.push(tm[1] !== undefined ? tm[1] : tm[2]);
    }
    if (tokens.length > 0) sections.get(curSec).push(tokens);
  }
  return sections;
}
