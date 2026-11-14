// The Fairy Elf's skill effects (registered in extraEffects.ts's tables).
//
// Followed C++ (client/src/source):
//   CreateEffect: MODEL_INFINITY_ARROW, MODEL_MULTI_SHOT1..3, BITMAP_GATHERING     Render/Effects/ZzzEffect.cpp
//   Move_MODEL_INFINITY_ARROW, _INFINITY_ARROW1, _MULTI_SHOT1..3, Move_BITMAP_GATHERING
//                                                                                 Render/Effects/Behaviors/MoveHandlers.cpp
//   AttackElf AT_SKILL_MULTI_SHOT (the three rings in front of the elf)           GameLogic/Combat/ClassAttack.cpp
//   EffectTypes.json: MODEL_INFINITY_ARROW1..4 (their creation values)

import * as THREE from 'three';
import { randInt, rotateByAngle } from './core.js';
import { EXTRA_EFFECTS } from './extraEffects.js';
import type { EffectType } from './types.js';

const WHITE = new THREE.Vector3(1, 1, 1);
/** The bone Infinity Arrow's quiver light sits on (BoneTransform[29]). */
const INFINITY_BONE = 29;

// MODEL_INFINITY_ARROW: SubType 0 makes the look (SubType 1 on the back and the four rings);
// SubType 1 shines on the elf's back and fades.
EXTRA_EFFECTS.infinityArrow = {
  model: ['Skill', 'arrowsre01.bmd'],
  create: (o, system) => {
    const owner = o.owner;
    if (!owner) return false;
    if (o.subType === 0) {
      o.lifeTime = 40;
      o.hiddenMesh = -2; // only SubType 1 is drawn
      system.createEffect('infinityArrow', owner.position, o.angle, o.light, 1, owner);
      for (let n = 1; n <= 4; n++) system.createEffect(`infinityArrow${n}` as EffectType, owner.position, o.angle, o.light, 0, owner);
      return true;
    }
    if (o.subType === 1) {
      o.lifeTime = 40;
      o.scale = 1;
      o.light.set(1, 1, 1);
      o.direction.copy(o.light);
      o.hiddenMesh = -2; // RenderObject draws nothing of MODEL_INFINITY_ARROW itself
      return true;
    }
    return false;
  },
  move: (o) => {
    const owner = o.owner;
    if (!owner) return true;
    if (o.subType === 1) o.light.multiplyScalar(0.95);
    o.position.copy(owner.bonePosition?.(INFINITY_BONE) ?? owner.position);
    o.angle.copy(owner.angle);
    o.direction.set(0, 0, 0);
    return true;
  },
};

// MODEL_INFINITY_ARROW1..3 (the catalogue: LifeTime 40, white): rings around the elf, fading,
// drawn bright (RENDER_BRIGHT: every mesh added, in the effect's light).
for (let n = 1; n <= 3; n++) {
  EXTRA_EFFECTS[`infinityArrow${n}` as EffectType] = {
    model: ['Skill', `arrowsre0${n + 1}.bmd`],
    create: (o) => {
      o.blendMesh = -2;
      return true;
    },
    move: (o) => {
      o.light.multiplyScalar(o.subType === 0 ? 0.95 : 0.98);
      if (o.owner) o.position.copy(o.owner.position);
      o.direction.set(0, 0, 0);
      return true;
    },
  };
}

// MODEL_INFINITY_ARROW4 (the catalogue: LifeTime 15, orange): a flash on the elf's back that
// grows and fades (MoveInfinityArrow4), drawn bright.
EXTRA_EFFECTS.infinityArrow4 = {
  model: ['Skill', 'arrowsre05.bmd'],
  create: (o) => {
    o.blendMesh = -2;
    return true;
  },
  move: (o) => {
    const owner = o.owner;
    if (!owner) {
      o.lifeTime = 0;
      return true;
    }
    o.scale *= 1.2;
    o.light.multiplyScalar(0.9);
    o.position.copy(owner.bonePosition?.(INFINITY_BONE) ?? owner.position);
    o.angle.copy(owner.angle);
    o.direction.set(0, 0, 0);
    return true;
  },
};

// MODEL_MULTI_SHOT1..3: rings that grow out of the bow and fade (all meshes glowing).
const MULTI_SHOT: Record<number, { life: number; grow: number }> = { 1: { life: 16, grow: 0.2 }, 2: { life: 13, grow: 0.3 }, 3: { life: 12, grow: 0.25 } };
for (const n of [1, 2, 3]) {
  EXTRA_EFFECTS[`multiShot${n}` as EffectType] = {
    model: ['Effect', `multishot0${n}.bmd`],
    create: (o) => {
      o.lifeTime = MULTI_SHOT[n].life;
      o.blendMesh = -2;
      o.scale = 0;
      return true;
    },
    move: (o) => {
      o.scale += MULTI_SHOT[n].grow;
      o.blendMeshLight = o.lifeTime / 18;
      o.alpha = o.blendMeshLight;
      return true;
    },
  };
}

// BITMAP_GATHERING SubType 0 (Penetration's charge): light gathers 150 over the ground in front
// of the elf, sparks flying into it.
EXTRA_EFFECTS.gathering = {
  create: (o) => {
    o.lifeTime = 10;
    if (o.subType !== 0) return false;
    o.position.add(rotateByAngle(new THREE.Vector3(0, -100, 0), o.angle));
    o.position.z += 150;
    return true;
  },
  move: (o, system) => {
    for (let j = 0; j < 3; j++) {
      const angle = new THREE.Vector3(randInt(360), 0, randInt(360));
      const position = rotateByAngle(new THREE.Vector3(0, 120, 0), angle).add(o.position);
      system.createSprite('shiny2', o.position, (randInt(8) + 8) * 0.3, o.light, o.self, randInt(360));
      system.createParticle('spark3', position, angle, WHITE, 2, (randInt(50) + 10) / 100, o.self);
    }
    return false;
  },
};
