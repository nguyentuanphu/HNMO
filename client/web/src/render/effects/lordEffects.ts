// The Magic Gladiator's and the Dark Lord's skill effects (registered in extraEffects.ts's tables).
//
// Followed C++ (client/src/source/Render/Effects):
//   CreateEffect: BITMAP_GATHERING (1), BITMAP_SWORD_FORCE, MODEL_WAVES (1, 2)   ZzzEffect.cpp
//   Move_BITMAP_GATHERING, _BITMAP_SWORD_FORCE, _MODEL_WAVES, _MODEL_PIERCING2   Behaviors/MoveHandlers.cpp
//   CreateJoint / MoveJoint / DescribeJoint: BITMAP_JOINT_FORCE (0), BITMAP_PIERCING (0)
//                                                                                ZzzEffectJoint.cpp
//   EffectTypes.json: MODEL_PIERCING2 (its creation values)
//   CreateEffect / Move_MODEL_EFFECT_FLAME_STRIKE and its draw in RenderEffects (the blurs not yet);
//   CreateEffect / Move_BITMAP_JOINT_THUNDER (Gigantic Storm: bolts, circles, smoke 54, stones 13, BITMAP_CHROME_ENERGY2)
//   CreateEffect / Move_MODEL_PIER_PART (0, 1), MODEL_DARKLORD_SKILL; MoveCharacter's release for
//   AT_SKILL_FIREBURST and AT_SKILL_ADD_CRITICAL (the trails' BITMAP_FIRE+1, two ticks each)

import * as THREE from 'three';
import { moveHumming, randInt, rotateByAngle } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS, EXTRA_PARTICLES } from './extraEffects.js';
import type { Effect, EffectOwner } from './types.js';

const WHITE = new THREE.Vector3(1, 1, 1);
/** The bone Fire Slash's gathering sits on (BoneTransform[33], the right hand). */
const HAND_BONE = 33;

// BITMAP_GATHERING SubType 1 (Fire Slash's charge): lightning and sparks gather at the
// gladiator's hand for twenty ticks. (SubType 0, Penetration's, is in elfEffects.ts.)
const elfGathering = EXTRA_EFFECTS.gathering;
EXTRA_EFFECTS.gathering = {
  create: (o, system) => {
    if (o.subType !== 1) return elfGathering?.create(o, system) ?? false;
    o.startPosition.copy(o.position);
    o.lifeTime = 20;
    return true;
  },
  move: (o, system, luminosity) => {
    if (o.subType !== 1) return elfGathering?.move(o, system, luminosity) ?? false;
    const hand = o.owner?.bonePosition?.(HAND_BONE);
    if (hand) o.position.copy(hand).setZ(hand.z + 10);
    for (let j = 0; j < 3; j++) {
      const angle = new THREE.Vector3(randInt(360), 0, randInt(360));
      const position = rotateByAngle(new THREE.Vector3(0, 120, 0), angle).add(o.position);
      if (Math.trunc(o.lifeTime) % 2 === 0) system.createJoint('jointThunder', position, o.position, angle, 3, null, 10, 10, 10);
      else system.createParticle('spark3', position, angle, WHITE, 2, (randInt(50) + 10) / 100, o.self);
      system.createSprite('shiny2', o.position, (randInt(8) + 8) * 0.2, o.light, o.self, randInt(360));
    }
    return false;
  },
};

// BITMAP_SWORD_FORCE (Fire Slash): on its first tick a burning sweep (BITMAP_JOINT_FORCE) leaves
// 100 over the gladiator, turned 45 from its facing.
EXTRA_EFFECTS.swordForce = {
  create: (o) => {
    o.lifeTime = 30;
    if (o.subType === 0 || o.subType === 1) o.light.set(0.8, 0.8, 0.8);
    o.headAngle.set(0, 0, o.angle.z + 45);
    o.startPosition.copy(o.position);
    return o.subType === 0;
  },
  move: (o, system) => {
    system.groundLight(o.position, 1, 0.8, 0.6, 1);
    if (o.lifeTime === 30) {
      const position = o.position.clone().setZ(o.position.z + 100);
      system.createJoint('jointForce', position, position, o.headAngle, 0, o.owner, 150);
    }
    return true;
  },
};

// BITMAP_JOINT_FORCE SubType 0: an arc swept around its centre, 8 segments a tick, burning and
// crackling; drawn as one ribbon that fades along its length.
EXTRA_JOINTS.jointForce = {
  init: (o) => {
    if (o.subType !== 0) return false;
    o.lifeTime = 20;
    o.createTails = false;
    o.velocity = 0;
    o.maxTails = 18;
    o.numTails = -1;
    o.targetPosition.copy(o.position);
    o.angle.z += 30;
    o.direction.copy(o.angle);
    o.position.add(rotateByAngle(new THREE.Vector3(0, -180, 0), o.angle));
    return true;
  },
  move: (o, system) => {
    const p = new THREE.Vector3(0, -145, 0);
    for (let j = 0; j < 8; j++) {
      if (o.numTails >= o.maxTails - 1) break;
      const from = o.position.clone();
      o.position.copy(rotateByAngle(p, o.direction).add(o.targetPosition));
      system.layTail(o, o.angle);
      o.direction.z -= 11;
      if (randInt(2) === 0) system.createParticle('fire', o.position, o.angle, o.light, 0);
      system.createJoint('jointThunder', from, o.position, o.angle, 3, null, randInt(10) + 5, 10, 5);
      system.createJoint('jointThunder', from, o.position, o.angle, 3, null, randInt(8) + 4, 10, 5);
    }
    o.light.setScalar(o.lifeTime < 10 ? o.light.x / 1.5 : o.lifeTime / 30);
  },
};

// MODEL_WAVES: SubType 1 (Force Wave's rings) grow from 80 over the lord with two piercing
// streaks; SubType 2 (left by the spear) grow to 1.5. Both fade.
EXTRA_EFFECTS.waves = {
  model: ['Skill', 'm_waves.bmd'],
  create: (o, system) => {
    o.lifeTime = 20;
    o.gravity = 0.1;
    o.blendMesh = -2;
    o.blendMeshLight = 1;
    const facing = o.angle.z;
    o.angle.set(0, 0, 0);
    o.startPosition.copy(o.position);
    o.position.z += 50;
    if (o.subType === 1) {
      o.lifeTime = 15;
      o.position.z += 80;
      o.scale = 0.1 + randInt(50) / 100;
      o.gravity = 0.01;
      o.angle.x = 90;
      o.angle.z = facing;
      for (let j = 0; j < 2; j++) system.createJoint('piercing', o.position, o.position, o.angle, 0, null, randInt(40) + 70);
      o.position.x += randInt(100) - 50;
      o.position.y += randInt(100) - 50;
      return true;
    }
    if (o.subType === 2) {
      o.lifeTime = 15;
      o.scale = o.pkKey * 0.05;
      o.pkKey = -1;
      o.gravity = 0.01;
      o.position.z -= 50;
      o.angle.x = 90;
      o.angle.z = facing;
      return true;
    }
    return false;
  },
  move: (o) => {
    o.scale += o.gravity;
    if (o.subType === 1) {
      o.gravity += 0.07;
      o.scale = Math.min(o.scale, 2);
    } else {
      o.gravity += 0.01;
      o.scale = Math.min(o.scale, 1.5);
    }
    o.blendMeshLight *= 1 / 1.5;
    return true;
  },
};

// MODEL_PIERCING2 (the catalogue: LifeTime 10, Scale 2, all meshes glowing, pushed 60 a tick,
// 130 up): Force Wave's spear; it slows down and leaves rings behind.
EXTRA_EFFECTS.piercing2 = {
  model: ['Skill', 'm_Piercing.bmd'],
  create: () => true,
  move: (o: Effect, system: EffectSystem, luminosity: number) => {
    system.groundLight(o.position, luminosity * 0.6, luminosity * 0.8, luminosity * 0.8, 2);
    o.direction.y = Math.min(0, o.direction.y + 12);
    if (o.lifeTime > (o.subType === 0 ? 5 : 1)) {
      o.blendMeshLight *= 1 / 1.6;
      system.createEffect('waves', o.position, o.angle, o.light, 2, null, 0, { pkKey: o.lifeTime });
    }
    return true;
  },
};

// BITMAP_PIERCING SubType 0: a streak shot straight up the ring's axis, slowing, fading.
EXTRA_JOINTS.piercing = {
  init: (o) => {
    if (o.subType !== 0) return false;
    o.createTails = false;
    o.lifeTime = 10;
    o.maxTails = 30;
    o.velocity = 20;
    o.angle.set(-90, 0, o.angle.z);
    o.direction.set(0, 0, 0);
    if (randInt(2) === 0) o.light.set(1, 0.8, 0.6);
    else o.light.set(1, 1, 1);
    o.startPosition.copy(o.position);
    return true;
  },
  move: (o, system) => {
    if (o.lifeTime > 9) {
      for (let i = 0; i < 30; i++) {
        system.layTail(o, o.angle);
        o.position.add(rotateByAngle(new THREE.Vector3(0, 0, -o.velocity), o.angle));
      }
    }
    o.velocity -= 2;
    o.light.multiplyScalar(1 / 1.4);
  },
};

// ---------------------------------------------------------------------------
// Gigantic Storm (CreateEffect BITMAP_JOINT_THUNDER, Move_BITMAP_JOINT_THUNDER; the catalogue's
// row: LifeTime 20, StartPosition 800 over it): five storms around the gladiator, each a
// column of bolts from high above with blue circles on the ground.
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.jointThunderEffect = {
  create: (o) => {
    o.lifeTime = 20;
    o.startPosition.copy(o.position).setZ(o.position.z + 800);
    return o.subType === 0 || o.subType === 1;
  },
  move: (o, system) => {
    const scale = (randInt(400) + 100) / 10;
    const from = o.startPosition.clone(), to = o.position.clone();
    from.x += randInt(100) - 50;
    from.y += randInt(100) - 50;
    to.x += randInt(100) - 50;
    to.y += randInt(100) - 50;
    if (randInt(2) === 0) system.createJoint('jointThunder', from, to, o.angle, 16, null, scale);
    const light = new THREE.Vector3(0.45, 0.45, 0.7);
    if (o.lifeTime > 10 && randInt(2) === 0) system.createEffect('magicGround2', to, o.angle, light, 11, o.self);
    // Smoke rising, stones thrown up and an energy flash on the ground (its "chrome").
    if (randInt(4) === 0) system.createParticle('smoke', to, o.angle, light, 54, 2.8);
    if (randInt(4) === 0) system.createEffect(randInt(2) ? 'stone2' : 'stone1', o.position, o.angle, light, 13);
    if (o.lifeTime > 5 && randInt(5) === 0) system.createEffect('chromeEnergy2', to, o.angle, new THREE.Vector3(0.15, 0.15, 0.4), 0);
    // On a character (the debuff's bolt): blue lights on its bones now and then.
    const owner = o.owner;
    const bones = owner?.boneCount?.() ?? 0;
    if (owner && bones > 0 && randInt(5) === 0) {
      for (let i = 0; i < bones; i++) {
        if (bones > 100 && randInt(bones) > bones / 10) continue;
        if (bones > 50 && randInt(bones) > bones / 5) continue;
        if (bones > 20 && randInt(bones) > bones / 2) continue;
        const position = owner.bonePosition?.(i);
        if (position) system.createParticle('light', position, new THREE.Vector3(), new THREE.Vector3(0.2, 0.2, 0.8), 5, 3 + (randInt(20) - 10) * 0.1);
      }
    }
    return true;
  },
};

// BITMAP_FIRE+1 SubType 0 (Fire Burst's trails): two ticks where it was made, glowing.
EXTRA_PARTICLES.fire2 = {
  texture: 'fire2',
  init: (o) => {
    o.lifeTime = 2;
    return o.subType === 0;
  },
  move: () => undefined,
};

// BITMAP_CHROME_ENERGY2 (the catalogue gives LifeTime 30 and a size of 1.5 cells): a flash on the ground that fades
// over its last ten ticks.
EXTRA_EFFECTS.chromeEnergy2 = {
  create: (o) => o.subType === 0,
  move: (o) => {
    if (o.lifeTime < 10) o.light.multiplyScalar(0.8);
    return true;
  },
  draw: (o, system) => {
    const size = o.startPosition.x > 0 ? o.startPosition.x : 1.5;
    system.quads.decal('chromeEnergy2', 'glow', o.position.x, o.position.y, size, o.light, 0, (x, y) => system.heightAt(x, y), o.position.z);
  },
};

// ---------------------------------------------------------------------------
// Flame Strike (MODEL_EFFECT_FLAME_STRIKE, Effect/FlameStrike.bmd): a burning blade held in the
// gladiator's right hand that follows its swing, fading in and out.
// ---------------------------------------------------------------------------

/** PLAYER_SKILL_FLAMESTRIKE. */
const FLAME_STRIKE_ACTION = 184;
const flameMatrix = new THREE.Matrix4();

EXTRA_EFFECTS.flameStrike = {
  model: ['Effect', 'FlameStrike.bmd'],
  create: (o) => {
    if (o.subType !== 0 || !o.owner) return false;
    o.alpha = 0;
    o.lifeTime = 35;
    o.velocity = o.owner.playSpeed?.(FLAME_STRIKE_ACTION) ?? 0.3;
    o.blendMesh = -2; // RENDER_BRIGHT: both meshes glow with Alpha
    o.kind = 0; // o->AI: the swing has passed
    return true;
  },
  move: (o, system) => {
    const owner = o.owner;
    if ((o.lifeTime < 20 && o.alpha < 0.1) || !owner) {
      o.live = false;
      return false;
    }
    if (o.lifeTime < 20) o.alpha -= 0.1;
    else if (o.alpha < 1) o.alpha += 0.1;
    o.blendMeshLight = o.alpha;
    const frame = owner.animationFrame?.() ?? 0;
    if (frame > 13 || o.kind === 1) o.kind = 1;
    else if (owner.currentAction?.() === FLAME_STRIKE_ACTION) o.animationFrame = frame;
    // The flame's second mesh scrolls through four rows of its texture.
    o.blendMeshTexCoordV = Math.trunc((Math.trunc(system.time * 0.05) % 16) / 4) * 0.25;
    return true;
  },
  draw: (o) => {
    // Its bones hang from the hand's bone (the owner's BoneTransform[33] as the parent matrix).
    const hand = o.owner?.boneMatrix?.(HAND_BONE);
    const model = o.model;
    if (!hand || !model) return;
    model.group.matrixAutoUpdate = false;
    model.group.matrix.copy(hand).multiply(flameMatrix.makeScale(o.scale, o.scale, o.scale));
    model.group.matrixWorldNeedsUpdate = true;
  },
};

// ---------------------------------------------------------------------------
// Fire Burst (MODEL_PIER_PART, Skill/PierPart.bmd): three flames shoot from the lord and swerve
// into the target, leaving burning trails (SubType 1); Critical Damage and Fire Burst light the
// lord's hands (MODEL_DARKLORD_SKILL, Skill/DarkLordSkill.bmd).
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.pierPart = {
  model: ['Skill', 'PierPart.bmd'],
  create: (o, system) => {
    if (o.subType === 0) {
      o.lifeTime = 20;
      o.gravity = 2;
      o.velocity = 10;
      o.hiddenMesh = 1;
      o.scale = 1.2;
      o.startPosition.copy(o.light); // the call's light is the aim's offset over the target
      o.light.set(1, 1, 1);
      o.direction.set(0, -26, 0);
      o.headAngle.copy(o.angle);
      o.angle.set(0, 0, o.angle.z);
      return o.owner !== null;
    }
    if (o.subType === 1) {
      o.lifeTime = o.skill; // the flame's life time (o->Owner->LifeTime)
      o.hiddenMesh = 0;
      o.scale = 0.5;
      o.alpha = (20 - o.lifeTime) / 5;
      // BITMAP_FIRE+1 SubType 0: CreateParticle has no case for it, so it keeps the common two ticks, unmoving.
      system.createParticle('fire2', o.position, o.angle, o.light, 0, 1, o.self);
      o.direction.set(0, 0, 0);
      return true;
    }
    return false;
  },
  move: (o, system) => {
    if (o.subType === 0) {
      const owner = o.owner;
      if (!owner) return true;
      const p = owner.position.clone().add(o.startPosition);
      for (let i = 1; i < o.gravity; i++) {
        if (randInt(2) === 0) o.angle.x += o.angle.x < -90 ? 20 : -20;
        moveHumming(o.position, o.angle, p, o.velocity);
        o.velocity += 0.4;
        if (o.lifeTime < 10) o.velocity += 0.1;
        o.position.add(rotateByAngle(o.direction, o.angle));
        system.createEffect('pierPart', o.position, o.angle, o.light, 1, o.self, 0, { skillIndex: o.lifeTime });
      }
      o.gravity += 0.1;
    } else if (o.lifeTime < 5) {
      o.alpha *= 1 / 1.3;
    }
    return true;
  },
};

// MODEL_DARKLORD_SKILL: a small burst turned 45 degrees at a hand for ten ticks.
EXTRA_EFFECTS.darkLordSkill = {
  model: ['Skill', 'DarkLordSkill.bmd'],
  create: (o) => {
    o.lifeTime = 10;
    o.scale = 0.2;
    o.velocity = 0.1;
    if (o.subType > 1) return false;
    let angle = 45 - 90 * o.subType;
    if (randInt(2) === 0) angle = o.subType ? 45 - 90 : 45;
    o.angle.set(45, angle, 0);
    return true;
  },
  move: () => true,
};

/** The height Fire Burst aims at over its target (to->BoundingBoxMax[2]; a man's height here). */
const FIRE_BURST_AIM = 150;

/** MoveCharacter's release for AT_SKILL_FIREBURST: three flames from in front of the lord into the target. */
export function fireBurst(system: EffectSystem, caster: EffectOwner, target: EffectOwner): void {
  const root = caster.boneMatrix?.(0);
  const position = root ? new THREE.Vector3(40, 0, 10).applyMatrix4(root) : caster.position.clone().setZ(caster.position.z + 10);
  const aim = new THREE.Vector3(0, 0, FIRE_BURST_AIM);
  system.createEffect('pierPart', position, new THREE.Vector3(0, 0, caster.angle.z + 90), aim, 0, target);
  aim.z -= FIRE_BURST_AIM / 2;
  system.createEffect('pierPart', position, new THREE.Vector3(0, 0, caster.angle.z), aim, 0, target);
  system.createEffect('pierPart', position, new THREE.Vector3(0, 0, caster.angle.z - 90), aim, 0, target);
  const light = new THREE.Vector3(1, 0.6, 0.3);
  system.createEffect('darkLordSkill', position, caster.angle, light, 0);
  system.createEffect('darkLordSkill', position, caster.angle, light, 1);
}

/** The release of Critical Damage: a burst at each hand that holds a weapon. */
export function criticalDamage(system: EffectSystem, caster: EffectOwner): void {
  const light = new THREE.Vector3(1, 0.6, 0.3);
  for (const hand of [0, 1] as const) {
    const at = caster.weaponPosition?.(hand);
    if (at) system.createEffect('darkLordSkill', at, caster.angle, light, hand);
  }
}
