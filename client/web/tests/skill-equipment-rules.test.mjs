// Tests that Web Client Combat (castable and updateSkill) enforces equipment rules for every
// class (not only Rage Fighter), matching the desktop client's CanExecuteSkill / Attack* rules.
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
const { skillCastPlan, SkillId } =
  await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/skillCast.js')).href);

await loadItemCatalog();

function createHarness(classIndex, equipped) {
  const sent = [];
  const walked = [];
  const actions = [];
  const messages = [];
  const monster = { id: 99, kind: 'monster', dead: false, cell: { x: 101, y: 100 }, position: { distanceTo: () => 100 } };
  const hero = {
    id: 1,
    kind: 'player',
    dead: false,
    cell: { x: 100, y: 100 },
    angle: 0,
    isWalking: false,
    isPlayingOnce: false,
    position: { distanceTo: (other) => Math.hypot(other.x ?? 10100 - 10000, 0) },
    faceTo: () => {},
    playOnce: (action) => actions.push(action),
  };
  const items = new Map();
  if (equipped.right) items.set(0, equipped.right);
  if (equipped.left) items.set(1, equipped.left);
  if (equipped.pet) items.set(8, equipped.pet);
  const connection = {
    onPacket: () => () => {},
    send: (group, name, fields) => sent.push({ group, name, fields }),
  };
  const online = {
    playerId: 1,
    hero,
    world: { flags: new Uint8Array(256 * 256) },
    characters: new Map([[99, monster]]),
    cellNextTo: (cell) => ({ ...cell }),
    walkTo: (cell) => { walked.push(cell); return true; },
  };
  const combat = new Combat(connection, online, { items }, classIndex, {
    onMessage: (text) => messages.push(text),
  });
  combat.skills = { current: () => null, mana: () => 9999, ability: () => 9999 };
  return { combat, monster, sent, walked, actions, messages };
}

function tryCast(classIndex, equipped, skill) {
  const h = createHarness(classIndex, equipped);
  const castable = h.combat.castable(skill);
  h.combat.cast(h.monster, { ...h.monster.cell }, skill);
  h.combat.update();
  return { castable, sent: h.sent, walked: h.walked, actions: h.actions, messages: h.messages, casting: h.combat.casting };
}

// Item catalog entries: (0,0) Kris sword, (3,0) Light Spear, (4,0) Short Bow, (4,8) Crossbow,
// (5,0) Skull Staff, (0,32) Sacred Glove, (0,38) S9 Frick Large Glove,
// (13,2) Uniria, (13,4) Dark Horse, (13,37) Horn of Fenrir.
const SWORD = { group: 0, number: 0 };
const SPEAR = { group: 3, number: 0 };
const BOW = { group: 4, number: 0 };
const CROSSBOW = { group: 4, number: 8 };
const STAFF = { group: 5, number: 0 };
const GLOVE_S6 = { group: 0, number: 32 };
const UNIRIA = { group: 13, number: 2 };
const DARK_HORSE = { group: 13, number: 4 };
const FENRIR = { group: 13, number: 37 };

const CLASS_DK = 1;
const CLASS_ELF = 2;
const CLASS_MG = 3;
const CLASS_DL = 4;
const CLASS_RF = 6;

test('Dark Knight Impale (47) requires both a spear and a mount', () => {
  const noMount = tryCast(CLASS_DK, { right: SPEAR }, SkillId.IMPALE);
  assert.equal(noMount.castable, false);
  assert.equal(noMount.sent.length, 0);
  assert.equal(noMount.walked.length, 0);
  assert.equal(noMount.actions.length, 0);

  const swordMount = tryCast(CLASS_DK, { right: SWORD, pet: UNIRIA }, SkillId.IMPALE);
  assert.equal(swordMount.castable, false);
  assert.equal(swordMount.sent.length, 0);

  const ok = tryCast(CLASS_DK, { right: SPEAR, pet: UNIRIA }, SkillId.IMPALE);
  assert.equal(ok.castable, true);
  assert.equal(ok.sent[0]?.name, 'TargetedSkill');
});

test('Magic Gladiator Power Slash (56) refuses while riding a mount and requires melee weapon', () => {
  const mounted = tryCast(CLASS_MG, { right: SWORD, pet: FENRIR }, SkillId.POWER_SLASH);
  assert.equal(mounted.castable, false);
  assert.equal(mounted.sent.length, 0);
  assert.equal(mounted.actions.length, 0);

  const unarmed = tryCast(CLASS_MG, {}, SkillId.POWER_SLASH);
  assert.equal(unarmed.castable, false);
  assert.equal(unarmed.sent.length, 0);

  const ok = tryCast(CLASS_MG, { right: SWORD }, SkillId.POWER_SLASH);
  assert.equal(ok.castable, true);
  assert.equal(ok.sent[0]?.name, 'AreaSkill');
});

test('Dark Knight Death Stab (43), Spiral Slash (57), Rageful Blow (42), Fire Slash (55) enforce weapon rules', () => {
  assert.equal(tryCast(CLASS_DK, { right: STAFF }, SkillId.DEATHSTAB).sent.length, 0);
  assert.equal(tryCast(CLASS_DK, {}, SkillId.DEATHSTAB).castable, false);
  assert.equal(tryCast(CLASS_DK, { right: SWORD }, SkillId.DEATHSTAB).sent.length, 1);

  assert.equal(tryCast(CLASS_DK, { right: SPEAR }, SkillId.SPIRAL_SLASH).sent.length, 0);
  assert.equal(tryCast(CLASS_DK, { right: SWORD }, SkillId.SPIRAL_SLASH).sent.length, 1);

  assert.equal(tryCast(CLASS_DK, {}, SkillId.RAGEFUL_BLOW).sent.length, 0);
  assert.equal(tryCast(CLASS_DK, { right: STAFF }, SkillId.RAGEFUL_BLOW).castable, false);
  assert.equal(tryCast(CLASS_DK, { right: SWORD }, SkillId.RAGEFUL_BLOW).sent.length, 1);

  assert.equal(tryCast(CLASS_MG, { right: STAFF }, SkillId.FIRE_SLASH).sent.length, 0);
  assert.equal(tryCast(CLASS_MG, { right: SWORD }, SkillId.FIRE_SLASH).sent.length, 1);
});

test('Dark Lord Earthshake (62) and any class Plasma Storm (76) require their mounts', () => {
  assert.equal(tryCast(CLASS_DL, {}, SkillId.EARTHSHAKE).castable, false);
  assert.equal(tryCast(CLASS_DL, {}, SkillId.EARTHSHAKE).sent.length, 0);
  assert.equal(tryCast(CLASS_DL, { pet: UNIRIA }, SkillId.EARTHSHAKE).sent.length, 0);
  assert.equal(tryCast(CLASS_DL, { pet: DARK_HORSE }, SkillId.EARTHSHAKE).sent.length, 1);

  assert.equal(tryCast(CLASS_DK, { pet: UNIRIA }, SkillId.PLASMA_STORM_FENRIR).sent.length, 0);
  assert.equal(tryCast(CLASS_DK, { pet: FENRIR }, SkillId.PLASMA_STORM_FENRIR).sent.length, 1);
});

test('Fairy Elf bow skills (24, 51, 52, 235) require a bow or crossbow', () => {
  for (const skill of [SkillId.TRIPLE_SHOT, SkillId.ICE_ARROW, SkillId.PENETRATION, SkillId.MULTI_SHOT]) {
    const unarmed = tryCast(CLASS_ELF, {}, skill);
    assert.equal(unarmed.castable, false, `unarmed ${skill}`);
    assert.equal(unarmed.sent.length, 0, `unarmed sent ${skill}`);

    const bowOk = tryCast(CLASS_ELF, { left: BOW }, skill);
    assert.equal(bowOk.castable, true, `bow ${skill}`);
    assert.equal(bowOk.sent.length, 1, `bow sent ${skill}`);

    const xbowOk = tryCast(CLASS_ELF, { right: CROSSBOW }, skill);
    assert.equal(xbowOk.castable, true, `crossbow ${skill}`);
    assert.equal(xbowOk.sent.length, 1, `crossbow sent ${skill}`);
  }
});

test('Rage Fighter glove and mount rules apply to strengthened Chain Drive (558) and Season 9 glove (0,38)', () => {
  const noGlove = skillCastPlan(SkillId.CHAIN_DRIVE_STR, CLASS_RF, {});
  assert.equal(noGlove?.equipmentOk, false);

  const withS6Glove = skillCastPlan(SkillId.CHAIN_DRIVE_STR, CLASS_RF, { rightHand: GLOVE_S6 });
  assert.equal(withS6Glove?.equipmentOk, true);

  const withS9Glove = skillCastPlan(SkillId.CHAIN_DRIVE_STR, CLASS_RF, { rightHand: { group: 0, number: 38 } });
  assert.equal(withS9Glove?.equipmentOk, true);
});
