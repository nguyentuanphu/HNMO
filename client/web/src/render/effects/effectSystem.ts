// The effect system: CreateEffect / CreateParticle / CreateSprite / CreateJoint and their move
// and draw passes, stepped once per 25 FPS tick like the desktop (MoveEffects, MoveJoints,
// MoveParticles after ClearSprites, Scenes/MainScene.cpp). Only the types and SubTypes listed
// in types.ts are ported; each case cites the C++ it follows.
//
// Followed C++ (client/src/source/Render/Effects):
//   CreateEffect, MoveEffect (and its shared tail), CheckTargetRange, CreateHealing,
//   MoveParticle, RenderEffects, DescribeEffectShadow (BITMAP_MAGIC+1)   ZzzEffect.cpp
//   Move_MODEL_MAGIC2, Move_MODEL_ICE_SMALL                              Behaviors/MoveHandlers.cpp
//   CreateParticle, MoveParticles, DescribeParticle, RenderParticles     ZzzEffectParticle.cpp
//   CreateSprite, DescribeSprite                                         zzzeffectsprite.cpp
//   CreateJoint, CreateTail, FollowTail, MoveJoint, DescribeJoint,
//   UpdateDrawnJointEffects                                              ZzzEffectJoint.cpp

import * as THREE from 'three';
import { loadModel, buildGeometries, computeBoneMatrices, createMaterial, AnimatedModel } from '../../bmd.js';
import type { BMDModel } from '../../formats.js';
import { EffectCatalogue } from './catalogue.js';
import {
  TICK_SECONDS, randInt, rotateByAngle, angleEuler, moveHumming, magicScrewAt, preloadEffectTextures,
} from './core.js';
import type { BitmapName } from './core.js';
import { QuadBatches } from './quadBatch.js';
import type { HeightAt } from './quadBatch.js';
import type { Effect, EffectOwner, EffectType, Joint, JointType, Particle, ParticleType, Sprite, SpriteType, Tail } from './types.js';

/** At most this many ticks are caught up in one update (after a long pause the rest is dropped). */
const MAX_TICKS_PER_UPDATE = 10;
const WHITE = new THREE.Vector3(1, 1, 1);
/** CheckTargetRange: an effect aimed at an owner ends within this flat distance of it. */
const TARGET_RANGE = 100;

// The models of the model effects (AccessModel in Engine/Object/ZzzOpenData.cpp).
const EFFECT_MODELS: Partial<Record<EffectType, string>> = {
  fireModel: 'Fire01.bmd',
  magic2: 'Magic02.bmd',
  stone1: 'Stone01.bmd',
  stone2: 'Stone02.bmd',
};
const MODEL_DIR = 'Skill';
const MODEL_TEXTURE_DIRS = ['Skill', 'Effect', 'Item'];

const PARTICLE_TEXTURES: Record<ParticleType, BitmapName> = {
  energy: 'energy', spark: 'spark', spark3: 'spark3', fire: 'fire', smoke: 'smoke', blood2: 'blood2',
};
const JOINT_TEXTURES: Record<JointType, BitmapName> = {
  jointThunder: 'jointThunder', jointHealing: 'jointHealing', spearSkill: 'flareBlue',
};

export interface EffectSystemOptions {
  /** The camera, for billboards. Without it sprites face +y. */
  camera?: THREE.Camera;
  /** Ground height (RequestTerrainHeight). Without it the ground is z = 0. */
  heightAt?: HeightAt;
}

/** Something the system runs once per tick until it returns false (a skill's timeline). */
export type TickTask = (tick: number) => boolean;

interface LoadedModel {
  model: BMDModel;
}

const modelLoads = new Map<string, Promise<LoadedModel | null>>();

function loadEffectModel(file: string): Promise<LoadedModel | null> {
  let load = modelLoads.get(file);
  if (!load) {
    load = loadModel(MODEL_DIR, file).then((model) => ({ model }), (error: unknown) => {
      console.warn(`Effect model ${file} not drawn`, (error as Error).message);
      return null;
    });
    modelLoads.set(file, load);
  }
  return load;
}

function newVector(v?: THREE.Vector3): THREE.Vector3 {
  return v ? v.clone() : new THREE.Vector3();
}

function ownerLive(owner: EffectOwner | null): boolean {
  return owner !== null && (owner.isLive?.() ?? true);
}

export class EffectSystem {
  readonly root: THREE.Group;
  private readonly options: EffectSystemOptions;
  private readonly quads: QuadBatches;
  private catalogue: EffectCatalogue | null = null;
  private readonly waiting: (() => void)[] = [];
  private effects: Effect[] = [];
  private particles: Particle[] = [];
  private joints: Joint[] = [];
  private sprites: Sprite[] = [];
  private tasks: TickTask[] = [];
  private accumulator = 0;
  private tick = 0;
  private jointIndex = 0;
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
        'magicGround2', 'jointThunder', 'jointHealing']),
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
      case 'spark':
        o.scale = (randInt(4) + 4) * 0.1;
        if (o.subType !== 0) return false;
        o.lifeTime = randInt(16) + 24;
        o.angle.z = randInt(360);
        o.gravity = randInt(16) + 6;
        rotateByAngle(new THREE.Vector3(0, (randInt(20) + 20) * 0.1, 0), o.angle, o.velocity);
        return true;
      case 'spark3':
        if (o.subType === 1) {
          o.angle.set(randInt(360), randInt(360), randInt(360));
          rotateByAngle(new THREE.Vector3(0, -50, 0), o.angle, o.velocity);
        }
        return o.subType === 0 || o.subType === 1;
      case 'fire':
        o.lifeTime = 24;
        if (o.subType === 1) {
          o.velocity.set(0, -(randInt(16) + 32) * 0.1, 0);
          o.scale = (randInt(4) + 10) * 0.01;
          o.rotation = randInt(360);
        } else if (o.subType === 5) {
          o.velocity.set(0, -(randInt(16) + 32) * 0.1, 0);
          o.scale = scale * (randInt(64) + 128) * 0.01;
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
        o.scale -= o.subType === 1 ? 2 : 0.5;
        if (o.scale < 0.2) o.live = false;
        break;
      case 'fire':
        o.gravity += 0.004;
        if (o.subType === 5) {
          o.scale -= 0.04;
          o.rotation += 5;
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
    }
  }

  // RenderParticles / DescribeParticle: JPEG textures glow, the others are alpha tested.
  private drawParticle(o: Particle): void {
    const size = this.quads.textureSize(o.texture);
    if (!size) return;
    const width = size.width * o.scale, height = size.height * o.scale;
    const blend = o.texture === 'blood2' ? 'alpha' : 'glow';
    switch (o.type) {
      case 'blood2':
        this.quads.billboard(o.texture, blend, o.position, width, height, o.light, 0, (o.frame % 2) * 0.5, Math.trunc(o.frame / 2) * 0.5, 0.5, 0.5);
        break;
      case 'fire':
        this.quads.billboard(o.texture, blend, o.position, width * 0.25, height, o.light,
          o.subType === 5 ? o.rotation : o.angle.x, (o.frame % 4) * 0.25, 0, 0.25, 1);
        break;
      default:
        this.quads.billboard(o.texture, blend, o.position, width, height, o.light, o.rotation);
    }
  }

  // =========================================================================
  // CreateJoint (ZzzEffectJoint.cpp): trails drawn as two crossed ribbons.
  // =========================================================================

  createJoint(type: JointType, position: THREE.Vector3, targetPosition: THREE.Vector3, angle: THREE.Vector3,
    subType = 0, target: EffectOwner | null = null, scale = 10): Joint | null {
    const o: Joint = {
      type, subType, texture: JOINT_TEXTURES[type],
      position: position.clone(), targetPosition: targetPosition.clone(), angle: angle.clone(),
      direction: new THREE.Vector3(), light: WHITE.clone(), scale, velocity: 0, lifeTime: 0,
      maxTails: 1, numTails: 0, tails: [], target, index: this.jointIndex++, live: true,
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
        if (o.subType !== 0) return false;
        o.lifeTime = 2;
        return true;
      case 'jointHealing':
        o.lifeTime = 12;
        o.maxTails = 2;
        o.velocity = 0;
        return o.subType >= 1 && o.subType <= 3;
      case 'spearSkill':
        if (!o.target) return false;
        o.targetPosition.copy(o.target.position);
        if (o.subType !== 4) return false;
        o.light.set(0.4, 0.8, 0.2);
        o.lifeTime = 10000;
        o.maxTails = 30;
        return true;
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

  // CreateTail (not blurred): a new tail at the joint's position, the older ones move back.
  private createTail(o: Joint, angle: THREE.Vector3): void {
    o.numTails = Math.min(o.numTails + 1, o.maxTails - 1);
    for (let j = o.numTails - 1; j >= 0; j--) o.tails[j + 1] = o.tails[j];
    o.tails[0] = this.tailAt(o.position, angle, o.scale);
  }

  private moveJoint(o: Joint): void {
    // The common move along -y of the joint's angle.
    if (o.velocity !== 0) o.position.add(rotateByAngle(new THREE.Vector3(0, -o.velocity, 0), o.angle));
    switch (o.type) {
      case 'jointThunder': this.moveThunder(o); break;
      case 'jointHealing': this.moveHealing(o); break;
      case 'spearSkill': if (!this.moveSpearSkill(o)) return; break;
    }
    if (!o.live) return;
    this.createTail(o, o.angle); // FollowTail, one tail per reference frame
    o.lifeTime -= 1;
    if (o.lifeTime < 0) o.live = false;
  }

  // BITMAP_JOINT_THUNDER, SubType 0: rebuilt each move as a jittered path homing in on the target.
  private moveThunder(o: Joint): void {
    for (let j = 0; j < o.maxTails; j++) {
      if (o.target) o.targetPosition.copy(o.target.position).setZ(o.target.position.z + 80);
      const distance = moveHumming(o.position, o.angle, o.targetPosition, 50);
      o.direction.x = (randInt(1024) - 512) / o.scale;
      o.direction.z = (randInt(1024) - 512) / o.scale;
      const angle = o.angle.clone().add(o.direction);
      this.createTail(o, angle);
      if (distance < o.velocity * 1.5) {
        if (o.scale === 50) {
          this.createParticle('energy', o.position, o.angle, o.light);
          if (randInt(8) === 0) this.createParticle('smoke', o.position, o.angle, o.light);
        }
        break;
      }
      o.position.add(rotateByAngle(new THREE.Vector3(0, -o.velocity, 0), angle));
    }
  }

  // BITMAP_JOINT_HEALING, SubTypes 1-3: speeds up towards the target's chest, coloured by SubType.
  private moveHealing(o: Joint): void {
    o.velocity += 4;
    const p = o.target ? o.target.position.clone().setZ(o.target.position.z + 120) : o.targetPosition.clone();
    moveHumming(o.position, o.angle, p, 10);
    const luminosity = (12 - o.lifeTime) * 0.1;
    if (o.subType === 1) o.light.set(luminosity * 0.4, luminosity * 0.6, luminosity);
    else if (o.subType === 2) o.light.set(luminosity * 0.4, luminosity, luminosity * 0.6);
    else o.light.set(luminosity, luminosity * 0.6, luminosity * 0.4);
    if (Math.trunc(o.lifeTime) === 1) this.createSprite('shiny2', p, (randInt(8) + 8) * 0.2, o.light, o.target, randInt(360));
  }

  // MODEL_SPEARSKILL, SubType 4: a light circling the target while its aura buff lasts.
  private moveSpearSkill(o: Joint): boolean {
    const target = o.target;
    if (!target || !ownerLive(target) || !(target.hasAura?.() ?? true)) {
      o.lifeTime = 0;
      o.live = false;
      return false;
    }
    o.lifeTime = 100;
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

  // DescribeJoint: face one (the z corners) and face two (the x corners); thunder scrolls.
  private drawJoint(o: Joint): void {
    const scroll = (Math.floor(this.worldTime) % 1000) * 0.001;
    const thunder = o.type === 'jointThunder';
    for (let j = 0; j < o.numTails; j++) {
      const current = o.tails[j], next = o.tails[j + 1];
      if (!current || !next) continue;
      let l1 = (o.numTails - j) / (o.maxTails - 1);
      let l2 = (o.numTails - (j + 1)) / (o.maxTails - 1);
      if (thunder) {
        l1 = l1 * 2 - scroll;
        l2 = l2 * 2 - scroll;
      }
      this.quads.quad(o.texture, 'glow', [current[2], current[3], next[3], next[2]], [l1, l1, l2, l2], [1, 0, 0, 1], o.light);
      if (thunder) {
        l1 += scroll * 2;
        l2 += scroll * 2;
      }
      this.quads.quad(o.texture, 'glow', [current[0], current[1], next[1], next[0]], [l1, l1, l2, l2], [0, 1, 1, 0], o.light);
    }
  }

  // UpdateDrawnJointEffects: the spear light's flare in the middle of its trail.
  private jointSprites(o: Joint): void {
    if (o.type !== 'spearSkill' || o.numTails <= 0) return;
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
    subType = 0, owner: EffectOwner | null = null, scale = 0): void {
    if (!this.catalogue) {
      const [p, a, l] = [position.clone(), angle.clone(), light.clone()];
      this.waiting.push(() => this.createEffect(type, p, a, l, subType, owner, scale));
      return;
    }
    // The common initialisation.
    const o: Effect = {
      type, subType, position: newVector(position), startPosition: new THREE.Vector3(), angle: newVector(angle),
      direction: new THREE.Vector3(), light: newVector(light), scale: scale <= 0 ? 0.9 : scale,
      lifeTime: 0, gravity: 0, velocity: 0.3, blendMesh: -1, blendMeshLight: 1, blendMeshTexCoordU: 0,
      hiddenMesh: -1, alpha: 1, owner, animationFrame: 0, model: null, live: true,
    };
    if (this.catalogue.has(type)) {
      this.catalogue.apply(type, o, { position, angle, light, scale });
    } else if (!this.initEffect(o)) {
      return;
    }
    this.effects.push(o);
    const file = EFFECT_MODELS[type];
    if (file) void this.attachModel(o, file);
  }

  private initEffect(o: Effect): boolean {
    switch (o.type) {
      case 'fireModel':
        o.blendMesh = 1;
        if (o.subType !== 1) return false; // the else branch: SubType 1 (Fire Ball)
        o.lifeTime = 60;
        o.scale = (randInt(4) + 8) * 0.1;
        o.position.z += 120;
        o.direction.set(0, -50, 0);
        return true;
      case 'magic2':
        o.blendMesh = 0;
        o.lifeTime = 20;
        o.direction.set(0, -60, 0);
        return o.subType === 0 || o.subType === 2;
      case 'magicGround2':
        o.lifeTime = 20;
        return o.subType >= 1 && o.subType <= 3;
      case 'stone1':
      case 'stone2': {
        if (o.subType !== 0) return false;
        const p1 = new THREE.Vector3(0, (randInt(256) + 64) * 0.1, 0);
        o.lifeTime = randInt(16) + 32;
        o.scale = (randInt(4) + 8) * 0.1;
        o.angle.z = randInt(360);
        rotateByAngle(p1, o.angle, o.direction);
        o.gravity = randInt(16) + 8;
        return true;
      }
      case 'energy':
        return false; // created from the catalogue
    }
  }

  private async attachModel(o: Effect, file: string): Promise<void> {
    const loaded = await loadEffectModel(file);
    if (!loaded || !o.live) return;
    const { model } = loaded;
    const parts = buildGeometries(model, computeBoneMatrices(model, 0, 0));
    const materials = await Promise.all(parts.map(async (part) => {
      const blend = o.blendMesh <= -2 || model.meshes[part.meshIndex]?.texture === o.blendMesh;
      const material = await createMaterial(MODEL_TEXTURE_DIRS, part.textureName, { glow: blend });
      if (blend && material.map) {
        // Its own copy of the texture, so the scroll (BlendMeshTexCoordU) is this effect's.
        material.map = material.map.clone();
        material.userData.ownMap = true;
      }
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
    switch (o.type) {
      case 'energy': this.moveEnergy(o); break;
      case 'fireModel': this.moveFire(o, luminosity); break;
      case 'magic2': this.moveMagic2(o, luminosity); break;
      case 'magicGround2': this.createHealing(o); break;
      case 'stone1':
      case 'stone2': this.moveStone(o); break;
    }
    // The shared tail: the model's animation, MoveParticle(o, true), the life time.
    if (o.model) {
      const keys = o.model.skeleton.actions[0]?.numKeys ?? 1;
      o.animationFrame = Math.min(o.animationFrame + o.velocity, Math.max(0, keys - 1));
    }
    o.position.add(rotateByAngle(o.direction, o.angle));
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

  // MODEL_FIRE, SubType 1 (Fire Ball): a trail of flames; two stones fly off at the target.
  private moveFire(o: Effect, luminosity: number): void {
    o.blendMeshLight = 0;
    const light = new THREE.Vector3(luminosity, luminosity * 0.1, 0);
    this.createParticle('fire', o.position, o.angle, light, 5);
    if (this.checkTargetRange(o)) {
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

  // MODEL_STONE1/2 SubType 0: Move_MODEL_ICE_SMALL, bouncing and burning.
  private moveStone(o: Effect): void {
    o.position.add(o.direction);
    o.direction.multiplyScalar(0.9);
    o.position.z += o.gravity;
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
    if (randInt(10) === 0) this.createParticle('fire', o.position, o.angle, o.light, 1 + randInt(3));
  }

  // CheckTargetRange: within 100 (flat) of its live owner the effect ends next tick (LifeTime 1).
  private checkTargetRange(o: Effect): boolean {
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
    model.group.position.copy(o.position);
    angleEuler(o.angle, model.group.rotation);
    model.group.scale.setScalar(o.scale);
    model.pose(0, o.animationFrame);
    for (const part of model.parts) {
      const material = part.mesh.material;
      const meshTexture = model.skeleton.meshes[part.meshIndex]?.texture;
      part.mesh.visible = o.hiddenMesh !== -2 && o.hiddenMesh !== part.meshIndex;
      const blend = o.blendMesh <= -2 || meshTexture === o.blendMesh;
      if (blend) {
        material.color.setRGB(o.light.x, o.light.y, o.light.z).multiplyScalar(o.blendMeshLight);
        if (material.map) material.map.offset.x = o.blendMeshTexCoordU;
      } else {
        material.color.setRGB(o.light.x, o.light.y, o.light.z);
      }
      material.opacity = o.alpha;
    }
  }

  // BITMAP_MAGIC+1 (DescribeEffectShadow): the circle on the ground grows and fades.
  private drawGroundCircle(o: Effect): void {
    let luminosity = 1;
    if (o.lifeTime < 5) luminosity -= (5 - o.lifeTime) * 0.2;
    const scale = (20 - o.lifeTime) * 0.15;
    const light = o.subType === 2 ? new THREE.Vector3(0.4, 1, 0.6)
      : o.subType === 3 ? new THREE.Vector3(1, 0.6, 0.4) : new THREE.Vector3(0.4, 0.6, 1);
    light.multiplyScalar(luminosity);
    this.quads.decal('magicGround2', 'glow', o.position.x, o.position.y, scale, light, -o.angle.z,
      this.options.heightAt ?? null, o.position.z);
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
    for (let i = 0; i < this.joints.length; i++) if (this.joints[i].live) this.moveJoint(this.joints[i]);
    this.joints = this.joints.filter((joint) => joint.live);
    for (const joint of this.joints) this.jointSprites(joint);
    for (let i = 0; i < this.particles.length; i++) this.moveParticle(this.particles[i]);
    this.particles = this.particles.filter((particle) => particle.live);
  }

  private draw(): void {
    this.quads.beginFrame(this.options.camera ?? null);
    for (const effect of this.effects) {
      if (effect.type === 'magicGround2') this.drawGroundCircle(effect);
      this.placeModel(effect);
    }
    for (const joint of this.joints) this.drawJoint(joint);
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
