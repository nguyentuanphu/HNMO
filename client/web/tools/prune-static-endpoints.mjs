#!/usr/bin/env node
// Drops the admin panel's static asset routes whose file the package left out.
//
//   node client/web/tools/prune-static-endpoints.mjs <server folder>
//
// package-hnmo.sh deletes the precompressed .br/.gz copies under server/wwwroot (and the loose item
// icons, which are served from items.zip). The server's *.staticwebassets.endpoints.json still listed
// them: a browser that accepts gzip was answered from the missing .gz file with an empty body, the
// script's integrity check failed, and the admin panel stayed unstyled and could not save anything.
// Removing those routes leaves each asset with its plain file only.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Removes the endpoints whose asset file does not exist under <c>webRoot</c>.
 * @param {{ Endpoints: Array<{ Route: string, AssetFile: string }> }} manifest
 * @param {(assetFile: string) => boolean} exists
 * @returns {{ kept: number, removed: number }}
 */
export function pruneEndpoints(manifest, exists) {
  const before = manifest.Endpoints.length;
  manifest.Endpoints = manifest.Endpoints.filter((endpoint) => exists(endpoint.AssetFile));
  return { kept: manifest.Endpoints.length, removed: before - manifest.Endpoints.length };
}

/** Prunes every endpoints manifest in a published server folder; returns one line per file. */
export function pruneServerFolder(serverFolder) {
  const webRoot = path.join(serverFolder, 'wwwroot');
  const lines = [];
  for (const name of fs.readdirSync(serverFolder)) {
    if (!name.endsWith('.staticwebassets.endpoints.json')) continue;
    const file = path.join(serverFolder, name);
    const manifest = JSON.parse(fs.readFileSync(file, 'utf8'));
    const result = pruneEndpoints(manifest, (asset) => fs.existsSync(path.join(webRoot, asset)));
    fs.writeFileSync(file, JSON.stringify(manifest));
    lines.push(`${name}: ${result.kept} routes kept, ${result.removed} without a file removed`);
  }

  return lines;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const folder = process.argv[2];
  if (!folder) {
    console.error('Usage: node prune-static-endpoints.mjs <server folder>');
    process.exit(2);
  }

  const lines = pruneServerFolder(folder);
  console.log(lines.length ? lines.join('\n') : `No static asset manifest in ${folder}`);
}
