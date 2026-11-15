// Particle types several skills of the third pass share (registered in extraEffects.ts's tables).
//
// Followed C++ (client/src/source):
//   CreateParticle / MoveParticles / DescribeParticle: BITMAP_EXPLOTION+1, BITMAP_ADV_SMOKE+1   Render/Effects/ZzzEffectParticle.cpp

import { randInt } from './core.js';
import * as THREE from 'three';
import { EXTRA_EFFECTS, EXTRA_PARTICLES } from './extraEffects.js';

// BITMAP_EXPLOTION+1 (DinoE.jpg): a burst of fire, a strip of four frames turned by its angle.
EXTRA_PARTICLES.explosion2 = {
  texture: 'explosion2',
  init: (o, scale) => {
    o.lifeTime = 12;
    o.scale = scale;
    return true;
  },
  move: (o) => {
    o.frame = Math.trunc((12 - o.lifeTime) / 3);
  },
  draw: (o, system, width, height, blend) => {
    system.quads.billboard(o.texture, blend, o.position, width * 0.25, height, o.light, o.angle.x, (o.frame % 4) * 0.25, 0, 0.25, 1);
  },
};

// BITMAP_ADV_SMOKE+1 (fi02.tga): smoke drifting up and growing. SubType 2 draws with its light added to the texture
// (light from 1 down to -1: white, then the smoke, then gone); here the texture is multiplied by 1 + that light.
EXTRA_PARTICLES.advSmoke2 = {
  texture: 'advSmoke2',
  init: (o, scale) => {
    o.lifeTime = 25 + randInt(5);
    o.rotation = randInt(360);
    o.scale = 0.5;
    if (o.subType === 1) o.scale *= scale;
    o.velocity.set((randInt(10) + 5) * 0.4, 0, (randInt(10) + 5) * 0.2);
    return true;
  },
  move: (o) => {
    o.position.add(o.velocity);
    o.velocity.x *= 0.95;
    o.velocity.y *= 0.95;
    o.velocity.z += 0.6;
    o.position.x += randInt(4) - 2;
    o.position.y += randInt(4) - 2;
    o.position.z += (randInt(4) - 2) * 0.8;
    o.scale += 0.05;
    const light = o.subType === 2 ? o.lifeTime / 25 * 2 - 1 : o.lifeTime / 25;
    o.light.setScalar(o.subType === 2 ? Math.max(0, 1 + light) : light);
    o.rotation += 1 + randInt(2);
  },
  blend: () => 'blend3',
  draw: (o, system, width, height, blend) => {
    system.quads.billboard(o.texture, blend, o.position, width, height, o.light, o.rotation);
  },
};

// BITMAP_BLUE_BLUR SubType 1: drawn with BITMAP_POUNDING_BALL, 45 under where it is made; it rises, grows and fades.
EXTRA_PARTICLES.blueBlur = {
  texture: 'poundingBall',
  init: (o) => {
    if (o.subType !== 1) return false;
    o.lifeTime = 30;
    o.scale = (randInt(64) + 64) * 0.01;
    o.rotation = randInt(360);
    o.gravity = (randInt(32) + 60) * 0.1;
    o.position.z -= 45;
    return true;
  },
  move: (o) => {
    o.light.setScalar(o.lifeTime / 20);
    o.scale += 0.19;
    o.position.z += 5;
  },
  draw: (o, system, width, height, blend) => {
    system.quads.billboard(o.texture, blend, o.position, width, height, o.light, o.rotation);
  },
};

// BITMAP_LIGHTNING+1 SubType 1: no sprite (scale 0), only a green glow on the ground fading over ten ticks.
EXTRA_PARTICLES.lightning2 = {
  texture: 'lightning2',
  init: (o) => {
    if (o.subType !== 1) return false;
    o.lifeTime = 10;
    o.scale = 0;
    return true;
  },
  move: (o, system) => {
    const l = Math.max(0, o.lifeTime) / 10;
    system.groundLight(o.position, l * 0.5, l, l * 0.8, 3);
  },
  draw: () => { /* scale 0: nothing drawn */ },
};

/**
 * BITMAP_FIRE_HIK1 (SubTypes 0, 5 and 6), _CURSEDLICH (4 and 9) and _HIK3 (0 and 6): a flame that rises fast, turning,
 * fading in to its colour and out again in its last ticks while it shrinks. The straight SubTypes (6, 9: Dragon Roar's
 * volcanoes) don't turn and rise 2.2 times as fast.
 */
function risingFlame(texture: 'fireHik1' | 'fireCursedLich' | 'fireHik3', subTypes: readonly number[], straight: number, life: number, fadeAt: number, shrink: number): NonNullable<typeof EXTRA_PARTICLES.fireHik1> {
  return {
    texture,
    init: (o, scale) => {
      if (!subTypes.includes(o.subType)) return false;
      o.lifeTime = randInt(5) + life;
      o.scale = (randInt(72) + 72) * 0.01 * scale;
      o.rotation = randInt(360);
      o.gravity = (randInt(24) + 64) * 0.1;
      o.alpha = 0;
      o.turningForce.copy(o.light);
      o.light.set(0, 0, 0);
      return true;
    },
    move: (o) => {
      if (o.lifeTime < fadeAt) o.alpha -= 0.2;
      else if (o.alpha < 1) o.alpha += (randInt(2) + 2) * 0.1;
      else o.alpha = 1;
      if (o.alpha < 0.1) o.live = false;
      o.light.copy(o.turningForce).multiplyScalar(o.alpha);
      if (o.scale > 0) o.scale -= (randInt(3) + shrink) * 0.01;
      else o.live = false;
      o.position.z += o.gravity;
      o.rotation += 3;
      if (o.subType === straight) {
        o.rotation = 0;
        o.position.z += o.gravity * 1.2;
      }
    },
    draw: (o, system, width, height, blend) => {
      system.quads.billboard(o.texture, blend, o.position, width, height, o.light, o.rotation);
    },
  };
}
EXTRA_PARTICLES.fireHik1 = risingFlame('fireHik1', [0, 5, 6], 6, 27, 15, 5);
EXTRA_PARTICLES.fireCursedLich = risingFlame('fireCursedLich', [4, 9], 9, 12, 10, 6);
EXTRA_PARTICLES.fireHik3 = risingFlame('fireHik3', [0, 6], 6, 17, 10, 7);

// BITMAP_FIRE_CURSEDLICH SubType 2 (the catalogue gives 20 ticks): the Flame Strike debuff; every tick eight flames and
// orange lights on random bones of its owner.
EXTRA_EFFECTS.fireCursedLich = {
  create: (o) => o.subType === 2 && o.owner !== null,
  move: (o, system) => {
    const owner = o.owner;
    if (!owner) {
      o.live = false;
      return true;
    }
    const bones = owner.boneCount?.() ?? 0;
    const white = new THREE.Vector3(1, 1, 1);
    for (let i = 0; i < 8 && bones > 0; i++) {
      const p = owner.bonePosition?.(randInt(bones));
      if (!p) continue;
      system.createSprite('light', p, 4, new THREE.Vector3(1, 0.2, 0), owner);
      const scale = (randInt(5) + 13) * 0.1;
      const kind = randInt(3);
      system.createParticle(kind === 0 ? 'fireHik1' : kind === 1 ? 'fireCursedLich' : 'fireHik3', p, o.angle, white, kind === 1 ? 4 : 0, scale);
    }
    return true;
  },
};
