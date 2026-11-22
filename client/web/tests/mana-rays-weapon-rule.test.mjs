// Verifies Mana Rays (skill 73, AT_SKILL_DEATH_CANNON) enforces the right-hand staff/book rule in Combat.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDirectory = path.resolve(__dirname, '../../src/bin/Data');

globalThis.fetch = async (url) => {
  const match = /^\/data\/(.+)$/.exec(String(url));
  if (!match) return { ok: false, status: 404, json: async () => null, arrayBuffer: async () => new ArrayBuffer(0) };
  try {
    const buf = await readFile(path.join(dataDirectory, match[1]));
    return {
      ok: true,
      status: 200,
      json: async () => JSON.parse(buf.toString('utf8').replace(/^\uFEFF/, '')),
      arrayBuffer: async () => buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength),
    };
  } catch {
    return { ok: false, status: 404, json: async () => null, arrayBuffer: async () => new ArrayBuffer(0) };
  }
};

const { loadItemCatalog } =
  await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/items.js')).href);
const { Combat } =
  await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/combat.js')).href);
const { SkillId } =
  await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/skillCast.js')).href);

await loadItemCatalog();

function tryManaRays(rightHand) {
  const sent = [];
  const monster = { id: 99, kind: 'monster', dead: false, cell: { x: 101, y: 100 }, position: { distanceTo: () => 100 } };
  const hero = {
    id: 1, kind: 'player', dead: false, cell: { x: 100, y: 100 }, angle: 0, isWalking: false, isPlayingOnce: false,
    position: { distanceTo: () => 100 }, faceTo: () => {}, playOnce: () => {},
  };
  const items = new Map();
  if (rightHand) items.set(0, rightHand);
  const connection = { onPacket: () => () => {}, send: (_g, name, fields) => sent.push({ name, fields }) };
  const online = {
    playerId: 1, hero, world: { flags: new Uint8Array(256 * 256) },
    characters: new Map([[99, monster]]), cellNextTo: (c) => ({ ...c }), walkTo: () => true,
  };
  const combat = new Combat(connection, online, { items }, 3); // Magic Gladiator
  combat.skills = { current: () => null, mana: () => 9999, ability: () => 9999 };
  const castable = combat.castable(SkillId.DEATH_CANNON);
  combat.cast(monster, { ...monster.cell }, SkillId.DEATH_CANNON);
  combat.update();
  return { castable, sent };
}

test('Mana Rays (73) requires a staff or book in the right hand in Combat', () => {
  const unarmed = tryManaRays(null);
  assert.equal(unarmed.castable, false);
  assert.equal(unarmed.sent.length, 0);

  const sword = tryManaRays({ group: 0, number: 23 }); // Explosion Blade
  assert.equal(sword.castable, false);
  assert.equal(sword.sent.length, 0);

  const staff = tryManaRays({ group: 5, number: 0 }); // Skull Staff
  assert.equal(staff.castable, true);
  assert.equal(staff.sent[0]?.name, 'TargetedSkill');
});
