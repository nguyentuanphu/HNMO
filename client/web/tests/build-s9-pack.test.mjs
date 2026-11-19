// Unit tests of tools/build-s9-pack.mjs with small made-up files (run: node --test client/web/tests/*.test.mjs).
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { buildSeasonPack, formatSummary } from '../tools/build-s9-pack.mjs';

function makeTree(root, files) {
  for (const [rel, content] of Object.entries(files)) {
    const file = path.join(root, rel);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, content);
  }
}

function setUp() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'hnmo-s9-pack-'));
  const season = path.join(root, 'season');
  const base = path.join(root, 'base');
  const out = path.join(root, 'pack');
  makeTree(base, {
    'Player/player.bmd': 'base player',
    'Item/Sword01.bmd': 'same sword',
    'World1/EncTerrain1.map': 'base map',
    'Local/Item.bmd': 'base texts',
    'Sound/hit.wav': 'same sound',
  });
  makeTree(season, {
    'Player/player.bmd': 'new player model',
    // Another case than the base game's, in a folder of its own: on Windows "item" and "Item" are one folder.
    'item/SWORD01.bmd': 'same sword',
    'Monster/Monster300.bmd': 'new monster',
    'World2/Thumbs.db': 'thumbnail cache',
    'World2/descript.ion': 'file comments',
    'World1/EncTerrain1.map': 'new map',
    'World92/EncTerrain92.map': 'new land',
    'Object92/Object01.bmd': 'new object',
    'Local/Item.bmd': 'season texts',
    'Interface/frame.ozj': 'season interface',
    'Sound/hit.wav': 'same sound',
    'Music/new.mp3': 'new music',
    'readme.txt': 'top-level file',
  });
  return { root, season, base, out };
}

test('takes only the served folders, and only files that differ from the base game', () => {
  const { root, season, base, out } = setUp();
  try {
    const summary = buildSeasonPack({ season, base, out });
    const written = [];
    const walk = (dir, rel = '') => {
      for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const next = rel ? `${rel}/${entry.name}` : entry.name;
        if (entry.isDirectory()) walk(path.join(dir, entry.name), next);
        else written.push(next);
      }
    };
    walk(out);
    assert.deepEqual(written.sort(), [
      'Monster/Monster300.bmd',
      'Music/new.mp3',
      'Object92/Object01.bmd',
      'Player/player.bmd',
      'World1/EncTerrain1.map',
      'World92/EncTerrain92.map',
    ]);
    assert.equal(fs.readFileSync(path.join(out, 'Player/player.bmd'), 'utf8'), 'new player model');
    assert.equal(summary.taken, 6);
    assert.equal(summary.same, 2, 'the sword (other case) and the sound are the base game\'s bytes');
    assert.equal(summary.outside, 3, 'folder notes (Thumbs.db, descript.ion) are not counted at all');
    assert.deepEqual(summary.outsideFolders, ['(top level)', 'Interface', 'Local']);
    assert.deepEqual(summary.folders.WorldN, { taken: 2, takenBytes: 'new map'.length + 'new land'.length, same: 0, outside: 0 });
    assert.equal(summary.folders.Item.same, 1);
    assert.equal(summary.folders.Monster.taken, 1);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('with the base allowlist, a base file the HNMO folder leaves out is taken although it is the same', () => {
  const { root, season, base, out } = setUp();
  try {
    // The packaged base keeps the sword and the player, not the sound: the season's equal sound must come along.
    const summary = buildSeasonPack({ season, base, out, baseAllowlist: ['Item\\Sword01.bmd', 'player/PLAYER.bmd'] });
    assert.equal(summary.same, 1, 'only the sword is left out');
    assert.equal(summary.notShipped, 1);
    assert.equal(summary.taken, 7);
    assert.equal(fs.readFileSync(path.join(out, 'Sound/hit.wav'), 'utf8'), 'same sound');
    assert.equal(fs.existsSync(path.join(out, 'item')), false);
    assert.match(formatSummary(summary), /Taken although the same as base \(the base allowlist leaves them out\): 1 files/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('a dry run counts the same way and writes nothing', () => {
  const { root, season, base, out } = setUp();
  try {
    const summary = buildSeasonPack({ season, base, out, dryRun: true });
    assert.equal(summary.taken, 6);
    assert.equal(fs.existsSync(out), false);
    const text = formatSummary(summary);
    assert.match(text, /^Total\s+6\s/m);
    assert.match(text, /Left out \(never served from a pack\): 3 files in \(top level\), Interface, Local/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('refuses a pack folder inside the Season 9 folder and missing input folders', () => {
  const { root, season, base } = setUp();
  try {
    assert.throws(() => buildSeasonPack({ season, base, out: path.join(season, 'pack') }), /must not be inside/);
    assert.throws(() => buildSeasonPack({ season: path.join(root, 'nope'), base, out: path.join(root, 'x') }), /No Season 9 Data folder/);
    assert.throws(() => buildSeasonPack({ season, base: path.join(root, 'nope'), out: path.join(root, 'x') }), /No base game Data folder/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
