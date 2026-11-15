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

