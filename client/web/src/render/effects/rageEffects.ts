// The Rage Fighter's skill effects (registered in extraEffects.ts's tables). The desktop makes
// them in CMonkSystem::RageCreateEffect when the cast arrives (ReceiveMagic for others; the
// hero's own cast at the same moment), not at the attack's release.
//
// Followed C++ (client/src/source):
//   CMonkSystem::RageCreateEffect                                          GameLogic/Social/MonkSystem.cpp
//   CreateEffect: MODEL_WOLF_HEAD_EFFECT, BITMAP_SBUMB, MODEL_DOWN_ATTACK_DUMMY_L/R,
//   MODEL_DRAGON_KICK_DUMMY, BITMAP_LIGHT_RED; CreateBomb3; the catalogue's rows for
//   BITMAP_SWORD_EFFECT_MONO, MODEL_SHOCKWAVE_GROUND01, BITMAP_EVENT_CLOUD, MODEL_WINDFOCE
//   (and their draws in RenderEffects)                                     Render/Effects/ZzzEffect.cpp
//   Move_MODEL_WOLF_HEAD_EFFECT, _BITMAP_SBUMB, _MODEL_DOWN_ATTACK_DUMMY_L/R, _DRAGON_KICK_DUMMY,
//   _BITMAP_SWORD_EFFECT_MONO, _SHOCKWAVE_GROUND01, _BITMAP_EVENT_CLOUD, _MODEL_WINDFOCE,
//   _BITMAP_LIGHT_RED                                                      Render/Effects/Behaviors/MoveHandlers.cpp
//   RenderObject (the dummies and wolf heads RENDER_BRIGHT; MODEL_WINDFOCE and
//   MODEL_SHOCKWAVE_GROUND01 bright at six times their light)             Engine/Object/ZzzObject.cpp
//   CreateJoint / MoveJoint: BITMAP_PIN_LIGHT, BITMAP_FORCEPILLAR          Render/Effects/ZzzEffectJoint.cpp
//   CreateParticle / MoveParticles / DescribeParticle: BITMAP_SBUMB, BITMAP_SWORD_EFFECT_MONO,
//   BITMAP_SUMMON_SAHAMUTT_EXPLOSION                                       Render/Effects/ZzzEffectParticle.cpp

import * as THREE from 'three';
import { angleEuler, createAngle2D, randInt, rotateByAngle } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS, EXTRA_PARTICLES } from './extraEffects.js';
import type { Effect, EffectOwner } from './types.js';
import { computeBoneMatrices, loadModel } from '../../bmd.js';
import type { BMDModel } from '../../formats.js';
import { chainDrive, dragonRoar } from './rageStrikeEffects.js';

const WHITE = new THREE.Vector3(1, 1, 1);

/** The player actions the effects time themselves by (PLAYER_SKILL_THRUST ...). */
export const RageAction = { THRUST: 247, STAMP: 248, GIANTSWING: 249, DRAGONKICK: 252 } as const;

/** The eBuffState numbers of the fighter's party buffs. */
export const RageBuff = { ATT_UP: 129, HP_UP: 130, DEF_UP: 131 } as const;

function live(owner: EffectOwner | null): owner is EffectOwner {
  return owner !== null && (owner.isLive?.() ?? true);
}

function ownerBone(owner: EffectOwner, bone: number): THREE.Vector3 {
  return owner.bonePosition?.(bone) ?? owner.position.clone().setZ(owner.position.z + 120);
}

const euler = new THREE.Euler();
/** TransformByObjectBone on the effect's own model: a bone of its pose, scaled, turned and placed. */
function effectBone(o: Effect, bone: number): THREE.Vector3 | null {
  const matrix = o.model?.matrices[bone];
  if (!matrix) return null;
  return new THREE.Vector3().applyMatrix4(matrix).multiplyScalar(o.scale).applyEuler(angleEuler(o.angle, euler)).add(o.position);
}

/** RENDER_BRIGHT at `brightness` times the light: every mesh glows. */
function bright(o: Effect, brightness: number): void {
  o.blendMesh = -2;
  o.blendMeshLight = brightness;
}

// ---------------------------------------------------------------------------
// Killing Blow: a wolf's head lunges from the fist, more heads fly ahead, and the target is
// struck by bursts of hits.
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.wolfHeadEffect = {
  model: ['Effect', 'wolf_head_effect.bmd'],
  create: (o) => {
    const owner = o.owner;
    if (!owner) return false;
    o.position.copy(ownerBone(owner, 27));
    bright(o, 1);
    if (o.subType === 0) {
      o.scale = 1;
      o.lifeTime = 10 / (owner.playSpeed?.(RageAction.THRUST) ?? 0.4);
      return true;
    }
    if (o.subType === 1) {
      o.velocity = 0.1;
      o.scale = 1.2 + randInt(5) * 0.1;
      o.direction.y = -50;
      o.lifeTime = 2 + randInt(2);
      return true;
    }
    if (o.subType === 2) {
      o.velocity = 0.1;
      o.scale = 0.8 + randInt(3) * 0.1;
      o.direction.y = -40;
      o.lifeTime = 3 + randInt(3);
      o.angle.z += randInt(10) - 5;
      return true;
    }
    return false;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (o.subType !== 0 || !owner) return true;
    o.blendMeshLight = o.lifeTime / 20;
    o.alpha = o.blendMeshLight;
    if (o.lifeTime >= 3) {
      // The desktop shakes the fighter's angle for these children and puts it back.
      const angle = owner.angle.clone();
      const n = randInt(6);
      const value = 7 + randInt(3);
      const step = value * ((n % 3) * 2 + 1);
      angle.z += Math.trunc(n / 3) ? step : -step;
      const tilt = randInt(3);
      if (tilt === 1) angle.x += step;
      else if (tilt === 2) angle.x -= step;
      for (let i = 0; i < 3; i++) {
        const p = ownerBone(owner, 27);
        o.position.copy(p);
        o.scale = 1.5;
        p.y += randInt(20) - 10;
        p.z += randInt(28) + 6;
        system.createJoint('pinLightJoint', p, p, angle, 0, owner, 5);
      }
      system.createEffect('wolfHeadEffect', owner.position, angle, owner.light ?? WHITE, 1, owner);
      system.createEffect('wolfHeadEffect', owner.position, angle, owner.light ?? WHITE, 2, owner);
    }
    return true;
  },
};

// BITMAP_PIN_LIGHT as a joint: a short streak shot along its angle that slows and fades.
EXTRA_JOINTS.pinLightJoint = {
  init: (o) => {
    o.velocity = randInt(60) + 15;
    o.lifeTime = randInt(3) + 1;
    o.maxTails = 5;
    return true;
  },
  move: (o) => {
    o.velocity += 0.1;
    o.light.multiplyScalar(0.9);
  },
};

// BITMAP_SBUMB: twenty ticks of hit bursts on the target.
EXTRA_EFFECTS.sbumb = {
  create: (o) => {
    const owner = o.owner;
    if (!owner) return false;
    o.lifeTime = 20;
    o.scale = o.callScale;
    o.position.z = ownerBone(owner, 27).z;
    return true;
  },
  move: (o, system) => {
    const target = o.target;
    if (o.lifeTime < 17 && target) {
      const p = new THREE.Vector3(target.position.x + randInt(80) - 40, target.position.y + randInt(80) - 40, target.position.z + randInt(120) + 30);
      system.createParticle('sbumb', p, o.angle, WHITE, 0, (1.5 + randInt(10) * 0.02) * o.scale, o.owner);
    }
    return true;
  },
};

EXTRA_PARTICLES.sbumb = {
  texture: 'sbumb',
  init: (o, scale) => {
    o.lifeTime = 4;
    o.scale = scale;
    return true;
  },
  move: (o) => {
    o.frame = 4 - o.lifeTime;
  },
  draw: (o, system, width, height, blend) => {
    system.quads.billboard('sbumb', blend, o.position, width * 0.25, height, o.light, 0, (o.frame % 4) * 0.25 + 0.005, 0, 0.25 - 0.01, 1);
  },
};

// ---------------------------------------------------------------------------
// Beast Uppercut: two glowing fists (Effect/down_left_punch.bmd, then down_right_punch.bmd)
// strike down with pillars of light, and the ground at the target bursts.
// ---------------------------------------------------------------------------

function downAttackDummy(right: boolean): NonNullable<typeof EXTRA_EFFECTS.downAttackDummyR> {
  const end = right ? 6 : 8, hit = right ? 3 : 5;
  return {
    model: ['Effect', right ? 'down_left_punch.bmd' : 'down_right_punch.bmd'],
    create: (o) => {
      const owner = o.owner;
      if (!owner) return false;
      // Its own animation plays at twice the fighter's stamp speed.
      o.velocity = (owner.playSpeed?.(RageAction.STAMP) ?? 0.3) * 2;
      o.lifeTime = 100;
      bright(o, 1);
      return true;
    },
    move: (o, system) => {
      const owner = o.owner;
      if (!live(owner)) {
        o.live = false;
        return false;
      }
      if (o.animationFrame >= end) {
        o.live = false;
        if (right) system.createEffect('downAttackDummyL', owner.position, owner.angle, owner.light ?? WHITE, 0, owner, 0, { target: o.target });
      }
      if (o.animationFrame >= hit && o.lifeTime > 50) {
        if (o.target) createBomb3(system, o.target.position, 3, 0.9);
        o.lifeTime = 40;
      }
      const start = effectBone(o, 0);
      if (start) system.createJoint('forcePillar', start, start, o.angle, right ? 1 : 0, o.self, 50);
      o.angle.copy(owner.angle);
      o.position.copy(owner.position);
      const tip = effectBone(o, 0);
      if (tip) system.createSprite('flare', tip, 5, new THREE.Vector3(0.47, 0.36, 0.24), o.self);
      return true;
    },
  };
}
EXTRA_EFFECTS.downAttackDummyR = downAttackDummy(true);
EXTRA_EFFECTS.downAttackDummyL = downAttackDummy(false);

// BITMAP_FORCEPILLAR: a trail that follows the fist's first bone.
EXTRA_JOINTS.forcePillar = {
  init: (o) => {
    o.faces = 2;
    o.lifeTime = 7;
    o.maxTails = 5;
    o.light.set(0.95, 0.72, 0.48);
    return o.target !== null;
  },
  move: (o) => {
    const target = o.target;
    if (!live(target)) {
      o.live = false;
      return;
    }
    o.position.copy(target.position);
  },
};

/** CreateBomb3: an explosion, sparks (and stones) over a point. */
function createBomb3(system: EffectSystem, at: THREE.Vector3, subType: number, scale: number): void {
  const angle = new THREE.Vector3(randInt(60) + 60 + 90, 0, randInt(30));
  let bomb = at.clone();
  for (let i = 0; i < 2; i++) {
    if (subType === 3) bomb = new THREE.Vector3(at.x + randInt(90) - 40, at.y + randInt(90) - 40, at.z + randInt(100) + 70);
    else {
      if (i === 1 || randInt(3) !== 0) break;
      bomb = new THREE.Vector3(at.x + randInt(30) - 15, at.y + randInt(30) - 15, at.z + randInt(80) + 30);
    }
    system.createParticle('sahamuttExplosion', bomb, angle, WHITE, 0, scale !== 1 ? scale : 0.15 * randInt(10));
  }
  const count = subType === 3 ? 20 : 5;
  for (let i = 0; i < count; i++) system.createParticle('spark', bomb, angle, WHITE, 2);
}

// BITMAP_SUMMON_SAHAMUTT_EXPLOSION: a 4x4 sheet of an explosion played in sixteen ticks.
EXTRA_PARTICLES.sahamuttExplosion = {
  texture: 'sahamuttExplosion',
  init: (o, scale) => {
    o.lifeTime = 16;
    o.scale = scale;
    return true;
  },
  move: (o) => {
    o.frame = 16 - o.lifeTime;
  },
  draw: (o, system, width, height, blend) => {
    system.quads.billboard('sahamuttExplosion', blend, o.position, width, height, o.light, 0,
      (o.frame % 4) * 0.25 + 0.005, Math.trunc(o.frame / 4) * 0.25 + 0.005, 0.25 - 0.01, 0.25 - 0.01);
  },
};

// ---------------------------------------------------------------------------
// The party buffs (Increase Attack / Health / Defense): a blade of light rises, rings of wind
// spread on the ground with clouds and a circle; the buff then glows under the fighter.
// ---------------------------------------------------------------------------

// BITMAP_SWORD_EFFECT_MONO: rising blades every tick but every third.
EXTRA_EFFECTS.swordEffectMono = {
  create: (o) => {
    o.scale = o.callScale > 0 ? o.callScale : o.scale;
    return true;
  },
  move: (o, system) => {
    if (Math.trunc(o.lifeTime) % 3 !== 0) system.createParticle('swordEffectMonoParticle', o.position, o.angle, o.light, 0, o.scale, o.self);
    // At its 14th tick from the end a shock wave swells up from the blade, in the buff's colour.
    if (Math.trunc(o.lifeTime) === 14 && o.owner) {
      const light = o.subType === 1 ? new THREE.Vector3(0.7, 0.5, 1) : o.subType === 2 ? new THREE.Vector3(1, 0.12, 0) : new THREE.Vector3(0.4, 0.4, 1);
      system.createEffect('shockWave1', o.position, o.angle, light, 2, o.owner, 1);
    }
    return true;
  },
};

EXTRA_PARTICLES.swordEffectMonoParticle = {
  texture: 'swordEffectMono',
  init: (o, scale) => {
    o.lifeTime = 20;
    o.scale = scale;
    return true;
  },
  move: (o) => {
    if (o.subType === 1) o.scale *= 0.8;
    else if (o.subType === 0) o.scale *= 1.2;
    if (o.scale > 6) {
      o.subType = 1;
      return;
    }
    if (o.subType === 1 && o.scale < 1) {
      o.live = false;
      return;
    }
    o.light.multiplyScalar(0.94);
  },
  draw: (o, system, width, height, blend) => {
    const p = o.position.clone();
    p.z += 31 * o.scale;
    system.quads.billboard('swordEffectMono', blend, p, width * 0.9, height * 1.1, o.light, o.rotation);
  },
};

// MODEL_SHOCKWAVE_GROUND01 (Effect/shockwave_ground01.bmd): a ring on the ground that widens.
EXTRA_EFFECTS.shockWaveGround1 = {
  model: ['Effect', 'shockwave_ground01.bmd'],
  prewarmBlendMesh: -2,
  // RenderMesh(0): SubType 1 (Dragon Roar's ring) in Kwave2's texture.
  skin: (o) => (o.subType === 1 ? 'Kwave2.jpg' : undefined),
  skins: ['Kwave2.jpg'],
  paint: (_o, meshIndex, _material, mesh) => { if (meshIndex !== 0) mesh.visible = false; },
  create: (o) => {
    bright(o, 6);
    return true;
  },
  move: (o) => {
    o.scale *= 1.2;
    o.light.multiplyScalar(o.subType === 1 ? 0.9 : o.subType === 2 ? 0.7 : 0.8);
    return true;
  },
};

// BITMAP_EVENT_CLOUD: a turning cloud on the ground that grows (to 3, SubType 1 to 5) and fades.
EXTRA_EFFECTS.eventCloud = {
  create: () => true,
  move: (o, system) => {
    o.scale = Math.min(o.subType === 1 ? 5 : 3, o.scale * 1.08);
    o.angle.x = -Math.trunc(system.time) * 0.5;
    o.light.multiplyScalar(0.9);
    return true;
  },
  draw: (o, system) => {
    system.quads.decal('eventCloud', 'glow', o.position.x, o.position.y, o.scale, o.light.clone().multiplyScalar(o.alpha), o.angle.x,
      (x, y) => system.heightAt(x, y), o.position.z);
  },
};

// MODEL_WINDFOCE (Effect/wind_foce.bmd): a whirl of wind that turns, grows and fades; SubType 1
// stays on the fighter while its party buff lasts.
EXTRA_EFFECTS.windForce = {
  model: ['Effect', 'wind_foce.bmd'],
  create: (o) => {
    bright(o, 6);
    return o.subType <= 5;
  },
  move: (o, system) => {
    o.angle.z = -Math.trunc(system.time) * 0.3;
    if (o.subType === 0 || o.subType === 4 || o.subType === 5) {
      o.scale = Math.min(3, o.scale + 0.1);
      o.alpha *= 0.92;
      o.light.multiplyScalar(0.9);
      if (Math.trunc(o.lifeTime) === 40 || Math.trunc(o.lifeTime) === 30) {
        const light = o.subType === 0 ? new THREE.Vector3(0.5, 0.55, 1) : o.subType === 4 ? new THREE.Vector3(0.85, 0.2, 1) : new THREE.Vector3(1, 0.2, 0);
        system.createEffect('windForce', o.position, o.angle, light, 3, o.self, 1);
      }
    } else if (o.subType === 1) {
      const owner = o.owner;
      const buffed = live(owner) && [RageBuff.ATT_UP, RageBuff.HP_UP, RageBuff.DEF_UP].some((b) => owner.hasBuff?.(b) ?? true);
      o.lifeTime = buffed ? 10 : 0;
      if (!buffed || !owner) return true;
      const fade = Math.sin(system.time * 0.001) + 1;
      o.light.set(0.6, 0.6, 1).multiplyScalar(fade * 0.25 + 0.2);
      o.scale = 1;
      o.position.copy(owner.position);
    } else {
      o.angle.z = -Math.trunc(system.time) * 0.9;
      o.scale = Math.min(3, o.scale + 0.1);
      o.alpha *= 0.92;
      o.light.multiplyScalar(0.85);
      if (o.subType === 2 && (Math.trunc(o.lifeTime) === 60 || Math.trunc(o.lifeTime) === 50)) {
        system.createEffect('windForce', o.position, o.angle, new THREE.Vector3(1, 0.5, 0), 3, o.self, 1);
      }
    }
    return true;
  },
};

// MODEL_WINDFOCE_MIRROR (the catalogue gives LifeTime 50 and the call's scale): turns with the clock, grows to 1.5 and
// fades, following the fighter while Increase Defense lasts; drawn bright at six times its light.
EXTRA_EFFECTS.windForceMirror = {
  model: ['Effect', 'wind_foce_mirror.bmd'],
  create: (o) => {
    bright(o, 6);
    return o.owner !== null;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (!live(owner) || !(owner.hasBuff?.(RageBuff.DEF_UP) ?? true)) {
      o.live = false;
      return false;
    }
    o.angle.z = Math.trunc(system.time) * 0.4;
    o.scale = Math.min(1.5, o.scale * 1.015);
    o.light.multiplyScalar(0.9);
    o.position.copy(owner.position);
    return true;
  },
};

// BITMAP_SMOKELINE1, SubTypes 0 and 5 (5 moves with its owner): a line of smoke that fades in, grows, rises and fades.
EXTRA_PARTICLES.smokeLine1 = {
  texture: 'smokeLine1',
  init: (o, scale) => {
    if (o.subType !== 0 && o.subType !== 5) return false;
    o.lifeTime = 35;
    o.scale = (randInt(96) + 96) * 0.01 * scale;
    o.rotation = randInt(360);
    o.gravity = (randInt(16) + 12) * 0.1;
    o.alpha = 0;
    o.turningForce.copy(o.light);
    o.light.set(0, 0, 0);
    if (o.subType === 5) {
      if (!o.target) return false;
      o.startPosition.copy(o.target.position);
    }
    return true;
  },
  move: (o) => {
    if (o.lifeTime < 10) o.alpha -= 0.1;
    else if (o.alpha < 0.7) o.alpha += (randInt(5) + 2) * 0.1;
    else o.alpha = 0.7;
    if (o.alpha < 0.1) o.live = false;
    o.light.copy(o.turningForce).multiplyScalar(o.alpha);
    o.scale += (randInt(3) + 6) * 0.01;
    o.position.z += o.gravity;
    if (o.subType === 5 && o.target) {
      o.position.sub(o.startPosition);
      o.startPosition.copy(o.target.position);
      o.position.add(o.startPosition);
    }
  },
};

// BITMAP_LIGHT_RED: the party buffs' glow. SubType 0 (Increase Health) lies under the fighter
// while a party buff lasts, breathing; 1 and 2 only keep the buff's other looks going.
EXTRA_EFFECTS.lightRed = {
  create: (o, system) => {
    o.lifeTime = o.subType === 1 ? 50 : 9999;
    o.velocity = 0;
    o.scale = 1.5;
    o.light.set(1, 1, 1);
    if (o.subType === 3 || o.subType === 4) {
      // Dragon Roar's red glow under a volcano (3) and at its crack (4): the ground reddens at once.
      o.scale = o.callScale;
      o.lifeTime = o.subType === 4 ? 50 : 100;
      const l = randInt(5) * 0.01;
      system.groundLight(o.position, l + 1, l + 0.2, l + 0.2, o.subType === 4 ? 4 : 2);
      return o.owner !== null;
    }
    return o.subType <= 2 && o.owner !== null;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (o.subType === 3 || o.subType === 4) {
      const l = randInt(5) * 0.1;
      system.groundLight(o.position, l + 0.4, l, l, o.subType === 4 ? 3 : 2);
      return true;
    }
    // An effect's glow (no buffs: MODEL_DRAGON_LOWER_DUMMY's SubType 1) ends at once, as g_isCharacterBuff is false for it.
    const buffed = live(owner) && [RageBuff.ATT_UP, RageBuff.HP_UP, RageBuff.DEF_UP].some((b) => owner.hasBuff?.(b) ?? owner.bonePosition !== undefined);
    if (buffed) o.lifeTime = 10;
    else o.lifeTime = 0;
    if (!buffed || !owner) return true;
    // Increase Defense: now and then a wind passes over the fighter (MODEL_WINDFOCE_MIRROR, one at a time).
    if ((owner.hasBuff?.(RageBuff.DEF_UP) ?? false) && randInt(70) === 7 && !system.searchEffect('windForceMirror', owner)) {
      system.createEffect('windForceMirror', owner.position, o.angle, new THREE.Vector3(0.6, 0.6, 1), 0, owner, 1);
    }
    // Increase Attack: red smoke lines rise from the fists (bones 26 and 35) every tick.
    if (owner.hasBuff?.(RageBuff.ATT_UP) ?? false) {
      const hand = owner.bonePosition?.(randInt(2) === 0 ? 26 : 35);
      if (hand) system.createParticle('smokeLine1', hand, o.angle, new THREE.Vector3(1, 0.12, 0), 5, 0.005, owner);
    }
    return true;
  },
  draw: (o, system) => {
    const owner = o.owner;
    if ((o.subType === 3 || o.subType === 4) && owner) {
      const at = o.subType === 3 ? owner.position : o.position;
      const light = new THREE.Vector3(1, 1, 1).multiplyScalar(o.lifeTime * 0.01);
      for (let i = 0; i < (o.subType === 4 ? 2 : 1); i++) {
        system.quads.decal('lightRed', 'glow', at.x, at.y, o.scale, light, o.angle.x, (x, y) => system.heightAt(x, y), at.z);
      }
      return;
    }
    if (o.subType !== 0 || !owner) return;
    const light = new THREE.Vector3(1, 1, 1).multiplyScalar((Math.sin(system.time * 0.0005) + 1) * 0.25 + 0.2);
    for (let i = 0; i < 2; i++) {
      system.quads.decal('lightRed', 'glow', owner.position.x, owner.position.y, 3.5, light, o.angle.x, (x, y) => system.heightAt(x, y), owner.position.z);
    }
  },
};

/** RageCreateEffect for the party buffs: the blade, rings, clouds, wind and circle in the buff's colours. */
function partyBuff(system: EffectSystem, o: EffectOwner, kind: 0 | 1 | 2): void {
  const colours = [
    { blade: [0.2, 0.4, 1], ring: [0.6, 0.65, 1], cloud: [0.65, 0.8, 1], wind: [0.5, 0.55, 1], windType: 0, circle: [0.5, 0.55, 0.9] },
    { blade: [0.5, 0.28, 1], ring: [1, 0.35, 1], cloud: [0.8, 0.4, 1], wind: [0.85, 0.2, 1], windType: 4, circle: [0.78, 0.2, 1] },
    { blade: [1, 0.15, 0], ring: [1, 0.1, 0], cloud: [1, 0.3, 0.2], wind: [1, 0.2, 0], windType: 5, circle: [1, 0.15, 0] },
  ][kind];
  const v = (c: number[]): THREE.Vector3 => new THREE.Vector3(c[0], c[1], c[2]);
  system.createEffect('swordEffectMono', o.position, o.angle, v(colours.blade), kind, o, 1);
  for (let i = 0; i < 3; i++) {
    system.createEffect('shockWaveGround1', o.position, o.angle, v(colours.ring), 0, o, 1 - i * 0.15);
    system.createEffect('eventCloud', o.position, o.angle, v(colours.cloud), 0, o, 1);
  }
  system.createEffect('windForce', o.position, o.angle, v(colours.wind), colours.windType, o, 1);
  system.createEffect('magic', o.position, o.angle, v(colours.circle), 13);
}

// ---------------------------------------------------------------------------
// Dragon Slasher: rings burst ahead of the fighter and the target is struck by lightning.
// ---------------------------------------------------------------------------

// MODEL_DRAGON_KICK_DUMMY: lives while the kick plays; from its sixth key the target crackles.
EXTRA_EFFECTS.dragonKickDummy = {
  model: ['Effect', 'dragon_kick_dummy.bmd'],
  alwaysModel: true, // hidden, but it animates: its bones lead the wind
  create: (o) => {
    const owner = o.owner;
    if (!owner) return false;
    o.lifeTime = 200;
    o.velocity = (owner.playSpeed?.(RageAction.DRAGONKICK) ?? 0.3) * 2;
    o.hiddenMesh = -2; // only its bones guide the wind; the desktop draws nothing of it
    kickDummies.set(o.self, o);
    void kickDummyModel();
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    const frame = owner?.animationFrame?.() ?? 0;
    if (!live(owner) || owner.currentAction?.() !== RageAction.DRAGONKICK) {
      o.live = false;
      return false;
    }
    // AnimationFrame in (4, 5): the web's frames can step over that window between ticks, so the window is taken as
    // crossed when it overlaps the frames since the last tick (o->Distance keeps the last frame).
    const before = o.distance;
    o.distance = frame;
    if (frame > 4 && before < 5 && frame !== before) {
      // The wind along the kick: a trail per tenth of a key, each following the dummy's second bone that much later.
      const count = Math.round(o.velocity * 10);
      for (let i = 0; i < count; i++) system.createJoint('groundWind', o.position, o.position, o.angle, i, o.self, 150);
    }
    const target = o.target;
    if (target && frame > 5) {
      const fade = Math.sin(system.time * 0.002) * 0.2;
      for (let j = 0; j < 2; j++) {
        const p = target.position.clone().setZ(target.position.z + 30);
        const scale = (randInt(40) + 50) * 0.01 * 1.5 * o.scale * 1.8;
        for (let i = 0; i < 4; i++) {
          system.createParticle('energy', p, o.angle, o.light, 7, scale);
          system.createParticle('lightningMega', p, o.angle, o.light, 0, scale * 0.9);
          system.createParticle('spark3', p, o.angle, o.light, 3, 1 + fade * 0.05);
          system.createParticle('spark3', p, o.angle, o.light, 8, 1 + fade * 0.05);
          if (i % 2) system.createParticle('spark', p, o.angle, o.light, 13);
          if (i % 2) p.set(p.x + randInt(40) + 20, p.y + randInt(30) + 10, p.z + randInt(30) + 25);
          else p.set(p.x - randInt(20) - 10, p.y + randInt(30) - 10, p.z + randInt(30) + 30);
        }
      }
    }
    o.angle.copy(owner.angle);
    o.position.copy(owner.position);
    if (frame > 8) o.lifeTime = 150;
    return true;
  },
};

// MODEL_DRAGON_KICK_DUMMY's own model: its bones lead the wind (Effect/dragon_kick_dummy.bmd).
const kickDummies = new WeakMap<EffectOwner, Effect>();
let kickModel: BMDModel | null = null;
let kickLoad: Promise<void> | null = null;
function kickDummyModel(): Promise<void> {
  kickLoad ??= loadModel('Effect', 'dragon_kick_dummy.bmd').then((model) => { kickModel = model; }).catch(() => { /* no wind */ });
  return kickLoad;
}

/** TransformByObjectBone on the dummy posed at `frame`: the bone in the world. */
function kickDummyBone(o: Effect, bone: number, frame: number): THREE.Vector3 | null {
  if (!kickModel) return null;
  const matrix = computeBoneMatrices(kickModel, 0, frame)[bone];
  if (!matrix) return null;
  return new THREE.Vector3().applyMatrix4(matrix).multiplyScalar(o.scale).applyEuler(angleEuler(o.angle, euler)).add(o.position);
}

// BITMAP_GROUND_WIND: a wind trail on the dummy's second bone, SubType tenths of a key behind it, 20 up, fading.
EXTRA_JOINTS.groundWind = {
  init: (o) => {
    o.faces = 2; // RENDER_FACE_TWO
    o.lifeTime = 10;
    o.maxTails = 8;
    o.light.set(0.7, 0.7, 1);
    return o.target !== null && kickDummies.has(o.target);
  },
  move: (o) => {
    const dummy = o.target ? kickDummies.get(o.target) : undefined;
    if (!dummy || !dummy.live) {
      o.live = false;
      return;
    }
    const p = kickDummyBone(dummy, 2, dummy.animationFrame + o.subType * 0.1);
    if (p) o.position.copy(p);
    o.position.z += 20;
    o.light.multiplyScalar(0.7);
  },
};

/** The rings ahead of the fighter (Dragon Slasher; the same as Multi-Shot's). */
function ringsAhead(system: EffectSystem, o: EffectOwner, angle: THREE.Vector3): void {
  const light = new THREE.Vector3(0.8, 0.9, 1.6);
  const front = rotateByAngle(new THREE.Vector3(0, 20, 0), angle).add(o.position);
  system.createEffect('multiShot3', front, angle, light);
  system.createEffect('multiShot3', front, angle, light);
  for (let i = 0; i < 3; i++) system.createEffect('multiShot1', o.position, angle, light);
  system.createEffect('multiShot2', front, angle, light);
  system.createEffect('multiShot2', front, angle, light);
}

/** The skill numbers of the fighter's skills whose effects are ported. */
export const RageSkill = {
  KILLING_BLOW: 260,
  BEAST_UPPERCUT: 261,
  DRAGON_KICK: 265,
  ATT_UP: 266,
  HP_UP: 267,
  DEF_UP: 268,
  DARK_SIDE: 263,
  PHOENIX_SHOT: 270,
  CHARGE: 269,
  CHAIN_DRIVE: 262,
  DRAGON_ROAR: 264,
} as const;

/** CMonkSystem::RageCreateEffect (and ReceiveMagic's buff glows) for a fighter's cast. */
export function rageCreateEffect(system: EffectSystem, skill: number, caster: EffectOwner, target: EffectOwner | null): boolean {
  const light = caster.light ?? WHITE;
  switch (skill) {
    case RageSkill.KILLING_BLOW: {
      const angle = caster.angle.clone();
      if (target) angle.z = createAngle2D(caster.position, target.position);
      system.createEffect('wolfHeadEffect', caster.position, angle, light, 0, caster);
      if (target) system.createEffect('sbumb', target.position, angle, light, 0, caster, 2.1, { target });
      return true;
    }
    case RageSkill.BEAST_UPPERCUT:
      system.createEffect('downAttackDummyR', caster.position, caster.angle, light, 0, caster, 0, { target });
      return true;
    case RageSkill.DRAGON_KICK: {
      system.createEffect('dragonKickDummy', caster.position, caster.angle, new THREE.Vector3(0.7, 0.7, 1), 0, caster, 1, { target });
      if (!target) return true;
      const angle = caster.angle.clone();
      angle.z = createAngle2D(caster.position, target.position);
      ringsAhead(system, caster, angle);
      return true;
    }
    case RageSkill.DARK_SIDE:
      // A red cloud, a burning circle and a whirl of wind as the fighter vanishes (its dash and afterimages, CDummyUnit
      // and DarksideRendering, follow the server's Dark Side target list: darkSideDash.ts).
      system.createEffect('eventCloud', caster.position, caster.angle, new THREE.Vector3(1, 0.6, 0.6), 1, caster, 1);
      system.createEffect('magic', caster.position, caster.angle, new THREE.Vector3(1, 0.4, 0), 14);
      system.createEffect('windForce', caster.position, caster.angle, new THREE.Vector3(1, 0.5, 0.2), 2, caster, 1);
      return true;
    case RageSkill.CHAIN_DRIVE:
      chainDrive(system, caster, target);
      return true;
    case RageSkill.DRAGON_ROAR:
      dragonRoar(system, caster, target);
      return true;
    case RageSkill.CHARGE: // AT_SKILL_OCCUPY: the rings in front as the fighter rushes
      ringsAhead(system, caster, caster.angle);
      return false; // its attack (the rush's sparks) follows
    case RageSkill.PHOENIX_SHOT:
      // MODEL_PHOENIX_SHOT at the target: CreateEffect has no case for it, so it gets no life time and ends the tick
      // it is made; the desktop shows nothing for it.
      return true;
    case RageSkill.ATT_UP:
    case RageSkill.HP_UP:
    case RageSkill.DEF_UP: {
      partyBuff(system, caster, skill === RageSkill.ATT_UP ? 0 : skill === RageSkill.HP_UP ? 1 : 2);
      // ReceiveMagic: the glow that stays with the buff (on the fighter, as for others' casts).
      const glow = skill === RageSkill.HP_UP ? 0 : skill === RageSkill.DEF_UP ? 2 : 1;
      system.createEffect('lightRed', caster.position, caster.angle, light, glow, caster, 1.5);
      if (skill === RageSkill.DEF_UP) system.createEffect('windForce', caster.position, caster.angle, new THREE.Vector3(0.7, 0.7, 1), 1, caster, 1);
      return true;
    }
  }
  return false;
}

