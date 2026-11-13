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
//   CreateArrows (the Triple Shot fan)                               Render/Effects/ZzzEffectMagicSkill.cpp

import * as THREE from 'three';
import { createAngle2D, randInt } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import type { EffectOwner } from './types.js';

/** The skill numbers (AT_SKILL_* in Core/Globals/_enum.h) whose effects are ported. */
export const SkillEffect = {
  LIGHTNING: 3,
  FIRE_BALL: 4,
  POWER_WAVE: 11,
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
  POWER_SLASH: 56,
} as const;

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
}

/** Starts a skill's effects at its cast; returns false for a skill whose effects aren't ported. */
export function playSkillEffect(system: EffectSystem, skill: number, caster: EffectOwner, target: EffectOwner | null,
  options: SkillEffectOptions = {}): boolean {
  if (!isPortedSkill(skill)) return false;
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
  }
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
