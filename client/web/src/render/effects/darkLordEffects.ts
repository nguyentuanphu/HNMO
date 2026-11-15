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
//   CreateJoint / MoveJoint / DescribeJoint: BITMAP_FLARE_FORCE (0-7), BITMAP_2LINE_GHOST (0)
//                                                                          Render/Effects/ZzzEffectJoint.cpp

import * as THREE from 'three';
import { randInt, rotateByAngle } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS } from './extraEffects.js';
import type { Effect, EffectOwner, Joint } from './types.js';

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
    if (o.subType >= 1 && o.subType <= 4) {
      o.skill = 1;
      o.texture = 'jointThunder';
      o.targetPosition.set(80, 180, 0);
      o.counter = randInt(3) + 2;
      if (o.subType === 2 || o.subType === 4) o.targetPosition.y = -180;
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
    o.lifeTime -= 1;
    if (o.lifeTime <= 0) o.live = false;
    return false;
  },
};

// BITMAP_SHOCK_WAVE SubType 0: a ring on the ground 20 cells wide that shrinks by one a tick.
EXTRA_EFFECTS.shockWave = {
  create: (o) => {
    if (o.subType !== 0) return false;
    o.lifeTime = 30;
    o.scale = 20;
    return true;
  },
  move: (o) => {
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
