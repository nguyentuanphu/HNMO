// Characters on the map: the player, other players, NPCs and monsters. Each one is an
// animated model that walks from cell to cell like the client's characters do.

import * as THREE from 'three';
import { TERRAIN_SCALE, TERRAIN_SIZE, TerrainFlag } from '../formats.js';
import { loadModel, buildGeometries, computeBoneMatrices, createMaterial, isHiddenTexture, sharedGeometries, AnimatedModel } from '../bmd.js';
import { meshDraws, type ItemPlace, type MeshDraw } from './itemLooks.js';
import { sampleHeight, sampleModelLight } from '../terrain.js';
import { BODY_PART_SLOTS as BODY_PART_SLOT_NAMES, describeMonster, isSkeletonSkin, MonsterAction } from './monsterData.js';
import { itemDefinition, itemModel, partModel, ItemGroup } from './items.js';
import { addItemGlow, reflectionMaterial, setCloaked, setItemGlowState, updateReflections } from './itemGlow.js';
import { performanceFlags } from '../render/quality.js';
import { createCompanions, isDarkRaven } from './pets.js';
import type { Companion, CompanionOwner } from './pets.js';
import { playerActionSpeed, playerStopAction, playerWalkAction, type MonsterAttacksData, type Mount } from './combatLook.js';
import type { BMDModel, Vec3 } from '../formats.js';
import type { GeometryPart, ModelPart } from '../bmd.js';
import type { DescribeOptions, MonsterLook, MonsterSetupData } from './monsterData.js';
import type { AppearanceItem, EquipmentAppearance, EquipmentSlotName, ItemGlowData, ItemModelSource } from './items.js';

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
  /** The map's number (pets and mounts look different on some maps). */
  mapNumber?: number;
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
const PLAYER_SKELETON = { dir: 'Player', fileName: 'player.bmd' } as const;
const NO_BODY: BMDModel = { name: 'none', meshes: [], actions: [], bones: [] };
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
export interface BodyPartSource {
  dir: string;
  fileName: string;
  textureDir: string | string[];
  item?: AppearanceItem;
  glow?: ItemGlowData;
  /** A texture name that replaces the file's, by mesh (Models.json textureNames). */
  textureNames?: Record<number, string>;
}

/** An item's model as a body part, or null when the item list has no model for it. */
export function itemBodySource(item: AppearanceItem | null | undefined): BodyPartSource | null {
  const model = item ? itemModel(item.group, item.number) : null;
  return item && model ? { dir: model.dir, fileName: model.fileName, textureDir: model.textureDir, item, glow: model.glow } : null;
}

function bodyPartSource(part: BodyPart, bodySet: number, equipment: EquipmentAppearance | null | undefined): BodyPartSource {
  return itemBodySource(equipment?.[BODY_PART_SLOTS[part]])
    ?? { dir: 'Player', fileName: `${part}Class${String(bodySet).padStart(2, '0')}.bmd`, textureDir: 'Player' };
}

/**
 * A model made of body parts that follow one skeleton (the player's Player.bmd, or a monster's own model):
 * each part's meshes posed by the skeleton's bones, each worn item glowing by its own level and options.
 * `skeletonParts` draws the skeleton file's own meshes too (a monster model with parts, the merchants).
 */
export async function createBodyModel(skeletonFile: { dir: string; fileName: string }, sources: BodyPartSource[], startAction = 0,
  skeletonParts?: BodyPartSource): Promise<AnimatedModel> {
  const skeleton = await loadModel(skeletonFile.dir, skeletonFile.fileName);
  const all = skeletonParts ? [skeletonParts, ...sources] : sources;
  const loaded = await Promise.all(all.map(async (source): Promise<{ geometries: GeometryPart[]; materials: THREE.MeshBasicMaterial[] }> => {
    try {
      const model = source === skeletonParts ? skeleton : await loadModel(source.dir, source.fileName);
      const geometries = sharedGeometries(model);
      const materials = await Promise.all(geometries.map((geometry) =>
        createMaterial(source.textureDir, source.textureNames?.[geometry.meshIndex] ?? geometry.textureName)));
      return { geometries, materials };
    } catch (error) {
      if (source === skeletonParts) throw error; // the body itself is missing
      console.warn(`Body part ${source.dir}/${source.fileName} missing`, (error as Error).message);
      return { geometries: [], materials: [] };
    }
  }));
  const animated = new AnimatedModel(skeleton, loaded.flatMap((part) => part.geometries), loaded.flatMap((part) => part.materials), { skinned: true });
  animated.pose(startAction, 0);
  // Each worn item glows by its own level and options, on its own meshes.
  let first = 0;
  await Promise.all(loaded.map((part, index) => {
    const parts = animated.parts.slice(first, first + part.geometries.length);
    first += part.geometries.length;
    const source = all[index];
    return source.item ? addItemGlow(animated, parts, source.glow, source.item) : null;
  }));
  return animated;
}

export async function createPlayerModel(bodySet: number, equipment?: EquipmentAppearance | null): Promise<AnimatedModel> {
  return createBodyModel(PLAYER_SKELETON, BODY_PARTS.map((part) => bodyPartSource(part, bodySet, equipment)), PlayerAction.STOP);
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

/** A texture a look names ("Item/Deye_R.jpg" under Data): its folder and file. */
function lookTexture(path: string): { dir: string; name: string } {
  const slash = path.lastIndexOf('/');
  return { dir: path.slice(0, slash), name: path.slice(slash + 1) };
}

// A mesh as one drawing of its look draws it (game/itemLooks.ts).
async function drawMaterial(textureDir: string[], textureName: string, draw: MeshDraw): Promise<THREE.MeshBasicMaterial> {
  const texture = draw.texture ? lookTexture(draw.texture) : null;
  const material = await createMaterial(texture ? [texture.dir] : textureDir, texture ? texture.name : textureName, { glow: draw.additive });
  // An additive drawing keeps its light as its colour (setLight leaves additive materials alone).
  if (draw.additive) material.color.setRGB(...draw.light);
  return material;
}

/**
 * An item's model as its look draws it: each mesh with its first drawing, further drawings of a mesh as
 * extra meshes over it, meshes the look doesn't draw hidden; then its level / excellent / ancient glow.
 * `place`: worn by a character, in a window, or on the ground (some looks differ).
 */
export async function createItemModel(item: AppearanceItem, place: ItemPlace = 'worn'): Promise<AnimatedModel | null> {
  const source = itemModel(item.group, item.number);
  return source ? createSourceModel(source, item, place) : null;
}

/** A model of the catalog (an item's or a part model's) drawn as its look says, with the item's glow. */
async function createSourceModel(source: ItemModelSource, item: AppearanceItem, place: ItemPlace): Promise<AnimatedModel> {
  const model = await loadModel(source.dir, source.fileName);
  // Worn by a character it shares its model's geometry, skinned on the GPU; a window or the ground poses its own.
  const skinned = place === 'worn';
  const parts = skinned ? sharedGeometries(model) : buildGeometries(model, computeBoneMatrices(model, 0, 0));
  const draws = meshDraws({ ...source.look, level: item.glowLevel ?? 0 }, {
    meshTextures: model.meshes.map((mesh) => mesh.texture),
    hiddenByTexture: (mesh) => isHiddenTexture(model.meshes[mesh]?.textureName ?? ''),
  }, place);
  const hidden = () => new THREE.MeshBasicMaterial({ visible: false });
  // In a window and on the ground the item is drawn without the skin and hair it has on a character
  // (BMD HideSkin; MarkSkinAndHair: textures named "ski…", "level…" and "hair…").
  if (place !== 'worn') {
    for (const part of parts) if (/^(ski|hair)/.test(part.textureName) || /^level/i.test(part.textureName)) draws[part.meshIndex] = [];
  }
  // A mesh whose first drawing is a reflection is drawn only by its passes.
  const base = (part: { meshIndex: number }): MeshDraw | undefined => {
    const first = draws[part.meshIndex]?.[0];
    return first && !first.reflection ? first : undefined;
  };
  const materials = await Promise.all(parts.map((part) => {
    const first = base(part);
    return first ? drawMaterial(source.textureDir, part.textureName, first) : Promise.resolve(hidden());
  }));
  const animated = new AnimatedModel(model, parts, materials, { skinned });
  /** The drawings whose light or scroll moves with the clock, and what they move. */
  const moving: { material: THREE.MeshBasicMaterial; draw: MeshDraw; part: ModelPart | null }[] = [];
  const keepMoving = (material: THREE.MeshBasicMaterial, draw: MeshDraw, part: ModelPart | null): void => {
    if (!draw.lightAt && !draw.scrollAt) return;
    if (draw.scrollAt && material.map) {
      // Its own texture object for its scroll, sharing the image and the GPU texture (a version, not an upload).
      const map = material.map.clone();
      map.wrapS = map.wrapT = THREE.RepeatWrapping;
      map.version = Math.max(1, material.map.version);
      material.map = map;
    }
    moving.push({ material, draw, part });
  };
  for (const part of animated.parts) {
    const first = base(part);
    if (first && !first.additive && first.light.some((channel) => channel !== 1)) part.tint = new THREE.Color(...first.light);
    if (first) keepMoving(part.mesh.material, first, first.additive ? null : part);
  }
  const extra: THREE.Mesh[] = [];
  for (const part of animated.parts) {
    const all = draws[part.meshIndex] ?? [];
    for (const draw of all.slice(base(part) ? 1 : 0)) {
      if (draw.reflection) {
        const material = await reflectionMaterial(draw.reflection.motion, draw.reflection.texture, draw.light, place === 'inventory');
        if (material) extra.push(animated.overlay(part.geometry, material));
        continue;
      }
      const material = await drawMaterial(source.textureDir, part.textureName, draw);
      if (!draw.additive) material.color.setRGB(...draw.light);
      keepMoving(material, draw, null);
      extra.push(animated.overlay(part.geometry, material));
    }
  }
  if (extra.length || moving.length) {
    animated.addGlow({
      meshes: extra,
      update: () => {
        updateReflections();
        if (!moving.length) return;
        const ms = performance.now();
        for (const { material, draw, part } of moving) {
          if (draw.lightAt) {
            const light = draw.lightAt(ms);
            if (part?.tint) part.tint.setRGB(light[0], light[1], light[2]);
            else if (!part) material.color.setRGB(light[0], light[1], light[2]);
          }
          if (draw.scrollAt && material.map) {
            const [u, v] = draw.scrollAt(ms);
            material.map.offset.set(u, v);
          }
        }
      },
    });
  }
  await addItemGlow(animated, animated.parts, source.glow, item, place);
  return animated;
}

/** The equipment slots drawn as models of their own, hanging from a bone. */
export type AttachmentSlot = 'rightHand' | 'leftHand' | 'wings' | 'ornament';

export class Attachment {
  model: AnimatedModel;
  item: AppearanceItem;
  slot: AttachmentSlot;
  /** Where a weapon sits on the back; null for wings. */
  backMatrix: THREE.Matrix4 | null;

  /** The bone a held weapon hangs from (a monster model's linkBones); the player's hands without one. */
  bone: number;
  /** Arrows and bolts: never in a hand, always on the back (DrawsHeldWeapon, DrawsWeaponOnBack). */
  readonly ammunition: boolean;
  /** Whether it plays an animation (wings flap); a still part is posed once. */
  private readonly animated: boolean;
  private posed = false;

  constructor({ model, item, slot, bone, placement }: { model: AnimatedModel; item: AppearanceItem; slot: AttachmentSlot; bone?: number; placement?: THREE.Matrix4 }) {
    this.model = model;
    this.item = item;
    this.slot = slot;
    this.bone = bone ?? (slot === 'leftHand' ? Bone.LEFT_HAND : Bone.RIGHT_HAND);
    this.animated = slot === 'wings' && (model.skeleton.actions[0]?.numKeys ?? 1) > 1;
    this.ammunition = slot !== 'wings' && slot !== 'ornament' && (itemDefinition(item.group, item.number)?.tags?.includes('ammunition') ?? false);
    this.model.group.matrixAutoUpdate = false;
    this.backMatrix = slot === 'wings' ? null : slot === 'ornament' ? placement ?? new THREE.Matrix4() : backPlacement(item, slot === 'rightHand');
  }

  place(boneMatrices: THREE.Matrix4[], onBack: boolean, time: number): void {
    const matrix = this.model.group.matrix;
    if (this.slot === 'wings') matrix.copy(boneMatrices[Bone.BACK]);
    else if (this.slot === 'ornament') matrix.multiplyMatrices(boneMatrices[this.bone] ?? IDENTITY, this.backMatrix!); // set for ornaments
    else if (onBack) matrix.multiplyMatrices(boneMatrices[Bone.BACK], this.backMatrix!); // set for all but wings
    else matrix.copy(boneMatrices[this.bone] ?? IDENTITY);
    this.model.group.matrixWorldNeedsUpdate = true;
    // A weapon is drawn at its first key: its vertices only need posing once.
    if (this.animated || !this.posed) this.model.pose(0, this.slot === 'wings' ? time * WING_KEYS_PER_SECOND : 0);
    this.posed = true;
  }
}

const IDENTITY = new THREE.Matrix4();

/** The models hanging from bones: the weapons in the hands (`linkBones`: right, left; the player's without) and the wings. */
export async function createAttachments(equipment?: EquipmentAppearance | null, linkBones?: [number, number]): Promise<Attachment[]> {
  // The Dark Raven in a hand flies next to its owner instead (pets.ts).
  const slots = (['rightHand', 'leftHand', 'wings'] as const).filter((slot) => equipment?.[slot] && !isDarkRaven(equipment[slot]));
  const attachments = await Promise.all(slots.map(async (slot) => {
    const item = equipment![slot]!; // the filter keeps the worn slots
    try {
      const model = await createItemModel(item);
      const bone = slot === 'wings' || !linkBones ? undefined : linkBones[slot === 'rightHand' ? 0 : 1];
      return model ? new Attachment({ model, item, slot, bone }) : null;
    } catch (error) {
      console.warn(`Item ${item.group},${item.number} not drawn`, (error as Error).message);
      return null;
    }
  }));
  return attachments.filter((attachment): attachment is Attachment => Boolean(attachment));
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

// The pets a player rides (Horn of Fenrir, Dark Horse, Horn of Uniria, Horn of Dinorant), which change its stance.
function mountOfPet(pet: AppearanceItem | null | undefined): Mount {
  if (!pet || pet.group !== 13) return 'none';
  return ({ 37: 'fenrir', 4: 'darkHorse', 2: 'uniria', 3: 'dinorant' } as Record<number, Mount>)[pet.number] ?? 'none';
}

// The additive second drawings of frozen bodies, one material per texture and colour.
const bodyGlowMaterials = new Map<string, THREE.MeshBasicMaterial>();
function bodyGlowMaterial(map: THREE.Texture, color: THREE.Color): THREE.MeshBasicMaterial {
  const key = `${map.uuid}|${color.getHexString()}`;
  let material = bodyGlowMaterials.get(key);
  if (!material) {
    material = new THREE.MeshBasicMaterial({ map, color, blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, depthFunc: THREE.LessEqualDepth });
    bodyGlowMaterials.set(key, material);
  }
  return material;
}

const TWO_PI = Math.PI * 2;
// Smoothly turns toward the target angle per frame (matching desktop MovePath's fractional turn
// without snapping at 45-degree grid corners; capped per frame so slow frames still turn smoothly).
function turnAngleTowards(current: number, target: number, dt: number): number {
  let delta = (target - current) % TWO_PI;
  if (delta > Math.PI) delta -= TWO_PI;
  else if (delta < -Math.PI) delta += TWO_PI;
  if (Math.abs(delta) < 1e-4) return target;
  const factor = Math.min(0.35, 1 - Math.pow(0.5, dt * 25));
  return current + delta * factor;
}

const WALK_GRACE_SECONDS = 0.25;
// The pick boxes (MonsterModelBody.h): DefaultBoxMin/Max for monsters and NPCs, PlayerBoxMax for the player model.
const DEFAULT_PICK_BOX_MIN = [-60, -60, 0];
const DEFAULT_PICK_BOX_MAX = [50, 50, 150];
const PLAYER_PICK_BOX_MAX = [40, 40, 120];

/** A model's own pick box corner from Models.json ("boundingBox": { "min": [x, y, z], "max": [x, y, z] }). */
function pickBoxCorner(box: unknown, corner: 'min' | 'max'): number[] | null {
  const value = box && typeof box === 'object' ? (box as Record<string, unknown>)[corner] : undefined;
  return Array.isArray(value) && value.length === 3 && value.every((n) => typeof n === 'number') ? value as number[] : null;
}

export class Character {
  id: number;
  name: string;
  kind: string;
  model: AnimatedModel;
  /**
   * Out of the camera's view this frame (main.ts cullCharacters): its body and items are not posed or drawn
   * (it still walks, turns and keeps its clock, so it shows right as soon as it comes into view).
   */
  culled = false;
  private readonly culledChildren: THREE.Object3D[] = [];
  actions: CharacterActions;
  keysPerSecond: (action: number) => number;
  /** Where it stands, in world units. */
  position: THREE.Vector2;
  cell: Cell;
  /** The cells still to walk. */
  path: Cell[];
  private walkGrace = 0;
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
  /** Pets and mounts next to it (the helper slot, the Dark Raven), drawn beside its group. */
  companions: Companion[] = [];
  /** Life left as a share (0-1) from the last hit the server reported; -1 while unknown. */
  healthStatus = -1;
  /** Set once the server reported its death: the death animation holds its last key. */
  dead = false;
  /** A monster's or NPC's type number, and how it attacks (Monsters.json). */
  monsterNumber: number | null = null;
  attacks: MonsterAttacksData | null = null;
  /** A player's class (0 Dark Wizard … 6 Rage Fighter): with its weapons it picks how it stands and walks. */
  classIndex: number | null = null;
  /** A monster or NPC of the player model (o->Type == MODEL_PLAYER): it stands, walks, attacks and is hit as a player. */
  playerShaped = false;
  /** A player-shaped skeleton (SubType MODEL_SKELETON1-3): it sounds as a monster and bursts into bones when it dies. */
  skeletonShape = false;
  /** The model slot a monster is drawn with (its map or random variant's), for its sound set. */
  monsterModel: string | null = null;
  /** How a monster appeared (describeMonster). */
  look: MonsterLook | null = null;
  /** Drawn without the scene's light (setup "light": false). */
  fullBright = false;
  /** A debuff's body colour (poisoned green, frozen blue...), drawn instead of the ground's light (b->BodyLight). */
  bodyTint: THREE.Color | null = null;
  /** Stands this high above the ground (setup "heightOffset"), or at this height whatever the ground ("height"). */
  heightOffset = 0;
  fixedHeight: number | null = null;
  /** Its body beside its tile, in world units (setup "positionOffset"). */
  bodyOffset = new THREE.Vector2();
  /**
   * The box a click picks it by, in its model's units around its feet (the desktop's BoundingBoxMin/Max,
   * MonsterModelBody.h; a model's own in Models.json "boundingBox"). Its drawn mesh can't be measured for this:
   * the bones bend it on the GPU, so the mesh's own bounds are a small knot at its feet.
   */
  pickBoxMin = new THREE.Vector3().fromArray(DEFAULT_PICK_BOX_MIN);
  pickBoxMax = new THREE.Vector3().fromArray(DEFAULT_PICK_BOX_MAX);

  /** Its pick box in world space, placed, turned and scaled like its body (for clicks, hovers and marks over it). */
  pickBounds(out: THREE.Box3): THREE.Box3 {
    this.group.updateMatrixWorld();
    return out.set(this.pickBoxMin, this.pickBoxMax).applyMatrix4(this.group.matrixWorld);
  }
  /** Faces this way whatever the server sends (setup "facing"), in radians. */
  fixedAngle: number | null = null;
  /** The animation it stands in (setup "startAction"), else its stop action. */
  startAction: number | null = null;

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
    // Companions live beside the character in the map's group: they come and go with it.
    this.group.addEventListener('added', () => { for (const companion of this.companions) this.showCompanion(companion); });
    this.group.addEventListener('removed', () => { for (const companion of this.companions) companion.detach(); });
  }

  private showCompanion(companion: Companion): void {
    const parent = this.group.parent;
    if (!parent) return;
    parent.add(companion.group);
    companion.attach();
  }

  setCompanions(companions: Companion[]): void {
    for (const companion of this.companions) companion.detach();
    this.companions = companions;
    for (const companion of companions) this.showCompanion(companion);
  }

  setAttachments(attachments: Attachment[]): void {
    for (const attachment of this.attachments) this.model.group.remove(attachment.model.group);
    this.attachments = attachments;
    for (const attachment of attachments) this.model.group.add(attachment.model.group);
    this.looksDirty = true;
  }

  /** What its buffs do to how its items are drawn (RenderPartObjectEffect): see itemGlow.ts setItemGlowState / setCloaked. */
  itemLooks = { debuffed: false, hardened: false, cloaked: false };
  private looksDirty = false;
  private lastReducedEffects = false;

  setItemLooks(looks: { debuffed: boolean; hardened: boolean; cloaked: boolean }): void {
    const now = this.itemLooks;
    if (now.debuffed === looks.debuffed && now.hardened === looks.hardened && now.cloaked === looks.cloaked) return;
    this.itemLooks = { ...looks };
    this.looksDirty = true;
  }

  private bodyGlow: THREE.Mesh[] | null = null;

  /**
   * A frozen (or Blow of Destruction) monster's second drawing (RenderObject): its body again, every mesh added in the
   * body's colour (BlendMesh -2). null: none.
   */
  setBodyGlow(color: THREE.Color | null): void {
    if (this.bodyGlow) for (const mesh of this.bodyGlow) mesh.removeFromParent();
    this.bodyGlow = null;
    if (!color) return;
    this.bodyGlow = this.model.parts.filter((part) => part.mesh.material.visible && part.mesh.material.map).map((part) => {
      const mesh = this.model.overlay(part.geometry, bodyGlowMaterial(part.mesh.material.map!, color)); // the filter keeps parts with a map
      this.model.group.add(mesh);
      return mesh;
    });
  }

  private applyItemLooks(): void {
    this.looksDirty = false;
    const { debuffed, hardened, cloaked } = this.itemLooks;
    const reduced = performanceFlags().reducedEffects;
    this.lastReducedEffects = reduced;
    for (const model of [this.model, ...this.attachments.map((attachment) => attachment.model)]) {
      if (!cloaked) void setCloaked(model, false);
      setItemGlowState(model, { debuffed: debuffed || cloaked || reduced, hardened: hardened || cloaked || reduced });
      if (cloaked) void setCloaked(model, true);
    }
  }

  get group(): THREE.Group { return this.model.group; }

  /** It left the map for good: its GPU skeletons go back to their pool (the shared geometry and textures stay). */
  release(): void {
    this.model.release();
    for (const attachment of this.attachments) attachment.model.release();
  }

  placeAt(x: number, y: number, rotation?: number): void {
    this.cell = { x, y };
    this.position.set(cellCenter(x), cellCenter(y));
    this.path = [];
    this.walkGrace = 0;
    if (rotation !== undefined) this.angle = angleFromRotation(rotation);
    if (this.fixedAngle !== null) this.angle = this.fixedAngle;
  }

  walk(cells: Cell[]): void {
    const path = cells.slice();
    // Skip a leading cell that is already behind the character's current position relative to
    // the next cell, so a mid-walk redirect never steps backwards.
    if (path.length > 1) {
      const tx0 = cellCenter(path[0].x), ty0 = cellCenter(path[0].y);
      const tx1 = cellCenter(path[1].x), ty1 = cellCenter(path[1].y);
      if ((tx0 - this.position.x) * (tx1 - tx0) + (ty0 - this.position.y) * (ty1 - ty0) < 0) {
        path.shift();
      }
    }
    this.path = path;
    if (this.path.length > 0) this.walkGrace = WALK_GRACE_SECONDS;
  }

  // Walks straight to a cell, used for others whose exact steps we don't simulate.
  walkStraightTo(x: number, y: number): void {
    this.path = [{ x, y }];
    this.walkGrace = WALK_GRACE_SECONDS;
  }

  get isWalking(): boolean { return this.path.length > 0; }

  /** The walkable cell the character stands closest to (it can be between two cells while walking). */
  nearestCell(flags: ArrayLike<number>): Cell {
    const here = { x: Math.floor(this.position.x / TERRAIN_SCALE), y: Math.floor(this.position.y / TERRAIN_SCALE) };
    const walkable = (c: Cell) => c.x >= 0 && c.y >= 0 && c.x < TERRAIN_SIZE && c.y < TERRAIN_SIZE && (flags[c.y * TERRAIN_SIZE + c.x] & TerrainFlag.NOMOVE) === 0;
    return walkable(here) ? here : { ...this.cell };
  }

  // Plays an animation once, then goes back to standing or walking; hold keeps its last key (death).
  playOnce(action: number, keysPerSecond?: number, hold = false): void {
    this.walkGrace = 0;
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

  // How it stands or walks: a player by its class, weapons, wings and the safe zone (the desktop's
  // SetPlayerStop / SetPlayerWalk); others always with their stop and walk actions.
  private stanceAction(world: CharacterWorld): number {
    const walking = this.isWalking || this.walkGrace > 0;
    if (this.classIndex === null || !this.equipment) return walking ? this.actions.walk : this.startAction ?? this.actions.stop;
    const hand = (item: AppearanceItem | null | undefined) => (item ? { group: item.group, number: item.number } : null);
    const input = {
      classIndex: this.classIndex, rightHand: hand(this.equipment.rightHand), leftHand: hand(this.equipment.leftHand),
      mount: mountOfPet(this.equipment.pet), wings: Boolean(this.equipment.wings),
      safeZone: (world.flags[this.cell.y * TERRAIN_SIZE + this.cell.x] & TerrainFlag.SAFEZONE) !== 0,
    };
    const action = walking ? playerWalkAction(input) : playerStopAction(input);
    return action >= 0 ? action : this.action;
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
      const action = this.stanceAction(world);
      if (action !== this.action) { this.action = action; this.time = 0; }
      this.time += dt * (this.keysPerSecond(action) ?? 6);
    }
    const x = this.position.x + this.bodyOffset.x, y = this.position.y + this.bodyOffset.y;
    this.group.position.set(x, y, this.fixedHeight ?? sampleHeight(world.heights, x, y) + this.heightOffset);
    this.group.rotation.set(0, 0, this.angle);
    if (this.culled) {
      // Its drawings are put away (the group itself stays visible: the effects and arrows read that as
      // "in the world") and come back as they were when it is in view again.
      for (const child of this.group.children) {
        if (child.visible) { child.visible = false; this.culledChildren.push(child); }
      }
      this.clock += dt;
      if (this.companions.length) this.updateCompanions(dt, world);
      return;
    }
    if (this.culledChildren.length) {
      for (const child of this.culledChildren) if (child.parent === this.group) child.visible = true;
      this.culledChildren.length = 0;
    }
    const perf = performanceFlags();
    if (perf.reducedEffects !== this.lastReducedEffects) this.looksDirty = true;
    this.model.pose(this.action, this.time);
    if (this.looksDirty) this.applyItemLooks();
    this.clock += dt;
    const light = this.bodyTint ? this.light.copy(this.bodyTint) : this.fullBright ? this.light.setRGB(1, 1, 1) : sampleModelLight(world.light, x, y, this.light);
    this.model.setLight(light, this.clock);
    // Players carry their weapons on the back in a safe zone (AreWeaponsOnBack), never in Blood Castle and Chaos
    // Castle; a monster model shows no weapons there at all (only the player model has a back for them).
    const mapNumber = world.mapNumber ?? -1;
    const castle = (mapNumber >= 11 && mapNumber <= 23) || mapNumber === 52 || mapNumber === 53;
    this.weaponsOnBack = !this.holdWeapons && !castle && (world.flags[this.cell.y * TERRAIN_SIZE + this.cell.x] & TerrainFlag.SAFEZONE) !== 0;
    const backs = this.classIndex !== null;
    for (const attachment of this.attachments) {
      if (perf.hideWingsPets && attachment.slot === 'wings') {
        attachment.model.group.visible = false;
        continue;
      }
      // Only the player model has a back; a monster model shows its weapons in its hands, outside a safe zone.
      const ornament = attachment.slot === 'ornament';
      const onBack = !ornament && (this.weaponsOnBack || attachment.ammunition);
      // The +15 armour's decorations: not in Chaos Castle (DrawsNextGradeObjects).
      const shown = ornament ? !(perf.reducedEffects || (mapNumber >= 18 && mapNumber <= 23) || mapNumber === 53) : attachment.slot === 'wings' || backs || !onBack;
      attachment.model.group.visible = shown;
      if (!shown) continue;
      attachment.place(this.model.matrices, onBack, this.clock);
      attachment.model.setLight(light, this.clock);
    }
    if (this.companions.length) {
      if (perf.hideWingsPets) {
        for (const companion of this.companions) companion.group.visible = false;
      } else {
        for (const companion of this.companions) companion.group.visible = true;
        this.updateCompanions(dt, world);
      }
    }
  }

  private updateCompanions(dt: number, world: CharacterWorld): void {
    const safeZone = (world.flags[this.cell.y * TERRAIN_SIZE + this.cell.x] & TerrainFlag.SAFEZONE) !== 0;
    const owner: CompanionOwner = {
      position: this.position, height: this.group.position.z, angle: this.angle, action: this.action,
      scale: this.group.scale.x, safeZone, boneMatrices: this.model.matrices, mapNumber: world.mapNumber ?? null,
      ridesDarkHorse: this.equipment?.pet?.group === 13 && this.equipment.pet.number === 4,
    };
    for (const companion of this.companions) companion.update(dt, owner, world.light);
  }

  advance(dt: number): void {
    if (this.path.length > 0) {
      this.walkGrace = WALK_GRACE_SECONDS;
    } else if (this.walkGrace > 0) {
      this.walkGrace = Math.max(0, this.walkGrace - dt);
    }

    let budget = WALK_CELLS_PER_SECOND * TERRAIN_SCALE * dt;
    while (budget > 0 && this.path.length) {
      const next = this.path[0];
      const tx = cellCenter(next.x), ty = cellCenter(next.y);
      const dx = tx - this.position.x, dy = ty - this.position.y;
      const distance = Math.hypot(dx, dy);
      if (distance > 1) {
        const targetAngle = Math.atan2(dx, -dy); // CreateAngle (Engine/AI/AngleMath.cpp)
        this.angle = turnAngleTowards(this.angle, targetAngle, dt);
      }
      if (distance <= budget) {
        this.position.set(tx, ty);
        this.cell = { x: next.x, y: next.y };
        this.path.shift();
        budget -= distance;
      } else {
        const step = budget / distance;
        this.position.x += dx * step;
        this.position.y += dy * step;
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

// The decorations of +15 armour (ForEachNextGradeObject): per slot the part models and the bones they hang from,
// placed as PlaceLinkedPart places MODEL_15GRADE_ARMOR_OBJ_* (angles in degrees, then the translation).
const GRADE15_ORNAMENTS: Partial<Record<EquipmentSlotName, [string, number, Vec3, Vec3][]>> = {
  helm: [['grade15Head', 20, [180, -90, 0], [28, 20, 0]]],
  armor: [['grade15BodyLeft', 35, [0, -90, 0], [5, -20, 0]], ['grade15BodyRight', 26, [0, -90, 0], [5, -20, 0]]],
  pants: [['grade15PantLeft', 3, [0, 90, 180], [25, 5, -5]], ['grade15PantRight', 10, [0, 90, 180], [25, 5, 5]]],
  gloves: [['grade15ArmLeft', 36, [0, -90, 0], [30, 0, 20]], ['grade15ArmRight', 27, [0, -90, 0], [30, 0, -20]]],
  boots: [['grade15BootLeft', 4, [0, 90, 180], [20, 15, -10]], ['grade15BootRight', 11, [0, 90, 180], [20, 15, 10]]],
};

/** The decorations a player's +15 armour pieces wear (drawn without a level of their own, in their render style). */
async function createOrnaments(equipment?: EquipmentAppearance | null): Promise<Attachment[]> {
  const made: Promise<Attachment | null>[] = [];
  for (const [slot, ornaments] of Object.entries(GRADE15_ORNAMENTS) as [EquipmentSlotName, [string, number, Vec3, Vec3][]][]) {
    const item = equipment?.[slot];
    if (!item || (item.glowLevel ?? 0) !== 15) continue;
    for (const [name, bone, angles, translation] of ornaments) {
      const source = partModel(name);
      if (!source) continue;
      made.push(createSourceModel(source, { group: -1, number: -1, glowLevel: 0 }, 'worn')
        .then((model) => new Attachment({ model, item, slot: 'ornament', bone, placement: angleMatrix(angles, translation) }))
        .catch(() => null));
    }
  }
  return (await Promise.all(made)).filter((attachment): attachment is Attachment => attachment !== null);
}

export async function createPlayerCharacter({ id, name, bodySet, equipment, kind = 'player' }: PlayerCharacterOptions): Promise<Character> {
  const [model, held, ornaments, companions] = await Promise.all([
    createPlayerModel(bodySet, equipment), createAttachments(equipment), createOrnaments(equipment), kind === 'player' ? createCompanions(equipment) : Promise.resolve([]),
  ]);
  const attachments = [...held, ...ornaments];
  const character = new Character({
    id, name, kind, model,
    actions: { stop: PlayerAction.STOP, walk: PlayerAction.WALK },
    keysPerSecond: (action: number) => playerActionSpeed(action, { ...playerSpeeds, classIndex: Math.max(0, bodySet - 1) }),
  });
  character.setAttachments(attachments);
  character.setCompanions(companions);
  if (kind === 'player') character.classIndex = Math.max(0, bodySet - 1);
  character.equipment = equipment ?? {};
  character.pickBoxMax.fromArray(PLAYER_PICK_BOX_MAX);
  return character;
}

/** Where a monster appears (its map variants), and the draw of its random look. */
export interface MonsterSpawnOptions extends DescribeOptions {
  id: number;
  type: number;
}

// The setup fields that change how the model is drawn (DressCharacter): the glowing mesh (BlendMesh, its light),
// the hidden mesh (-2: none drawn), how see-through it is and whether it takes the scene's light.
function applyDrawSetup(model: AnimatedModel, setup: MonsterSetupData, ownMeshes: number): void {
  const own = model.parts.slice(0, ownMeshes);
  if (setup.hiddenMesh !== undefined) {
    for (const part of own) if (setup.hiddenMesh === -2 || part.meshIndex === setup.hiddenMesh) part.mesh.visible = false;
  }
  if (setup.glowMesh !== undefined) {
    const light = setup.glowLight ?? 1;
    for (const part of own) {
      if (part.meshIndex !== setup.glowMesh) continue;
      Object.assign(part.mesh.material, { blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, alphaTest: 0 });
      part.mesh.material.color.setScalar(light);
    }
  }
  if (setup.alphaTarget !== undefined && setup.alphaTarget < 1) {
    model.group.traverse((object) => {
      if (!(object instanceof THREE.Mesh)) return;
      const material = object.material as THREE.Material;
      material.transparent = true;
      material.opacity = setup.alphaTarget!; // checked above
      material.depthWrite = false;
    });
  }
}

// The body sources of a player-shaped monster: its skin (a skeleton) alone, or its body parts, each the worn item's
// model or a body that is no item. A part it wears nothing on is not drawn (CreateCharacterPointer: Type -1).
function playerShapedSources(look: MonsterLook): BodyPartSource[] {
  const skin = look.player?.skin;
  if (skin) return [{ dir: skin.dir, fileName: skin.fileName, textureDir: skin.textureDir }];
  const sources: BodyPartSource[] = [];
  for (const slot of BODY_PART_SLOT_NAMES) {
    const source = itemBodySource(look.equipment[slot]) ?? (look.bodyParts[slot] ? { ...look.bodyParts[slot]! } : null); // checked
    if (source) sources.push(source);
  }
  return sources;
}

// The body parts a monster model wears over its own meshes (the merchants' heads and clothes).
function modelPartSources(look: MonsterLook): BodyPartSource[] {
  const sources: BodyPartSource[] = [];
  for (const slot of BODY_PART_SLOT_NAMES) {
    const source = itemBodySource(look.equipment[slot]) ?? (look.bodyParts[slot] ? { ...look.bodyParts[slot]! } : null); // checked
    if (source) sources.push(source);
  }
  return sources;
}

/** The skin's render tier as an item glow (the Lorencia skeleton NPC's RenderPartObject with c->Level). */
async function addSkinGlow(model: AnimatedModel, level: number): Promise<void> {
  if (level > 0) await addItemGlow(model, model.parts, {}, { group: -1, number: -1, glowLevel: level });
}

/**
 * A monster or NPC as the desktop makes it when it appears (Setup::Create): its model on the map (or a random
 * variant's), its setup, and what it wears. A player-shaped one is the player model with its skin or body parts,
 * weapons, wings and helper, standing, walking and attacking as a player; any other its own model with the
 * weapons on the model's link bones. A model whose file is missing has the unknown monsters' look.
 */
export async function createMonsterCharacter({ id, type, ...place }: MonsterSpawnOptions): Promise<Character> {
  let look = describeMonster(type, place);
  try {
    return await buildMonsterCharacter(id, look);
  } catch (error) {
    if (look.unknown || look.model === 'player') throw error;
    // A map object the map's folder lacks is not drawn on the desktop either (no model in its slot).
    if (look.model.startsWith('mapObject ')) return buildMonsterCharacter(id, { ...look, body: null });
    console.warn(`Monster ${type}: model ${look.model} not drawn (${(error as Error).message}); the unknown monsters' look instead`);
    look = describeMonster(type, { ...place, unknownReason: 'missingFile' });
    return buildMonsterCharacter(id, look);
  }
}

async function buildMonsterCharacter(id: number, look: MonsterLook): Promise<Character> {
  const { setup } = look;
  let character: Character;
  if (look.player) {
    const player = look.player;
    const equipment = look.equipment;
    const startAction = setup.startAction ?? PlayerAction.STOP;
    const [model, attachments, companions] = await Promise.all([
      createBodyModel(PLAYER_SKELETON, playerShapedSources(look), startAction), createAttachments(equipment), createCompanions(equipment),
    ]);
    await addSkinGlow(model, player.skinLevel);
    character = new Character({
      id, name: look.name, kind: look.kind, model,
      actions: { stop: PlayerAction.STOP, walk: PlayerAction.WALK },
      keysPerSecond: (action: number) => playerActionSpeed(action, { ...playerSpeeds, classIndex: player.classIndex }),
    });
    character.setAttachments(attachments);
    character.setCompanions(companions);
    character.classIndex = player.classIndex;
    character.equipment = equipment;
    character.playerShaped = true;
    character.skeletonShape = isSkeletonSkin(setup.subType);
    applyDrawSetup(model, setup, model.parts.length);
  } else if (!look.body) {
    // No body (model "none", or one the map's own code draws): nothing to see, as on the desktop.
    const model = new AnimatedModel(NO_BODY, [], [], { skinned: true });
    character = new Character({ id, name: look.name, kind: look.kind, model, actions: { stop: 0, walk: 0 }, keysPerSecond: () => 0 });
  } else {
    const body = look.body;
    const extra = modelPartSources(look);
    const own: BodyPartSource = { dir: body.dir, fileName: body.fileName, textureDir: body.textureDir, textureNames: body.textureNames };
    const [model, attachments] = await Promise.all([
      createBodyModel(body, extra, setup.startAction ?? MonsterAction.STOP, own), createAttachments(look.equipment, body.linkBones),
    ]);
    applyDrawSetup(model, setup, model.skeleton.meshes.length); // the model's own meshes come first
    character = new Character({
      id, name: look.name, kind: look.kind, model,
      actions: { stop: MonsterAction.STOP, walk: MonsterAction.WALK },
      keysPerSecond: (action: number) => body.keysPerSecond[action] ?? 0,
    });
    character.setAttachments(attachments.filter((attachment) => attachment.slot !== 'wings'));
  }
  character.group.scale.setScalar(look.scale);
  character.monsterNumber = look.number;
  character.monsterModel = look.model;
  character.attacks = look.attacks;
  character.look = look;
  if (look.player) character.pickBoxMax.fromArray(PLAYER_PICK_BOX_MAX);
  const boxMin = pickBoxCorner(setup.boundingBox, 'min'), boxMax = pickBoxCorner(setup.boundingBox, 'max');
  if (boxMin) character.pickBoxMin.fromArray(boxMin);
  if (boxMax) character.pickBoxMax.fromArray(boxMax);
  if (setup.light === false) character.fullBright = true;
  if (setup.heightOffset !== undefined) character.heightOffset = setup.heightOffset;
  if (setup.height !== undefined) character.fixedHeight = setup.height;
  if (setup.positionOffset) character.bodyOffset.set(setup.positionOffset[0], setup.positionOffset[1]);
  // A tile offset: the body stands that many tiles beside the server's tile (the Smelting NPC).
  if (setup.tileOffset) character.bodyOffset.add(new THREE.Vector2(setup.tileOffset[0] * TERRAIN_SCALE, setup.tileOffset[1] * TERRAIN_SCALE));
  if (setup.startAction !== undefined) { character.action = setup.startAction; character.startAction = setup.startAction; }
  if (setup.startFrame !== undefined) character.time = setup.startFrame;
  if (setup.facing !== undefined) character.fixedAngle = setup.facing * DEGREES;
  // The Lorencia skeleton NPC faces east there, the Devias one north (AnimationCharacter).
  if (look.player && look.kind === 'npc' && isSkeletonSkin(setup.subType) && (look.place.map === 0 || look.place.map === 2)) {
    character.fixedAngle = look.place.map === 0 ? 90 * DEGREES : 0;
  }
  return character;
}
