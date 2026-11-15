// The Berserker buff's look (eBuff_Berserker): turning auras and sparks at both hands and red marks on the body
// (registered in extraEffects.ts's tables).
//
// Followed C++ (client/src/source):
//   InsertBuffPhysicalEffect / ClearBuffPhysicalEffect eBuff_Berserker          Network/Server/WSclient.cpp
//   CreateEffect BITMAP_ORORA, Move_BITMAP_ORORA; BITMAP_SPARK+2 and BITMAP_LIGHT_MARKS in the effects' update
//                                                                              Render/Effects/ZzzEffect.cpp, MoveHandlers.cpp
//   CreateParticle / MoveParticles / DescribeParticle BITMAP_ORORA, BITMAP_SPARK+2  Render/Effects/ZzzEffectParticle.cpp

import * as THREE from 'three';
import { randInt } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_PARTICLES } from './extraEffects.js';
import type { EffectOwner } from './types.js';

const LEFT_HAND = 37, RIGHT_HAND = 28;
const live = (owner: EffectOwner | null): owner is EffectOwner => owner !== null && (owner.isLive?.() ?? true);

// BITMAP_ORORA: 100 ticks (0, 1) or 25 (2, 3); it makes its aura particle and is made again just before it ends.
EXTRA_EFFECTS.orora = {
  create: (o, system) => {
    if (o.subType < 0 || o.subType > 3 || !o.owner) return false;
    o.lifeTime = o.subType <= 1 ? 100 : 25;
    system.createParticle('orora', o.position, o.angle, o.light, o.subType, 1, o.owner);
    return true;
  },
  move: (o, system) => {
    if (!live(o.owner)) o.live = false;
    else if (o.lifeTime <= 5) {
      o.live = false;
      system.createEffect('orora', o.position, o.angle, o.light, o.subType, o.owner);
    }
    return true;
  },
};

// The aura: on the left (0, 2) or right (1, 3) hand, turning, growing; once large it fades.
EXTRA_PARTICLES.orora = {
  texture: 'orora',
  init: (o) => {
    if (!o.target) return false;
    o.scale = 0.2;
    o.lifeTime = o.subType <= 1 ? 100 : 25;
    return true;
  },
  move: (o) => {
    const target = o.target;
    if (!target) return;
    const big = o.subType >= 2;
    o.rotation += (o.subType % 2 === 0 ? 1 : -1) * (big ? 20 : 5);
    const p = target.bonePosition?.(o.subType % 2 === 0 ? LEFT_HAND : RIGHT_HAND);
    if (p) o.position.copy(p);
    o.scale += big ? 0.04 : 0.01;
    if (o.scale >= 0.8) o.light.multiplyScalar(1 / (big ? 1.33 : 1.05));
  },
  draw: (o, system, width, height, blend) => {
    system.quads.billboard(o.texture, blend, o.position, width, height, o.light, o.rotation);
  },
};

// BITMAP_SPARK+2 (2, 3): lasts while its owner does; one tick in sixty it bursts in sparks at a hand.
EXTRA_EFFECTS.sparks = {
  create: (o) => (o.subType === 2 || o.subType === 3) && o.owner !== null,
  move: (o, system) => {
    if (!live(o.owner)) {
      o.live = false;
      return true;
    }
    o.lifeTime = 100;
    if (randInt(60) === 0) system.createParticle('sparks', o.position, o.angle, o.light, o.subType, 0.5, o.owner);
    return true;
  },
};

// The burst: four frames of a 2x2 sheet on the hand.
EXTRA_PARTICLES.sparks = {
  texture: 'sparkBurst',
  init: (o) => {
    if ((o.subType !== 2 && o.subType !== 3) || !o.target) return false;
    o.scale = 0.4;
    o.lifeTime = 16;
    return true;
  },
  move: (o) => {
    o.frame = Math.trunc((16 - o.lifeTime) / 4);
    const p = o.target?.bonePosition?.(o.subType === 2 ? LEFT_HAND : RIGHT_HAND);
    if (p) o.position.copy(p);
  },
  draw: (o, system, width, height, blend) => {
    system.quads.billboard(o.texture, blend, o.position, width, height, o.light, 0, (o.frame % 2) * 0.5, Math.trunc(o.frame / 2) * 0.5, 0.5, 0.5);
  },
};

// BITMAP_LIGHT_MARKS (the catalogue gives 65 ticks): marks on fourteen bones that dim for 30 ticks, then brighten.
const MARK_BONES = [20, 20, 19, 18, 17, 2, 35, 26, 36, 27, 37, 28, 39, 30];
const MARK_SCALES = [1.5, 1.5, 0.6, 1.1, 0.9, 0.8, 0.6, 0.6, 0.8, 0.8, 0.8, 0.8, 0.7, 0.7];
EXTRA_EFFECTS.lightMarks = {
  create: (o) => o.owner !== null,
  move: (o) => {
    if (!live(o.owner)) o.live = false;
    o.light.multiplyScalar(o.lifeTime >= 35 ? 1 / 1.035 : 1.035);
    return true;
  },
  sprites: (o, system) => {
    const owner = o.owner;
    if (!owner) return;
    MARK_BONES.forEach((bone, i) => {
      const p = owner.bonePosition?.(bone);
      if (p) system.createSprite('lightMarks', p, o.scale * MARK_SCALES[i], o.light, owner);
    });
  },
};

/** InsertBuffPhysicalEffect (on) / ClearBuffPhysicalEffect (off) for eBuff_Berserker. */
export function berserkerLook(system: EffectSystem, owner: EffectOwner, on: boolean): void {
  const red = new THREE.Vector3(0.9, 0, 0.1);
  const white = new THREE.Vector3(1, 1, 1);
  for (let i = 0; i < 4; i++) {
    system.deleteEffect('orora', owner, i);
    if (on) system.createEffect('orora', owner.position, owner.angle, red, i, owner);
    if (i >= 2) {
      system.deleteEffect('sparks', owner, i);
      if (on) system.createEffect('sparks', owner.position, owner.angle, white, i, owner);
    }
  }
  system.deleteEffect('lightMarks', owner);
  if (on) system.createEffect('lightMarks', owner.position, owner.angle, red, 0, owner);
}
