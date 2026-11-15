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
import { summonerCurse } from './summonerEffects.js';
import { RageSkill, rageCreateEffect } from './rageEffects.js';
import { criticalDamage, fireBurst } from './lordEffects.js';
import { multiShotArrows } from './elfEffects.js';
import { LordAction, chaoticDiseier, earthshake, electricSpike } from './darkLordEffects.js';
import { spiralSlashBlades } from './knightEffects.js';
import { playBuffLook } from './buffLooks.js';

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
  SUMMON: 30,
  SUMMON_LAST: 36,
  HEALING: 26,
  DEFENSE: 27,
  ATTACK: 28,
  DECAY: 38,
  ICE_STORM: 39,
  NOVA: 40,
  TWISTING_SLASH: 41,
  RAGEFUL_BLOW: 42,
  DEATH_STAB: 43,
  IMPALE: 47,
  SWELL_LIFE: 48,
  ICE_ARROW: 51,
  PENETRATION: 52,
  FIRE_SLASH: 55,
  POWER_SLASH: 56,
  NOVA_BEGIN: 58,
  COMBO: 59,
  FORCE: 60,
  FORCE_WAVE: 66,
  INFINITY_ARROW: 77,
  MULTI_SHOT: 235,
  DRAIN_LIFE: 214,
  CHAIN_LIGHTNING: 215,
  BERSERKER: 218,
  SLEEP: 219,
  WEAKNESS: 221,
  INNOVATION: 222,
  LIGHTNING_SHOCK: 230,
  FIRE_BURST: 61,
  CRITICAL_DAMAGE: 64,
  FLAME_STRIKE: 236,
  GIGANTIC_STORM: 237,
  SPIRAL_SLASH: 57,
  EARTHSHAKE: 62,
  ELECTRIC_SPIKE: 65,
  CHAOTIC_DISEIER: 238,
  CRESCENT_MOON_SLASH: 44,
} as const;

/** PLAYER_ATTACK_RUSH: Crescent Moon Slash's and Charge's rush. */
const RUSH_ACTION = 137;
/** PLAYER_ATTACK_ONE_FLASH: Spiral Slash's swing. */
const ONE_FLASH_ACTION = 136;
/** Without the skill's action playing, the timings count this many keys a tick. */
const FALLBACK_KEYS_PER_TICK = 0.3;
/** A skill that waits for its action's key releases after this many ticks at most. */
const RELEASE_WAIT_TICKS = 40;

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
/** The fighter's party buffs. */
const RAGE_BUFFS: readonly number[] = [RageSkill.ATT_UP, RageSkill.HP_UP, RageSkill.DEF_UP];
/** The skills whose AttackStage case sets AttackTime to 15 on the first tick: they release at once. */
const IMMEDIATE_SKILLS: readonly number[] = [SkillEffect.LIGHTNING_SHOCK, SkillEffect.FLAME_STRIKE];
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
  /** The caster is the hero (Chaotic Diseier's burst at the selected target is the hero's own). */
  casterIsHero?: boolean;
}

/** The key the caster's action is at, when it plays one of `actions`; else a count from the ticks so far. */
function actionKey(caster: EffectOwner, actions: readonly number[], ticks: number): number {
  const action = caster.currentAction?.();
  if (action !== undefined && actions.includes(action)) return caster.animationFrame?.() ?? ticks * FALLBACK_KEYS_PER_TICK;
  return ticks * (caster.playSpeed?.(actions[0]) ?? FALLBACK_KEYS_PER_TICK);
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
  if (skill === SkillEffect.INFINITY_ARROW) { // ReceiveMagic: the quiver's light and rings at once
    system.createEffect('infinityArrow', caster.position, caster.angle, caster.light ?? WHITE, 0, caster);
    return true;
  }
  if (skill === SkillEffect.MULTI_SHOT) {
    multiShotRings(system, caster);
    // The fan over the head (CalcAddPosition 0, -120, 145) and its arrows.
    const fan = new THREE.Vector3(0, -120, 145).applyEuler(new THREE.Euler(0, 0, caster.angle.z * Math.PI / 180)).add(caster.position);
    system.createEffect('bladeSkill', fan, caster.angle, new THREE.Vector3(0.8, 0.9, 1.6), 1, caster);
    const shoot = options.shootArrow;
    if (shoot) multiShotArrows(system, (offset) => shoot(offset, null));
    return true;
  }
  // The fighter's effects come with the cast (CMonkSystem::RageCreateEffect); its attack then
  // releases as any other (the hit's blood).
  const rage = rageCreateEffect(system, skill, caster, target);
  if (rage && RAGE_BUFFS.includes(skill)) return true; // no attack follows a party buff
  if (skill === SkillEffect.CHAIN_LIGHTNING) return true; // its links come with ChainLightningHitInfo (playChainLightning)
  if (skill === SkillEffect.CHAOTIC_DISEIER) chaoticDiseier(system, caster, target, options.casterIsHero ?? false); // ReceiveMagic
  if (skill === SkillEffect.EARTHSHAKE) earthshake(system, caster); // the dark horse's stomp starts with the lord's action
  if (skill === SkillEffect.COMBO) { // ReceiveMagic: the combo's burst at once
    system.createEffect('combo', caster.position, caster.angle, caster.light ?? WHITE);
    return true;
  }
  let attackTime = 1;
  let ticks = 0;
  system.schedule(() => {
    ticks++;
    if (skill === SkillEffect.SPIRAL_SLASH || skill === SkillEffect.ELECTRIC_SPIKE) {
      attackTime = keyedStage(system, skill, caster, attackTime, ticks);
      attackTime += 1;
      if (attackTime < ATTACK_LIMIT_TIME) return true;
      release(system, skill, caster, target, options);
      return false;
    }
    attackTime = attackStage(system, skill, caster, attackTime, target);
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
  return (Object.values(SkillEffect) as number[]).includes(skill) || (Object.values(RageSkill) as number[]).includes(skill) || isSummon(skill);
}

function isSummon(skill: number): boolean {
  return skill >= SkillEffect.SUMMON && skill <= SkillEffect.SUMMON_LAST;
}

/**
 * CreateArrows with Skill 1: Triple Shot's fan, as offsets from the facing. Some bows shoot four
 * (Divine Crossbow of Archangel, Great Reign Crossbow, Albatross, Stinger and Air Lyn Bow).
 */
export function tripleShotAngles(rightHand?: number, leftHand?: number): number[] {
  const fourArrows = rightHand === 18 || rightHand === 19 || leftHand === 22 || leftHand === 23 || leftHand === 24;
  return fourArrows ? [5, 15, -5, -15] : [0, 15, -15];
}

// AttackStage: Power Slash's five blades leave in a fan on its first tick; Death Stab gathers its
// wind and cones; Impale makes its cones and thrusts. Returns the AttackTime to go on with.
function attackStage(system: EffectSystem, skill: number, caster: EffectOwner, attackTime: number, target: EffectOwner | null): number {
  if (skill === SkillEffect.DEATH_STAB) return deathStabStage(system, caster, attackTime, target);
  if (skill === SkillEffect.IMPALE) {
    impaleStage(system, caster, attackTime);
    return attackTime;
  }
  if (skill === SkillEffect.SWELL_LIFE && attackTime > 9) return ATTACK_LIMIT_TIME; // PLAYER_SKILL_VITALITY
  if (skill === SkillEffect.CRESCENT_MOON_SLASH || skill === RageSkill.CHARGE) {
    // AT_SKILL_RUSH / AT_SKILL_OCCUPY: sparks and fire burst at the feet every tick of the rush (the Charge's fire,
    // BITMAP_FIRE SubType 18, isn't ported); the release after key 5.
    const position = caster.position.clone();
    position.x += randInt(30) - 15;
    position.y += randInt(30) - 15;
    position.z += 20;
    for (let i = 0; i < 4; i++) {
      const angle = new THREE.Vector3(randInt(60) + 60 + 90, 0, caster.angle.z);
      system.createJoint('jointSpark', position, position, angle);
      if (skill === SkillEffect.CRESCENT_MOON_SLASH) system.createParticle('fire', position, angle, caster.light ?? WHITE, 2, 1.5);
    }
    return actionKey(caster, [RUSH_ACTION], attackTime) > 5 ? ATTACK_LIMIT_TIME : attackTime;
  }
  if (IMMEDIATE_SKILLS.includes(skill)) return ATTACK_LIMIT_TIME; // AttackStage sets AttackTime to 15 at once
  if (skill === SkillEffect.FIRE_SLASH) {
    // The charge at the hand on the first two ticks; the sweep when the swing reaches its third
    // key (PLAYER_ATTACK_SKILL_WHEEL), then the release.
    if (attackTime === 1 || attackTime === 2) system.createEffect('gathering', caster.position, caster.angle, caster.light ?? WHITE, 1, caster);
    if (attackTime >= 6) {
      system.createEffect('swordForce', caster.position, caster.angle, caster.light ?? WHITE, 0, caster);
      return ATTACK_LIMIT_TIME;
    }
    return attackTime;
  }
  if (skill === SkillEffect.PENETRATION) { // the charge at 3, the shot at 5 (g_iLimitAttackTime = 5)
    if (attackTime === 3) system.createEffect('gathering', caster.position, caster.angle, caster.light ?? WHITE, 0, caster);
    return attackTime >= 4 ? ATTACK_LIMIT_TIME : attackTime;
  }
  if (skill !== SkillEffect.POWER_SLASH || attackTime !== 1) return attackTime;
  const angle = caster.angle.clone();
  angle.z -= 40;
  for (let i = 0; i < 5; i++) {
    system.createEffect('magic2', caster.position, angle, caster.light ?? WHITE, 2, caster);
    angle.z += 20;
  }
  return attackTime;
}

// AttackStage's cases timed by the caster's action keys: Spiral Slash's blades (two as the swing passes key 2.3-2.6,
// three more and the release after key 5) and Electric Spike's release at key 5.5 of PLAYER_SKILL_FLASH.
const spiralFirstBlades = new WeakSet<EffectOwner>();
function keyedStage(system: EffectSystem, skill: number, caster: EffectOwner, attackTime: number, ticks: number): number {
  if (skill === SkillEffect.ELECTRIC_SPIKE) {
    const key = actionKey(caster, [LordAction.SKILL_FLASH, LordAction.RIDE_ATTACK_FLASH, LordAction.FENRIR_ATTACK_DARKLORD_FLASH], ticks);
    return key >= 5.5 || ticks >= RELEASE_WAIT_TICKS ? ATTACK_LIMIT_TIME : 10;
  }
  const key = actionKey(caster, [ONE_FLASH_ACTION], ticks);
  if (key > 5 || ticks >= RELEASE_WAIT_TICKS) {
    spiralSlashBlades(system, caster, [0, 1, 4]);
    spiralFirstBlades.delete(caster);
    return ATTACK_LIMIT_TIME;
  }
  if (key > 2.3 && !spiralFirstBlades.has(caster)) { // once, as the swing passes key 2.3-2.6
    spiralFirstBlades.add(caster);
    spiralSlashBlades(system, caster, [2, 3]);
  }
  return attackTime;
}

/** The point `distance` in front of the caster (sin / -cos of its facing). */
function ahead(caster: EffectOwner, from: THREE.Vector3, distance: number): THREE.Vector3 {
  const radians = caster.angle.z * Math.PI / 180;
  return new THREE.Vector3(from.x + distance * Math.sin(radians), from.y - distance * Math.cos(radians), from.z);
}

/** GetNearRandomPos: a point up to `range` away on each axis. */
function nearRandom(at: THREE.Vector3, range: number): THREE.Vector3 {
  return new THREE.Vector3(at.x + randInt(range * 2 + 1) - range, at.y + randInt(range * 2 + 1) - range, at.z + randInt(range * 2 + 1) - range);
}

// AttackStage AT_SKILL_DEATHSTAB: from AttackTime 2 to 8 red gusts fly in from far ahead to the
// sword; from 6 to 12 two spiralling blue flares leave the sword's point every tick; at 12 the release.
function deathStabStage(system: EffectSystem, caster: EffectOwner, attackTime: number, _target: EffectOwner | null): number {
  const sword = caster.weaponPosition?.(0) ?? caster.position.clone().setZ(caster.position.z + 120);
  if (attackTime >= 2 && attackTime <= 8) {
    for (let j = 0; j < 3; j++) {
      // GetNearRandomPos, then 1400 along (-sin, +cos) of the facing.
      const from = ahead(caster, nearRandom(caster.position.clone().setZ(caster.position.z + 120), 300), -1400);
      system.createJoint('spearSkill', from, from, caster.angle, 2, caster, 40);
    }
  }
  if (attackTime >= 6 && attackTime <= 12) {
    const position = ahead(caster, sword, 100 + (attackTime - 8) * 10);
    system.createEffect('lightSpear', position, caster.angle, WHITE, 1, caster);
    system.createEffect('lightSpear', position, caster.angle, WHITE, 1, caster);
  }
  return attackTime >= 12 ? ATTACK_LIMIT_TIME : attackTime;
}

// AttackStage AT_SKILL_IMPALE: at 8 two spiralling white flares in front of the chest; at 13 and
// 14 three spears thrust forward around the point.
function impaleStage(system: EffectSystem, caster: EffectOwner, attackTime: number): void {
  if (attackTime === 8) {
    const position = ahead(caster, caster.position, 50);
    position.z += 110;
    system.createEffect('lightSpear', position, caster.angle, WHITE, 0, caster);
    system.createEffect('lightSpear', position, caster.angle, WHITE, 0, caster);
  }
  if (attackTime >= 13 && attackTime <= 14) {
    for (let i = 0; i < 3; i++) {
      const position = ahead(caster, caster.position, 145);
      position.x += randInt(60) - 30;
      position.y += randInt(60) - 30;
      position.z += 110 + randInt(60) - 30;
      system.createEffect('spearSkillModel', position, caster.angle, new THREE.Vector3(0.3, 0.3, 0.3), 0, caster);
    }
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
    case SkillEffect.FORCE:
    case SkillEffect.FORCE_WAVE: // two rings and the spear, from the lord along its facing
      system.createEffect('waves', caster.position, caster.angle, light, 1);
      system.createEffect('waves', caster.position, caster.angle, light, 1);
      system.createEffect('piercing2', caster.position, caster.angle, light);
      break;
    case SkillEffect.PENETRATION:
    case SkillEffect.ICE_ARROW:
      options.shootArrow?.(0, target); // CreateArrows: one arrow
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
    case SkillEffect.FIRE_BURST:
      fireBurst(system, caster, target);
      break;
    case SkillEffect.DRAIN_LIFE: // the caster's Owner is the target (pSourceObj->Owner)
      system.createEffect('aliceDrainLife', caster.position, caster.angle, light, 0, caster, 0, { target });
      break;
    case SkillEffect.SLEEP:
      system.createEffect('magicGround2', caster.position, caster.angle, new THREE.Vector3(0.7, 0.3, 0.8), 11, caster);
      system.createEffect('aliceBuffSkillEffect', to, target.angle, new THREE.Vector3(0.8, 0.3, 0.9), 0, target);
      system.createEffect('aliceBuffSkillEffect2', to, target.angle, new THREE.Vector3(0.8, 0.3, 0.9), 0, target);
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
  if (isSummon(skill)) system.createEffect('magicGround2', at, caster.angle, light, 3, caster);
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
    case SkillEffect.BERSERKER: {
      const red = new THREE.Vector3(1, 0.1, 0.2);
      system.createEffect('magicGround2', at, caster.angle, red, 11, caster);
      system.createEffect('aliceBuffSkillEffect', at, caster.angle, red, 0, caster);
      system.createEffect('aliceBuffSkillEffect2', at, caster.angle, red, 0, caster);
      break;
    }
    case SkillEffect.WEAKNESS:
    case SkillEffect.INNOVATION:
      summonerCurse(system, at, caster.angle, skill === SkillEffect.WEAKNESS);
      break;
    case SkillEffect.LIGHTNING_SHOCK:
      system.createEffect('lightningShock', at, caster.angle, WHITE, 0, caster);
      break;
    case SkillEffect.FLAME_STRIKE:
      system.createEffect('flameStrike', at, caster.angle, light, 0, caster);
      break;
    case SkillEffect.CRITICAL_DAMAGE:
      criticalDamage(system, caster);
      break;
    case SkillEffect.ELECTRIC_SPIKE:
      electricSpike(system, caster);
      break;
    case SkillEffect.CRESCENT_MOON_SLASH: // AT_SKILL_RUSH: the sword's force sweeps ahead
      system.createEffect('swordForce', at, caster.angle, light, 0, caster);
      break;
    case SkillEffect.GIGANTIC_STORM: // five storms 200 around the gladiator
      for (let i = 0; i < 5; i++) {
        const p = new THREE.Vector3(0, 200, 0).applyEuler(new THREE.Euler(0, 0, (caster.angle.z + i * 72) * Math.PI / 180)).add(at);
        system.createEffect('jointThunderEffect', p, caster.angle, light);
      }
      break;
    case SkillEffect.TWISTING_SLASH:
      system.createEffect('skillWheel1', at, caster.angle, light, 0, caster);
      break;
    case SkillEffect.RAGEFUL_BLOW:
      system.createEffect('skillFuryStrike', at, caster.angle, light, 0, caster);
      break;
    case SkillEffect.SWELL_LIFE:
      for (let i = 0; i < 36; i++) {
        const angle = new THREE.Vector3(-10, 0, i * 10);
        const position = at.clone().setZ(at.z + 100);
        system.createJoint('jointSpirit', position, position, angle, 2, caster, 60);
        if (i % 20 === 0) system.createEffect('magicGround2', at, angle, light, 4, caster);
      }
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
  LIFE: 8,
} as const;

/**
 * A buff's look when it comes on or goes off a player (InsertBuffPhysicalEffect and
 * ClearBuffLogicalEffect in Network/Server/WSclient.cpp).
 */
export function playBuffEffect(system: EffectSystem, buff: number, owner: EffectOwner, on: boolean, player = true): void {
  if (playBuffLook(system, buff, owner, on)) return;
  switch (buff) {
    case BuffEffect.WIZ_DEFENSE: // Soul Barrier: five lights circle the player
      if (!player) break;
      system.deleteJoint('spearSkill', owner, 0);
      if (on) for (let j = 0; j < 5; j++) system.createJoint('spearSkill', owner.position, owner.position, owner.angle, 0, owner, 50);
      break;
    case BuffEffect.DEFENSE:
      if (!on) system.deleteJoint('spearSkill', owner, 4);
      break;
    case BuffEffect.LIFE: // Swell Life: lights rise from the upper body while it lasts
      if (!on) system.deleteEffect('buffLight', owner, 1);
      else system.createEffect('buffLight', owner.position, owner.angle, owner.light ?? WHITE, 1, owner);
      break;
  }
}

// AttackElf AT_SKILL_MULTI_SHOT: three rings grow out in front of the elf as it shoots.
function multiShotRings(system: EffectSystem, caster: EffectOwner): void {
  const light = new THREE.Vector3(0.8, 0.9, 1.6);
  const front = new THREE.Vector3(0, 20, 0).applyEuler(new THREE.Euler(0, 0, caster.angle.z * Math.PI / 180)).add(caster.position);
  system.createEffect('multiShot3', front, caster.angle, light);
  system.createEffect('multiShot3', front, caster.angle, light);
  for (let i = 0; i < 3; i++) system.createEffect('multiShot1', caster.position, caster.angle, light);
  system.createEffect('multiShot2', front, caster.angle, light);
  system.createEffect('multiShot2', front, caster.angle, light);
}

/**
 * ReceiveChainMagic: Chain Lightning's links, one MODEL_CHAIN_LIGHTNING per target: the first
 * from the caster's hands, the next ones from the target before (SubType = the link's number).
 */
export function playChainLightning(system: EffectSystem, caster: EffectOwner, targets: readonly EffectOwner[]): void {
  let from: EffectOwner = caster;
  const angle = new THREE.Vector3(-60, 0, caster.angle.z);
  targets.forEach((target, i) => {
    if (from !== target && (target.isLive?.() ?? true)) system.createEffect('chainLightning', caster.position, angle, caster.light ?? WHITE, i, from, 0, { target });
    from = target;
  });
}
