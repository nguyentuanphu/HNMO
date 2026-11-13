// The mix recipes as the desktop client knows them (GameLogic/Items/MixMgr.cpp): Data/Local/mix.bmd (a count of
// recipes for each of the 14 mix types, then 656-byte recipes, each xor'ed with the BUX code), and what the mix
// window works out from the items in its grid: which recipe they make, the success rate, the zen it costs, the
// recipe's ingredients with how far they're met, and the most similar recipe while they make none.

import { fetchBytes } from '../bmd.js';
import type { Item } from './inventory.js';
import { itemDefinition } from './items.js';
import { itemNameText, itemRequiredLevel } from './itemTooltip.js';
import { itemValue } from './itemValue.js';

/** The mix types (_MIX_TYPES): which recipes the window checks the items against. */
export const MixType = {
  GOBLIN_NORMAL: 0, GOBLIN_CHAOSITEM: 1, GOBLIN_ADD380: 2, CASTLE_SENIOR: 3, TRAINER: 4, OSBOURNE: 5, JERRIDON: 6,
  ELPIS: 7, CHAOS_CARD: 8, CHERRYBLOSSOM: 9, EXTRACT_SEED: 10, SEED_SPHERE: 11, ATTACH_SOCKET: 12, DETACH_SOCKET: 13,
} as const;
export type MixType = (typeof MixType)[keyof typeof MixType];
const MIX_TYPE_COUNT = 14;

/** The item storage each mix type's grid is (GetMixInventoryEquipmentIndex, the desktop's STORAGE_TYPE *_MIX). */
export const MIX_STORAGE: readonly number[] = [3, 3, 3, 3, 5, 7, 8, 6, 9, 10, 11, 12, 13, 14];

// The desktop's item type: group * 512 + number.
const MAX_ITEM_INDEX = 512;
const typeOf = (item: Pick<Item, 'group' | 'number'>): number => item.group * MAX_ITEM_INDEX + item.number;
const STAFF = 5 * MAX_ITEM_INDEX, SHIELD = 6 * MAX_ITEM_INDEX, BOOTS = 11 * MAX_ITEM_INDEX, WING = 12 * MAX_ITEM_INDEX;
const HELPER = 13 * MAX_ITEM_INDEX, POTION = 14 * MAX_ITEM_INDEX;
const T = {
  JEWEL_OF_CHAOS: WING + 15, PACKED_BLESS: WING + 30, PACKED_SOUL: WING + 31, JEWEL_OF_BLESS: POTION + 13, JEWEL_OF_SOUL: POTION + 14,
  JEWEL_OF_LIFE: POTION + 16, JEWEL_OF_CREATION: POTION + 22, JEWEL_OF_GUARDIAN: POTION + 31, JEWEL_OF_HARMONY: POTION + 42,
  TALISMAN_OF_LUCK: POTION + 53, TALISMAN_OF_CHAOS_ASSEMBLY: POTION + 96, HORN_OF_FENRIR: HELPER + 37,
  LARGE_HEALING_POTION: POTION + 3, SMALL_COMPLEX_POTION: POTION + 38, MEDIUM_COMPLEX_POTION: POTION + 39,
  WHITE_CHERRY: POTION + 88, RED_CHERRY: POTION + 89, GOLDEN_CHERRY: POTION + 90, LUCKY_COIN: POTION + 100,
  CHAOS_DRAGON_AXE: 2 * MAX_ITEM_INDEX + 6, CHAOS_NATURE_BOW: 4 * MAX_ITEM_INDEX + 6, CHAOS_LIGHTNING_STAFF: STAFF + 7,
  WINGS_OF_SATAN: WING + 2, WINGS_OF_SPIRITS: WING + 3, WINGS_OF_DARKNESS: WING + 6, WING_OF_CURSE: WING + 41, WINGS_OF_DESPAIR: WING + 42,
  SEED_FIRE: WING + 60, SEED_EARTH: WING + 65, SPHERE_MONO: WING + 70, SPHERE_5: WING + 74,
  SEED_SPHERE_FIRE_1: WING + 100, SEED_SPHERE_WATER_1: WING + 101, SEED_SPHERE_LIGHTNING_5: WING + 128, SEED_SPHERE_EARTH_5: WING + 129,
} as const;

// _SPECIAL_ITEM_RECIPE_
const SP = { EXCELLENT: 1, ADD380ITEM: 2, SETITEM: 4, HARMONY: 8, SOCKETITEM: 16 } as const;
// _MIXRATE_OPS
const OP = { NUMBER: 0, ADD: 1, SUB: 2, MUL: 3, DIV: 4, LP: 5, RP: 6, INT: 7, MAXRATE: 32, ITEM: 33, WING: 34, EXCELLENT: 35, EQUIP: 36, SET: 37, LEVEL1: 38, NONJEWELITEM: 39, LUCKOPT: 64 } as const;

/** How a recipe's ingredient is met (_MIX_SOURCE_STATUS). */
export const SourceStatus = { ERROR: 0, NO: 1, PARTIALLY: 2, YES: 3 } as const;
export type SourceStatus = (typeof SourceStatus)[keyof typeof SourceStatus];

/** A recipe's ingredient (MIX_RECIPE_ITEM). */
export interface RecipeItem {
  typeMin: number; typeMax: number; levelMin: number; levelMax: number; optionMin: number; optionMax: number;
  durabilityMin: number; durabilityMax: number; countMin: number; countMax: number; special: number;
}

/** A recipe of mix.bmd (MIX_RECIPE); the option letters are the file's ('A', 'B', …). */
export interface Recipe {
  index: number; id: number; names: number[]; descriptions: number[]; advice: number[];
  width: number; height: number; requiredLevel: number; zenType: string; zen: number;
  rate: { op: number; value: number }[]; successRate: number; mixOption: string; charmOption: string; chaosCharmOption: string;
  sources: RecipeItem[];
}

// The text numbers mix.bmd stores in its recipes' names, descriptions and advice (kMixRecipeTexts).
const RECIPE_TEXTS: Record<number, string> = {
  588: 'in case you fail on', 589: 'Please note that', 590: 'the level of items decreases',
  600: 'Item levels must be identical to combine. items are of the same Level', 603: 'create a ticket of Devil Square',
  604: 'Create +10 item', 605: 'Create +11 item', 606: 'During creation of +10 ~ +15 items,', 607: 'notice that there is a possibility',
  608: 'that you may lose the items.', 610: 'Notice that when you fail to combine the items', 611: 'you can lose the items',
  612: 'Create Dinorant', 613: 'Create Fruit', 614: 'Create Wings', 615: 'Create cloak of Invisibility', 665: '+12 Item creation',
  666: '+13 Item creation', 736: 'Chaos Weapon Combination', 1172: 'Create cloak item', 1187: 'Dark horse', 1212: 'Resurrection',
  1214: 'Dark Raven', 1412: 'Create', 1413: 'Potion of bless', 1414: 'Potion of soul', 1415: 'Life Stone', 1550: 'Improve',
  1640: 'Store', 1666: "Condor's feather", 1667: '3rd wing', 1863: '+Destroy', 1864: '+Protect', 1921: 'Fragment of horn',
  1922: 'Broken horn', 1923: "Fenrir's horn", 2038: 'SD potion', 2061: 'Refine', 2062: 'Restore', 2194: 'Add 380 item option',
  2203: 'Refine Gemstone', 2222: 'Item will disappear when failed.', 2261: 'You can achieve special items with combinations.',
  2263: 'Items except Chaos Card', 2265: 'Chaos card combination', 2267: "Can't execute combination.", 2336: 'Equipment item',
  2396: 'Scroll of Blood', 2560: 'Cherry Blossoms branches assembly', 2664: 'Seed Extraction', 2665: 'Seed Sphere Assembly',
  2669: 'Seed sphere application', 2670: 'Seed sphere destruction', 2791: 'Complete Secromicon', 2881: 'Golden Box', 2882: 'Silver Box',
  3086: '+14 Item Creation', 3087: '+15 Item Creation',
};

/** GetMixRecipeText: the text of a number in a recipe, empty for one mix.bmd doesn't use. */
export const recipeText = (id: number): string => RECIPE_TEXTS[id] ?? '';

/** The recipe's name as one line (m_iMixName: one to three texts). */
export function recipeName(recipe: Recipe): string {
  const [a, b, c] = recipe.names;
  if (b === 0) return recipeText(a);
  if (c === 0) return `${recipeText(a)} ${recipeText(b)}`;
  return `${recipeText(a)} ${recipeText(b)} ${recipeText(c)}`;
}

const BUX = [0xfc, 0xcf, 0xab];
const RECIPE_SIZE = 656;
let recipes: Recipe[][] | null = null;
let loading: Promise<Recipe[][]> | null = null;

function unbux(bytes: Uint8Array, at: number, length: number): DataView {
  const out = new Uint8Array(length);
  for (let i = 0; i < length; i++) out[i] = bytes[at + i] ^ BUX[i % 3];
  return new DataView(out.buffer);
}

function parseRecipe(v: DataView): Recipe {
  const i32 = (at: number) => v.getInt32(at, true);
  const letter = (at: number) => String.fromCharCode(v.getUint8(at));
  const rateCount = Math.min(Math.max(i32(64), 0), 32);
  const sourceCount = Math.min(Math.max(i32(652), 0), 8);
  return {
    index: i32(0), id: i32(4), names: [i32(8), i32(12), i32(16)], descriptions: [i32(20), i32(24), i32(28)], advice: [i32(32), i32(36), i32(40)],
    width: i32(44), height: i32(48), requiredLevel: i32(52), zenType: letter(56), zen: v.getUint32(60, true),
    rate: Array.from({ length: rateCount }, (_, k) => ({ op: i32(68 + 8 * k), value: v.getFloat32(72 + 8 * k, true) })),
    successRate: i32(324), mixOption: letter(328), charmOption: letter(329), chaosCharmOption: letter(330),
    sources: Array.from({ length: sourceCount }, (_, k) => {
      const at = 332 + 40 * k;
      return {
        typeMin: v.getInt16(at, true), typeMax: v.getInt16(at + 2, true), levelMin: i32(at + 4), levelMax: i32(at + 8),
        optionMin: i32(at + 12), optionMax: i32(at + 16), durabilityMin: i32(at + 20), durabilityMax: i32(at + 24),
        countMin: i32(at + 28), countMax: i32(at + 32), special: v.getUint32(at + 36, true),
      };
    }),
  };
}

/** CMixRecipeMgr::OpenRecipeFile: the recipes of each mix type. */
export function loadMixRecipes(): Promise<Recipe[][]> {
  if (recipes) return Promise.resolve(recipes);
  loading ??= fetchBytes('/data/Local/mix.bmd').then((bytes) => {
    const header = unbux(bytes, 0, 4 * MIX_TYPE_COUNT);
    const list: Recipe[][] = [];
    let at = 4 * MIX_TYPE_COUNT;
    for (let type = 0; type < MIX_TYPE_COUNT; type++) {
      const count = header.getInt32(type * 4, true);
      if (count < 0 || count > 1000) throw new Error('mix.bmd - Version not matched.');
      const ofType: Recipe[] = [];
      for (let i = 0; i < count && at + RECIPE_SIZE <= bytes.length; i++, at += RECIPE_SIZE) ofType.push(parseRecipe(unbux(bytes, at, RECIPE_SIZE)));
      list.push(ofType);
    }
    recipes = list;
    return list;
  });
  return loading;
}

/** The recipes once loaded (null before). */
export const loadedMixRecipes = (): Recipe[][] | null => recipes;

// ---------- CMixItem ----------

/** An item of the mix grid as the recipes see it (CMixItem); the same items are counted together. */
export interface MixItem {
  type: number; level: number; option: number; durability: number; special: number; count: number; testCount: number;
  mixLuck: boolean; isEquipment: boolean; canStack: boolean; isWing: boolean; isUpgradedWing: boolean; is3rdUpgradedWing: boolean;
  mixValue: number; isCharm: boolean; isChaosCharm: boolean; isJewel: boolean; added380: boolean; fenrirAdded: boolean;
  harmony: number; socketCount: number; sockets: number[];
}

const STACKING = new Set<number>([T.LARGE_HEALING_POTION, T.SMALL_COMPLEX_POTION, T.MEDIUM_COMPLEX_POTION, T.TALISMAN_OF_LUCK,
  T.WHITE_CHERRY, T.RED_CHERRY, T.GOLDEN_CHERRY, T.LUCKY_COIN]);
const JEWELS = new Set<number>([T.JEWEL_OF_CHAOS, T.PACKED_BLESS, T.PACKED_SOUL, T.JEWEL_OF_BLESS, T.JEWEL_OF_SOUL, T.JEWEL_OF_LIFE,
  T.JEWEL_OF_CREATION, T.JEWEL_OF_GUARDIAN, T.JEWEL_OF_HARMONY]);

function requiredLevelOf(item: Item): number {
  try { return itemRequiredLevel(item); } catch { return 0; }
}

/** CMixItem::SetItem. */
export function mixItemOf(item: Item, mixValue: number): MixItem {
  const type = typeOf(item);
  const definition = itemDefinition(item.group, item.number);
  let special = 0;
  if (item.excellent > 0) special |= SP.EXCELLENT;
  if (requiredLevelOf(item) >= 380) special |= SP.ADD380ITEM;
  if (item.ancientSet > 0) special |= SP.SETITEM;
  const socketCount = item.sockets.length;
  if (socketCount > 0) { special |= SP.SOCKETITEM; special ^= SP.ADD380ITEM; }
  if (item.harmony > 0) special |= SP.HARMONY;
  const tier = definition?.wingTier;
  const isWing = tier === 'first', isUpgradedWing = tier === 'second', is3rdUpgradedWing = tier === 'third';
  if ((isWing || isUpgradedWing || is3rdUpgradedWing) && (special & SP.EXCELLENT)) special ^= SP.EXCELLENT;
  const canStack = STACKING.has(type);
  return {
    type, level: item.level,
    // The extra option's value (SpecialValue: four per level; life regeneration counts four times its percent).
    option: item.optionLevel * 4,
    durability: item.durability, special, count: canStack ? item.durability : 1, testCount: 0,
    mixLuck: item.luck, isEquipment: type >= 0 && type <= BOOTS + MAX_ITEM_INDEX - 1, canStack,
    isWing, isUpgradedWing, is3rdUpgradedWing, mixValue,
    isCharm: type === T.TALISMAN_OF_LUCK, isChaosCharm: type === T.TALISMAN_OF_CHAOS_ASSEMBLY, isJewel: JEWELS.has(type),
    added380: item.guardian, fenrirAdded: type === T.HORN_OF_FENRIR && item.excellent !== 0,
    harmony: item.harmony, socketCount, sockets: [...item.sockets],
  };
}

// CMixItemInventory::EvaluateMixItemValue.
function mixValueOf(item: Item): number {
  switch (typeOf(item)) {
    case T.JEWEL_OF_CHAOS: return 40000;
    case T.JEWEL_OF_BLESS: return 100000;
    case T.JEWEL_OF_SOUL: return 70000;
    case T.JEWEL_OF_CREATION: return 450000;
    case T.JEWEL_OF_LIFE: return 0;
    default:
      try { return itemValue(item, 0); } catch { return 0; }
  }
}

const sameItem = (a: MixItem, b: MixItem): boolean => a.type === b.type && a.level === b.level
  && (a.canStack || a.durability === b.durability) && a.option === b.option && a.special === b.special;

/** CMixItemInventory::AddItem for every item of the grid: the same items counted together. */
export function mixItemsOf(items: Iterable<Item>): MixItem[] {
  const result: MixItem[] = [];
  for (const item of items) {
    const value = mixValueOf(item);
    const mixItem = mixItemOf(item, value);
    const same = result.find((m) => sameItem(m, mixItem));
    if (same) {
      same.mixValue += value;
      same.count += same.canStack ? item.durability : 1;
    } else result.push(mixItem);
  }
  return result;
}

const SEED_SPHERE_TAG = 'socketSeedSphere';
const hasTag = (type: number, tag: string): boolean =>
  (itemDefinition(Math.floor(type / MAX_ITEM_INDEX), type % MAX_ITEM_INDEX)?.tags ?? []).includes(tag);

// A source of the 380 refining stone (IsSourceOfRefiningStone): not the low items below +4.
const LOW_ITEMS = new Set<number>([
  1, 2, 3, 5, // kris, short sword, rapier, sword of assassin
  MAX_ITEM_INDEX * 1 + 0, MAX_ITEM_INDEX * 1 + 1, MAX_ITEM_INDEX * 1 + 2, // small, hand, double axe
  MAX_ITEM_INDEX * 2 + 0, MAX_ITEM_INDEX * 2 + 1, MAX_ITEM_INDEX * 2 + 2, // mace, morning star, flail
  MAX_ITEM_INDEX * 3 + 1, MAX_ITEM_INDEX * 3 + 2, MAX_ITEM_INDEX * 3 + 3, MAX_ITEM_INDEX * 3 + 5, MAX_ITEM_INDEX * 3 + 6,
  MAX_ITEM_INDEX * 4 + 0, MAX_ITEM_INDEX * 4 + 1, MAX_ITEM_INDEX * 4 + 2, MAX_ITEM_INDEX * 4 + 3,
  MAX_ITEM_INDEX * 4 + 8, MAX_ITEM_INDEX * 4 + 9, MAX_ITEM_INDEX * 4 + 10, MAX_ITEM_INDEX * 4 + 11,
  STAFF + 0, STAFF + 1, STAFF + 2,
  SHIELD + 0, SHIELD + 1, SHIELD + 2, SHIELD + 3, SHIELD + 4, SHIELD + 6, SHIELD + 7, SHIELD + 8, SHIELD + 9,
]);
// The armour sets of IsSourceOfRefiningStone: bronze, pad, bone, leather, scale, sphinx, brass, vine, silk, wind.
const LOW_ARMOUR_SETS = [0, 2, 4, 5, 6, 7, 8, 10, 11, 12];

function isSourceOfRefiningStone(item: MixItem): boolean {
  if (item.level >= 4) return true;
  if (LOW_ITEMS.has(item.type)) return false;
  const group = Math.floor(item.type / MAX_ITEM_INDEX), number = item.type % MAX_ITEM_INDEX;
  return !(group >= 7 && group <= 11 && LOW_ARMOUR_SETS.includes(number));
}

const isSeedSphere = (item: MixItem): boolean => hasTag(item.type, SEED_SPHERE_TAG);
const forWeapon = (item: MixItem): boolean => isSeedSphere(item) && (item.type - WING) % 2 === 0;
const forArmor = (item: MixItem): boolean => isSeedSphere(item) && (item.type - WING) % 2 === 1;
const isChaosItem = (item: MixItem): boolean => item.type === T.CHAOS_DRAGON_AXE || item.type === T.CHAOS_NATURE_BOW || item.type === T.CHAOS_LIGHTNING_STAFF;
const isUpgradable = (item: MixItem): boolean => item.isEquipment || item.isWing || item.isUpgradedWing || item.is3rdUpgradedWing;
const isOptionItem = (source: RecipeItem): boolean => source.countMin === 0;

function checkItem(r: RecipeItem, s: MixItem): boolean {
  return r.typeMin <= s.type && r.typeMax >= s.type && r.levelMin <= s.level && r.levelMax >= s.level
    && r.durabilityMin <= s.durability && r.durabilityMax >= s.durability && r.optionMin <= s.option && r.optionMax >= s.option
    && [SP.EXCELLENT, SP.ADD380ITEM, SP.SETITEM, SP.HARMONY, SP.SOCKETITEM].every((bit) => (r.special & bit) <= (s.special & bit));
}

/** The texts the source lines put together (I18N::Game). */
const NAMES = {
  rateIncrease: 'Rate increase', level: '+%d', levelAtMost: '+%d or lower', levelAtLeast: '+%d or higher', levelRange: '+%d~%d',
  option: 'option +%d', optionAtMost: 'option +%d or lower', optionAtLeast: 'option +%d or higher', optionRange: 'option +%d~%d',
  count: 'x%d', countAtMost: 'x%d or fewer', countAtLeast: 'x%d or more', countRange: 'x%d~%d',
};

/** printf's %d / %s, in order. */
export function format(text: string, ...values: (string | number)[]): string {
  let i = 0;
  return text.replace(/%%|%d|%s/g, (m) => (m === '%%' ? '%' : String(values[i++] ?? '')));
}

function itemNameAt(type: number, level: number): string {
  const item: Item = {
    group: Math.floor(type / MAX_ITEM_INDEX), number: type % MAX_ITEM_INDEX, level, durability: 0, luck: false, skill: false, guardian: false,
    optionLevel: 0, optionType: 0, excellent: 0, ancientSet: 0, ancientBonus: 0, harmony: 0, sockets: [], socketBonus: 0, length: 0, width: 1, height: 1,
  };
  try { return itemNameText(item); } catch { return ''; }
}

/** CMixRecipes for one mix type: the recipe check of the items in the grid. */
export class MixRecipes {
  readonly recipes: Recipe[];
  curIndex = 0;
  mostSimilarIndex = 0;
  successRate = 0;
  requiredZen = 0;
  private findLuck = false;
  private totalValue = 0;
  private excellentValue = 0;
  private equipmentValue = 0;
  private wingValue = 0;
  private setValue = 0;
  private nonJewelValue = 0;
  firstItemLevel = 0;
  firstItemType = 0;
  firstItemSocketCount = 0;
  firstItemSockets: number[] = [];
  totalCharmBonus = 0;
  totalChaosCharmCount = 0;
  private mixSourceTest: number[] = [];
  private mostSimilarSourceTest: number[] = [];
  private rateAt = 0;
  private rates: { op: number; value: number }[] = [];

  constructor(recipes: Recipe[]) { this.recipes = recipes; }

  get current(): Recipe | null { return this.curIndex === 0 ? null : this.recipes[this.curIndex - 1]; }
  get mostSimilar(): Recipe | null { return this.mostSimilarIndex === 0 ? null : this.recipes[this.mostSimilarIndex - 1]; }
  get readyToMix(): boolean { return this.curIndex > 0; }
  get currentMixId(): number { return this.current?.id ?? 0; }

  clear(): void {
    this.curIndex = 0; this.mostSimilarIndex = 0; this.successRate = 0; this.requiredZen = 0; this.findLuck = false;
    this.totalValue = 0; this.excellentValue = 0; this.equipmentValue = 0; this.wingValue = 0; this.setValue = 0;
    this.firstItemLevel = 0; this.firstItemType = 0; this.nonJewelValue = 0; this.firstItemSocketCount = 0; this.firstItemSockets = [];
    this.totalCharmBonus = 0;
  }

  /** IsMixSource: whether an item can go into the grid (with the grid's items as they are). */
  isMixSource(item: Item, mixType: MixType, gridItems: MixItem[]): boolean {
    this.calcCharmBonus(gridItems);
    this.calcChaosCharmCount(gridItems);
    const mixItem = mixItemOf(item, 0);
    const definition = itemDefinition(item.group, item.number);
    if ((definition?.tags ?? []).includes('luckyItem') && mixType !== MixType.JERRIDON) return false;
    const current = this.current;
    if (mixItem.isCharm && (current === null || current.charmOption === 'A') && this.totalCharmBonus + mixItem.count <= 10) return true;
    if (mixItem.isChaosCharm && (current === null || current.charmOption === 'A') && this.totalChaosCharmCount < 1) return true;
    for (const recipe of this.recipes) {
      for (const source of recipe.sources) {
        if (this.totalCharmBonus > 0 && recipe.charmOption !== 'A') continue;
        if (checkItem(source, mixItem)
          && !(recipe.mixOption === 'B' && isChaosItem(mixItem))
          && !(recipe.mixOption === 'C' && mixItem.added380)
          && !(recipe.mixOption === 'D' && mixItem.fenrirAdded)
          && !(recipe.mixOption === 'E' && !isUpgradable(mixItem))
          && !(recipe.mixOption === 'G' && !isSourceOfRefiningStone(mixItem))) return true;
      }
    }
    return false;
  }

  /** CheckRecipe: the first recipe the items make; its mix id, -1 for none. */
  checkRecipe(items: MixItem[]): number {
    this.curIndex = 0;
    for (const recipe of this.recipes) {
      for (const item of items) item.testCount = item.count;
      if (this.checkRecipeSub(recipe, items)) {
        this.curIndex = recipe.index + 1;
        this.evaluate(items);
        this.calcCharmBonus(items);
        this.calcMixRate();
        this.calcRequiredZen(items);
        return recipe.id;
      }
      this.successRate = 0;
      this.requiredZen = 0;
    }
    return -1;
  }

  private checkRecipeSub(recipe: Recipe, items: MixItem[]): boolean {
    let found = false;
    const test = new Array<number>(8).fill(0);
    for (let j = 0; j < recipe.sources.length; j++) {
      const source = recipe.sources[j];
      if (!isOptionItem(source)) found = false;
      for (const item of items) {
        if (checkItem(source, item) && item.testCount > 0 && source.countMax >= test[j] + item.testCount
          && !(recipe.mixOption === 'H' && forArmor(item)) && !(recipe.mixOption === 'I' && forWeapon(item))) {
          if (item.testCount >= source.countMax) { test[j] += source.countMax; item.testCount -= source.countMax; }
          else { test[j] += item.testCount; item.testCount = 0; }
          found = true;
          if (j === 0) {
            this.firstItemLevel = item.level;
            this.firstItemType = item.type;
            this.firstItemSocketCount = item.socketCount;
            if (item.socketCount > 0) this.firstItemSockets = [...item.sockets];
          }
        }
      }
      if (!found || source.countMin > test[j]) return false;
    }
    for (const item of items) {
      if (item.testCount > 0 && !(item.isCharm && recipe.charmOption === 'A') && !(item.isChaosCharm && recipe.chaosCharmOption === 'A')) return false;
    }
    return true;
  }

  /** CheckRecipeSimilarity: the recipe the items come closest to. */
  checkSimilarity(items: MixItem[]): number {
    if (items.length === 0 && this.recipes.length === 1) {
      this.mostSimilarIndex = 1;
      const first = this.recipes[0];
      this.mostSimilarSourceTest = first.sources.map(() => first.sources[0].countMax);
      return 1;
    }
    let best = 0, bestPoints = 0;
    this.mostSimilarSourceTest = new Array<number>(8).fill(0);
    for (const recipe of this.recipes) {
      this.mixSourceTest = new Array<number>(8).fill(0);
      recipe.sources.forEach((source, i) => { this.mixSourceTest[i] = source.countMax; });
      for (const item of items) item.testCount = item.count;
      let points = this.similaritySub(recipe, items);
      if (points === 1 && this.recipes.length > 1) points = 0;
      if (points > bestPoints || (points === bestPoints && points > 0 && this.curIndex === recipe.index + 1)) {
        bestPoints = points;
        best = recipe.index + 1;
        this.mostSimilarSourceTest = new Array<number>(8).fill(0);
        recipe.sources.forEach((_, i) => { this.mostSimilarSourceTest[i] = this.mixSourceTest[i]; });
      }
    }
    this.mostSimilarIndex = best;
    return best;
  }

  private similaritySub(recipe: Recipe, items: MixItem[]): number {
    let total = 0;
    recipe.sources.forEach((source, j) => {
      for (const item of items) {
        if (checkItem(source, item) && item.testCount > 0
          && !(recipe.mixOption === 'H' && forArmor(item)) && !(recipe.mixOption === 'I' && forWeapon(item))) {
          let points: number;
          if (item.type === T.JEWEL_OF_CHAOS) points = 1;
          else if (source.countMax < item.testCount) points = 1;
          else if (j === 0) points = 10;
          else if (j === 1) points = 5;
          else points = 3;
          total += points;
          if (item.testCount > 0 && this.mixSourceTest[j] > 0) {
            this.mixSourceTest[j] -= item.testCount;
            item.testCount -= source.countMax;
          }
        }
      }
    });
    for (const item of items) {
      if (item.testCount > 0 && !(item.isCharm && recipe.charmOption === 'A') && !(item.isChaosCharm && recipe.chaosCharmOption === 'A')) return 0;
    }
    return total;
  }

  private evaluate(items: MixItem[]): void {
    this.findLuck = false;
    this.totalValue = 0; this.excellentValue = 0; this.equipmentValue = 0; this.wingValue = 0; this.setValue = 0; this.nonJewelValue = 0;
    for (const item of items) {
      if (item.mixLuck) this.findLuck = true;
      if (item.special & SP.EXCELLENT) this.excellentValue += item.mixValue;
      if (item.isEquipment) this.equipmentValue += item.mixValue;
      if (item.isWing) this.wingValue += item.mixValue;
      if (item.special & SP.SETITEM) this.setValue += item.mixValue;
      if (!item.isJewel) this.nonJewelValue += item.mixValue;
      this.totalValue += item.mixValue;
    }
    // DWORD sums
    this.totalValue >>>= 0; this.excellentValue >>>= 0; this.equipmentValue >>>= 0; this.wingValue >>>= 0; this.setValue >>>= 0; this.nonJewelValue >>>= 0;
  }

  calcCharmBonus(items: MixItem[]): void {
    this.totalCharmBonus = 0;
    for (const item of items) if (item.isCharm) this.totalCharmBonus += item.count;
  }

  calcChaosCharmCount(items: MixItem[]): void {
    this.totalChaosCharmCount = 0;
    for (const item of items) if (item.isChaosCharm) this.totalChaosCharmCount += 1;
  }

  private calcMixRate(): void {
    this.successRate = 0;
    const recipe = this.current;
    if (!recipe) return;
    this.rates = recipe.rate;
    this.rateAt = 0;
    this.successRate = Math.trunc(this.addSub());
    if (this.successRate > recipe.successRate) this.successRate = recipe.successRate;
    if (recipe.charmOption === 'A') this.successRate += this.totalCharmBonus;
    if (this.successRate > 100) this.successRate = 100;
  }

  private atEnd(): boolean { return this.rateAt >= this.rates.length || this.rates[this.rateAt].op === OP.RP; }

  private addSub(): number {
    let value = 0;
    while (!this.atEnd()) {
      const op = this.rates[this.rateAt].op;
      if (op === OP.ADD) { this.rateAt++; value = Math.fround(value + this.mulDiv()); }
      else if (op === OP.SUB) { this.rateAt++; value = Math.fround(value - this.mulDiv()); }
      else value = this.mulDiv();
    }
    return value;
  }

  private mulDiv(): number {
    let value = 0;
    while (!this.atEnd()) {
      const op = this.rates[this.rateAt].op;
      if (op === OP.ADD || op === OP.SUB) return value;
      if (op === OP.MUL) { this.rateAt++; value = Math.fround(value * this.factor()); }
      else if (op === OP.DIV) { this.rateAt++; value = Math.fround(value / this.factor()); }
      else value = this.factor();
    }
    return value;
  }

  private factor(): number {
    let value = 0;
    const token = this.rates[this.rateAt];
    switch (token?.op) {
      case OP.LP: this.rateAt++; value = this.addSub(); break;
      case OP.INT: this.rateAt += 2; value = Math.trunc(this.addSub()); break;
      case OP.NUMBER: value = token.value; break;
      case OP.MAXRATE: value = this.current?.successRate ?? 0; break;
      case OP.ITEM: value = this.totalValue; break;
      case OP.WING: value = this.wingValue; break;
      case OP.EXCELLENT: value = this.excellentValue; break;
      case OP.EQUIP: value = this.equipmentValue; break;
      case OP.SET: value = this.setValue; break;
      case OP.LUCKOPT: value = this.findLuck ? 25 : 0; break;
      case OP.LEVEL1: value = this.firstItemLevel; break;
      case OP.NONJEWELITEM: value = this.nonJewelValue; break;
      default: break;
    }
    this.rateAt++;
    return Math.fround(value);
  }

  private calcRequiredZen(items: MixItem[]): void {
    this.requiredZen = 0;
    const recipe = this.current;
    if (!recipe) return;
    switch (recipe.zenType) {
      case 'A': case 'C': this.requiredZen = recipe.zen; break;
      case 'B': this.requiredZen = (this.successRate * recipe.zen) >>> 0; break;
      // 'D' (restoring a harmony option) reads the harmony option's price table, not loaded on the web yet.
      default: void items; break;
    }
  }

  /** GetSourceName: an ingredient line of the most similar recipe and how far it is met. */
  sourceName(index: number, items: MixItem[], mixInit: boolean): { text: string; status: SourceStatus } {
    const recipe = this.mostSimilar;
    if (!recipe || index >= recipe.sources.length) return { text: '', status: SourceStatus.ERROR };
    void items;
    const r = recipe.sources[index];
    let name = itemNameAt(r.typeMin, r.levelMin);
    if (r.typeMin === r.typeMax && (r.levelMin === r.levelMax || (r.levelMin === 0 && r.levelMax === 255))
      && (r.optionMin === r.optionMax || (r.optionMin === 0 && r.optionMax === 255))) {
      if (r.durabilityMin === r.durabilityMax) name += `(${r.durabilityMin})`;
    } else {
      if (r.special & SP.ADD380ITEM) name = '380 Level item';
      else if (r.typeMin === 0 && r.typeMax === BOOTS + MAX_ITEM_INDEX - 1) name = 'Equipment item';
      else if (r.typeMin === 0 && r.typeMax === HELPER + MAX_ITEM_INDEX - 1) name = 'Equipment item';
      else if (r.typeMin === 0 && r.typeMax === STAFF + MAX_ITEM_INDEX - 1) name = 'Weapon item';
      else if (r.typeMin === SHIELD && r.typeMax === BOOTS + MAX_ITEM_INDEX - 1) name = 'Defense item';
      else if (r.typeMin === WING && r.typeMax === T.WINGS_OF_SATAN) name = 'Basic wing';
      else if (r.typeMin === T.WINGS_OF_SPIRITS && r.typeMax === T.WINGS_OF_DARKNESS) name = '2nd Wing';
      else if (r.typeMin === T.WING_OF_CURSE && r.typeMax === T.WING_OF_CURSE) name = 'Basic wing';
      else if (r.typeMin === T.WINGS_OF_DESPAIR && r.typeMax === T.WINGS_OF_DESPAIR) name = '2nd Wing';
      else if (r.typeMin === r.typeMax && (r.typeMin === T.CHAOS_DRAGON_AXE || r.typeMin === T.CHAOS_NATURE_BOW || r.typeMin === T.CHAOS_LIGHTNING_STAFF)) name = 'Chaos weapon';
      else if (r.typeMin === T.SEED_FIRE && r.typeMax === T.SEED_EARTH) name = 'Seed';
      else if (r.typeMin === T.SPHERE_MONO && r.typeMax === T.SPHERE_5) name = 'Sphere';
      else if (r.typeMin === T.SEED_SPHERE_FIRE_1 && r.typeMax === T.SEED_SPHERE_EARTH_5) name = 'Seed Sphere';
      else if (r.typeMin === T.SEED_SPHERE_FIRE_1 && r.typeMax === T.SEED_SPHERE_LIGHTNING_5) name = 'Seed Sphere (fire, ice, lightning)';
      else if (r.typeMin === T.SEED_SPHERE_WATER_1 && r.typeMax === T.SEED_SPHERE_EARTH_5) name = 'Seed Sphere (water, wind, earth)';
      else {
        // The level part of the name ("+9") goes when the recipe takes several levels.
        for (let j = 1; j <= 3 && name.length - j - 1 >= 0; j++) if (name[name.length - j] === '+') { name = name.slice(0, name.length - j - 1); break; }
      }
      if (r.durabilityMin === r.durabilityMax) name += `(${r.durabilityMin})`;
      if (r.levelMin === 0 && r.levelMax === 255) { /* any level */ }
      else if (r.levelMin === r.levelMax) name += ` ${format(NAMES.level, r.levelMin)}`;
      else if (r.levelMin === 0) name += ` ${format(NAMES.levelAtMost, r.levelMax)}`;
      else if (r.levelMax === 255) name += ` ${format(NAMES.levelAtLeast, r.levelMin)}`;
      else name += ` ${format(NAMES.levelRange, r.levelMin, r.levelMax)}`;
      if (r.optionMin === 0 && r.optionMax === 255) { /* any option */ }
      else if (r.optionMin === r.optionMax) name += ` ${format(NAMES.option, r.optionMin)}`;
      else if (r.optionMin === 0) name += ` ${format(NAMES.optionAtMost, r.optionMax)}`;
      else if (r.optionMax === 255) name += ` ${format(NAMES.optionAtLeast, r.optionMin)}`;
      else name += ` ${format(NAMES.optionRange, r.optionMin, r.optionMax)}`;
    }
    if (r.countMin === 0 && r.countMax === 255) name += ` (${NAMES.rateIncrease})`;
    else if (r.countMin === r.countMax) name += ` ${format(NAMES.count, r.countMin)}`;
    else if (r.countMin === 0) name += ` ${format(NAMES.countAtMost, r.countMax)}`;
    else if (r.countMax === 255) name += ` ${format(NAMES.countAtLeast, r.countMin)}`;
    else name += ` ${format(NAMES.countRange, r.countMin, r.countMax)}`;

    let text = name;
    if (r.special & SP.EXCELLENT) text = `Excellent ${name}`;
    if (r.special & SP.SETITEM) text = `Set ${name}`;
    if (r.special & SP.HARMONY) text = `Improve ${name}`;
    if (r.special & SP.SOCKETITEM) text = `Socket ${name}`;

    let status: SourceStatus;
    if (mixInit) status = r.countMin === 0 ? SourceStatus.PARTIALLY : SourceStatus.NO;
    else if (this.mostSimilarSourceTest[index] === 0) status = SourceStatus.YES;
    else if (this.mostSimilarSourceTest[index] < r.countMax) status = r.countMin <= 1 ? SourceStatus.YES : SourceStatus.PARTIALLY;
    else status = r.countMin === 0 ? SourceStatus.PARTIALLY : SourceStatus.NO;
    return { text, status };
  }
}
