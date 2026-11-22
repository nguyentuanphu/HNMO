// Verifies Summoner book summons (Explosion 223, Requiem 224, Pollution 225) have a cast plan
// matching CSummonSystem::SendRequestSummonSkill and AreaSkill at the target or clicked tile.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as THREE from 'three';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const gameBase = path.resolve(__dirname, '../public/js/game');

const { skillCastPlan, skillAnimationAction } =
  await import(pathToFileURL(path.join(gameBase, 'skillCast.js')).href);
const { CharacterClass, PlayerActions } =
  await import(pathToFileURL(path.join(gameBase, 'combatLook.js')).href);
const { Combat } =
  await import(pathToFileURL(path.join(gameBase, 'combat.js')).href);

const SUMMONER_BOOK_SKILLS = [223, 224, 225]; // Explosion, Requiem, Pollution

function fakeCharacter(id, x, y) {
  let playedAction = null;
  return {
    id,
    kind: id === 1 ? 'player' : 'monster',
    cell: { x, y },
    position: new THREE.Vector3(x * 100, y * 100, 0),
    angle: 0,
    dead: false,
    isWalking: false,
    isPlayingOnce: false,
    faceTo(cell) { this.faced = cell; },
    playOnce(action) { playedAction = action; },
    get lastPlayedAction() { return playedAction; },
  };
}

test('Summoner book summons 223..225 produce AreaSkill target-tile plans and mount-aware actions', () => {
  const mounts = [
    ['none', PlayerActions.SKILL_SUMMON],
    ['uniria', PlayerActions.SKILL_SUMMON_UNI],
    ['dinorant', PlayerActions.SKILL_SUMMON_DINO],
    ['fenrir', PlayerActions.SKILL_SUMMON_FENRIR],
  ];

  for (const skill of SUMMONER_BOOK_SKILLS) {
    for (const [mount, expectedAction] of mounts) {
      const plan = skillCastPlan(skill, CharacterClass.SUMMONER, { mount });
      assert.ok(plan, `skillCastPlan(${skill}, SUMMONER, ${mount}) must not be null`);
      assert.equal(plan.packet, 'area');
      assert.equal(plan.areaAt, 'target');
      assert.equal(plan.areaExtraTarget, true);
      assert.equal(plan.needsTarget, false);
      assert.equal(plan.rotationScale, 256);
      assert.equal(plan.keepAutoAttack, true);
      assert.equal(plan.equipmentOk, true);
      assert.equal(plan.heroAction, expectedAction);

      const otherAction = skillAnimationAction(skill, CharacterClass.SUMMONER, { own: false, via: 'area', mount });
      assert.equal(otherAction, expectedAction, `areaAnimationAction(${skill}, ${mount})`);
    }
  }
});

test('Combat casts Summoner book summons 223..225 at a clicked tile or a selected monster', () => {
  const hero = fakeCharacter(1, 120, 120);
  const monster = fakeCharacter(42, 123, 124);
  const sent = [];
  const onSkillCalls = [];
  const connection = {
    onPacket: () => () => {},
    send(dir, type, payload) { sent.push({ dir, type, payload }); },
  };
  const online = {
    playerId: 1,
    hero,
    characters: new Map([[monster.id, monster]]),
    world: { flags: new Uint8Array(256 * 256) },
    cellNextTo: (cell) => cell,
    walkTo: () => {},
  };
  const inventory = { items: new Map() };
  const combat = new Combat(connection, online, inventory, CharacterClass.SUMMONER, {
    onSkill(caster, skill, target, cell) { onSkillCalls.push({ caster, skill, target, cell }); },
  });

  // 1) Ground-tile cast with no target
  assert.equal(combat.cast(null, { x: 122, y: 121 }, 224), true);
  combat.update();
  assert.equal(sent.length, 1);
  assert.equal(sent[0].type, 'AreaSkill');
  assert.equal(sent[0].payload.SkillId, 224);
  assert.equal(sent[0].payload.TargetX, 122);
  assert.equal(sent[0].payload.TargetY, 121);
  assert.equal(sent[0].payload.ExtraTargetId, 0xffff);
  assert.equal(hero.lastPlayedAction, PlayerActions.SKILL_SUMMON);
  assert.equal(onSkillCalls[0].skill, 224);

  // 2) Targeted monster cast sets ExtraTargetId to monster.id and TargetX/Y to monster's cell
  assert.equal(combat.cast(monster, { x: 123, y: 124 }, 225), true);
  combat.update();
  assert.equal(sent.length, 2);
  assert.equal(sent[1].type, 'AreaSkill');
  assert.equal(sent[1].payload.SkillId, 225);
  assert.equal(sent[1].payload.TargetX, 123);
  assert.equal(sent[1].payload.TargetY, 124);
  assert.equal(sent[1].payload.ExtraTargetId, 42);
});
