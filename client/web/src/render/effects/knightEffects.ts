// The Dark Knight's skill effects (registered in extraEffects.ts's tables).
//
// Followed C++ (client/src/source/Render/Effects):
//   CreateEffect: MODEL_SKILL_WHEEL1/2, MODEL_SKILL_FURY_STRIKE (+1..+8), MODEL_COMBO,
//   MODEL_SPEARSKILL; RenderWheelWeapon, RenderFuryStrike, RenderSkillSpear;
//   MoveEffect's MODEL_SKILL_FURY_STRIKE+1..+8 cases                     ZzzEffect.cpp
//   Move_MODEL_SKILL_WHEEL1/2, _SKILL_FURY_STRIKE, _WAVE, _TAIL, _COMBO   Behaviors/MoveHandlers.cpp
//   MoveSpear (MODEL_SPEAR)                                              Behaviors/EffectBehaviors.cpp
//   CreateJoint / MoveJoint: BITMAP_FLARE (2, 4, 12), BITMAP_JOINT_SPARK (0), BITMAP_LIGHT (0)
//                                                                        ZzzEffectJoint.cpp
//   EffectTypes.json: MODEL_WAVE, MODEL_TAIL, MODEL_SPEAR (their creation values)

import * as THREE from 'three';
import { angleEuler, randInt, rotateByAngle } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS } from './extraEffects.js';
import type { Effect, EffectType } from './types.js';

const WHITE = new THREE.Vector3(1, 1, 1);
const ITEM_GROUP_SPEAR = 3;

/** A see-through copy of the owner's weapon, drawn by the effect (RenderPartObject of o->Owner->Weapon). */
function weaponCopy(o: Effect, system: EffectSystem): void {
  const weapon = o.owner?.weapon?.();
  if (!weapon) return;
  const copy = weapon.model.clone(true);
  copy.traverse((node) => {
    if (node instanceof THREE.Mesh) {
      const material = (node.material as THREE.Material).clone();
      material.transparent = true;
      node.material = material;
    }
  });
  copy.name = 'effect-weapon';
  copy.matrixAutoUpdate = true;
  copy.visible = false;
  o.extra = copy;
  o.counter = weapon.group;
  system.root.add(copy);
}

function placeWeapon(o: Effect, position: THREE.Vector3, angle: THREE.Vector3, visible: boolean): void {
  const copy = o.extra;
  if (!copy) return;
  copy.visible = visible;
  copy.position.copy(position);
  angleEuler(angle, copy.rotation);
  copy.traverse((node) => {
    if (node instanceof THREE.Mesh) (node.material as THREE.Material).opacity = Math.max(0, Math.min(1, o.alpha));
  });
}

// MODEL_SKILL_WHEEL1 (Twisting Slash): five ticks, each leaving a spinning copy of the weapon.
EXTRA_EFFECTS.skillWheel1 = {
  create: (o) => {
    o.lifeTime = 5;
    return true;
  },
  move: (o, system) => {
    system.createEffect('skillWheel2', o.position, o.angle, o.light, 4 - o.lifeTime, o.owner);
    return false; // no animation, no MoveParticle (the wheels are skipped in the shared tail)
  },
};

// MODEL_SKILL_WHEEL2: a weapon copy circling the knight at 150 (180 for a spear), the later ones
// fainter, throwing sparks; the copy itself spins faster and faster.
EXTRA_EFFECTS.skillWheel2 = {
  create: (o, system) => {
    o.lifeTime = 25;
    weaponCopy(o, system);
    return o.owner !== null;
  },
  move: (o, system, luminosity) => {
    const owner = o.owner;
    if (!owner) return false;
    if (o.subType === 1) o.alpha = 0.6;
    else if (o.subType === 2) o.alpha = 0.5;
    else if (o.subType === 3) o.alpha = 0.4;
    else if (o.subType === 4) o.alpha = 0.3;
    const p = new THREE.Vector3(0, o.counter === ITEM_GROUP_SPEAR ? -180 : -150, 0);
    o.position.copy(rotateByAngle(p, o.angle).add(owner.position));
    o.angle.z -= 18;
    system.createParticle('smoke', o.position, o.angle, o.light, 3);
    system.groundLight(o.position, luminosity * 0.3, luminosity * 0.3, luminosity * 0.3, 3);
    for (let j = 0; j < 4; j++) {
      const angle = new THREE.Vector3(randInt(60) + 60 - 90, 0, randInt(30) + 90).add(o.angle);
      const position = o.position.clone();
      position.x += randInt(20) - 10;
      position.y += randInt(20) - 10;
      system.createJoint('jointSpark', position, position, angle);
      if (randInt(4) === 0) system.createParticle('spark', position, angle, WHITE);
    }
    system.createSprite('light', o.position.clone().setZ(o.position.z + 20), 2, new THREE.Vector3(1, 0.8, 0.6), o.self);
    o.direction.z -= 30; // UpdateDrawnEffectSprites: the wheel spins faster each frame
    return false;
  },
  draw: (o) => {
    // RenderWheelWeapon: Angle[2] + Direction[2], Angle[1] = 90, 100 over the ground.
    const angle = new THREE.Vector3(o.angle.x, 90, o.angle.z + o.direction.z);
    placeWeapon(o, o.position.clone().setZ(o.position.z + 100), angle, true);
  },
};

// MODEL_SKILL_FURY_STRIKE (Rageful Blow): the weapon swings up and smashes down in front of the
// knight; at LifeTime 11 the ground breaks (explosion, sparks, a flash, the big crack models and
// five smaller ones), at 10 five cracks run out in zigzags.
EXTRA_EFFECTS.skillFuryStrike = {
  create: (o, system) => {
    o.headAngle.copy(o.angle);
    o.startPosition.copy(o.position);
    o.lifeTime = 20;
    o.subType = randInt(100);
    o.angle.z += 330;
    o.headAngle.x += 80;
    o.headAngle.z += 180;
    o.gravity = 50;
    weaponCopy(o, system);
    return true;
  },
  move: (o, system) => {
    const life = Math.trunc(o.lifeTime);
    const owner = o.owner;
    if (life === 11) {
      if (o.kind !== 3 && owner) {
        o.startPosition.copy(rotateByAngle(new THREE.Vector3(-25, -80, 0), owner.angle).add(o.position));
      }
      o.startPosition.z = system.heightAt(o.startPosition.x, o.startPosition.y) + 25;
      const zero = new THREE.Vector3();
      system.createParticle('explosion', o.startPosition, zero, WHITE, 0, 0.5);
      if (o.kind === 0) {
        for (let j = 0; j < 8; j++) {
          const angle = new THREE.Vector3(randInt(60) - 60, 0, randInt(30) + 90).add(o.angle);
          const position = o.startPosition.clone();
          position.x += randInt(20) - 10;
          position.y += randInt(20) - 10;
          system.createJoint('jointSpark', position, position, angle);
          if (randInt(8) === 0) system.createParticle('spark', position, angle, WHITE);
        }
        system.createEffect('wave', o.startPosition, zero, o.light);
      }
      o.startPosition.z -= 27;
      if (owner) {
        system.createEffect('earthQuake3', o.startPosition, zero, o.light, 0, o.self, 0, { pkKey: 150 });
        system.createEffect('earthQuake1', o.startPosition, zero, o.light, 0, o.self, 0, { pkKey: 150 });
        system.createEffect('earthQuake2', o.startPosition, zero, o.light, 0, o.self, 0, { pkKey: 150 });
      }
      for (let i = 0; i < 5; i++) {
        const angle = new THREE.Vector3(0, 0, o.subType + i * 72);
        const position = rotateByAngle(new THREE.Vector3(0, randInt(150) + 100, 0), angle).add(o.startPosition);
        const scale = randInt(50) + 40;
        const crackAngle = new THREE.Vector3(0, 0, 45 + randInt(30) - 15);
        position.z = system.heightAt(position.x, position.y) + 3;
        system.createEffect('earthQuake4', position, crackAngle, o.light, 0, owner, 0, { pkKey: scale });
        system.createEffect('earthQuake5', position, crackAngle, o.light, 0, owner, 0, { pkKey: scale });
      }
    } else if (life === 10) {
      const positions = Array.from({ length: 5 }, () => o.startPosition.clone());
      const angles = [0, 0, 0, 0, 0];
      let count = 0;
      const angle = new THREE.Vector3();
      for (let j = 0; j < 4; j++) {
        const p = new THREE.Vector3(0, randInt(15) + 85, 0);
        if (j >= 3) count = randInt(1 << 30);
        for (let i = 0; i < 5; i++) {
          const random = count % 2 === 0 ? randInt(30) + 50 : -(randInt(30) + 50);
          angles[i] += random;
          angle.z = angles[i] + i * (randInt(10) + 62);
          positions[i].add(rotateByAngle(p, angle));
          positions[i].z = system.heightAt(positions[i].x, positions[i].y) + 3;
          angle.z += 270;
          system.createEffect('earthQuake7', positions[i], angle, o.light, 0, owner, 0, { pkKey: 100 });
          system.createEffect('earthQuake8', positions[i], angle, o.light, 0, owner, 0, { pkKey: 100 });
        }
        count++;
      }
      o.lifeTime = 0;
    } else {
      let count = o.lifeTime;
      let addAngle = 15;
      if (o.lifeTime > 9 && o.lifeTime < 16) {
        count = 12.5;
        addAngle = 18;
        if (life === 15) o.gravity *= -1;
      }
      if (o.kind === 0 && life === 13 && owner) {
        const pos = rotateByAngle(new THREE.Vector3(-25, -40, 0), owner.angle).add(o.position);
        const zero = new THREE.Vector3();
        for (let i = 0; i < 4; i++) system.createEffect('tail', new THREE.Vector3(pos.x, pos.y, pos.z - i * 50), zero, o.light);
        pos.x += randInt(30) + 20;
        pos.z += randInt(500) - 250;
        for (let i = 0; i < 4; i++) system.createEffect('tail', new THREE.Vector3(pos.x, pos.y, pos.z - i * 30), zero, o.light);
      }
      const angle = (20 - count) * addAngle;
      o.angle.x += 80;
      o.direction.y = Math.sin(angle * Math.PI / 180) * 260;
      if (count !== 12.5) o.gravity += 8;
      else o.gravity -= 8;
      o.position.copy(rotateByAngle(o.direction, o.headAngle).add(o.startPosition));
      o.position.z += o.gravity + 200;
    }
    return false; // skipped in the shared tail
  },
  draw: (o) => {
    // RenderFuryStrike: the weapon while LifeTime > 10.
    placeWeapon(o, o.position, o.angle, o.lifeTime > 10 && o.kind === 0);
  },
};

/** The MODEL_SKILL_FURY_STRIKE+N cracks: lifetime, blend mesh, and their glow over time. */
interface Crack { life: number; blend: boolean; glow: (o: Effect) => void; sinkBelow: number; stones?: number }

const CRACKS: Record<number, Crack> = {
  1: { life: 35, blend: true, glow: (o) => { o.blendMeshLight = o.lifeTime * 0.1 / 3; }, sinkBelow: 10 },
  2: { life: 20, blend: true, glow: (o) => { o.blendMeshLight = o.lifeTime >= 10 ? (20 - o.lifeTime) * 0.1 : o.lifeTime * 0.1; }, sinkBelow: 5, stones: 10 },
  3: { life: 35, blend: false, glow: (o) => { o.blendMeshLight = o.lifeTime * 0.1 / 10; }, sinkBelow: 13 },
  4: { life: 35, blend: true, glow: (o) => { o.blendMeshLight = o.lifeTime * 0.1 / 3; }, sinkBelow: 10 },
  5: { life: 40, blend: true, glow: (o) => { o.blendMeshLight = o.lifeTime >= 30 ? (40 - o.lifeTime) * 0.1 : o.lifeTime * 0.1; }, sinkBelow: 15, stones: 15 },
  6: { life: 35, blend: false, glow: (o) => { o.blendMeshLight = o.lifeTime * 0.1 / 10; }, sinkBelow: 13 },
  7: { life: 40, blend: true, glow: (o) => { o.blendMeshLight = o.lifeTime * 0.1 / 3; }, sinkBelow: 10 },
  8: { life: 40, blend: true, glow: (o) => { o.blendMeshLight = o.lifeTime >= 30 ? (40 - o.lifeTime) * 0.1 : o.lifeTime * 0.1; }, sinkBelow: 15 },
};

for (let n = 1; n <= 8; n++) {
  const crack = CRACKS[n];
  EXTRA_EFFECTS[`earthQuake${n}` as EffectType] = {
    model: ['Skill', `EarthQuake0${n}.bmd`],
    create: (o) => {
      if (o.subType !== 0) return false;
      o.lifeTime = crack.life;
      o.scale = o.pkKey / 100;
      if (crack.blend) o.blendMesh = 0;
      return true;
    },
    move: (o, system, luminosity) => {
      crack.glow(o);
      if (n === 2 || n === 5 || n === 8) system.groundLight(o.position, luminosity, 0, 0, 1); // the red glow of the cracks
      // The glowing cracks (2, 5) send up stones while they glow; +2 ends at once if it grew too big.
      if (n === 2 && o.scale > 50) o.lifeTime = 0;
      if ((n === 2 || n === 5) && o.lifeTime < (n === 2 ? 10 : 30) && o.lifeTime >= 5 && crack.stones && randInt(crack.stones) === 0) {
        const angle = new THREE.Vector3(0, 0, randInt(360));
        const position = rotateByAngle(new THREE.Vector3(0, randInt(150), 0), angle).add(o.position);
        system.createEffect(randInt(2) ? 'stone2' : 'stone1', position, o.angle, o.light, 0);
      }
      if (n === 2 || n === 5) o.blendMeshTexCoordU = -Math.trunc(o.lifeTime) * 0.01;
      if (o.lifeTime < crack.sinkBelow) o.position.z -= 0.5;
      return true;
    },
  };
}

// MODEL_WAVE (the catalogue gives LifeTime 15, Scale 0.5, BlendMesh 0, BlendMeshLight 1.5, white, z - 15):
// the flash grows fast, then slowly, and fades.
EXTRA_EFFECTS.wave = {
  model: ['Skill', 'flashing.bmd'],
  create: () => true,
  move: (o, system, luminosity) => {
    system.groundLight(o.position, -luminosity * 0.5, -luminosity * 0.5, -luminosity * 0.5, 5); // the flash darkens the ground
    if (o.scale > 2) {
      o.scale += 0.1;
      o.position.x -= 1;
      o.position.z -= 1.5;
      o.blendMeshLight = o.lifeTime / 30;
    } else {
      o.scale += 1.2;
      o.position.x -= 1.2;
      o.position.z -= 1.8;
    }
    return true;
  },
};

// MODEL_TAIL (the catalogue gives LifeTime 6, Gravity 80, BlendMesh -2, grey, turned 45): a streak falling fast.
EXTRA_EFFECTS.tail = {
  model: ['Skill', 'tail.bmd'],
  create: () => true,
  move: (o) => {
    o.position.z -= o.gravity;
    o.gravity += 60;
    o.blendMeshLight = o.lifeTime / 20;
    return true;
  },
};

// MODEL_SPEAR (the catalogue gives LifeTime 10): every tick a spiralling flare leaves it, Death
// Stab's (SubType 1) big and blue, Impale's (0) white.
EXTRA_EFFECTS.lightSpear = {
  create: (o) => o.subType === 0 || o.subType === 1,
  move: (o, system) => {
    if (o.subType === 1) system.createJoint('flare', o.position, o.position, o.angle, 12, o.self, 100);
    else system.createJoint('flare', o.position, o.position, o.angle, 4, o.self, 50);
    return true;
  },
};

// MODEL_SPEARSKILL as an effect (Impale's thrusts): a spear that slides forward, shining as it fades
// (RenderSkillSpear: bright, light times LifeTime * 0.05).
EXTRA_EFFECTS.spearSkillModel = {
  model: ['Skill', 'RidingSpear01.bmd'],
  create: (o) => {
    o.lifeTime = 20;
    o.scale = 1.5;
    o.blendMesh = -2;
    o.blendMeshLight = 1;
    o.direction.set(5 * Math.sin(o.angle.z * Math.PI / 180), -5 * Math.cos(o.angle.z * Math.PI / 180), 0);
    return true;
  },
  move: (o) => {
    o.blendMeshLight = o.lifeTime * 0.05;
    o.position.add(o.direction); // MoveParticle(o, false)
    return false;
  },
};

// MODEL_COMBO: a burst that swells and fades, with sixty light streaks flying out.
EXTRA_EFFECTS.combo = {
  model: ['Skill', 'combo.bmd'],
  create: (o, system) => {
    o.lifeTime = 20;
    o.gravity = 0.1;
    o.blendMesh = -2;
    o.blendMeshLight = 1;
    o.angle.set(0, 0, 0);
    o.startPosition.copy(o.position);
    o.position.z += 50;
    if (o.subType === 0) for (let j = 0; j < 60; j++) system.createJoint('lightJoint', o.position, o.position, o.angle, 0, null, randInt(40) + 70);
    return true;
  },
  move: (o) => {
    if (o.subType === 0) {
      if (o.lifeTime > 4) {
        o.scale += o.gravity;
        o.gravity += 0.1;
      }
      o.blendMeshLight *= 1 / 1.4;
    }
    return true;
  },
};

// BITMAP_FLARE joints: SubType 2 (Swell Life's rising pillars), 4 and 12 (the spiralling spear flares).
EXTRA_JOINTS.flare = {
  init: (o) => {
    o.lifeTime = 100;
    o.maxTails = 20;
    if (o.subType === 2) {
      o.direction.z = randInt(20) + 35;
      o.lifeTime = 25 + randInt(50);
      o.light.set(1, 1, 1);
      return true;
    }
    if (o.subType === 4 || o.subType === 12) {
      if (o.subType === 12) {
        o.lifeTime = 70;
        o.maxTails = 50;
        o.light.set(0.1, 0.1, 1);
      } else {
        o.lifeTime = 110;
        o.maxTails = 200;
        if (o.target?.light) o.light.copy(o.target.light);
      }
      const speed = o.subType === 12 ? -4 : -2;
      o.direction.set(randInt(360), speed * Math.sin(-o.angle.z * Math.PI / 180), speed * Math.cos(-o.angle.z * Math.PI / 180));
      return true;
    }
    return false;
  },
  move: (o) => {
    if (o.subType === 2) {
      if (o.lifeTime <= 25) {
        o.direction.z += 5;
        o.position.x = o.targetPosition.x;
        o.position.y = o.targetPosition.y;
        o.position.z += o.direction.z;
      }
      return;
    }
    let vx: number, vy: number;
    const count = (o.direction.x + o.lifeTime) * 0.1;
    if (o.subType === 12) {
      vx = -Math.cos(count) * 40 * 0.65;
      vy = Math.sin(count) * 40 * 0.65 - (90 - o.lifeTime) * 0.3;
    } else {
      vx = -Math.cos(count) * Math.max(o.lifeTime + 40, 10) * 0.65;
      vy = Math.sin(count) * Math.max(o.lifeTime + 40, 10) * 0.65;
    }
    o.targetPosition.x += o.direction.y;
    o.targetPosition.y += o.direction.z;
    const turn = (90 - o.angle.z) * Math.PI / 180;
    o.position.set(vx * Math.sin(turn) + o.targetPosition.x, vx * Math.cos(turn) + o.targetPosition.y, vy + o.targetPosition.z);
  },
};

// BITMAP_JOINT_SPARK SubType 0: a short white spark streak.
EXTRA_JOINTS.jointSpark = {
  init: (o) => {
    if (o.subType !== 0) return false;
    o.scale = 2;
    o.velocity = randInt(20) + 6;
    o.lifeTime = randInt(8) + 8;
    o.maxTails = 2;
    o.light.set(1, 1, 1);
    return true;
  },
  move: () => { /* it flies along its angle (the joints' common move) */ },
};

// BITMAP_LIGHT joint SubType 0 (the combo's streaks): ten tails a tick while young, fading fast.
EXTRA_JOINTS.lightJoint = {
  init: (o) => {
    if (o.subType !== 0) return false;
    o.createTails = false;
    o.lifeTime = 20;
    o.maxTails = 30;
    o.velocity = randInt(10) + 10;
    o.angle.set(-randInt(40) + 30, 0, randInt(360));
    o.direction.set(0, 0, 0);
    o.light.set(0.1, 0.5, 1);
    o.startPosition.copy(o.position);
    return true;
  },
  move: (o, system) => {
    if (o.lifeTime > 16) {
      for (let i = 0; i < 10; i++) {
        system.layTail(o, o.angle);
        o.position.add(rotateByAngle(new THREE.Vector3(0, -o.velocity, 0), o.angle));
      }
    }
    o.light.multiplyScalar(1 / 1.2);
  },
};

/** The upper body's bones the Swell Life buff shines from (g_byUpperBoneLocation). */
const UPPER_BONES = [25, 26, 27, 20, 34, 35, 36];

// BITMAP_LIGHT SubType 1: the eBuff_Life look (Swell Life): two rising orange lights from the
// upper body's bones every tick while the buff lasts (light particles SubType 4).
EXTRA_EFFECTS.buffLight = {
  create: (o) => {
    if (o.subType !== 1) return false;
    o.lifeTime = 1000;
    o.velocity = 0;
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    o.lifeTime = owner && (owner.isLive?.() ?? true) && (owner.hasBuff?.(8) ?? false) ? 10 : 0;
    if (!owner || o.lifeTime === 0) return false;
    const light = new THREE.Vector3(1, 0.5, 0.1);
    const index = randInt(7);
    system.createParticle('light', o.position, new THREE.Vector3(), light, 4, UPPER_BONES[index], owner);
    system.createParticle('light', o.position, new THREE.Vector3(), light, 4, UPPER_BONES[6 - index], owner);
    return false;
  },
};
