// The Summoner's summons (registered in extraEffects.ts's tables): the casting circles under the Summoner, and Requiem's
// creature (Neil), which rises before the Summoner, slashes the air with its red-lit blade and drops knives on the
// target that sink into glowing marks on the ground.
//
// Followed C++ (client/src/source):
//   CSummonSystem::CastSummonSkill, CreateCastingEffect, CreateSummonObject (AT_SKILL_SUMMON_REQUIEM; the book's
//   level picks the size)                                                       GameLogic/Skills/SummonSystem.cpp
//   CreateEffect: BITMAP_MAGIC (9, 10); the catalogue's rows for MODEL_SUMMONER_CASTING_EFFECT1/11/111/4,
//   MODEL_SUMMONER_SUMMON_NEIL, _NEIL_NIFE1..3, _NEIL_GROUND1..3; DescribeEffectShadow (BITMAP_MAGIC 9, 10)
//                                                                                Render/Effects/ZzzEffect.cpp
//   Move_MODEL_SUMMONER_CASTING_EFFECT1, Move_MODEL_SUMMONER_SUMMON_NEIL, Move_BITMAP_MAGIC (9, 10)
//                                                                                Render/Effects/Behaviors/MoveHandlers.cpp
//   MoveSummonerNeilNife, MoveSummonerNeilGround                                 Render/Effects/Behaviors/EffectBehaviors.cpp
//   RenderObject (the casting circles RENDER_BRIGHT; NEIL, its knives and its ground marks by mesh);
//   UpdateRenderObjectEffects (MODEL_SUMMONER_SUMMON_NEIL: its blade's lights and blur)  Engine/Object/ZzzObject.cpp
//   CreateObjectBlur                                                             Render/Effects/ZzzEffectBlurSpark.cpp

import * as THREE from 'three';
import { angleEuler, randInt, rotateByAngle } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { computeBoneMatrices } from '../../bmd.js';
import { EXTRA_EFFECTS } from './extraEffects.js';
import type { ExtraEffect } from './extraEffects.js';
import type { Effect, EffectOwner } from './types.js';

const WHITE = new THREE.Vector3(1, 1, 1);
const RED = new THREE.Vector3(1, 0, 0);

/** AT_SKILL_SUMMON_EXPLOSION, _REQUIEM, _POLLUTION. */
export const SummonSkill = { EXPLOSION: 223, REQUIEM: 224, POLLUTION: 225 } as const;

// ---------------------------------------------------------------------------
// The casting circles (CreateCastingEffect).
// ---------------------------------------------------------------------------

/** MODEL_SUMMONER_CASTING_EFFECT1/11/111/4: rings turning in turns (4 swells instead), fading in and out. */
function castingRing(file: string, turn: number, swell = 0): ExtraEffect {
  return {
    model: ['Effect', file],
    prewarmBlendMesh: 0,
    create: () => true,
    move: (o) => {
      if (o.lifeTime < 20) o.blendMeshLight -= 0.03;
      else if (o.blendMeshLight < 0.5) o.blendMeshLight += 0.05;
      o.angle.z += turn;
      o.scale += swell;
      return true;
    },
  };
}
EXTRA_EFFECTS.summonerCastingEffect1 = castingRing('Suhwanzin1.bmd', -3);
EXTRA_EFFECTS.summonerCastingEffect11 = castingRing('Suhwanzin11.bmd', 3);
EXTRA_EFFECTS.summonerCastingEffect111 = castingRing('Suhwanzin111.bmd', -3);
EXTRA_EFFECTS.summonerCastingEffect4 = castingRing('Suhwanzin4.bmd', 0, 0.6);

/** The colours of CreateCastingEffect's layers for each summon: the circle, the rings, the second rings, the swell. */
const CASTING_COLOURS: Record<number, [number[], number[], number[], number[]]> = {
  [SummonSkill.EXPLOSION]: [[1, 0.6, 0.4], [1, 0.5, 0], [1, 0.5, 0], [1, 0.5, 0.8]],
  [SummonSkill.REQUIEM]: [[0.7, 0.7, 1], [0, 0.7, 1], [0, 0, 1], [0.8, 0.5, 1]],
  [SummonSkill.POLLUTION]: [[0.6, 0.6, 0.9], [0.6, 0.3, 0.9], [0.8, 0.1, 0.6], [0.9, 0.1, 1]],
};

/** CreateCastingEffect: the circles and rings under the Summoner. */
export function summonCasting(system: EffectSystem, caster: EffectOwner, skill: number): void {
  const colours = CASTING_COLOURS[skill] ?? CASTING_COLOURS[SummonSkill.REQUIEM];
  const v = (c: number[]): THREE.Vector3 => new THREE.Vector3(c[0], c[1], c[2]);
  const at = caster.position, angle = caster.angle;
  system.createEffect('magic', at, angle, WHITE, 10);
  system.createEffect('magic', at, angle, v(colours[0]), 9);
  for (const type of ['summonerCastingEffect1', 'summonerCastingEffect11', 'summonerCastingEffect111'] as const) system.createEffect(type, at, angle, v(colours[1]));
  for (const type of ['summonerCastingEffect2', 'summonerCastingEffect22', 'summonerCastingEffect222'] as const) system.createEffect(type, at, angle, v(colours[2]));
  system.createEffect('summonerCastingEffect4', at, angle, v(colours[3]));
}

// ---------------------------------------------------------------------------
// Requiem: Neil.
// ---------------------------------------------------------------------------

/** RENDER_TEXTURE for a mesh, or RENDER_BRIGHT at `brightness` (the blend switches; the program stays the same). */
function meshLook(material: THREE.MeshBasicMaterial, bright: boolean, colour: THREE.Vector3, alpha: number): void {
  if (bright) {
    material.blending = THREE.AdditiveBlending;
    material.depthWrite = false;
    material.color.setRGB(colour.x, colour.y, colour.z).multiplyScalar(alpha);
    material.opacity = 1;
  } else {
    material.blending = THREE.NormalBlending;
    material.depthWrite = true;
    material.color.setRGB(colour.x, colour.y, colour.z);
    material.opacity = Math.max(0, Math.min(1, alpha));
  }
}

const neilBones: THREE.Matrix4[] = [];
const neilEuler = new THREE.Euler();
const blurStart = new THREE.Vector3();
const blurEnd = new THREE.Vector3();

/** EffectWorldPose: the effect's model posed at `frame`, its bones placed in the world (scale, angle, position). */
function effectPose(o: Effect, frame: number): THREE.Matrix4[] | null {
  const skeleton = o.model?.skeleton;
  if (!skeleton) return null;
  while (neilBones.length < skeleton.bones.length) neilBones.push(new THREE.Matrix4());
  computeBoneMatrices(skeleton, 0, Math.max(0, frame), neilBones);
  return neilBones;
}

function placeBone(o: Effect, matrix: THREE.Matrix4, out: THREE.Vector3): THREE.Vector3 {
  return out.set(0, 0, 0).applyMatrix4(matrix).multiplyScalar(o.scale).applyEuler(angleEuler(o.angle, neilEuler)).add(o.position);
}

// MODEL_SUMMONER_SUMMON_NEIL (Skill/summon_neil.bmd; the catalogue: 80 ticks, 0.35 keys a tick, rising from 10 up,
// heading for the call's light, the target): fades in to 0.7; at key 8 the knives fall at the target (one more per
// level of the book), at key 10 their marks open there and one before Neil.
EXTRA_EFFECTS.summonerSummonNeil = {
  model: ['Skill', 'summon_neil.bmd'],
  prewarmBlendMesh: 1,
  create: (o) => {
    o.blendMesh = 1; // its blade (mesh 1) is drawn bright
    return true;
  },
  move: (o, system) => {
    if (o.lifeTime < 20) o.alpha -= 0.05;
    else if (o.alpha < 0.7) o.alpha += 0.04;
    if (o.animationFrame > 8 && o.skill === 0) {
      o.skill = 1;
      system.createEffect('summonerSummonNeilKnife1', o.headTargetAngle, o.angle, o.light, o.subType);
      if (o.subType >= 1) system.createEffect('summonerSummonNeilKnife2', o.headTargetAngle, o.angle, o.light, o.subType);
      if (o.subType >= 2) system.createEffect('summonerSummonNeilKnife3', o.headTargetAngle, o.angle, o.light, o.subType);
    }
    if (o.animationFrame > 10 && o.skill === 1) {
      o.skill = 2;
      const before = rotateByAngle(new THREE.Vector3(0, -60, 0), o.angle).add(o.position);
      system.createEffect('summonerSummonNeilGround1', before, o.angle, o.light, o.subType, o.self);
      system.createEffect('summonerSummonNeilGround1', o.headTargetAngle, o.angle, o.light, o.subType);
      if (o.subType >= 1) system.createEffect('summonerSummonNeilGround2', o.headTargetAngle, o.angle, o.light, o.subType);
      if (o.subType >= 2) system.createEffect('summonerSummonNeilGround3', o.headTargetAngle, o.angle, o.light, o.subType);
    }
    return true;
  },
  // UpdateRenderObjectEffects: red lights on the blade's bones (51 to 59) and, while its first ten keys play, the
  // blade's blur from bone 51 to 59 at ten poses over the tick.
  sprites: (o, system) => {
    const pose = effectPose(o, o.animationFrame);
    if (!pose) return;
    for (let i = 51; i <= 59 && i < pose.length; i++) system.createSprite('light', placeBone(o, pose[i], blurStart), 1, RED, o.self);
    if (o.animationFrame < 0 || o.animationFrame > 10 || pose.length <= 59) return;
    let frame = o.animationFrame - o.velocity;
    for (let i = 0; i < 10; i++) {
      const bones = effectPose(o, frame);
      if (bones) system.objectBlurs.add(o, placeBone(o, bones[51], blurStart), placeBone(o, bones[59], blurEnd), RED, 2);
      frame += o.velocity / 10;
    }
  },
  // Meshes 0 and 2 bright at their alpha while it fades in (below 0.7), then textured; its blade (1) red, bright.
  paint: (o, meshIndex, material) => {
    if (meshIndex === 1) meshLook(material, true, RED, o.alpha);
    else meshLook(material, o.alpha < 0.7, o.light, o.alpha);
  },
};

/** MODEL_SUMMONER_SUMMON_NEIL_NIFE1..3 (the catalogue: 50 ticks): a knife; meshes 0 and 2 textured, 1 bright red. */
function neilKnife(file: string): ExtraEffect {
  return {
    model: ['Skill', file],
    prewarmBlendMesh: 1,
    create: (o) => {
      o.blendMesh = 1;
      return true;
    },
    move: (o) => {
      if (o.lifeTime < 20) o.alpha -= 0.05;
      else if (o.alpha < 1) o.alpha += 0.05;
      return true;
    },
    paint: (o, meshIndex, material) => meshLook(material, meshIndex === 1, meshIndex === 1 ? RED : o.light, o.alpha),
  };
}
EXTRA_EFFECTS.summonerSummonNeilKnife1 = neilKnife('nelleff_nife01.bmd');
EXTRA_EFFECTS.summonerSummonNeilKnife2 = neilKnife('nelleff_nife02.bmd');
EXTRA_EFFECTS.summonerSummonNeilKnife3 = neilKnife('nelleff_nife03.bmd');

/** MODEL_SUMMONER_SUMMON_NEIL_GROUND1..3 (the catalogue: 50 ticks): a glowing mark; its second mesh scrolls. */
function neilGround(file: string): ExtraEffect {
  return {
    model: ['Skill', file],
    prewarmBlendMesh: -2,
    create: (o) => {
      o.blendMesh = -2;
      return true;
    },
    move: (o) => {
      if (o.lifeTime < 20) o.alpha -= 0.05;
      else if (o.alpha < 1) o.alpha += 0.3;
      return true;
    },
    paint: (o, meshIndex, material, _mesh, system) => {
      meshLook(material, true, o.light, o.alpha);
      if (meshIndex === 1 && material.map) material.map.offset.x = -system.time * 0.001;
    },
  };
}
EXTRA_EFFECTS.summonerSummonNeilGround1 = neilGround('nell_nifegrund01.bmd');
EXTRA_EFFECTS.summonerSummonNeilGround2 = neilGround('nell_nifegrund02.bmd');
EXTRA_EFFECTS.summonerSummonNeilGround3 = neilGround('nell_nifegrund03.bmd');

/** The summon's size from the book's level (CreateSummonObject: +11 and up 2, +7 and up 1, else 0). */
export function summonLevel(bookLevel: number): number {
  return bookLevel >= 11 ? 2 : bookLevel >= 7 ? 1 : 0;
}

/**
 * CastSummonSkill: the Summoner turns to the tile, the casting circles open, the summon comes (Requiem's Neil 100 before
 * the Summoner, heading for the tile).
 */
export function castSummon(system: EffectSystem, skill: number, caster: EffectOwner, cell: { x: number; y: number }, bookLevel: number): boolean {
  if (skill !== SummonSkill.REQUIEM && skill !== SummonSkill.POLLUTION) return false;
  const target = new THREE.Vector3(cell.x * 100, cell.y * 100, 0);
  target.z = system.heightAt(target.x, target.y);
  summonCasting(system, caster, skill);
  const level = summonLevel(bookLevel);
  if (skill === SummonSkill.REQUIEM) {
    const at = rotateByAngle(new THREE.Vector3(0, -100, 0), caster.angle).add(caster.position);
    system.createEffect('summonerSummonNeil', at, caster.angle, target, level);
  } else {
    pollution(system, target, level);
  }
  return true;
}

// ---------------------------------------------------------------------------
// Pollution: Lagul.
// ---------------------------------------------------------------------------

/** How often (one tick in so many) the poison's smoke, cloud and drops come, by the book's level. */
const POLLUTION_RATE = [5, 4, 3];
/** The bones of Lagul's head the six blue trails hang on (BITMAP_JOINT_ENERGY 48..53). */
const LAGUL_TRAIL_BONES = [24, 28, 32, 44, 48, 52];

/** CreateSummonObject for Pollution: the poison over the tile, and one, two or four of Lagul's heads flying about it. */
function pollution(system: EffectSystem, at: THREE.Vector3, level: number): void {
  system.createEffect('summonerSummonLagul', at, WHITE, WHITE, 0, null, 0, { pkKey: level });
  const heads = [1, 2, 4][level] ?? 1;
  for (let i = 0; i < heads; i++) {
    const angle = new THREE.Vector3(0, 0, i * 90);
    system.createJoint('jointSpirit', at, at, angle, 24, null, 100);
    system.createJoint('jointSpirit', at, at, angle, 24, null, 20);
  }
}

/** Lagul's heads as owners their trails follow: their bones in the world. */
const lagulOwners = new WeakMap<Effect, EffectOwner>();
function lagulOwner(o: Effect): EffectOwner {
  let owner = lagulOwners.get(o);
  if (!owner) {
    owner = {
      position: o.position, angle: o.angle, light: o.light,
      isLive: () => o.live,
      bonePosition: (bone) => {
        const pose = effectPose(o, o.animationFrame);
        return pose?.[bone] ? placeBone(o, pose[bone], new THREE.Vector3()) : null;
      },
    };
    lagulOwners.set(o, owner);
  }
  return owner;
}

// MODEL_SUMMONER_SUMMON_LAGUL: SubType 0 (no model drawn) pours the poison over the tile for 160 ticks; SubType 1 is a
// head (Skill/summon_lagul.bmd, both meshes bright at its alpha) riding a red spirit's trail, its six blue trails on it.
EXTRA_EFFECTS.summonerSummonLagul = {
  model: ['Skill', 'summon_lagul.bmd'],
  prewarmBlendMesh: -2,
  create: (o, system) => {
    o.lifeTime = 160;
    if (o.subType === 0) {
      o.scale = 1;
      o.velocity = 0.5;
      o.hiddenMesh = -2;
      o.headTargetAngle.copy(o.position);
      return true;
    }
    if (o.subType === 1 && o.joint) {
      o.alpha = 0;
      o.blendMesh = -2;
      o.joint.target = lagulOwner(o);
      for (let i = 48; i <= 53; i++) system.createJoint('jointEnergy', o.position, o.position, o.angle, i, lagulOwner(o), 10);
      return true;
    }
    return false;
  },
  move: (o, system) => {
    if (o.subType === 0) {
      if (randInt(POLLUTION_RATE[o.pkKey] ?? 5) !== 0) return true;
      const smoke = o.headTargetAngle.clone().add(new THREE.Vector3(randInt(500) - 250, randInt(500) - 250, 0));
      system.createParticle('smoke', smoke, o.angle, WHITE, 57, 3.5);
      const cloud = o.headTargetAngle.clone().add(new THREE.Vector3(randInt(400) - 200, randInt(400) - 200, 0));
      const purple = new THREE.Vector3(0.6, 0.1, 1);
      system.createEffect('cloud', cloud, o.angle, purple, 0, null, 2);
      system.createParticle('twintailWater', cloud, o.angle, purple, 1);
      return true;
    }
    // Its spirit carries it (the joint sets its place and angle).
    if (o.joint) {
      o.position.copy(o.joint.position);
      o.angle.copy(o.joint.angle);
    }
    if (o.lifeTime < 30) o.alpha = o.lifeTime / 40;
    else if (o.lifeTime > 144) o.alpha = (160 - o.lifeTime) / 20;
    if (o.lifeTime <= 1) for (let i = 48; i <= 53; i++) system.deleteJoint('jointEnergy', lagulOwner(o), i); // EffectDestructor
    return true;
  },
  paint: (o, _meshIndex, material) => meshLook(material, true, o.light, o.alpha),
};

/** BITMAP_JOINT_ENERGY 48..53 on a Lagul head's bones: short blue trails while it lives. */
export function lagulTrailBone(subType: number): number | undefined {
  return LAGUL_TRAIL_BONES[subType - 48];
}

// BITMAP_CLOUD SubType 0 (the catalogue's row; 60 ticks): a cloud on the ground that spreads and fades.
EXTRA_EFFECTS.cloud = {
  create: (o) => {
    if (o.subType !== 0) return false;
    o.lifeTime = 60;
    o.scale = o.callScale;
    o.angle.z = randInt(360);
    return true;
  },
  move: (o) => {
    o.light.multiplyScalar(1 / 1.05);
    o.scale += 0.03;
    return true;
  },
  draw: (o, system) => {
    const luminosity = (randInt(4) + 8) * 0.1;
    system.quads.decal('cloud', 'glow', o.position.x, o.position.y, o.scale, o.light.clone().multiplyScalar(luminosity), -o.angle.z,
      (x, y) => system.heightAt(x, y), o.position.z);
  },
};
