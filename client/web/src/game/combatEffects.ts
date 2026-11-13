// What the desktop client draws around a fight that is not an animation of the fighters
// themselves: the arrows and bolts of bows and crossbows, and what happens to a body after
// its death (it rots: waits, fades out and sinks; leaves a blood pool; a few monsters burst
// into pieces). Numbers are the desktop's, per 25 FPS frame (ANIMATION_FRAMES_PER_SECOND);
// update(dt) scales them by dt * 25 the way the desktop scales by FPS_ANIMATION_FACTOR.
//
// Followed C++ (client/src/source):
//   CreateArrow, CreateArrows              Render/Effects/ZzzEffectMagicSkill.cpp
//   CreateEffect (the arrow cases, MODEL_ICE_SMALL, MODEL_EFFECT_BROKEN_ICE0..3,
//     MODEL_SHADOW_*_*, MODEL_ICE_GIANT_PART*), MoveEffect's shared tail, MoveParticle,
//     MoveJump, CheckClientArrow, CheckCharacterRange, IsLiveCharacterInRange
//                                          Render/Effects/ZzzEffect.cpp
//   Move_MODEL_ARROW, Move_MODEL_ARROW_STEEL, Move_MODEL_ARROW_SPARK, ..., Move_MODEL_ICE_SMALL
//                                          Render/Effects/Behaviors/MoveHandlers.cpp
//   MoveCharacter (AttackTime, g_iLimitAttackTime), DeadCharacter,
//   PlayerStopAnimationSetting, EtcStopAnimationSetting
//                                          Engine/Object/ZzzCharacter.cpp
//   CreateBlood                            Render/Effects/ZzzEffectBlurSpark.cpp
//   CreatePointer, MovePointers, RenderPointers
//                                          Render/Effects/ZzzEffectPointer.cpp
//   RenderTerrainAlphaBitmap               Render/Terrain/ZzzLodTerrain.cpp
//   GMSwampOfQuiet::UpdateMonsterEffects / RenderObject (Shadow Pawn, Knight, Look)
//   GM_Raklion (Ice Giant, Dark Giant death)
//                                          World/GameMaps/

import * as THREE from 'three';
import { loadModel, buildGeometries, computeBoneMatrices, createMaterial, loadTexture, AnimatedModel } from '../bmd.js';
import { TERRAIN_SCALE } from '../formats.js';
import { ANIMATION_FRAMES_PER_SECOND, PlayerActions, MonsterActions } from './combatLook.js';
import type { BMDModel } from '../formats.js';
import type { Character } from './characters.js';
import type { HandItem } from './combatLook.js';

const DEGREES = Math.PI / 180;

/** Rand() % n. */
function randInt(n: number): number {
  return Math.floor(Math.random() * n);
}

/** AngleMatrix (Core/Math): degrees around x, y and z, applied z, y, x. */
function angleEuler(x: number, y: number, z: number, out = new THREE.Euler()): THREE.Euler {
  return out.set(x * DEGREES, y * DEGREES, z * DEGREES, 'ZYX');
}

/** The ground height under a point; the caller's terrain (terrain.ts sampleHeight). */
export type HeightAt = (x: number, y: number) => number;

// ---------------------------------------------------------------------------
// Effect models (CreateEffect's models, drawn by RenderObject with BlendMesh).
// ---------------------------------------------------------------------------

interface EffectModel {
  model: BMDModel;
  materials: THREE.MeshBasicMaterial[];
}

const effectModels = new Map<string, Promise<EffectModel | null>>();

// A model of Data/<dir>/<file>; the meshes whose texture is the effect's BlendMesh (all of
// them for BlendMesh -2) are drawn additively (BMD::RenderBody, MeshMaterial::BlendMesh).
function loadEffectModel(dir: string, file: string, textureDirs: string[], blendMesh: number): Promise<EffectModel | null> {
  const key = `${dir}/${file}|${textureDirs.join(',')}|${blendMesh}`;
  let loaded = effectModels.get(key);
  if (!loaded) {
    loaded = (async (): Promise<EffectModel | null> => {
      try {
        const model = await loadModel(dir, file);
        const materials = await Promise.all(model.meshes.map((mesh) =>
          createMaterial(textureDirs, mesh.textureName, { glow: blendMesh <= -2 || mesh.texture === blendMesh })));
        return { model, materials };
      } catch (error) {
        console.warn(`Effect model ${dir}/${file} not drawn`, (error as Error).message);
        return null;
      }
    })();
    effectModels.set(key, loaded);
  }
  return loaded;
}

// One drawn copy. Materials are cloned when the copy fades on its own (o->Alpha).
function instantiate(loaded: EffectModel, ownMaterials: boolean): AnimatedModel {
  const parts = buildGeometries(loaded.model, computeBoneMatrices(loaded.model, 0, 0));
  const materials = parts.map((part) => {
    const material = loaded.materials[part.meshIndex];
    return ownMaterials ? material.clone() : material;
  });
  const animated = new AnimatedModel(loaded.model, parts, materials);
  for (const part of animated.parts) part.mesh.frustumCulled = false;
  return animated;
}

function disposeInstance(animated: AnimatedModel, ownMaterials: boolean): void {
  animated.group.removeFromParent();
  for (const part of animated.parts) {
    part.geometry.dispose();
    if (ownMaterials) part.mesh.material.dispose();
  }
}

// ---------------------------------------------------------------------------
// Monster data (Data/Monsters/Monsters.json and Models.json): the parts these effects read.
// ---------------------------------------------------------------------------

interface WeaponEntry { group: number; number: number; level?: number }

interface MonsterEntry {
  model?: string;
  /** CHARACTER::Blood set at spawn: the blood pool of its death counts as done. */
  noBloodPool?: boolean;
  /** Object.LifeTime set at spawn; 100 arms the model's own death effect. */
  deathEffect?: number;
  equipment?: { weapons?: (WeaponEntry | null)[] };
  numbers: { number: number; maps?: number[] }[];
}

interface ModelPart { effect?: string; slot?: string; file: string; textureFolders?: string[] }

interface ModelEntry {
  slot: string;
  file: string;
  textureFolders?: string[];
  /** BMD::BoneHead; -1 (none) when left out. */
  headBone?: number;
  parts?: ModelPart[];
}

interface EffectsData {
  monsters: Map<number, MonsterEntry>;
  models: Map<string, ModelEntry>;
  unknownModel: string;
}

let effectsData: EffectsData | null = null;
let effectsDataLoading: Promise<EffectsData> | null = null;

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return response.json() as Promise<T>;
}

/** Loads the monster data the effects read. Safe to call often; the arrow helpers for monsters need it loaded. */
export function loadCombatEffectsData(): Promise<void> {
  if (!effectsDataLoading) {
    effectsDataLoading = (async () => {
      const [monsters, models] = await Promise.all([
        fetchJson<{ unknownMonsters: { model: string }; monsters: MonsterEntry[] }>('/data/Monsters/Monsters.json'),
        fetchJson<{ models: ModelEntry[] }>('/data/Monsters/Models.json'),
      ]);
      const byNumber = new Map<number, MonsterEntry>();
      for (const monster of monsters.monsters) for (const entry of monster.numbers) byNumber.set(entry.number, monster);
      effectsData = { monsters: byNumber, models: new Map(models.models.map((model) => [model.slot, model])), unknownModel: monsters.unknownMonsters.model };
      return effectsData;
    })();
  }
  return effectsDataLoading.then(() => undefined);
}

/** "Data/Monster/x.bmd" as the loadModel folder and file name. */
function splitDataPath(path: string): { dir: string; file: string } {
  const relative = path.replace(/^Data\//, '');
  const slash = relative.lastIndexOf('/');
  return { dir: relative.slice(0, slash), file: relative.slice(slash + 1) };
}

// ---------------------------------------------------------------------------
// Arrows and bolts.
// ---------------------------------------------------------------------------

/** The arrow effects (MODEL_ARROW, MODEL_ARROW_STEEL, ...), named after the desktop's model types. */
export type ProjectileKind =
  | 'arrow' | 'arrowSteel' | 'arrowThunder' | 'arrowLaser' | 'arrowV' | 'arrowSaw' | 'arrowNature'
  | 'arrowWing' | 'arrowBomb' | 'arrowDouble' | 'arrowHoly' | 'arrowBestCrossbow' | 'arrowDrill'
  | 'laceArrow' | 'arrowSpark' | 'arrowRing' | 'arrowDarkStinger' | 'arrowGamble';

interface ArrowSpec {
  /** The model in Data/Skill; null when the desktop loads none (MODEL_ARROW_HOLY draws only its trails). */
  file: string | null;
  textureDirs: string[];
  scale: number;
  /** -Direction[1]: units per frame along the facing. */
  speed: number;
  /** LifeTime in frames. */
  lifeFrames: number;
  /** Where it starts: 'bow' adds (-10, -60, 135) turned by the shooter's angle; 'top' adds 130 up. */
  start: 'bow' | 'top' | 'holy';
  /** o->BlendMesh: the mesh texture drawn additively, -2 for all, -1 for none. */
  blendMesh: number;
  /** Degrees per frame added to Angle[1] (the roll) by its move handler. */
  spin: number | 'random30to89';
  /** Angle[1] = Rand() % 360 when made. */
  randomRoll: boolean;
  /** o->Velocity: animation keys per frame (CreateEffect's default is 0.3). */
  keysPerFrame: number;
  /** Its move handler calls CheckClientArrow (it ends near the first character it meets). */
  stopsAtCharacters: boolean;
  /** MoveJump: falls with Gravity and bounces on the ground (MODEL_ARROW_BOMB). */
  gravity: number | null;
}

const SKILL_TEXTURES = ['Skill', 'Item', 'Effect'];
const ITEM_TEXTURES = ['Item', 'Skill', 'Effect'];

function bowArrow(file: string, overrides: Partial<ArrowSpec> = {}): ArrowSpec {
  return {
    file, textureDirs: SKILL_TEXTURES, scale: 0.8, speed: 70, lifeFrames: 30, start: 'bow', blendMesh: -1,
    spin: 0, randomRoll: false, keysPerFrame: 1, stopsAtCharacters: true, gravity: null, ...overrides,
  };
}

// CreateEffect's arrow cases (ZzzEffect.cpp) and each type's move handler.
const ARROWS: Record<ProjectileKind, ArrowSpec> = {
  arrow: bowArrow('Arrow01.bmd', { blendMesh: 1 }),
  arrowSteel: bowArrow('ArrowSteel01.bmd'),
  arrowThunder: bowArrow('ArrowThunder01.bmd', { blendMesh: 0 }),
  arrowLaser: bowArrow('ArrowLaser01.bmd', { blendMesh: 0 }),
  arrowV: bowArrow('ArrowV01.bmd', { blendMesh: 0 }),
  arrowSaw: bowArrow('ArrowSaw01.bmd'),
  arrowNature: bowArrow('ArrowNature01.bmd', { blendMesh: -2, spin: 30 }),
  arrowWing: bowArrow('ArrowWing01.bmd', { blendMesh: 0, scale: 1.8, speed: 50 }),
  laceArrow: bowArrow('LaceArrow.bmd', { textureDirs: ITEM_TEXTURES, blendMesh: 1, spin: 60, randomRoll: true }),
  arrowSpark: bowArrow('Arrow_Spark.bmd', { scale: 1, spin: 35 }),
  arrowRing: bowArrow('CW_Bow_Skill.bmd', { scale: 1, blendMesh: 0, randomRoll: true }),
  arrowDarkStinger: bowArrow('sketbows_arrows.bmd'),
  // MODEL_ARROW_GAMBLE has no move handler, so it never calls CheckClientArrow.
  arrowGamble: bowArrow('gamble_arrows01.bmd', { stopsAtCharacters: false }),
  arrowBomb: bowArrow('ArrowBomb01.bmd', { textureDirs: ITEM_TEXTURES, scale: 1, speed: 30, lifeFrames: 40, blendMesh: 0, gravity: -10 }),
  arrowDouble: bowArrow('ArrowDouble01.bmd', { scale: 1, start: 'top', blendMesh: -2, spin: 30, keysPerFrame: 0.3 }),
  arrowBestCrossbow: bowArrow('kcross.bmd', { scale: 1, start: 'top', blendMesh: -2, spin: 'random30to89', keysPerFrame: 0.3 }),
  arrowDrill: bowArrow('Carow.bmd', { scale: 1, start: 'top', blendMesh: -2, spin: 30, keysPerFrame: 0.3 }),
  // Celestial Bow: no model is loaded for it; a plain shot (SubType 0) doesn't check for characters.
  arrowHoly: bowArrow('', { file: null, scale: 1, speed: 60, start: 'holy', blendMesh: -2, spin: 30, stopsAtCharacters: false }),
};

/**
 * How long after the attack starts the arrow leaves: the desktop's AttackTime starts at 1
 * with the attack and the shot is made when it reaches g_iLimitAttackTime (15) in MoveCharacter.
 */
export const ARROW_RELEASE_DELAY_SECONDS = (15 - 1) / ANIMATION_FRAMES_PER_SECOND;
/** CheckClientArrow: an arrow ends within this distance (x, y) of a live character other than its shooter. */
export const ARROW_HIT_RANGE = 100;
/** CreateArrow: riding a Fenrir lifts the start this much. */
export const ARROW_FENRIR_LIFT = 30;
/** The offset of a bow shot's start from the shooter, turned by its angle (CreateEffect). */
const BOW_START_OFFSET = new THREE.Vector3(-10, -60, 135);
const TOP_START_LIFT = 130;
const HOLY_START_OFFSET = new THREE.Vector3(-10, -100, 15);

const BOW_GROUP = 4;
// CreateArrow's switch on the right hand (Weapon[0]): crossbows, by item number of group 4.
const RIGHT_HAND_ARROWS: Partial<Record<number, ProjectileKind>> = {
  8: 'arrowSteel', 9: 'arrowSteel', 10: 'arrowSaw', 11: 'arrowLaser', 12: 'arrowThunder', 13: 'arrowWing',
  14: 'arrowBomb', 16: 'arrowDouble', 18: 'arrowBestCrossbow', 19: 'arrowDrill',
};
// ... and on the left hand (Weapon[1]): bows.
const LEFT_HAND_ARROWS: Partial<Record<number, ProjectileKind>> = {
  0: 'arrow', 1: 'arrow', 2: 'arrowV', 3: 'arrow', 4: 'arrow', 5: 'arrow', 6: 'arrowNature', 17: 'arrowHoly',
  20: 'laceArrow', 21: 'arrowSpark', 22: 'arrowRing', 23: 'arrowDarkStinger', 24: 'arrowGamble',
};

/**
 * CreateArrow: the arrows a shot makes, by the weapon in each hand (the ammunition item is
 * not looked at). The desktop makes one per matching hand, right hand first; normally one.
 */
export function arrowKindsForWeapons(rightHand: HandItem | null, leftHand: HandItem | null): ProjectileKind[] {
  const kinds: ProjectileKind[] = [];
  const right = rightHand?.group === BOW_GROUP ? RIGHT_HAND_ARROWS[rightHand.number] : undefined;
  const left = leftHand?.group === BOW_GROUP ? LEFT_HAND_ARROWS[leftHand.number] : undefined;
  if (right) kinds.push(right);
  if (left) kinds.push(left);
  return kinds;
}

// MoveCharacter: monsters of these models shoot at a target; MODEL_ORC_ARCHER only when the
// attack has no target character (TargetCharacter == -1).
const SHOOTING_MONSTER_MODELS = ['MONSTER_MODEL_HUNTER', 'MONSTER_MODEL_VALKYRIE', 'MONSTER_MODEL_SOLDIER'];
const UNTARGETED_SHOOTING_MONSTER_MODELS = [...SHOOTING_MONSTER_MODELS, 'MONSTER_MODEL_ORC_ARCHER'];

/**
 * The arrows a monster's plain attack makes (MoveCharacter, then CreateArrow with its
 * Monsters.json weapons). Monsters drawn with the player model shoot like players when they
 * play a bow attack. Needs loadCombatEffectsData(); returns [] until it is loaded.
 */
export function arrowKindsForMonster(monsterNumber: number, hasTarget: boolean): ProjectileKind[] {
  const monster = effectsData?.monsters.get(monsterNumber);
  if (!monster) return [];
  const weapons = monster.equipment?.weapons ?? [];
  const hands = (): ProjectileKind[] => arrowKindsForWeapons(weapons[0] ?? null, weapons[1] ?? null);
  if (monster.model === 'player') return hands();
  const models = hasTarget ? SHOOTING_MONSTER_MODELS : UNTARGETED_SHOOTING_MONSTER_MODELS;
  return monster.model && models.includes(monster.model) ? hands() : [];
}

export interface ShootOptions {
  /** The shooter rides a Fenrir (CreateArrow lifts the start by ARROW_FENRIR_LIFT). */
  fenrir?: boolean;
  /** Degrees added to the shooter's facing for this arrow (CreateArrows' triple-shot fan: 0, +15, -15). */
  angleOffsetDegrees?: number;
}

export interface ProjectileLayerOptions {
  /** Ground height, for the bouncing bomb arrow. Without it the shooter's ground is used. */
  heightAt?: HeightAt;
  /** Every character on the map: an arrow ends near the first live one it meets. Without it only the target counts. */
  characters?: () => Iterable<Character>;
  /** The own character: arrows fly through it (CheckCharacterRange skips the Hero). */
  hero?: () => Character | null;
}

interface Projectile {
  spec: ArrowSpec;
  shooter: Character;
  target: Character;
  position: THREE.Vector3;
  /** Degrees, as the desktop's o->Angle. */
  angle: THREE.Vector3;
  /** Direction in the effect's own frame (turned by angle each frame, MoveParticle). */
  direction: THREE.Vector3;
  gravity: number;
  ground: number;
  life: number;
  keys: number;
  model: AnimatedModel | null;
  done: boolean;
  resolve: () => void;
}

const tmpEuler = new THREE.Euler();
const tmpVector = new THREE.Vector3();

export class ProjectileLayer {
  private readonly root: THREE.Object3D;
  private readonly options: ProjectileLayerOptions;
  private readonly flying = new Set<Projectile>();

  constructor(root: THREE.Object3D, options: ProjectileLayerOptions = {}) {
    this.root = root;
    this.options = options;
  }

  /**
   * CreateArrow: one arrow from the shooter along its facing (the desktop turns the shooter
   * to its target when the attack starts and the arrow flies straight, it doesn't home in).
   * Call it ARROW_RELEASE_DELAY_SECONDS after the attack starts. Resolves when the arrow is gone.
   */
  shoot(from: Character, to: Character, kind: ProjectileKind, options: ShootOptions = {}): Promise<void> {
    const spec = ARROWS[kind];
    const facing = from.angle / DEGREES + (options.angleOffsetDegrees ?? 0);
    const angle = new THREE.Vector3(0, spec.randomRoll ? randInt(360) : 0, facing);
    const position = new THREE.Vector3(from.position.x, from.position.y, from.group.position.z);
    if (options.fenrir) position.z += ARROW_FENRIR_LIFT;
    if (spec.start === 'bow') {
      position.add(tmpVector.copy(BOW_START_OFFSET).applyEuler(angleEuler(angle.x, angle.y, angle.z, tmpEuler)));
    } else {
      position.z += TOP_START_LIFT;
      if (spec.start === 'holy') position.add(tmpVector.copy(HOLY_START_OFFSET).applyEuler(angleEuler(angle.x, angle.y, angle.z, tmpEuler)));
    }
    return new Promise<void>((resolve) => {
      const projectile: Projectile = {
        spec, shooter: from, target: to, position, angle,
        direction: new THREE.Vector3(0, -spec.speed, 0),
        gravity: spec.gravity ?? 0,
        ground: from.group.position.z,
        life: spec.lifeFrames, keys: 0, model: null, done: false, resolve,
      };
      this.flying.add(projectile);
      if (spec.file) {
        void loadEffectModel('Skill', spec.file, spec.textureDirs, spec.blendMesh).then((loaded) => {
          if (!loaded || projectile.done) return;
          projectile.model = instantiate(loaded, false);
          projectile.model.group.scale.setScalar(spec.scale);
          this.place(projectile);
          this.root.add(projectile.model.group);
        });
      }
    });
  }

  update(dt: number): void {
    const frames = dt * ANIMATION_FRAMES_PER_SECOND;
    for (const projectile of this.flying) {
      this.move(projectile, frames);
      if (projectile.done) {
        this.flying.delete(projectile);
        if (projectile.model) disposeInstance(projectile.model, false);
        projectile.resolve();
      } else {
        this.place(projectile);
      }
    }
  }

  private move(projectile: Projectile, frames: number): void {
    const { spec } = projectile;
    // The type's move handler: the roll, MoveJump for the bomb, then CheckClientArrow.
    const spin = spec.spin === 'random30to89' ? randInt(60) + 30 : spec.spin;
    projectile.angle.y += spin * frames;
    if (spec.gravity !== null) this.jump(projectile, frames);
    if (spec.stopsAtCharacters && this.meetsCharacter(projectile)) {
      projectile.done = true;
      return;
    }
    // MoveEffect's tail: MoveParticle(o, true), then LifeTime -= 1.
    const { angle } = projectile;
    projectile.position.addScaledVector(tmpVector.copy(projectile.direction).applyEuler(angleEuler(angle.x, angle.y, angle.z, tmpEuler)), frames);
    projectile.keys += spec.keysPerFrame * frames;
    projectile.life -= frames;
    if (projectile.life <= 0) projectile.done = true;
  }

  // MoveJump: Position[2] += Gravity, Gravity -= 1; on the ground it bounces with 0.4 of its speed.
  private jump(projectile: Projectile, frames: number): void {
    projectile.position.z += projectile.gravity * frames;
    projectile.gravity -= frames;
    const ground = this.options.heightAt?.(projectile.position.x, projectile.position.y) ?? projectile.ground;
    if (projectile.position.z < ground) {
      projectile.position.z = ground;
      projectile.gravity = -projectile.gravity * 0.4;
      projectile.direction.y *= 0.4;
    }
  }

  // CheckCharacterRange(o, 100, PKKey): any live, visible, not dead character but the shooter and the Hero.
  private meetsCharacter(projectile: Projectile): boolean {
    const hero = this.options.hero?.() ?? null;
    const candidates = this.options.characters?.() ?? [projectile.target];
    for (const character of candidates) {
      if (character === projectile.shooter || character === hero || character.dead || !character.group.visible || !character.group.parent) continue;
      const dx = projectile.position.x - character.position.x;
      const dy = projectile.position.y - character.position.y;
      if (dx * dx + dy * dy <= ARROW_HIT_RANGE * ARROW_HIT_RANGE) return true;
    }
    return false;
  }

  private place(projectile: Projectile): void {
    const model = projectile.model;
    if (!model) return;
    const { angle } = projectile;
    model.group.position.copy(projectile.position);
    angleEuler(angle.x, angle.y, angle.z, model.group.rotation);
    model.pose(0, projectile.keys);
  }
}

// ---------------------------------------------------------------------------
// Death: the body rots (DeadCharacter), the blood pool (CreateBlood), and the monsters that
// burst when they die (CreateBlood's Ice Monster; the deathEffect monsters of the Swamp of
// Peace and Raklion).
// ---------------------------------------------------------------------------

/** DeadCharacter: c->Rot grows this much per frame from the kill packet (c->Dead = 1). */
export const DEATH_ROT_PER_FRAME = 0.02;
/** ... 0.05 for Bali in Atlans (map 7) ... */
export const DEATH_ROT_PER_FRAME_BALI_ATLANS = 0.05;
/** ... and 0.01 for Kundun while its death effect is armed (LifeTime >= 100). */
export const DEATH_ROT_PER_FRAME_KUNDUN = 0.01;
/** When Rot reaches 1 the body fades: Alpha = 1 - (Rot - 1); it is gone below 0.01 (Live = false). */
export const DEATH_ROT_TIME = 1;
export const DEATH_GONE_ALPHA = 0.01;
/** While fading the body sinks this much per frame (Position[2] -= 0.4). */
export const DEATH_SINK_PER_FRAME = 0.4;
/** Seconds from the kill packet until a body starts to fade, and how long the fade lasts (normal monsters). */
export const DEATH_FADE_START_SECONDS = DEATH_ROT_TIME / DEATH_ROT_PER_FRAME / ANIMATION_FRAMES_PER_SECOND;
export const DEATH_FADE_SECONDS = (1 - DEATH_GONE_ALPHA) / DEATH_ROT_PER_FRAME / ANIMATION_FRAMES_PER_SECOND;

/** CreateBlood: two pools around the head bone, offset up to 50 each way in the bone's frame. */
export const BLOOD_POOLS = 2;
export const BLOOD_OFFSET = 50;
/** CreatePointer(BITMAP_BLOOD): LifeTime 50 + Rand() % 31 frames; size 0.8-1.2 cells, growing 0.004 per frame; fades over its last 50 frames. */
export const BLOOD_LIFETIME_MIN_FRAMES = 50;
export const BLOOD_LIFETIME_RANDOM_FRAMES = 31;
export const BLOOD_GROWTH_PER_FRAME = 0.004;
export const BLOOD_FADE_FRAMES = 50;
/** MovePointers sets a blood pool's light to (0.1, 0, 0) every frame. */
const BLOOD_LIGHT = new THREE.Color(0.1, 0, 0);
/** Models[MODEL_PLAYER].BoneHead. */
const PLAYER_HEAD_BONE = 20;
// CreateBlood draws no pool for these models.
const NO_BLOOD_MODELS = ['MONSTER_MODEL_GHOST', 'MONSTER_MODEL_ASSASSIN', 'MONSTER_MODEL_ICE_QUEEN'];
const ICE_MONSTER_MODEL = 'MONSTER_MODEL_ICE_MONSTER';
/** CreateBlood: the Ice Monster vanishes (Live = false) into this many MODEL_ICE_SMALL. */
export const ICE_MONSTER_PIECES = 10;
const ARMED_DEATH_EFFECT = 100;
const ATLANS_MAP = 7;
const DECAL_GRID = 4;
const DECAL_LIFT = 1;

// GMSwampOfQuiet::UpdateMonsterEffects: the pieces, by the part's effect name, and the bone each starts at.
const SHADOW_PIECES: Record<string, { bones: Record<string, number>; scale: number; glowMesh: number; glowLight: THREE.Color }> = {
  MONSTER_MODEL_SHADOW_PAWN: {
    bones: { shadowPawnAnkleLeft: 34, shadowPawnAnkleRight: 39, shadowPawnBelt: 0, shadowPawnChest: 4, shadowPawnHelmet: 6, shadowPawnKneeLeft: 33, shadowPawnKneeRight: 38, shadowPawnWristLeft: 13, shadowPawnWristRight: 23 },
    scale: 1.1, glowMesh: 3, glowLight: new THREE.Color(1, 0.2, 0.2),
  },
  MONSTER_MODEL_SHADOW_KNIGHT: {
    bones: { shadowKnightAnkleLeft: 34, shadowKnightAnkleRight: 39, shadowKnightBelt: 0, shadowKnightChest: 4, shadowKnightHelmet: 6, shadowKnightKneeLeft: 33, shadowKnightKneeRight: 38, shadowKnightWristLeft: 13, shadowKnightWristRight: 23 },
    scale: 1.1, glowMesh: 3, glowLight: new THREE.Color(0.5, 0.8, 1),
  },
  MONSTER_MODEL_SHADOW_LOOK: {
    bones: { shadowRookAnkleLeft: 34, shadowRookAnkleRight: 39, shadowRookBelt: 0, shadowRookChest: 4, shadowRookHelmet: 6, shadowRookKneeLeft: 33, shadowRookKneeRight: 38, shadowRookWristLeft: 12, shadowRookWristRight: 23 },
    scale: 1.3, glowMesh: 4, glowLight: new THREE.Color(0.5, 1, 0.5),
  },
};
// GM_Raklion: the Ice Giant and the Dark Giant break into the Ice Giant's parts (only parts 1-4
// have their setup in CreateEffect; 5 and 6 get none, so they aren't drawn here) and ice shards.
const GIANT_MODELS = ['MONSTER_MODEL_ICE_GIANT', 'MONSTER_MODEL_DARK_GIANT'];
const GIANT_PARTS_SLOT = 'MONSTER_MODEL_ICE_GIANT';
const GIANT_PART_BONES: Record<string, number> = { iceGiantPart1: 5, iceGiantPart2: 3, iceGiantPart3: 11, iceGiantPart4: 23 };
const GIANT_PART_SCALE = 1.1;
const GIANT_SHARD_BONES = [4, 7, 10, 22, 39, 44, 12, 24];
const GIANT_SHARDS_PER_BONE = 12;
const GIANT_SHARD_FILES = ['ice_stone00.bmd', 'ice_stone01.bmd', 'ice_stone02.bmd'];
const GIANT_SHARD_LIGHT = new THREE.Color(0.3, 0.6, 1);
/** Shadow monsters: their glowing mesh fades between these keys of the death animation. */
const SHADOW_GLOW_FADE_FROM_KEY = 9;
const SHADOW_GLOW_FADE_KEYS = 3;

export interface DeathEffectsOptions {
  /** Ground height, for blood pools on slopes and falling pieces. Without it the body's ground is used. */
  heightAt?: HeightAt;
  /** The current map number (Bali rots faster in Atlans). */
  map?: () => number;
}

/** What the death effects need of a dead character's type. */
interface DeathLook {
  slot: string | null;
  playerModel: boolean;
  headBone: number;
  bloodDone: boolean;
  deathEffectArmed: boolean;
  parts: ModelPart[];
}

interface FadingMaterial {
  material: THREE.Material;
  opacity: number;
  transparent: boolean;
}

/** A moving piece: its own update returns false when it's gone. */
interface Piece {
  update(frames: number): boolean;
  dispose(): void;
}

interface Dying {
  character: Character;
  monsterNumber: number | null;
  look: DeathLook | null;
  rot: number;
  alpha: number;
  sink: number;
  appliedZ: number | null;
  baseZ: number;
  materials: FadingMaterial[] | null;
  bloodDone: boolean;
  burstDone: boolean;
  gone: boolean;
  shadowGlow: { material: THREE.MeshBasicMaterial; light: THREE.Color } | null;
}

export class DeathEffects {
  private readonly root: THREE.Object3D;
  private readonly options: DeathEffectsOptions;
  private readonly dying = new Map<Character, Dying>();
  private readonly pieces = new Set<Piece>();

  constructor(root: THREE.Object3D, options: DeathEffectsOptions = {}) {
    this.root = root;
    this.options = options;
  }

  /**
   * The kill packet arrived (the desktop's c->Dead = 1). monsterNumber is the monster type,
   * null for players. Call update after the characters' own update every frame: it moves
   * the body down from where Character.update put it.
   */
  died(character: Character, monsterNumber: number | null): void {
    if (this.dying.has(character)) return;
    const record: Dying = {
      character, monsterNumber, look: null, rot: 0, alpha: 1, sink: 0, appliedZ: null, baseZ: 0,
      materials: null, bloodDone: false, burstDone: false, gone: false, shadowGlow: null,
    };
    this.dying.set(character, record);
    void loadCombatEffectsData().then(() => { record.look = this.lookOf(character, monsterNumber); })
      .catch((error: unknown) => console.warn('Monster data for death effects not loaded', (error as Error).message));
  }

  update(dt: number): void {
    const frames = dt * ANIMATION_FRAMES_PER_SECOND;
    for (const record of this.dying.values()) {
      // The server removed it (out of view): nothing more to do.
      if (!record.character.group.parent) {
        this.dying.delete(record.character);
        continue;
      }
      this.rot(record, frames);
      if (record.look) this.animationEffects(record);
    }
    for (const piece of this.pieces) {
      if (!piece.update(frames)) {
        piece.dispose();
        this.pieces.delete(piece);
      }
    }
  }

  private lookOf(character: Character, monsterNumber: number | null): DeathLook {
    const data = effectsData!; // loaded before this is called
    if (monsterNumber === null) {
      return { slot: null, playerModel: true, headBone: PLAYER_HEAD_BONE, bloodDone: false, deathEffectArmed: false, parts: [] };
    }
    const monster = data.monsters.get(monsterNumber);
    const slot = monster?.model ?? data.unknownModel;
    const playerModel = slot === 'player' || character.kind === 'player';
    const model = playerModel ? undefined : data.models.get(slot);
    return {
      slot: playerModel ? null : slot,
      playerModel,
      headBone: playerModel ? PLAYER_HEAD_BONE : model?.headBone ?? -1,
      bloodDone: monster?.noBloodPool ?? false,
      deathEffectArmed: monster?.deathEffect === ARMED_DEATH_EFFECT,
      parts: data.models.get(GIANT_MODELS.includes(slot) ? GIANT_PARTS_SLOT : slot)?.parts ?? [],
    };
  }

  // DeadCharacter: Rot grows from the kill; past 1 the body fades and sinks, then is gone.
  private rot(record: Dying, frames: number): void {
    if (record.gone) return;
    const slot = record.look?.slot ?? null;
    let perFrame = DEATH_ROT_PER_FRAME;
    if (slot === 'MONSTER_MODEL_BALI' && this.options.map?.() === ATLANS_MAP) perFrame = DEATH_ROT_PER_FRAME_BALI_ATLANS;
    else if (slot === 'MONSTER_MODEL_ILLUSION_OF_KUNDUN') perFrame = record.look?.deathEffectArmed ? DEATH_ROT_PER_FRAME_KUNDUN : DEATH_ROT_PER_FRAME;
    record.rot += perFrame * frames;
    if (record.rot >= DEATH_ROT_TIME) {
      // MODEL_DREADFEAR keeps its alpha: it sinks on until the server removes it.
      if (slot !== 'MONSTER_MODEL_DREADFEAR') record.alpha = 1 - (record.rot - DEATH_ROT_TIME);
      if (record.alpha >= DEATH_GONE_ALPHA) {
        record.sink += DEATH_SINK_PER_FRAME * frames;
      } else {
        this.vanish(record);
        return;
      }
      this.applyAlpha(record);
    }
    this.applySink(record);
  }

  private vanish(record: Dying): void {
    record.gone = true;
    record.character.group.visible = false;
  }

  // o->Alpha on the whole body, weapons and glows included.
  private applyAlpha(record: Dying): void {
    if (!record.materials) {
      const materials: FadingMaterial[] = [];
      const seen = new Set<THREE.Material>();
      record.character.group.traverse((object) => {
        if (!(object instanceof THREE.Mesh)) return;
        const list: THREE.Material[] = Array.isArray(object.material) ? object.material : [object.material];
        for (const material of list) {
          if (seen.has(material)) continue;
          seen.add(material);
          materials.push({ material, opacity: material.opacity, transparent: material.transparent });
        }
      });
      record.materials = materials;
    }
    const alpha = Math.max(0, Math.min(1, record.alpha));
    for (const fading of record.materials) {
      fading.material.transparent = true;
      fading.material.opacity = fading.opacity * alpha;
    }
  }

  // Character.update puts the body on the ground every frame; the sink is applied on top.
  private applySink(record: Dying): void {
    const group = record.character.group;
    if (record.appliedZ === null || Math.abs(group.position.z - record.appliedZ) > 1e-6) record.baseZ = group.position.z;
    group.position.z = record.baseZ - record.sink;
    record.appliedZ = group.position.z;
  }

  // What happens as the death animation plays and when it stops.
  private animationEffects(record: Dying): void {
    const { character } = record;
    const look = record.look!; // checked by the caller
    const shot = character.oneShot;
    const dieActions: number[] = look.playerModel ? [PlayerActions.DIE1, PlayerActions.DIE2] : [MonsterActions.DIE];
    if (!shot || !dieActions.includes(shot.action)) return;
    // The deathEffect monsters burst when the death animation starts (CurrentAction == MONSTER01_DIE, LifeTime == 100).
    if (!record.burstDone && look.deathEffectArmed && look.slot) {
      record.burstDone = true;
      this.burst(record, look.slot);
    }
    if (record.shadowGlow) this.shadowGlow(record);
    // The animation stopped on its last key: Player/EtcStopAnimationSetting make the blood.
    const lastKey = Math.max(1, character.model.skeleton.actions[shot.action]?.numKeys ?? 1) - 1;
    if (!record.bloodDone && shot.hold && character.time >= lastKey) {
      record.bloodDone = true;
      if (!look.bloodDone) this.blood(record, look);
    }
  }

  // ---------- CreateBlood ----------

  private blood(record: Dying, look: DeathLook): void {
    const { character } = record;
    if (look.headBone < 0) return;
    if (look.slot === ICE_MONSTER_MODEL) {
      this.iceMonsterBurst(record);
      return;
    }
    if (look.slot && NO_BLOOD_MODELS.includes(look.slot)) return;
    const head = character.model.matrices[look.headBone];
    if (!head) return;
    character.group.updateMatrix();
    for (let i = 0; i < BLOOD_POOLS; i++) {
      const offset = new THREE.Vector3((Math.random() * 2 - 1) * BLOOD_OFFSET, (Math.random() * 2 - 1) * BLOOD_OFFSET, 0);
      offset.applyMatrix4(head).applyMatrix4(character.group.matrix);
      const rotation = Math.random() * 360;
      const scale = 0.8 + Math.random() * 0.4;
      this.addBloodPool(offset.x, offset.y, character.group.position.z, rotation, scale);
    }
  }

  // CreatePointer(BITMAP_BLOOD) drawn by RenderTerrainAlphaBitmap: a square of Scale cells on the
  // ground, turned by -Angle, BITMAP_BLOOD (Effect/blood01.tga) lit (0.1, 0, 0).
  private addBloodPool(x: number, y: number, groundZ: number, rotationDegrees: number, startScale: number): void {
    const material = new THREE.MeshBasicMaterial({ color: BLOOD_LIGHT, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 });
    void loadTexture('Effect', 'blood01.tga').then((texture) => { material.map = texture; material.needsUpdate = true; });
    const vertices = (DECAL_GRID + 1) * (DECAL_GRID + 1);
    const positions = new Float32Array(vertices * 3);
    const uvs = new Float32Array(vertices * 2);
    const indices: number[] = [];
    for (let j = 0; j <= DECAL_GRID; j++) {
      for (let i = 0; i <= DECAL_GRID; i++) {
        const v = j * (DECAL_GRID + 1) + i;
        uvs[v * 2] = i / DECAL_GRID;
        uvs[v * 2 + 1] = j / DECAL_GRID;
        if (i < DECAL_GRID && j < DECAL_GRID) indices.push(v, v + 1, v + DECAL_GRID + 2, v, v + DECAL_GRID + 2, v + DECAL_GRID + 1);
      }
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('uv', new THREE.BufferAttribute(uvs, 2));
    geometry.setIndex(indices);
    const mesh = new THREE.Mesh(geometry, material);
    mesh.frustumCulled = false;
    mesh.renderOrder = 1;
    this.root.add(mesh);
    const cos = Math.cos(-rotationDegrees * DEGREES), sin = Math.sin(-rotationDegrees * DEGREES);
    let life = BLOOD_LIFETIME_MIN_FRAMES + randInt(BLOOD_LIFETIME_RANDOM_FRAMES);
    let scale = startScale;
    const heightAt = this.options.heightAt;
    const lay = (): void => {
      const size = scale * TERRAIN_SCALE;
      for (let j = 0; j <= DECAL_GRID; j++) {
        for (let i = 0; i <= DECAL_GRID; i++) {
          const v = j * (DECAL_GRID + 1) + i;
          const u = (i / DECAL_GRID - 0.5) * size, w = (j / DECAL_GRID - 0.5) * size;
          const px = x + u * cos - w * sin, py = y + u * sin + w * cos;
          positions[v * 3] = px;
          positions[v * 3 + 1] = py;
          positions[v * 3 + 2] = (heightAt ? heightAt(px, py) : groundZ) + DECAL_LIFT;
        }
      }
      geometry.getAttribute('position').needsUpdate = true;
    };
    lay();
    this.pieces.add({
      update: (frames) => {
        life -= frames; // MovePointers
        scale += BLOOD_GROWTH_PER_FRAME * frames;
        if (life <= 0) return false;
        material.opacity = life < BLOOD_FADE_FRAMES ? life / BLOOD_FADE_FRAMES : 1;
        lay();
        return true;
      },
      dispose: () => {
        mesh.removeFromParent();
        geometry.dispose();
        material.dispose();
      },
    });
  }

  // CreateBlood's MODEL_ICE_MONSTER: Live = false, and ten MODEL_ICE_SMALL (Skill/Ice02.bmd) fly off.
  private iceMonsterBurst(record: Dying): void {
    this.vanish(record);
    const { character } = record;
    const start = new THREE.Vector3(character.position.x, character.position.y, character.group.position.z);
    for (let i = 0; i < ICE_MONSTER_PIECES; i++) this.addIceSmall(start);
  }

  // CreateEffect(MODEL_ICE_SMALL, SubType 0) and Move_MODEL_ICE_SMALL, then MoveEffect's tail.
  private addIceSmall(origin: THREE.Vector3): void {
    const position = origin.clone();
    position.z += 50;
    let life = randInt(16) + 32;
    const scale = (randInt(4) + 8) * 0.1;
    const angle = new THREE.Vector3(0, 0, randInt(360));
    const direction = new THREE.Vector3(0, (randInt(256) + 64) * 0.1, 0).applyEuler(angleEuler(angle.x, angle.y, angle.z));
    let gravity = randInt(16) + 8;
    const ground = origin.z;
    const piece = this.effectPiece('Skill', 'Ice02.bmd', ['Skill'], 0, false, scale, (model) => {
      // BlendMesh 0 with BlendMeshLight 0.3: the one mesh drawn additively and dim.
      for (const part of model.parts) part.mesh.material.color.setScalar(0.3);
    });
    piece.step = (frames) => {
      position.addScaledVector(direction, frames);
      direction.multiplyScalar(Math.pow(0.9, frames));
      position.z += gravity * frames;
      gravity -= 3 * frames;
      const height = this.options.heightAt?.(position.x, position.y) ?? ground;
      if (position.z < height) {
        position.z = height;
        gravity = -gravity * 0.5;
        life -= 4 * frames;
        angle.x -= scale * 128 * frames;
      } else {
        angle.x -= scale * 32 * frames;
      }
      // The tail's MoveParticle(o, true) moves it once more along its turned direction.
      position.addScaledVector(tmpVector.copy(direction).applyEuler(angleEuler(angle.x, angle.y, angle.z, tmpEuler)), frames);
      life -= frames;
      piece.place(position, angle, 1);
      return life > 0;
    };
  }

  // ---------- the deathEffect bursts ----------

  private burst(record: Dying, slot: string): void {
    const shadow = SHADOW_PIECES[slot];
    if (shadow) {
      for (const part of record.look!.parts) {
        const bone = part.effect ? shadow.bones[part.effect] : undefined;
        if (bone !== undefined) this.addArmorPiece(record.character, bone, part, shadow.scale);
      }
      this.startShadowGlow(record, shadow.glowMesh, shadow.glowLight);
      return;
    }
    if (GIANT_MODELS.includes(slot)) {
      for (const bone of GIANT_SHARD_BONES) {
        const at = this.boneWorldPosition(record.character, bone);
        if (!at) continue;
        for (let j = 0; j < GIANT_SHARDS_PER_BONE; j++) this.addIceShard(at, record.character.group.position.z, GIANT_SHARD_FILES[randInt(GIANT_SHARD_FILES.length)]);
      }
      for (const part of record.look!.parts) {
        const bone = part.effect ? GIANT_PART_BONES[part.effect] : undefined;
        if (bone !== undefined) this.addArmorPiece(record.character, bone, part, GIANT_PART_SCALE);
      }
    }
  }

  // TransformByObjectBone: a bone's place in the world (in the root's units).
  private boneWorldPosition(character: Character, bone: number): THREE.Vector3 | null {
    const matrix = character.model.matrices[bone];
    if (!matrix) return null;
    character.group.updateMatrix();
    return new THREE.Vector3().setFromMatrixPosition(matrix).applyMatrix4(character.group.matrix);
  }

  // CreateEffect(MODEL_SHADOW_*_*, MODEL_ICE_GIANT_PART*) and their case in MoveEffect: thrown
  // up and out, spinning, bouncing on the ground (20 above it) and fading there.
  private addArmorPiece(character: Character, bone: number, part: ModelPart, scale: number): void {
    const start = this.boneWorldPosition(character, bone);
    if (!start) return;
    const position = start;
    let life = 30 + randInt(30);
    const angle = new THREE.Vector3(0, 0, randInt(360));
    const velocity = new THREE.Vector3(0, (randInt(64) + 48) * 0.1, 0).applyEuler(angleEuler(angle.x, angle.y, angle.z));
    velocity.z = 15;
    const gravity = 3.5;
    const turnsForward = randInt(2) === 0;
    const light = 0.5 + randInt(6) * 0.1;
    let alpha = 1;
    const ground = character.group.position.z;
    const { dir, file } = splitDataPath(part.file);
    const piece = this.effectPiece(dir, file, part.textureFolders ?? [dir], -1, true, scale, (model) => {
      for (const meshPart of model.parts) meshPart.mesh.material.color.setScalar(light);
    });
    piece.step = (frames) => {
      velocity.z -= gravity * frames;
      position.addScaledVector(velocity, frames);
      const height = (this.options.heightAt?.(position.x, position.y) ?? ground) + 20;
      if (position.z <= height) {
        position.z = height;
        velocity.x *= Math.pow(0.5, frames);
        velocity.y *= Math.pow(0.5, frames);
        velocity.z += 0.6 * life * frames;
        if (velocity.z < 5) velocity.z = 0;
        alpha -= 0.05 * frames;
      } else {
        const turn = (turnsForward ? 0.15 : -0.15) * life * frames;
        angle.x += turn;
        angle.y += turn;
      }
      life -= frames;
      piece.place(position, angle, alpha);
      return life > 0;
    };
  }

  // CreateEffect(MODEL_EFFECT_BROKEN_ICE0..2, SubType 0) and its move case.
  private addIceShard(at: THREE.Vector3, ground: number, file: string): void {
    const position = at.clone();
    let life = randInt(16) + 35;
    const scale = (randInt(13) + 3) * 0.2;
    const gravity = randInt(3) + 3;
    const angle = new THREE.Vector3(0, 0, randInt(360));
    const velocity = new THREE.Vector3(0, (randInt(128) + 64) * 0.1, 0).applyEuler(angleEuler(angle.x, angle.y, angle.z));
    velocity.z += 25;
    let alpha = 1;
    const piece = this.effectPiece('Effect', file, ['Effect'], -1, true, scale, (model) => {
      for (const part of model.parts) part.mesh.material.color.copy(GIANT_SHARD_LIGHT);
    });
    piece.step = (frames) => {
      velocity.z -= gravity * frames;
      position.addScaledVector(velocity, frames);
      angle.x += 0.5 * life * frames;
      angle.y += 0.5 * life * frames;
      const height = this.options.heightAt?.(position.x, position.y) ?? ground;
      if (position.z <= height) {
        position.z = height;
        velocity.x *= Math.pow(0.6, frames);
        velocity.y *= Math.pow(0.6, frames);
        velocity.z += life * frames;
        if (velocity.z < 0.5) velocity.z = 0;
        alpha -= 0.1 * frames;
      }
      life -= frames;
      piece.place(position, angle, alpha);
      return life > 0;
    };
  }

  // GMSwampOfQuiet::RenderObject during MONSTER01_DIE: only the glowing mesh is drawn, bright in
  // the monster's colour, fading out from key 9 to key 12.
  private startShadowGlow(record: Dying, glowMesh: number, light: THREE.Color): void {
    const parts = record.character.model.parts;
    for (const glow of record.character.model.glows) for (const mesh of glow.meshes) mesh.visible = false;
    let glowMaterial: THREE.MeshBasicMaterial | null = null;
    for (const part of parts) {
      if (part.meshIndex !== glowMesh) {
        part.mesh.visible = false;
        continue;
      }
      glowMaterial = part.mesh.material.clone();
      Object.assign(glowMaterial, { blending: THREE.AdditiveBlending, transparent: true, depthWrite: false });
      part.mesh.material = glowMaterial;
    }
    record.materials = null; // collect the new material if it fades later
    if (glowMaterial) record.shadowGlow = { material: glowMaterial, light: light.clone() };
  }

  private shadowGlow(record: Dying): void {
    const glow = record.shadowGlow!; // checked by the caller
    const key = record.character.time;
    const luminosity = key > SHADOW_GLOW_FADE_FROM_KEY ? Math.max(0, (SHADOW_GLOW_FADE_FROM_KEY + SHADOW_GLOW_FADE_KEYS - key) / SHADOW_GLOW_FADE_KEYS) : 1;
    glow.material.color.copy(glow.light).multiplyScalar(luminosity);
  }

  // A model effect that moves by its step function; drawn once its model is loaded.
  private effectPiece(dir: string, file: string, textureDirs: string[], blendMesh: number, ownMaterials: boolean, scale: number, setup: (model: AnimatedModel) => void): EffectPiece {
    const piece = new EffectPiece(ownMaterials);
    void loadEffectModel(dir, file, textureDirs, blendMesh).then((loaded) => {
      if (!loaded || piece.finished) return;
      const model = instantiate(loaded, true);
      model.group.scale.setScalar(scale);
      setup(model);
      piece.attach(model);
      this.root.add(model.group);
    });
    this.pieces.add(piece);
    return piece;
  }
}

class EffectPiece implements Piece {
  step: (frames: number) => boolean = () => false;
  finished = false;
  private model: AnimatedModel | null = null;
  private readonly position = new THREE.Vector3();
  private readonly angle = new THREE.Vector3();
  private alpha = 1;
  private readonly ownMaterials: boolean;

  constructor(ownMaterials: boolean) {
    this.ownMaterials = ownMaterials;
  }

  attach(model: AnimatedModel): void {
    this.model = model;
    this.draw();
  }

  place(position: THREE.Vector3, angle: THREE.Vector3, alpha: number): void {
    this.position.copy(position);
    this.angle.copy(angle);
    this.alpha = alpha;
    this.draw();
  }

  update(frames: number): boolean {
    return this.step(frames);
  }

  dispose(): void {
    this.finished = true;
    if (this.model) disposeInstance(this.model, this.ownMaterials);
  }

  private draw(): void {
    const model = this.model;
    if (!model) return;
    model.group.position.copy(this.position);
    angleEuler(this.angle.x, this.angle.y, this.angle.z, model.group.rotation);
    const alpha = Math.max(0, Math.min(1, this.alpha));
    model.group.visible = alpha > 0;
    if (alpha < 1) {
      for (const part of model.parts) {
        part.mesh.material.transparent = true;
        part.mesh.material.opacity = alpha;
      }
    }
  }
}
