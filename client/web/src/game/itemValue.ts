// An item's price in zen, as the desktop client works it out: the buy price an NPC shop asks, the sell price it
// pays, the item's repair cost (at a repair NPC or with the inventory's own repair button) and the cost of repairing
// everything. Ported from the desktop client:
//   ItemValue           (Engine/Object/ZzzInfomation.cpp)
//   CalcMaxDurability   (Engine/Object/ZzzInventory.cpp)
//   CalcRepairCost, ConvertRepairGold, RepairAllGold (Engine/Object/ZzzInventory.cpp)
//   SetItemAttributes, CalcWingOptions, CalcWizardsRingOptions (ZzzInfomation.cpp): the item's option slots
//   (ITEM::Special) the price reads
//   AddLinkedOptions, HasExcellentOptions, LinksOptionType (GameLogic/Items/ItemOptions.cpp)
//   FindItemOptionEffect (Data/GameData/ItemData/ItemOptionEffects.cpp)
//   COMGEM::CalcItemValue (GameLogic/Items/CComGem.cpp), CSocketItemMgr::CalcSocketBonusItemValue and IsSocketItem
//   (Network/Server/SocketSystem.cpp), IsRepairBan (GameLogic/Items/ShopRestrictions.cpp) and the tag helpers of
//   GameLogic/Items/ItemCategories.cpp.
//
// The item fields come from Data/Items/GroupNN_*.json as the desktop maps them (ItemAttributeConversion.cpp):
// buyPrice = ITEM_ATTRIBUTE::iZen, sellValue = Value, level = Level, durability = Durability,
// magicDurability = MagicDur, twoHanded = TwoHand, skill = m_wSkillIndex, repairable = the Repair action,
// tags = the ItemCategories helpers, options = the option definitions the item links (Data/Items/Options/*.json).
//
// Call loadItemValueData() once before using the functions: it loads the item catalog and the option definitions.

import { itemDefinition, loadItemCatalog } from './items.js';
import type { ItemDefinition } from './items.js';
import type { Item } from './inventory.js';

// ---------- item types (Core/Globals/_define.h, _enum.h): group * MAX_ITEM_INDEX + number ----------

const MAX_ITEM_INDEX = 512;
const t = (group: number, number: number): number => group * MAX_ITEM_INDEX + number;

const ITEM_WING = t(12, 0);
const ITEM_POTION = t(14, 0);

const ITEM_BOLT = t(4, 7);
const ITEM_ARROWS = t(4, 15);

const ITEM_WINGS_OF_HEAVEN = t(12, 1);
const ITEM_WINGS_OF_SATAN = t(12, 2);
const ITEM_WINGS_OF_SPIRITS = t(12, 3);
const ITEM_WINGS_OF_SOUL = t(12, 4);
const ITEM_WINGS_OF_DRAGON = t(12, 5);
const ITEM_WINGS_OF_DARKNESS = t(12, 6);
const ITEM_ORB_OF_TWISTING_SLASH = t(12, 7);
const ITEM_JEWEL_OF_CHAOS = t(12, 15);
const ITEM_ORB_OF_DEATH_STAB = t(12, 19);
const ITEM_WING_OF_STORM = t(12, 36);
const ITEM_WING_OF_ETERNAL = t(12, 37);
const ITEM_WING_OF_ILLUSION = t(12, 38);
const ITEM_WING_OF_RUIN = t(12, 39);
const ITEM_CAPE_OF_EMPEROR = t(12, 40);
const ITEM_WING_OF_CURSE = t(12, 41);
const ITEM_WINGS_OF_DESPAIR = t(12, 42);
const ITEM_WING_OF_DIMENSION = t(12, 43);
const ITEM_CAPE_OF_FIGHTER = t(12, 49);
const ITEM_CAPE_OF_OVERRULE = t(12, 50);
const ITEM_SEED_SPHERE_FIRE_1 = t(12, 100);
const ITEM_SMALL_CAPE_OF_LORD = t(12, 130);
const ITEM_SMALL_WING_OF_CURSE = t(12, 131);
const ITEM_SMALL_WINGS_OF_ELF = t(12, 132);
const ITEM_SMALL_WINGS_OF_HEAVEN = t(12, 133);
const ITEM_SMALL_WINGS_OF_SATAN = t(12, 134);
const ITEM_LITTLE_WARRIORS_CLOAK = t(12, 135);

const ITEM_HORN_OF_DINORANT = t(13, 3);
const ITEM_CONTRACT_SUMMON = t(13, 7);
const ITEM_TRANSFORMATION_RING = t(13, 10);
const ITEM_LIFE_STONE_ITEM = t(13, 11);
const ITEM_LOCHS_FEATHER = t(13, 14);
const ITEM_FRUITS = t(13, 15);
const ITEM_INVISIBILITY_CLOAK = t(13, 18);
const ITEM_WEAPON_OF_ARCHANGEL = t(13, 19);
const ITEM_WIZARDS_RING = t(13, 20);
const ITEM_ARMOR_OF_GUARDSMAN = t(13, 29);
const ITEM_CAPE_OF_LORD = t(13, 30);
const ITEM_SPIRIT = t(13, 31);
const ITEM_SPLINTER_OF_ARMOR = t(13, 32);
const ITEM_BLESS_OF_GUARDIAN = t(13, 33);
const ITEM_CLAW_OF_BEAST = t(13, 34);
const ITEM_FRAGMENT_OF_HORN = t(13, 35);
const ITEM_BROKEN_HORN = t(13, 36);
const ITEM_HORN_OF_FENRIR = t(13, 37);
const ITEM_OLD_SCROLL = t(13, 49);
const ITEM_ILLUSION_SORCERER_COVENANT = t(13, 50);
const ITEM_SCROLL_OF_BLOOD = t(13, 51);
const ITEM_FLAME_OF_CONDOR = t(13, 52);
const ITEM_FEATHER_OF_CONDOR = t(13, 53);
const ITEM_DEMON = t(13, 64);
const ITEM_SPIRIT_OF_GUARDIAN = t(13, 65);
const ITEM_PANDA_TRANSFORMATION_RING = t(13, 76);
const ITEM_PET_PANDA = t(13, 80);
const ITEM_LETHAL_WIZARDS_RING = t(13, 107);
const ITEM_SAPPHIRE_RING = t(13, 109);
const ITEM_RUBY_RING = t(13, 110);
const ITEM_TOPAZ_RING = t(13, 111);
const ITEM_AMETHYST_RING = t(13, 112);
const ITEM_RUBY_NECKLACE = t(13, 113);
const ITEM_EMERALD_NECKLACE = t(13, 114);
const ITEM_SAPPHIRE_NECKLACE = t(13, 115);
const ITEM_SKELETON_TRANSFORMATION_RING = t(13, 122);
const ITEM_PET_SKELETON = t(13, 123);

const ITEM_LARGE_HEALING_POTION = t(14, 3);
const ITEM_LARGE_MANA_POTION = t(14, 6);
const ITEM_SIEGE_POTION = t(14, 7);
const ITEM_ANTIDOTE = t(14, 8);
const ITEM_ALE = t(14, 9);
const ITEM_TOWN_PORTAL_SCROLL = t(14, 10);
const ITEM_JEWEL_OF_BLESS = t(14, 13);
const ITEM_JEWEL_OF_SOUL = t(14, 14);
const ITEM_JEWEL_OF_LIFE = t(14, 16);
const ITEM_DEVILS_EYE = t(14, 17);
const ITEM_DEVILS_KEY = t(14, 18);
const ITEM_DEVILS_INVITATION = t(14, 19);
const ITEM_REMEDY_OF_LOVE = t(14, 20);
const ITEM_RENA = t(14, 21);
const ITEM_JEWEL_OF_CREATION = t(14, 22);
const ITEM_LOST_MAP = t(14, 28);
const ITEM_SYMBOL_OF_KUNDUN = t(14, 29);
const ITEM_JEWEL_OF_GUARDIAN = t(14, 31);
const ITEM_SMALL_SHIELD_POTION = t(14, 35);
const ITEM_MEDIUM_SHIELD_POTION = t(14, 36);
const ITEM_LARGE_SHIELD_POTION = t(14, 37);
const ITEM_SMALL_COMPLEX_POTION = t(14, 38);
const ITEM_MEDIUM_COMPLEX_POTION = t(14, 39);
const ITEM_LARGE_COMPLEX_POTION = t(14, 40);
const ITEM_PUMPKIN_OF_LUCK = t(14, 45);
const ITEM_JACK_OLANTERN_DRINK = t(14, 50);
const ITEM_CHRISTMAS_STAR = t(14, 51);
const ITEM_GM_GIFT = t(14, 52);
const ITEM_TALISMAN_OF_LUCK = t(14, 53);
const ITEM_FIRECRACKER = t(14, 63);
const ITEM_CHERRY_BLOSSOM_WINE = t(14, 85);
const ITEM_CHERRY_BLOSSOM_RICE_CAKE = t(14, 86);
const ITEM_CHERRY_BLOSSOM_FLOWER_PETAL = t(14, 87);
const ITEM_GOLDEN_CHERRY_BLOSSOM_BRANCH = t(14, 90);
const ITEM_SUSPICIOUS_SCRAP_OF_PAPER = t(14, 101);
const ITEM_GAIONS_ORDER = t(14, 102);
const ITEM_FIRST_SECROMICON_FRAGMENT = t(14, 103);
const ITEM_SIXTH_SECROMICON_FRAGMENT = t(14, 108);
const ITEM_COMPLETE_SECROMICON = t(14, 109);
const ITEM_SILVER_KEY = t(14, 112);
const ITEM_GOLD_KEY = t(14, 113);
const ITEM_SEALED_GOLDEN_BOX = t(14, 121);
const ITEM_SEALED_SILVER_BOX = t(14, 122);
const ITEM_GOLDEN_BOX = t(14, 123);
const ITEM_SILVER_BOX = t(14, 124);

// ---------- option slot ids (ITEM::Special values; Core/Globals/_enum.h) ----------

const AT_SKILL_BLOCKING = 18;
const AT_SKILL_FALLING_SLASH = 19;
const AT_SKILL_LUNGE = 20;
const AT_SKILL_UPPERCUT = 21;
const AT_SKILL_CYCLONE = 22;
const AT_SKILL_SLASH = 23;
const AT_SKILL_TRIPLE_SHOT = 24;
const AT_SKILL_POWER_SLASH = 56;
const AT_SKILL_RECOVER = 234;
const AT_SKILL_MULTI_SHOT = 235;
const AT_SKILL_CHAOTIC_DISEIER = 238;
const AT_SKILL_KILLING_BLOW = 260;
const AT_SKILL_BEAST_UPPERCUT = 261;
const AT_SKILL_CYCLONE_STR = 326;
const AT_SKILL_SLASH_STR = 327;
const AT_SKILL_FALLING_SLASH_STR = 328;
const AT_SKILL_LUNGE_STR = 329;
const AT_SKILL_TRIPLE_SHOT_STR = 414;
const AT_SKILL_TRIPLE_SHOT_MASTERY = 418;
const AT_SKILL_CYCLONE_STR_MG = 479;
const AT_SKILL_POWER_SLASH_STR = 482;
const AT_SKILL_KILLING_BLOW_STR = 551;
const AT_SKILL_BEAST_UPPERCUT_STR = 552;
const AT_SKILL_KILLING_BLOW_MASTERY = 554;
const AT_SKILL_BEAST_UPPERCUT_MASTERY = 555;

const AT_IMPROVE_DAMAGE = 80;
const AT_IMPROVE_MAGIC = 81;
const AT_IMPROVE_CURSE = 82;
const AT_IMPROVE_BLOCKING = 83;
const AT_IMPROVE_DEFENSE = 84;
const AT_LUCK = 85;
const AT_LIFE_REGENERATION = 86;
const AT_IMPROVE_LIFE = 87;
const AT_IMPROVE_MANA = 88;
const AT_DECREASE_DAMAGE = 89;
const AT_REFLECTION_DAMAGE = 90;
const AT_IMPROVE_BLOCKING_PERCENT = 91;
const AT_IMPROVE_GAIN_GOLD = 92;
const AT_EXCELLENT_DAMAGE = 93;
const AT_IMPROVE_DAMAGE_LEVEL = 94;
const AT_IMPROVE_DAMAGE_PERCENT = 95;
const AT_IMPROVE_MAGIC_LEVEL = 96;
const AT_IMPROVE_MAGIC_PERCENT = 97;
const AT_IMPROVE_ATTACK_SPEED = 98;
const AT_IMPROVE_GAIN_LIFE = 99;
const AT_IMPROVE_GAIN_MANA = 100;
const AT_IMPROVE_HP_MAX = 101;
const AT_IMPROVE_MP_MAX = 102;
const AT_ONE_PERCENT_DAMAGE = 103;
const AT_IMPROVE_AG_MAX = 104;
const AT_DAMAGE_ABSORB = 105;
const AT_DAMAGE_REFLECTION = 106;
const AT_RECOVER_FULL_LIFE = 107;
const AT_RECOVER_FULL_MANA = 108;
const AT_IMPROVE_EVADE = 110;
// Slots the price doesn't read; their exact ids (far up the enum) don't matter here, only that they match no case.
const AT_SET_OPTION_IMPROVE_CHARISMA = -1;
const AT_IMPROVE_MAX_MANA = -2;
const AT_IMPROVE_MAX_AG = -3;
const NO_ITEM_OPTION_EFFECT = 0;

/** The excellent slots (ItemValue's first loop, which raises the level by 25, and the "Gold += Gold" cases). */
const EXCELLENT_SLOTS = new Set([AT_IMPROVE_LIFE, AT_IMPROVE_MANA, AT_DECREASE_DAMAGE, AT_REFLECTION_DAMAGE,
  AT_IMPROVE_BLOCKING_PERCENT, AT_IMPROVE_GAIN_GOLD, AT_EXCELLENT_DAMAGE, AT_IMPROVE_DAMAGE_LEVEL,
  AT_IMPROVE_DAMAGE_PERCENT, AT_IMPROVE_MAGIC_LEVEL, AT_IMPROVE_MAGIC_PERCENT, AT_IMPROVE_ATTACK_SPEED,
  AT_IMPROVE_GAIN_LIFE, AT_IMPROVE_GAIN_MANA]);

/** The skills that make an item worth 2.5 times as much (ItemValue's second loop). */
const PRICED_SKILLS = new Set([AT_SKILL_BLOCKING, AT_SKILL_FALLING_SLASH, AT_SKILL_FALLING_SLASH_STR, AT_SKILL_LUNGE,
  AT_SKILL_LUNGE_STR, AT_SKILL_UPPERCUT, AT_SKILL_CYCLONE, AT_SKILL_CYCLONE_STR, AT_SKILL_CYCLONE_STR_MG, AT_SKILL_SLASH,
  AT_SKILL_SLASH_STR, AT_SKILL_TRIPLE_SHOT, AT_SKILL_TRIPLE_SHOT_STR, AT_SKILL_TRIPLE_SHOT_MASTERY, AT_SKILL_RECOVER,
  AT_SKILL_CHAOTIC_DISEIER, AT_SKILL_MULTI_SHOT, AT_SKILL_POWER_SLASH, AT_SKILL_POWER_SLASH_STR, AT_SKILL_KILLING_BLOW,
  AT_SKILL_KILLING_BLOW_STR, AT_SKILL_KILLING_BLOW_MASTERY, AT_SKILL_BEAST_UPPERCUT, AT_SKILL_BEAST_UPPERCUT_STR,
  AT_SKILL_BEAST_UPPERCUT_MASTERY]);

// MAX_ITEM_SPECIAL (Core/Globals/_define.h): the slots after it are dropped (ItemOptionSlots.cpp AddSlot).
const MAX_ITEM_SPECIAL = 16;
// Data::Items::MaxExcellentOptionNumber
const MAX_EXCELLENT_OPTION_NUMBER = 6;
// SEASON4A::MAX_SOCKET_OPTION, MAX_SOCKET_TYPES, SOCKET_EMPTY, MAX_SOCKETS
const MAX_SOCKET_OPTION = 50;
const MAX_SOCKET_TYPES = 6;
const SOCKET_EMPTY = 0xfe;
const MAX_SOCKETS = 5;
// COMGEM's FIRST: a bundle at level n holds (n + 1) * 10 jewels.
const COMGEM_FIRST = 10;

// ---------- integer helpers (the C++ works in int / int64 / float) ----------

/** C++ integer division (truncates toward zero). */
const div = (a: number, b: number): number => Math.trunc(a / b);
/** A product of C++ ints, wrapping at 32 bits as the desktop's int arithmetic does. */
const imul = (...factors: number[]): number => factors.reduce((product, factor) => Math.imul(product, factor), 1);
/** `(DWORD)` of a float: MSVC truncates to int64 and keeps the low 32 bits; NaN and infinities give 0. */
function toDword(value: number): number {
  if (!Number.isFinite(value)) return 0;
  const truncated = Math.trunc(value);
  return ((truncated % 0x100000000) + 0x100000000) % 0x100000000;
}
/** The rounding to tens below 1000 and hundreds from 1000 (ItemValue's EXIT_CALCULATE, CalcRepairCost). */
function roundGold(gold: number): number {
  if (gold >= 1000) return div(gold, 100) * 100;
  if (gold >= 100) return div(gold, 10) * 10;
  return gold;
}

// ---------- the option definitions (Data/Items/Options/*.json) ----------

type OptionType = 'luck' | 'option' | 'excellent';

interface OptionValueData { values?: number | number[] }
interface OptionData { number?: number; attribute: string; add?: OptionValueData; percent?: OptionValueData }
interface OptionDefinitionData { name: string; type: string; options: OptionData[] }
interface OptionsFile { definitions: OptionDefinitionData[] }

/** An option's slot: its id (ITEM::Special) and whether it writes its value (ItemOptionEffect). */
interface OptionEffect { effect: number; writesValue: boolean }

// FindItemOptionEffect's rows (ItemOptionEffects.cpp): option type, attribute, add or per cent → slot.
const EFFECT_ROWS: [OptionType, string, 'add' | 'percent', number, boolean][] = [
  ['luck', 'criticalDamageChance', 'percent', AT_LUCK, true],
  ['option', 'physicalBaseDmg', 'add', AT_IMPROVE_DAMAGE, true],
  ['option', 'baseDamageBonus', 'add', AT_IMPROVE_DAMAGE, true],
  ['option', 'wizardryBaseDmg', 'add', AT_IMPROVE_MAGIC, true],
  ['option', 'curseBaseDmg', 'add', AT_IMPROVE_CURSE, true],
  ['option', 'defenseRatePvm', 'add', AT_IMPROVE_BLOCKING, true],
  ['option', 'defenseBase', 'add', AT_IMPROVE_DEFENSE, true],
  ['option', 'healthRecoveryMultiplier', 'percent', AT_LIFE_REGENERATION, true],
  ['option', 'maximumMana', 'percent', AT_IMPROVE_MAX_MANA, true],
  ['option', 'maximumAbility', 'percent', AT_IMPROVE_MAX_AG, true],
  ['excellent', 'maximumHealth', 'percent', AT_IMPROVE_LIFE, false],
  ['excellent', 'maximumMana', 'percent', AT_IMPROVE_MANA, false],
  ['excellent', 'armorDamageDecrease', 'percent', AT_DECREASE_DAMAGE, false],
  ['excellent', 'damageReflection', 'percent', AT_REFLECTION_DAMAGE, false],
  ['excellent', 'defenseRatePvm', 'percent', AT_IMPROVE_BLOCKING_PERCENT, true],
  ['excellent', 'moneyAmountRate', 'percent', AT_IMPROVE_GAIN_GOLD, false],
  ['excellent', 'excellentDamageChance', 'percent', AT_EXCELLENT_DAMAGE, false],
  ['excellent', 'physicalBaseDmg', 'add', AT_IMPROVE_DAMAGE_LEVEL, true],
  ['excellent', 'wizardryBaseDmg', 'add', AT_IMPROVE_MAGIC_LEVEL, true],
  ['excellent', 'physicalBaseDmgIncrease', 'percent', AT_IMPROVE_DAMAGE_PERCENT, true],
  ['excellent', 'wizardryBaseDmgIncrease', 'percent', AT_IMPROVE_MAGIC_PERCENT, true],
  ['excellent', 'attackSpeedAny', 'add', AT_IMPROVE_ATTACK_SPEED, true],
  ['excellent', 'healthAfterMonsterKillMultiplier', 'add', AT_IMPROVE_GAIN_LIFE, false],
  ['excellent', 'manaAfterMonsterKillMultiplier', 'add', AT_IMPROVE_GAIN_MANA, false],
];

function findEffect(type: OptionType, option: OptionData): OptionEffect {
  const add = option.add ? 'add' : option.percent ? 'percent' : null;
  const row = EFFECT_ROWS.find(([rowType, attribute, rowAdd]) => rowType === type && attribute === option.attribute && rowAdd === add);
  return row ? { effect: row[3], writesValue: row[4] } : { effect: NO_ITEM_OPTION_EFFECT, writesValue: false };
}

interface OptionDefinition {
  type: OptionType;
  /** The options in the order AddLinkedOptions adds them (GetOptionOrder: the excellent ones by number, highest first). */
  options: { number: number | null; values: number[]; isList: boolean; effect: OptionEffect }[];
}

let definitions: Map<string, OptionDefinition> | null = null;
let loading: Promise<void> | null = null;

function isOptionType(type: string): type is OptionType {
  return type === 'luck' || type === 'option' || type === 'excellent';
}

function readDefinition(data: OptionDefinitionData): OptionDefinition | null {
  if (!isOptionType(data.type)) return null;
  const type = data.type;
  const options = data.options.map((option) => {
    const values = (option.add ?? option.percent)?.values;
    return {
      number: option.number ?? null,
      values: values === undefined ? [] : Array.isArray(values) ? values : [values],
      isList: Array.isArray(values),
      effect: findEffect(type, option),
    };
  });
  if (type === 'excellent') {
    // std::stable_sort by number, highest first; Array.prototype.sort is stable.
    options.sort((a, b) => (b.number ?? 0) - (a.number ?? 0));
  }
  return { type, options };
}

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return response.json() as Promise<T>;
}

/** Loads what the prices need (the item catalog and the option definitions); once. */
export function loadItemValueData(): Promise<void> {
  loading ??= (async () => {
    const [, ...files] = await Promise.all([
      loadItemCatalog(),
      fetchJson<OptionsFile>('/data/Items/Options/Excellent.json'),
      fetchJson<OptionsFile>('/data/Items/Options/Luck.json'),
      fetchJson<OptionsFile>('/data/Items/Options/Option.json'),
    ]);
    const map = new Map<string, OptionDefinition>();
    for (const file of files) {
      for (const data of file.definitions) {
        const definition = readDefinition(data);
        if (definition) map.set(data.name, definition);
      }
    }
    definitions = map;
  })().catch((error: unknown) => { loading = null; throw error; });
  return loading;
}

function optionDefinitions(): Map<string, OptionDefinition> {
  if (!definitions) throw new Error('loadItemValueData() has not finished');
  return definitions;
}

// ---------- the item's definition (ITEM_ATTRIBUTE) and data rules ----------

/** The fields the price reads besides ItemDefinition's typed ones (docs/item-data.md). */
interface PriceDefinition extends ItemDefinition {
  sellValue?: number;
  magicDurability?: number;
  repairable?: boolean;
}

function definitionOf(item: Item): PriceDefinition | null {
  return itemDefinition(item.group, item.number);
}

const itemTypeOf = (item: Item): number => t(item.group, item.number);

/** ItemDatabase::HasTag: an entry without a name has no rule data, so no tags. */
function hasTag(definition: PriceDefinition | null, tag: string): boolean {
  return definition?.name !== undefined && (definition.tags?.includes(tag) ?? false);
}

/** GameLogic::Items::IsRepairBan: the Repair action isn't allowed (an entry without a name allows nothing). */
export function isRepairBan(item: Item): boolean {
  const definition = definitionOf(item);
  return definition?.name === undefined || definition.repairable === false;
}

/** The option definitions an item links, the first of each type (ItemOptionDatabase::ResolveLinks). */
function linkedDefinitions(definition: PriceDefinition | null): OptionDefinition[] {
  const all = optionDefinitions();
  const links: OptionDefinition[] = [];
  if (definition?.name === undefined) return links;
  for (const name of definition.options ?? []) {
    const linked = all.get(name);
    if (linked && !links.some((link) => link.type === linked.type)) links.push(linked);
  }
  return links;
}

/** ItemOptionValue::At: a list's value at the level (its last one above it), a constant's at any level. */
function valueAt(option: OptionDefinition['options'][number], level: number): number {
  if (option.values.length === 0 || (option.isList && level < 1)) return 0;
  if (!option.isList) return option.values[0];
  return option.values[Math.min(level - 1, option.values.length - 1)];
}

/** GameLogic::Items::HasExcellentOptions(item): a linked excellent option whose bit is set. */
function hasExcellentOptions(item: Item, definition: PriceDefinition | null): boolean {
  if (item.excellent === 0) return false;
  return linkedDefinitions(definition).some((link) => link.type === 'excellent' &&
    link.options.some((option) => isExcellentBitSet(item.excellent, option.number)));
}

function isExcellentBitSet(excellent: number, number: number | null): boolean {
  return number !== null && number >= 1 && number <= MAX_EXCELLENT_OPTION_NUMBER && ((excellent >> (number - 1)) & 1) !== 0;
}

// ---------- the option slots (SetItemAttributes) ----------

interface Slot { option: number; value: number }

/** The item's option slots (ITEM::Special / SpecialValue) as SetItemAttributes fills them. */
function optionSlots(item: Item, definition: PriceDefinition | null): Slot[] {
  const type = itemTypeOf(item);
  const slots: Slot[] = [];
  // ItemOptionSlots.cpp AddSlot: a WORD value; the slots after MAX_ITEM_SPECIAL are dropped.
  const add = (option: number, value = 0): void => {
    if (slots.length < MAX_ITEM_SPECIAL) slots.push({ option, value: value & 0xffff });
  };
  const links = linkedDefinitions(definition);

  // AddLinkedOptions: the options of the linked definitions of a type that the item's data turns on.
  const addLinked = (optionType: OptionType): void => {
    const turnedOn = optionType === 'luck' ? item.luck : optionType === 'option' ? item.optionLevel > 0 : item.excellent !== 0;
    if (!turnedOn) return;
    for (const link of links) {
      if (link.type !== optionType) continue;
      for (const option of link.options) {
        if (optionType === 'option' && option.number !== item.optionType) continue;
        if (optionType === 'excellent' && !isExcellentBitSet(item.excellent, option.number)) continue;
        // The value is taken at the additional option's level, else at 1. The terms that read the hero's level
        // (the excellent damage's level / 20) are left out: the price reads only whether the slot is there.
        const level = optionType === 'option' ? item.optionLevel : 1;
        add(option.effect.effect, option.effect.writesValue ? valueAt(option, level) : 0);
      }
    }
  };

  // CalcWingOptions
  const wingTier = definition?.name !== undefined ? definition.wingTier : undefined;
  if (wingTier === 'second' && !hasTag(definition, 'cape')) {
    if (item.excellent & 0x01) add(AT_IMPROVE_HP_MAX, 50 + item.level * 5);
    if ((item.excellent >> 1) & 0x01) add(AT_IMPROVE_MP_MAX, 50 + item.level * 5);
    if ((item.excellent >> 2) & 0x01) add(AT_ONE_PERCENT_DAMAGE, 3);
    if ((item.excellent >> 3) & 0x01) add(AT_IMPROVE_AG_MAX, 50);
    if ((item.excellent >> 4) & 0x01) add(AT_IMPROVE_ATTACK_SPEED, 5);
  } else if (type === ITEM_CAPE_OF_LORD || type === ITEM_CAPE_OF_FIGHTER) {
    if (item.excellent & 0x01) add(AT_IMPROVE_HP_MAX, 50 + item.level * 5);
    if ((item.excellent >> 1) & 0x01) add(AT_IMPROVE_MP_MAX, 50 + item.level * 5);
    if ((item.excellent >> 2) & 0x01) add(AT_ONE_PERCENT_DAMAGE, 3);
    if ((item.excellent >> 3) & 0x01 && type !== ITEM_CAPE_OF_FIGHTER) add(AT_SET_OPTION_IMPROVE_CHARISMA, 10 + item.level * 5);
  } else if (wingTier === 'third') {
    if (item.excellent & 0x01) add(AT_ONE_PERCENT_DAMAGE, 5);
    if ((item.excellent >> 1) & 0x01) add(AT_DAMAGE_REFLECTION, 5);
    if ((item.excellent >> 2) & 0x01) add(AT_RECOVER_FULL_LIFE, 5);
    if ((item.excellent >> 3) & 0x01) add(AT_RECOVER_FULL_MANA, 5);
  }

  // The item's skill (ITEM_ATTRIBUTE::m_wSkillIndex)
  if (item.skill && (definition?.skill ?? 0) !== 0) add(definition?.skill ?? 0);

  addLinked('luck');
  addLinked('option');

  // The Horn of Dinorant's and the wings' additional options, still code when the item links no additional option.
  const option3 = item.optionLevel;
  if (option3 && !links.some((link) => link.type === 'option')) {
    if (type === ITEM_HORN_OF_DINORANT) {
      if (option3 & 0x01) add(AT_DAMAGE_ABSORB, 5);
      if ((option3 >> 1) & 0x01) add(AT_IMPROVE_AG_MAX, 50);
      if ((option3 >> 2) & 0x01) add(AT_IMPROVE_ATTACK_SPEED, 5);
    } else {
      wingOption(type, item.optionType, option3, add);
    }
  }
  // (The Dark Horse's SetPetItemConvert slots are left out: they need the pet's data, and the price of a Dark Lord
  // pet comes from the pet's level anyway.)

  addLinked('excellent');

  // CalcWizardsRingOptions
  if (type === ITEM_WIZARDS_RING && (item.level === 0 || item.level === 3)) {
    add(AT_IMPROVE_MAGIC_PERCENT, 10);
    add(AT_IMPROVE_DAMAGE_PERCENT, 10);
    add(AT_IMPROVE_ATTACK_SPEED, 10);
  }
  if (type === ITEM_LETHAL_WIZARDS_RING) {
    add(AT_IMPROVE_MAGIC_PERCENT, 15);
    add(AT_IMPROVE_DAMAGE_PERCENT, 15);
    add(AT_IMPROVE_ATTACK_SPEED, 10);
  }
  return slots;
}

/** SetItemAttributes: the wings' additional option by the wing and its option type. */
function wingOption(type: number, optionType: number, option3: number, add: (option: number, value?: number) => void): void {
  // [wing, [option type, slot, times the option level]...]
  const byType = (choices: [number, number, number][]): void => {
    for (const [choice, slot, times] of choices) if (optionType === choice) add(slot, option3 * times);
  };
  switch (type) {
    case ITEM_WING: add(AT_LIFE_REGENERATION, option3); break;
    case ITEM_WINGS_OF_HEAVEN: case ITEM_WING_OF_CURSE: add(AT_IMPROVE_MAGIC, option3 * 4); break;
    case ITEM_WINGS_OF_SATAN: add(AT_IMPROVE_DAMAGE, option3 * 4); break;
    case ITEM_WINGS_OF_SPIRITS: byType([[2, AT_LIFE_REGENERATION, 1], [0, AT_IMPROVE_DAMAGE, 4]]); break;
    case ITEM_WINGS_OF_SOUL: byType([[0, AT_LIFE_REGENERATION, 1], [2, AT_IMPROVE_MAGIC, 4]]); break;
    case ITEM_WINGS_OF_DRAGON: byType([[2, AT_IMPROVE_DAMAGE, 4], [0, AT_LIFE_REGENERATION, 1]]); break;
    case ITEM_WINGS_OF_DARKNESS: byType([[2, AT_IMPROVE_DAMAGE, 4], [0, AT_IMPROVE_MAGIC, 4]]); break;
    case ITEM_CAPE_OF_LORD: add(AT_IMPROVE_DAMAGE, option3 * 4); break;
    case ITEM_CAPE_OF_FIGHTER: byType([[2, AT_IMPROVE_DAMAGE, 4], [0, AT_LIFE_REGENERATION, 1]]); break;
    case ITEM_WINGS_OF_DESPAIR: byType([[2, AT_IMPROVE_MAGIC, 4], [0, AT_IMPROVE_CURSE, 4]]); break;
    case ITEM_WING_OF_STORM: case ITEM_WING_OF_ILLUSION: case ITEM_CAPE_OF_EMPEROR: case ITEM_CAPE_OF_OVERRULE:
      byType([[0, AT_LIFE_REGENERATION, 1], [2, AT_IMPROVE_DEFENSE, 4], [3, AT_IMPROVE_DAMAGE, 4]]); break;
    case ITEM_WING_OF_ETERNAL: byType([[0, AT_LIFE_REGENERATION, 1], [2, AT_IMPROVE_DEFENSE, 4], [3, AT_IMPROVE_MAGIC, 4]]); break;
    case ITEM_WING_OF_RUIN: byType([[0, AT_LIFE_REGENERATION, 1], [2, AT_IMPROVE_MAGIC, 4], [3, AT_IMPROVE_DAMAGE, 4]]); break;
    case ITEM_WING_OF_DIMENSION: byType([[0, AT_LIFE_REGENERATION, 1], [2, AT_IMPROVE_CURSE, 4], [3, AT_IMPROVE_MAGIC, 4]]); break;
  }
}

// ---------- jewel bundles and sockets ----------

// COMGEM::Check_Jewel's bundles (group 12) and COMGEM::CalcItemValue's price of one jewel of each.
const BUNDLE_JEWEL_PRICES = new Map<number, number>([
  [t(12, 30), 9000000], // Jewel of Bless
  [t(12, 31), 6000000], // Jewel of Soul
  [t(12, 136), 45000000], // Jewel of Life
  [t(12, 137), 36000000], // Jewel of Creation
  [t(12, 138), 60000000], // Jewel of Guardian
  [t(12, 139), 18600], // Gemstone
  [t(12, 140), 18600], // Jewel of Harmony
  [t(12, 141), 810000], // Jewel of Chaos
  [t(12, 142), 18600], // Lower refine stone
  [t(12, 143), 18600], // Higher refine stone
]);

// CSocketItemMgr::IsSocketItem: weapons and shields by group, and the armour sets 45 to 53.
const SOCKET_ITEMS: Record<number, number[]> = { 0: [26, 27, 28], 2: [16, 17], 4: [23], 5: [30, 31, 32], 6: [17, 18, 19, 20] };
function isSocketItem(item: Item): boolean {
  if (item.group >= 7 && item.group <= 11) return item.number >= 45 && item.number <= 53;
  return SOCKET_ITEMS[item.group]?.includes(item.number) ?? false;
}

/** CSocketItemMgr::CalcSocketBonusItemValue: 80% more per socket, and the price of each socketed seed sphere. */
function socketBonus(item: Item, gold: number): number {
  if (!isSocketItem(item)) return 0;
  const count = Math.min(item.sockets.length, MAX_SOCKETS);
  // int64 * float: the sum is a float, cast back to int64.
  let result = Math.trunc(Math.fround(Math.fround(gold) * Math.fround(count * Math.fround(0.8))));
  for (let i = 0; i < count; i++) {
    const socket = item.sockets[i];
    if (socket === SOCKET_EMPTY) continue;
    const seed = socket % MAX_SOCKET_OPTION;
    const sphereLevel = div(socket, MAX_SOCKET_OPTION) + 1;
    let seedSphereType = 0;
    if (seed <= 9) seedSphereType = 0;
    else if (seed <= 15) seedSphereType = 1;
    else if (seed <= 20) seedSphereType = 2;
    else if (seed <= 28) seedSphereType = 3;
    else if (seed <= 33) seedSphereType = 4;
    else if (seed <= 40) seedSphereType = 5;
    const sphereType = ITEM_SEED_SPHERE_FIRE_1 + (sphereLevel - 1) * MAX_SOCKET_TYPES + seedSphereType;
    // The C++ prices an uninitialised ITEM of that type; the spheres the data prices (buyPrice) don't read the rest.
    // For the others (levels 4 and 5) this takes level, durability and options as 0.
    const sphere: Item = { group: Math.floor(sphereType / MAX_ITEM_INDEX), number: sphereType % MAX_ITEM_INDEX, level: 0,
      durability: 0, luck: false, skill: false, guardian: false, optionLevel: 0, optionType: 0, excellent: 0, ancientSet: 0,
      ancientBonus: 0, harmony: 0, sockets: [], socketBonus: 0, length: 0, width: 1, height: 1 };
    result += itemValue(sphere, 0);
  }
  return result;
}

// ---------- ItemValue ----------

/**
 * ItemValue: the item's price. goldType 0 = what an NPC shop asks for it, 1 = what it pays for it (a third, less the
 * wear), 2 = the base of the repair cost (a third, rounded first). A Dark Lord pet (Dark Horse, Dark Raven) is priced
 * by its level, which comes from the pet's own data: without `petLevel` it is -1, as the desktop's "no pet info".
 */
export function itemValue(item: Item, goldType: 0 | 1 | 2, petLevel?: number): number {
  const type = itemTypeOf(item);
  const p = definitionOf(item);
  const pLevel = p?.level ?? 0;
  const pDurability = p?.durability ?? 0;
  const pValue = p?.sellValue ?? 0;

  // A fixed price (ITEM_ATTRIBUTE::iZen, buyPrice): a third when selling or repairing.
  const buyPrice = p?.buyPrice ?? 0;
  if (buyPrice !== 0) {
    let gold = buyPrice;
    if (goldType) gold = div(gold, 3);
    return roundGold(gold) | 0;
  }

  const group = div(type, MAX_ITEM_INDEX);
  const level = item.level;
  const slots = optionSlots(item, p);
  const excellent = slots.some((slot) => EXCELLENT_SLOTS.has(slot.option));
  let level2 = pLevel + level * 3;
  if (excellent) level2 += 25;

  let gold = 0;
  if (type === ITEM_BOLT || type === ITEM_ARROWS) {
    const sellMoney = (type === ITEM_BOLT ? [100, 1400, 2200, 3000] : [70, 1200, 2000, 2800])[level] ?? 0;
    if (pDurability > 0) gold = div(sellMoney * item.durability, pDurability);
  } else if (BUNDLE_JEWEL_PRICES.has(type)) {
    // COMGEM::CalcItemValue (an int)
    gold = imul(BUNDLE_JEWEL_PRICES.get(type) ?? 0, level + 1, COMGEM_FIRST);
  } else if (type === ITEM_JEWEL_OF_BLESS) gold = 9000000;
  else if (type === ITEM_JEWEL_OF_SOUL) gold = 6000000;
  else if (type === ITEM_JEWEL_OF_CHAOS) gold = 810000;
  else if (type === ITEM_JEWEL_OF_LIFE) gold = 45000000;
  else if (type === ITEM_JEWEL_OF_CREATION) gold = 36000000;
  else if (type === ITEM_POTION + 141) gold = 224000 * 3;
  else if (type === ITEM_POTION + 142) gold = 182000 * 3;
  else if (type === ITEM_POTION + 143) gold = 157000 * 3;
  else if (type === ITEM_POTION + 144) gold = 121000 * 3;
  else if (type === ITEM_LOCHS_FEATHER) {
    gold = level === 0 ? 180000 : level === 1 ? 7500000 : 0;
  } else if (type === ITEM_HORN_OF_DINORANT) {
    gold = 960000;
    for (const slot of slots) {
      if (slot.option === AT_DAMAGE_ABSORB || slot.option === AT_IMPROVE_AG_MAX || slot.option === AT_IMPROVE_ATTACK_SPEED) gold += 300000;
    }
  } else if (type === ITEM_FRUITS) gold = 33000000;
  else if (hasTag(p, 'bloodCastleTicketPart')) {
    gold = [0, 10000, 50000, 100000, 300000, 500000, 800000, 1000000, 1200000][level] ?? 0;
  } else if (type === ITEM_INVISIBILITY_CLOAK) {
    gold = level === 1 ? 50000 : 200000 + 20000 * (level - 1);
    gold *= 3;
  } else if (type === ITEM_LOST_MAP) gold = 200000 * 3;
  else if (type === ITEM_SYMBOL_OF_KUNDUN) gold = item.durability * 10000 * 3;
  else if (type === ITEM_POTION + 111) gold = 200000 * 3;
  else if (type === ITEM_POTION + 110) gold = item.durability * 10000 * 3;
  else if (type === ITEM_GAIONS_ORDER) gold = 10000 * 3;
  else if (type === ITEM_COMPLETE_SECROMICON) gold = 10000 * 3;
  else if (type === ITEM_SUSPICIOUS_SCRAP_OF_PAPER || (type >= ITEM_FIRST_SECROMICON_FRAGMENT && type <= ITEM_SIXTH_SECROMICON_FRAGMENT)) {
    gold = item.durability * 10000 * 3;
  } else if (type === ITEM_ARMOR_OF_GUARDSMAN) gold = 5000;
  else if (type === ITEM_RENA) {
    gold = level === 0 || level === 1 ? 9000 : level === 2 ? 3000 * 3 : level === 3 ? item.durability * 3900 : 0;
  } else if (type === ITEM_DEVILS_EYE) {
    gold = [30000, 10000, 50000, 100000, 300000, 500000, 800000, 1000000][Math.min(Math.max(0, level), 7)];
  } else if (type === ITEM_DEVILS_KEY) {
    gold = [30000, 15000, 75000, 150000, 450000, 750000, 1200000, 1500000][Math.min(Math.max(0, level), 7)];
  } else if (type === ITEM_DEVILS_INVITATION) {
    gold = [120000, 60000, 84000, 120000, 180000, 240000, 300000, 180000][Math.min(Math.max(0, level), 7)];
  } else if (type === ITEM_OLD_SCROLL || type === ITEM_ILLUSION_SORCERER_COVENANT || type === ITEM_SCROLL_OF_BLOOD) {
    gold = [3000 * 3, 500000, 600000, 800000, 1000000, 1200000, 1400000][level] ?? 3000 * 3;
  } else if (hasTag(p, 'secondClassQuestItem') || hasTag(p, 'thirdClassQuestItem')) gold = 9000;
  else if (type === ITEM_ALE && level === 1) gold = 1000;
  else if (type === ITEM_REMEDY_OF_LOVE) gold = 900;
  else if (type === ITEM_CHRISTMAS_STAR) gold = 200000;
  else if (type === ITEM_FIRECRACKER) gold = 200000;
  else if (type === ITEM_GM_GIFT) gold = 33 * 3;
  else if (type === ITEM_TOWN_PORTAL_SCROLL) gold = 750;
  else if (type === ITEM_JEWEL_OF_GUARDIAN) gold = 60000000;
  else if (type === ITEM_SIEGE_POTION) {
    gold = level === 0 ? 900000 * item.durability : level === 1 ? 450000 * item.durability : 0;
  } else if (type === ITEM_CONTRACT_SUMMON) {
    gold = level === 0 ? 1500000 : level === 1 ? 1200000 : 0;
  } else if (type === ITEM_LIFE_STONE_ITEM) {
    gold = level === 0 ? 100000 : level === 1 ? 2400000 : 0;
  } else if (type >= ITEM_SMALL_SHIELD_POTION && type <= ITEM_LARGE_SHIELD_POTION) {
    gold = (type === ITEM_SMALL_SHIELD_POTION ? 2000 : type === ITEM_MEDIUM_SHIELD_POTION ? 4000 : 6000) * item.durability;
  } else if (hasTag(p, 'complexPotion')) {
    gold = type === ITEM_SMALL_COMPLEX_POTION ? 2500 : type === ITEM_MEDIUM_COMPLEX_POTION ? 5000
      : type === ITEM_LARGE_COMPLEX_POTION ? 7500 : 0;
    gold *= item.durability;
  } else if (type === ITEM_POTION + 100) {
    gold = 100 * 3 * item.durability;
  } else if (type === ITEM_GOLDEN_CHERRY_BLOSSOM_BRANCH || type === ITEM_CHERRY_BLOSSOM_WINE
    || type === ITEM_CHERRY_BLOSSOM_RICE_CAKE || type === ITEM_CHERRY_BLOSSOM_FLOWER_PETAL) {
    gold = 300 * item.durability;
  } else if (pValue > 0) {
    // The data's sell value (ITEM_ATTRIBUTE::Value): its square * 10 / 12.
    gold = div(pValue * pValue * 10, 12);
    if (type === ITEM_LARGE_HEALING_POTION || type === ITEM_LARGE_MANA_POTION) gold = 1500;
    // The potions (Apple to Antidote): doubled per level, per item of the stack; a third, to tens, when selling.
    if (type >= ITEM_POTION && type <= ITEM_ANTIDOTE) {
      if (level > 0) gold *= Math.trunc(2 ** level);
      gold = div(gold, 10) * 10;
      gold *= item.durability;
      if (goldType) {
        gold = div(gold, 3);
        gold = div(gold, 10) * 10;
      }
      return gold;
    }
  } else if (type === ITEM_WIZARDS_RING) {
    if (level === 0) gold = 3000;
    else if (level === 1) gold = 0;
  } else if (type === ITEM_FLAME_OF_CONDOR || type === ITEM_FEATHER_OF_CONDOR) gold = 3000000;
  else if (type === ITEM_SPIRIT) {
    gold = level === 0 ? 10000000 * 3 : level === 1 ? 5000000 * 3 : 0;
  } else if ((group === 12 && type > ITEM_WINGS_OF_DARKNESS && !(type >= ITEM_WING_OF_STORM && type <= ITEM_WING_OF_DIMENSION)
    && type !== ITEM_CAPE_OF_OVERRULE) || group === 13 || group === 15) {
    // The other wing-group items, the helpers and the scrolls: 100 + level^3 (an int).
    gold = 100 + imul(level2, level2, level2);
    for (const slot of slots) {
      if (slot.option === AT_LIFE_REGENERATION) gold += gold * slot.value;
    }
  } else if (type === ITEM_TALISMAN_OF_LUCK) {
    gold = 0;
  } else {
    // Weapons, shields, armour and the wings: by the level, its + level adding more from +5.
    const LEVEL_BONUS: Record<number, number> = { 5: 4, 6: 10, 7: 25, 8: 45, 9: 65, 10: 95, 11: 135, 12: 185, 13: 245, 14: 305, 15: 365 };
    level2 += LEVEL_BONUS[level] ?? 0;
    const wing = (group === 12 && type <= ITEM_WINGS_OF_DARKNESS) || type === ITEM_CAPE_OF_LORD
      || (type >= ITEM_WING_OF_STORM && type <= ITEM_WING_OF_DIMENSION) || type === ITEM_CAPE_OF_OVERRULE;
    if (wing) {
      gold = (40000000 + imul(40 + level2, level2, level2, 11)) | 0;
    } else {
      gold = 100 + div(imul(40 + level2, level2, level2), 8);
    }
    // One-handed weapons and shields: 80%.
    if (group >= 0 && group <= 6 && !(p?.twoHanded ?? false)) gold = div(gold * 80, 100);

    const wingOptions = (group === 12 && type <= ITEM_WINGS_OF_DARKNESS)
      || (type >= ITEM_WING_OF_STORM && type <= ITEM_WING_OF_DIMENSION)
      || (type >= ITEM_CAPE_OF_FIGHTER && type <= ITEM_CAPE_OF_OVERRULE);
    const byStep = (value: number, step: number): void => {
      const tenths = [6, 14, 28, 56][value / step - 1];
      if (Number.isInteger(value / step) && tenths !== undefined) gold += Math.trunc(gold * tenths / 10);
    };
    for (const slot of slots) {
      const option = slot.option;
      if (PRICED_SKILLS.has(option)) {
        gold += Math.trunc(gold * 1.5);
      } else if (option === AT_IMPROVE_DAMAGE || option === AT_IMPROVE_MAGIC || option === AT_IMPROVE_CURSE
        || option === AT_IMPROVE_DEFENSE || option === AT_LIFE_REGENERATION) {
        // The additional option: +60%, +140%, +280%, +560% at 4, 8, 12, 16 (a wing's regeneration counts 4 a level).
        byStep(wingOptions && option === AT_LIFE_REGENERATION ? slot.value * 4 : slot.value, 4);
      } else if (option === AT_IMPROVE_BLOCKING) {
        byStep(slot.value, 5);
      } else if (option === AT_LUCK) {
        gold += Math.trunc(gold * 25 / 100);
      } else if (EXCELLENT_SLOTS.has(option) || option === AT_IMPROVE_EVADE) {
        gold += gold;
      } else if (option === AT_IMPROVE_HP_MAX || option === AT_IMPROVE_MP_MAX || option === AT_ONE_PERCENT_DAMAGE
        || option === AT_IMPROVE_AG_MAX || option === AT_DAMAGE_ABSORB || option === AT_DAMAGE_REFLECTION
        || option === AT_RECOVER_FULL_LIFE || option === AT_RECOVER_FULL_MANA) {
        gold += Math.trunc(gold * 25 / 100);
      }
    }
    gold += socketBonus(item, gold);
  }
  gold = Math.min(gold, 3000000000);

  if (goldType === 2) gold = roundGold(gold);
  if (goldType) gold = div(gold, 3);

  // Prices that don't change when selling.
  if (type === ITEM_SPLINTER_OF_ARMOR) gold = item.durability * 50;
  else if (type === ITEM_BLESS_OF_GUARDIAN) gold = item.durability * 100;
  else if (type === ITEM_CLAW_OF_BEAST) gold = item.durability * 1000;
  else if (type === ITEM_FRAGMENT_OF_HORN) gold = item.durability * 10000;
  else if (type === ITEM_BROKEN_HORN) gold = 30000;
  else if (type === ITEM_HORN_OF_FENRIR) gold = 50000;

  if (type >= ITEM_PUMPKIN_OF_LUCK && type <= ITEM_JACK_OLANTERN_DRINK) gold = item.durability * 50;

  if (hasTag(p, 'gambleItem')) gold = 2000000;

  // A Dark Lord pet: giPetManager::GetPetItemValue, 2,000,000 (Dark Horse) or 1,000,000 (Dark Raven) a pet level.
  if (hasTag(p, 'darkLordPet')) {
    if (petLevel === undefined) return -1;
    gold = toDword(petLevel * (group === 13 && item.number === 4 ? 2000000 : 1000000));
  }

  const FIXED_1000 = [ITEM_SILVER_KEY, ITEM_GOLD_KEY, ITEM_SEALED_GOLDEN_BOX, ITEM_SEALED_SILVER_BOX, ITEM_GOLDEN_BOX,
    ITEM_SILVER_BOX, ITEM_PET_PANDA, ITEM_PANDA_TRANSFORMATION_RING, ITEM_DEMON, ITEM_SPIRIT_OF_GUARDIAN,
    ITEM_SAPPHIRE_RING, ITEM_RUBY_RING, ITEM_TOPAZ_RING, ITEM_AMETHYST_RING, ITEM_RUBY_NECKLACE, ITEM_EMERALD_NECKLACE,
    ITEM_SAPPHIRE_NECKLACE];
  const FIXED_2000 = [ITEM_SKELETON_TRANSFORMATION_RING, ITEM_PET_SKELETON];
  const SMALL_WINGS = [ITEM_SMALL_CAPE_OF_LORD, ITEM_SMALL_WING_OF_CURSE, ITEM_SMALL_WINGS_OF_ELF,
    ITEM_SMALL_WINGS_OF_HEAVEN, ITEM_SMALL_WINGS_OF_SATAN, ITEM_LITTLE_WARRIORS_CLOAK];
  if (FIXED_1000.includes(type)) gold = 1000;
  else if (FIXED_2000.includes(type)) gold = 2000;
  else if (SMALL_WINGS.includes(type)) gold = 80;

  // The items whose sell price doesn't drop with wear (the jumps to EXIT_CALCULATE).
  const keepsPrice = type === ITEM_TRANSFORMATION_RING || type === ITEM_WIZARDS_RING || type === ITEM_ARMOR_OF_GUARDSMAN
    || hasTag(p, 'ammunition') || type >= ITEM_POTION
    || (type >= ITEM_ORB_OF_TWISTING_SLASH && type <= ITEM_ORB_OF_DEATH_STAB)
    || (type >= ITEM_LOCHS_FEATHER && type <= ITEM_WEAPON_OF_ARCHANGEL) || type === ITEM_RENA
    || type === ITEM_SIEGE_POTION || type === ITEM_CONTRACT_SUMMON || type === ITEM_LIFE_STONE_ITEM
    || (type >= ITEM_OLD_SCROLL && type <= ITEM_SCROLL_OF_BLOOD)
    || FIXED_1000.includes(type) || FIXED_2000.includes(type) || SMALL_WINGS.includes(type)
    || (type >= t(13, 128) && type <= t(13, 134));

  if (!keepsPrice && goldType === 1 && !(type >= ITEM_SPLINTER_OF_ARMOR && type <= ITEM_HORN_OF_FENRIR)) {
    // Selling: 60% of the price times the share of durability lost comes off (float arithmetic, a DWORD).
    const max = maxDurability(item);
    const percent = Math.fround(1 - Math.fround(Math.fround(item.durability) / Math.fround(max)));
    let repairGold = toDword(Math.fround(Math.fround(Math.fround(gold) * Math.fround(0.6)) * percent));
    if (type === ITEM_SPIRIT) repairGold = 0;
    gold -= repairGold;
  }

  // EXIT_CALCULATE
  gold = roundGold(gold);
  if (hasTag(p, 'luckyItem')) gold = 0;
  return gold;
}

// ---------- CalcMaxDurability ----------

/** CalcMaxDurability: the item's full durability (a WORD), with its + level, ancient or excellent bonus. */
export function maxDurability(item: Item): number {
  const p = definitionOf(item);
  const type = itemTypeOf(item);
  let max = p?.durability ?? 0;
  // Staffs, sticks and books have a durability of their own for magic.
  if ((p?.magicDurability ?? 0) > 0) max = p?.magicDurability ?? 0;
  for (let i = 0; i < item.level; i++) {
    if (type >= ITEM_SCROLL_OF_BLOOD) break;
    if (i >= 14) max = max + 8 >= 255 ? 255 : max + 8;
    else if (i >= 13) max = max + 7 >= 255 ? 255 : max + 7;
    else if (i >= 12) max += 6;
    else if (i >= 11) max += 5;
    else if (i >= 10) max += 4;
    else if (i >= 9) max += 3;
    else if (i >= 4) max += 2;
    else max++;
    max &= 0xffff;
  }
  if (hasTag(p, 'darkLordPet')) max = 255;
  if (item.ancientSet > 0) max += 20;
  // Excellent, not the Divine Archangel weapons.
  else if (hasExcellentOptions(item, p) && !hasTag(p, 'divineArchangelWeapon')) max += 15;
  if (hasTag(p, 'luckyItem')) max = 255;
  return max & 0xffff;
}

// ---------- the repair cost ----------

/**
 * CalcRepairCost: the cost of repairing an item worth `value` (ItemValue(item, 2)) from `durability` to `max`:
 * 3.5 * value^0.75 * the share lost, + 1; 40% more at 0 durability (double for a Dark Lord pet); 2.5 times as much
 * with the inventory's own repair button (selfRepair) as at a repair NPC; to tens or hundreds.
 */
export function calcRepairCost(value: number, durability: number, max: number, darkLordPet: boolean, selfRepair: boolean): number {
  let repairGold = Math.min(value, 400000000);
  const percent = Math.fround(1 - Math.fround(Math.fround(durability) / Math.fround(max)));
  if (percent > 0) {
    const root = Math.sqrt(repairGold);
    const rootRoot = Math.sqrt(root);
    repairGold = Math.trunc(3.5 * root * rootRoot * percent) + 1;
    if (durability <= 0) repairGold = darkLordPet ? repairGold * 2 : Math.trunc(repairGold * 1.4);
  } else {
    repairGold = 0;
  }
  if (selfRepair) repairGold = Math.trunc(repairGold * 2.5);
  return roundGold(repairGold);
}

/**
 * The cost of repairing the item (ConvertRepairGold with ItemValue(item, 2)): at a repair NPC, or with the
 * inventory's own repair button (selfRepair, 2.5 times as much); the desktop picks the NPC's price while a repair
 * shop's window is open, the inventory's while only the inventory is. 0 for an item that can't be repaired (the data
 * says so, or it's a potion or above), a lucky item (as RepairAllGold has it), one at full durability, or a Dark Lord
 * pet without `petLevel`.
 */
export function repairCost(item: Item, selfRepair: boolean, petLevel?: number): number {
  const p = definitionOf(item);
  if (isRepairBan(item) || itemTypeOf(item) >= ITEM_POTION) return 0;
  // ItemValue gives a lucky item 0, which CalcRepairCost would still charge 1 for; RepairAllGold makes it 0.
  if (hasTag(p, 'luckyItem')) return 0;
  const max = maxDurability(item);
  if (item.durability >= max) return 0;
  const value = itemValue(item, 2, petLevel);
  if (value < 0) return 0;
  return calcRepairCost(value, item.durability, max, hasTag(p, 'darkLordPet'), selfRepair);
}

/**
 * RepairAllGold: the cost of repairing every item, over the equipment and the bag ([slot, item] pairs). The desktop
 * shows it on the repair NPC's "repair all" button, so it is the NPC's price unless `selfRepair`. `petLevel` prices
 * an equipped Dark Lord pet.
 */
export function repairAllCost(items: Iterable<[number, Item]>, selfRepair = false, petLevel?: (item: Item) => number | undefined): number {
  let total = 0;
  for (const [, item] of items) {
    if (isRepairBan(item)) continue;
    const p = definitionOf(item);
    const max = maxDurability(item);
    if (item.durability >= max) continue;
    const value = itemValue(item, 2, petLevel?.(item));
    if (value < 0) continue;
    let gold = calcRepairCost(value, item.durability, max, hasTag(p, 'darkLordPet'), selfRepair) | 0;
    if (hasTag(p, 'luckyItem')) gold = 0;
    total += gold;
  }
  return total;
}
