// Verifies self buffs (and party buffs when the selected target is a monster) do not walk
// toward or face a selected out-of-range monster, while party buffs still walk to allied players.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as THREE from 'three';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDirectory = path.resolve(__dirname, '../../src/bin/Data');
const gameBase = path.resolve(__dirname, '../public/js/game');

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

const { loadSkillData, skillInfo } = await import(pathToFileURL(path.join(gameBase, 'skills.js')).href);
const { CharacterClass } = await import(pathToFileURL(path.join(gameBase, 'combatLook.js')).href);
const { Combat } = await import(pathToFileURL(path.join(gameBase, 'combat.js')).href);

function fakeCharacter(id, kind, x, y) {
  return {
    id,
    kind,
    cell: { x, y },
    position: new THREE.Vector3(x * 100, y * 100, 0),
    angle: 0,
    dead: false,
    isWalking: false,
    isPlayingOnce: false,
    faced: null,
    faceTo(cell) { this.faced = cell; },
    playOnce(action) { this.lastAction = action; },
  };
}

function makeHarness(classIndex, extraCharacters = []) {
  const hero = fakeCharacter(1, 'player', 120, 120);
  const sent = [];
  const walkCalls = [];
  const onSkillCalls = [];
  const connection = {
    onPacket: () => () => {},
    send(dir, type, payload) { sent.push({ dir, type, payload }); },
  };
  const online = {
    playerId: 1,
    hero,
    characters: new Map(extraCharacters.map((c) => [c.id, c])),
    world: { flags: new Uint8Array(256 * 256) },
    cellNextTo: (cell) => ({ x: cell.x - 1, y: cell.y }),
    walkTo(cell, opts) { walkCalls.push({ cell, opts }); },
  };
  const combat = new Combat(connection, online, { items: new Map() }, classIndex, {
    onSkill(caster, skill, target, cell) { onSkillCalls.push({ caster, skill, target, cell }); },
  });
  return { hero, sent, walkCalls, onSkillCalls, combat };
}

test('self buffs do not walk towards a far selected monster and cast on self immediately', async () => {
  await loadSkillData();
  // Expansion of Wizardry (233) has distance 6 in skill_eng.bmd; monster is 8 cells away.
  assert.ok((skillInfo(233)?.distance ?? 0) > 0, 'Expansion of Wizardry has positive skill distance');

  const selfBuffCases = [
    [CharacterClass.DARK_WIZARD, 233, 'Expansion of Wizardry'],
    [CharacterClass.SUMMONER, 218, 'Berserker'],
    [CharacterClass.DARK_KNIGHT, 48, 'Swell Life'],
    [CharacterClass.DARK_LORD, 64, 'Increase Critical Damage'],
    [CharacterClass.ELF, 77, 'Infinity Arrow'],
  ];

  for (const [cls, skill, label] of selfBuffCases) {
    const monster = fakeCharacter(50, 'monster', 128, 120);
    const { hero, sent, walkCalls, onSkillCalls, combat } = makeHarness(cls, [monster]);

    assert.equal(combat.cast(monster, { ...monster.cell }, skill), true, `${label}: cast accepted`);
    combat.update();

    assert.equal(walkCalls.length, 0, `${label}: must not walk toward selected monster`);
    assert.equal(hero.faced, null, `${label}: must not turn toward selected monster`);
    assert.equal(sent.length, 1, `${label}: TargetedSkill sent immediately`);
    assert.equal(sent[0].type, 'TargetedSkill');
    assert.equal(sent[0].payload.SkillId, skill);
    assert.equal(sent[0].payload.TargetId, 1);
    assert.equal(onSkillCalls[0].target, null, `${label}: onSkill receives null target for self cast`);
    assert.equal(onSkillCalls[0].cell, null, `${label}: onSkill receives null cell for self cast`);
  }
});

test('party buffs cast on self without walking when target is a monster, but walk to a far allied player', async () => {
  await loadSkillData();

  for (const [cls, skill, label] of [
    [CharacterClass.ELF, 26, 'Heal'],
    [CharacterClass.DARK_WIZARD, 16, 'Soul Barrier'],
    [CharacterClass.SUMMONER, 217, 'Damage Reflection'],
  ]) {
    const monster = fakeCharacter(51, 'monster', 129, 120);
    const monsterHarness = makeHarness(cls, [monster]);
    assert.equal(monsterHarness.combat.cast(monster, { ...monster.cell }, skill), true);
    monsterHarness.combat.update();
    assert.equal(monsterHarness.walkCalls.length, 0, `${label} vs monster: must not walk`);
    assert.equal(monsterHarness.sent[0].payload.TargetId, 1, `${label} vs monster: casts on self`);

    const ally = fakeCharacter(77, 'player', 129, 120);
    const allyHarness = makeHarness(cls, [ally]);
    assert.equal(allyHarness.combat.cast(ally, { ...ally.cell }, skill), true);
    allyHarness.combat.update();
    assert.equal(allyHarness.walkCalls.length, 1, `${label} vs far ally: walks into range`);
    assert.equal(allyHarness.sent.length, 0, `${label} vs far ally: waits until in range`);
  }
});
