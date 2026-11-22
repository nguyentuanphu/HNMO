// Verifies the client skill table (skill_eng.bmd) matches the Season 6 and Season 9 server suites for
// Recovery (168), Power Slash (100), and the seven Fairy Elf summons (90, 170, 190, 230, 250, 260, 280).
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDirectory = path.resolve(__dirname, '../../src/bin/Data');

globalThis.fetch = async (url) => {
  const match = /^\/data\/(.+)$/.exec(String(url));
  if (!match) return { ok: false, status: 404, arrayBuffer: async () => new ArrayBuffer(0) };
  try {
    const buf = await readFile(path.join(dataDirectory, match[1]));
    return {
      ok: true,
      status: 200,
      arrayBuffer: async () => buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength),
    };
  } catch {
    return { ok: false, status: 404, arrayBuffer: async () => new ArrayBuffer(0) };
  }
};

const { loadSkillData, requiredEnergy } =
  await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/skills.js')).href);

test('client skill table matches server energy requirements for Recovery, Power Slash and Elf summons', async () => {
  await loadSkillData();

  assert.equal(requiredEnergy(234, 2), 168, 'Recovery');
  assert.equal(requiredEnergy(56, 3), 100, 'Power Slash');

  const expectedSummons = [
    [30, 90, 'Summon Goblin'],
    [31, 170, 'Summon Stone Golem'],
    [32, 190, 'Summon Assassin'],
    [33, 230, 'Summon Elite Yeti'],
    [34, 250, 'Summon Dark Knight'],
    [35, 260, 'Summon Bali'],
    [36, 280, 'Summon Soldier'],
  ];

  for (const [id, energy, name] of expectedSummons) {
    assert.equal(requiredEnergy(id, 2), energy, name);
  }
});
