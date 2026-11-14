// The objects of the effect system, mirroring the desktop's OBJECT (effects, sprites), PARTICLE
// and JOINT records (Engine/Object/ZzzObject.h). Units are the desktop's: x and y on the ground,
// z up, angles in degrees, lifetimes and speeds per 25 FPS tick.

import type * as THREE from 'three';
import type { AnimatedModel } from '../../bmd.js';
import type { BitmapName } from './core.js';

/**
 * What an effect can follow or aim at: a character, a monster or another object. The effect
 * system reads it every tick, so keep the vectors up to date (or return live views).
 */
export interface EffectOwner {
  /** Where it stands: x, y on the ground and z at its feet (world units). */
  readonly position: THREE.Vector3;
  /** Its angles in degrees; z is the facing (0 faces -y, as the desktop's o->Angle[2]). */
  readonly angle: THREE.Vector3;
  /** Its light (o->Light), the colour some effects take from their maker. White when left out. */
  readonly light?: THREE.Vector3;
  /** Whether it is alive and shown (o->Live && o->Visible). Effects that track it end when it isn't. */
  isLive?(): boolean;
  /**
   * The world position of the bone a weapon hangs on (c->Weapon[hand].LinkBone, 33 and 42 on the
   * player model). Lightning starts there; without it the start is 130 units over the feet.
   */
  weaponPosition?(hand: 0 | 1): THREE.Vector3 | null;
  /**
   * Whether it still has a buff that keeps aura joints (Greater Defense's orbiting lights) alive:
   * eBuff_Attack, eBuff_Defense or eBuff_HelpNpc (ShouldKeepAuraJointAlive). Alive while it lives
   * when left out.
   */
  hasAura?(): boolean;
}

/** CreateEffect types ported (names as in Data/Effects/EffectTypes.json). */
export type EffectType =
  | 'energy' // BITMAP_ENERGY: Energy Ball's flying ball
  | 'fireModel' // MODEL_FIRE: Fire Ball's ball (Skill/Fire01.bmd)
  | 'magic2' // MODEL_MAGIC2: Power Wave's wave, Power Slash's blades (Skill/Magic02.bmd)
  | 'magicGround2' // BITMAP_MAGIC+1: the healing / buff circle on the ground
  | 'stone1' | 'stone2'; // MODEL_STONE1/2: debris of an exploding fire ball

/** CreateParticle types ported (names as in Data/Effects/ParticleTypes.json). */
export type ParticleType = 'energy' | 'spark' | 'spark3' | 'fire' | 'smoke' | 'blood2';

/** CreateSprite types ported (names as in Data/Effects/SpriteTypes.json). */
export type SpriteType = 'energy' | 'light' | 'shiny2' | 'flareBlue';

/** CreateJoint types ported (names as in Data/Effects/JointTypes.json). */
export type JointType = 'jointThunder' | 'jointHealing' | 'spearSkill';

export interface Effect {
  type: EffectType;
  subType: number;
  position: THREE.Vector3;
  startPosition: THREE.Vector3;
  angle: THREE.Vector3;
  direction: THREE.Vector3;
  light: THREE.Vector3;
  scale: number;
  lifeTime: number;
  gravity: number;
  /** o->Velocity: animation keys per tick. */
  velocity: number;
  blendMesh: number;
  blendMeshLight: number;
  blendMeshTexCoordU: number;
  hiddenMesh: number;
  alpha: number;
  owner: EffectOwner | null;
  animationFrame: number;
  /** The drawn model, once loaded (model effects only). */
  model: AnimatedModel | null;
  live: boolean;
}

export interface Particle {
  type: ParticleType;
  subType: number;
  texture: BitmapName;
  position: THREE.Vector3;
  startPosition: THREE.Vector3;
  velocity: THREE.Vector3;
  angle: THREE.Vector3;
  light: THREE.Vector3;
  turningForce: THREE.Vector3;
  scale: number;
  gravity: number;
  lifeTime: number;
  frame: number;
  rotation: number;
  target: EffectOwner | null;
  live: boolean;
}

export interface Sprite {
  type: SpriteType;
  /** 0 glow (additive), the only blend the ported calls use. */
  subType: number;
  position: THREE.Vector3;
  scale: number;
  light: THREE.Vector3;
  rotation: number;
}

/** One cross-section of a trail: four corners (two along x, two along z of its matrix). */
export type Tail = [THREE.Vector3, THREE.Vector3, THREE.Vector3, THREE.Vector3];

export interface Joint {
  type: JointType;
  subType: number;
  texture: BitmapName;
  position: THREE.Vector3;
  targetPosition: THREE.Vector3;
  angle: THREE.Vector3;
  direction: THREE.Vector3;
  light: THREE.Vector3;
  scale: number;
  velocity: number;
  lifeTime: number;
  maxTails: number;
  numTails: number;
  tails: (Tail | null)[];
  target: EffectOwner | null;
  /** The joint's slot number on the desktop; orbits use it to differ. */
  index: number;
  live: boolean;
}
