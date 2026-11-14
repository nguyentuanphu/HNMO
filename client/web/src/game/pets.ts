// Pets and mounts: what the helper slot (equipment slot 8) and the Dark Lord's Dark Raven show
// next to their owner, as the desktop client draws them:
//   mounts (Uniria, Dinorant, Dark Horse, Fenrir)  MoveMount / CreateMountSub (Engine/AI/GOBoid.cpp)
//   the Guardian Angel flying around               MoveMount, MODEL_HELPER
//   the Imp on the shoulder                         PlanAttachments / ImpOffset (ZzzCharacter.cpp)
//   Demon, Spirit of Guardian, Rudolph, Panda,
//   Unicorn, Skeleton                               PetProcess / PetObject and the PetAction* classes
//                                                   (GameLogic/Pets), with Data/Local/pet.bmd
//   the Dark Raven                                  CSPetDarkSpirit::MovePet (GameLogic/Pets/CSPetSystem.cpp)
// Positions are the desktop's units (x, y on the ground, z up, 100 per cell), angles degrees, and
// the movement steps once per 25 FPS tick like the desktop's.

import * as THREE from 'three';
import { loadModel, buildGeometries, computeBoneMatrices, createMaterial, fetchBytes, AnimatedModel } from '../bmd.js';
import { sampleLight } from '../terrain.js';
import { itemModel } from './items.js';
import { PlayerActions as A } from './combatLook.js';
import { createAngle, createAngle2D, moveHumming, rotateByAngle, turnAngle2 } from '../render/effects/core.js';
import type { EffectSystem } from '../render/effects/index.js';
import type { AppearanceItem, EquipmentAppearance } from './items.js';

const TICK_SECONDS = 1 / 25;
const MAX_TICKS_PER_UPDATE = 8;
const DEGREES = Math.PI / 180;
const HELPER_GROUP = 13;

/** The maps whose mounts fly or float (WD_8TARKAN, WD_10HEAVEN = Icarus) and Devias' snow smoke. */
const MapNumber = { DEVIAS: 2, ATLANS: 7, TARKAN: 8, ICARUS: 10 } as const;

export type CompanionKind =
  | 'uniria' | 'dinorant' | 'darkHorse' | 'fenrirRed' | 'fenrirBlack' | 'fenrirBlue' | 'fenrirGold'
  | 'guardianAngel' | 'imp' | 'darkRaven'
  | 'demon' | 'spiritOfGuardian' | 'rudolph' | 'panda' | 'unicorn' | 'skeleton';

/** The helper items (group 13) by number. */
const Helper = {
  GUARDIAN_ANGEL: 0, IMP: 1, UNIRIA: 2, DINORANT: 3, DARK_HORSE: 4, DARK_RAVEN: 5, FENRIR: 37,
  DEMON: 64, SPIRIT_OF_GUARDIAN: 65, RUDOLPH: 67, PANDA: 80, UNICORN: 106, SKELETON: 123,
} as const;

const PET_OF_NUMBER: Record<number, CompanionKind> = {
  [Helper.GUARDIAN_ANGEL]: 'guardianAngel', [Helper.IMP]: 'imp', [Helper.UNIRIA]: 'uniria', [Helper.DINORANT]: 'dinorant',
  [Helper.DARK_HORSE]: 'darkHorse', [Helper.DEMON]: 'demon', [Helper.SPIRIT_OF_GUARDIAN]: 'spiritOfGuardian',
  [Helper.RUDOLPH]: 'rudolph', [Helper.PANDA]: 'panda', [Helper.UNICORN]: 'unicorn', [Helper.SKELETON]: 'skeleton',
};

const FENRIR_OF_VARIANT: CompanionKind[] = ['fenrirRed', 'fenrirBlack', 'fenrirBlue', 'fenrirGold'];

/** Whether an item is the Dark Raven (a Dark Lord's left hand): drawn flying, not in the hand. */
export function isDarkRaven(item: AppearanceItem | null | undefined): boolean {
  return Boolean(item && item.group === HELPER_GROUP && item.number === Helper.DARK_RAVEN);
}

/**
 * The Fenrir's colour of an own item, from its excellent flags as the desktop reads them
 * (MyInventory.cpp: 0x01 black, 0x02 blue, 0x04 gold): the appearance's helper variant 0-3.
 */
export function helperVariant(group: number, number: number, excellentFlags: number): number {
  if (group !== HELPER_GROUP || number !== Helper.FENRIR) return 0;
  if (excellentFlags === 0x01) return 1;
  if (excellentFlags === 0x02) return 2;
  if (excellentFlags === 0x04) return 3;
  return 0;
}

/** The companions a player's equipment brings (ReadEquipmentExtended's helper switch and the Dark Raven). */
export function companionKinds(equipment: EquipmentAppearance | null | undefined): CompanionKind[] {
  const kinds: CompanionKind[] = [];
  const pet = equipment?.pet;
  if (pet && pet.group === HELPER_GROUP) {
    if (pet.number === Helper.FENRIR) kinds.push(FENRIR_OF_VARIANT[pet.variant ?? 0] ?? 'fenrirRed');
    else if (PET_OF_NUMBER[pet.number]) kinds.push(PET_OF_NUMBER[pet.number]);
  }
  if (isDarkRaven(equipment?.rightHand) || isDarkRaven(equipment?.leftHand)) kinds.push('darkRaven');
  return kinds;
}

// ---------- pet.bmd ----------

/** One pet's line of Data/Local/pet.bmd: its scale, additive mesh and animation speeds. */
interface PetInfo { blendMesh: number; scale: number; speeds: number[] }

const BUX = [0xfc, 0xcf, 0xab];
let petInfos: Promise<Map<number, PetInfo>> | null = null;

/** PetProcess::LoadData: a version, the action array size, a count, then BuxConvert'ed records. */
function loadPetInfos(): Promise<Map<number, PetInfo>> {
  petInfos ??= fetchBytes('/data/Local/pet.bmd').then((bytes) => {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const array = view.getInt32(4, true), count = view.getInt32(8, true);
    const size = 16 + 8 * array;
    const infos = new Map<number, PetInfo>();
    for (let i = 0; i < count && 12 + (i + 1) * size <= bytes.length; i++) {
      const record = new Uint8Array(size);
      for (let j = 0; j < size; j++) record[j] = bytes[12 + i * size + j] ^ BUX[j % 3];
      const r = new DataView(record.buffer);
      const type = r.getInt32(0, true), actions = r.getInt32(12, true);
      if (type < 0 || type > 10000 || actions <= 0 || actions > array) continue;
      const speeds = Array.from({ length: actions }, (_, k) => r.getFloat32(16 + 4 * array + 4 * k, true));
      infos.set(type, { blendMesh: r.getInt32(4, true), scale: r.getFloat32(8, true), speeds });
    }
    return infos;
  }).catch((error: unknown) => {
    console.warn('pet.bmd not read', (error as Error).message);
    return new Map<number, PetInfo>();
  });
  return petInfos;
}

// ---------- models ----------

interface ModelSource { dir: string; fileName: string; textureDir: string[]; blendMesh: number }

const SKILL_MODELS: Partial<Record<CompanionKind, string>> = {
  uniria: 'Rider01.bmd', dinorant: 'Rider02.bmd', darkHorse: 'DarkHorse.bmd',
  fenrirRed: 'fenril_red.bmd', fenrirBlack: 'fenril_black.bmd', fenrirBlue: 'fenril_blue.bmd', fenrirGold: 'fenril_gold.bmd',
  darkRaven: 'darkspirit.bmd',
};

const ITEM_OF_KIND: Partial<Record<CompanionKind, number>> = {
  guardianAngel: Helper.GUARDIAN_ANGEL, imp: Helper.IMP, demon: Helper.DEMON, spiritOfGuardian: Helper.SPIRIT_OF_GUARDIAN,
  rudolph: Helper.RUDOLPH, panda: Helper.PANDA, unicorn: Helper.UNICORN, skeleton: Helper.SKELETON,
};

function modelSource(kind: CompanionKind, petInfo: PetInfo | undefined): ModelSource | null {
  const skill = SKILL_MODELS[kind];
  if (skill) return { dir: 'Skill', fileName: skill, textureDir: ['Skill'], blendMesh: -1 };
  const number = ITEM_OF_KIND[kind];
  const source = number === undefined ? null : itemModel(HELPER_GROUP, number);
  if (!source) return null;
  // The Guardian Angel's second mesh glows (CreateMountSub: BlendMesh = 1); pets take theirs from pet.bmd.
  const blendMesh = kind === 'guardianAngel' ? 1 : petInfo?.blendMesh ?? -1;
  return { dir: source.dir, fileName: source.fileName, textureDir: source.textureDir, blendMesh };
}

async function loadCompanionModel(source: ModelSource): Promise<AnimatedModel> {
  const model = await loadModel(source.dir, source.fileName);
  const parts = buildGeometries(model, computeBoneMatrices(model, 0, 0));
  const materials = await Promise.all(parts.map((part, index) =>
    createMaterial(source.textureDir, part.textureName, { glow: index === source.blendMesh })));
  return new AnimatedModel(model, parts, materials);
}

// ---------- the owner ----------

/** What a companion reads of its owner each frame (a game/characters.ts Character). */
export interface CompanionOwner {
  /** Where it stands on the ground (x, y) and the terrain height under it. */
  readonly position: THREE.Vector2;
  readonly height: number;
  /** Its facing in radians (0 faces -y). */
  readonly angle: number;
  /** Its current player action (PlayerActions). */
  readonly action: number;
  readonly scale: number;
  readonly safeZone: boolean;
  /** The owner model's bone matrices (model space). */
  readonly boneMatrices: THREE.Matrix4[];
  readonly mapNumber: number | null;
  /** Whether it rides a Dark Horse (the raven flies higher, CSPetDarkSpirit::MovePet). */
  readonly ridesDarkHorse: boolean;
}

const tmpMatrix = new THREE.Matrix4();
const tmpEuler = new THREE.Euler();
const tmpQuaternion = new THREE.Quaternion();
const tmpScale = new THREE.Vector3();
const tmpVector = new THREE.Vector3();

/** The owner's model matrix in the map's space. */
function ownerMatrix(owner: CompanionOwner, out: THREE.Matrix4): THREE.Matrix4 {
  tmpQuaternion.setFromAxisAngle(new THREE.Vector3(0, 0, 1), owner.angle);
  return out.compose(tmpVector.set(owner.position.x, owner.position.y, owner.height), tmpQuaternion, tmpScale.setScalar(owner.scale));
}

/** b->TransformPosition(Owner->BoneTransform[bone], offset, out, true): an owner bone's point in the map's space. */
function ownerBonePoint(owner: CompanionOwner, bone: number, offset: THREE.Vector3, out: THREE.Vector3): THREE.Vector3 {
  const matrix = owner.boneMatrices[bone];
  out.copy(offset);
  if (matrix) out.applyMatrix4(matrix);
  return out.applyMatrix4(ownerMatrix(owner, tmpMatrix));
}

function ownerPosition(owner: CompanionOwner, out: THREE.Vector3): THREE.Vector3 {
  return out.set(owner.position.x, owner.position.y, owner.height);
}

function ownerAngleDegrees(owner: CompanionOwner): number {
  return ((owner.angle / DEGREES) % 360 + 360) % 360;
}

function rand(n: number): number {
  return Math.floor(Math.random() * n);
}

// ---------- the effect hook ----------

let effectSystem: EffectSystem | null = null;
/** The map the companions are on (their owners'), for map-dependent effects. */
let effectsMap: number | null = null;
let effectGeneration = 0;
let lastEffectTick = -1;
const liveCompanions = new Set<Companion>();

/** The effect system companions draw their glows with (main.ts, once it exists). */
export function setCompanionEffects(effects: EffectSystem | null): void {
  effectSystem = effects;
  effectGeneration++;
  lastEffectTick = -1;
}

// One task per effect system draws every companion's sprites each tick (they live one tick);
// scheduled again when the system dropped its tasks (a map change clears them).
function keepEffectTask(): void {
  const effects = effectSystem;
  if (!effects || (lastEffectTick >= 0 && effects.time - lastEffectTick <= 200)) return;
  const generation = ++effectGeneration;
  lastEffectTick = effects.time;
  effects.schedule(() => {
    if (generation !== effectGeneration || effects !== effectSystem) return false;
    lastEffectTick = effects.time;
    for (const companion of liveCompanions) if (companion.visible) companion.effects(effects);
    return true;
  });
}

const WHITE = new THREE.Vector3(1, 1, 1);

// ---------- companions ----------

/** A pet or mount next to its owner. */
export abstract class Companion {
  readonly kind: CompanionKind;
  readonly model: AnimatedModel;
  /** Where it is (map space) and its angles in degrees (x pitch, z facing). */
  readonly position = new THREE.Vector3();
  readonly angle = new THREE.Vector3();
  /** o->Direction: its move per tick before turning by its angle. */
  readonly direction = new THREE.Vector3();
  action = 0;
  frame = 0;
  /** o->Velocity: animation keys per tick. */
  velocity = 0.5;
  scale = 1;
  visible = true;
  /** Milliseconds since it was made (the pets' `tick`). */
  time = 0;
  private started = false;
  private accumulator = 0;
  private readonly light = new THREE.Color();

  constructor(kind: CompanionKind, model: AnimatedModel) {
    this.kind = kind;
    this.model = model;
    model.group.matrixAutoUpdate = false;
    model.group.name = `companion:${kind}`;
  }

  get group(): THREE.Group { return this.model.group; }

  /** Starts it next to its owner (Create / CreateMountSub). */
  protected abstract start(owner: CompanionOwner): void;
  /** One 25 FPS step of its movement and animation choice. */
  protected abstract tick(owner: CompanionOwner): void;
  /** Its glows for one tick (sprites live one tick). */
  effects(_effects: EffectSystem): void { /* most have none the effect system draws */ }

  /** PlayAnimation: advances the frame by the velocity; false when the action wrapped. */
  protected playAnimation(speed = this.velocity): boolean {
    const keys = Math.max(1, this.model.skeleton.actions[this.action]?.numKeys ?? 1);
    if (keys <= 1) return true;
    this.frame += speed;
    if (this.frame >= keys) { this.frame %= keys; return false; }
    return true;
  }

  /** SetAction: a new action starts at its first key; actions the model lacks are ignored. */
  protected setAction(action: number): void {
    if (action >= this.model.skeleton.actions.length) return;
    if (action !== this.action) { this.action = action; this.frame = 0; }
  }

  update(dt: number, owner: CompanionOwner, light: Float32Array): void {
    effectsMap = owner.mapNumber;
    if (!this.started) { this.started = true; this.start(owner); }
    this.accumulator += dt;
    let ticks = 0;
    while (this.accumulator >= TICK_SECONDS) {
      this.accumulator -= TICK_SECONDS;
      this.time += TICK_SECONDS * 1000;
      if (ticks++ < MAX_TICKS_PER_UPDATE) this.tick(owner);
    }
    if (ticks > MAX_TICKS_PER_UPDATE) this.catchUp(owner);
    this.place(owner);
    this.group.visible = this.visible;
    if (!this.visible) return;
    this.model.pose(this.action, this.frame);
    this.model.setLight(sampleLight(light, this.position.x, this.position.y, this.light), this.time / 1000);
    keepEffectTask();
  }

  /** After a long pause (a hidden tab) it starts again next to its owner. */
  protected catchUp(owner: CompanionOwner): void { this.start(owner); }

  /** Its model matrix: at its position, turned by its angles, at its scale. */
  protected place(_owner: CompanionOwner): void {
    tmpEuler.set(this.angle.x * DEGREES, this.angle.y * DEGREES, this.angle.z * DEGREES, 'ZYX');
    this.group.matrix.compose(this.position, tmpQuaternion.setFromEuler(tmpEuler), tmpScale.setScalar(this.scale));
    this.group.matrixWorldNeedsUpdate = true;
  }

  /** A point on one of its bones in the map's space. */
  protected bonePoint(bone: number, x: number, y: number, z: number, out = new THREE.Vector3()): THREE.Vector3 {
    out.set(x, y, z);
    const matrix = this.model.matrices[bone];
    if (matrix) out.applyMatrix4(matrix);
    return out.applyMatrix4(this.group.matrix);
  }

  /** Comes into the world (its owner was added to the map). */
  attach(): void { liveCompanions.add(this); }
  /** Leaves it (its owner was removed or changed what it wears). */
  detach(): void { liveCompanions.delete(this); this.group.removeFromParent(); }
}

// --- mounts: MoveMount's Fenrir, Dark Horse, Uniria and Dinorant (Pegasus) ---

class Mount extends Companion {
  protected start(owner: CompanionOwner): void {
    // CreateMountSub's scales, then CullMounts: 1 for all but the Fenrirs in the game.
    this.scale = this.kind.startsWith('fenrir') ? 0.9 : 1;
    this.velocity = 0.5;
    this.follow(owner);
  }

  private follow(owner: CompanionOwner): void {
    ownerPosition(owner, this.position);
    this.angle.set(0, 0, ownerAngleDegrees(owner));
    if (this.kind === 'dinorant') {
      const map = owner.mapNumber;
      this.position.z -= map === MapNumber.TARKAN || map === MapNumber.ICARUS ? 10 : map === null ? 0 : 30;
    }
  }

  protected tick(owner: CompanionOwner): void {
    // Mounts aren't drawn in a safe zone: the rider stands on foot there.
    this.visible = !owner.safeZone;
    if (!this.visible) return;
    this.follow(owner);
    if (this.kind.startsWith('fenrir')) this.fenrirAction(owner.action);
    else if (this.kind === 'darkHorse') this.darkHorseAction(owner.action);
    else this.riderAction(owner);
    this.playAnimation();
  }

  private fenrirAction(a: number): void {
    const FENRIR = { STAND: 0, WALK: 1, RUN: 2, ATTACK: 3, ATTACK_SKILL: 4, DAMAGE: 5 } as const;
    const set = (action: number, velocity: number) => { this.setAction(action); this.velocity = velocity; };
    const alice = a === A.SKILL_CHAIN_LIGHTNING_FENRIR || a === A.SKILL_LIGHTNING_ORB_FENRIR || a === A.SKILL_DRAIN_LIFE_FENRIR;
    if ((a >= A.FENRIR_ATTACK && a <= A.FENRIR_ATTACK_BOW) || alice || a === A.RAGE_FENRIR_ATTACK_RIGHT) set(FENRIR.ATTACK, 0.4);
    else if (a >= A.FENRIR_SKILL && a <= A.FENRIR_SKILL_ONE_LEFT) set(FENRIR.ATTACK_SKILL, 0.4);
    else if (a >= A.FENRIR_DAMAGE && a <= A.FENRIR_DAMAGE_ONE_LEFT) set(FENRIR.DAMAGE, 0.4);
    else if (a >= A.FENRIR_STAND && a <= A.FENRIR_STAND_ONE_LEFT) set(FENRIR.STAND, 0.4);
    else if (a === A.DIE1) set(FENRIR.STAND, 0.4);
    else if (a >= A.RAGE_FENRIR_DAMAGE && a <= A.RAGE_FENRIR_DAMAGE_ONE_LEFT) set(FENRIR.DAMAGE, 0.4);
    else if (a >= A.RAGE_FENRIR && a <= A.RAGE_FENRIR_ONE_LEFT) set(FENRIR.ATTACK_SKILL, 0.4);
    else if (a >= A.RAGE_FENRIR_STAND && a <= A.RAGE_FENRIR_STAND_ONE_LEFT) set(FENRIR.STAND, 0.4);
    else if (a >= A.SKILL_THRUST && a <= A.SKILL_HP_UP_OURFORCES) set(FENRIR.STAND, 0.4);
    else if (a >= A.FENRIR_WALK && a <= A.FENRIR_WALK_ONE_LEFT) set(FENRIR.WALK, 1);
    else if (a >= A.FENRIR_RUN && a <= A.FENRIR_RUN_ONE_LEFT_ELF) set(FENRIR.RUN, 0.6);
    else if (a >= A.RAGE_FENRIR_RUN && a <= A.RAGE_FENRIR_RUN_ONE_LEFT) set(FENRIR.RUN, 0.6);
    else if (a >= A.RAGE_FENRIR_WALK && a <= A.RAGE_FENRIR_WALK_TWO_SWORD) set(FENRIR.WALK, 1);
  }

  private darkHorseAction(a: number): void {
    if (a === A.ATTACK_DARKHORSE) { this.setAction(3); this.velocity = 0.34; }
    else if (a === A.RUN_RIDE_HORSE) { this.setAction(1); this.velocity = 0.34; }
    else if (a >= A.ATTACK_RIDE_STRIKE && a <= A.ATTACK_RIDE_ATTACK_MAGIC) { this.setAction(2); this.velocity = 0.34; }
    else if (a === A.IDLE1_DARKHORSE) { this.setAction(5); this.velocity = 1; }
    else if (a === A.IDLE2_DARKHORSE) { this.setAction(6); this.velocity = 1; }
    else { this.setAction(0); this.velocity = 0.3; }
  }

  // Uniria (MODEL_UNICON) and Dinorant (MODEL_PEGASUS), which flies over Tarkan and Icarus.
  private riderAction(owner: CompanionOwner): void {
    const a = owner.action;
    const flying = this.kind === 'dinorant' && (owner.mapNumber === MapNumber.TARKAN || owner.mapNumber === MapNumber.ICARUS);
    const fly = flying ? 1 : 0;
    const moving = (a >= A.WALK_MALE && a <= A.RUN_RIDE_WEAPON) || a === A.FLY_RIDE || a === A.FLY_RIDE_WEAPON
      || a === A.RAGE_UNI_RUN || a === A.RAGE_UNI_RUN_ONE_RIGHT;
    const alice = a === A.SKILL_CHAIN_LIGHTNING_UNI || a === A.SKILL_CHAIN_LIGHTNING_DINO || a === A.SKILL_LIGHTNING_ORB_UNI
      || a === A.SKILL_LIGHTNING_ORB_DINO || a === A.SKILL_DRAIN_LIFE_UNI || a === A.SKILL_DRAIN_LIFE_DINO;
    if (moving) this.setAction(this.kind === 'dinorant' ? 2 + fly : 2);
    else if (a === A.SKILL_RIDER || a === A.SKILL_RIDER_FLY) this.setAction(6 + fly);
    else if ((a >= A.ATTACK_FIST && a <= A.ATTACK_RIDE_CROSSBOW) || alice) this.setAction(this.kind === 'dinorant' ? 4 + fly : 3);
    else this.setAction(this.kind === 'dinorant' ? fly : 0);
    this.velocity = 0.34;
  }

  override effects(effects: EffectSystem): void {
    const map = effectsMap;
    if (this.kind === 'darkHorse' && rand(3) === 0) {
      // Breath at the nose (bone 27); bubbles in Atlans are not drawn yet.
      if (map !== MapNumber.ATLANS) effects.createParticle('smoke', this.bonePoint(27, 50, -4, 0), this.angle, WHITE);
    }
    // Dust at the feet while running: Devias' snow (BITMAP_SMOKE); the other maps' dust is BITMAP_SMOKE+1.
    const running = (this.kind === 'darkHorse' && this.action === 1) || (this.kind.startsWith('fenrir') && (this.action === 1 || this.action === 2))
      || ((this.kind === 'uniria' || this.kind === 'dinorant') && (this.action === 2 || this.action === 3));
    if (running && map === MapNumber.DEVIAS && rand(this.kind.startsWith('fenrir') ? 3 : 2) === 0) {
      const p = new THREE.Vector3(this.position.x + rand(64) - 32, this.position.y + rand(64) - 32, this.position.z + rand(32) - 16);
      effects.createParticle('smoke', p, this.angle, WHITE);
    }
    if (this.kind.startsWith('fenrir')) {
      // The eyes and the mouth (RenderObject_AfterImage's Fenrir glow, ZzzObject.cpp).
      const l = Math.sin(effects.time * 0.002) * 0.2;
      const eyes = new THREE.Vector3(0.9 + l, 0.2 + l * 0.5, 0.1 + l * 0.5);
      for (const side of [11, -11]) {
        const p = this.bonePoint(11, 50, 2, side);
        effects.createSprite('light', p, 0.5 + l * 0.1, eyes);
        effects.createSprite('light', p, 0.5 + l * 0.1, eyes);
      }
      const mouth = this.bonePoint(13, 40, 15, 0);
      const red = new THREE.Vector3(1, 0.3, 0.2);
      effects.createSprite('light', mouth, 1.5, red);
      effects.createSprite('light', mouth, 1, red);
    }
  }
}


// --- the Imp: drawn on the owner's shoulder bone (34), 20 units out, playing at half speed ---

class Imp extends Companion {
  private readonly offset = new THREE.Matrix4().makeTranslation(20, 0, 0);
  protected start(): void { this.velocity = 0.5; }
  protected tick(): void { this.playAnimation(); }
  protected override place(owner: CompanionOwner): void {
    const bone = owner.boneMatrices[34];
    ownerMatrix(owner, this.group.matrix);
    if (bone) this.group.matrix.multiply(bone);
    this.group.matrix.multiply(this.offset);
    this.group.matrixWorldNeedsUpdate = true;
    this.position.setFromMatrixPosition(this.group.matrix);
  }
}

// --- the Guardian Angel: MoveMount's MODEL_HELPER, flying around its owner ---

class GuardianAngel extends Companion {
  protected start(owner: CompanionOwner): void {
    this.scale = 1;
    this.velocity = 0.5;
    ownerPosition(owner, this.position);
    this.position.x += rand(512) - 256;
    this.position.y += rand(512) - 256;
    this.position.z += rand(128) + 128;
    this.angle.set(0, 0, ownerAngleDegrees(owner));
    this.direction.set(0, 0, 0);
  }

  protected tick(owner: CompanionOwner): void {
    this.playAnimation();
    const flyRange = 150;
    const target = ownerPosition(owner, new THREE.Vector3());
    const distance = (target.x - this.position.x) ** 2 + (target.y - this.position.y) ** 2;
    if (distance >= flyRange * flyRange) {
      this.angle.z = turnAngle2(this.angle.z, createAngle(this.position.x, this.position.y, target.x, target.y), 20);
    }
    this.position.add(rotateByAngle(this.direction, this.angle));
    this.position.z += rand(16) - 8;
    if (rand(32) === 0) {
      let speed: number;
      if (distance >= flyRange * flyRange) speed = -(rand(64) + 128) * 0.1;
      else { speed = -(rand(64) + 16) * 0.1; this.angle.z = rand(360); }
      this.direction.set(0, speed, (rand(64) - 32) * 0.1);
    }
    if (this.position.z < target.z + 100) this.direction.z += 1.5;
    if (this.position.z > target.z + 200) this.direction.z -= 1.5;
    // Far behind (a warp): back next to the owner.
    if (distance > 2000 * 2000) this.start(owner);
  }
}

// --- the PetProcess pets (pet.bmd): Demon, Spirit of Guardian, Rudolph, Panda, Unicorn, Skeleton ---

const CIRCLE_STAND_RADIUS = 50;
const SEARCH_LENGTH = 300;

class Pet extends Companion {
  private readonly info: PetInfo | undefined;
  private baseVelocity = 0.5;

  constructor(kind: CompanionKind, model: AnimatedModel, info: PetInfo | undefined) {
    super(kind, model);
    this.info = info;
  }

  protected start(owner: CompanionOwner): void {
    // PetObject::Create, then the pet.bmd scale and the stand action's speed.
    this.scale = this.info?.scale || 0.7;
    this.baseVelocity = this.info?.speeds[0] ?? 0.5;
    this.velocity = this.baseVelocity;
    ownerPosition(owner, this.position);
    if (this.kind === 'skeleton') this.position.y += 60 * owner.scale;
    this.angle.set(0, 0, ownerAngleDegrees(owner));
    this.direction.set(0, 0, 0);
  }

  protected tick(owner: CompanionOwner): void {
    this.playAnimation();
    const target = ownerPosition(owner, new THREE.Vector3());
    if (this.kind === 'demon' || this.kind === 'spiritOfGuardian') this.hover(owner, target);
    else this.collect(owner, target);
  }

  // PetActionStand (Spirit of Guardian) and PetActionDemon: hovering over the owner's head.
  private hover(owner: CompanionOwner, target: THREE.Vector3): void {
    const flyRange = 50;
    this.position.z = target.z + 230 * owner.scale;
    const distance = (target.x - this.position.x) ** 2 + (target.y - this.position.y) ** 2;
    if (distance >= flyRange ** 4) {
      this.position.x = target.x;
      this.position.y = target.y;
      if (this.kind === 'spiritOfGuardian') this.position.z = target.z + 200;
      this.angle.z = ownerAngleDegrees(owner);
    } else if (distance >= flyRange * flyRange) {
      this.angle.z = turnAngle2(this.angle.z, createAngle2D(this.position, target), 10);
    }
    this.position.add(rotateByAngle(this.direction, this.angle));
    const near = flyRange * flyRange >= distance;
    const speed = near ? 0 : this.kind === 'demon' ? Math.log(distance) + 5 : Math.log(distance) * 1.8;
    this.direction.set(0, -speed, 0);
  }

  // PetActionCollecter (Rudolph), PetActionCollecterAdd (Panda), PetActionUnicorn and
  // PetActionCollecterSkeleton, standing by: circling the owner or following it.
  private collect(owner: CompanionOwner, target: THREE.Vector3): void {
    const circle = (2 * Math.PI / 4000) * (this.time % 4000);
    const height = this.kind === 'unicorn' ? 200 * owner.scale : this.kind === 'skeleton' ? 50 * owner.scale : 20;
    const flyRange = this.kind === 'skeleton' ? 12 : 10;
    this.position.z = target.z + height;
    let distance = Math.hypot(this.position.x - target.x, this.position.y - target.y);
    if (distance > SEARCH_LENGTH * 3) {
      this.position.x = target.x + Math.sin(circle) * CIRCLE_STAND_RADIUS;
      this.position.y = target.y + Math.cos(circle) * CIRCLE_STAND_RADIUS;
      this.angle.z = ownerAngleDegrees(owner);
    }
    const circles = this.kind === 'rudolph' || this.kind === 'panda';
    const goal = circles
      ? new THREE.Vector3(target.x + Math.sin(circle) * CIRCLE_STAND_RADIUS, target.y + Math.cos(circle) * CIRCLE_STAND_RADIUS, target.z)
      : target;
    distance = Math.hypot(goal.x - this.position.x, goal.y - this.position.y);
    this.angle.z = turnAngle2(this.angle.z, createAngle2D(this.position, goal), 8);
    this.position.add(rotateByAngle(this.direction, this.angle));
    // Rudolph and the Panda stop within the fly range, the Unicorn and the Skeleton within its square.
    const stopAt = circles ? flyRange : flyRange * flyRange;
    const speed = stopAt >= distance ? 0 : Math.log(distance) * 2.3;
    this.direction.set(0, -speed, 0);
    if (this.kind === 'unicorn') this.velocity = this.baseVelocity * (speed === 0 ? 0.35 : 1.2);
  }

  override effects(effects: EffectSystem): void {
    const pulse = Math.sin((Math.PI / 3000) * (this.time % 3000)) * 0.2;
    if (this.kind === 'panda') {
      const p = this.bonePoint(4, 0, 0, 0);
      effects.createSprite('light', p, 1.5 + pulse, new THREE.Vector3(0.9, 0.9, 0));
      effects.createSprite('light', p, 2.5 + pulse, new THREE.Vector3(0.6, 1, 0.2));
    } else if (this.kind === 'rudolph') {
      effects.createSprite('light', this.bonePoint(10, 0, 0, 0), 2 + pulse, new THREE.Vector3(1, 0.1, 0.2));
      for (const bone of [19, 32, 33, 34, 35]) {
        const p = this.bonePoint(bone, 0, 0, 0);
        effects.createSprite('light', p, 0.6, new THREE.Vector3(0.8, 0.6, 0.2));
        effects.createSprite('shiny2', p, 0.4, new THREE.Vector3(0.8, 0.8, 0.2));
      }
    } else if (this.kind === 'unicorn') {
      effects.createSprite('light', this.bonePoint(4, 0, 0, 0), 4, new THREE.Vector3(0.5, 0.5, 1));
    } else if (this.kind === 'skeleton') {
      const lumi = (Math.sin(effects.time * 0.003) + 1) * 0.5 + 0.5;
      const light = new THREE.Vector3(0, lumi, 0.5 * lumi);
      for (const [bone, size] of [[44, 0.3], [46, 0.6], [45, 0.4], [62, 0.3]] as const) {
        effects.createSprite('light', this.bonePoint(bone, 0, 0, 0), size, light);
      }
    } else if (this.kind === 'spiritOfGuardian') {
      for (const bone of [25, 26, 27]) effects.createSprite('light', this.bonePoint(bone, 0, 0, 0), 0.2, new THREE.Vector3(0.6, 0.2, 0.8));
    }
  }
}

// --- the Dark Raven: CSPetDarkSpirit, flying around and sitting on the shoulder in a safe zone ---

const PetAi = { FLYING: 0, FLY: 1, STAND: 3, STAND_START: 4 } as const;
type PetAiState = typeof PetAi[keyof typeof PetAi];

class DarkRaven extends Companion {
  ai: PetAiState = PetAi.FLYING;
  private moveVelocity = 1;
  private lifeTime = 0;

  protected start(owner: CompanionOwner): void {
    this.scale = 0.7;
    ownerPosition(owner, this.position);
    this.position.z += 300;
    this.angle.set(0, 0, 0);
    this.direction.set(0, 0, 0);
    this.ai = PetAi.FLYING;
    this.action = 0;
  }

  private setAi(ai: PetAiState): void {
    this.ai = ai;
    this.lifeTime = 0;
  }

  protected tick(owner: CompanionOwner): void {
    const target = ownerPosition(owner, new THREE.Vector3());
    if (owner.safeZone) {
      if (this.ai !== PetAi.STAND && this.ai !== PetAi.STAND_START) {
        // SetAI(PET_STAND), then always on to PET_STAND_START: it flies to the shoulder first.
        this.setAi(PetAi.STAND_START);
        this.moveVelocity = 3;
      }
    } else if (this.ai === PetAi.STAND || this.ai === PetAi.STAND_START) {
      this.setAi(PetAi.FLYING);
    }

    if (!this.playAnimation(0.4)) {
      if (this.ai === PetAi.FLY) this.setAction(0);
      else if (this.ai === PetAi.FLYING) this.setAction(1);
      else if (this.ai === PetAi.STAND) this.setAction(2);
      else this.setAction(1);
    }

    const range = new THREE.Vector3().subVectors(target, this.position);
    const flyRange = 150;
    if (this.ai === PetAi.FLY || this.ai === PetAi.FLYING) {
      const distance = range.x * range.x + range.y * range.y;
      if (distance >= flyRange * flyRange) {
        this.angle.z = turnAngle2(this.angle.z, createAngle2D(this.position, target), 5 + Math.random() * 14);
      }
      this.position.add(rotateByAngle(this.direction, this.angle));
      let speedRandom = 28;
      const height = target.z + (owner.ridesDarkHorse ? 350 : 250);
      if (this.position.z < height) {
        speedRandom = 10;
        this.angle.x = Math.max(-15, this.angle.x - 2);
      } else if (this.position.z > height + 100) {
        speedRandom = 20;
        this.angle.x = Math.min(15, this.angle.x + 2);
      }
      if (rand(speedRandom) === 0) {
        let speed: number;
        if (distance >= flyRange * flyRange) speed = -(Math.random() * 63 + 128) * 0.1;
        else { speed = -(Math.random() * 7 + 32) * 0.1; this.angle.z += Math.random() * 59; }
        speed = (speed + this.direction.y) / 2;
        this.direction.set(0, speed, (Math.random() * 63 - 32) * 0.1);
      }
      if (this.direction.y < -12) { if (this.ai !== PetAi.FLYING) this.setAi(PetAi.FLYING); }
      else if (this.ai !== PetAi.FLY) this.setAi(PetAi.FLY);
    } else if (this.ai === PetAi.STAND) {
      ownerBonePoint(owner, 37, new THREE.Vector3(-10, 0, 10), this.position);
      this.angle.set(0, 0, ownerAngleDegrees(owner) - 120);
    } else if (this.ai === PetAi.STAND_START) {
      if (this.moveVelocity !== 0) this.position.add(rotateByAngle(new THREE.Vector3(0, -this.moveVelocity, 0), this.angle));
      const shoulder = ownerBonePoint(owner, 42, new THREE.Vector3(), new THREE.Vector3());
      const distance = moveHumming(this.position, this.angle, shoulder, this.moveVelocity);
      this.moveVelocity += 1;
      if (distance < 50) this.setAi(PetAi.STAND);
    }

    // Lost far below or away: back over the owner.
    const away = range.x * range.x + range.y * range.y;
    if (this.position.z < target.z - 200 || away > 409600) this.lifeTime += 1;
    if (this.lifeTime > 90) {
      this.lifeTime = 0;
      this.position.copy(target);
      this.position.z += 250;
    }
  }
}

// ---------- making them ----------

/** Makes a companion of a kind (loads its model); null when its model is missing. */
export async function createCompanion(kind: CompanionKind): Promise<Companion | null> {
  const number = ITEM_OF_KIND[kind];
  const petInfo = number !== undefined && number >= Helper.DEMON ? (await loadPetInfos()).get(number) : undefined;
  const source = modelSource(kind, petInfo);
  if (!source) return null;
  try {
    const model = await loadCompanionModel(source);
    if (kind === 'imp') return new Imp(kind, model);
    if (kind === 'guardianAngel') return new GuardianAngel(kind, model);
    if (kind === 'darkRaven') return new DarkRaven(kind, model);
    if (number !== undefined && number >= Helper.DEMON) return new Pet(kind, model, petInfo);
    return new Mount(kind, model);
  } catch (error) {
    console.warn(`Companion ${kind} not drawn`, (error as Error).message);
    return null;
  }
}

/** The companions of a player's equipment. */
export async function createCompanions(equipment: EquipmentAppearance | null | undefined): Promise<Companion[]> {
  const companions = await Promise.all(companionKinds(equipment).map((kind) => createCompanion(kind)));
  return companions.filter((companion): companion is Companion => companion !== null);
}
