// The Dark Lord's other skill effects (registered in extraEffects.ts's tables): Electric Spike, Earthshake (the
// dark horse's stomp), Chaotic Diseier and the Critical Damage buff's lasting sparks.
//
// Followed C++ (client/src/source):
//   MoveCharacter's release AT_SKILL_THUNDER_STRIKE, AttackStage (its and Earthshake's timing)
//                                                                          Engine/Object/ZzzCharacter.cpp
//   UpdateHeldWeaponEffects (eBuff_AddCriticalDamage: every 1.2 s)         Engine/Object/ZzzCharacter.cpp
//   ReceiveMagic AT_SKILL_CHAOTIC_DISEIER                                  Network/Server/WSclient.cpp
//   MoveMount MODEL_DARK_HORSE, UpdateDarkHorseSkill                       Engine/AI/GOBoid.cpp
//   CreateEffect / MoveEffect: BITMAP_FLARE_FORCE (0, 1), MODEL_GROUND_STONE(2), BITMAP_SHOCK_WAVE (0),
//   BITMAP_SHINY+6 (3), MODEL_FEATHER (2, 3); RenderEffects' BITMAP_SHOCK_WAVE decal    Render/Effects/ZzzEffect.cpp
//   Move_MODEL_GROUND_STONE, _BITMAP_SHOCK_WAVE                            Render/Effects/Behaviors/MoveHandlers.cpp
//   MoveDesair (MODEL_DESAIR)                                              Render/Effects/Behaviors/EffectBehaviors.cpp
//   RenderObject: MODEL_DESAIR and MODEL_FEATHER 2, 3 drawn RENDER_DARK    Engine/Object/ZzzObject.cpp
//   CreateJoint / MoveJoint / DescribeJoint: BITMAP_FLARE_FORCE (0-7), BITMAP_2LINE_GHOST (0), BITMAP_FLASH (0, 1, 6, 7)
//                                                                          Render/Effects/ZzzEffectJoint.cpp

import * as THREE from 'three';
import { createAngle, createAngle2D, moveHumming, randInt, rotateByAngle, turnAngle2 } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS, PREWARM_LOADS } from './extraEffects.js';
import type { Effect, EffectOwner, Joint } from './types.js';
import { computeBoneMatrices, loadModel, loadTexture } from '../../bmd.js';
import type { BMDModel } from '../../formats.js';

const WHITE = new THREE.Vector3(1, 1, 1);

function live(owner: EffectOwner | null): owner is EffectOwner {
  return owner !== null && (owner.isLive?.() ?? true);
}

/** CalcAddPosition: a point offset in the owner's frame (x right, -y ahead, z up). */
function calcAddPosition(owner: EffectOwner, x: number, y: number, z: number): THREE.Vector3 {
  return rotateByAngle(new THREE.Vector3(x, y, z), owner.angle).add(owner.position);
}

// ---------------------------------------------------------------------------
// Electric Spike (AT_SKILL_THUNDER_STRIKE): BITMAP_FLARE_FORCE SubType 0 lays a beam forward and four bolts that
// twist around it. SubType 1 (the Critical Damage buff) makes three sparks spiralling out of a weapon's bone.
// ---------------------------------------------------------------------------

EXTRA_EFFECTS.flareForce = {
  create: (o, system) => {
    const owner = o.owner;
    if (!owner) return false;
    if (o.subType === 0) {
      // Joint SubType 1 at 100, 0 (the beam) at 250, then 2, 3 and 4 at 100; the effect itself ends at once.
      for (const [sub, scale] of [[1, 100], [0, 250], [2, 100], [3, 100], [4, 100]]) {
        system.createJoint('flareForceJoint', o.position, o.position, o.angle, sub, owner, scale);
      }
      return false;
    }
    if (o.subType === 1) {
      // PKKey: 1 for a bow (the sparks don't drift); SkillIndex: the weapon's bone.
      for (const sub of [5, 6, 7]) system.createJoint('flareForceJoint', o.position, o.position, o.angle, sub, owner, 20, o.skill, o.pkKey);
      return false;
    }
    return false;
  },
  move: () => true,
};

// The joint's MultiUse is kept in `skill` for SubTypes 0-4 (they have no SkillIndex) and the bone in `skill` for
// 5-7; Weapon (a start delay, the spark's length) in `counter`.
EXTRA_JOINTS.flareForceJoint = {
  init: (o) => {
    o.lifeTime = 20;
    o.createTails = false;
    o.maxTails = 30;
    o.velocity = -3;
    o.position.z += 150;
    o.direction.set(0, 0, 0);
    o.targetPosition.set(0, 0, 0);
    o.light.set(1, 0.8, 1);
    o.startPosition.copy(o.position);
    o.counter = 0;
    if (o.subType === 0) {
      o.skill = 1;
      o.texture = 'jointThunder';
      return true;
    }
    if ((o.subType >= 1 && o.subType <= 4) || (o.subType >= 11 && o.subType <= 13)) {
      o.skill = 1;
      o.texture = 'jointThunder';
      o.targetPosition.set(80, 180, 0);
      o.counter = randInt(3) + 2;
      // 11-13: Plasma Storm's bolts over the Fenrir, in the colour of the Fenrir (black, red, blue).
      if (o.subType === 11) o.light.set(0.7, 1, 0.7);
      else if (o.subType === 12) o.light.set(1, 0.6, 0.6);
      else if (o.subType === 13) o.light.set(0.7, 0.7, 1);
      if (o.subType === 2 || o.subType === 4 || o.subType >= 11) o.targetPosition.y = -180;
      if (o.subType === 3 || o.subType === 4) o.counter = 0;
      o.lifeTime += o.counter;
      const p = rotateByAngle(new THREE.Vector3(0, 0, o.targetPosition.x), o.targetPosition);
      o.position.copy(o.startPosition).add(p);
      return true;
    }
    if (o.subType >= 5 && o.subType <= 7) {
      if (!o.target) return false;
      o.lifeTime = 15;
      o.maxTails = 1;
      o.counter = 30;
      o.velocity = 0;
      o.targetPosition.set(30, 0, o.subType * 90);
      o.direction.set(0, -4, 0);
      if (o.pkKey !== -1) o.direction.y = 0;
      o.texture = 'firecracker';
      o.numTails = -1; // no start tail
      return true;
    }
    return false;
  },
  move: (o, system) => {
    if (o.subType >= 5) {
      moveCriticalSpark(o, system);
      return;
    }
    if (o.numTails < o.maxTails - 1) {
      if (o.subType === 0) {
        for (let i = 1; i < o.skill; i++) {
          system.layTail(o, o.angle);
          o.startPosition.add(rotateByAngle(new THREE.Vector3(0, o.velocity, 0), o.angle));
          o.position.copy(o.startPosition);
          o.velocity -= 2;
        }
        o.skill += 2;
      } else if (o.counter <= 0) {
        for (let i = 1; i < o.skill; i++) {
          system.layTail(o, o.angle);
          o.startPosition.add(rotateByAngle(new THREE.Vector3(0, o.velocity, 0), o.angle));
          o.targetPosition.y -= 20;
          const p = rotateByAngle(new THREE.Vector3(0, 0, o.targetPosition.x), new THREE.Vector3(0, o.targetPosition.y, o.angle.z));
          if (o.subType === 3 || o.subType === 4) o.position.copy(o.startPosition).sub(p);
          else o.position.copy(o.startPosition).add(p);
          o.velocity -= 2;
          o.targetPosition.x -= 2.5;
        }
        o.skill += 2;
      } else {
        o.counter -= 1;
      }
    }
    if (o.lifeTime < 10) o.light.multiplyScalar(1 / 1.3);
    if (o.subType === 0 || o.subType === 1 || o.subType === 2) {
      const luminosity = (randInt(4) + 8) * 0.03;
      system.groundLight(o.position, luminosity * 0.4, luminosity * 0.8, luminosity, 2);
    }
  },
  draw: (o, system) => {
    if (o.subType >= 5) return false;
    // DescribeJoint: the texture scrolls along a half-length trail; the faces fade towards the end.
    const scroll = (Math.floor(system.time) % 1000) * 0.001;
    const half = Math.trunc((o.maxTails - 1) / 2);
    for (let j = 0; j < o.numTails; j++) {
      const current = o.tails[j], next = o.tails[j + 1];
      if (!current || !next) continue;
      const l1 = (o.numTails - j) / half - scroll, l2 = (o.numTails - (j + 1)) / half - scroll;
      const luminosity = (o.numTails - 1 - j) / o.maxTails * 2;
      const light = o.light.clone().multiplyScalar(luminosity);
      system.quads.quad(o.texture, 'glow', [current[2], current[3], next[3], next[2]], [l1, l1, l2, l2], [1, 0, 0, 1], light);
      system.quads.quad(o.texture, 'glow', [current[0], current[1], next[1], next[0]], [l1, l1, l2, l2], [0, 1, 1, 0], light);
    }
    return true;
  },
};

// SubTypes 5-7: rebuilt every tick from the weapon's bone, a spiral of tails (one more each tick, at most 30) with a
// flare at every other one; the last ticks fade.
function moveCriticalSpark(o: Joint, system: EffectSystem): void {
  const owner = o.target;
  const bone = owner && live(owner) ? owner.boneMatrix?.(o.skill) : null;
  if (!owner || !bone) {
    o.live = false;
    return;
  }
  const rotation = new THREE.Matrix4().extractRotation(bone);
  const root = owner.boneMatrix?.(0);
  const tailMatrix = root ? new THREE.Matrix4().extractRotation(root) : rotation;
  const direction = o.direction.clone();
  o.startPosition.copy(new THREE.Vector3(0, 20, 0).applyMatrix4(bone));
  o.position.copy(o.startPosition);
  o.numTails = -1;
  const maxTails = Math.min(o.maxTails, o.counter);
  o.maxTails = maxTails;
  o.tails.length = Math.max(o.tails.length, 32);
  o.targetPosition.y = o.targetPosition.z;
  for (let i = 0; i < maxTails; i++) {
    o.startPosition.add(direction.clone().applyMatrix4(rotation));
    o.targetPosition.y += o.subType % 2 ? 40 : -40;
    const p = rotateByAngle(new THREE.Vector3(0, 0, o.targetPosition.x), new THREE.Vector3(o.targetPosition.y, 0, 0));
    o.position.copy(o.startPosition).add(p);
    system.layTailMatrix(o, tailMatrix);
    o.targetPosition.x -= 0.15;
    if (o.pkKey === -1) direction.y -= 0.1;
    if (i % 2 === 0) system.createSprite('flare', o.startPosition, o.light.x / 2, o.light);
  }
  o.maxTails++;
  if (o.lifeTime < 7) o.light.multiplyScalar(1 / 1.5);
}

/** Electric Spike's release (MoveCharacter AT_SKILL_THUNDER_STRIKE): 90 ahead of the lord and 50 down (40 / 80 up mounted). */
export function electricSpike(system: EffectSystem, caster: EffectOwner): void {
  const position = calcAddPosition(caster, 0, -90, -50);
  const action = caster.currentAction?.();
  if (action === LordAction.RIDE_ATTACK_FLASH) position.z += 80;
  else if (action === LordAction.FENRIR_ATTACK_DARKLORD_FLASH) position.z += 40;
  system.createEffect('flareForce', position, caster.angle, caster.light ?? WHITE, 0, caster);
}

/** The lord's actions the timings read (PLAYER_SKILL_FLASH, PLAYER_ATTACK_DARKHORSE, ...). */
export const LordAction = { SKILL_FLASH: 152, RIDE_ATTACK_FLASH: 85, FENRIR_ATTACK_DARKLORD_FLASH: 95, ATTACK_DARKHORSE: 87 } as const;

/** eBuff_AddCriticalDamage: every 1.2 s sparks spiral out of the weapons in hand, now and then with the glow. */
export const CRITICAL_DAMAGE_BUFF = 5;
const CRITICAL_SPARK_INTERVAL = 1200;
const lastCriticalSpark = new WeakMap<EffectOwner, number>();

/**
 * UpdateHeldWeaponEffects: while the owner has the Critical Damage buff (and isn't cloaked), every 1.2 s each held
 * weapon (not arrows, bolts or a shield) throws BITMAP_FLARE_FORCE SubType 1 at its bone, and one time in twenty the
 * hands' glow (MODEL_DARKLORD_SKILL).
 */
export function criticalDamageSparks(system: EffectSystem, owner: EffectOwner, weapons: readonly { bone: number; bow: boolean }[]): void {
  const last = lastCriticalSpark.get(owner) ?? -Infinity;
  if (last >= system.time - CRITICAL_SPARK_INTERVAL) return;
  lastCriticalSpark.set(owner, system.time);
  const wave = randInt(20) === 0;
  let bow = false;
  weapons.forEach((weapon, hand) => {
    const position = owner.bonePosition?.(weapon.bone);
    if (!position) return;
    if (weapon.bow) bow = true;
    system.createEffect('flareForce', position, owner.angle, owner.light ?? WHITE, 1, owner, 0, { pkKey: bow ? 1 : -1, skillIndex: weapon.bone });
    if (wave) system.createEffect('darkLordSkill', position, owner.angle, new THREE.Vector3(1, 0.6, 0.3), hand);
  });
}

// ---------------------------------------------------------------------------
// Earthshake (AT_SKILL_EARTHSHAKE): the dark horse rears and stamps (its action 3 while the lord plays
// PLAYER_ATTACK_DARKHORSE): a shock wave every 10 ticks, at its 19th tick the strike's cracks (MODEL_SKILL_FURY_STRIKE
// with Skill 2: no weapon, no flash), and while the stamp's keys 8-9.5 play rings of ground stones and the camera
// shake.
// ---------------------------------------------------------------------------

/** The horse's attack plays 0.34 keys a tick (o->Velocity); its stamp is keys 8 to 9.5. */
const HORSE_KEYS_PER_TICK = 0.34;
const HORSE_ATTACK_TICKS = 32;
const SHOCK_WAVE_TICKS = 10;

EXTRA_EFFECTS.darkHorseSkill = {
  create: (o) => {
    o.lifeTime = HORSE_ATTACK_TICKS;
    o.counter = 0; // o->WeaponLevel of the horse
    o.distance = 0; // the ticks since the last shock wave
    return o.owner !== null;
  },
  move: (o, system) => {
    const owner = o.owner;
    if (!live(owner)) {
      o.live = false;
      return false;
    }
    o.position.copy(owner.position);
    o.angle.copy(owner.angle);
    const frame = (HORSE_ATTACK_TICKS - o.lifeTime) * HORSE_KEYS_PER_TICK;
    o.counter += 1;
    if (o.distance <= 0) {
      system.createEffect('shockWave', o.position, o.angle, owner.light ?? WHITE, 0);
      o.distance = SHOCK_WAVE_TICKS;
    }
    o.distance -= 1;
    if (frame >= 8 && frame <= 9.5) {
      if (o.counter % 2 === 1) {
        const p = new THREE.Vector3(0, 150 * Math.trunc(o.counter / 2), 0);
        const angle = new THREE.Vector3(0, 0, randInt(360));
        for (let i = 0; i < 6; i++) {
          angle.z += 60;
          const position = rotateByAngle(p, angle).add(o.position);
          system.createEffect(randInt(2) ? 'groundStone2' : 'groundStone', position, o.angle, owner.light ?? WHITE);
        }
      }
      system.setEarthQuakeAt(o.position, (randInt(3) - 3) * 0.7);
    } else if (o.counter === 19) {
      system.createEffect('skillFuryStrike', o.position, o.angle, owner.light ?? WHITE, 0, owner, 0, { kind: 2 });
      o.counter = -3;
    }
    return false; // the shared tail counts the life time down
  },
};

// BITMAP_SHOCK_WAVE SubType 0: a ring on the ground 20 cells wide that shrinks by one a tick.
EXTRA_EFFECTS.shockWave = {
  create: (o) => {
    if (o.subType === 14) { // Expansion of Wizardry's ring around the wizard (the call's scale): it fades in and out
      o.lifeTime = 30;
      o.alpha = 0;
      o.pkKey = 0;
      o.startPosition.copy(o.light); // EyeRight: the full colour
      return o.owner !== null;
    }
    if (o.subType === 4) { // where Starfall's flashes land: a small ring that grows
      o.lifeTime = 10;
      o.scale = (randInt(6) + 6) / 10;
      return true;
    }
    if (o.subType !== 0) return false;
    o.lifeTime = 30;
    o.scale = 20;
    return true;
  },
  move: (o) => {
    if (o.subType === 14) {
      twilightRing(o, 0.15);
      return true;
    }
    if (o.subType === 4) {
      o.scale += 0.3;
      return true;
    }
    o.scale -= 1;
    return true;
  },
  draw: (o, system) => {
    system.quads.decal('shockWave', 'glow', o.position.x, o.position.y, o.scale, o.light, -o.angle.z, (x, y) => system.heightAt(x, y), o.position.z);
  },
};

// MODEL_GROUND_STONE / 2: a slab breaks out of the ground (its one action, 0.3 keys a tick), throws up dust and
// stones at its LifeTime 33-36 and fades at the end; the ground under it glows.
function groundStone(file: string, baseScale: number): NonNullable<typeof EXTRA_EFFECTS.groundStone> {
  return {
    model: ['Skill', file],
    create: (o) => {
      o.lifeTime = 40;
      o.scale = baseScale + randInt(30) / 100;
      o.velocity = 0.3;
      o.angle.z = randInt(360);
      return true;
    },
    move: (o, system) => {
      if (o.lifeTime > 32 && o.lifeTime < 37) {
        const position = new THREE.Vector3(o.position.x + 60, o.position.y - 60, o.position.z + 50);
        system.createParticle('smoke', position, o.angle, new THREE.Vector3(1, 0.8, 0.6), 11, 2, o.self);
        system.createEffect(randInt(2) ? 'stone2' : 'stone1', position, o.angle, o.light, 10);
      }
      if (o.lifeTime < 8) {
        o.alpha /= 1.3;
        o.blendMeshLight /= 2;
      }
      system.groundLight(o.position, 0.79, 0.72, 0.49, 2);
      return true;
    },
  };
}
EXTRA_EFFECTS.groundStone = groundStone('GroundStone.bmd', 1.2);
EXTRA_EFFECTS.groundStone2 = groundStone('GroundStone2.bmd', 1);

/** Earthshake's look: the dark horse's stomp around the lord. */
export function earthshake(system: EffectSystem, caster: EffectOwner): void {
  system.createEffect('darkHorseSkill', caster.position, caster.angle, caster.light ?? WHITE, 0, caster);
}

// ---------------------------------------------------------------------------
// Chaotic Diseier (AT_SKILL_CHAOTIC_DISEIER): dark shines at the lord's bones, eight ghost trails (BITMAP_2LINE_GHOST)
// sweep forward trailing dark smoke, most of them carrying a winged shadow (MODEL_DESAIR) that sheds feathers; the
// hero's own cast bursts at the selected target (CreateBomb).
// ---------------------------------------------------------------------------

/** ReceiveMagic AT_SKILL_CHAOTIC_DISEIER (and the hero's own cast). */
export function chaoticDiseier(system: EffectSystem, caster: EffectOwner, target: EffectOwner | null, isHero: boolean): void {
  const light = new THREE.Vector3(0.5, 0.5, 0.5);
  for (let i = 0; i < 5; i++) system.createEffect('shiny5', caster.position, caster.angle, light, 3, caster, 0.5);
  for (let i = 0; i < 8; i++) {
    const position = new THREE.Vector3(caster.position.x - 119 + randInt(240), caster.position.y, caster.position.z + 49 + randInt(60));
    system.createJoint('twoLineGhost', position, caster.position, caster.angle, 0, caster, 20);
  }
  if (isHero && target) system.createBomb(target.position, true);
}

// BITMAP_2LINE_GHOST SubType 0: flies ahead at 40-60 a tick, weaving left and right every 8 ticks, trailing dark
// smoke; subtracted (RENDER_TYPE_ALPHA_BLEND_MINUS).
EXTRA_JOINTS.twoLineGhost = {
  init: (o, system) => {
    if (o.subType === 1) { // Expansion of Wizardry's purple streaks: from 300 around, curving in to the wizard
      o.lifeTime = 25 + randInt(10);
      o.maxTails = 15;
      o.velocity = 20 + randInt(10);
      o.targetPosition.copy(o.position);
      const around = rotateByAngle(new THREE.Vector3(0, 300, 0), new THREE.Vector3(0, 0, randInt(360)));
      o.position.x += around.x;
      o.position.y += around.y;
      o.position.z = randInt(100) + 200;
      o.targetPosition.z = o.position.z - (randInt(100) - 100) * (randInt(2) === 0 ? 1 : -1);
      o.direction.copy(o.targetPosition).sub(o.position);
      o.angle.z = createAngle2D(o.position, o.targetPosition);
      return true;
    }
    if (o.subType !== 0) return false;
    o.blend = 'subtract';
    o.velocity = 40 + randInt(20);
    o.lifeTime = randInt(2) === 0 ? 67 : 75;
    o.maxTails = 26;
    o.scale = o.scale + randInt(200) + 1;
    o.direction.x = 0;
    if (randInt(3) < 2) {
      o.light.set(0.5, 0.5, 0.5);
      system.createEffect('desair', o.position, o.angle, o.light, 0, null, 1, { joint: o });
    }
    return true;
  },
  move: (o, system) => {
    if (o.subType === 1) {
      const heading = createAngle2D(o.position, o.targetPosition);
      o.angle.z = heading - 65;
      const distance = Math.hypot(o.position.x - o.targetPosition.x, o.position.y - o.targetPosition.y);
      if (distance > 60) o.angle.x = turnAngle2(o.angle.x, 360 - createAngle(o.position.z, distance, o.targetPosition.z, 0), 3);
      else o.angle.z = heading - 90;
      return;
    }
    o.angle.z += Math.trunc(o.lifeTime) % 16 <= 7 ? 10 : -10;
    system.createParticle('smoke', o.position, o.angle, o.light, 59, 1);
  },
};

// MODEL_DESAIR (the catalogue gives LifeTime 52, Scale 1.4): rides its ghost trail, shedding two feathers every 10
// ticks; drawn dark (RENDER_DARK) in white light.
EXTRA_EFFECTS.desair = {
  model: ['Skill', 'desair.bmd'],
  create: (o) => {
    if (!o.joint) return false;
    o.blendMesh = -2;
    o.blendMeshLight = 1;
    o.renderType = 'dark';
    return true;
  },
  move: (o, system) => {
    const joint = o.joint;
    o.light.set(1, 1, 1);
    if (!joint?.live) return true;
    o.position.copy(joint.position);
    o.angle.copy(joint.angle);
    if (Math.trunc(o.lifeTime) % 10 === 0) {
      system.createEffect('feather', o.position, o.angle, o.light, 2, null, 1.4);
      system.createEffect('feather', o.position, o.angle, o.light, 3, null, 1.4);
    }
    return true;
  },
};

// MODEL_FEATHER SubTypes 2 and 3 (Skill/darkwing_hetachi.bmd): scattered around where they fall off, they tumble,
// fade and (2 and 3) shrink; drawn dark.
const featherSpin = new WeakMap<Effect, THREE.Vector3>();
EXTRA_EFFECTS.feather = {
  model: ['Skill', 'darkwing_hetachi.bmd'],
  create: (o) => {
    if (o.subType !== 2 && o.subType !== 3) return false;
    const origin = o.position.clone();
    o.position.x += (randInt(20) - 10) * 4;
    o.position.y += (randInt(20) - 10) * 4;
    o.position.z += (randInt(20) - 10) * 4;
    o.direction.copy(origin).sub(o.position).normalize();
    o.scale = o.scale + (randInt(20) - 10) * (o.scale * 0.03);
    o.lifeTime = 100;
    o.alpha = 0.6 + randInt(10) * 0.02;
    o.gravity = 0;
    o.angle.set(randInt(360), randInt(360), randInt(360));
    featherSpin.set(o, new THREE.Vector3(randInt(10) - 5, randInt(10) - 5, randInt(10) - 5));
    o.blendMesh = -2;
    o.blendMeshLight = 1;
    o.renderType = 'dark';
    return true;
  },
  move: (o) => {
    o.light.multiplyScalar(0.97);
    o.scale *= 0.97;
    o.alpha *= 0.97;
    const spin = featherSpin.get(o);
    if (spin) o.angle.add(spin);
    return true;
  },
};

// BITMAP_SHINY+6 SubType 3 (the catalogue gives LifeTime 24, subtracted): a dark shine at a random bone of the owner,
// 20 ahead of it, every tick.
export function chaoticShine(o: Effect, system: EffectSystem): void {
  const owner = o.owner;
  if (!owner) return;
  const bones = owner.boneCount?.() ?? 0;
  const bone = bones > 0 ? owner.bonePosition?.(randInt(bones)) : null;
  const at = bone ?? owner.position.clone().setZ(owner.position.z + 100);
  const position = rotateByAngle(new THREE.Vector3(0, -20, 0), owner.angle).add(at);
  system.createSprite('shiny5', position, 2, o.light, o.self, 0, 1);
}

// ---------------------------------------------------------------------------
// Stun, Cancel Stun, Swell Mana, Invisibility, Cancel Invisibility (the Castle Siege skills).
// Followed: MoveCharacter's release AT_SKILL_STUN, _REMOVAL_STUN, _MANA, _INVISIBLE, _REMOVAL_INVISIBLE
// (Engine/Object/ZzzCharacter.cpp); CreateJoint / MoveJoint / DescribeJoint BITMAP_FLASH (0, 1, 6, 7)
// (Render/Effects/ZzzEffectJoint.cpp).
// ---------------------------------------------------------------------------

/** BITMAP_FLASH (Flashing.jpg): 0 and 1 fall from 1200 over a target and flare where they land; 7 opens three 6s,
 * bolts that arc forward from the lord's chest and bounce along the ground (CreateTailAxis on the y axis). */
EXTRA_JOINTS.flash = {
  init: (o, system) => {
    if (o.subType === 4) { // rides its target (Starfall's arrow) as a blue trail
      if (!o.target) return false;
      o.maxTails = 20;
      o.lifeTime = 30;
      o.velocity = 0;
      o.texture = 'flareBlue';
      o.light.set(1, 1, 1);
      o.direction.set(0, 0, 0);
      o.position.copy(o.target.position);
      o.angle.copy(o.target.angle);
      return true;
    }
    if (o.subType <= 3) {
      if (o.subType <= 1) void shadowBody();
      o.maxTails = 10;
      o.lifeTime = 40;
      o.velocity = o.subType >= 2 ? 30 : 70;
      o.counter = o.subType >= 2 ? 300 : 150; // MultiUse: how far under the ground it may go
      if (o.subType === 2) o.texture = 'flareForce';
      else if (o.subType === 3) o.texture = 'flareBlue';
      o.pkKey = 0;
      o.angle.set(90, 0, 0);
      o.createTails = false;
      flashHeads.set(o, o.angle.clone());
      return true;
    }
    if (o.subType === 6) {
      o.maxTails = 30;
      o.lifeTime = 25;
      o.counter = 25; // Weapon
      o.velocity = (70 + randInt(3)) * Math.PI / 180;
      o.light.set(0.8, 0.8, 1);
      o.direction.set(0, -(40 + randInt(4)), Math.sin(o.velocity) * 10);
      o.position.y -= 20;
      o.position.z += 130;
      o.startPosition.copy(o.position);
      o.createTails = false;
      o.numTails = -1;
      layTailAxisY(o);
      o.numTails = 0;
      return true;
    }
    if (o.subType === 7) {
      for (let i = 0; i < 3; i++) {
        const p = rotateByAngle(new THREE.Vector3((i - 1) * 100, -50, 0), o.angle).add(o.position);
        system.createJoint('flash', p, p, o.angle, 6, null, 40);
      }
      return false; // LifeTime 0: the opener itself ends at once
    }
    return false;
  },
  move: (o, system) => {
    if (o.subType === 6) {
      o.direction.y += randInt(5) - 2;
      o.direction.z -= 10;
      o.position.add(rotateByAngle(o.direction, o.angle));
      if (o.counter - o.lifeTime < 10) {
        // The burst where the bolt leaves the lord (BITMAP_EXPLOTION+1).
        system.createParticle('explosion2', o.startPosition, o.angle, o.light, 0, 3);
        o.scale = 100;
      } else {
        o.scale = 40;
      }
      const height = system.heightAt(o.position.x, o.position.y);
      if (o.position.z <= height) {
        // Where it touches the ground: a short bolt rises and smoke puffs up.
        system.createJoint('jointThunder', o.position, o.position, o.angle, 4, null, 60);
        if (o.direction.z <= 0) o.direction.z = Math.sin(o.velocity) * 20;
        o.position.z = height + 5;
        system.createParticle('advSmoke2', o.position.clone().setZ(o.position.z + 30), o.angle, o.light, 2, 3);
      }
      layTailAxisY(o);
      if (o.lifeTime < o.counter / 2) o.light.multiplyScalar(1 / 1.3);
      return;
    }
    if (o.subType === 4) {
      if (!o.target || !(o.target.isLive?.() ?? true)) {
        o.live = false;
        return;
      }
      o.position.copy(o.target.position);
      if (o.lifeTime < 15) o.light.multiplyScalar(1 / 1.3);
      return;
    }
    // SubTypes 0 to 3: the common move along the angle, then again along the head (each 10 faster first).
    if (o.subType === 2) o.angle.z += randInt(10) + 10;
    o.velocity += 10;
    if (o.pkKey === 0 && o.numTails < o.maxTails) system.layTail(o, o.angle);
    o.velocity += 10;
    o.position.add(rotateByAngle(new THREE.Vector3(0, -o.velocity, 0), flashHeads.get(o) ?? o.angle));
    const floor = system.heightAt(o.position.x, o.position.y) - o.counter;
    if (o.position.z < floor) {
      // The first touch: shards of skin fly off the target (SpawnMeshEffects(0, MODEL_SKIN_SHELL, SubType)); Starfall's
      // flashes leave a ring.
      if (o.pkKey === 0 && o.subType <= 1 && o.target) skinShells(system, o.target, o.subType);
      if (o.pkKey === 0 && o.subType >= 2) system.createEffect('shockWave', o.position, o.angle, new THREE.Vector3(0.3, 0.8, 1), 4);
      o.position.z = floor;
      o.pkKey = 1; // no more tails (m_bCreateTails = false)
      o.light.multiplyScalar(1 / 1.2);
    }
    if (o.subType <= 1) system.createSprite('flare', o.position, 1, o.light, null, randInt(360));
    else system.createSprite('shiny3', o.position, 1.6, new THREE.Vector3(0, 1, 1), null, randInt(360));
  },
  draw: (o, system) => {
    if (o.subType !== 6) return false;
    // m_byReverseUV 3: the texture runs from the head (1) to the end of the trail (0).
    for (let j = 0; j < o.numTails; j++) {
      const current = o.tails[j], next = o.tails[j + 1];
      if (!current || !next) continue;
      const l1 = 1 - j / (o.maxTails - 1), l2 = 1 - (j + 1) / (o.maxTails - 1);
      system.quads.quad(o.texture, 'glow', [current[2], current[3], next[3], next[2]], [l1, l1, l2, l2], [1, 0, 0, 1], o.light);
      system.quads.quad(o.texture, 'glow', [current[0], current[1], next[1], next[0]], [l1, l1, l2, l2], [0, 1, 1, 0], o.light);
    }
    return true;
  },
};
const flashHeads = new WeakMap<Joint, THREE.Vector3>();

// MODEL_FENRIR_SKILL_THUNDER (Plasma Storm): a bolt from over the Fenrir's head that zigzags to its target, rebuilt
// every tick; 0-3 thunder, 4-7 flashes, by the Fenrir's colour (black, red, blue, gold).
const FENRIR_LIGHTS: [number, number, number][] = [
  [0.7, 1, 0.7], [1, 0.6, 0.6], [0.7, 0.7, 1], [0.9, 0.9, 0.3], [0.1, 0.8, 0.1], [1, 0.3, 0.2], [0.2, 0.3, 1], [0.8, 0.8, 0.1],
];
EXTRA_JOINTS.fenrirSkillThunder = {
  init: (o) => {
    const light = FENRIR_LIGHTS[o.subType];
    if (!light) return false;
    o.maxTails = 50;
    o.velocity = 50;
    o.lifeTime = 20;
    o.texture = o.subType <= 3 ? 'jointThunder' : 'flash';
    o.light.set(...light);
    return true;
  },
  move: (o, system) => {
    for (let j = 0; j < o.maxTails; j++) {
      if (o.target) o.targetPosition.copy(o.target.position).setZ(o.target.position.z + 80);
      const distance = moveHumming(o.position, o.angle, o.targetPosition, 50);
      o.direction.x = (randInt(1024) - 512) / o.scale;
      o.direction.z = (randInt(1024) - 512) / o.scale;
      const angle = o.angle.clone().add(o.direction);
      system.layTail(o, angle);
      if (distance < o.velocity * 1.5) break;
      if (o.scale >= 50) {
        const l = (randInt(4) + 4) * 0.04;
        system.groundLight(o.position, l * 0.4, l * 0.4, l * 0.4, 2);
      }
      o.position.add(rotateByAngle(new THREE.Vector3(0, -o.velocity, 0), angle));
    }
  },
};

/**
 * Plasma Storm's release (on a Fenrir): two pairs of bolts from 130 over the Fenrir's head to the target, and six
 * flickering bolts over it. `fenrir`: 0 black, 1 red (the plain horn), 2 blue, 3 gold.
 */
export function plasmaStorm(system: EffectSystem, caster: EffectOwner, target: EffectOwner, fenrir = 1): void {
  const head = (): THREE.Vector3 => rotateByAngle(new THREE.Vector3(0, -140, 130), caster.angle).add(caster.position);
  for (let j = 0; j < 2; j++) {
    const angle = new THREE.Vector3(randInt(360), 0, randInt(360));
    system.createJoint('fenrirSkillThunder', head(), target.position, angle, fenrir, target, 100);
    system.createJoint('fenrirSkillThunder', head(), target.position, angle, 3 + fenrir, target, 80);
  }
  for (let k = 0; k < 6; k++) {
    const p = rotateByAngle(new THREE.Vector3(0, 10 + randInt(40) - 20, 130), caster.angle).add(caster.position);
    system.createJoint('flareForceJoint', p, p, new THREE.Vector3(randInt(360), 0, randInt(360)), 11 + fenrir, null, 60);
  }
}

// MODEL_ARROW_IMPACT (Starfall): a glowing arrow over the elf that tips back and flies off with a blue trail; as it
// ends, two flashes fall from 1200 over the target (its owner).
EXTRA_EFFECTS.arrowImpact = {
  model: ['Skill', 'ArrowImpact.bmd'],
  create: (o, system) => {
    const caster = o.target;
    if (!o.owner || !caster) return false;
    o.velocity = 1;
    o.lifeTime = 20;
    o.scale = 1.8;
    o.blendMesh = -2;
    o.direction.set(0, -30, 0);
    o.light.set(0.3, 0.8, 1);
    o.position.copy(caster.position).add(rotateByAngle(new THREE.Vector3(-10, -80, 200), caster.angle));
    o.angle.copy(caster.angle);
    o.angle.x = -30;
    system.createJoint('flash', o.position, o.position, o.angle, 4, o.self, 40, 50);
    return true;
  },
  move: (o, system) => {
    o.angle.x -= 5;
    o.direction.y -= 8;
    if (o.lifeTime < 2 && o.subType === 0 && o.owner) {
      o.angle.x = 90;
      o.subType = 1;
      const p = o.owner.position.clone();
      p.x += randInt(100) - 50;
      p.y += randInt(100) - 50;
      p.z += 1200;
      system.createJoint('flash', p, p, o.angle, 2, o.self, 50);
      system.createJoint('flash', p, p, o.angle, 3, o.self, 50);
    } else if (o.subType === 1) {
      o.live = false;
    }
    return true;
  },
};

/** Starfall's release: the arrow from the elf, falling on the target. */
export function starfall(system: EffectSystem, caster: EffectOwner, target: EffectOwner): void {
  system.createEffect('arrowImpact', caster.position, caster.angle, caster.light ?? WHITE, 0, target, 0, { target: caster });
}

// MODEL_SHADOW_BODY (Player/Shadow01.bmd) at its first key: the body the cancels' skin shards come off.
let shadow: { model: BMDModel; bones: THREE.Matrix4[] } | null = null;
let shadowLoad: Promise<void> | null = null;
function shadowBody(): Promise<void> {
  shadowLoad ??= loadModel('Player', 'Shadow01.bmd').then((model) => { shadow = { model, bones: computeBoneMatrices(model, 0, 0) }; })
    .catch(() => { /* no shards */ });
  return shadowLoad;
}

// Loaded when the effects are warmed (EffectSystem.prewarm), so the first cancel finds it.
PREWARM_LOADS.push(shadowBody);

/** b->SpawnMeshEffects(0, MODEL_SKIN_SHELL, sub): one shard in eight of mesh 0's corners, on the target's body. */
function skinShells(system: EffectSystem, target: EffectOwner, sub: number): void {
  const mesh = shadow?.model.meshes[0];
  if (!shadow || !mesh) return;
  const turn = new THREE.Vector3(0, 0, target.angle.z);
  const zero = new THREE.Vector3();
  const white = new THREE.Vector3(1, 1, 1);
  for (const triangle of mesh.triangles) {
    for (let k = 0; k < triangle.corners; k++) {
      if (randInt(8) !== 0) continue;
      const vertex = mesh.vertices[triangle.vertex[k]];
      if (!vertex) continue;
      const p = new THREE.Vector3(...vertex.position).applyMatrix4(shadow.bones[vertex.node] ?? new THREE.Matrix4());
      system.createEffect('skinShell', rotateByAngle(p, turn).add(target.position), zero, white, sub);
    }
  }
}

// MODEL_SKIN_SHELL: a shard thrown up and out that tumbles, bounces on the ground and fades; drawn in the
// BITMAP_CHROME texture, grey (0) or glowing blue (1).
let chrome: THREE.Texture | null = null;
EXTRA_EFFECTS.skinShell = {
  model: ['Skill', 'SkinShell.bmd'],
  create: (o) => {
    if (o.subType !== 0 && o.subType !== 1) return false;
    void loadTexture('Effect', 'Chrome01.jpg').then((map) => { chrome = map; });
    o.position.z += 50;
    o.lifeTime = randInt(16) + 32;
    o.scale = (randInt(3) + 3) * 0.1;
    o.alpha = 0.5;
    o.direction.copy(rotateByAngle(new THREE.Vector3(0, (randInt(128) + 32) * 0.1, 0), new THREE.Vector3(0, 0, randInt(360))));
    o.gravity = randInt(5) + 2 + randInt(5);
    o.angle.set(randInt(360), randInt(360), randInt(360));
    if (o.subType === 0) o.light.set(0.5, 0.5, 0.5);
    else {
      o.light.set(0.1, 0.5, 1);
      o.blendMesh = -2; // RENDER_BRIGHT
      o.blendMeshLight = 1;
    }
    return true;
  },
  move: (o, system) => {
    // Every copy of the shard model draws in the chrome texture (only this effect uses it).
    if (o.model && chrome && o.model.parts[0]?.mesh.material.map?.image !== chrome.image) {
      for (const part of o.model.parts) {
        part.mesh.material.map = chrome;
        part.mesh.material.needsUpdate = true;
      }
    }
    o.position.add(o.direction);
    o.direction.multiplyScalar(0.9);
    o.position.z += o.gravity;
    o.direction.multiplyScalar(1.1);
    o.gravity -= 3;
    const height = system.heightAt(o.position.x, o.position.y);
    if (o.position.z < height) {
      o.position.z = height;
      o.gravity = -o.gravity * 0.2;
      o.lifeTime -= 5;
      o.angle.x -= o.scale * 128;
    } else {
      o.angle.x -= o.scale * 16;
    }
    o.alpha = o.lifeTime / 10;
    return true;
  },
};

/** CreateTailAxis(o, AngleMatrix(o->Angle), 1): a new tail whose second pair of corners lies along y. */
function layTailAxisY(o: Joint): void {
  o.numTails = Math.min(o.numTails + 1, o.maxTails - 1);
  for (let j = o.numTails - 1; j >= 0; j--) o.tails[j + 1] = o.tails[j];
  const half = o.scale * 0.5;
  const corner = (x: number, y: number): THREE.Vector3 => rotateByAngle(new THREE.Vector3(x, y, 0), o.angle).add(o.position);
  o.tails[0] = [corner(-half, 0), corner(half, 0), corner(0, -half), corner(0, half)];
}

/** The lord's Castle Siege skills at their release: Stun's bolts, the cancels' falling flashes, Swell Mana's lights. */
export function siegeSkill(system: EffectSystem, skill: number, caster: EffectOwner, target: EffectOwner | null): void {
  switch (skill) {
    case 67: // AT_SKILL_STUN
      system.createJoint('flash', caster.position, caster.position, caster.angle, 7, null);
      break;
    case 68: // AT_SKILL_REMOVAL_STUN
    case 71: // AT_SKILL_REMOVAL_INVISIBLE
      if (target && target !== caster) {
        const position = target.position.clone().setZ(target.position.z + 1200);
        system.createJoint('flash', position, position, target.angle, skill === 68 ? 0 : 1, target, 120);
      }
      break;
    case 69: // AT_SKILL_MANA: 36 blue lights fly out and a blue circle glows on the ground
      for (let i = 0; i < 36; i++) {
        const angle = new THREE.Vector3(-10, 0, i * 10);
        const position = caster.position.clone().setZ(caster.position.z + 100);
        system.createJoint('jointSpirit', position, position, angle, 21, caster, 60);
        if (i % 20 === 0) system.createEffect('magicGround2', caster.position, angle, caster.light ?? WHITE, 10, caster);
      }
      break;
    case 70: // AT_SKILL_INVISIBLE: the target's Greater Defense lights end; lights and rings on the target (BITMAP_MAGIC+1 6)
      if (target) {
        system.deleteJoint('spearSkill', target, 4);
        system.deleteJoint('spearSkill', target, 9);
        system.createEffect('magicGround2', target.position, target.angle, target.light ?? WHITE, 6, target);
      }
      break;
  }
}

// ---------------------------------------------------------------------------
// Fire Scream (AT_SKILL_FIRE_SCREAM): three burning claws (MODEL_DARK_SCREAM on the ground, MODEL_DARK_SCREAM_FIRE
// glowing over it) race ahead of the lord, 80 to either side turned 10 degrees out, each with an inferno ribbon
// (BITMAP_JOINT_FORCE SubType 7) and shedding flames (BITMAP_FLAME SubType 8).
// Followed: MoveCharacter's release AT_SKILL_FIRE_SCREAM (ZzzCharacter.cpp); CreateEffect's arrow case for
// MODEL_DARK_SCREAM(_FIRE) (ZzzEffect.cpp); Move_MODEL_DARK_SCREAM_FIRE (MoveHandlers.cpp); RenderObject (the claw
// plain, the fire RENDER_BRIGHT) (ZzzObject.cpp); BITMAP_JOINT_FORCE SubType 7 (ZzzEffectJoint.cpp); BITMAP_FLAME
// SubType 8 (ZzzEffectParticle.cpp).
// ---------------------------------------------------------------------------

function darkScream(file: string, fire: boolean): NonNullable<typeof EXTRA_EFFECTS.darkScream> {
  return {
    model: ['Skill', file],
    create: (o, system) => {
      o.velocity = 1;
      o.lifeTime = 19;
      o.position.add(rotateByAngle(new THREE.Vector3(-10, -60, 135), o.angle));
      o.scale = 2.3;
      o.direction.set(0, -35, 0);
      if (fire) {
        o.blendMesh = -2; // RENDER_BRIGHT
        return true;
      }
      o.scale = 0.9;
      o.position.z = system.heightAt(o.position.x, o.position.y) + 3;
      if (o.subType === 0) system.createJoint('jointForce', o.position, o.position, o.angle, 7, o.self, 150, 40);
      o.position.z += 20;
      o.position.add(rotateByAngle(new THREE.Vector3(0, -20, 0), o.angle));
      system.createParticle('blueBlur', o.position, o.angle, new THREE.Vector3(0, 1, 0), 1, 1);
      return true;
    },
    move: (o, system) => {
      o.scale -= fire ? 0.14 : 0.04;
      if (o.scale < 0.1) o.scale = 0;
      o.position.z = system.heightAt(o.position.x, o.position.y) + 3;
      system.createParticle('flame', o.position, o.angle, o.light, 8, (o.scale - 0.4) * 3.5);
      return true;
    },
  };
}
EXTRA_EFFECTS.darkScream = darkScream('darkfirescrem02.bmd', false);
EXTRA_EFFECTS.darkScreamFire = darkScream('darkfirescrem01.bmd', true);

/** Fire Scream's release: the claws ahead, then 80 to the right turned 10 out, then 80 to the left turned 10 out. */
export function fireScream(system: EffectSystem, caster: EffectOwner): void {
  const light = caster.light ?? WHITE;
  for (const [x, turn] of [[0, 0], [80, 10], [-80, -10]] as const) {
    const angle = caster.angle.clone();
    angle.z += turn;
    const position = rotateByAngle(new THREE.Vector3(x, 0, 0), angle).add(caster.position);
    system.createEffect('darkScream', position, angle, light, 0, caster);
    system.createEffect('darkScreamFire', position, angle, light, 0, caster);
  }
}

/** BITMAP_SHOCK_WAVE 14 / BITMAP_TWLIGHT 3: follows its owner, shrinks, and its light (EyeRight) rises and falls by tenths. */
function twilightRing(o: Effect, shrink: number): void {
  if (o.owner) o.position.copy(o.owner.position);
  o.scale -= shrink;
  if (o.lifeTime >= 20) {
    o.alpha += 0.1;
    o.pkKey += 1;
  } else if (o.lifeTime <= 10) {
    o.pkKey -= 1;
    o.alpha -= 0.1;
  }
  if (o.lifeTime >= 20 || o.lifeTime <= 10) o.light.copy(o.startPosition).multiplyScalar(o.pkKey * 0.1);
  o.direction.set(0, 0, 0);
}

// BITMAP_TWLIGHT SubType 3 (the catalogue gives LifeTime 30, Alpha 0): a turning ring of twilight on the ground.
// SubTypes 0-2 (the catalogue: LifeTime 80, Scale 8; Recovery's rings) shrink and turn 10, 5 or 15 a tick.
EXTRA_EFFECTS.twlight = {
  create: (o) => {
    if (o.subType <= 2) return true;
    if (o.subType !== 3) return false;
    o.pkKey = 0;
    o.startPosition.copy(o.light);
    return o.owner !== null;
  },
  move: (o) => {
    if (o.subType <= 2) {
      o.scale -= 0.1;
      o.angle.z += [10, 5, 15][o.subType];
      o.direction.set(0, 0, 0);
      return true;
    }
    twilightRing(o, 0.15);
    o.angle.z += 10;
    return true;
  },
  draw: (o, system) => {
    system.quads.decal('twlight', 'glow', o.position.x, o.position.y, o.scale, o.light, -o.angle.z, (x, y) => system.heightAt(x, y), o.position.z);
  },
};
