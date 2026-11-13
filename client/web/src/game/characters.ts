// Characters on the map: the player, other players, NPCs and monsters. Each one is an
// animated model that walks from cell to cell like the client's characters do.

import * as THREE from 'three';
import { TERRAIN_SCALE, TERRAIN_SIZE, TerrainFlag } from '../formats.js';
import { loadModel, buildGeometries, computeBoneMatrices, createMaterial, AnimatedModel } from '../bmd.js';
import { sampleHeight, sampleLight } from '../terrain.js';
import { describeMonster, MonsterAction } from './monsterData.js';
import { itemModel, ItemGroup } from './items.js';
import { addItemGlow } from './itemGlow.js';
import { playerActionSpeed } from './combatLook.js';
import type { Vec3 } from '../formats.js';
import type { GeometryPart } from '../bmd.js';
import type { ModelMonster } from './monsterData.js';
import type { AppearanceItem, EquipmentAppearance, ItemGlowData } from './items.js';

/** A terrain cell. */
export interface Cell {
  x: number;
  y: number;
}

/** What characters need of the map they walk on (see world.js). */
export interface CharacterWorld {
  /** Terrain heights, TERRAIN_SIZE x TERRAIN_SIZE. */
  heights: Float32Array;
  /** Terrain light, r g b per cell. */
  light: Float32Array;
  /** Terrain flags per cell (TerrainFlag). */
  flags: ArrayLike<number>;
}

/** The animation actions a character plays when standing and walking. */
export interface CharacterActions {
  stop: number;
  walk: number;
}

export const PlayerAction = { STOP: 1, WALK: 15 } as const; // PLAYER_STOP_MALE, PLAYER_WALK_MALE
// The speeds of the player model's animations depend on the attack and magic speed; the desktop
// client uses the own character's for every player (SetAttackSpeed), so they're set once here.
const playerSpeeds = { attackSpeed: 0, magicSpeed: 0 };
export function setPlayerAnimationSpeeds(attackSpeed: number, magicSpeed: number): void {
  playerSpeeds.attackSpeed = attackSpeed;
  playerSpeeds.magicSpeed = magicSpeed;
}
const BODY_PARTS = ['Helm', 'Armor', 'Pant', 'Glove', 'Boot'] as const;
export const WALK_CELLS_PER_SECOND = 3.3;
const DEGREES = Math.PI / 180;
const ROTATION_STEP_DEGREES = 45;

export function cellCenter(cell: number): number {
  return (cell + 0.5) * TERRAIN_SCALE;
}

// The angle the client gives an object received with a packet rotation (WSclient.cpp).
export function angleFromRotation(rotation: number): number {
  return (rotation - 1) * ROTATION_STEP_DEGREES * DEGREES;
}

// Each body part shows the worn item's model, or the class's own body without one.
const BODY_PART_SLOTS = { Helm: 'helm', Armor: 'armor', Pant: 'pants', Glove: 'gloves', Boot: 'boots' } as const;
type BodyPart = keyof typeof BODY_PART_SLOTS;

/** Where a body part's model comes from, and the worn item when it's an item's. */
interface BodyPartSource {
  dir: string;
  fileName: string;
  textureDir: string | string[];
  item?: AppearanceItem;
  glow?: ItemGlowData;
}

function bodyPartSource(part: BodyPart, bodySet: number, equipment: EquipmentAppearance | null | undefined): BodyPartSource {
  const item = equipment?.[BODY_PART_SLOTS[part]];
  const model = item ? itemModel(item.group, item.number) : null;
  if (item && model) return { dir: model.dir, fileName: model.fileName, textureDir: model.textureDir, item, glow: model.glow };
  return { dir: 'Player', fileName: `${part}Class${String(bodySet).padStart(2, '0')}.bmd`, textureDir: 'Player' };
}

export async function createPlayerModel(bodySet: number, equipment?: EquipmentAppearance | null): Promise<AnimatedModel> {
  const skeleton = await loadModel('Player', 'player.bmd');
  const pose = computeBoneMatrices(skeleton, PlayerAction.STOP, 0);
  const sources = BODY_PARTS.map((part) => bodyPartSource(part, bodySet, equipment));
  const loaded = await Promise.all(sources.map(async (source): Promise<{ geometries: GeometryPart[]; materials: THREE.MeshBasicMaterial[] }> => {
    try {
      const model = await loadModel(source.dir, source.fileName);
      const geometries = buildGeometries(model, pose);
      const materials = await Promise.all(geometries.map((geometry) => createMaterial(source.textureDir, geometry.textureName)));
      return { geometries, materials };
    } catch (error) {
      console.warn(`Body part ${source.dir}/${source.fileName} missing`, (error as Error).message);
      return { geometries: [], materials: [] };
    }
  }));
  const animated = new AnimatedModel(skeleton, loaded.flatMap((part) => part.geometries), loaded.flatMap((part) => part.materials));
  // Each worn item glows by its own level and options, on its own meshes.
  let first = 0;
  await Promise.all(loaded.map((part, index) => {
    const parts = animated.parts.slice(first, first + part.geometries.length);
    first += part.geometries.length;
    const source = sources[index];
    return source.item ? addItemGlow(animated, parts, source.glow, source.item) : null;
  }));
  return animated;
}

// ---------- weapons and wings ----------

// The player skeleton's bones parts hang from (ZzzCharacter.cpp: Weapon[].LinkBone, RenderCharacterBackItem).
const Bone = { RIGHT_HAND: 33, LEFT_HAND: 42, BACK: 47 } as const;
const WING_KEYS_PER_SECOND = 0.25 * 25;

// AngleMatrix (Core/Math) with a translation: degrees around x, y and z, applied z, y, x.
function angleMatrix([x, y, z]: Vec3, [tx, ty, tz]: Vec3 = [0, 0, 0]): THREE.Matrix4 {
  const matrix = new THREE.Matrix4().makeRotationFromEuler(new THREE.Euler(x * DEGREES, y * DEGREES, z * DEGREES, 'ZYX'));
  return matrix.setPosition(tx, ty, tz);
}

// How a weapon sits on the back in a safe zone (PlaceLinkedPart with Link = true).
function backPlacement(item: AppearanceItem, rightHand: boolean): THREE.Matrix4 {
  let translation: Vec3 = [-20, 5, 40];
  if (item.group === ItemGroup.SHIELD) translation = [-10, 0, 0];
  else if (item.group === ItemGroup.BOW) translation = [-10, 5, 10];
  const matrix = angleMatrix([70, 0, 90], translation);
  if (!rightHand && item.group !== ItemGroup.SHIELD) matrix.multiply(angleMatrix([145, 0, 275], [0, 10, -30]));
  return matrix;
}

export async function createItemModel(item: AppearanceItem): Promise<AnimatedModel | null> {
  const source = itemModel(item.group, item.number);
  if (!source) return null;
  const model = await loadModel(source.dir, source.fileName);
  const parts = buildGeometries(model, computeBoneMatrices(model, 0, 0));
  const materials = await Promise.all(parts.map((part, index) => createMaterial(source.textureDir, part.textureName, { glow: source.isGlowMesh?.(index) })));
  const animated = new AnimatedModel(model, parts, materials);
  await addItemGlow(animated, animated.parts, source.glow, item);
  return animated;
}

/** The equipment slots drawn as models of their own, hanging from a bone. */
export type AttachmentSlot = 'rightHand' | 'leftHand' | 'wings';

export class Attachment {
  model: AnimatedModel;
  item: AppearanceItem;
  slot: AttachmentSlot;
  /** Where a weapon sits on the back; null for wings. */
  backMatrix: THREE.Matrix4 | null;

  constructor({ model, item, slot }: { model: AnimatedModel; item: AppearanceItem; slot: AttachmentSlot }) {
    this.model = model;
    this.item = item;
    this.slot = slot;
    this.model.group.matrixAutoUpdate = false;
    this.backMatrix = slot === 'wings' ? null : backPlacement(item, slot === 'rightHand');
  }

  place(boneMatrices: THREE.Matrix4[], onBack: boolean, time: number): void {
    const matrix = this.model.group.matrix;
    if (this.slot === 'wings') matrix.copy(boneMatrices[Bone.BACK]);
    else if (onBack) matrix.multiplyMatrices(boneMatrices[Bone.BACK], this.backMatrix!); // set for all but wings
    else matrix.copy(boneMatrices[this.slot === 'rightHand' ? Bone.RIGHT_HAND : Bone.LEFT_HAND]);
    this.model.group.matrixWorldNeedsUpdate = true;
    this.model.pose(0, this.slot === 'wings' ? time * WING_KEYS_PER_SECOND : 0);
  }
}

export async function createAttachments(equipment?: EquipmentAppearance | null): Promise<Attachment[]> {
  const slots = (['rightHand', 'leftHand', 'wings'] as const).filter((slot) => equipment?.[slot]);
  const attachments = await Promise.all(slots.map(async (slot) => {
    const item = equipment![slot]!; // the filter keeps the worn slots
    try {
      const model = await createItemModel(item);
      return model ? new Attachment({ model, item, slot }) : null;
    } catch (error) {
      console.warn(`Item ${item.group},${item.number} not drawn`, (error as Error).message);
      return null;
    }
  }));
  return attachments.filter((attachment): attachment is Attachment => Boolean(attachment));
}

async function createMonsterModel(description: ModelMonster): Promise<AnimatedModel> {
  const model = await loadModel(description.dir, description.fileName);
  const parts = buildGeometries(model, computeBoneMatrices(model, 0, 0));
  const materials = await Promise.all(parts.map((part) => createMaterial(description.textureDir, part.textureName)));
  return new AnimatedModel(model, parts, materials);
}

export interface CharacterOptions {
  id: number;
  name: string;
  model: AnimatedModel;
  /** 'player', 'monster', 'npc', … */
  kind: string;
  scale?: number;
  actions: CharacterActions;
  keysPerSecond: (action: number) => number;
}

export class Character {
  id: number;
  name: string;
  kind: string;
  model: AnimatedModel;
  actions: CharacterActions;
  keysPerSecond: (action: number) => number;
  /** Where it stands, in world units. */
  position: THREE.Vector2;
  cell: Cell;
  /** The cells still to walk. */
  path: Cell[];
  angle: number;
  action: number;
  time: number;
  light: THREE.Color;
  attachments: Attachment[];
  weaponsOnBack: boolean;
  holdWeapons: boolean;
  clock: number;
  /** What a player wears (players only). */
  equipment?: EquipmentAppearance;
  /** An animation played once over the standing or walking one (an attack, getting hit, dying). */
  oneShot: { action: number; keysPerSecond: number; hold: boolean } | null = null;
  /** Life left as a share (0-1) from the last hit the server reported; -1 while unknown. */
  healthStatus = -1;
  /** Set once the server reported its death: the death animation holds its last key. */
  dead = false;

  constructor({ id, name, model, kind, scale = 1, actions, keysPerSecond }: CharacterOptions) {
    this.id = id;
    this.name = name;
    this.kind = kind;
    this.model = model;
    this.model.group.scale.setScalar(scale);
    this.actions = actions;
    this.keysPerSecond = keysPerSecond;
    this.position = new THREE.Vector2();
    this.cell = { x: 0, y: 0 };
    this.path = [];
    this.angle = 0;
    this.action = actions.stop;
    this.time = Math.random() * 10;
    this.light = new THREE.Color();
    this.attachments = [];
    this.weaponsOnBack = false;
    this.holdWeapons = false; // the wardrobe can show weapons in the hands in town
    this.clock = 0;
  }

  setAttachments(attachments: Attachment[]): void {
    for (const attachment of this.attachments) this.model.group.remove(attachment.model.group);
    this.attachments = attachments;
    for (const attachment of attachments) this.model.group.add(attachment.model.group);
  }

  get group(): THREE.Group { return this.model.group; }

  placeAt(x: number, y: number, rotation?: number): void {
    this.cell = { x, y };
    this.position.set(cellCenter(x), cellCenter(y));
    this.path = [];
    if (rotation !== undefined) this.angle = angleFromRotation(rotation);
  }

  walk(cells: Cell[]): void {
    this.path = cells.slice();
  }

  // Walks straight to a cell, used for others whose exact steps we don't simulate.
  walkStraightTo(x: number, y: number): void {
    this.path = [{ x, y }];
  }

  get isWalking(): boolean { return this.path.length > 0; }

  // Plays an animation once, then goes back to standing or walking; hold keeps its last key (death).
  playOnce(action: number, keysPerSecond?: number, hold = false): void {
    this.oneShot = { action, keysPerSecond: keysPerSecond ?? this.keysPerSecond(action) ?? 6, hold };
    this.action = action;
    this.time = 0;
  }

  get isPlayingOnce(): boolean { return this.oneShot !== null; }

  // Turns to face a cell (the desktop's CreateAngle2D before an attack or a talk).
  faceTo(cell: Cell): void {
    const dx = cellCenter(cell.x) - this.position.x, dy = cellCenter(cell.y) - this.position.y;
    if (dx || dy) this.angle = Math.atan2(dx, -dy);
  }

  update(dt: number, world: CharacterWorld): void {
    if (!this.dead) this.advance(dt);
    const keys = (action: number) => Math.max(1, this.model.skeleton.actions[action]?.numKeys ?? 1);
    if (this.oneShot) {
      const shot = this.oneShot;
      this.action = shot.action;
      this.time = Math.min(this.time + dt * shot.keysPerSecond, keys(shot.action) - 1);
      if (this.time >= keys(shot.action) - 1 && !shot.hold) { this.oneShot = null; this.time = 0; }
    }
    if (!this.oneShot) {
      const action = this.isWalking ? this.actions.walk : this.actions.stop;
      if (action !== this.action) { this.action = action; this.time = 0; }
      this.time += dt * (this.keysPerSecond(action) ?? 6);
    }
    const { x, y } = this.position;
    this.group.position.set(x, y, sampleHeight(world.heights, x, y));
    this.group.rotation.set(0, 0, this.angle);
    this.model.pose(this.action, this.time);
    this.clock += dt;
    const light = sampleLight(world.light, x, y, this.light);
    this.model.setLight(light, this.clock);
    // Players carry their weapons on the back in a safe zone (AreWeaponsOnBack).
    this.weaponsOnBack = !this.holdWeapons && (world.flags[this.cell.y * TERRAIN_SIZE + this.cell.x] & TerrainFlag.SAFEZONE) !== 0;
    for (const attachment of this.attachments) {
      attachment.place(this.model.matrices, this.weaponsOnBack, this.clock);
      attachment.model.setLight(light, this.clock);
    }
  }

  advance(dt: number): void {
    let budget = WALK_CELLS_PER_SECOND * TERRAIN_SCALE * dt;
    while (budget > 0 && this.path.length) {
      const next = this.path[0];
      const target = new THREE.Vector2(cellCenter(next.x), cellCenter(next.y));
      const delta = target.clone().sub(this.position);
      const distance = delta.length();
      if (distance > 1) this.angle = Math.atan2(delta.x, -delta.y); // CreateAngle (Engine/AI/AngleMath.cpp)
      if (distance <= budget) {
        this.position.copy(target);
        this.cell = { x: next.x, y: next.y };
        this.path.shift();
        budget -= distance;
      } else {
        this.position.add(delta.multiplyScalar(budget / distance));
        budget = 0;
      }
    }
  }
}

export interface PlayerCharacterOptions {
  id: number;
  name: string;
  /** The class's body set (class index + 1). */
  bodySet: number;
  equipment?: EquipmentAppearance | null;
  kind?: string;
}

export async function createPlayerCharacter({ id, name, bodySet, equipment, kind = 'player' }: PlayerCharacterOptions): Promise<Character> {
  const [model, attachments] = await Promise.all([createPlayerModel(bodySet, equipment), createAttachments(equipment)]);
  const character = new Character({
    id, name, kind, model,
    actions: { stop: PlayerAction.STOP, walk: PlayerAction.WALK },
    keysPerSecond: (action: number) => playerActionSpeed(action, { ...playerSpeeds, classIndex: Math.max(0, bodySet - 1) }),
  });
  character.setAttachments(attachments);
  character.equipment = equipment ?? {};
  return character;
}

const DEFAULT_NPC_BODY_SET = 2;

export async function createMonsterCharacter({ id, type }: { id: number; type: number }): Promise<Character> {
  const description = describeMonster(type);
  if (description.usesPlayerModel) {
    const character = await createPlayerCharacter({ id, name: description.name, bodySet: DEFAULT_NPC_BODY_SET, kind: description.kind });
    character.group.scale.setScalar(description.scale);
    return character;
  }
  return new Character({
    id,
    name: description.name,
    kind: description.kind,
    model: await createMonsterModel(description),
    scale: description.scale,
    actions: { stop: MonsterAction.STOP, walk: MonsterAction.WALK },
    keysPerSecond: (action: number) => description.keysPerSecond[action],
  });
}
