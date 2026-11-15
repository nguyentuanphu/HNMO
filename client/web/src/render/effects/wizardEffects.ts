// The Dark Wizard's later skill effects (registered in extraEffects.ts's tables): Cometfall and Expansion of
// Wizardry.
//
// Followed C++ (client/src/source):
//   MoveCharacter's release AT_SKILL_BLAST (two MODEL_SKILL_BLAST at the tile)      Engine/Object/ZzzCharacter.cpp
//   ReceiveMagic AT_SKILL_EXPANSION_OF_WIZARDRY (MODEL_SWELL_OF_MAGICPOWER)         Network/Server/WSclient.cpp
//   CreateEffect MODEL_SKILL_BLAST; MoveEffect MODEL_SWELL_OF_MAGICPOWER, MODEL_ARROWSRE06 (1),
//   BITMAP_SHOCK_WAVE (14), BITMAP_TWLIGHT (3); RenderEffects' BITMAP_TWLIGHT / BITMAP_SHOCK_WAVE decals
//                                                                                   Render/Effects/ZzzEffect.cpp
//   Move_MODEL_SKILL_BLAST                                                          Render/Effects/Behaviors/MoveHandlers.cpp
//   RenderObject: MODEL_SWELL_OF_MAGICPOWER (mesh 0 glowing, body light 0.7 0.4 0.9), MODEL_ARROWSRE06
//                                                                                   Engine/Object/ZzzObject.cpp
//   CreateParticle / MoveParticles: BITMAP_SHINY+4 (0)                              Render/Effects/ZzzEffectParticle.cpp
//   EffectTypes.json: MODEL_SWELL_OF_MAGICPOWER, MODEL_ARROWSRE06, BITMAP_TWLIGHT (their creation values)

import * as THREE from 'three';
import { randInt } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_PARTICLES } from './extraEffects.js';
import type { Effect, EffectOwner } from './types.js';

const WHITE = new THREE.Vector3(1, 1, 1);
const TERRAIN_SCALE = 100;

function live(owner: EffectOwner | null): owner is EffectOwner {
  return owner !== null && (owner.isLive?.() ?? true);
}

// ---------------------------------------------------------------------------
// Cometfall (AT_SKILL_BLAST): two comets fall on the tile from up to 800 above, each with a blue trail; where one
// lands it throws stones and bursts into a ring and an explosion.
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.skillBlast = {
  model: ['Skill', 'Blast01.bmd'],
  create: (o, system) => {
    o.lifeTime = 30;
    o.blendMesh = 0;
    o.scale = (randInt(8) + 10) * 0.1;
    o.position.x += randInt(100) + 200;
    o.position.y += randInt(100) - 50;
    o.position.z += randInt(500) + 300;
    o.direction.set(0, 0, -50 - randInt(50));
    o.angle.set(0, 20, 0);
    // BITMAP_JOINT_ENERGY SubType 5 follows the comet (its EyeLeft is its position).
    system.createJoint('jointEnergy', o.position, o.position, o.angle, 5, o.self, 100, 0, 0, new THREE.Vector3(0.5, 0.1, 1));
    return true;
  },
  move: (o, system, luminosity) => {
    const height = system.heightAt(o.position.x, o.position.y);
    if (o.position.z < height) {
      o.position.z = height;
      o.direction.set(0, 0, 0);
      const position = new THREE.Vector3(o.position.x, o.position.y, o.position.z + 80);
      for (let j = 0; j < 6; j++) system.createEffect(randInt(2) ? 'stone2' : 'stone1', o.position, o.angle, o.light);
      system.createParticle('shiny4', position, o.angle, WHITE);
      system.createParticle('explosion', position, o.angle, WHITE);
      o.live = false;
      return false;
    }
    system.groundLight(o.position, luminosity * 0.2, luminosity * 0.4, luminosity, 2);
    return true;
  },
};

// BITMAP_SHINY+4 SubType 0 (ring.jpg): a ring that swells and fades over its twenty ticks.
EXTRA_PARTICLES.shiny4 = {
  texture: 'shiny4',
  init: (o) => {
    if (o.subType !== 0) return false;
    o.lifeTime = 20;
    return true;
  },
  move: (o) => {
    o.scale = Math.sin(o.lifeTime * 10 * Math.PI / 180) * 3 + 2;
    o.light.setScalar(o.lifeTime / 5);
  },
};

/** Cometfall's release (MoveCharacter AT_SKILL_BLAST): two comets at the tile's centre. */
export function cometfall(system: EffectSystem, caster: EffectOwner, cell: { x: number; y: number }): void {
  const x = (cell.x + 0.5) * TERRAIN_SCALE, y = (cell.y + 0.5) * TERRAIN_SCALE;
  const position = new THREE.Vector3(x, y, system.heightAt(x, y));
  for (let i = 0; i < 2; i++) system.createEffect('skillBlast', position, caster.angle, caster.light ?? WHITE, 0, caster);
}


// ---------------------------------------------------------------------------
// Expansion of Wizardry (AT_SKILL_EXPANSION_OF_WIZARDRY): MODEL_SWELL_OF_MAGICPOWER (45 ticks) stands on the
// wizard: rings of light (BITMAP_SHOCK_WAVE 14, BITMAP_TWLIGHT 3) at its 45th, 35th and 25th tick, purple streaks
// (BITMAP_2LINE_GHOST 1) while it is young, flashes at both hands (MODEL_ARROWSRE06 1) at the start and lights on
// every bone in its last twenty ticks. Players only.
// ---------------------------------------------------------------------------

/** The player model's hands (Bip01 R Hand, Bip01 L Hand). */
const HANDS = [28, 37] as const;

EXTRA_EFFECTS.swellOfMagicPower = {
  model: ['Effect', 'magic_powerup.bmd'],
  create: (o) => {
    if (o.subType !== 0 || !o.owner) return false;
    o.blendMesh = 0; // mesh 0 glows; the body light is 0.7 0.4 0.9 (RenderObject)
    o.light.set(0.7, 0.4, 0.9);
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (!live(owner)) return true;
    o.position.copy(owner.position);
    o.direction.set(0, 0, 0);
    const tick = Math.trunc(o.lifeTime);
    if (tick === 45 || tick === 35 || tick === 25) {
      const ring = new THREE.Vector3(0.4, 0.3, 0.9);
      system.createEffect('shockWave', o.position, o.angle, ring, 14, owner, 5);
      system.createEffect('shockWave', o.position, o.angle, ring, 14, owner, 5);
      system.createEffect('twlight', o.position, o.angle, ring, 3, owner, 6);
    }
    if (o.lifeTime <= 20) {
      const light = new THREE.Vector3(0.7, 0.3, 0.9).multiplyScalar(o.lifeTime * 0.05);
      const bones = owner.boneCount?.() ?? 0;
      for (let i = 0; i < bones; i++) {
        const p = owner.bonePosition?.(i);
        if (p) system.createSprite('light', p, 1.5, light, owner);
      }
      o.blendMeshLight *= 0.86;
    }
    if (tick === 45) {
      for (const bone of HANDS) {
        const p = owner.bonePosition?.(bone) ?? owner.position;
        system.createEffect('arrowsre6', p, o.angle, new THREE.Vector3(0.2, 0.2, 0.9), 1, owner, 0, { pkKey: bone });
      }
    }
    if (o.lifeTime >= 30) {
      for (let i = 0; i < 2; i++) system.createJoint('twoLineGhost', o.position, o.position, o.angle, 1, o.self, 20 + randInt(10), 0, 0, new THREE.Vector3(0.3, 0.2, 0.9));
    }
    return true;
  },
};

// MODEL_ARROWSRE06 SubType 1 (the catalogue: LifeTime 40): a flash on a hand's bone that swells for 25 ticks, then
// shrinks, with a light; drawn with its glowing mesh in its light, which fades in the last ten ticks.
EXTRA_EFFECTS.arrowsre6 = {
  model: ['Effect', 'arrowsre06.bmd'],
  create: (o) => {
    if (o.subType !== 1 || !o.owner) return false;
    o.blendMesh = 0;
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (live(owner)) o.position.copy(owner.bonePosition?.(o.pkKey) ?? owner.position);
    o.scale *= o.lifeTime >= 15 ? 1.05 : 0.95;
    system.createSprite('light', o.position, o.scale, o.light, owner);
    if (o.lifeTime <= 10) o.blendMeshLight *= 0.8;
    o.direction.set(0, 0, 0);
    return true;
  },
};

/** ReceiveMagic AT_SKILL_EXPANSION_OF_WIZARDRY: the swell on the wizard (players only). */
export function expansionOfWizardry(system: EffectSystem, caster: EffectOwner): void {
  if ((caster.boneCount?.() ?? 0) === 0) return;
  system.createEffect('swellOfMagicPower', caster.position, caster.angle, new THREE.Vector3(0.3, 0.2, 0.9), 0, caster);
}

// MODEL_SWELL_OF_MAGICPOWER_BUFF_EFF SubType 0: while the buff lasts, a pulsing purple light on every bone and, every
// six seconds, flashes at both hands. Only its sprites show.
EXTRA_EFFECTS.swellOfMagicPowerBuffEffect = {
  create: (o, system) => {
    if (o.subType !== 0 || !o.owner) return false;
    o.lifeTime = 999;
    o.counter = system.time; // Timer
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (!live(owner)) {
      o.live = false;
      return true;
    }
    o.position.copy(owner.position);
    o.direction.set(0, 0, 0);
    if (system.time - o.counter >= 6000) {
      for (const bone of HANDS) system.createEffect('arrowsre6', o.position, o.angle, new THREE.Vector3(0.2, 0.2, 0.9), 1, owner, 0, { pkKey: bone });
      o.counter = system.time;
    }
    o.lifeTime = 999;
    return true;
  },
  sprites: (o, system) => {
    const owner = o.owner;
    if (!live(owner)) return;
    const lumi = (Math.abs(Math.sin(system.time * 0.001)) + 0.2) * 0.5;
    const light = new THREE.Vector3(lumi * 0.7, lumi * 0.3, lumi * 0.9);
    const bones = owner.boneCount?.() ?? 0;
    for (let i = 0; i < bones; i++) {
      const p = owner.bonePosition?.(i);
      if (p) system.createSprite('light', p, 1.8, light, owner);
    }
  },
};
