// What the desktop draws when a skill is cast, for the first skills of each class. The timeline
// is MoveCharacter's (Engine/Object/ZzzCharacter.cpp): the cast sets AttackTime to 1; every tick
// AttackStage and AttackEffect run and AttackTime grows by one; when it reaches
// g_iLimitAttackTime (15) the skill's release effects are made (the switch on c->Skill after
// "AttackTime >= g_iLimitAttackTime"), with the hit's blood and the sword skills' sparks.
//
// Followed C++ (client/src/source):
//   MoveCharacter (release: AT_SKILL_FIREBALL, AT_SKILL_POWERWAVE, AT_SKILL_ENERGYBALL,
//     AT_SKILL_HEALING, AT_SKILL_DEFENSE, AT_SKILL_ATTACK, AT_SKILL_TRIPLE_SHOT; tc->Hit blood;
//     CreateSpark for PLAYER_ATTACK_SKILL_SWORD1-5), AttackStage (AT_SKILL_POWER_SLASH),
//     AttackEffect (AT_SKILL_LIGHTNING, the player / default case)  Engine/Object/ZzzCharacter.cpp
//   CreateSpark                                                      Render/Effects/ZzzEffectBlurSpark.cpp
//   CreateArrows (the Triple Shot fan), CreateTeleportBegin           Render/Effects/ZzzEffectMagicSkill.cpp
//   ReceiveMagic (AT_SKILL_TELEPORT, AT_SKILL_NOVA_BEGIN)            Network/Server/WSclient.cpp

import * as THREE from 'three';
import { createAngle2D, randInt } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import type { EffectOwner } from './types.js';

/** The skill numbers (AT_SKILL_* in Core/Globals/_enum.h) whose effects are ported. */
export const SkillEffect = {
  POISON: 1,
  METEORITE: 2,
  LIGHTNING: 3,
  FIRE_BALL: 4,
  FLAME: 5,
  TELEPORT: 6,
  ICE: 7,
  TWISTER: 8,
  EVIL_SPIRIT: 9,
  HELLFIRE: 10,
  POWER_WAVE: 11,
  AQUA_BEAM: 12,
  INFERNO: 14,
  SOUL_BARRIER: 16,
  ENERGY_BALL: 17,
  FALLING_SLASH: 19,
  LUNGE: 20,
  UPPERCUT: 21,
  CYCLONE: 22,
  SLASH: 23,
  TRIPLE_SHOT: 24,
  HEALING: 26,
  DEFENSE: 27,
  ATTACK: 28,
  DECAY: 38,
  ICE_STORM: 39,
  NOVA: 40,
  POWER_SLASH: 56,
  NOVA_BEGIN: 58,
} as const;

/** PLAYER_SKILL_HELL_BEGIN and PLAYER_SKILL_HELL_START: Nova's charge and release actions. */
const NOVA_CHARGE_ACTIONS: readonly number[] = [72, 73];
/** The player model's bones Nova's charge lights (0, 2, ... 38). */
const NOVA_BONES = 40;
const TERRAIN_SCALE = 100;

/** g_iLimitAttackTime: the release comes when AttackTime (1 at the cast) reaches it. */
export const ATTACK_LIMIT_TIME = 15;
/** Seconds from the cast to the release (14 ticks). */
export const SKILL_RELEASE_DELAY_SECONDS = (ATTACK_LIMIT_TIME - 1) / 25;

const WHITE = new THREE.Vector3(1, 1, 1);
const SWORD_SKILLS: readonly number[] = [SkillEffect.FALLING_SLASH, SkillEffect.LUNGE, SkillEffect.UPPERCUT, SkillEffect.CYCLONE, SkillEffect.SLASH];
/** AttackEffect: a monster's lightning starts this far down its weapon bone; without a bone the start is over the feet. */
const LIGHTNING_FALLBACK_HEIGHT = 130;
const BLOOD_DROPS = 10;
const SPARKS = 20;

export interface SkillEffectOptions {
  /** The target was hit (tc->Hit >= 1, from the damage packet): it bleeds. Default true. */
  hit?: boolean;
  /** Slash on an odd SwordCount plays PLAYER_ATTACK_TWO_HAND_SWORD3, which makes no sparks. */
  slashTwoHandAction?: boolean;
  /** Greater Defense: the target already had eBuff_Defense (the orbiting lights then aren't remade while they exist). */
  targetHadDefense?: boolean;
  /** The caster is cloaked (eBuff_Cloaking): Greater Defense and Greater Damage make nothing. */
  casterCloaked?: boolean;
  /**
   * Shoots one arrow turned by `angleOffsetDegrees` from the caster's facing, at `target` or
   * straight ahead (CreateArrow; the web client's ProjectileLayer.shoot). Triple Shot calls it
   * at the release.
   */
  shootArrow?: (angleOffsetDegrees: number, target: EffectOwner | null) => void;
  /** The caster's bow and crossbow (group 4 item numbers) for Triple Shot's fan. */
  bowNumbers?: { rightHand?: number; leftHand?: number };
  /** The tile an area skill was cast at (c->SkillX, c->SkillY). */
  cell?: { x: number; y: number } | null;
}

/** Starts a skill's effects at its cast; returns false for a skill whose effects aren't ported. */
export function playSkillEffect(system: EffectSystem, skill: number, caster: EffectOwner, target: EffectOwner | null,
  options: SkillEffectOptions = {}): boolean {
  if (!isPortedSkill(skill)) return false;
  // Effects made when the cast arrives (ReceiveMagic), not at the release.
  if (skill === SkillEffect.TELEPORT) {
    system.createEffect('spark3', caster.position, caster.angle, caster.light ?? WHITE);
    return true;
  }
  if (skill === SkillEffect.NOVA_BEGIN) {
    novaCharge(system, caster);
    return true;
  }
  let attackTime = 1;
  system.schedule(() => {
    attackStage(system, skill, caster, attackTime);
    attackEffect(system, skill, caster, target);
    if (skill === SkillEffect.POWER_SLASH) attackTime = ATTACK_LIMIT_TIME;
    attackTime += 1;
    if (attackTime < ATTACK_LIMIT_TIME) return true;
    release(system, skill, caster, target, options);
    return false;
  });
  return true;
}

export function isPortedSkill(skill: number): boolean {
  return (Object.values(SkillEffect) as number[]).includes(skill);
}

/**
 * CreateArrows with Skill 1: Triple Shot's fan, as offsets from the facing. Some bows shoot four
 * (Divine Crossbow of Archangel, Great Reign Crossbow, Albatross, Stinger and Air Lyn Bow).
 */
export function tripleShotAngles(rightHand?: number, leftHand?: number): number[] {
  const fourArrows = rightHand === 18 || rightHand === 19 || leftHand === 22 || leftHand === 23 || leftHand === 24;
  return fourArrows ? [5, 15, -5, -15] : [0, 15, -15];
}

// AttackStage: Power Slash's five blades leave in a fan on its first tick.
function attackStage(system: EffectSystem, skill: number, caster: EffectOwner, attackTime: number): void {
  if (skill !== SkillEffect.POWER_SLASH || attackTime !== 1) return;
  const angle = caster.angle.clone();
  angle.z -= 40;
  for (let i = 0; i < 5; i++) {
    system.createEffect('magic2', caster.position, angle, caster.light ?? WHITE, 2, caster);
    angle.z += 20;
  }
}

// AttackEffect, the player (default) case of Lightning: two bolts and an energy flash from the
// weapon every tick until the release.
function attackEffect(system: EffectSystem, skill: number, caster: EffectOwner, target: EffectOwner | null): void {
  if (skill !== SkillEffect.LIGHTNING || !target) return;
  const position = caster.weaponPosition?.(0) ?? caster.position.clone().setZ(caster.position.z + LIGHTNING_FALLBACK_HEIGHT);
  const angle = new THREE.Vector3(-60, 0, caster.angle.z);
  system.createJoint('jointThunder', position, target.position, angle, 0, target, 50);
  system.createJoint('jointThunder', position, target.position, angle, 0, target, 10);
  system.createParticle('energy', position, caster.angle, WHITE);
}

function release(system: EffectSystem, skill: number, caster: EffectOwner, target: EffectOwner | null, options: SkillEffectOptions): void {
  releaseAtCaster(system, skill, caster, options);
  if (!target) {
    // TargetCharacter == -1: only a bow skill shoots (Triple Shot's fan).
    if (skill === SkillEffect.TRIPLE_SHOT) shootFan(options);
    return;
  }
  const to = target.position;
  if (options.hit ?? true) {
    for (let i = 0; i < BLOOD_DROPS; i++) {
      const position = new THREE.Vector3(to.x + randInt(64) - 32, to.y + randInt(64) - 32, to.z + randInt(64) + 90);
      system.createParticle('blood2', position, caster.angle, WHITE);
    }
  }
  if (SWORD_SKILLS.includes(skill) && !(skill === SkillEffect.SLASH && options.slashTwoHandAction)) createSpark(system, target);
  const angle = caster.angle.clone();
  angle.z = createAngle2D(caster.position, to);
  const light = caster.light ?? WHITE;
  switch (skill) {
    case SkillEffect.TRIPLE_SHOT:
      options.shootArrow?.(0, target); // the bow attack's own arrow
      shootFan(options);
      break;
    case SkillEffect.HEALING:
      system.createEffect('magicGround2', to, target.angle, target.light ?? WHITE, 1, target);
      break;
    case SkillEffect.DEFENSE:
      if (options.casterCloaked) break;
      system.createEffect('magicGround2', to, target.angle, target.light ?? WHITE, 2, target);
      if (!options.targetHadDefense || !system.searchJoint('spearSkill', target, 4)) {
        for (let j = 0; j < 5; j++) system.createJoint('spearSkill', to, to, target.angle, 4, target, 20);
      }
      break;
    case SkillEffect.ATTACK:
      if (options.casterCloaked) break;
      system.createEffect('magicGround2', to, target.angle, target.light ?? WHITE, 3, target);
      break;
    case SkillEffect.FIRE_BALL:
      system.createEffect('fireModel', caster.position, angle, light, 1, target);
      break;
    case SkillEffect.POWER_WAVE:
      system.createEffect('magic2', caster.position, angle, light);
      break;
    case SkillEffect.ENERGY_BALL:
      system.createEffect('energy', caster.position, angle, light, 0, target);
      break;
    case SkillEffect.METEORITE:
      system.createEffect('fireModel', to, target.angle, light);
      break;
    case SkillEffect.ICE:
      system.createEffect('ice', to, caster.angle, WHITE);
      for (let i = 0; i < 5; i++) system.createEffect('iceSmall', to, caster.angle, light);
      break;
    case SkillEffect.POISON: {
      system.createEffect('poison', to, caster.angle, light);
      const smoke = new THREE.Vector3(0.4, 0.6, 1);
      for (let i = 0; i < 10; i++) system.createParticle('smoke', to, caster.angle, smoke, 1);
      break;
    }
    case SkillEffect.SOUL_BARRIER:
      if (options.casterCloaked) break;
      system.deleteJoint('spearSkill', target, 0);
      for (let j = 0; j < 5; j++) system.createJoint('spearSkill', to, to, target.angle, 0, target, 20);
      break;
    case SkillEffect.FLAME:
      if (options.cell) break; // the area cast burns at its tile (releaseAtCaster)
      system.createEffect('flame', new THREE.Vector3(to.x, to.y, system.heightAt(to.x, to.y)), caster.angle, light, 5, caster);
      break;
  }
}

/** The tile's centre on the ground: ((SkillX + 0.5) * 100, (SkillY + 0.5) * 100, its height). */
function cellPosition(system: EffectSystem, cell: { x: number; y: number }): THREE.Vector3 {
  const x = (cell.x + 0.5) * TERRAIN_SCALE, y = (cell.y + 0.5) * TERRAIN_SCALE;
  return new THREE.Vector3(x, y, system.heightAt(x, y));
}

// The release switch's cases that don't need a target: the area skills at their tile and the
// skills around the caster (MoveCharacter, before "if (c->TargetCharacter == -1)").
function releaseAtCaster(system: EffectSystem, skill: number, caster: EffectOwner, options: SkillEffectOptions): void {
  const light = caster.light ?? WHITE;
  const at = caster.position;
  switch (skill) {
    case SkillEffect.HELLFIRE:
      system.createEffect('circle', at, caster.angle, light, 0, caster);
      system.createEffect('circleLight', at, caster.angle, light);
      break;
    case SkillEffect.NOVA:
      system.createEffect('circle', at, caster.angle, light, 1, caster);
      break;
    case SkillEffect.TWISTER:
      system.createEffect('storm', at, caster.angle, WHITE, 0, caster);
      break;
    case SkillEffect.INFERNO:
      system.createInferno(at);
      system.createEffect('skillInferno', at, caster.angle, light, 0, caster);
      break;
    case SkillEffect.EVIL_SPIRIT: {
      const position = at.clone().setZ(at.z + 100);
      for (let i = 0; i < 4; i++) {
        const angle = new THREE.Vector3(0, 0, i * 90);
        system.createJoint('jointSpirit', position, at, angle, 0, caster, 80);
        system.createJoint('jointSpirit', position, at, angle, 0, caster, 20);
      }
      break;
    }
    case SkillEffect.AQUA_BEAM: {
      // CalcAddPosition(o, -20, -90, 100): in front of the caster's chest.
      const position = new THREE.Vector3(-20, -90, 100).applyEuler(new THREE.Euler(
        caster.angle.x * Math.PI / 180, caster.angle.y * Math.PI / 180, caster.angle.z * Math.PI / 180, 'ZYX')).add(at);
      system.createEffect('bossLaser', position, caster.angle, WHITE, 0, caster);
      break;
    }
    case SkillEffect.FLAME:
      if (options.cell) system.createEffect('flame', cellPosition(system, options.cell), caster.angle, light, 0, caster);
      break;
    case SkillEffect.DECAY:
      if (!options.cell) break;
      for (let i = 0; i < 2; i++) system.createEffect('fireModel', cellPosition(system, options.cell), caster.angle, new THREE.Vector3(0.8, 0.5, 0.1), 6);
      break;
    case SkillEffect.ICE_STORM: {
      if (!options.cell) break;
      const target = cellPosition(system, options.cell);
      for (let i = 0; i < 10; i++) {
        const position = target.clone().setZ(target.z + randInt(50) * i);
        system.createEffect('blizzardModel', position, caster.angle, WHITE, 0, null, 0, { pkKey: i + 1 });
      }
      break;
    }
  }
}

// Nova's charge (MoveCharacter, PLAYER_SKILL_HELL_BEGIN / _START): while the caster gathers, its
// bones shine blue and trails gather at its feet (CreateForce) every tick.
function novaCharge(system: EffectSystem, caster: EffectOwner): void {
  const light = new THREE.Vector3(0.3, 0.3, 1);
  let started = false;
  let ticks = 0;
  system.schedule(() => {
    ticks++;
    const action = caster.currentAction?.();
    const charging = action !== undefined && NOVA_CHARGE_ACTIONS.includes(action);
    if (charging) started = true;
    // Until the caster's action starts (a few ticks) and while it lasts; at most 30 seconds.
    if ((started && !charging) || !(caster.isLive?.() ?? true) || ticks > 750 || (!started && ticks > 25)) return false;
    if (!charging) return true;
    const skillCount = 0; // m_bySkillCount, the charge the server reports
    for (let i = 0; i < NOVA_BONES; i += 2) {
      const position = caster.bonePosition?.(i);
      if (!position) continue;
      for (let j = 0; j < skillCount + 1; j++) system.createParticle('light', position, caster.angle, light, 6, 1.3 + skillCount * 0.08);
    }
    system.createForce(caster, caster.position.clone());
    return true;
  });
}

// CreateArrows(c, o, NULL, ..., 1): the fan flies straight, aimed at no character.
function shootFan(options: SkillEffectOptions): void {
  if (!options.shootArrow) return;
  for (const offset of tripleShotAngles(options.bowNumbers?.rightHand, options.bowNumbers?.leftHand)) options.shootArrow(offset, null);
}

/** CreateSpark: a flash and twenty bouncing sparks at the target's feet. */
export function createSpark(system: EffectSystem, target: EffectOwner): void {
  system.createParticle('spark3', target.position, target.angle, WHITE);
  for (let i = 0; i < SPARKS; i++) system.createParticle('spark', target.position, target.angle, WHITE);
}

/** The buff numbers (eBuffState) whose looks are ported. */
export const BuffEffect = {
  DEFENSE: 2,
  WIZ_DEFENSE: 4,
} as const;

/**
 * A buff's look when it comes on or goes off a player (InsertBuffPhysicalEffect and
 * ClearBuffLogicalEffect in Network/Server/WSclient.cpp).
 */
export function playBuffEffect(system: EffectSystem, buff: number, owner: EffectOwner, on: boolean): void {
  switch (buff) {
    case BuffEffect.WIZ_DEFENSE: // Soul Barrier: five lights circle the player
      system.deleteJoint('spearSkill', owner, 0);
      if (on) for (let j = 0; j < 5; j++) system.createJoint('spearSkill', owner.position, owner.position, owner.angle, 0, owner, 50);
      break;
    case BuffEffect.DEFENSE:
      if (!on) system.deleteJoint('spearSkill', owner, 4);
      break;
  }
}
