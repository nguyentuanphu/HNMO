// Particle types several skills of the third pass share (registered in extraEffects.ts's tables).
//
// Followed C++ (client/src/source):
//   CreateParticle / MoveParticles / DescribeParticle: BITMAP_EXPLOTION+1, BITMAP_ADV_SMOKE+1   Render/Effects/ZzzEffectParticle.cpp

import { randInt } from './core.js';
import { EXTRA_PARTICLES } from './extraEffects.js';

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
