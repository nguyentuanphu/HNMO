// The Summoner's skill effects (registered in extraEffects.ts's tables).
//
// Followed C++ (client/src/source):
//   CreateEffect: MODEL_LIGHTNING_SHOCK (0, 1), BITMAP_MAGIC (12); the catalogue's rows for
//   MODEL_ALICE_DRAIN_LIFE, MODEL_CHAIN_LIGHTNING, MODEL_ALICE_BUFFSKILL_EFFECT(2),
//   BITMAP_MAGIC_ZIN, BITMAP_SHINY+6, MODEL_SUMMONER_CASTING_EFFECT2/22/222, BITMAP_DAMAGE_01_MONO
//                                                                           Render/Effects/ZzzEffect.cpp
//   MoveEffect (BITMAP_SHINY+6), DescribeEffectShadow (BITMAP_MAGIC_ZIN, BITMAP_MAGIC 12,
//   BITMAP_DAMAGE_01_MONO)                                                  Render/Effects/ZzzEffect.cpp
//   Move_MODEL_ALICE_DRAIN_LIFE, _CHAIN_LIGHTNING, _ALICE_BUFFSKILL_EFFECT, _LIGHTNING_SHOCK,
//   _SUMMONER_CASTING_EFFECT*, Move_BITMAP_MAGIC_ZIN, _PIN_LIGHT, _MAGIC, _DAMAGE_01_MONO
//                                                                           Render/Effects/Behaviors/MoveHandlers.cpp
//   RenderObject (MODEL_ALICE_BUFFSKILL_EFFECT: RENDER_BRIGHT, SubType 1 RENDER_DARK)
//                                                                           Engine/Object/ZzzObject.cpp
//   CreateJoint / MoveJoint: BITMAP_DRAIN_LIFE_GHOST (0), BITMAP_JOINT_ENERGY (5, 45)
//                                                                           Render/Effects/ZzzEffectJoint.cpp
//   CreateParticle / MoveParticles / DescribeParticle: BITMAP_LIGHT+2 (6, 7), BITMAP_LIGHTNING_MEGA1..3 (0),
//   BITMAP_MAGIC (0), BITMAP_PIN_LIGHT (0), BITMAP_SHINY+6 (0)               Render/Effects/ZzzEffectParticle.cpp

import * as THREE from 'three';
import { randInt, rotateByAngle, moveHumming } from './core.js';
import type { BitmapName } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { chaoticShine } from './darkLordEffects.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS, EXTRA_PARTICLES } from './extraEffects.js';
import type { Effect, EffectOwner } from './types.js';

const WHITE = new THREE.Vector3(1, 1, 1);
const ZERO = new THREE.Vector3();

function live(owner: EffectOwner | null): owner is EffectOwner {
  return owner !== null && (owner.isLive?.() ?? true);
}

/** The world positions of all of an owner's bones (TransformPosition of each BoneTransform). */
function bonePositions(owner: EffectOwner): THREE.Vector3[] {
  const count = owner.boneCount?.() ?? 0;
  const out: THREE.Vector3[] = [];
  for (let i = 0; i < count; i++) {
    const p = owner.bonePosition?.(i);
    if (p) out.push(p);
  }
  return out;
}

function bone(owner: EffectOwner, index: number, height = 100): THREE.Vector3 {
  return owner.bonePosition?.(index) ?? owner.position.clone().setZ(owner.position.z + height);
}

const fRot = (system: EffectSystem): number => system.time * 0.0006 * 360;

// ---------------------------------------------------------------------------
// Drain Life: red lights rise around the Summoner and the target, ghosts fly from the hand to
// the target, then the target's life flows back along red trails.
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.aliceDrainLife = {
  create: (o) => o.subType === 0 && o.owner !== null && o.target !== null,
  move: (o, system) => {
    const source = o.owner, target = o.target;
    if (!live(source) || !live(target)) return true;
    const r = randInt(10);
    const count = r === 0 ? 0 : r <= 7 ? 1 : 2;
    const red = new THREE.Vector3(1, 0.2, 0.2);
    const around = (who: EffectOwner): THREE.Vector3 => new THREE.Vector3(
      who.position.x + randInt(80) - 40, who.position.y + randInt(60) - 30, who.position.z + 80 + randInt(180) - 100);
    for (let i = 0; i < count; i++) system.createParticle('light3', around(source), source.angle, red, 7, 1.8);
    if (o.lifeTime <= 60) for (let i = 0; i < count; i++) system.createParticle('light3', around(target), target.angle, red, 7, 1.8);
    if (o.lifeTime <= 70 && o.lifeTime >= 66) {
      // The desktop's switch falls through from 4 and 5 into 3 ghosts.
      const g = randInt(10);
      const ghosts = g === 0 ? 0 : g <= 3 ? 1 : g <= 8 ? 3 : 4;
      const radians = source.angle.z * Math.PI / 180;
      // AngleMatrix's second column: the facing's forward axis (sin, -cos) is -y; its y axis.
      const dir = new THREE.Vector3(-Math.sin(radians), Math.cos(radians), 0);
      for (let i = 0; i < ghosts; i++) {
        const from = bone(source, 18).clone();
        from.x += dir.x * 100 + randInt(10) * 5;
        from.y += dir.y * 100 + randInt(10) * 5;
        from.z += randInt(10) * 5;
        const to = target.position.clone().setZ(target.position.z + 100 + (randInt(10) - 5) * 4);
        system.createJoint('drainLifeGhost', from, to, o.angle, 0, target, 40, 0, 0, new THREE.Vector3(0.8, 0.1, 0.2));
      }
    }
    if (Math.trunc(o.lifeTime) === 64) {
      const to = source.position.clone().setZ(source.position.z + 80);
      for (const p of bonePositions(target)) {
        if (randInt(2) === 0) system.createJoint('jointEnergy', p, to, target.angle, 45, source, 10, 0, 0, new THREE.Vector3(1, 0, 0.1));
      }
    }
    return true;
  },
};

// BITMAP_DRAIN_LIFE_GHOST SubType 0: a ghost mark turned aside that swerves in to the target.
EXTRA_JOINTS.drainLifeGhost = {
  init: (o) => {
    if (o.subType !== 0) return false;
    o.angle.z += randInt(2) === 0 ? 90 : -90;
    o.angle.x += randInt(100) - 50;
    o.angle.y += randInt(100) - 50;
    o.angle.z += randInt(100) - 50;
    o.velocity = 1 + randInt(10) * 0.2;
    o.lifeTime = 30 + randInt(20) - 10;
    o.scale += randInt(60) - 30;
    o.maxTails = 20 + randInt(10) - 5;
    return true;
  },
  move: (o) => {
    const target = o.target;
    if (target) moveHumming(o.position, o.angle, new THREE.Vector3(target.position.x, target.position.y, o.targetPosition.z), o.velocity);
    o.velocity += 2;
    if (o.lifeTime < 10) o.light.multiplyScalar(1 / 1.2);
  },
};

// BITMAP_JOINT_ENERGY SubType 45: a red trail that spirals up from a bone for twenty ticks,
// then flies to the Summoner's chest and ends there.
EXTRA_JOINTS.jointEnergy = {
  init: (o) => {
    if (o.subType === 5) { // a trail behind its target (Cometfall's comet, its EyeLeft): endless while it lives
      o.lifeTime = Number.MAX_SAFE_INTEGER;
      o.maxTails = 10;
      o.velocity = 0;
      return o.target !== null;
    }
    if (o.subType !== 45) return false;
    o.lifeTime = 120;
    o.maxTails = 8;
    o.velocity = 3;
    return true;
  },
  move: (o, system) => {
    if (o.subType === 5) {
      if (!live(o.target)) o.live = false;
      else o.position.copy(o.target.position);
      return;
    }
    if (o.lifeTime >= 100) {
      o.angle.z += 10;
      o.position.z += 6;
    } else {
      o.velocity = Math.min(30, o.velocity + 5);
      const target = o.target;
      if (target) o.targetPosition.copy(target.position).setZ(target.position.z + 120);
      const before = o.angle.z;
      const distance = moveHumming(o.position, o.angle, o.targetPosition, o.velocity);
      if (distance <= 35) o.live = false;
      else if (distance <= 70 && Math.abs(before - o.angle.z) > 20 && o.velocity >= 20) o.velocity -= 10;
    }
    system.createSprite('flareRed', o.position, 0.3, WHITE, o.target);
  },
};

// BITMAP_LIGHT+2 SubTypes 6 and 7: a light thrown up that falls, spins and fades (7 is drawn
// with BITMAP_LIGHT and keeps its colour; 6 is subtracted).
EXTRA_PARTICLES.light3 = {
  texture: 'light3',
  init: (o, scale) => {
    if (o.subType !== 6 && o.subType !== 7) return false;
    o.lifeTime = o.subType === 6 ? 21 : 18;
    o.scale = scale + (randInt(4) + 8) * 0.01;
    o.gravity = (randInt(1) + 1) * 0.1;
    o.angle.z = randInt(360);
    o.turningForce.copy(rotateByAngle(new THREE.Vector3(0, (randInt(2) + 60) * 0.1, 0), o.angle));
    o.turningForce.z += 2;
    o.alpha = 1;
    o.velocity.set(0, 0, 0);
    if (o.subType === 7) o.texture = 'light';
    return true;
  },
  move: (o) => {
    o.turningForce.z -= o.gravity;
    o.position.add(o.turningForce);
    o.angle.x += 0.5 * o.lifeTime;
    o.angle.y += 0.5 * o.lifeTime;
    o.scale += 0.04;
    if (o.lifeTime < 10) o.alpha -= 0.1;
    if (o.lifeTime < 20) {
      o.turningForce.x *= 0.6;
      o.turningForce.y *= 0.6;
    }
    if (o.subType === 6) o.light.setScalar(o.alpha);
    else o.light.multiplyScalar(o.alpha);
    if (o.alpha < 0) o.live = false;
    o.velocity.x += 0.05;
    o.velocity.y += 0.05;
    o.rotation += 2;
    const luminosity = o.lifeTime / 8 * 0.02;
    o.light.addScalar(luminosity);
  },
  blend: (o) => (o.subType === 6 ? 'subtract' : 'glow'),
};

// ---------------------------------------------------------------------------
// Chain Lightning: bolts from both hands to the first target, then from each target to the next.
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.chainLightning = {
  create: (o) => o.subType <= 2 && o.owner !== null && o.target !== null,
  move: (o, system) => {
    const source = o.owner, target = o.target;
    if (!live(source) || !live(target)) {
      o.live = false;
      return false;
    }
    const light = new THREE.Vector3(0.4, 0.4, 1);
    if (o.subType === 0) {
      for (const [boneIndex, side] of [[37, 60], [28, -60]] as const) {
        const from = bone(source, boneIndex, 130);
        for (const angle of [new THREE.Vector3(-60, 0, source.angle.z), new THREE.Vector3(0, 0, source.angle.z + side)]) {
          system.createJoint('jointThunder', from, target.position, angle, 0, target, 50, 0, 0, light);
          system.createJoint('jointThunder', from, target.position, angle, 0, target, 10, 0, 0, light);
        }
      }
    } else if (source !== target) {
      const from = source.position.clone().setZ(source.position.z + 80);
      system.createJoint('jointThunder', from, target.position, target.angle, 0, target, 50, 0, 0, light);
      system.createJoint('jointThunder', from, target.position, target.angle, 0, target, 10, 0, 0, light);
    }
    if (Math.trunc(o.lifeTime) === 15) {
      const glow = new THREE.Vector3(0.2, 0.2, 0.8);
      for (const p of bonePositions(target)) system.createParticle('light', p, ZERO, glow, 5, 3 + (randInt(20) - 10) * 0.1);
    }
    return true;
  },
};

// ---------------------------------------------------------------------------
// Sleep, Berserker: a ring (Data/Effect/elshildring*.bmd) grows and turns over the head with
// lights and gathering trails.
// ---------------------------------------------------------------------------

function buffRing(clockwise: boolean): NonNullable<typeof EXTRA_EFFECTS.aliceBuffSkillEffect> {
  return {
    model: ['Effect', clockwise ? 'elshildring.bmd' : 'elshildring2.bmd'],
    create: (o) => {
      if (o.subType > 2) return false;
      o.blendMesh = -2; // RENDER_BRIGHT: the whole ring glows
      if (o.subType === 1) o.renderType = 'dark';
      return o.owner !== null;
    },
    move: (o, system) => {
      const owner = o.owner;
      if (!live(owner)) {
        o.live = false;
        return false;
      }
      o.position.copy(owner.position).setZ(owner.position.z + 100);
      if (o.lifeTime > 20) {
        o.alpha += 0.05;
        o.blendMeshLight += 0.05;
      } else {
        o.alpha -= 0.05;
        o.blendMeshLight -= 0.05;
        if (o.alpha < 0) o.live = false;
      }
      o.angle.z += clockwise ? 8 : -8;
      o.scale += 0.035;
      const a = o.alpha;
      const sprite = o.subType === 1 ? 1 : 0;
      const flare = o.subType === 0 ? new THREE.Vector3(0.8 * a, 0.1 * a, 0.9 * a) : o.subType === 1 ? new THREE.Vector3(a, a, a) : new THREE.Vector3(0.8 * a, 0.5 * a, 0.2 * a);
      system.createSprite('light', o.position, 5, flare, o.self, 0, sprite);
      system.createSprite('light', o.position, 5, flare, o.self, 0, sprite);
      const shiny = o.subType === 0 ? new THREE.Vector3(0.7 * a, 0.6 * a, 0.9 * a) : flare;
      system.createSprite('shiny5', o.position, 2, shiny, o.self, fRot(system), sprite);
      system.createSprite('shiny5', o.position, 1, shiny, o.self, -fRot(system), sprite);
      const trail = o.subType === 0 ? new THREE.Vector3(0.7, 0.5, 0.7) : o.subType === 1 ? new THREE.Vector3(1, 1, 1) : new THREE.Vector3(0.8, 0.5, 0.2);
      for (let i = 0; i < 3; i++) {
        const angle = new THREE.Vector3(randInt(90), 0, randInt(360));
        const from = o.position.clone().sub(rotateByAngle(new THREE.Vector3(0, -200, 0), angle));
        system.createJoint('jointHealing', from, o.position, angle, o.subType === 1 ? 16 : 15, o.self, 5, 0, 0, trail);
      }
      return true;
    },
  };
}
EXTRA_EFFECTS.aliceBuffSkillEffect = buffRing(true);
EXTRA_EFFECTS.aliceBuffSkillEffect2 = buffRing(false);

// ---------------------------------------------------------------------------
// Weakness and Innovation: magic circles on the ground, the turning summoning circles
// (Data/Effect/Suhwanzin2*.bmd) and pins of light falling around the Summoner.
// ---------------------------------------------------------------------------

// BITMAP_MAGIC_ZIN: a circle on the ground that fades in and out (SubType 2 also grows).
EXTRA_EFFECTS.magicZin = {
  create: (o) => {
    o.scale = o.callScale;
    return o.subType <= 2;
  },
  move: (o) => {
    if (o.subType === 0) {
      if (o.lifeTime < 20) o.alpha -= 0.05;
      else if (o.alpha < 1) o.alpha += 0.05;
    } else if (o.subType === 1) {
      if (o.lifeTime < 20) o.alpha -= 0.03;
      else if (o.alpha < 0.7) o.alpha += 0.06;
    } else {
      if (o.scale < 3.5) o.scale += 0.1;
      if (o.lifeTime < 20) o.alpha -= 0.05;
      else if (o.alpha < 1) o.alpha += 0.05;
    }
    return true;
  },
  draw: (o, system) => {
    const k = o.subType === 0 ? o.alpha * 2 : o.subType === 1 ? o.alpha / 2.5 : o.alpha;
    const light = o.light.clone().multiplyScalar(Math.max(0, k));
    system.quads.decal('magicZin', 'glow', o.position.x, o.position.y, o.scale, light, o.headAngle.y, (x, y) => system.heightAt(x, y), o.position.z);
  },
};

// MODEL_SUMMONER_CASTING_EFFECT2/22/222: the summoning circles' rings, turning in turns.
function castingCircle(file: string, turn: number): NonNullable<typeof EXTRA_EFFECTS.summonerCastingEffect2> {
  return {
    model: ['Effect', file],
    create: (o) => {
      o.scale = o.callScale > 0 ? o.callScale : o.scale;
      return true;
    },
    move: (o) => {
      if (o.lifeTime < 20) o.blendMeshLight -= 0.03;
      else if (o.blendMeshLight < 0.5) o.blendMeshLight += 0.05;
      o.angle.z += turn;
      return true;
    },
  };
}
EXTRA_EFFECTS.summonerCastingEffect2 = castingCircle('Suhwanzin2.bmd', 3);
EXTRA_EFFECTS.summonerCastingEffect22 = castingCircle('Suhwanzin22.bmd', -3);
EXTRA_EFFECTS.summonerCastingEffect222 = castingCircle('Suhwanzin222.bmd', 3);

/** BITMAP_SHINY+6 and BITMAP_PIN_LIGHT SubType 0: lights falling within 250 of the point. */
function fallingLights(type: 'shiny5Particle' | 'pinLightParticle'): NonNullable<typeof EXTRA_EFFECTS.shiny5> {
  return {
    create: (o) => {
      if (type === 'pinLightParticle') o.lifeTime = 40;
      o.scale = o.callScale > 0 ? o.callScale : o.scale;
      if (o.subType === 1 || o.subType === 2) o.lifeTime = 100;
      return o.subType === 0 || ((o.subType === 1 || o.subType === 2) && o.owner !== null) || (type === 'shiny5Particle' && o.subType === 3 && o.owner !== null);
    },
    move: (o, system) => {
      if (o.subType === 1 || o.subType === 2) { // on a debuffed owner: from a random bone, every other tick
        const owner = o.owner;
        const debuff = o.subType === 1 ? 76 : 77; // eDeBuff_AttackDown / _DefenseDown (ClearBuffPhysicalEffect ends them)
        if (!owner || !(owner.isLive?.() ?? true) || !(owner.hasBuff?.(debuff) ?? true)) {
          o.live = false;
          return false;
        }
        o.lifeTime = 100;
        const bones = owner.boneCount?.() ?? 0;
        const matrix = bones > 0 ? owner.boneMatrix?.(randInt(bones)) : null;
        if (matrix && randInt(2) === 0) {
          const p = new THREE.Vector3(0, 0, 100).applyMatrix4(matrix);
          p.z -= 20;
          system.createParticle(type, p, o.angle, o.light, 0, o.scale);
        }
        return true;
      }
      if (o.subType === 3) { // Chaotic Diseier's dark shines (darkLordEffects.ts)
        chaoticShine(o, system);
        return true;
      }
      const p = new THREE.Vector3(o.position.x + randInt(500) - 250, o.position.y + randInt(500) - 250, o.position.z - randInt(100) + 150);
      system.createParticle(type, p, o.angle, o.light, 0, o.scale);
      return true;
    },
  };
}
EXTRA_EFFECTS.shiny5 = fallingLights('shiny5Particle');
EXTRA_EFFECTS.pinLight = fallingLights('pinLightParticle');

function fallingParticle(texture: BitmapName, gravity: number, scatter: number, spin: number): NonNullable<typeof EXTRA_PARTICLES.pinLightParticle> {
  return {
    texture,
    init: (o, scale) => {
      if (o.subType !== 0) return false;
      o.alpha = 1;
      o.position.x += -5 + randInt(10);
      o.position.y += -5 + randInt(10);
      o.position.z += -5 + randInt(10);
      o.lifeTime = 30;
      o.gravity = gravity;
      o.scale = scale + randInt(scatter) / 10;
      return true;
    },
    move: (o) => {
      o.rotation += spin;
      o.scale -= 0.02;
      o.alpha -= 0.001;
      if (o.alpha <= 0) o.live = false;
      o.position.z -= o.gravity;
      o.light.addScalar(randInt(10) / 100 - 0.05);
    },
    draw: texture === 'shiny5' ? (o, system, width, height, blend) => {
      // DescribeParticle BITMAP_SHINY+6: the shine and a light over it.
      system.quads.billboard('shiny5', blend, o.position, width, height, o.light, o.rotation);
      system.quads.billboard('light', blend, o.position, width, height, o.light, o.rotation);
    } : undefined,
  };
}
EXTRA_PARTICLES.pinLightParticle = fallingParticle('pinLight', 10, 5, 0);
EXTRA_PARTICLES.shiny5Particle = fallingParticle('shiny5', 1.3, 3, 5);

// ---------------------------------------------------------------------------
// Lightning Shock: a red ball of lightning gathers over the Summoner, drops in front of it and
// bursts on the ground.
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.lightningShock = {
  create: (o, system) => {
    if (o.subType === 0) {
      if (!o.owner) return false;
      o.lifeTime = 20;
      o.position.z += 280;
      o.velocity = 0;
      o.gravity = 1;
      return true;
    }
    if (o.subType === 1) {
      o.lifeTime = 12;
      system.createEffect('damage01Mono', o.position, o.angle, new THREE.Vector3(1, 0.8, 0.5), 1);
      system.createEffect('damage01Mono', o.position, o.angle, new THREE.Vector3(1, 0, 0), 1);
      const ground = new THREE.Vector3(1, 0.2, 0.05);
      for (let i = 0; i < 5; i++) {
        const p = rotateByAngle(new THREE.Vector3(0, 150, 0), new THREE.Vector3(0, 0, o.angle.z + i * 72)).add(o.position);
        system.createEffect('magic', p, o.angle, ground, 12);
      }
      return true;
    }
    if (o.subType === 2) return o.owner !== null; // the debuff's crackle on the struck (eDeBuff_LightningShockDamage)
    return false;
  },
  move: (o, system) => {
    if (o.subType === 2) {
      const owner = o.owner;
      if (!owner || !(owner.isLive?.() ?? true) || !(owner.hasBuff?.(85) ?? true)) {
        o.live = false;
        return false;
      }
      o.lifeTime = 100;
      o.position.copy(owner.position);
      const bones = owner.boneCount?.() ?? 0;
      for (let i = 0; i < bones; i++) {
        const p = owner.bonePosition?.(i);
        if (!p) continue;
        if (randInt(60) === 0) system.createParticle('light', p, new THREE.Vector3(), new THREE.Vector3(0.8, 0, 0), 5, 5 + (randInt(20) - 10) * 0.1);
        if (randInt(5) === 0) system.createParticle('lightningMega', p, new THREE.Vector3(), new THREE.Vector3(1, 0.8, 0.4), 0, (randInt(70) + 22) * 0.01);
      }
      const red = new THREE.Vector3(1, 0, 0);
      system.createParticle('smoke', o.position, o.angle, red, 58);
      if (randInt(4) === 0) system.createParticle('smoke', o.position, o.angle, red, 54, 2.8);
      return true;
    }
    const megaAround = (light: THREE.Vector3, flare: THREE.Vector3 | null): void => {
      for (let i = 0; i < 11; i++) {
        const scale = (randInt(80) + 32) * 0.01;
        const p = new THREE.Vector3(o.position.x + randInt(70) - 35, o.position.y + randInt(70) - 35, o.position.z + randInt(70) - 35);
        if (flare) {
          system.createSprite('light', p, 2.2, flare, o.self);
          if (randInt(3) > 0) continue;
        }
        system.createParticle('lightningMega', p, o.angle, light, 0, scale);
      }
    };
    if (o.subType === 0) {
      const owner = o.owner;
      if (owner && ((owner.animationFrame?.() ?? 7) > 6 || o.lifeTime < 15)) {
        o.direction.set(0, -20, -75 - o.velocity);
        o.gravity += 0.1;
        o.velocity += o.gravity;
      }
      const scale = 0.8;
      const rot = fRot(system);
      system.createSprite('shiny2', o.position, 4 * scale, new THREE.Vector3(1, 0.4, 0.4), o.self, rot);
      system.createSprite('shiny2', o.position, 3 * scale, new THREE.Vector3(1, 0.4, 0.4), o.self, -rot);
      system.createSprite('magicGround1', o.position, 1 * scale, new THREE.Vector3(1, 0.2, 0.2), o.self, rot);
      system.createSprite('magicGround1', o.position, 0.5 * scale, new THREE.Vector3(1, 0.2, 0.2), o.self, -rot);
      system.createSprite('pinLight', o.position, 2 * scale, new THREE.Vector3(1, 0.4, 0.4), o.self, randInt(360));
      system.createSprite('pinLight', o.position, 2 * scale, new THREE.Vector3(1, 0.4, 0.4), o.self, randInt(360));
      system.createParticle('magicParticle', o.position, o.angle, new THREE.Vector3(1, 0.3, 0.3), 0, scale);
      megaAround(new THREE.Vector3(1, 0.7, 0.4), new THREE.Vector3(1, 0.2, 0.1));
      if (owner) {
        for (let i = 0; i < 2; i++) {
          const p = owner.bonePosition?.(randInt(41));
          if (!p) continue;
          p.x += randInt(30) - 15;
          p.y += randInt(30) - 15;
          p.z += randInt(30) - 15;
          system.createParticle('lightningMega', p, o.angle, new THREE.Vector3(1, 0.5, 0.4), 0, (randInt(60) + 22) * 0.01);
        }
      }
      if (o.position.z < system.heightAt(o.position.x, o.position.y)) {
        system.createEffect('lightningShock', o.position, o.angle, o.light, 1, o.self);
        o.live = false;
        return false;
      }
      return true;
    }
    megaAround(new THREE.Vector3(1, 0.7, 0.4), null);
    const red = new THREE.Vector3(1, 0, 0);
    for (let i = 0; i < 6; i++) {
      const p = rotateByAngle(new THREE.Vector3(0, randInt(400), 0), new THREE.Vector3(0, 0, o.angle.z + randInt(360))).add(o.position);
      p.z = system.heightAt(p.x, p.y) + 20;
      system.createParticle('lightningMega', p, o.angle, red, 0, (randInt(60) + 22) * 0.01);
    }
    // Red smoke from the ground, a rising column now and then, and a stone thrown up every tick.
    const ground = o.position.clone();
    ground.z = system.heightAt(ground.x, ground.y) + 10;
    for (let i = 0; i < 2; i++) system.createParticle('smoke', ground, o.angle, red, 58);
    if (randInt(2) === 0) system.createParticle('smoke', ground, o.angle, red, 54, 2.8);
    system.createEffect(randInt(2) ? 'stone2' : 'stone1', ground, o.angle, red, 13, o.self);
    return true;
  },
};

// BITMAP_LIGHTNING_MEGA1..3 SubType 0: a spark of lightning that fades in five ticks.
EXTRA_PARTICLES.lightningMega = {
  texture: 'lightningMega1',
  init: (o) => {
    if (o.subType !== 0) return false;
    o.texture = (['lightningMega1', 'lightningMega2', 'lightningMega3'] as const)[randInt(3)];
    o.lifeTime = 5;
    o.rotation = randInt(360);
    o.gravity = 0;
    o.alpha = 1;
    o.turningForce.copy(o.light);
    o.light.set(0, 0, 0);
    return true;
  },
  move: (o) => {
    o.alpha -= 0.15;
    if (o.alpha < 0.1) o.live = false;
    o.light.copy(o.turningForce).multiplyScalar(o.alpha);
  },
};

// BITMAP_MAGIC SubType 0 as a particle: a circle that shrinks and darkens.
EXTRA_PARTICLES.magicParticle = {
  texture: 'magicGround1',
  init: (o, scale) => {
    if (o.subType !== 0) return false;
    o.lifeTime = 10;
    o.scale = scale;
    return true;
  },
  move: (o) => {
    o.scale -= 0.05;
    o.light.subScalar(0.01);
  },
};

// BITMAP_MAGIC on the ground: SubType 12 (Lightning Shock's five circles) spreads as it fades;
// 13 and 14 (the Rage Fighter's buffs, Dark Side) grow and fade.
EXTRA_EFFECTS.magic = {
  create: (o) => {
    o.lifeTime = 20;
    o.scale = 0.5;
    if (o.subType === 12) {
      o.scale = o.callScale * 0.1;
      return true;
    }
    if (o.subType === 13 || o.subType === 14) {
      o.lifeTime = 30;
      o.scale = 1;
      return true;
    }
    return false;
  },
  move: (o) => {
    if (o.subType === 12) {
      o.alpha = Math.max(0, o.alpha - 0.05);
      o.scale += o.alpha;
      if (o.scale > 5) o.light.multiplyScalar(0.5);
    } else {
      o.scale *= 1.1;
      o.light.multiplyScalar(0.95);
      if (o.scale > 8) o.live = false;
      if (o.scale > 4) o.light.multiplyScalar(0.5);
    }
    return true;
  },
  draw: (o, system) => {
    const height = (x: number, y: number): number => system.heightAt(x, y);
    // Twice, as the desktop does.
    for (let i = 0; i < 2; i++) system.quads.decal('magicGround1', 'glow', o.position.x, o.position.y, o.scale, o.light, -o.angle.z, height, o.position.z);
  },
};

// BITMAP_DAMAGE_01_MONO on the ground: SubType 1 (Lightning Shock's burst) grows, then fades.
EXTRA_EFFECTS.damage01Mono = {
  create: (o) => {
    if (o.subType === 0) o.scale = o.callScale;
    return o.subType <= 1;
  },
  move: (o) => {
    if (o.subType === 0) o.scale += 5;
    else {
      o.scale += 0.5;
      if (o.scale > 3.5) o.light.multiplyScalar(0.5);
    }
    return true;
  },
  draw: (o, system) => {
    system.quads.decal('damage01Mono', 'glow', o.position.x, o.position.y, o.scale, o.light, -o.angle.z, (x, y) => system.heightAt(x, y), o.position.z);
  },
};

/** The effects a Summoner's curse makes at the caster (MoveCharacter's release, AT_SKILL_ALICE_WEAKNESS / _ENERVATION). */
export function summonerCurse(system: EffectSystem, at: THREE.Vector3, angle: THREE.Vector3, weakness: boolean): void {
  const light = weakness ? new THREE.Vector3(2, 0.1, 0.1) : new THREE.Vector3(0.25, 1, 0.7);
  system.createEffect('magicZin', at, angle, light, 1, null, 7);
  if (weakness) light.set(2, 0.4, 0.3);
  system.createEffect('magicZin', at, angle, light, 0, null, 2);
  for (const scale of [1, 0.2, 0.1]) system.createEffect('magicZin', at, angle, light, 2, null, scale);
  system.createEffect('summonerCastingEffect2', at, angle, light, 1, null, 0.6);
  system.createEffect('summonerCastingEffect22', at, angle, light, 1, null, 0.6);
  system.createEffect('summonerCastingEffect222', at, angle, light, 1, null, 0.6);
  if (weakness) light.set(1.4, 0.2, 0.2);
  system.createEffect('shiny5', at, angle, light, 0, null, 0.5);
  system.createEffect('pinLight', at, angle, light, 0, null, 1);
}


// ---------------------------------------------------------------------------
// Lance (AT_SKILL_JAVELIN): three lances (MODEL_SKILL_JAVELIN SubTypes 0-2) spin out over the caster, each turned
// aside by up to 90 degrees, then home in on the target 150 over its feet, dropping pounding balls.
// Followed: MoveCharacter's release AT_SKILL_JAVELIN (ZzzCharacter.cpp); CreateEffect MODEL_SKILL_JAVELIN (the owner
// is the target; MoveEffect leaves its move to it) (ZzzEffect.cpp); Move_MODEL_SKILL_JAVELIN (MoveHandlers.cpp);
// RenderObject (body light 1 0.6 0.3, then a bright chrome pass) (ZzzObject.cpp); BITMAP_POUNDING_BALL SubType 1
// (ZzzEffectParticle.cpp).
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.skillJavelin = {
  model: ['Skill', 'Javelin.bmd'],
  create: (o) => {
    const target = o.owner;
    if (!target) return false;
    o.lifeTime = 35;
    o.gravity = 2;
    o.velocity = 10;
    o.scale = 1.2;
    o.direction.set(0, -5, 0);
    o.headAngle.copy(o.angle);
    o.startPosition.copy(target.position);
    o.position.z += 150;
    const turn = randInt(80) + 10;
    o.headAngle.z += o.subType * turn - turn;
    o.light.set(1, 0.6, 0.3); // its body light
    return true;
  },
  move: (o, system) => {
    if (o.owner) o.startPosition.copy(o.owner.position).setZ(o.owner.position.z + 150);
    o.scale += 0.015;
    if (o.lifeTime < 25) {
      if (o.lifeTime < 20) {
        const distance = moveHumming(o.position, o.headAngle, o.startPosition, o.velocity);
        if (distance < 100) {
          o.position.copy(o.startPosition);
          o.scale += 0.04;
          if (Math.trunc(o.lifeTime) % 3 === 0) system.createParticle('poundingBall', o.position, o.angle, o.light, 1);
        } else {
          system.createParticle('poundingBall', o.position, o.angle, o.light, 1);
        }
      }
      o.velocity += 1.5;
      o.position.add(rotateByAngle(o.direction, o.headAngle));
      o.gravity = randInt(30) + 30;
      if (o.direction.y > -50) o.direction.y -= 8;
    } else {
      o.gravity += 10;
      o.position.add(rotateByAngle(o.direction, o.headAngle));
    }
    o.angle.z += o.gravity;
    o.blendMeshLight = o.lifeTime / 10;
    o.alpha = o.blendMeshLight;
    return false; // MoveEffect has no shared move for it
  },
};

// BITMAP_POUNDING_BALL SubType 1: a grey ball drifting off (its move case never runs: the desktop tests SubType 0
// and 1 at once).
EXTRA_PARTICLES.poundingBall = {
  texture: 'poundingBall',
  init: (o) => {
    if (o.subType !== 1) return false;
    o.lifeTime = 15;
    o.velocity.set(0, -(randInt(16) + 32) * 0.1, 0);
    o.scale = (randInt(128) + 128) * 0.01;
    o.rotation = randInt(360);
    o.light.set(0.5, 0.5, 0.5);
    return true;
  },
  move: () => undefined,
};

/** Lance's release: three lances from the caster at the target. */
export function lance(system: EffectSystem, caster: EffectOwner, target: EffectOwner): void {
  for (let sub = 0; sub < 3; sub++) system.createEffect('skillJavelin', caster.position, caster.angle, caster.light ?? WHITE, sub, target);
}
