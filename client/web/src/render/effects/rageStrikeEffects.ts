// The Rage Fighter's Chain Drive and Dragon Roar (registered in extraEffects.ts's tables).
//
// Chain Drive: while the giant swing plays (PLAYER_SKILL_GIANTSWING) a trail follows the fist; at key 6 a shock wave
// rings out with wolf heads and the target is struck by bursts of hits; at key 11 a second wave, spinning waves, more
// heads and swelling rings at the target.
// Dragon Roar: while PLAYER_SKILL_DRAGONLORE plays the fists burn (lava trails), a red wave and a ring spread and a
// column of fire rises; a burning crack opens toward the target, and the first targets struck get a volcano each,
// throwing burning stones, and burn.
//
// Followed C++ (client/src/source):
//   CMonkSystem::RageCreateEffect (AT_SKILL_CHAIN_DRIVE, AT_SKILL_DRAGON_ROAR), SetLowerEffEct  GameLogic/Social/MonkSystem.cpp
//   ReceiveMagic (AT_SKILL_DRAGON_ROAR: MODEL_VOLCANO_OF_MONK, MODEL_TARGETMON_EFFECT)            Network/Server/WSclient.cpp
//   CreateEffect: MODEL_SHOCKWAVE01, MODEL_SHOCKWAVE02, BITMAP_DAMAGE1, MODEL_SHOCKWAVE_SPIN01, BITMAP_LIGHT_RED (3, 4),
//   MODEL_WOLF_HEAD_EFFECT2, BITMAP_LAVA, MODEL_DRAGON_LOWER_DUMMY, MODEL_VOLCANO_OF_MONK, MODEL_VOLCANO_STONE,
//   MODEL_BLOW_OF_DESTRUCTION (2); MoveEffect (BITMAP_SHINY+4); RenderEffects (BITMAP_LIGHT_RED 3, 4),
//   the extra animation step of MODEL_DRAGON_LOWER_DUMMY                                         Render/Effects/ZzzEffect.cpp
//   Move_BITMAP_SWORDEFF, _MODEL_SHOCKWAVE01, _SHOCKWAVE02, _BITMAP_DAMAGE1, _SHOCKWAVE_SPIN01, _WOLF_HEAD_EFFECT2,
//   _BITMAP_LAVA, _DRAGON_LOWER_DUMMY, _TARGETMON_EFFECT, _VOLCANO_OF_MONK, _VOLCANO_STONE, _BLOW_OF_DESTRUCTION (2),
//   _BITMAP_SWORD_EFFECT_MONO (its wave at tick 14), _BITMAP_LIGHT_RED                         Render/Effects/Behaviors/MoveHandlers.cpp
//   RenderObject (MODEL_WOLF_HEAD_EFFECT2, MODEL_SHOCKWAVE01 by SubType, _SHOCKWAVE02, _SHOCKWAVE_SPIN01,
//   _SHOCKWAVE_GROUND01 (1: Kwave2), MODEL_DRAGON_LOWER_DUMMY, MODEL_VOLCANO_OF_MONK, MODEL_VOLCANO_STONE)  Engine/Object/ZzzObject.cpp
//   CreateJoint / MoveJoint: BITMAP_SWORDEFF, BITMAP_LAVA                                       Render/Effects/ZzzEffectJoint.cpp
//   CreateParticle / MoveParticles: BITMAP_DAMAGE1, BITMAP_SHINY+4 (2), BITMAP_SPARK (12)        Render/Effects/ZzzEffectParticle.cpp

import * as THREE from 'three';
import { randInt, rotateByAngle } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS, EXTRA_PARTICLES } from './extraEffects.js';
import type { Effect, EffectOwner } from './types.js';

const WHITE = new THREE.Vector3(1, 1, 1);

/** PLAYER_SKILL_GIANTSWING (Chain Drive), PLAYER_SKILL_DRAGONLORE (Dragon Roar). */
export const StrikeAction = { GIANTSWING: 249, DRAGONLORE: 253 } as const;
/** The keys per tick the timings take when the caster's action isn't known (the actions' PlaySpeed). */
const FALLBACK_SPEED = 0.4;

function live(owner: EffectOwner | null): owner is EffectOwner {
  return owner !== null && (owner.isLive?.() ?? true);
}

function speedOf(owner: EffectOwner | null, action: number): number {
  return owner?.playSpeed?.(action) ?? FALLBACK_SPEED;
}

function bone(owner: EffectOwner, index: number): THREE.Vector3 {
  return owner.bonePosition?.(index) ?? owner.position.clone().setZ(owner.position.z + 120);
}

/** TransformByObjectBone with a Relative: a point given in a bone's own frame, in the world. */
function boneOffset(owner: EffectOwner, index: number, relative: THREE.Vector3): THREE.Vector3 {
  const matrix = owner.boneMatrix?.(index);
  if (!matrix) return rotateByAngle(relative, owner.angle).add(owner.position).setZ(owner.position.z + 120);
  return relative.clone().applyMatrix4(matrix);
}

/** RENDER_BRIGHT at `brightness` times the light: every mesh glows. */
function bright(o: Effect, brightness: number): void {
  o.blendMesh = -2;
  o.blendMeshLight = brightness;
}

/** RenderMesh(0, ...): only the first mesh is drawn. */
function firstMeshOnly(_o: Effect, meshIndex: number, _material: THREE.MeshBasicMaterial, mesh: THREE.Object3D): void {
  if (meshIndex !== 0) mesh.visible = false;
}

/** StartPosition = the way from the owner out to the effect on the ground, `length` long (the waves' drift). */
function driftFromOwner(o: Effect, system: EffectSystem, length: number): void {
  const owner = o.owner;
  if (!owner) return;
  const here = new THREE.Vector3(o.position.x, o.position.y, system.heightAt(o.position.x, o.position.y));
  const from = new THREE.Vector3(owner.position.x, owner.position.y, system.heightAt(owner.position.x, owner.position.y));
  o.startPosition.copy(here.sub(from).normalize().multiplyScalar(length));
}

// ---------------------------------------------------------------------------
// Chain Drive
// ---------------------------------------------------------------------------

// BITMAP_SWORDEFF (the catalogue gives LifeTime 200): lives while the giant swing plays.
EXTRA_EFFECTS.swordEffect = {
  create: (o) => o.owner !== null,
  move: (o, system) => {
    const owner = o.owner;
    // The desktop ends it when the swing's keys run out; the web's swing ends by playing another action.
    if (!live(owner) || owner.currentAction?.() !== StrikeAction.GIANTSWING) {
      o.live = false;
      return false;
    }
    const frame = owner.animationFrame?.() ?? 0;
    system.createJoint('swordEff', bone(owner, 5), bone(owner, 4), owner.angle, 0, owner, 100);
    const target = o.target;
    if (frame > 6 && o.lifeTime > 50) {
      const position = boneOffset(owner, 0, new THREE.Vector3(-180, 100, 0));
      const angle = new THREE.Vector3(60, 0, owner.angle.z - 180);
      system.createEffect('shockWave1', position, angle, new THREE.Vector3(0.4, 0.5, 1), 0, owner, 1);
      system.createEffect('wolfHeadEffect2', o.position, o.angle, o.light, 3, owner);
      if (!target) return true;
      system.createEffect('damage1', target.position, angle, new THREE.Vector3(0.4, 0.5, 1), 0, owner, 1.4, { target });
      o.lifeTime = 45;
    }
    if (frame > 11 && o.lifeTime > 20) {
      const position = boneOffset(owner, 4, new THREE.Vector3(-80, 120, 0));
      const angle = new THREE.Vector3(90, 0, owner.angle.z - 180);
      system.createEffect('shockWave2', position, angle, new THREE.Vector3(0.5, 0.5, 1), 0, owner, 0.5);
      system.createEffect('wolfHeadEffect2', o.position, o.angle, o.light, 5, owner);
      const spin = boneOffset(owner, 4, new THREE.Vector3(-80, 20, 0));
      system.createEffect('shockWaveSpin1', spin, angle, WHITE, 0, owner, 0.5);
      system.createEffect('shockWaveSpin1', spin, angle, WHITE, 1, owner, 0.9);
      if (!target) return true;
      system.createEffect('ring', target.position, o.angle, WHITE, 0, owner, 1.9);
      o.lifeTime = 20;
    }
    return true;
  },
};

// BITMAP_SWORDEFF as a joint: a trail on the fist's fifth bone, drawn across (RENDER_FACE_TWO).
EXTRA_JOINTS.swordEff = {
  init: (o) => {
    o.faces = 2;
    o.lifeTime = 10;
    o.maxTails = 50;
    o.light.set(0.6, 0.6, 1);
    return o.target !== null;
  },
  move: (o) => {
    const target = o.target;
    if (!live(target)) {
      o.live = false;
      return;
    }
    o.position.copy(bone(target, 5));
  },
};

/** MODEL_SHOCKWAVE01's draw by SubType: its texture (Damage2 for 1, 3, 4, 5) and its brightness (the draws times the light). */
function shockWave1Brightness(subType: number): number {
  return subType === 1 ? 5 : subType === 2 ? 30 : subType === 3 || subType === 0 ? 2 : 1;
}

// MODEL_SHOCKWAVE01: SubType 0 (Chain Drive's first blow) drifts back and rises as it widens; 1 (Dragon Roar) and 2 (the
// party buffs' blade) swell up and away; 3 to 5 belong to Dark Side's dash (not drawn on the web: the server sends no dash).
EXTRA_EFFECTS.shockWave1 = {
  model: ['Effect', 'shockwave01.bmd'],
  prewarmBlendMesh: -2,
  skin: (o) => (o.subType === 0 || o.subType === 2 ? undefined : 'Damage2.jpg'),
  skins: ['Damage2.jpg'],
  paint: firstMeshOnly,
  create: (o, system) => {
    if (!o.owner || o.subType > 2) return false;
    o.scale = o.callScale;
    if (o.subType === 1) {
      o.velocity = speedOf(o.owner, StrikeAction.DRAGONLORE);
      o.lifeTime = 5 / o.velocity;
    } else if (o.subType === 2) {
      o.velocity = 0.3;
      o.lifeTime = 5 / o.velocity;
    } else {
      o.velocity = speedOf(o.owner, StrikeAction.GIANTSWING) * 2;
      o.lifeTime = 5 / o.velocity;
      driftFromOwner(o, system, 30);
    }
    bright(o, shockWave1Brightness(o.subType));
    return true;
  },
  move: (o) => {
    o.alpha = 1;
    if (o.subType === 1 || o.subType === 2) {
      o.scale *= 1.24;
      o.position.z *= 1.19;
      o.light.multiplyScalar(0.45);
      if (o.light.x < 0.001) o.light.set(0, 0, 0);
    } else {
      o.scale += 0.2;
      o.startPosition.multiplyScalar(0.99);
      o.position.sub(o.startPosition);
      o.light.multiplyScalar(0.65);
      o.position.z = 220 + o.scale * 80;
    }
    return true;
  },
};

// MODEL_SHOCKWAVE02: Chain Drive's second wave, drawn bright twice, widening as it drifts back.
EXTRA_EFFECTS.shockWave2 = {
  model: ['Effect', 'shockwave02.bmd'],
  prewarmBlendMesh: -2,
  create: (o, system) => {
    if (!o.owner) return false;
    o.scale = o.callScale;
    o.velocity = speedOf(o.owner, StrikeAction.GIANTSWING) * 2;
    o.lifeTime = 14 / o.velocity;
    driftFromOwner(o, system, 25);
    bright(o, 2);
    return true;
  },
  move: (o) => {
    o.alpha = 1;
    o.light.multiplyScalar(0.8);
    o.scale += 0.1;
    o.startPosition.multiplyScalar(0.95);
    o.position.sub(o.startPosition);
    return true;
  },
};

// MODEL_SHOCKWAVE_SPIN01: a spinning wave in Damage1's texture; SubType 1 turns the other way.
EXTRA_EFFECTS.shockWaveSpin1 = {
  model: ['Effect', 'shockwave_spin01.bmd'],
  prewarmBlendMesh: -2,
  skin: () => 'Damage1.jpg',
  skins: ['Damage1.jpg'],
  create: (o) => {
    if (!o.owner) return false;
    o.scale = o.callScale;
    const speed = speedOf(o.owner, StrikeAction.GIANTSWING);
    o.lifeTime = 8 / speed;
    o.velocity = speed * (o.subType === 1 ? 3 : 2);
    bright(o, 1);
    return true;
  },
  move: (o) => {
    o.scale *= 1.01;
    o.angle.y -= 10;
    if (o.subType === 1) o.angle.y += 20;
    o.light.multiplyScalar(0.9);
    o.alpha = 1;
    return true;
  },
};

// MODEL_WOLF_HEAD_EFFECT2: SubTypes 3 and 5 stay on the fist (bone 5, then 4 / 27) and throw heads (4 and 6) ahead as
// the fighter's angle shakes; drawn bright at (0.4, 0.4, 0.6) x 0.3.
EXTRA_EFFECTS.wolfHeadEffect2 = {
  model: ['Effect', 'wolf_head_effect2.bmd'],
  prewarmBlendMesh: -2,
  create: (o) => {
    const owner = o.owner;
    if (!owner) return false;
    o.position.copy(bone(owner, 27));
    if (o.subType === 3 || o.subType === 5) {
      o.position.copy(bone(owner, 5));
      o.scale = o.subType === 5 ? 1.5 : 1;
      o.lifeTime = 4 / speedOf(owner, StrikeAction.GIANTSWING);
    } else if (o.subType === 4) {
      o.velocity = 0.1;
      o.scale = 0.7 + randInt(3) * 0.1;
      o.direction.y = -40;
      o.lifeTime = 5 + randInt(2);
    } else if (o.subType === 6) {
      o.velocity = 0.1;
      o.scale = 1.2 + randInt(5) * 0.1;
      o.direction.y = -50;
      o.lifeTime = 2 + randInt(2);
    } else {
      return false;
    }
    o.light.set(0.4, 0.4, 0.6);
    bright(o, 0.3);
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (!owner) return true;
    if (o.subType === 3) {
      o.alpha = o.lifeTime / 20;
      o.angle.copy(owner.angle);
      if (o.lifeTime >= 3) {
        // The desktop shakes the fighter's angle for these children and puts it back.
        const angle = owner.angle.clone();
        const n = randInt(6);
        const step = (2 + randInt(3)) * ((n % 3) * 2 + 1);
        angle.z += Math.trunc(n / 3) ? step : -step;
        const tilt = randInt(3);
        if (tilt === 1) angle.x += step;
        else if (tilt === 2) angle.x -= step;
        o.position.copy(bone(owner, 4));
        system.createEffect('wolfHeadEffect2', owner.position, angle, owner.light ?? WHITE, 4, owner);
        system.createEffect('wolfHeadEffect2', owner.position, angle, owner.light ?? WHITE, 6, owner);
      }
    } else if (o.subType === 5) {
      o.position.copy(bone(owner, 27));
      o.angle.copy(owner.angle);
    }
    return true;
  },
};

// BITMAP_DAMAGE1: twenty ticks of hit bursts on the target, one every fourth tick from the fourth.
EXTRA_EFFECTS.damage1 = {
  create: (o) => {
    const owner = o.owner;
    if (!owner) return false;
    o.lifeTime = 20;
    o.scale = o.callScale;
    o.position.z = bone(owner, 27).z;
    return true;
  },
  move: (o, system) => {
    const target = o.target;
    if (o.lifeTime >= 17 || Math.trunc(o.lifeTime) % 4 !== 0 || !target) return true;
    const p = new THREE.Vector3(target.position.x + randInt(90) - 40, target.position.y + randInt(90) - 40, target.position.z + randInt(100) + 50);
    system.createParticle('damage1', p, o.angle, new THREE.Vector3(0.6, 0.94, 1), 0, (1.5 + randInt(5) * 0.01) * o.scale, o.owner);
    return true;
  },
};

// BITMAP_DAMAGE1 as a particle: five ticks, swelling 1.2 times a tick and growing brighter (x 0.8, then doubled).
EXTRA_PARTICLES.damage1 = {
  texture: 'damage1',
  init: (o, scale) => {
    o.lifeTime = 5;
    o.scale = scale;
    return true;
  },
  move: (o) => {
    o.scale *= 1.2;
    o.light.multiplyScalar(0.8 * 2);
  },
};

// BITMAP_SHINY+4 as an effect (the catalogue: 16 ticks at the call's scale): a ring.jpg flash about it every tick.
EXTRA_EFFECTS.ring = {
  create: () => true,
  move: (o, system) => {
    const p = new THREE.Vector3(o.position.x + randInt(100) - 40, o.position.y + randInt(100) - 40, o.position.z + randInt(100) + 50);
    system.createParticle('shiny4', p, o.angle, o.light, 2, o.scale);
    return true;
  },
};

// ---------------------------------------------------------------------------
// Dragon Roar
// ---------------------------------------------------------------------------

/** m_nLowerEffCnt: how many struck targets got a volcano since the last Dragon Roar (at most four). */
let lowerEffects = 0;

// BITMAP_LAVA (no model): lives while PLAYER_SKILL_DRAGONLORE plays.
EXTRA_EFFECTS.lava = {
  create: (o) => {
    const owner = o.owner;
    if (!owner) return false;
    o.lifeTime = 999;
    o.startPosition.copy(owner.position);
    o.scale = o.callScale;
    o.velocity = speedOf(owner, StrikeAction.DRAGONLORE);
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (!live(owner) || owner.currentAction?.() !== StrikeAction.DRAGONLORE) {
      o.live = false;
      return false;
    }
    const frame = owner.animationFrame?.() ?? 0;
    if (frame > 2 && frame < 8) {
      // Trails laid at tenths of a key between the last tick's pose and this one, on both fists (bones 36, 28).
      const delay = o.velocity * 10;
      for (let i = 0; i < delay; i++) {
        system.createJoint('lavaJoint', bone(owner, 36), bone(owner, 36), owner.angle, i, owner, 20);
        system.createJoint('lavaJoint', bone(owner, 28), bone(owner, 28), owner.angle, 7 + i, owner, 20);
      }
    }
    if (frame > 5 && o.lifeTime > 100) {
      system.createEffect('shockWave1', owner.position, owner.angle, new THREE.Vector3(1, 0.12, 0), 1, owner, 1);
      o.lifeTime = 95;
    }
    if (frame > 5 && o.lifeTime > 80) {
      system.createEffect('shockWaveGround1', o.position, o.angle, new THREE.Vector3(1, 0.1, 0), 1, o.self, 1);
      o.lifeTime = 80;
    }
    if (frame > 5) {
      // The column of fire, rising seven units a tick, with flames and falling sparks about it.
      const scale = o.scale * (randInt(5) + 18) * 0.1;
      const p = o.position.clone();
      p.z += (80 - o.lifeTime) * 10 - (80 - o.lifeTime) * 3;
      const kind = randInt(3);
      system.createParticle(kind === 0 ? 'fireHik1' : kind === 1 ? 'fireCursedLich' : 'fireHik3', p, o.angle, WHITE, kind === 1 ? 4 : 0, scale);
      const half = new THREE.Vector3(0.5, 0.5, 0.5);
      const z = p.z;
      for (let i = 1; i < 8; i += 2) {
        p.add(rotateByAngle(new THREE.Vector3(randInt(100) - 50, randInt(100) - 50, 0), new THREE.Vector3(randInt(90), 0, 45 * i)));
        p.z = z;
        system.createParticle('fireHik1', p, o.angle, half, 5, scale);
        system.createParticle('spark', new THREE.Vector3(p.x, p.y, 250), o.angle, o.light, 12, 1.5);
      }
    }
    return true;
  },
};

// BITMAP_LAVA as a joint: a burning trail on a fist (bone 36, SubTypes 7 and up bone 28), SubType tenths of a key
// behind the pose, fading.
EXTRA_JOINTS.lavaJoint = {
  init: (o) => {
    o.faces = 2;
    o.lifeTime = 10;
    o.maxTails = 10;
    o.light.set(1, 1, 1);
    // o->Velocity: the play speed of the owner's action, kept apart from the joint's own move.
    o.counter = speedOf(o.target, o.target?.currentAction?.() ?? StrikeAction.DRAGONLORE);
    return o.target !== null;
  },
  move: (o) => {
    const target = o.target;
    if (!live(target)) {
      o.live = false;
      return;
    }
    const step = o.subType >= 7 ? o.subType - 7 : o.subType;
    const frame = (target.animationFrame?.() ?? 0) - (o.counter * 10 - step) * 0.1;
    const index = o.subType >= 7 ? 28 : 36;
    o.position.copy(target.bonePositionAt?.(index, frame) ?? bone(target, index));
    o.light.multiplyScalar(0.8);
  },
};

// MODEL_DRAGON_LOWER_DUMMY (Effect/knight_plancrack_dragon.bmd): the burning crack, its glow (BITMAP_LIGHT_RED 1) and
// sparks falling into it while its first keys play; it fades 4% a tick. Meshes 0 and 2 glow gold, 1 red, all scrolling.
EXTRA_EFFECTS.dragonLowerDummy = {
  model: ['Effect', 'knight_plancrack_dragon.bmd'],
  prewarmBlendMesh: -2,
  create: (o, system) => {
    o.scale = o.callScale;
    o.velocity = 0.3 + 0.3 / 6; // its move's step and its draw's extra, slower step
    o.lifeTime = 300;
    o.alpha = 1;
    o.position.z = system.heightAt(o.position.x, o.position.y) + 10;
    o.angle.z = 45 + randInt(180);
    bright(o, 1);
    return true;
  },
  move: (o, system) => {
    if (o.animationFrame > 1 && o.animationFrame < 6) {
      // o->Light is the red its draw left on it.
      system.createParticle('spark', new THREE.Vector3(o.position.x, o.position.y, 250), o.angle, new THREE.Vector3(1, 0.2, 0.1), 12, 1.5);
    }
    if (o.animationFrame > 1 && o.lifeTime > 80) {
      system.createEffect('lightRed', o.position, o.angle, WHITE, 1, o.self, 15);
      o.lifeTime = 80;
    }
    o.alpha *= 0.96;
    return true;
  },
  paint: (o, meshIndex, material, _mesh, system) => {
    const colour = meshIndex === 1 ? [1, 0.2, 0.1] : [1, 0.8, 0.2];
    material.color.setRGB(colour[0], colour[1], colour[2]).multiplyScalar(o.alpha);
    material.opacity = 1;
    if (material.map) material.map.offset.y = -(Math.trunc(system.time) % 2000) * 0.0001;
  },
};

// MODEL_VOLCANO_OF_MONK: SubType 1 is the volcano (with its four stones and red glow), SubTypes 2 and 3 wait unseen and
// raise one later; SubType 4 has no case on the desktop and shows nothing.
const volcanoes = new WeakMap<EffectOwner, Effect>();
EXTRA_EFFECTS.volcanoOfMonk = {
  model: ['Effect', 'volcano_of_monk.bmd'],
  create: (o, system) => {
    if (o.subType === 1) {
      o.scale = o.callScale;
      o.position.z = system.heightAt(o.position.x, o.position.y);
      o.lifeTime = 100;
      o.velocity = 0.6;
      for (let i = 0; i < 4; i++) system.createEffect('volcanoStone', o.position, o.angle, WHITE, o.subType, o.self, 1);
      system.createEffect('lightRed', o.position, o.angle, WHITE, 3, o.self, 3);
      volcanoes.set(o.self, o);
      system.createEffect('volcanoOfMonkCore', o.position, o.angle, WHITE, 0, o.self, o.scale);
      return true;
    }
    if (o.subType === 2 || o.subType === 3) {
      o.lifeTime = o.subType === 3 ? 30 + randInt(20) : 20 + randInt(10);
      o.hiddenMesh = -2; // not drawn while it waits
      return o.owner !== null;
    }
    return false;
  },
  move: (o, system) => {
    if (o.subType === 1) {
      const p = o.position.clone();
      p.z += (80 - o.lifeTime) * (0.9 + randInt(5) * 0.1) + o.subType * 20;
      if (p.z <= 400) {
        const scale = o.scale * (randInt(5) + 8) * 0.19;
        const kind = randInt(3);
        system.createParticle(kind === 0 ? 'fireHik1' : kind === 1 ? 'fireCursedLich' : 'fireHik3', p, o.angle, WHITE, kind === 1 ? 9 : 6, scale);
        if (Math.trunc(o.lifeTime) === 10) {
          for (let i = 0; i < 4; i++) system.createEffect('volcanoStone', o.position, o.angle, WHITE, 0, o.self, 1);
          system.createEffect('shockWaveGround1', o.position, o.angle, new THREE.Vector3(1, 0.1, 0), 2, o.self, 1);
        }
        if (o.lifeTime < 20) o.alpha = o.lifeTime * 0.1;
        if (o.lifeTime < 30) o.blendMeshLight = o.lifeTime * 0.03;
        const l = randInt(5) * 0.01;
        system.groundLight(o.position, l + 1, l + 0.4, l + 0.4, 1);
      }
      return true;
    }
    if (!live(o.owner)) {
      o.live = false;
      return false;
    }
    if (Math.trunc(o.lifeTime) === 2) {
      system.createEffect('volcanoOfMonk', o.position, o.angle, o.light, 1, o.self, 0.8);
      o.live = false;
      return false;
    }
    o.position.z = -100;
    return true;
  },
};

// The volcano's core: its first mesh drawn again in volcano_core.jpg, bright at the volcano's BlendMeshLight.
EXTRA_EFFECTS.volcanoOfMonkCore = {
  model: ['Effect', 'volcano_of_monk.bmd'],
  prewarmBlendMesh: -2,
  skin: () => 'volcano_core.jpg',
  skins: ['volcano_core.jpg'],
  paint: firstMeshOnly,
  create: (o) => {
    o.lifeTime = 100;
    bright(o, 1);
    return o.owner !== null && volcanoes.has(o.owner);
  },
  move: (o) => {
    const volcano = o.owner ? volcanoes.get(o.owner) : undefined;
    if (!volcano || !volcano.live) {
      o.live = false;
      return false;
    }
    o.position.copy(volcano.position);
    o.angle.copy(volcano.angle);
    o.scale = volcano.scale;
    o.alpha = volcano.alpha;
    o.blendMeshLight = volcano.blendMeshLight;
    o.animationFrame = volcano.animationFrame;
    o.lifeTime = 10;
    return false;
  },
};

// MODEL_VOLCANO_STONE: a burning stone thrown up, bouncing on the ground and fading, flames trailing.
EXTRA_EFFECTS.volcanoStone = {
  model: ['Effect', 'volcano_stone.bmd'],
  create: (o) => {
    o.scale = o.callScale + (randInt(20) + 5) * 0.06;
    o.direction.set(0, 0, 0);
    o.lifeTime = randInt(10) + 30;
    o.gravity = randInt(2) + 2;
    o.angle.z = randInt(360);
    rotateByAngle(new THREE.Vector3(0, (randInt(128) + 64) * 0.1, 0), o.angle, o.headAngle);
    o.headAngle.z += 25;
    return true;
  },
  move: (o, system) => {
    o.headAngle.z -= o.gravity;
    o.position.add(o.headAngle);
    const height = system.heightAt(o.position.x, o.position.y);
    o.angle.x += 0.3 * o.lifeTime;
    o.angle.y += 0.3 * o.lifeTime;
    if (o.position.z <= height) {
      o.position.z = height;
      o.headAngle.x *= 0.6;
      o.headAngle.y *= 0.6;
      o.headAngle.z += o.lifeTime;
      if (o.headAngle.z < 0.5) o.headAngle.z = 0;
      o.alpha -= 0.05;
    }
    o.scale -= 0.03;
    const scale = o.scale * (randInt(5) + 5) * 0.1;
    const kind = randInt(3);
    system.createParticle(kind === 0 ? 'fireHik1' : kind === 1 ? 'fireCursedLich' : 'fireHik3', o.position, o.angle, WHITE, kind === 1 ? 4 : 0, scale);
    return true;
  },
};

// MODEL_TARGETMON_EFFECT (the catalogue: 100 ticks at the call's scale): the struck burns on a random bone every tick.
EXTRA_EFFECTS.targetMonsterEffect = {
  create: (o) => o.owner !== null,
  move: (o, system) => {
    const owner = o.owner;
    if (!live(owner)) {
      o.live = false;
      return false;
    }
    const bones = owner.boneCount?.() ?? 0;
    const p = bones > 1 ? owner.bonePosition?.(randInt(bones - 1)) ?? owner.position.clone() : owner.position.clone();
    o.position.copy(p);
    const scale = o.scale * (randInt(5) + 5) * 0.1;
    for (let i = 0; i < 3; i++) {
      const kind = randInt(3);
      system.createParticle(kind === 0 ? 'fireHik1' : kind === 1 ? 'fireCursedLich' : 'fireHik3', o.position, o.angle, WHITE, kind === 1 ? 4 : 0, scale);
    }
    return true;
  },
};

/** ReceiveMagic for Dragon Roar, at each struck target the server shows: the first four get a volcano, every one burns. */
export function dragonRoarHit(system: EffectSystem, target: EffectOwner): void {
  if (lowerEffects <= 3) {
    lowerEffects++;
    system.createEffect('volcanoOfMonk', target.position, target.angle, WHITE, lowerEffects, target, 0.8);
  }
  system.createEffect('targetMonsterEffect', target.position, target.angle, WHITE, 0, target, 1);
}

/** RageCreateEffect for Chain Drive. */
export function chainDrive(system: EffectSystem, caster: EffectOwner, target: EffectOwner | null): void {
  system.createEffect('swordEffect', caster.position, caster.angle, caster.light ?? WHITE, 0, caster, 0, { target });
}

/** RageCreateEffect for Dragon Roar: the burning fists and the crack toward the target (InitLower). */
export function dragonRoar(system: EffectSystem, caster: EffectOwner, target: EffectOwner | null): void {
  system.createEffect('lava', caster.position, caster.angle, WHITE, 0, caster, 1);
  system.createEffect('blowOfDestruction', caster.position, caster.angle, WHITE, 2, caster, 0, { target });
  lowerEffects = 0;
}
