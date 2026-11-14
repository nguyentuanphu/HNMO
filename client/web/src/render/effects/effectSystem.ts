// The effect system: CreateEffect / CreateParticle / CreateSprite / CreateJoint and their move
// and draw passes, stepped once per 25 FPS tick like the desktop (MoveEffects, MoveJoints,
// MoveParticles after ClearSprites, Scenes/MainScene.cpp). Only the types and SubTypes listed
// in types.ts are ported; each case cites the C++ it follows.
//
// Followed C++ (client/src/source/Render/Effects):
//   CreateEffect, MoveEffect (and its shared tail), CheckTargetRange, CreateHealing, CreateForce,
//   CreateBomb, CreateInferno, MoveParticle, RenderEffects, UpdateDrawnEffectSprites,
//   DescribeEffectShadow (BITMAP_MAGIC+1)                                ZzzEffect.cpp
//   Move_MODEL_MAGIC2, _ICE_SMALL, _POISON, _STORM, _CIRCLE, _CIRCLE_LIGHT, _SKILL_INFERNO,
//   _BLIZZARD, _LASER, Move_BITMAP_FLAME                                 Behaviors/MoveHandlers.cpp
//   CreateParticle, MoveParticles, DescribeParticle, RenderParticles     ZzzEffectParticle.cpp
//   CreateSprite, DescribeSprite                                         zzzeffectsprite.cpp
//   CreateJoint, CreateTail, FollowTail, MoveJoint, DescribeJoint,
//   UpdateDrawnJointEffects                                              ZzzEffectJoint.cpp
// Terrain light (AddTerrainLight) and the camera shake (SetEarthQuakeAt) are not drawn.

import * as THREE from 'three';
import { loadModel, buildGeometries, computeBoneMatrices, createMaterial, AnimatedModel } from '../../bmd.js';
import type { BMDModel } from '../../formats.js';
import { EffectCatalogue } from './catalogue.js';
import {
  TICK_SECONDS, randInt, rotateByAngle, angleEuler, moveHumming, magicScrewAt, preloadEffectTextures,
} from './core.js';
import type { BitmapName } from './core.js';
import { QuadBatches, applyBlend } from './quadBatch.js';
import type { Blend, HeightAt } from './quadBatch.js';
import { EXTRA_EFFECTS, EXTRA_JOINTS } from './extraEffects.js';
import type {
  Effect, EffectOwner, EffectType, Joint, JointBlend, JointType, Particle, ParticleType, Sprite, SpriteType, Tail,
} from './types.js';

/** At most this many ticks are caught up in one update (after a long pause the rest is dropped). */
const MAX_TICKS_PER_UPDATE = 10;
const WHITE = new THREE.Vector3(1, 1, 1);
/** CheckTargetRange: an effect aimed at an owner ends within this flat distance of it. */
const TARGET_RANGE = 100;

// The models of the model effects (AccessModel in Engine/Object/ZzzOpenData.cpp).
const EFFECT_MODELS: Partial<Record<EffectType, [string, string]>> = {
  fireModel: ['Skill', 'Fire01.bmd'],
  magic2: ['Skill', 'Magic02.bmd'],
  stone1: ['Skill', 'Stone01.bmd'],
  stone2: ['Skill', 'Stone02.bmd'],
  ice: ['Skill', 'Ice01.bmd'],
  iceSmall: ['Skill', 'Ice02.bmd'],
  poison: ['Skill', 'Poison01.bmd'],
  storm: ['Skill', 'Storm01.bmd'],
  circle: ['Skill', 'Circle01.bmd'],
  circleLight: ['Skill', 'Circle02.bmd'],
  skillInferno: ['Skill', 'Inferno01.bmd'],
  blizzardModel: ['Skill', 'blizzard.bmd'],
  laser: ['Skill', 'Laser01.bmd'],
};
const MODEL_TEXTURE_DIRS = ['Skill', 'Effect', 'Item'];

/** The particle types' textures (o->TexType = o->Type). */
const PARTICLE_TEXTURES: Record<ParticleType, BitmapName> = {
  energy: 'energy', spark: 'spark', spark3: 'spark3', fire: 'fire', fire3: 'fire3', smoke: 'smoke', blood2: 'blood2',
  explosion: 'explosion', flame: 'flame', light: 'light', flareBlue: 'flareBlue', flare: 'flare',
};
const JOINT_TEXTURES: Record<JointType, BitmapName> = {
  jointThunder: 'jointThunder', jointHealing: 'jointHealing', spearSkill: 'flareBlue', jointSpirit: 'jointSpirit',
  smokeTrail: 'flare', flare: 'flare', jointSpark: 'spark1', lightJoint: 'light', piercing: 'piercing', flareForceJoint: 'flareForce', jointForce: 'jointForce', blur: 'blur1',
};

export interface EffectSystemOptions {
  /** The camera, for billboards. Without it sprites face +y. */
  camera?: THREE.Camera;
  /** Ground height (RequestTerrainHeight). Without it the ground is z = 0. */
  heightAt?: HeightAt;
}

/** Something the system runs once per tick until it returns false (a skill's timeline). */
export type TickTask = (tick: number) => boolean;

/** Something drawn with the effects that keeps its own records (the weapon trails). */
export interface EffectLayer {
  /** Once per 25 FPS tick, after the effects move. */
  tick(): void;
  /** Once per drawn frame, before the draw. */
  frame(): void;
  draw(quads: QuadBatches): void;
}

/** CreateEffect's PKKey and SkillIndex, which some types read as numbers. */
export interface CreateEffectExtra {
  pkKey?: number;
  skillIndex?: number;
}

const modelLoads = new Map<string, Promise<BMDModel | null>>();

function loadEffectModel(dir: string, file: string): Promise<BMDModel | null> {
  const key = `${dir}/${file}`;
  let load = modelLoads.get(key);
  if (!load) {
    load = loadModel(dir, file).then((model) => model, (error: unknown) => {
      console.warn(`Effect model ${file} not drawn`, (error as Error).message);
      return null;
    });
    modelLoads.set(key, load);
  }
  return load;
}

function newVector(v?: THREE.Vector3): THREE.Vector3 {
  return v ? v.clone() : new THREE.Vector3();
}

function ownerLive(owner: EffectOwner | null): boolean {
  return owner !== null && (owner.isLive?.() ?? true);
}

/** The draw state a particle takes: glow for three-component textures, alpha tested for the rest (RenderParticles). */
function particleBlend(o: Particle): Blend {
  if (o.type === 'smoke' && [2, 5, 12, 14, 15, 20, 21, 29, 37, 38, 59].includes(o.subType)) return 'subtract';
  return o.texture === 'blood2' ? 'alpha' : 'glow';
}

export class EffectSystem {
  readonly root: THREE.Group;
  private readonly options: EffectSystemOptions;
  readonly quads: QuadBatches;
  private catalogue: EffectCatalogue | null = null;
  private readonly waiting: (() => void)[] = [];
  private effects: Effect[] = [];
  private particles: Particle[] = [];
  private joints: Joint[] = [];
  private sprites: Sprite[] = [];
  private tasks: TickTask[] = [];
  private readonly layers: EffectLayer[] = [];
  private accumulator = 0;
  private tick = 0;
  private jointIndex = 0;
  private effectIndex = 0;
  /** WorldTime: milliseconds, advanced 40 per tick. */
  private worldTime = 0;
  /** Resolves when the catalogue and the textures are loaded. */
  readonly ready: Promise<void>;

  /** Adds the effects to `parent` (the scene's world-space root: x, y ground, z up). */
  constructor(parent: THREE.Object3D, options: EffectSystemOptions = {}) {
    this.options = options;
    this.root = new THREE.Group();
    this.root.name = 'effects';
    parent.add(this.root);
    this.quads = new QuadBatches(this.root);
    this.ready = Promise.all([
      EffectCatalogue.load().then((catalogue) => {
        this.catalogue = catalogue;
        for (const create of this.waiting.splice(0)) create();
      }),
      preloadEffectTextures(['energy', 'spark', 'spark3', 'fire', 'smoke', 'blood2', 'light', 'shiny2', 'flareBlue',
        'magicGround2', 'jointThunder', 'jointHealing', 'jointSpirit', 'flame', 'explosion', 'fire2', 'fire3', 'flare']),
    ]).then(() => undefined);
  }

  set camera(camera: THREE.Camera | undefined) {
    this.options.camera = camera;
  }

  /** RequestTerrainHeight, or 0 without a terrain. */
  heightAt(x: number, y: number): number {
    return this.options.heightAt?.(x, y) ?? 0;
  }

  /** WorldTime in milliseconds. */
  get time(): number {
    return this.worldTime;
  }

  /** Adds a layer drawn with the effects (the weapon trails). */
  addLayer(layer: EffectLayer): void {
    this.layers.push(layer);
  }

  /** Runs a task once per tick, after the sprites are cleared and before the effects move. */
  schedule(task: TickTask): void {
    this.tasks.push(task);
  }

  /** Advances by dt seconds (whole 40 ms ticks) and redraws. */
  update(dtSeconds: number): void {
    this.accumulator += dtSeconds;
    let ticks = 0;
    while (this.accumulator >= TICK_SECONDS) {
      this.accumulator -= TICK_SECONDS;
      if (ticks++ < MAX_TICKS_PER_UPDATE) this.step();
    }
    for (const layer of this.layers) layer.frame();
    this.draw();
  }

  /** Removes everything. */
  clear(): void {
    for (const effect of this.effects) this.destroyEffect(effect);
    this.effects = [];
    this.particles = [];
    this.joints = [];
    this.sprites = [];
    this.tasks = [];
  }

  dispose(): void {
    this.clear();
    this.quads.dispose();
    this.root.removeFromParent();
  }

  /** How many of each kind are alive (for tests). */
  counts(): { effects: number; particles: number; joints: number; sprites: number } {
    return { effects: this.effects.length, particles: this.particles.length, joints: this.joints.length, sprites: this.sprites.length };
  }

  /** The live objects of each type (for tests): `effect:circle`, `joint:jointSpirit`, `particle:flame`. */
  census(): Record<string, number> {
    const out: Record<string, number> = {};
    const add = (key: string): void => { out[key] = (out[key] ?? 0) + 1; };
    for (const o of this.effects) add(`effect:${o.type}`);
    for (const o of this.joints) add(`joint:${o.type}`);
    for (const o of this.particles) add(`particle:${o.type}`);
    for (const o of this.sprites) add(`sprite:${o.type}`);
    return out;
  }

  // =========================================================================
  // CreateSprite (zzzeffectsprite.cpp): a billboard drawn until the next tick.
  // =========================================================================

  createSprite(type: SpriteType, position: THREE.Vector3, scale: number, light: THREE.Vector3,
    _owner: EffectOwner | null = null, rotation = 0, subType = 0): void {
    this.sprites.push({ type, subType, position: position.clone(), scale, light: light.clone(), rotation });
  }

  // =========================================================================
  // CreateParticle (ZzzEffectParticle.cpp).
  // =========================================================================

  createParticle(type: ParticleType, position: THREE.Vector3, angle: THREE.Vector3, light: THREE.Vector3,
    subType = 0, scale = 1, owner: EffectOwner | null = null): void {
    const o: Particle = {
      type, subType, texture: PARTICLE_TEXTURES[type],
      position: position.clone(), startPosition: position.clone(), velocity: new THREE.Vector3(),
      angle: angle.clone(), light: light.clone(), turningForce: new THREE.Vector3(),
      scale, gravity: 0, lifeTime: 2, frame: 0, rotation: 0, target: owner, live: true,
    };
    if (!this.initParticle(o, angle, scale)) return;
    this.particles.push(o);
  }

  private initParticle(o: Particle, callAngle: THREE.Vector3, scale: number): boolean {
    switch (o.type) {
      case 'energy': // thunder energy
        o.scale = scale * (randInt(8) + 6) * 0.1;
        o.rotation = randInt(360);
        o.gravity = 20;
        if (o.subType === 1) {
          o.lifeTime = 10;
          o.light.set(0.5, 0.5, 0.5);
        }
        return o.subType === 0 || o.subType === 1;
      case 'spark': {
        o.scale = (randInt(4) + 4) * 0.1;
        if (o.subType !== 0 && o.subType !== 2) return false;
        o.lifeTime = randInt(16) + 24;
        o.angle.z = randInt(360);
        o.gravity = randInt(16) + 6;
        const p = new THREE.Vector3(0, (randInt(20) + 20) * 0.1, 0);
        if (o.subType === 2) {
          o.scale *= 2;
          p.multiplyScalar(3);
        }
        rotateByAngle(p, o.angle, o.velocity);
        return true;
      }
      case 'spark3':
        if (o.subType === 1) {
          o.angle.set(randInt(360), randInt(360), randInt(360));
          rotateByAngle(new THREE.Vector3(0, -50, 0), o.angle, o.velocity);
        } else if (o.subType === 2) { // flies into its owner in ten ticks (BITMAP_GATHERING)
          if (!o.target) return false;
          o.lifeTime = 15;
          o.angle.set(0, 0, 0);
          o.velocity.copy(o.target.position).sub(o.position).divideScalar(10);
        }
        return o.subType === 0 || o.subType === 1 || o.subType === 2;
      case 'fire':
      case 'fire3':
        o.lifeTime = 24;
        if (o.subType === 0) {
          o.velocity.set(0, -(randInt(16) + 32) * 0.1, 0);
          o.scale = (randInt(64) + 128) * 0.01;
          o.rotation = randInt(360);
        } else if (o.subType === 1) {
          o.velocity.set(0, -(randInt(16) + 32) * 0.1, 0);
          o.scale = (randInt(4) + 10) * 0.01;
          o.rotation = randInt(360);
        } else if (o.subType === 5) {
          o.velocity.set(0, -(randInt(16) + 32) * 0.1, 0);
          o.scale = scale * (randInt(64) + 128) * 0.01;
          o.rotation = randInt(360);
        } else if (o.subType === 7) {
          o.velocity.set(0, -(randInt(32) - 16) * 0.1, 0);
          o.scale = (randInt(64) + 128) * 0.008 + scale;
          o.rotation = randInt(360);
        } else if (o.subType === 2 || o.subType === 3) { // the inner switch's default
          o.velocity.set(0, -(randInt(32) - 16) * 0.1, 0);
          o.rotation = randInt(360);
        } else {
          return false;
        }
        return true;
      case 'smoke':
        if (o.subType === 0) {
          o.lifeTime = 16;
          o.scale = (randInt(32) + 48) * 0.01;
          o.angle.x = randInt(360);
          o.rotation = Math.floor(this.worldTime) % 360;
        } else if (o.subType === 1) {
          o.lifeTime = 50;
          o.scale = (randInt(32) + 80) * 0.01;
          o.position.x += randInt(64) - 32;
          o.position.y += randInt(64) - 32;
          o.position.z += randInt(64) + 32;
          o.angle.x = randInt(90) - 45;
          o.angle.z = randInt(360);
          o.velocity.set(0, -(randInt(8) + 40), 0);
        } else if (o.subType === 3) {
          o.lifeTime = 10;
          o.scale = (randInt(32) + 80) * 0.01;
          o.angle.x = randInt(90) - 45;
          o.angle.z = randInt(360);
          o.velocity.set(0, -(randInt(8) + 40), 0);
        } else if (o.subType === 11) {
          o.lifeTime = 50;
          o.position.x += randInt(64) - 32;
          o.position.y += randInt(64) - 32;
          o.position.z += randInt(64) + 32;
          o.angle.x = randInt(90) - 45;
          o.angle.z = randInt(360);
          o.rotation = randInt(360);
          o.turningForce.copy(o.light);
          o.velocity.set(0, -(randInt(8) + 40), 0);
        } else {
          return false;
        }
        return true;
      case 'blood2': {
        o.lifeTime = 12;
        o.scale = (randInt(4) + 8) * 0.05;
        o.angle.x = randInt(360);
        // AngleMatrix(Angle): the call's angle, not the particle's.
        rotateByAngle(new THREE.Vector3(0, -(randInt(16) + 8), randInt(6) - 3), callAngle, o.velocity);
        o.light.set(0.1, 0, 0);
        return o.subType === 0;
      }
      case 'explosion': // BITMAP_EXPLOTION
        o.lifeTime = o.subType === 2 ? 30 : 20;
        o.scale = scale;
        return o.subType === 0;
      case 'flame': // BITMAP_FLAME
        if (o.subType === 0) {
          o.lifeTime = 20;
          o.velocity.set(0, 0, (randInt(128) + 128) * 0.15);
          o.scale = scale * (randInt(64) + 64) * 0.01;
          return true;
        }
        return false;
      case 'light': // BITMAP_LIGHT
        if (o.subType === 4) { // on a bone of its owner (the Swell Life buff); the scale is the bone
          o.lifeTime = 10;
          o.gravity = 0;
          o.rotation = scale;
          o.scale = 2;
        } else if (o.subType === 5) o.lifeTime = 50;
        else if (o.subType === 6) o.lifeTime = 2;
        else return false;
        return true;
      case 'flareBlue':
      case 'flare':
        return false;
    }
  }

  private moveParticle(o: Particle): void {
    o.lifeTime -= 1;
    if (o.lifeTime <= 0) o.live = false;
    // MovePosition(o->Position, o->Angle, o->Velocity) for every particle that moves.
    o.position.add(rotateByAngle(o.velocity, o.angle));
    switch (o.type) {
      case 'energy':
        o.rotation += o.gravity;
        if (o.subType === 1) {
          o.light.setScalar(o.lifeTime / 15);
          o.scale += 0.1;
        }
        break;
      case 'spark': {
        const luminosity = o.lifeTime / 16;
        if (o.lifeTime < 0) o.live = false;
        o.light.setScalar(luminosity);
        o.position.z += o.gravity;
        o.gravity -= 2;
        const height = this.heightAt(o.position.x, o.position.y);
        if (o.position.z < height) {
          o.position.z = height;
          o.gravity = -o.gravity * 0.6;
          o.lifeTime -= 4;
        }
        o.position.add(o.velocity);
        break;
      }
      case 'spark3':
        if (o.subType === 2) {
          // Placed from where it was by its velocity (the common move above is undone).
          if (o.lifeTime > 5 && o.target && !ownerLive(o.target)) o.lifeTime = 5;
          o.light.setScalar(o.lifeTime / 5);
          o.scale += 0.05;
          o.position.copy(o.startPosition).add(o.velocity);
          o.startPosition.copy(o.position);
          break;
        }
        o.scale -= o.subType === 1 ? 2 : 0.5;
        if (o.scale < 0.2) o.live = false;
        break;
      case 'fire':
      case 'fire3':
        o.gravity += 0.004;
        if (o.subType === 5) {
          o.scale -= 0.04;
          o.rotation += 5;
        } else if (o.subType === 7 || o.subType === 0) {
          o.scale -= 0.04;
        } else { // the move's default
          o.scale += o.gravity;
          o.velocity.multiplyScalar(0.98);
        }
        o.frame = Math.trunc((23 - o.lifeTime) / 6);
        o.position.z += o.gravity * 10;
        break;
      case 'smoke':
        if (o.subType === 0) {
          o.light.setScalar(o.lifeTime / 8);
          o.gravity += 0.2;
          o.position.z += o.gravity;
          o.scale += 0.05;
        } else if (o.subType === 1) {
          const luminosity = o.lifeTime / 50;
          o.light.set(luminosity * 0.5, luminosity, luminosity * 0.8);
          o.velocity.multiplyScalar(0.4);
          o.scale += 0.05;
        } else if (o.subType === 3) {
          const luminosity = o.lifeTime / 8;
          o.light.set(luminosity * 0.8, luminosity * 0.8, luminosity);
          o.velocity.multiplyScalar(0.4);
          o.scale += 0.1;
        } else {
          o.light.copy(o.turningForce).multiplyScalar(o.lifeTime / 50);
          o.velocity.multiplyScalar(0.4);
          o.scale += 0.05;
          o.position.z -= 1;
        }
        break;
      case 'blood2':
        o.frame = Math.trunc((12 - o.lifeTime) / 3);
        o.velocity.multiplyScalar(0.95);
        o.position.add(o.velocity);
        break;
      case 'explosion':
        o.frame = Math.trunc((20 - o.lifeTime) / 2);
        break;
      case 'flame':
        if (o.lifeTime <= 0) o.live = false;
        break;
      case 'light':
        // SubType 4 rises from its owner's bone; 5 and 6 drift, shrink and fade.
        if (o.subType === 4) {
          const bone = o.target?.bonePosition?.(o.rotation);
          if (!bone) { o.live = false; break; }
          o.position.copy(bone);
          o.gravity += (randInt(40) + 60) / 100 * 9.5;
          o.scale -= (randInt(400) + 400) / 10000;
          o.position.z += o.gravity;
          o.light.multiplyScalar(1 / 1.35);
          break;
        }
        if (o.subType === 6) {
          o.position.x += (randInt(2001) - 1000) * 0.0004 * 0.5;
          o.position.y += (randInt(2001) - 1000) * 0.0004 * 0.5;
          o.position.z += 5 * 0.5;
          o.scale *= 0.95;
          o.light.multiplyScalar(0.95);
        } else {
          o.scale *= 0.95;
          o.light.setScalar(0.3 * o.scale);
        }
        if (o.scale <= 0.1) o.live = false;
        break;
      default:
        break;
    }
  }

  // RenderParticles / DescribeParticle: JPEG textures glow, the others are alpha tested.
  private drawParticle(o: Particle): void {
    const size = this.quads.textureSize(o.texture);
    if (!size) return;
    const width = size.width * o.scale, height = size.height * o.scale;
    const blend = particleBlend(o);
    switch (o.type) {
      case 'blood2':
        this.quads.billboard(o.texture, blend, o.position, width, height, o.light, 0, (o.frame % 2) * 0.5, Math.trunc(o.frame / 2) * 0.5, 0.5, 0.5);
        break;
      case 'explosion':
        this.quads.billboard(o.texture, blend, o.position, width, height, o.light, 0,
          (o.frame % 4) * 0.25 + 0.005, Math.trunc(o.frame / 4) * 0.25 + 0.005, 0.25 - 0.01, 0.25 - 0.01);
        break;
      case 'fire':
      case 'fire3': {
        const strip = [5, 7, 8, 11, 12, 13, 17].includes(o.subType);
        this.quads.billboard(o.texture, blend, o.position, width * 0.25, height, o.light,
          strip ? o.rotation : o.angle.x, (o.frame % 4) * 0.25, 0, 0.25, 1);
        break;
      }
      default:
        this.quads.billboard(o.texture, blend, o.position, width, height, o.light, o.type === 'light' && o.subType === 4 ? 0 : o.rotation);
    }
  }

  // =========================================================================
  // CreateJoint (ZzzEffectJoint.cpp): trails drawn as two crossed ribbons.
  // =========================================================================

  createJoint(type: JointType, position: THREE.Vector3, targetPosition: THREE.Vector3, angle: THREE.Vector3,
    subType = 0, target: EffectOwner | null = null, scale = 10, skill = 0, pkKey = 0): Joint | null {
    const o: Joint = {
      type, subType, texture: JOINT_TEXTURES[type],
      position: position.clone(), targetPosition: targetPosition.clone(), startPosition: new THREE.Vector3(),
      angle: angle.clone(), direction: new THREE.Vector3(), light: WHITE.clone(), scale, velocity: 0, lifeTime: 0,
      maxTails: 1, numTails: 0, tails: [], target, index: this.jointIndex++, blend: 'glow', faces: 3,
      createTails: true, collision: false, counter: 0, skill, pkKey, live: true,
    };
    if (!this.initJoint(o)) return null;
    o.tails = new Array<Tail | null>(o.maxTails + 1).fill(null);
    // The start tail across the joint's position (laid with the call's scale, before the type's).
    o.tails[0] = this.tailAt(o.position, o.angle, scale);
    this.joints.push(o);
    return o;
  }

  private initJoint(o: Joint): boolean {
    switch (o.type) {
      case 'jointThunder':
        o.maxTails = 50;
        o.velocity = 50;
        if (o.subType === 3) { // a short bolt: CreateJoint's SkillIndex is its speed and length, PKKey its life
          o.velocity = o.skill;
          o.lifeTime = o.pkKey;
          o.pkKey = -1;
          o.maxTails = o.skill;
          o.light.set(0.5, 0.5, 1);
          o.startPosition.copy(o.position);
          return true;
        }
        if (o.subType !== 0) return false;
        o.lifeTime = 2;
        return true;
      case 'jointHealing':
        o.lifeTime = 12;
        o.maxTails = 2;
        o.velocity = 0;
        if (o.subType === 8) { // CreateForce's trails
          o.lifeTime = 17;
          o.maxTails = 3;
          o.light.set(0.5, 0.5, randInt(128) / 255 + 0.5);
          return true;
        }
        return o.subType >= 1 && o.subType <= 3;
      case 'spearSkill':
        if (!o.target) return false;
        if (o.subType !== 2) o.targetPosition.copy(o.target.position);
        if (o.subType === 0) {
          o.light.set(1, 1, 1);
          o.lifeTime = 999999;
          o.maxTails = 30;
          return true;
        }
        if (o.subType === 2) { // Death Stab's wind
          o.light.set(1, 0.3, 0.3);
          o.lifeTime = 20;
          o.maxTails = 5;
          o.texture = 'flareForce';
          return true;
        }
        if (o.subType !== 4) return false;
        o.light.set(0.4, 0.8, 0.2);
        o.lifeTime = 10000;
        o.maxTails = 30;
        return true;
      case 'jointSpirit':
        o.blend = 'subtract';
        switch (o.subType) {
          case 0:
            o.velocity = 70;
            o.lifeTime = 49;
            o.maxTails = 6;
            return true;
          case 2:
            o.blend = 'glow';
            o.faces = 2;
            o.velocity = 50;
            o.lifeTime = 20;
            o.maxTails = 3;
            o.light.set(0.5, 0.5, 0.5);
            o.startPosition.copy(o.position);
            return true;
          case 6:
          case 7:
            o.blend = 'glow';
            o.faces = o.subType === 6 ? 2 : 0;
            o.velocity = o.subType === 6 ? 50 : 10;
            o.lifeTime = 20;
            o.maxTails = 5;
            if (o.subType === 6) {
              o.pkKey = randInt(5);
              if (o.pkKey) o.createTails = false;
            }
            if (o.skill === 0) o.light.set(0.3, 0.3, 1);
            else if (o.skill === 1) {
              o.velocity = 10;
              o.light.set(0.5, 0.5, 0.5);
            }
            o.startPosition.copy(o.position);
            return true;
          default:
            return false;
        }
      case 'smokeTrail': // BITMAP_SMOKE SubType 0: a flare trail behind a falling effect
        if (o.subType !== 0) return false;
        o.maxTails = 20;
        o.lifeTime = 20;
        o.velocity = 0;
        o.texture = 'flare';
        return true;
      default:
        return EXTRA_JOINTS[o.type]?.init(o, this) ?? false;
    }
  }

  /** DeleteJoint: ends the joints of a type made for an owner (any SubType with -1). */
  deleteJoint(type: JointType, target: EffectOwner, subType = -1): void {
    for (const joint of this.joints) {
      if (joint.type === type && joint.target === target && (subType === -1 || joint.subType === subType)) joint.live = false;
    }
  }

  /** SearchJoint. */
  searchJoint(type: JointType, target: EffectOwner, subType = -1): boolean {
    return this.joints.some((joint) => joint.live && joint.type === type && joint.target === target && (subType === -1 || joint.subType === subType));
  }

  private tailAt(position: THREE.Vector3, angle: THREE.Vector3, scale: number): Tail {
    const half = scale * 0.5;
    const corner = (x: number, z: number): THREE.Vector3 => rotateByAngle(new THREE.Vector3(x, 0, z), angle).add(position);
    return [corner(-half, 0), corner(half, 0), corner(0, -half), corner(0, half)];
  }

  /** CreateTail, for the registered joint types that lay several tails a tick. */
  layTail(o: Joint, angle: THREE.Vector3): void {
    this.createTail(o, angle);
  }

  // CreateTail (not blurred): a new tail at the joint's position, the older ones move back.
  private createTail(o: Joint, angle: THREE.Vector3): void {
    o.numTails = Math.min(o.numTails + 1, o.maxTails - 1);
    for (let j = o.numTails - 1; j >= 0; j--) o.tails[j + 1] = o.tails[j];
    o.tails[0] = this.tailAt(o.position, angle, o.scale);
  }

  private moveJoint(o: Joint): void {
    // The common move along -y of the joint's angle; the tail is laid with this matrix.
    const matrixAngle = o.angle.clone();
    // (BITMAP_LIGHT and the spear flares place themselves.)
    if (o.velocity !== 0 && o.type !== 'lightJoint' && o.type !== 'piercing' && o.type !== 'jointForce') o.position.add(rotateByAngle(new THREE.Vector3(0, -o.velocity, 0), o.angle));
    switch (o.type) {
      case 'jointThunder': this.moveThunder(o); break;
      case 'jointHealing': this.moveHealing(o); break;
      case 'spearSkill': if (!this.moveSpearSkill(o)) return; break;
      case 'jointSpirit': this.moveSpirit(o); break;
      case 'smokeTrail':
        if (o.target && ownerLive(o.target)) o.position.copy(o.target.position);
        break;
      default: EXTRA_JOINTS[o.type]?.move(o, this); break;
    }
    if (!o.live) return;
    if (o.createTails) this.createTail(o, o.type === 'jointThunder' ? o.angle : matrixAngle); // FollowTail
    o.lifeTime -= 1;
    if (o.lifeTime < 0) o.live = false;
  }

  // BITMAP_JOINT_THUNDER, SubType 0: rebuilt each move as a jittered path homing in on the target.
  private moveThunder(o: Joint): void {
    for (let j = 0; j < o.maxTails; j++) {
      if (o.target) o.targetPosition.copy(o.target.position).setZ(o.target.position.z + 80);
      const distance = moveHumming(o.position, o.angle, o.targetPosition, o.subType === 3 ? 25 : 50);
      o.direction.x = (randInt(1024) - 512) / o.scale;
      o.direction.z = (randInt(1024) - 512) / o.scale;
      const angle = o.angle.clone().add(o.direction);
      this.createTail(o, angle);
      if (o.subType === 3) {
        if (distance > 150) o.lifeTime = 0;
      } else if (distance < o.velocity * 1.5) {
        if (o.scale === 50) {
          this.createParticle('energy', o.position, o.angle, o.light);
          if (randInt(8) === 0) this.createParticle('smoke', o.position, o.angle, o.light);
        }
        break;
      }
      o.position.add(rotateByAngle(new THREE.Vector3(0, -o.velocity, 0), angle));
    }
  }

  // BITMAP_JOINT_HEALING, SubTypes 1-3: speeds up towards the target's chest, coloured by SubType;
  // SubType 8 (CreateForce) homes in on the point it was made for.
  private moveHealing(o: Joint): void {
    o.velocity += 4;
    if (o.subType === 8) {
      if (o.lifeTime > 10) moveHumming(o.position, o.angle, o.targetPosition, 10);
      else if (o.lifeTime < 6) o.light.multiplyScalar(1 / 1.8);
      this.createSprite('light', o.position, 1, o.light);
      return;
    }
    const p = o.target ? o.target.position.clone().setZ(o.target.position.z + 120) : o.targetPosition.clone();
    moveHumming(o.position, o.angle, p, 10);
    const luminosity = (12 - o.lifeTime) * 0.1;
    if (o.subType === 1) o.light.set(luminosity * 0.4, luminosity * 0.6, luminosity);
    else if (o.subType === 2) o.light.set(luminosity * 0.4, luminosity, luminosity * 0.6);
    else o.light.set(luminosity, luminosity * 0.6, luminosity * 0.4);
    if (Math.trunc(o.lifeTime) === 1) this.createSprite('shiny2', p, (randInt(8) + 8) * 0.2, o.light, o.target, randInt(360));
  }

  // MODEL_SPEARSKILL: SubTypes 0 (Soul Barrier) and 4 (Greater Defense) circle the target while it
  // lives (4 while its aura buff lasts); SubType 2 is Death Stab's gust flying in to the sword.
  private moveSpearSkill(o: Joint): boolean {
    const target = o.target;
    if (o.subType === 2) {
      if (!target || !ownerLive(target)) { o.target = null; return true; }
      o.scale = o.lifeTime * 3;
      const rate1 = Math.max(0, Math.min((o.lifeTime - 10) / 10, 1));
      const magic = magicScrewAt(o.index * 17721, 1.4).add(o.targetPosition);
      const sword = target.weaponPosition?.(0) ?? target.position.clone().setZ(target.position.z + 120);
      o.position.copy(sword).multiplyScalar(1 - rate1).addScaledVector(magic, rate1);
      return true;
    }
    if (!target || !ownerLive(target) || (o.subType === 4 && !(target.hasAura?.() ?? true))) {
      o.lifeTime = 0;
      o.live = false;
      return false;
    }
    if (o.subType === 4) o.lifeTime = 100;
    // The trail moves with the target.
    const shift = new THREE.Vector3(target.position.x, target.position.y, target.position.z + 10).sub(o.targetPosition);
    for (const tail of o.tails) if (tail) for (const corner of tail) corner.add(shift);
    o.targetPosition.add(shift);
    const clock = this.worldTime / 40;
    const frame = (o.index % 2 ? clock : -clock) + o.index * 53731;
    const dir = magicScrewAt(frame, 1);
    o.position.set(o.targetPosition.x + dir.x * 80, o.targetPosition.y + dir.y * 80, 110 + o.targetPosition.z + dir.z * 120);
    return true;
  }

  // BITMAP_JOINT_SPIRIT: SubType 0 (Evil Spirit) wanders towards its target with a dark head
  // (MODEL_LASER) on the big ones; 2, 6 and 7 fly out as lights (Swell Life, Nova).
  private moveSpirit(o: Joint): void {
    if (o.subType === 0) {
      if (o.scale === 80) this.createEffect('laser', o.position, o.angle, o.light);
      if (o.target) o.targetPosition.copy(o.target.position).setZ(o.target.position.z + 80);
      const dx = o.position.x - o.targetPosition.x, dy = o.position.y - o.targetPosition.y;
      moveHumming(o.position, o.angle, o.targetPosition, 10);
      if (!o.collision && Math.sqrt(dx * dx + dy * dy) <= o.velocity * 2) o.collision = true;
      o.direction.x += (randInt(32) - 16) * 0.2;
      o.direction.z += (randInt(32) - 16) * 0.8;
      o.angle.x += o.direction.x;
      o.angle.z += o.direction.z;
      o.direction.x *= 0.6;
      o.direction.z *= 0.8;
      const height = this.heightAt(o.position.x, o.position.y);
      if (o.position.z < height + 100) {
        o.direction.x = 0;
        o.angle.x = -5;
      }
      if (o.position.z > height + 400) {
        o.direction.x = 0;
        o.angle.x = 5;
      }
      o.light.setScalar(o.lifeTime * 0.1);
      return;
    }
    let light = new THREE.Vector3();
    if (o.subType === 2) light.set(1, 0.5, 0.1);
    else if (o.skill === 0) light.set(0.3, 0.3, 1);
    else light.set(0.5, 0.5, 0.5);
    if (o.lifeTime < 10) {
      o.light.multiplyScalar(1 / 1.2);
      light = o.light.clone();
    } else if (o.lifeTime > 18 && o.lifeTime < 20 && o.subType === 2) {
      const angle = new THREE.Vector3(0, 0, randInt(360));
      const position = new THREE.Vector3(o.startPosition.x + randInt(200) - 100, o.startPosition.y + randInt(200) - 100, o.startPosition.z - 200);
      this.createJoint('flare', position, position, angle, 2, null, 40);
    }
    this.createSprite('light', o.position, 4 + (20 - o.lifeTime) / 5, light, o.target, randInt(360));
    o.velocity += 5;
  }

  // DescribeJoint: face one (the z corners) and face two (the x corners); thunder scrolls.
  private drawJoint(o: Joint): void {
    const scroll = (Math.floor(this.worldTime) % 1000) * 0.001;
    const thunder = o.type === 'jointThunder';
    const blend: JointBlend = o.blend;
    for (let j = 0; j < o.numTails; j++) {
      if (o.type === 'smokeTrail' && j < 1) continue;
      const current = o.tails[j], next = o.tails[j + 1];
      if (!current || !next) continue;
      if (o.type === 'jointForce') {
        // BITMAP_JOINT_FORCE SubType 0: the x corners only, fading along the arc.
        const u1 = (o.numTails - j) / (o.maxTails - 1), u2 = (o.numTails - (j + 1)) / (o.maxTails - 1);
        const luminosity = (o.maxTails - j) / o.maxTails * 2 * o.light.x;
        this.quads.quad(o.texture, blend, [current[0], current[1], next[1], next[0]], [u1, u1, u2, u2], [0, 1, 1, 0], new THREE.Vector3(luminosity, luminosity, luminosity));
        continue;
      }
      let l1 = (o.numTails - j) / (o.maxTails - 1);
      let l2 = (o.numTails - (j + 1)) / (o.maxTails - 1);
      if (thunder) {
        l1 = l1 * 2 - scroll;
        l2 = l2 * 2 - scroll;
      }
      if (o.faces & 1) this.quads.quad(o.texture, blend, [current[2], current[3], next[3], next[2]], [l1, l1, l2, l2], [1, 0, 0, 1], o.light);
      if (thunder) {
        l1 += scroll * 2;
        l2 += scroll * 2;
      }
      if (o.faces & 2) this.quads.quad(o.texture, blend, [current[0], current[1], next[1], next[0]], [l1, l1, l2, l2], [0, 1, 1, 0], o.light);
    }
  }

  // UpdateDrawnJointEffects: the spear light's flare in the middle of its trail.
  private jointSprites(o: Joint): void {
    if (o.type !== 'spearSkill' || o.subType === 2 || o.numTails <= 0) return;
    const current = o.tails[Math.trunc(o.numTails / 2)];
    if (!current || !o.tails[Math.trunc(o.numTails / 2) + 1]) return;
    const position = new THREE.Vector3();
    for (const corner of current) position.add(corner);
    position.multiplyScalar(0.25);
    const light = o.light.clone();
    if (o.target) {
      const height = (current[0].z - (o.target.position.z + 50)) * 0.01;
      if (height > 0) light.subScalar(height);
    }
    this.createSprite('flareBlue', position, 0.7, light);
  }

  // =========================================================================
  // CreateEffect (ZzzEffect.cpp).
  // =========================================================================

  createEffect(type: EffectType, position: THREE.Vector3, angle: THREE.Vector3, light: THREE.Vector3,
    subType = 0, owner: EffectOwner | null = null, scale = 0, extra: CreateEffectExtra = {}): Effect | null {
    if (!this.catalogue) {
      const [p, a, l] = [position.clone(), angle.clone(), light.clone()];
      this.waiting.push(() => { this.createEffect(type, p, a, l, subType, owner, scale, extra); });
      return null;
    }
    // The common initialisation.
    const o: Effect = {
      type, subType, position: newVector(position), startPosition: new THREE.Vector3(), angle: newVector(angle),
      headAngle: new THREE.Vector3(), direction: new THREE.Vector3(), light: newVector(light), scale: scale <= 0 ? 0.9 : scale,
      lifeTime: 0, gravity: 0, velocity: 0.3, blendMesh: -1, blendMeshLight: 1, blendMeshTexCoordU: 0, blendMeshTexCoordV: 0,
      hiddenMesh: -1, alpha: 1, renderType: 'normal', owner, animationFrame: 0,
      counter: 0, kind: 0, skill: extra.skillIndex ?? 0, pkKey: extra.pkKey ?? -1, distance: 0,
      model: null, extra: null, self: null as unknown as EffectOwner, live: true,
    };
    o.self = { position: o.position, angle: o.angle, light: o.light, isLive: () => o.live };
    const known = EXTRA_EFFECTS[type];
    if (known) {
      // A registered type: the catalogue's values (when it has a row), then its creation hook.
      if (this.catalogue.has(type)) this.catalogue.apply(type, o, { position, angle, light, scale });
      if (!known.create(o, this)) return null;
    } else if (this.catalogue.has(type)) {
      this.catalogue.apply(type, o, { position, angle, light, scale });
    } else if (!this.initEffect(o)) {
      return null;
    }
    this.effects.push(o);
    this.effectIndex++;
    const file = EFFECT_MODELS[type] ?? known?.model;
    if (file && o.hiddenMesh !== -2) void this.attachModel(o, file[0], file[1]);
    else if (file && o.hiddenMesh === -2 && known?.alwaysModel) void this.attachModel(o, file[0], file[1]);
    return o;
  }

  private initEffect(o: Effect): boolean {
    switch (o.type) {
      case 'fireModel':
        o.blendMesh = 1;
        if (o.subType === 0) { // Meteorite
          o.lifeTime = 40;
          o.scale = (randInt(8) + 10) * 0.1;
          o.position.x += 130 + randInt(32);
          o.position.z += 400;
          o.direction.set(0, 0, -50);
          o.angle.set(0, 20, 0);
          return true;
        }
        if (o.subType === 1) { // Fire Ball
          o.lifeTime = 60;
          o.scale = (randInt(4) + 8) * 0.1;
          o.position.z += 120;
          o.direction.set(0, -50, 0);
          return true;
        }
        if (o.subType === 6) { // Decay
          o.lifeTime = 40;
          o.hiddenMesh = -2;
          o.scale = (randInt(8) + 15) * 0.1;
          o.pkKey = -1;
          o.position.x += randInt(100) + 200;
          o.position.y += randInt(100) - 50;
          o.position.z += randInt(300) + 500;
          o.direction.set(0, 0, -50 - randInt(50));
          o.headAngle.set(0, 20, 0);
          this.createJoint('smokeTrail', o.position, o.position, o.headAngle, 0, o.self, 100);
          return true;
        }
        return false;
      case 'magic2':
        o.blendMesh = 0;
        o.lifeTime = 20;
        o.direction.set(0, -60, 0);
        return o.subType === 0 || o.subType === 2;
      case 'magicGround2':
        o.lifeTime = 20;
        if (o.subType === 4) { // Swell Life's circle
          o.lifeTime = 40;
          o.scale = (randInt(50) + 50) / 100 * 4;
        }
        return o.subType >= 1 && o.subType <= 4;
      case 'stone1':
      case 'stone2': {
        if (o.subType !== 0 && o.subType !== 1 && o.subType !== 2 && o.subType !== 10) return false;
        // The MODEL_ICE_SMALL branch they share: no push for SubType 1.
        const p1 = new THREE.Vector3(0, o.subType === 1 ? 0 : (randInt(256) + 64) * 0.1, 0);
        o.lifeTime = randInt(16) + 32;
        o.scale = (randInt(4) + 8) * 0.1;
        o.angle.z = randInt(360);
        rotateByAngle(p1, o.angle, o.direction);
        o.gravity = randInt(16) + 8;
        return true;
      }
      case 'iceSmall': {
        if (o.subType !== 0) return false;
        o.blendMesh = 0;
        o.blendMeshLight = 0.3;
        o.position.z += 50;
        const p1 = new THREE.Vector3(0, (randInt(256) + 64) * 0.1, 0);
        o.lifeTime = randInt(16) + 32;
        o.scale = (randInt(4) + 8) * 0.1;
        o.angle.z = randInt(360);
        rotateByAngle(p1, o.angle, o.direction);
        o.gravity = randInt(16) + 8;
        return true;
      }
      case 'ice':
        if (o.subType !== 0) return false;
        o.lifeTime = 50;
        o.scale = 0.8;
        o.velocity = 1;
        o.angle.x = 0;
        o.blendMesh = 0;
        return true;
      case 'flame':
        if (o.subType === 0) o.lifeTime = 40;
        else if (o.subType === 5) o.lifeTime = 20;
        else return false;
        return true;
      case 'storm':
        if (o.subType !== 0) return false;
        o.lifeTime = 59;
        o.blendMesh = 0;
        o.direction.set(0, -10, 0);
        o.position.z = this.heightAt(o.position.x, o.position.y);
        return true;
      case 'circle':
        o.lifeTime = 45;
        o.blendMesh = 0;
        if (o.subType === 1) {
          o.scale = 1;
          o.hiddenMesh = -2;
        } else if (o.subType !== 0) {
          return false;
        }
        return true;
      case 'bossLaser': {
        if (o.subType !== 0) return false;
        o.lifeTime = 20;
        o.light.set(0.5, 0.7, 1);
        o.scale = 16;
        rotateByAngle(new THREE.Vector3(0, -50, 0), o.angle, o.direction);
        return true;
      }
      case 'skillInferno':
        o.blendMesh = -2;
        o.scale = 0.9;
        o.velocity = 0.5;
        if (o.subType === 0) {
          o.light.set(0.8, 0.8, 0.8);
          o.lifeTime = 15;
        } else if (o.subType === 2) {
          o.lifeTime = 12;
          o.hiddenMesh = o.skill;
          o.skill = 0;
          o.distance = o.pkKey;
          o.scale = o.pkKey / 100;
          o.pkKey = -1;
          o.blendMeshLight = 0.1;
        } else {
          return false;
        }
        return true;
      case 'blizzardModel':
        if (o.subType === 0) {
          o.blendMesh = -2;
          o.lifeTime = randInt(15) + 15;
          o.gravity = -20;
          o.velocity = randInt(360);
          o.light.set(0, 0, 0);
          o.scale = 0.5;
          o.gravity -= randInt(30) + 10;
          o.position.x += randInt(300) - 150 + 100;
          o.position.y += randInt(300) - 150;
          o.position.z += 600;
          o.startPosition.copy(o.position);
          return true;
        }
        if (o.subType === 1) {
          o.blendMesh = -2;
          o.lifeTime = 20;
          o.velocity = 0;
          return true;
        }
        return false;
      case 'laser':
        if (o.subType !== 0) return false;
        o.lifeTime = 1;
        o.blendMesh = 0;
        o.blendMeshLight = o.light.x;
        o.scale = 1.3;
        o.renderType = 'dark';
        return true;
      default: // the catalogue's types (energy, poison, circleLight, spark3) don't come here
        return false;
    }
  }

  private async attachModel(o: Effect, dir: string, file: string): Promise<void> {
    const model = await loadEffectModel(dir, file);
    if (!model || !o.live) return;
    const parts = buildGeometries(model, computeBoneMatrices(model, 0, 0));
    const materials = await Promise.all(parts.map(async (part) => {
      const blend = o.blendMesh <= -2 || model.meshes[part.meshIndex]?.texture === o.blendMesh;
      const material = await createMaterial(MODEL_TEXTURE_DIRS, part.textureName, { glow: blend });
      if (blend && material.map) {
        // Its own copy of the texture, so the scroll (BlendMeshTexCoordU) is this effect's.
        material.map = material.map.clone();
        material.map.wrapS = THREE.RepeatWrapping;
        material.map.wrapT = THREE.RepeatWrapping;
        material.map.needsUpdate = true;
        material.userData.ownMap = true;
      }
      if (blend && o.renderType === 'dark') applyBlend(material, 'subtract');
      material.userData.blend = blend;
      material.transparent = true;
      return material;
    }));
    if (!o.live) {
      for (const material of materials) material.dispose();
      for (const part of parts) part.geometry.dispose();
      return;
    }
    const animated = new AnimatedModel(model, parts, materials);
    o.model = animated;
    this.root.add(animated.group);
    this.placeModel(o);
  }

  private destroyEffect(o: Effect): void {
    o.live = false;
    if (o.extra) {
      o.extra.removeFromParent();
      o.extra = null;
    }
    const model = o.model;
    if (!model) return;
    model.group.removeFromParent();
    for (const part of model.parts) {
      part.geometry.dispose();
      const material = part.mesh.material;
      if (material.userData.ownMap === true) material.map?.dispose();
      material.dispose();
    }
    o.model = null;
  }

  private moveEffect(o: Effect): void {
    let luminosity = (randInt(4) + 7) * 0.1;
    if (o.lifeTime < 5) luminosity = Math.max(0, luminosity - (5 - o.lifeTime) * 0.2);
    let tail = true;
    const known = EXTRA_EFFECTS[o.type];
    if (known) {
      tail = known.move(o, this, luminosity);
    } else {
      switch (o.type) {
        case 'energy': this.moveEnergy(o); break;
        case 'fireModel': this.moveFire(o, luminosity); break;
        case 'magic2': this.moveMagic2(o, luminosity); break;
        case 'magicGround2':
          if (o.subType !== 4) this.createHealing(o);
          break;
        case 'stone1':
        case 'stone2':
        case 'iceSmall': this.moveIceSmall(o); break;
        case 'ice': this.moveIce(o); break;
        case 'poison':
          o.blendMeshLight = o.lifeTime * 0.1;
          o.alpha = o.lifeTime * 0.1;
          break;
        case 'flame': this.moveFlame(o); break;
        case 'storm': this.moveStorm(o); break;
        case 'circle': this.moveCircle(o); break;
        case 'circleLight': this.moveCircleLight(o); break;
        case 'skillInferno':
          if (o.subType === 2) o.scale += 0.04;
          o.blendMeshLight = o.lifeTime / 20;
          break;
        case 'blizzardModel': this.moveBlizzard(o); break;
        case 'spark3': this.moveTeleportSparks(o); break;
        default: break;
      }
    }
    if (!o.live) return;
    // The shared tail: the model's animation, MoveParticle(o, true), the life time.
    if (tail) {
      if (o.model) {
        const keys = o.model.skeleton.actions[0]?.numKeys ?? 1;
        o.animationFrame = Math.min(o.animationFrame + o.velocity, Math.max(0, keys - 1));
      }
      if (o.type !== 'bossLaser') o.position.add(rotateByAngle(o.direction, o.angle));
    }
    o.lifeTime -= 1;
    if (o.lifeTime <= 0) o.live = false;
  }

  // BITMAP_ENERGY, SubType 0: the ball leaves energy and sparks, and bursts at its target.
  private moveEnergy(o: Effect): void {
    if (o.subType !== 0) return;
    const luminosity = o.lifeTime * 0.2;
    const light = new THREE.Vector3(luminosity, luminosity, luminosity);
    this.createParticle('energy', o.position, o.angle, light);
    this.createParticle('spark3', o.position, o.angle, light, 0, 4);
    if (this.checkTargetRange(o)) this.createParticle('spark3', o.position, o.angle, WHITE, 1, 6);
  }

  // MODEL_FIRE: Fire Ball (1) flies at its target; Meteorite (0) and Decay (6) fall and burst on
  // the ground, Meteorite in stones and fire, Decay in a green splash.
  private moveFire(o: Effect, luminosity: number): void {
    const falling = o.subType === 0 || o.subType === 6;
    if (falling) {
      if (o.subType === 6) {
        this.createSprite('light', o.position, 3, new THREE.Vector3(0.5, 0.5, 1));
        this.createSprite('shiny2', o.position, 3, WHITE, null, randInt(360), 1);
        this.createSprite('shiny2', o.position, 4, o.light, null, randInt(360));
        o.angle.copy(o.headAngle);
      }
      const height = this.heightAt(o.position.x, o.position.y);
      if (o.position.z < height) {
        o.position.z = height;
        o.direction.set(0, 0, 0);
        const position = new THREE.Vector3(o.position.x, o.position.y, o.position.z + 80);
        let light = WHITE.clone();
        if (o.subType === 6) {
          this.createEffect('skillInferno', o.position, new THREE.Vector3(), new THREE.Vector3(0, 0.5, 0), 2, o.self, 0, { pkKey: 30, skillIndex: 0 });
          light = new THREE.Vector3(0.1, 0.5, 0.1);
          for (let i = 0; i < 15; i++) {
            const p = new THREE.Vector3(o.position.x + randInt(160) - 80, o.position.y + randInt(160) - 100, o.position.z + 50);
            this.createParticle('smoke', p, o.angle, light, 11, (randInt(32) + 80) * 0.025);
          }
          for (let j = 0; j < 6; j++) this.createEffect(randInt(2) ? 'stone2' : 'stone1', o.position, o.angle, light);
        } else {
          for (let j = 0; j < 6; j++) this.createEffect(randInt(2) ? 'stone2' : 'stone1', o.position, o.angle, o.light);
        }
        this.createParticle('explosion', position, o.angle, light);
        o.live = false;
        return;
      }
    }
    o.blendMeshLight = o.subType === 0 || o.subType === 6 ? (randInt(4) + 4) * 0.1 : 0;
    const light = o.subType === 6
      ? new THREE.Vector3(luminosity * 0.1, luminosity, 0)
      : new THREE.Vector3(luminosity, luminosity * 0.1, 0);
    if (o.subType === 6) {
      for (let j = 0; j < 2; j++) this.createParticle('fire', new THREE.Vector3(o.position.x + randInt(50) - 25, o.position.y, o.position.z), o.angle, light, 5);
    } else {
      this.createParticle('fire', o.position, o.angle, light, 5);
    }
    if (o.subType === 1 && this.checkTargetRange(o)) {
      for (let j = 0; j < 2; j++) this.createEffect(randInt(2) ? 'stone2' : 'stone1', o.position, o.angle, o.light);
    }
  }

  // Move_MODEL_MAGIC2: the wave's glow fades and scrolls, smoke follows; Power Slash (SubType 2)
  // hides the wave and draws shining lights instead.
  private moveMagic2(o: Effect, luminosity: number): void {
    o.blendMeshLight = o.lifeTime * 0.1;
    o.blendMeshTexCoordU = -o.lifeTime * 0.2;
    for (let j = 0; j < 4; j++) this.createParticle('smoke', o.position, o.angle, o.light, 3);
    const light = new THREE.Vector3(luminosity * 0.3, luminosity * 0.6, luminosity);
    if (o.subType !== 2) return;
    o.hiddenMesh = 0;
    this.createParticle('smoke', o.position, o.angle, light, 11, (randInt(32) + 80) * 0.015);
    const position = o.position.clone().setZ(o.position.z + 50);
    this.createSprite('shiny2', position, 1.5, WHITE, null, randInt(360));
    this.createSprite('shiny2', position, 1.5, WHITE, null, randInt(360));
    this.createSprite('light', position, 3.5, light, null, randInt(360));
  }

  // CreateHealing: three healing trails from 200 units away fly into the circle's owner.
  private createHealing(o: Effect): void {
    const p = new THREE.Vector3(0, -200, 0);
    for (let j = 0; j < 3; j++) {
      const angle = new THREE.Vector3(randInt(90), 0, randInt(360));
      const position = o.position.clone().sub(rotateByAngle(p, angle));
      position.z += 120;
      this.createJoint('jointHealing', position, o.position, angle, o.subType, o.owner, 5);
    }
  }

  /** CreateForce: three trails from 500 units away gather at the point (Nova's charge). */
  createForce(owner: EffectOwner, at: THREE.Vector3): void {
    const p = new THREE.Vector3(0, -500, 0);
    for (let j = 0; j < 3; j++) {
      const angle = new THREE.Vector3(randInt(90), 0, randInt(360));
      const position = at.clone().sub(rotateByAngle(p, angle));
      position.z += 120;
      this.createJoint('jointHealing', position, at, angle, 8, owner, 10);
    }
  }

  /** CreateBomb: a burst of sparks and an explosion 80 over the point (SubType 0's). */
  createBomb(p: THREE.Vector3, explosion: boolean): void {
    const position = p.clone().setZ(p.z + 80);
    let angle = new THREE.Vector3();
    for (let j = 0; j < 20; j++) {
      angle = new THREE.Vector3(randInt(60) + 60 + 90, 0, randInt(30));
      this.createParticle('spark', position, angle, WHITE, 2);
    }
    if (explosion) this.createParticle('explosion', position, angle, new THREE.Vector3(0.7, 0.7, 0.7));
  }

  /** CreateInferno (SubType 0): eight bombs and their stones in a ring of 220. */
  createInferno(position: THREE.Vector3): void {
    for (let j = 0; j < 8; j++) {
      const angle = new THREE.Vector3(0, 0, j * 45);
      const p2 = rotateByAngle(new THREE.Vector3(0, -220, 0), angle).add(position);
      this.createBomb(p2, true);
      this.createEffect(randInt(2) ? 'stone2' : 'stone1', p2, angle, WHITE, 1);
      this.createEffect(randInt(2) ? 'stone2' : 'stone1', p2, angle, WHITE, 0);
    }
  }

  // Move_MODEL_ICE_SMALL (also the stones): bouncing and smoking or burning; SubType 1 stones
  // float up, SubType 2 ones spin up (Twister).
  private moveIceSmall(o: Effect): void {
    if (o.subType === 0 || o.subType === 10) o.position.add(o.direction);
    else o.position.add(rotateByAngle(o.direction, o.angle));
    o.direction.multiplyScalar(0.9);
    o.position.z += o.gravity;
    if (o.subType === 0 || o.subType === 10) {
      o.gravity -= 3;
      const height = this.heightAt(o.position.x, o.position.y);
      if (o.position.z < height) {
        o.position.z = height;
        o.gravity = -o.gravity * 0.5;
        o.lifeTime -= 4;
        o.angle.x -= o.scale * 128;
      } else {
        o.angle.x -= o.scale * 32;
      }
      if (o.subType === 0 && randInt(10) === 0) {
        if (o.type === 'iceSmall') this.createParticle('smoke', o.position, o.angle, o.light);
        else this.createParticle('fire', o.position, o.angle, o.light, 1 + randInt(3));
      }
    } else if (o.subType === 1) {
      o.gravity += 0.5;
    } else {
      o.angle.z += 20;
      o.gravity += 0.5;
    }
  }

  // MODEL_ICE SubType 0: the crystal grows, then smokes and fades.
  private moveIce(o: Effect): void {
    if (o.animationFrame < 5) return;
    o.velocity = 0;
    if (randInt(2) === 0) {
      const position = new THREE.Vector3(o.position.x + randInt(64) - 32, o.position.y + randInt(64) - 32, o.position.z + randInt(128) + 32);
      this.createParticle('smoke', position, o.angle, o.light);
    }
    o.alpha -= 0.05;
    if (o.alpha < 0) o.live = false;
  }

  // Move_BITMAP_FLAME: SubType 0 (Flame on the ground) burns with six flames and throws stones;
  // SubType 5 (at a target) burns with one.
  private moveFlame(o: Effect): void {
    if (o.subType === 0) {
      for (let j = 0; j < 6; j++) {
        const position = new THREE.Vector3(randInt(50) - 25, randInt(50) - 25, 0).add(o.position);
        this.createParticle('flame', position, o.angle, WHITE);
      }
      this.createEffect(randInt(2) ? 'stone2' : 'stone1', o.position, o.angle, o.light);
    } else if (o.subType === 5) {
      const position = new THREE.Vector3(randInt(32) - 16, randInt(32) - 16, 0).add(o.position);
      this.createParticle('flame', position, o.angle, WHITE, 0, o.scale);
    }
  }

  // Move_MODEL_STORM SubType 0 (Twister): the funnel fades and scrolls, smoke and lightning hit
  // it from above, stones spin up from the ground.
  private moveStorm(o: Effect): void {
    o.blendMeshLight = o.lifeTime * 0.1;
    o.blendMeshTexCoordU = -o.lifeTime * 0.1;
    this.createParticle('smoke', o.position, o.angle, o.light, 3);
    const angle = new THREE.Vector3(90, 0, o.angle.z);
    if (randInt(2) === 0) this.createJoint('jointThunder', new THREE.Vector3(o.position.x - 200, o.position.y, o.position.z + 700), o.position, angle, 0, o.self, 10);
    if (randInt(2) === 0) this.createJoint('jointThunder', new THREE.Vector3(o.position.x + 200, o.position.y, o.position.z + 700), o.position, angle, 0, o.self, 10);
    o.position.z = this.heightAt(o.position.x, o.position.y);
    if (randInt(4) === 0) this.createEffect(randInt(2) ? 'stone2' : 'stone1', o.position, o.angle, o.light, 2);
  }

  // Move_MODEL_CIRCLE: Hellfire's circle (0) fades; Nova's hidden ring (1) sends out its lights.
  private moveCircle(o: Effect): void {
    o.blendMeshLight = o.lifeTime * 0.1;
    if (o.subType !== 1) return;
    const skillCount = 0; // m_bySkillCount: the charge the server reports
    if (o.lifeTime <= 44 - skillCount) return;
    for (let i = 0; i < 36; i++) {
      const angle = new THREE.Vector3(-10, 0, i * (10 + randInt(10)));
      const position = o.position.clone().setZ(o.position.z + 100);
      this.createJoint('jointSpirit', position, position, angle, 6, o.self, 60);
      if (Math.trunc(o.lifeTime) === 44 - skillCount + 1) {
        angle.z = i * 10;
        this.createJoint('jointSpirit', position, position, angle, 7, o.self, 60);
      }
    }
  }

  // Move_MODEL_CIRCLE_LIGHT (SubType 0): the light rises and falls, scrolls, and stones float up.
  private moveCircleLight(o: Effect): void {
    o.blendMeshLight = o.lifeTime >= 30 ? (40 - o.lifeTime) * 0.1 : o.lifeTime * 0.1;
    o.blendMeshTexCoordU = -o.lifeTime * 0.01;
    if (randInt(4) === 0) {
      const angle = new THREE.Vector3(0, 0, randInt(360));
      const position = rotateByAngle(new THREE.Vector3(0, randInt(300), 0), angle).add(o.position);
      this.createEffect(randInt(2) ? 'stone2' : 'stone1', position, o.angle, o.light, 1);
    }
  }

  // Move_MODEL_BLIZZARD: SubType 0 falls from the sky trailing ice smoke, energy and lights, and
  // leaves a splash (SubType 1, which fades) and ice shards where it lands.
  private moveBlizzard(o: Effect): void {
    if (o.subType === 1) {
      o.blendMeshLight *= 1 / 1.1;
      return;
    }
    o.position.x = o.startPosition.x + Math.sin(randInt(1000) * 0.01) * 10;
    o.position.y = o.startPosition.y + Math.sin(randInt(1000) * 0.01) * 10;
    o.position.z += o.gravity;
    o.gravity -= randInt(5);
    o.startPosition.x -= 10;
    this.createParticle('smoke', o.position, o.angle, WHITE, 0, 1.5);
    if (randInt(2) === 0) this.createParticle('energy', o.position, o.angle, WHITE, 1, 0.5);
    else this.createParticle('fire3', o.position, o.angle, WHITE, 7, o.scale);
    o.light.x += 0.1;
    o.light.y = o.light.x;
    o.light.z = o.light.x;
    this.createSprite('shiny2', o.position, (randInt(4) + 4) * 0.2, o.light, o.self, randInt(360));
    this.createSprite('light', o.position, 1, o.light, o.self, randInt(360));
    const height = this.heightAt(o.position.x, o.position.y);
    if (o.position.z < height) {
      const position = o.position.clone().setZ(o.position.z + 50);
      this.createParticle('smoke', position, o.angle, new THREE.Vector3(0.24, 0.28, 0.8), 11, (randInt(32) + 80) * 0.025);
      if (randInt(5) === 0) this.createEffect('iceSmall', position, o.angle, o.light);
      this.createEffect('blizzardModel', position, o.angle, o.light, 1, null, 0, { pkKey: o.pkKey });
      o.live = false;
    }
  }

  // BITMAP_SPARK+1 (Teleport): a column of eighteen sparks over the caster, fading.
  private moveTeleportSparks(o: Effect): void {
    const position = o.position.clone();
    const luminosity = o.lifeTime * 0.1;
    const light = new THREE.Vector3(luminosity, luminosity, luminosity);
    const scale = 6;
    for (let j = 0; j < 18; j++) {
      position.z += scale * 4;
      this.createParticle('spark3', position, o.angle, light, 1, j === 0 ? scale * 2 : scale);
    }
  }

  // CheckTargetRange: within 100 (flat) of its live owner the effect ends next tick (LifeTime 1).
  checkTargetRange(o: Effect): boolean {
    const to = o.owner;
    if (!to || !ownerLive(to)) return false;
    const dx = o.position.x - to.position.x, dy = o.position.y - to.position.y;
    if (Math.sqrt(dx * dx + dy * dy) > TARGET_RANGE) return false;
    o.lifeTime = 1;
    return true;
  }

  // RenderObject for the model effects: the blend mesh additive with BlendMeshLight, HiddenMesh hidden.
  private placeModel(o: Effect): void {
    const model = o.model;
    if (!model) return;
    model.group.visible = o.hiddenMesh !== -2;
    model.group.position.copy(o.position);
    angleEuler(o.angle, model.group.rotation);
    model.group.scale.setScalar(o.scale);
    model.pose(0, o.animationFrame);
    for (const part of model.parts) {
      const material = part.mesh.material;
      part.mesh.visible = o.hiddenMesh !== part.meshIndex;
      if (material.userData.blend === true) {
        material.color.setRGB(o.light.x, o.light.y, o.light.z).multiplyScalar(o.blendMeshLight);
        if (material.map) {
          material.map.offset.x = o.blendMeshTexCoordU;
          material.map.offset.y = o.blendMeshTexCoordV;
        }
      } else {
        material.color.setRGB(o.light.x, o.light.y, o.light.z);
      }
      material.opacity = Math.max(0, Math.min(1, o.alpha));
    }
  }

  // BITMAP_MAGIC+1 (DescribeEffectShadow): the circle on the ground grows and fades.
  private drawGroundCircle(o: Effect): void {
    let luminosity = 1;
    if (o.lifeTime < 5) luminosity -= (5 - o.lifeTime) * 0.2;
    let scale = (20 - o.lifeTime) * 0.15;
    if (o.subType === 4) {
      scale = o.scale;
      if (luminosity === 1) luminosity = Math.sin((60 - o.lifeTime) * 0.05) + 0.5;
    }
    const light = o.subType === 2 ? new THREE.Vector3(0.4, 1, 0.6)
      : o.subType === 3 ? new THREE.Vector3(1, 0.6, 0.4)
      : o.subType === 4 ? new THREE.Vector3(1, 0.5, 0.1) : new THREE.Vector3(0.4, 0.6, 1);
    light.multiplyScalar(luminosity);
    this.quads.decal('magicGround2', 'glow', o.position.x, o.position.y, scale, light, -o.angle.z,
      this.options.heightAt ?? null, o.position.z);
  }

  // UpdateDrawnEffectSprites for the drawn bitmap effects: Aqua Beam's twenty sparks along its direction.
  private effectSprites(o: Effect): void {
    if (o.type === 'bossLaser') {
      const position = o.position.clone();
      for (let j = 0; j < 20; j++) {
        this.createSprite('spark3', position, o.scale, o.light, o.self);
        position.add(o.direction);
      }
    }
    EXTRA_EFFECTS[o.type]?.sprites?.(o, this);
  }

  // =========================================================================
  // The tick and the draw.
  // =========================================================================

  private step(): void {
    this.tick++;
    this.worldTime += TICK_SECONDS * 1000;
    this.sprites = []; // ClearSprites
    this.tasks = this.tasks.filter((task) => task(this.tick));
    for (let i = 0; i < this.effects.length; i++) {
      const effect = this.effects[i];
      if (effect.live) this.moveEffect(effect);
    }
    this.effects = this.effects.filter((effect) => {
      if (!effect.live) this.destroyEffect(effect);
      return effect.live;
    });
    for (const effect of this.effects) this.effectSprites(effect);
    for (let i = 0; i < this.joints.length; i++) {
      const joint = this.joints[i];
      if (!joint.live) continue;
      this.moveJoint(joint);
      // MoveJointForFrame: the spear flares (Impale's, Death Stab's) move again until their life
      // time reaches a multiple of 12.
      const spearFlare = joint.type === 'flare' && (joint.subType === 4 || joint.subType === 12);
      for (let moves = 1; spearFlare && moves < 64 && joint.live && Math.trunc(joint.lifeTime) % 12 !== 0; moves++) this.moveJoint(joint);
    }
    this.joints = this.joints.filter((joint) => joint.live);
    for (const joint of this.joints) this.jointSprites(joint);
    for (let i = 0; i < this.particles.length; i++) this.moveParticle(this.particles[i]);
    this.particles = this.particles.filter((particle) => particle.live);
    for (const layer of this.layers) layer.tick();
  }

  private draw(): void {
    this.quads.beginFrame(this.options.camera ?? null);
    for (const effect of this.effects) {
      if (effect.type === 'magicGround2') this.drawGroundCircle(effect);
      this.placeModel(effect);
      EXTRA_EFFECTS[effect.type]?.draw?.(effect, this);
    }
    for (const joint of this.joints) this.drawJoint(joint);
    for (const layer of this.layers) layer.draw(this.quads);
    for (const particle of this.particles) this.drawParticle(particle);
    for (const sprite of this.sprites) {
      const size = this.quads.textureSize(sprite.type);
      if (!size) continue;
      // DescribeSprite: Width x Height of the texture times the scale; SubType 0 glows.
      this.quads.billboard(sprite.type, 'glow', sprite.position, size.width * sprite.scale, size.height * sprite.scale, sprite.light, sprite.rotation);
    }
    this.quads.endFrame();
  }
}
