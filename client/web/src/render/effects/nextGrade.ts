// The sparkles of +15 weapons in a character's hands (SpawnNextGradeWeaponEffects): every tick three orange lights
// along the hand's first bone, and at its second a pulsing magic circle, a red flare and a spark of lightning.
// Not drawn in Chaos Castle (DrawsNextGradeObjects).
//
// Followed C++ (client/src/source):
//   SpawnNextGradeWeaponEffects, DrawsNextGradeObjects     Engine/Object/ZzzObject.cpp, ZzzCharacter.cpp

import * as THREE from 'three';
import { randInt } from './core.js';
import type { EffectSystem } from './effectSystem.js';
import type { EffectOwner } from './types.js';

/** A weapon in a hand: which hand (0 right, 1 left), its level and whether it is a bow or crossbow. */
export interface GradedWeapon { hand: 0 | 1; level: number; bow: boolean }

/** Who carries graded weapons, read every tick. */
export interface GradedCarrier { owner: EffectOwner; weapons: GradedWeapon[] }

const ORANGE = new THREE.Vector3(1, 0.6, 0);
const WHITE = new THREE.Vector3(1, 1, 1);
const RED = new THREE.Vector3(1, 0.2, 0);

/** b->TransformByObjectBone: a point in a bone's frame, in the world. */
function boneAt(owner: EffectOwner, bone: number, x: number): THREE.Vector3 | null {
  const matrix = owner.boneMatrix?.(bone);
  return matrix ? new THREE.Vector3(x, 0, 0).applyMatrix4(matrix) : null;
}

/** One tick of SpawnNextGradeWeaponEffects for one character. */
export function nextGradeWeaponSparkles(system: EffectSystem, owner: EffectOwner, weapons: GradedWeapon[]): void {
  for (const weapon of weapons) {
    if (weapon.level !== 15) continue;
    const first = weapon.bow || weapon.hand === 0 ? 27 : 36;
    const second = weapon.bow || weapon.hand === 0 ? 28 : 37;
    for (const x of [0, 10, 20]) {
      const p = boneAt(owner, first, x);
      if (p) system.createSprite('light', p, 0.6, ORANGE, owner);
    }
    const p = boneAt(owner, second, 10);
    if (!p) continue;
    const pulse = Math.abs(Math.sin(system.time * 0.002));
    system.createSprite('magicGround1', p, 0.35, new THREE.Vector3(0.7 * pulse + 0.3, 0.1 * pulse + 0.1, 0), owner);
    system.createSprite('flareRed', p, 0.6 * (randInt(60) * 0.01) + 0.4, WHITE, owner);
    system.createParticle('lightningMega', p, owner.angle, RED, 0, (randInt(80) + 10) * 0.01);
  }
}

let task: { system: EffectSystem; tick: number } | null = null;

/**
 * Keeps one task on the effect system that sparkles every carrier's +15 weapons each tick; scheduled again when the
 * system dropped its tasks (a map change clears them). Call it every frame.
 */
export function keepNextGradeSparkles(system: EffectSystem, carriers: () => Iterable<GradedCarrier>, shown: () => boolean): void {
  if (task && task.system === system && system.time - task.tick <= 200) return;
  const mine = { system, tick: system.time };
  task = mine;
  system.schedule(() => {
    if (task !== mine) return false;
    mine.tick = system.time;
    if (!shown()) return true;
    for (const { owner, weapons } of carriers()) {
      if (weapons.length && (owner.isLive?.() ?? true)) nextGradeWeaponSparkles(system, owner, weapons);
    }
    return true;
  });
}
