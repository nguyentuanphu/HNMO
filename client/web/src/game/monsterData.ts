// Monster and NPC looks from the client's data files (Data/Monsters/Monsters.json and
// Models.json, see client/docs/monster-data.md), as the desktop sets a monster up when it
// appears (GameLogic/Monsters/MonsterSetupApply.cpp: Create, ApplySetup, DressCharacter):
// which model it gets (its entry's, a map variant's or a random variant's), its size, its
// equipment, its skin and the rest of the setup fields, and how fast its model's animations play.

import { currentLanguage } from '../i18n.js';
import { filesFolder, worldModelFiles } from '../world.js';
import type { MonsterAttacksData } from './combatLook.js';
import type { AppearanceItem, EquipmentAppearance } from './items.js';

const FRAMES_PER_SECOND = 25; // animation speeds in the data are keys per 25 FPS frame (ADR 0001)
/** The animations by name, in their numbers' order (MonsterAnimations.cpp). */
export const MONSTER_ANIMATION_NAMES = ['stop1', 'stop2', 'walk', 'attack1', 'attack2', 'shock', 'die', 'appear', 'attack3', 'attack4', 'run', 'attack5'] as const;
// OpenMonsterModel's speeds before a model's own: stop 1 and 2, walk, the first two attacks, shock and die;
// the monster range plays the others at 0. The NPC range plays all at 0.25 (MonsterAnimations.cpp).
const MONSTER_RANGE_SPEEDS = [0.25, 0.2, 0.34, 0.33, 0.33, 0.5, 0.55];
const NPC_RANGE_SPEED = 0.25;
export const MonsterAction = { STOP: 0, WALK: 2 };
/** CreateCharacterPointer's size of every character before its setup (o->Scale = 0.9f). */
const DEFAULT_SCALE = 0.9;
/** CreateCharacterPointer's weapon bones without a model entry's own (Weapon[0].LinkBone = 33, [1] = 42). */
const DEFAULT_LINK_BONES: [number, number] = [33, 42];
/** The game holds up to 64 characters of a name (UTF-16 units). */
const MAX_NAME_LENGTH = 64;

// ---------------------------------------------------------------------------
// The data files' shapes (the parts this client reads)
// ---------------------------------------------------------------------------

/** An item a monster wears, or a body that is no item ({"part": "MODEL_BODY_HELM+15"}). */
export interface MonsterItemData {
  group?: number;
  number?: number;
  level?: number;
  excellent?: number;
  part?: string;
  mountLook?: string;
}

export interface MonsterEquipmentData {
  weapons?: (MonsterItemData | null)[];
  helm?: MonsterItemData | null;
  armor?: MonsterItemData | null;
  pants?: MonsterItemData | null;
  gloves?: MonsterItemData | null;
  boots?: MonsterItemData | null;
  wings?: MonsterItemData | null;
  helper?: MonsterItemData | null;
}

/** A row of spawnEffects (and of appear's effects). */
export interface MonsterSpawnEffectData {
  joint?: string;
  effect?: string;
  particle?: string;
  object?: string;
  eyes?: number[];
  subType?: number;
  scale?: number;
  color?: [number, number, number];
  key?: number;
  count?: number;
  offset?: [number, number, number];
  targetOffset?: [number, number, number];
  scaleRandom?: number;
  bone?: number;
  angle?: number;
}

type MapRef = number | { map: number; stage?: number };

/** The setup fields (docs/monster-data.md, "Setup at spawn"); each may also be a number's or a map variant's own. */
export interface MonsterSetupData {
  scale?: number | 'character';
  outline?: number;
  subType?: number;
  lookLevel?: number;
  glowMesh?: number;
  glowLight?: number;
  hiddenMesh?: number;
  shadow?: boolean;
  hideShadow?: boolean;
  alphaTarget?: number;
  light?: boolean;
  pkColor?: string;
  helperPlate?: boolean;
  selectable?: boolean;
  appear?: unknown;
  teleport?: unknown;
  gate?: unknown;
  facing?: number;
  facingStep?: number;
  facingByTile?: unknown;
  class?: string;
  classStep?: number;
  equipment?: MonsterEquipmentData;
  spawnEffects?: MonsterSpawnEffectData[];
  spawnBuffs?: string[];
  spawnSound?: string;
  randomLook?: number[];
  randomStart?: boolean;
  positionOffset?: [number, number];
  keepGroundHeight?: boolean;
  heightOffset?: number;
  height?: number;
  tileOffset?: [number, number];
  boundingBox?: unknown;
  startAction?: number;
  startFrame?: number;
  deathEffect?: number;
  live?: boolean;
  noTurnOnHit?: boolean;
  moveSpeed?: number;
  noBloodPool?: boolean;
  hooks?: unknown[];
  stateVariants?: unknown[];
}

/** The setup field names, as the desktop lists them (GetMonsterSetupFields). */
export const SETUP_FIELDS = [
  'scale', 'outline', 'subType', 'lookLevel', 'glowMesh', 'glowLight', 'hiddenMesh', 'shadow', 'hideShadow', 'alphaTarget',
  'light', 'pkColor', 'helperPlate', 'selectable', 'appear', 'teleport', 'gate', 'facing', 'facingStep', 'facingByTile',
  'class', 'classStep', 'equipment', 'spawnEffects', 'spawnBuffs', 'spawnSound', 'randomLook', 'randomStart',
  'positionOffset', 'keepGroundHeight', 'heightOffset', 'height', 'tileOffset', 'boundingBox', 'startAction', 'startFrame',
  'deathEffect', 'live', 'noTurnOnHit', 'moveSpeed', 'noBloodPool', 'hooks', 'stateVariants',
] as const satisfies readonly (keyof MonsterSetupData)[];

interface MapVariantData extends MonsterSetupData {
  maps: MapRef[];
  model?: string;
  attacks?: MonsterAttacksData;
}

interface RandomVariantData {
  model: string;
  soundSet?: string;
  equipment?: MonsterEquipmentData;
}

type Names = { en?: string; [language: string]: string | undefined };

/** A monster of Monsters.json: its shared values, and its numbers each with their own. */
export interface MonsterEntry extends MonsterSetupData {
  name?: Names | string;
  kind?: string;
  /** A model slot of Models.json, 'player', or another code's model. */
  model?: string;
  soundSet?: string;
  attacks?: MonsterAttacksData;
  mapVariants?: MapVariantData[];
  randomVariants?: RandomVariantData[];
  numbers: { number: number; maps?: number[] }[];
}

/** A number's entry: the shared values with the number's own (MonstersJson.cpp, ReadNumberEntries). */
export type NumberEntry = Omit<MonsterEntry, 'numbers'> & { number: number; maps?: number[] };

interface UnknownLookData { model: string; scale?: number; for?: string[] }

interface MonstersFile {
  unknownMonsters?: UnknownLookData;
  monsters: MonsterEntry[];
}

/** A model loaded with its model (an NPC body part, a map's effect model). */
export interface ModelPartData { slot?: string; effect?: string; file: string; textureFolders?: string[]; maps?: number[] }

export interface ModelEntry {
  slot: string;
  file: string;
  textureFolders?: string[];
  mapTextureFolders?: { maps: MapRef[]; folders: string[] }[];
  /** A texture name that replaces the file's, by mesh. */
  textureNames?: Record<string, string>;
  /** Keys per frame by animation name or number. */
  animations?: Record<string, number>;
  holdLastFrame?: string[];
  headBone?: number;
  linkBones?: number[];
  parts?: ModelPartData[];
}

interface ModelsFile {
  models: ModelEntry[];
}

export interface MonsterCatalog {
  byNumber: Map<number, NumberEntry>;
  modelsBySlot: Map<string, ModelEntry>;
  unknown: UnknownLookData;
}

let catalog: MonsterCatalog | null = null;
let catalogLoading: Promise<MonsterCatalog> | null = null;

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return response.json() as Promise<T>;
}

/** A number's own values over the monster's: each key it has replaces the monster's; null clears one. */
export function numberEntry(monster: MonsterEntry, own: MonsterEntry['numbers'][number] & Record<string, unknown>): NumberEntry {
  const { numbers: _numbers, ...shared } = monster;
  const entry: Record<string, unknown> = { ...shared };
  for (const [key, value] of Object.entries(own)) {
    if (value === null) delete entry[key];
    else entry[key] = value;
  }
  return entry as NumberEntry;
}

/** The web's list of the Season 9 monsters and NPCs the base game lacks, with their models (public/data). */
export interface ExtraMonstersFile {
  monsters: MonsterEntry[];
  models: ModelEntry[];
}

/** The web's own list of the Season 9 monsters and NPCs (their numbers are no Season 6.3 monster's). */
export const SEASON9_MONSTERS_URL = '/data/Monsters/Season9.json';

/**
 * The catalog of Monsters.json and Models.json, with the extra lists' monsters and models after them: an extra number
 * or slot the game's files already have is left out (the game's own data comes first).
 */
export function buildMonsterCatalog(monsters: MonstersFile, models: ModelsFile, extras: ExtraMonstersFile[] = []): MonsterCatalog {
  const modelsBySlot = new Map(models.models.map((model) => [model.slot, model]));
  const byNumber = new Map<number, NumberEntry>();
  for (const monster of monsters.monsters) {
    for (const own of monster.numbers) byNumber.set(own.number, numberEntry(monster, own as MonsterEntry['numbers'][number] & Record<string, unknown>));
  }
  for (const extra of extras) {
    for (const model of extra.models) if (!modelsBySlot.has(model.slot)) modelsBySlot.set(model.slot, model);
    for (const monster of extra.monsters) {
      for (const own of monster.numbers) {
        if (!byNumber.has(own.number)) byNumber.set(own.number, numberEntry(monster, own as MonsterEntry['numbers'][number] & Record<string, unknown>));
      }
    }
  }
  return { byNumber, modelsBySlot, unknown: monsters.unknownMonsters ?? { model: 'MONSTER_MODEL_GHOST', scale: 1 } };
}

export function loadMonsterCatalog(): Promise<MonsterCatalog> {
  if (catalog) return Promise.resolve(catalog);
  if (!catalogLoading) {
    // Without the Season 9 list (an older web folder) those numbers keep the unknown monsters' look.
    const season9 = fetchJson<ExtraMonstersFile>(SEASON9_MONSTERS_URL).catch(() => null);
    catalogLoading = Promise.all([fetchJson<MonstersFile>('/data/Monsters/Monsters.json'), fetchJson<ModelsFile>('/data/Monsters/Models.json'), season9])
      .then(([monsters, models, extra]) => (catalog = buildMonsterCatalog(monsters, models, extra ? [extra] : [])));
    catalogLoading.catch(() => { catalogLoading = null; });
  }
  return catalogLoading;
}

/** The loaded catalog (null before loadMonsterCatalog finished). */
export function monsterCatalog(): MonsterCatalog | null {
  return catalog;
}

// ---------------------------------------------------------------------------
// Names
// ---------------------------------------------------------------------------

/** A name in the UI language, else the English one (an empty translation counts as none), cut to 64 characters. */
export function localName(names: Names | string | undefined, language = currentLanguage()): string {
  if (names === undefined) return '';
  const all: Names = typeof names === 'string' ? { en: names } : names;
  const name = (all[language] || all.en) ?? '';
  return name.slice(0, MAX_NAME_LENGTH);
}

/** A monster's or NPC's name by its number; "Unknown monster {number}" for a number without an entry. */
export function monsterName(number: number): string {
  const entry = catalog?.byNumber.get(number);
  return entry ? localName(entry.name) : `Unknown monster ${number}`;
}

// ---------------------------------------------------------------------------
// Maps
// ---------------------------------------------------------------------------

/** Where the monster appears: the server's map number and its stage (0 when the server sends none). */
export interface MonsterPlace {
  map?: number | null;
  stage?: number;
}

function namesMap(refs: readonly MapRef[], place: MonsterPlace): boolean {
  if (place.map === undefined || place.map === null) return false;
  const stage = place.stage ?? 0;
  return refs.some((ref) => (typeof ref === 'number' ? ref === place.map : ref.map === place.map && (ref.stage === undefined || ref.stage === stage)));
}

function mapVariantOf(entry: NumberEntry, place: MonsterPlace): MapVariantData | null {
  return entry.mapVariants?.find((variant) => namesMap(variant.maps, place)) ?? null;
}

/** The setup on the map: a map variant's fields over the entry's, one by one (GetSetupOnMap). */
function setupOnMap(entry: NumberEntry, place: MonsterPlace): MonsterSetupData {
  const setup: Record<string, unknown> = {};
  for (const field of SETUP_FIELDS) if (entry[field] !== undefined) setup[field] = entry[field];
  const variant = mapVariantOf(entry, place);
  if (variant) for (const field of SETUP_FIELDS) if (variant[field] !== undefined) setup[field] = variant[field];
  return setup as MonsterSetupData;
}

/** The model on the map: the first map variant with a model that names the map, else the entry's (GetModelOnMap). */
function modelOnMap(entry: NumberEntry, place: MonsterPlace): string | undefined {
  return entry.mapVariants?.find((variant) => variant.model !== undefined && namesMap(variant.maps, place))?.model ?? entry.model;
}

function attacksOnMap(entry: NumberEntry, place: MonsterPlace): MonsterAttacksData | null {
  return entry.mapVariants?.find((variant) => variant.attacks !== undefined && namesMap(variant.maps, place))?.attacks ?? entry.attacks ?? null;
}

// ---------------------------------------------------------------------------
// Models that are not in the model list
// ---------------------------------------------------------------------------

/**
 * The models of other areas a monster may name (MonsterModelSlots.cpp OtherAreaModels, ZzzOpenData.cpp): the Kalima
 * gates' HellGate (its one animation at 0.15) and the soccer ball (0.5); and the Kanturu cannon trap, which that map
 * loads (GM_Kanturu_2nd.cpp).
 */
const OTHER_AREA_MODELS: Record<string, ModelEntry> = {
  MODEL_WARCRAFT: { slot: 'MODEL_WARCRAFT', file: 'Data/Skill/HellGate.bmd', textureFolders: ['Skill'], animations: { stop1: 0.15 } },
  // Its texture (wwe.jpg) is in Item: the desktop finds it there once an item loaded it.
  MODEL_BALL: { slot: 'MODEL_BALL', file: 'Data/Skill/Ball01.bmd', textureFolders: ['Skill', 'Item'], animations: { stop1: 0.5 } },
  MODEL_TRAP_CANON: { slot: 'MODEL_TRAP_CANON', file: 'Data/NPC/c_mon.bmd', textureFolders: ['NPC'] },
};
/** "none": no body, no sounds (the Imperial Guardian's traps); a monster slot whose model the map's own code draws (Maya). */
const NO_BODY_MODELS = ['none', 'MONSTER_MODEL_MAYA'];
const MAP_OBJECT_PREFIX = 'mapObject ';

/** A map object's model on the map (the traps): Data/Object{folder}/ with the world's file name of the type. */
function mapObjectModel(slot: string, place: MonsterPlace): ModelEntry | null {
  const type = Number(slot.slice(MAP_OBJECT_PREFIX.length));
  if (!Number.isInteger(type) || place.map === undefined || place.map === null) return null;
  const folder = filesFolder(place.map);
  const file = worldModelFiles(folder).get(type);
  return file ? { slot, file: `Data/Object${folder}/${file}`, textureFolders: [`Object${folder}`] } : null;
}

/** The model entry of a slot a monster names on a map, or null: the model list's, another area's, a map object. */
function modelEntryOf(slot: string, place: MonsterPlace): ModelEntry | null {
  if (slot.startsWith(MAP_OBJECT_PREFIX)) return mapObjectModel(slot, place);
  return catalog?.modelsBySlot.get(slot) ?? OTHER_AREA_MODELS[slot] ?? null;
}

// ---------------------------------------------------------------------------
// Player-shaped monsters
// ---------------------------------------------------------------------------

/** The classes as the items name them, by class index (0 Dark Wizard … 6 Rage Fighter). */
const CLASS_NAMES = ['darkWizard', 'darkKnight', 'fairyElf', 'magicGladiator', 'darkLord', 'summoner', 'rageFighter'];
/** SetCharacterScale's sizes by base class: a first-step skin (Skin 0), and the others. */
const CHARACTER_SCALE = [0.9, 0.9, 0.88, 0.95, 0.92, 0.9, 1.03];
const CHARACTER_SCALE_SKIN = [0.93, 0.93, 0.86, 0.95, 0.92, 0.9, 1.03];

/** The player model's skins a SubType draws instead of the body (ZzzOpenData.cpp OpenSkills, Event.cpp). */
export const PlayerSkin = {
  SKELETON1: 244, SKELETON2: 245, SKELETON3: 246, SKELETON_PCBANG: 247, HALLOWEEN: 248,
  CURSEDTEMPLE_ALLIED: 256, CURSEDTEMPLE_ILLUSION: 257, GM_CHARACTER: 194, XMAS_CHANGE_GIRL: 198,
  XMAS_SANTA: 195, XMAS_SNOWMAN: 196, XMAS_DEER: 197, PANDA: 537, SKELETON_CHANGED: 555,
} as const;

/** The file of each skin and the folder its textures are in. */
const SKIN_FILES: Record<number, { dir: string; fileName: string; textureDir: string }> = {
  [PlayerSkin.SKELETON1]: { dir: 'Skill', fileName: 'Skeleton01.bmd', textureDir: 'Skill' },
  [PlayerSkin.SKELETON2]: { dir: 'Skill', fileName: 'Skeleton02.bmd', textureDir: 'Skill' },
  [PlayerSkin.SKELETON3]: { dir: 'Skill', fileName: 'Skeleton03.bmd', textureDir: 'Skill' },
  [PlayerSkin.SKELETON_PCBANG]: { dir: 'Skill', fileName: 'Skeleton03.bmd', textureDir: 'Skill' },
  [PlayerSkin.HALLOWEEN]: { dir: 'Skill', fileName: 'jack.bmd', textureDir: 'Skill' },
  [PlayerSkin.CURSEDTEMPLE_ALLIED]: { dir: 'Skill', fileName: 'unitedsoldier.bmd', textureDir: 'Skill' },
  [PlayerSkin.CURSEDTEMPLE_ILLUSION]: { dir: 'Skill', fileName: 'illusionist.bmd', textureDir: 'Skill' },
  [PlayerSkin.GM_CHARACTER]: { dir: 'Skill', fileName: 'youngza.bmd', textureDir: 'Skill' },
  [PlayerSkin.XMAS_CHANGE_GIRL]: { dir: 'Skill', fileName: 'santa.bmd', textureDir: 'Skill' },
  [PlayerSkin.PANDA]: { dir: 'Item', fileName: 'panda.bmd', textureDir: 'Item' },
  [PlayerSkin.SKELETON_CHANGED]: { dir: 'Item', fileName: 'trans_skeleton.bmd', textureDir: 'Item' },
};

/** The skins only a player (a transformed one) is drawn with; as a monster or NPC its body parts are drawn instead. */
const PLAYER_ONLY_SKINS: number[] = [
  PlayerSkin.SKELETON_PCBANG, PlayerSkin.HALLOWEEN, PlayerSkin.CURSEDTEMPLE_ALLIED, PlayerSkin.CURSEDTEMPLE_ILLUSION,
  PlayerSkin.PANDA, PlayerSkin.SKELETON_CHANGED, PlayerSkin.XMAS_CHANGE_GIRL, PlayerSkin.GM_CHARACTER,
];

export function isSkeletonSkin(subType: number | undefined): boolean {
  return subType !== undefined && subType >= PlayerSkin.SKELETON1 && subType <= PlayerSkin.SKELETON3;
}

/** The skin model a player-shaped character of this kind is drawn with (RenderCharacter's branches), or null for its body parts. */
export function playerSkinModel(subType: number | undefined, kind: string): { dir: string; fileName: string; textureDir: string } | null {
  if (subType === undefined) return null;
  if (isSkeletonSkin(subType)) return SKIN_FILES[subType];
  if (kind === 'player' && PLAYER_ONLY_SKINS.includes(subType)) return SKIN_FILES[subType] ?? null;
  return null;
}

/** The player bodies by the code's names: MODEL_BODY_HELM + n and so on (MonsterSetup.cpp, PlayerBodyModels.cpp). */
const PLAYER_BODY_PREFIX: Record<string, string> = {
  MODEL_BODY_HELM: 'Helm', MODEL_BODY_ARMOR: 'Armor', MODEL_BODY_PANTS: 'Pant', MODEL_BODY_GLOVES: 'Glove', MODEL_BODY_BOOTS: 'Boot',
};
const MAX_CLASS = 7;

/** A player body part's file for a slot of MODEL_BODY_* + offset: stage * 7 + class (PlayerBodyFileName). */
export function playerBodyFile(part: string): { dir: string; fileName: string; textureDir: string } | null {
  const match = /^(MODEL_BODY_[A-Z]+)(?:\+(\d+))?$/.exec(part);
  const prefix = match ? PLAYER_BODY_PREFIX[match[1]] : undefined;
  if (!match || !prefix) return null;
  const offset = Number(match[2] ?? 0);
  const stage = Math.floor(offset / MAX_CLASS), baseClass = offset % MAX_CLASS;
  const fileName = `${prefix}Class${stage > 0 ? stage + 1 : ''}${String(baseClass + 1).padStart(2, '0')}.bmd`;
  return { dir: 'Player', fileName, textureDir: 'Player' };
}

// ---------------------------------------------------------------------------
// The look a monster appears with
// ---------------------------------------------------------------------------

/** Why a monster gets the unknown monsters' look (D64). */
export type UnknownReason = 'noEntry' | 'noModel' | 'missingFile';

/** A model file to draw: its folder, file and the folders its textures are searched in. */
export interface ModelFile {
  dir: string;
  fileName: string;
  textureDir: string[];
}

/** A body part slot of a character (BodyPart[]). */
export type BodyPartSlot = 'helm' | 'armor' | 'pants' | 'gloves' | 'boots';
export const BODY_PART_SLOTS: readonly BodyPartSlot[] = ['helm', 'armor', 'pants', 'gloves', 'boots'];

/** A body part that is no item: a part model of the monster's own model, or a player body. */
export type BodyPartModels = Partial<Record<BodyPartSlot, ModelFile>>;

export interface MonsterLook {
  number: number;
  /** Where it appears. */
  place: MonsterPlace;
  name: string;
  /** 'monster', 'npc', 'trap' or 'other'. */
  kind: string;
  /** The model slot it is drawn with ('player' for player-shaped), after map and random variants. */
  model: string;
  /** The random variant drawn (an index of randomVariants), or -1 for the entry's look. */
  variant: number;
  /** Set when it has the unknown monsters' look. */
  unknown: UnknownReason | null;
  scale: number;
  attacks: MonsterAttacksData | null;
  /** The setup on this map. */
  setup: MonsterSetupData;
  /** What it wears, as a player's appearance; `pet` is its helper. */
  equipment: EquipmentAppearance;
  /** The bodies of its body part slots that are no items. */
  bodyParts: BodyPartModels;
  /** Its model's file, textures, animations and weapon bones; null for player-shaped monsters and those without a body. */
  body: (ModelFile & {
    /** A texture name that replaces the file's, by mesh. */
    textureNames: Record<number, string>;
    /** Animation speed per animation number (MONSTER_ANIMATION_NAMES order, then numbered ones), in keys per second. */
    keysPerSecond: number[];
    linkBones: [number, number];
    range: 'monster' | 'npc' | 'other';
  }) | null;
  /** A player-shaped monster: its class, class step and skin. */
  player: {
    classIndex: number;
    classStep: number;
    /** The skin drawn instead of the body parts (a skeleton), or null. */
    skin: ModelFile | null;
    /** The render tier the skin is drawn at (the Lorencia skeleton NPC's golden glow), 0 for none. */
    skinLevel: number;
  } | null;
}

function toAppearanceItem(item: MonsterItemData | null | undefined): AppearanceItem | null {
  if (!item || item.part !== undefined || item.group === undefined || item.number === undefined) return null;
  return { group: item.group, number: item.number, glowLevel: item.level ?? 0, excellent: (item.excellent ?? 0) > 0 };
}

function classIndexOf(name: string | undefined): number {
  const index = name === undefined ? -1 : CLASS_NAMES.indexOf(name);
  return index >= 0 ? index : 0; // CreateCharacterPointer: c->Class = CLASS_WIZARD
}

function modelRange(slot: string): 'monster' | 'npc' | 'other' {
  if (slot.startsWith('MONSTER_MODEL_')) return 'monster';
  if (slot.startsWith('MODEL_')) return 'npc';
  return 'other';
}

/** The keys per second of each animation of a model (its own speeds over its range's defaults). */
export function animationSpeeds(model: ModelEntry): number[] {
  const range = modelRange(model.slot);
  const numbered = Object.keys(model.animations ?? {}).map(Number).filter((n) => Number.isInteger(n) && n >= MONSTER_ANIMATION_NAMES.length);
  const count = Math.max(MONSTER_ANIMATION_NAMES.length, ...numbered.map((n) => n + 1));
  const speeds: number[] = [];
  for (let index = 0; index < count; index++) {
    const name = MONSTER_ANIMATION_NAMES[index] ?? String(index);
    const own = model.animations?.[name];
    const fallback = range === 'monster' ? MONSTER_RANGE_SPEEDS[index] ?? 0 : NPC_RANGE_SPEED;
    speeds.push((own ?? fallback) * FRAMES_PER_SECOND);
  }
  return speeds;
}

function fileOf(path: string, textureFolders: string[] | undefined): ModelFile {
  const file = path.replace(/^Data\//, '');
  const slash = file.lastIndexOf('/');
  const dir = slash >= 0 ? file.slice(0, slash) : '';
  return { dir, fileName: file.slice(slash + 1), textureDir: textureFolders?.length ? textureFolders : [dir] };
}

/** The texture folders of a model on a map (mapTextureFolders before textureFolders). */
function textureFoldersOn(model: ModelEntry, place: MonsterPlace): string[] | undefined {
  return model.mapTextureFolders?.find((row) => namesMap(row.maps, place))?.folders ?? model.textureFolders;
}

/** A body part's model: a part of the monster's own model (its `parts` by slot) or a player body. */
function partModel(part: string, model: ModelEntry | undefined): ModelFile | null {
  const own = model?.parts?.find((entry) => entry.slot === part);
  if (own) return fileOf(own.file, own.textureFolders);
  const body = playerBodyFile(part);
  return body ? { dir: body.dir, fileName: body.fileName, textureDir: [body.textureDir] } : null;
}

export interface DescribeOptions extends MonsterPlace {
  /** Random draws (the random variant): 0 <= r < 1. */
  random?: () => number;
  /** Draw it with the unknown monsters' look for this reason (its model's file turned out missing). */
  unknownReason?: UnknownReason;
}

/**
 * How a monster of this number appears on the map (Setup::Create): the look drawn (the entry's or one of its
 * random variants, each as likely), the model on the map, the setup with the map variant's fields, and its body.
 */
export function describeMonster(number: number, options: DescribeOptions = {}): MonsterLook {
  if (!catalog) throw new Error('The monster catalog is not loaded yet (loadMonsterCatalog).');
  const random = options.random ?? Math.random;
  const entry = catalog.byNumber.get(number);
  const unknownLook = catalog.unknown;
  const unknownFor = (reason: UnknownReason) => (unknownLook.for ?? ['noEntry', 'noModel', 'missingFile']).includes(reason);
  let variant = -1;
  if (entry?.randomVariants?.length) {
    const look = Math.floor(random() * (entry.randomVariants.length + 1));
    if (look > 0) variant = look - 1;
  }
  const variantData = variant >= 0 ? entry!.randomVariants![variant] : null; // a variant is drawn only with an entry
  const setup = entry ? setupOnMap(entry, options) : {};
  if (variantData?.equipment) setup.equipment = variantData.equipment;
  let slot = variantData?.model ?? (entry ? modelOnMap(entry, options) : undefined);
  let unknown: UnknownReason | null = options.unknownReason ?? null;
  if (unknown) { /* given */ } else if (!entry) unknown = 'noEntry';
  else if (!slot) unknown = 'noModel';
  else if (slot !== 'player' && !NO_BODY_MODELS.includes(slot) && !slot.startsWith(MAP_OBJECT_PREFIX) && !modelEntryOf(slot, options)) unknown = 'noModel';
  if (unknown && unknownFor(unknown)) slot = unknownLook.model;
  const kind = entry?.kind ?? 'monster';
  const name = entry ? localName(entry.name) : `Unknown monster ${number}`;
  const equipmentData = setup.equipment ?? {};
  const weapons = equipmentData.weapons ?? [];
  const equipment: EquipmentAppearance = {
    rightHand: toAppearanceItem(weapons[0]), leftHand: toAppearanceItem(weapons[1]),
    helm: toAppearanceItem(equipmentData.helm), armor: toAppearanceItem(equipmentData.armor), pants: toAppearanceItem(equipmentData.pants),
    gloves: toAppearanceItem(equipmentData.gloves), boots: toAppearanceItem(equipmentData.boots),
    wings: toAppearanceItem(equipmentData.wings), pet: toAppearanceItem(equipmentData.helper),
  };
  const model = slot && slot !== 'player' ? modelEntryOf(slot, options) ?? undefined : undefined;
  const bodyParts: BodyPartModels = {};
  for (const part of BODY_PART_SLOTS) {
    const item = equipmentData[part];
    if (item?.part !== undefined) {
      const file = partModel(item.part, model);
      if (file) bodyParts[part] = file;
    }
  }
  const look: MonsterLook = {
    number, place: { map: options.map, stage: options.stage }, name, kind, model: slot ?? '', variant, unknown, scale: DEFAULT_SCALE,
    attacks: entry ? attacksOnMap(entry, options) : null, setup, equipment, bodyParts, body: null, player: null,
  };
  if (unknown && unknownFor(unknown)) look.scale = unknownLook.scale ?? 1;
  else if (typeof setup.scale === 'number' && Number.isFinite(setup.scale)) look.scale = setup.scale;
  if (slot === 'player') {
    const classIndex = classIndexOf(setup.class);
    const classStep = setup.classStep ?? 0;
    const skin = playerSkinModel(setup.subType, kind);
    // The Lorencia skeleton NPC draws its skin at its render tier (RenderPartObject with c->Level).
    const lorenciaSkeleton = kind === 'npc' && options.map === 0 && isSkeletonSkin(setup.subType);
    look.player = {
      classIndex, classStep,
      skin: skin ? { dir: skin.dir, fileName: skin.fileName, textureDir: [skin.textureDir] } : null,
      skinLevel: lorenciaSkeleton ? setup.lookLevel ?? 0 : 0,
    };
    // SetCharacterScale: the size a player of its class has.
    if (setup.scale === 'character') look.scale = (classStep === 0 ? CHARACTER_SCALE : CHARACTER_SCALE_SKIN)[classIndex];
  } else if (model) {
    look.body = {
      ...fileOf(model.file, textureFoldersOn(model, options)),
      textureNames: Object.fromEntries(Object.entries(model.textureNames ?? {}).map(([mesh, name]) => [Number(mesh), name])),
      keysPerSecond: animationSpeeds(model),
      linkBones: model.linkBones?.length ? [model.linkBones[0], model.linkBones[1] ?? DEFAULT_LINK_BONES[1]] : [...DEFAULT_LINK_BONES],
      range: modelRange(model.slot),
    };
  }
  return look;
}
