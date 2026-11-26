// Item definitions and their 3D models, from the client's data (Data/Items, see
// docs/item-data.md): an item is a group (0-15) and a number, and its model entry
// names the .bmd file and the folders of its textures.

import { loadItemLooks } from './itemLooks.js';
import { currentLanguage } from '../i18n.js';
import { currentSeason, onSeasonChange, type SeasonKey } from '../dataRoot.js';

const GROUP_FILES = ['Group00_Sword', 'Group01_Axe', 'Group02_Mace', 'Group03_Spear', 'Group04_Bow', 'Group05_Staff',
  'Group06_Shield', 'Group07_Helm', 'Group08_Armor', 'Group09_Pants', 'Group10_Gloves', 'Group11_Boots', 'Group12_Wing',
  'Group13_Helper', 'Group14_Potion', 'Group15_Etc'];

export const ItemGroup = { SWORD: 0, AXE: 1, MACE: 2, SPEAR: 3, BOW: 4, STAFF: 5, SHIELD: 6, HELM: 7, ARMOR: 8, PANTS: 9, GLOVES: 10, BOOTS: 11, WING: 12, HELPER: 13 } as const;
export type ItemGroup = (typeof ItemGroup)[keyof typeof ItemGroup];
export const ARMOR_GROUPS: number[] = [ItemGroup.HELM, ItemGroup.ARMOR, ItemGroup.PANTS, ItemGroup.GLOVES, ItemGroup.BOOTS];
export const WEAPON_GROUPS: number[] = [ItemGroup.SWORD, ItemGroup.AXE, ItemGroup.MACE, ItemGroup.SPEAR, ItemGroup.BOW, ItemGroup.STAFF];

// ---------- data files ----------

/** A text by language code ("en", "de", …). */
export type LocalizedText = Partial<Record<string, string>>;

/** How an item is drawn in an inventory cell (Data/Items/Models "inventory"). */
export interface InventoryDisplay {
  /** Where the model's origin sits in the item's box, 0..1 from the top left. */
  anchor?: [number, number];
  offset?: [number, number];
  /** Degrees around x, y and z, like AngleMatrix. */
  rotation?: [number, number, number];
  scale?: number;
  bodyHeight?: number;
}

/** How an item lies on the ground (Data/Items/Models "ground"). */
export interface GroundDisplay {
  bodyHeight?: number;
  rotation?: [number, number, number];
  scale?: number;
}

/** How an item glows by level and options (Data/Items/Models "glow"). */
export interface ItemGlowData {
  color?: string;
  ancientColor?: string;
  shineColor?: string;
  shineWhite?: boolean;
  /** The glow level by item level, or one level for all. */
  level?: number | number[];
  /** false: no excellent glow. */
  excellent?: boolean;
  excellentMesh?: number;
  excellentMeshWithoutSkin?: number;
  /** Only these meshes glow (all but hiddenMesh without it). */
  meshes?: number[] | number;
  hiddenMesh?: number;
  shineMeshes?: number[] | number;
  shineHiddenMesh?: number;
  glowAgainInWhite?: number;
  shineBelowPlus3?: string;
}

/** The parts of a model entry that name a .bmd file. */
export interface ModelFileData {
  file?: string;
  textureFolders?: string[];
  noneBlendMeshes?: number[];
  inventory?: InventoryDisplay;
  ground?: GroundDisplay;
}

/** A model of SharedModels.json, used by name from several items. */
export interface SharedModelData extends ModelFileData {
  name: string;
}

/** An item's entry in Data/Items/Models/GroupNN_*.json. */
export interface ItemModelData extends ModelFileData {
  number: number;
  /** The name of a shared model, used in place of file. */
  model?: string;
  ground?: GroundDisplay;
  material?: { specular?: number };
  glow?: ItemGlowData;
  renderStyle?: string;
  renderStyleValues?: Record<string, unknown>;
  itemEffect?: string;
  itemEffectValues?: Record<string, unknown>;
  cloth?: boolean;
}

interface SharedModelsFile { formatVersion: number; models: SharedModelData[] }
interface ItemModelsFile { formatVersion: number; group: number; models: ItemModelData[] }
interface ItemDefinitionsFile { formatVersion: number; group: number; items: ItemDefinition[] }

/** An item's definition (Data/Items/GroupNN_*.json); fields with their default value are left out. */
export interface ItemDefinition {
  number: number;
  /** Missing for a number the client uses without an item. */
  name?: LocalizedText;
  width?: number;
  height?: number;
  /** The equipment slot ("mainHand", "helm", …); none when not equippable. */
  slot?: string;
  level?: number;
  durability?: number;
  skill?: number;
  damageMin?: number;
  damageMax?: number;
  attackSpeed?: number;
  defense?: number;
  twoHanded?: boolean;
  weaponKind?: string;
  wingTier?: string;
  requirements?: Partial<Record<string, number>>;
  classRequirements?: Partial<Record<string, number>>;
  options?: string[];
  tags?: string[];
  buyPrice?: number;
  tooltip?: LocalizedText;
  /** The many other fields of the format (docs/item-data.md). */
  [field: string]: unknown;
}

/** An item override served dynamically from the admin panel via /website/items or seasonal override tables. */
export interface WebsiteItemOverride {
  group: number;
  number: number;
  name?: LocalizedText;
  width?: number;
  height?: number;
  level?: number;
  maximumItemLevel?: number;
  durability?: number;
  magicDurability?: number;
  damageMin?: number;
  damageMax?: number;
  attackSpeed?: number;
  defense?: number;
  skill?: number;
  buyPrice?: number;
  tradable?: boolean;
  sellable?: boolean;
  storable?: boolean;
  requirements?: Partial<Record<string, number | null>>;
  classRequirements?: Partial<Record<string, number>>;
  glowColor?: string;
  tooltip?: LocalizedText | null;
  modelSourceGroup?: number | null;
  modelSourceNumber?: number | null;
}

/** What the catalog keeps of an item's model: where its files are and how it glows. */
export interface ItemModelSource {
  dir: string;
  fileName: string;
  textureDir: string[];
  /** Its look: the item effect and render style that say how its meshes are drawn (game/itemLooks.ts). */
  look: { itemEffect?: string; itemEffectValues?: Record<string, unknown>; renderStyle?: string; renderStyleValues?: Record<string, unknown> };
  glow: ItemGlowData;
  inventory: InventoryDisplay | null;
  /** How it lies on the ground. */
  ground: GroundDisplay | null;
}

export interface ItemCatalog {
  /** By "group:number". */
  models: Map<string, ItemModelSource>;
  names: Map<string, string>;
  definitions: Map<string, ItemDefinition>;
}

export interface ItemChoice { group: number; number: number; name: string }

let catalog: ItemCatalog | null = null;
let loadedSeason: SeasonKey | null = null;
/** The load running now and the season it is for: a second caller of the same season waits for it. */
let inFlight: { season: SeasonKey; promise: Promise<ItemCatalog> } | null = null;
/** Counts season changes: a load started before one publishes nothing (its answers may come after the new season's). */
let generation = 0;
onSeasonChange(() => { catalog = null; loadedSeason = null; inFlight = null; generation++; });
/** The part models (Data/Items/Models/PartModels.json): the +15 armour's decorations, by name. */
let partModels = new Map<string, ItemModelSource>();

/** A part model by name ("grade15Head"), once the catalog is loaded. */
export function partModel(name: string): ItemModelSource | null {
  return partModels.get(name) ?? null;
}

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return response.json() as Promise<T>;
}

function splitPath(file: string): { dir: string; fileName: string } {
  const path = file.replace(/^Data\//, '');
  const slash = path.lastIndexOf('/');
  return { dir: path.slice(0, slash), fileName: path.slice(slash + 1) };
}

/**
 * The catalog of the current season. It is built completely (base data, then the Season 9 overrides, then the admin
 * panel's) before it is published, so a caller never sees a half-built one; a load started before a season change
 * publishes nothing.
 */
export function loadItemCatalog(): Promise<ItemCatalog> {
  const season = currentSeason();
  if (catalog && loadedSeason === season) return Promise.resolve(catalog);
  if (inFlight?.season === season) return inFlight.promise;
  const started = generation;
  const promise = buildItemCatalog(season).then(({ built, parts }) => {
    if (started === generation && currentSeason() === season) {
      catalog = built;
      partModels = parts;
      loadedSeason = season;
    }
    return built;
  }).finally(() => {
    if (inFlight?.promise === promise) inFlight = null;
  });
  inFlight = { season, promise };
  return promise;
}

async function buildItemCatalog(season: SeasonKey): Promise<{ built: ItemCatalog; parts: Map<string, ItemModelSource> }> {
  const looks = loadItemLooks();
  const [shared, ...groups] = await Promise.all([
    fetchJson<SharedModelsFile>('/data/Items/Models/SharedModels.json'),
    ...GROUP_FILES.map((name) => fetchJson<ItemModelsFile>(`/data/Items/Models/${name}.json`)),
  ]);
  const items = await Promise.all(GROUP_FILES.map((name) => fetchJson<ItemDefinitionsFile>(`/data/Items/${name}.json`)));
  const parts = await fetchJson<{ partModels?: (ModelFileData & { name: string; renderStyle?: string })[] }>('/data/Items/Models/PartModels.json')
    .catch(() => ({ partModels: [] }));
  const partModelsOfSeason = new Map((parts.partModels ?? []).filter((part) => part.file).map((part) => [part.name, {
    ...splitPath(part.file!), // the filter keeps parts with a file
    textureDir: part.textureFolders?.length ? part.textureFolders : ['Item'],
    look: { renderStyle: part.renderStyle }, glow: {}, inventory: null, ground: null,
  }]));
  const sharedByName = new Map(shared.models.map((model) => [model.name, model]));
  const models = new Map<string, ItemModelSource>();
  const names = new Map<string, string>();
  const definitions = new Map<string, ItemDefinition>();
  groups.forEach((group, groupIndex) => {
    for (const entry of group.models) {
      const model: ModelFileData | undefined = entry.model ? sharedByName.get(entry.model) : entry;
      if (!model?.file) continue;
      models.set(`${groupIndex}:${entry.number}`, {
        ...splitPath(model.file),
        textureDir: model.textureFolders?.length ? model.textureFolders : ['Item'],
        look: { itemEffect: entry.itemEffect, itemEffectValues: entry.itemEffectValues, renderStyle: entry.renderStyle, renderStyleValues: entry.renderStyleValues },
        glow: entry.glow ?? {},
        inventory: entry.inventory ?? model.inventory ?? null,
        ground: entry.ground ?? model.ground ?? null,
      });
    }
  });
  items.forEach((group, groupIndex) => {
    for (const item of group.items) {
      if (item.name?.en) names.set(`${groupIndex}:${item.number}`, item.name.en);
      definitions.set(`${groupIndex}:${item.number}`, item);
    }
  });
  await looks;
  const built: ItemCatalog = { models, names, definitions };
  if (season === 's9') {
    const seasonResponse = await fetch('/data/Items/Season9Overrides.json').catch(() => null);
    if (seasonResponse?.ok) {
      const seasonPayload = await seasonResponse.json().catch(() => null) as { items?: WebsiteItemOverride[] } | null;
      if (Array.isArray(seasonPayload?.items) && seasonPayload.items.length > 0) {
        applyItemOverrides(built, seasonPayload.items);
      }
    }
  }
  // The admin panel's overrides are the same for every season (one game configuration for all worlds): last, on top.
  const overridesResponse = await fetch('/website/items').catch(() => null);
  if (overridesResponse?.ok) {
    const payload = await overridesResponse.json().catch(() => null) as { items?: WebsiteItemOverride[] } | null;
    if (Array.isArray(payload?.items) && payload.items.length > 0) {
      applyItemOverrides(built, payload.items);
    }
  }
  return { built, parts: partModelsOfSeason };
}

/** Applies admin-panel item definition overrides into a loaded item catalog. */
export function applyItemOverrides(targetCatalog: ItemCatalog, overrides: WebsiteItemOverride[]): void {
  for (const entry of overrides) {
    const key = `${entry.group}:${entry.number}`;
    const existing = targetCatalog.definitions.get(key);
    const mergedRequirements: Partial<Record<string, number>> = { ...(existing?.requirements ?? {}) };
    if (entry.requirements) {
      for (const [reqKey, reqVal] of Object.entries(entry.requirements)) {
        if (typeof reqVal === 'number' && reqVal > 0) mergedRequirements[reqKey] = reqVal;
        else delete mergedRequirements[reqKey];
      }
    }
    const mergedName: LocalizedText = { ...(existing?.name ?? {}), ...(entry.name ?? {}) };
    const merged: ItemDefinition = {
      ...(existing ?? { number: entry.number }),
      number: entry.number,
      name: mergedName,
      width: entry.width ?? existing?.width ?? 1,
      height: entry.height ?? existing?.height ?? 1,
      level: entry.level ?? existing?.level ?? 0,
      maximumItemLevel: entry.maximumItemLevel ?? existing?.maximumItemLevel,
      durability: entry.durability ?? existing?.durability,
      magicDurability: entry.magicDurability ?? existing?.magicDurability,
      damageMin: entry.damageMin ?? existing?.damageMin,
      damageMax: entry.damageMax ?? existing?.damageMax,
      attackSpeed: entry.attackSpeed ?? existing?.attackSpeed,
      defense: entry.defense ?? existing?.defense,
      skill: entry.skill ?? existing?.skill,
      buyPrice: entry.buyPrice ?? existing?.buyPrice,
      tradable: entry.tradable ?? existing?.tradable,
      sellable: entry.sellable ?? existing?.sellable,
      storable: entry.storable ?? existing?.storable,
      requirements: Object.keys(mergedRequirements).length > 0 ? mergedRequirements : undefined,
      classRequirements: entry.classRequirements ? { ...(existing?.classRequirements ?? {}), ...entry.classRequirements } : existing?.classRequirements,
      tooltip: entry.tooltip ?? existing?.tooltip,
    };
    targetCatalog.definitions.set(key, merged);
    if (mergedName.en) targetCatalog.names.set(key, mergedName.en);

    let model = targetCatalog.models.get(key);
    if (!model && entry.modelSourceGroup !== undefined && entry.modelSourceGroup !== null
      && entry.modelSourceNumber !== undefined && entry.modelSourceNumber !== null) {
      const source = targetCatalog.models.get(`${entry.modelSourceGroup}:${entry.modelSourceNumber}`);
      if (source) {
        model = {
          ...source,
          glow: { ...source.glow },
          look: { ...source.look },
        };
        targetCatalog.models.set(key, model);
      }
    }
    if (model && entry.glowColor) {
      model.glow = { ...model.glow, color: entry.glowColor };
    }
  }
}

export function itemModel(group: number, number: number): ItemModelSource | null {
  return catalog?.models.get(`${group}:${number}`) ?? null;
}

// The item's definition (Data/Items/GroupNN_*.json): size in cells, slot, requirements, …
export function itemDefinition(group: number, number: number): ItemDefinition | null {
  return catalog?.definitions.get(`${group}:${number}`) ?? null;
}

/** Every item definition with its group (empty until the catalog is loaded). */
export function* allItemDefinitions(): Generator<{ group: number; definition: ItemDefinition }> {
  if (!catalog) return;
  for (const [key, definition] of catalog.definitions) yield { group: Number(key.split(':')[0]), definition };
}

export function localizedItemName(definition: Pick<ItemDefinition, 'name'> | null | undefined, language = currentLanguage()): string {
  const name = definition?.name;
  if (!name) return '';
  return (name[language] || name.en) ?? '';
}

export function itemName(group: number, number: number): string {
  const definition = catalog?.definitions.get(`${group}:${number}`);
  const localized = localizedItemName(definition);
  if (localized) return localized;
  return catalog?.names.get(`${group}:${number}`) ?? `Item ${group},${number}`;
}

// The items of a group that have both a name and a model, for pickers.
// Called once the catalog is loaded (loadItemCatalog).
export function itemsWithModels(group: number): ItemChoice[] {
  const result: ItemChoice[] = [];
  for (const key of catalog!.models.keys()) {
    const [g, number] = key.split(':').map(Number);
    const name = catalog!.names.get(key);
    if (g === group && name !== undefined) result.push({ group, number, name });
  }
  return result.sort((a, b) => a.number - b.number);
}

// ---------- appearance ----------

const NO_ITEM_GROUP = 0xf;
const NO_ITEM_NUMBER = 0xfff;
// the server sends (level - 1) / 2; the client draws the level LevelConvert gives (ZzzCharacter.cpp).
const LEVEL_OF_GLOW_STEP = [0, 3, 5, 7, 9, 11, 13, 15];

/** An item as others see it worn: its glow level and whether it's excellent or ancient. */
export interface AppearanceItem {
  group: number;
  number: number;
  glowLevel?: number;
  excellent?: boolean;
  ancient?: boolean;
  /** The helper's variant (a Fenrir's colour: 0 red, 1 black, 2 blue, 3 gold). */
  variant?: number;
}

export type EquipmentSlotName = 'rightHand' | 'leftHand' | 'helm' | 'armor' | 'pants' | 'gloves' | 'boots' | 'wings' | 'pet';

/** The worn items by slot; null or missing for an empty slot. */
export type EquipmentAppearance = Partial<Record<EquipmentSlotName, AppearanceItem | null>>;

export interface Appearance {
  classNumber: number;
  equipment: EquipmentAppearance;
}

function shinyItem(bytes: ArrayLike<number>, at: number): AppearanceItem | null {
  const group = bytes[at] >> 4;
  const number = ((bytes[at] & 0xf) << 8) | bytes[at + 1];
  if (group === NO_ITEM_GROUP && number === NO_ITEM_NUMBER) return null;
  return { group, number, glowLevel: LEVEL_OF_GLOW_STEP[(bytes[at + 2] >> 4) & 7], excellent: (bytes[at + 2] & 0x08) !== 0, ancient: (bytes[at + 2] & 0x04) !== 0 };
}

function plainItem(bytes: ArrayLike<number>, at: number): AppearanceItem | null {
  const group = bytes[at] >> 4;
  const number = ((bytes[at] & 0xf) << 8) | bytes[at + 1];
  if (group === NO_ITEM_GROUP && number === NO_ITEM_NUMBER) return null;
  return { group, number };
}

// The helper slot: bits 1-3 of the first byte carry the variant (ReadEquipmentExtended's HelperVariant,
// the number masked to MAX_ITEM_INDEX - 1).
const HELPER_NUMBER_MASK = 0x1ff;
function helperItem(bytes: ArrayLike<number>, at: number): AppearanceItem | null {
  const item = plainItem(bytes, at);
  if (!item) return null;
  return { group: item.group, number: item.number & HELPER_NUMBER_MASK, variant: (bytes[at] & 0x0e) >> 1 };
}

// the server's extended appearance (AppearanceSerializerExtended, 27 bytes), which it
// sends to the desktop client in character lists and when players come into view.
export function parseAppearance(bytes: ArrayLike<number> | null | undefined): Appearance {
  if (!bytes || bytes.length < 27) return { classNumber: bytes?.[0] ?? 0, equipment: {} };
  const items = 2;
  return {
    classNumber: bytes[0],
    equipment: {
      rightHand: shinyItem(bytes, items),       // inventory slot 0, drawn in the right hand
      leftHand: shinyItem(bytes, items + 3),    // inventory slot 1
      helm: shinyItem(bytes, items + 6),
      armor: shinyItem(bytes, items + 9),
      pants: shinyItem(bytes, items + 12),
      gloves: shinyItem(bytes, items + 15),
      boots: shinyItem(bytes, items + 18),
      wings: plainItem(bytes, items + 21),
      pet: helperItem(bytes, items + 23),
    },
  };
}
