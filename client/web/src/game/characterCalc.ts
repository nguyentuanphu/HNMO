// The character's attack, wizardry, curse and defense values as the desktop client works them out for its character
// window: CHARACTER_MACHINE::CalculateAll and its parts (Engine/Object/ZzzInfomation.cpp), GetAttackDamage
// (Engine/Object/ZzzInventory.cpp), CCharacterManager::GetMagicSkillDamage / GetCurseSkillDamage
// (Character/CharacterManager.cpp) and the extra terms CCharacterInfoWindow::BuildAttributeLines adds
// (UI/Character/CharacterInfoWindow.cpp). Pure functions of the hero's stats and worn items.
//
// Not here (the web client has no such systems yet; each counts as 0 / off): buffs and debuffs, ancient set options,
// socket options, master skills and the master level, the quest's ABILITY_PLUS_DAMAGE, period items (counted as not
// expired), the Dark Horse's and the Dark Raven's pet level, and the party's attack-rate share.

import type { ItemCombatValues } from './itemTooltip.js';

/** Equipment slots (EQUIPMENT_*, _define.h). */
export const Slot = {
  RIGHT: 0, LEFT: 1, HELM: 2, ARMOR: 3, PANTS: 4, GLOVES: 5, BOOTS: 6, WING: 7, HELPER: 8, AMULET: 9,
  RING_RIGHT: 10, RING_LEFT: 11,
} as const;
export const EQUIPMENT_COUNT = 12;

/** Base classes (CLASS_*), the web client's class index. */
export const BaseClass = { WIZARD: 0, KNIGHT: 1, ELF: 2, DARK: 3, DARK_LORD: 4, SUMMONER: 5, RAGE_FIGHTER: 6 } as const;

const ITEMS_PER_GROUP = 512;
const type = (group: number, number: number) => group * ITEMS_PER_GROUP + number;
const ITEM = {
  BOLT: type(4, 7), ARROWS: type(4, 15), IMP: type(13, 1), HORN_OF_FENRIR: type(13, 37),
  ELITE_TRANSFER_SKELETON_RING: type(13, 39), CHRISTMAS_TRANSFORMATION_RING: type(13, 41), DEMON: type(13, 64),
  SPIRIT_OF_GUARDIAN: type(13, 65), PANDA_TRANSFORMATION_RING: type(13, 76), PET_PANDA: type(13, 80),
  PET_UNICORN: type(13, 106), SKELETON_TRANSFORMATION_RING: type(13, 122), PET_SKELETON: type(13, 123),
  CAPE_OF_LORD: type(13, 30),
} as const;
// The Magic Gladiator's armours that make a set without a helm (CheckFullSet, BuildAttributeLines).
const GLADIATOR_SET_ARMORS = [15, 20, 23, 32, 37, 47, 48].map((number) => type(8, number));
// Summoning skills: the curse spell adds their damage, the wizardry doesn't (AT_SKILL_SUMMON_EXPLOSION..REQUIEM).
const SUMMON_EXPLOSION = 223;
const SUMMON_REQUIEM = 224;
// An excellent hit: the maximum damage times this (GetExcellentHitDamage).
const EXCELLENT_DAMAGE_FACTOR = 1.2;

/** The hero as the sums read it (CharacterAttribute). */
export interface HeroStats {
  /** 0 Dark Wizard … 6 Rage Fighter. */
  classIndex: number;
  /** 1 first, 2 second, 3 third class (GetStepClass). */
  classLevel: number;
  level: number;
  strength: number;
  agility: number;
  vitality: number;
  energy: number;
  leadership: number;
}

/** A worn item: its state and what it gives (game/itemTooltip.ts itemCombatValues). */
export interface WornItem {
  level: number;
  durability: number;
  /** The excellent byte (ExcellentFlags). */
  excellent: number;
  /** The ancient set byte (AncientDiscriminator). */
  ancient: number;
  values: ItemCombatValues;
}

/** The worn items by slot (Slot); null for an empty slot. */
export type WornEquipment = readonly (WornItem | null)[];

/** What CalculateAll leaves in CharacterAttribute for the window. */
export interface CharacterMachine {
  addCharisma: number;
  attackDamageMinRight: number; attackDamageMaxRight: number;
  attackDamageMinLeft: number; attackDamageMaxLeft: number;
  magicDamageMin: number; magicDamageMax: number;
  curseDamageMin: number; curseDamageMax: number;
  attackRating: number; attackRatingPK: number;
  successfulBlocking: number; successfulBlockingPK: number;
  defense: number;
}

const word = (value: number) => Math.trunc(value) & 0xffff;
const isEmpty = (item: WornItem | null): item is null => item === null;
const kindOf = (item: WornItem | null) => item?.values.weaponKind ?? null;
const typeOf = (item: WornItem | null) => item?.values.type ?? -1;
const hasTag = (item: WornItem | null, tag: string) => item?.values.tags.includes(tag) ?? false;
const isStaffOrBook = (item: WornItem | null) => kindOf(item) === 'staff' || kindOf(item) === 'book';
const isAmmunition = (item: WornItem | null) => hasTag(item, 'ammunition');
const isBowCrossbowOrAmmunition = (item: WornItem | null) =>
  kindOf(item) === 'bow' || kindOf(item) === 'crossbow' || isAmmunition(item);
const WEAPON_KINDS = ['sword', 'glove', 'axe', 'mace', 'spear', 'bow', 'crossbow', 'staff', 'book'];
const isWeaponOrAmmunition = (item: WornItem | null) => WEAPON_KINDS.includes(kindOf(item) ?? '') || isAmmunition(item);
const isSwordAxeMaceOrGlove = (item: WornItem | null) => ['sword', 'axe', 'mace', 'glove'].includes(kindOf(item) ?? '');
const itemNumber = (item: WornItem | null) => (item ? item.values.type % ITEMS_PER_GROUP : -1);

/** CalcDurabilityPercent: how much a worn-down item loses, 0 to 0.5. */
export function calcDurabilityPercent(durability: number, maxDurability: number, level: number, excellentFlags: number,
  ancient: number): number {
  let max = maxDurability;
  for (let i = 0; i < level; i++) max += i >= 4 ? 2 : 1;
  if (ancient > 0) max += 20;
  else if ((excellentFlags & 63) > 0) max += 15;
  const lost = 1 - durability / max;
  if (lost > 0.8) return 0.5;
  if (lost > 0.7) return 0.3;
  if (lost > 0.5) return 0.2;
  return 0;
}

const durabilityPercent = (item: WornItem, wing = false) =>
  calcDurabilityPercent(item.durability, item.values.baseDurability, item.level, wing ? 0 : item.excellent, wing ? 0 : item.ancient);

/** IsRequireEquipItem: whether the hero can wear the item (class, class step, stats and level). */
export function isRequireEquipItem(item: WornItem, hero: HeroStats, addCharisma = 0): boolean {
  const classes = item.values.classes;
  let equipable = (classes[hero.classIndex] ?? 0) > 0;
  if (!equipable && hero.classIndex === BaseClass.DARK && (classes[BaseClass.WIZARD] ?? 0) > 0
    && (classes[BaseClass.KNIGHT] ?? 0) > 0) equipable = true;
  if ((classes[hero.classIndex] ?? 0) > hero.classLevel) return false;
  if (!equipable) return false;
  const { require, harmony } = item.values;
  if (require.strength - harmony.strength > hero.strength) return false;
  if (require.dexterity - harmony.dexterity > hero.agility) return false;
  if (require.energy > hero.energy) return false;
  if (require.vitality > hero.vitality) return false;
  if (require.leadership > hero.leadership + addCharisma) return false;
  if (require.level > hero.level) return false;
  return true;
}

/** AreItemOptionsActive: worn, not broken and wearable. */
export function areItemOptionsActive(item: WornItem | null, hero: HeroStats): item is WornItem {
  return item !== null && item.durability !== 0 && isRequireEquipItem(item, hero);
}

/** PlusSpecial: the value plus the item's options of an effect. */
export function plusSpecial(value: number, effect: string, item: WornItem | null, hero: HeroStats): number {
  if (!areItemOptionsActive(item, hero)) return value;
  for (const option of item.values.options) if (option.effect === effect) value = word(value + option.value);
  return value;
}

/** PlusSpecialPercentOfSlot: the value raised by the item's per cent options of an effect, up to 65535. */
export function plusSpecialPercent(value: number, effect: string, item: WornItem | null, hero: HeroStats): number {
  if (!areItemOptionsActive(item, hero)) return value;
  for (const option of item.values.options) {
    if (option.effect === effect) value = Math.min(value + Math.trunc(value * option.value / 100), 0xffff);
  }
  return value;
}

// GetEquipedBowType(): a bow in the left hand, a crossbow in the right.
function bowType(equipment: WornEquipment): 'bow' | 'crossbow' | 'none' {
  if (kindOf(equipment[Slot.LEFT]) === 'bow') return 'bow';
  if (kindOf(equipment[Slot.RIGHT]) === 'crossbow') return 'crossbow';
  return 'none';
}

interface AttackDamage { minRight: number; maxRight: number; minLeft: number; maxLeft: number }

// CalculateDamage.
function calculateDamage(hero: HeroStats, equipment: WornEquipment): AttackDamage {
  const right = equipment[Slot.RIGHT], left = equipment[Slot.LEFT];
  const { strength, agility: dexterity, energy, vitality } = hero;
  const d = (a: number, b: number) => Math.trunc(a / b);
  let min: number, max: number;
  if ((kindOf(left) === 'bow' && left?.durability !== 0) || (kindOf(right) === 'crossbow' && right?.durability !== 0)) {
    min = d(dexterity, 7) + d(strength, 14);
    max = d(dexterity, 4) + d(strength, 8);
  } else {
    switch (hero.classIndex) {
      case BaseClass.ELF: case BaseClass.SUMMONER: min = d(strength + dexterity, 7); max = d(strength + dexterity, 4); break;
      case BaseClass.KNIGHT: min = d(strength, 6); max = d(strength, 4); break;
      case BaseClass.DARK: min = d(strength, 6) + d(energy, 12); max = d(strength, 4) + d(energy, 9); break;
      case BaseClass.DARK_LORD: min = d(strength, 7) + d(energy, 14); max = d(strength, 5) + d(energy, 10); break;
      case BaseClass.RAGE_FIGHTER: min = d(strength, 7) + d(vitality, 15); max = d(strength, 5) + d(vitality, 12); break;
      default: min = d(strength, 8); max = d(strength, 4); break;
    }
  }
  const damage: AttackDamage = { minRight: word(min), maxRight: word(max), minLeft: word(min), maxLeft: word(max) };

  const wing = equipment[Slot.WING];
  if (wing && wing.values.baseDurability !== 0) {
    const percent = durabilityPercent(wing, true);
    let wingMin = plusSpecial(0, 'improveDamage', wing, hero);
    let wingMax = plusSpecial(0, 'improveDamage', wing, hero);
    wingMin -= word(wingMin * percent);
    wingMax -= word(wingMax * percent);
    damage.minRight = word(damage.minRight + wingMin); damage.maxRight = word(damage.maxRight + wingMax);
    damage.minLeft = word(damage.minLeft + wingMin); damage.maxLeft = word(damage.maxLeft + wingMax);
  }

  if (right && right.durability !== 0) {
    const percent = durabilityPercent(right);
    let weaponMin = plusSpecial(right.values.damageMin, 'improveDamage', right, hero);
    let weaponMax = plusSpecial(right.values.damageMax, 'improveDamage', right, hero);
    weaponMin = word(weaponMin - word(weaponMin * percent));
    weaponMax = word(weaponMax - word(weaponMax * percent));
    if (isStaffOrBook(right)) {
      damage.minLeft = word(damage.minLeft + weaponMin); damage.maxLeft = word(damage.maxLeft + weaponMax);
    } else {
      damage.minRight = word(damage.minRight + weaponMin); damage.maxRight = word(damage.maxRight + weaponMax);
    }
    damage.minRight = plusSpecialPercent(plusSpecial(damage.minRight, 'improveDamageLevel', right, hero), 'improveDamagePercent', right, hero);
    damage.maxRight = plusSpecialPercent(plusSpecial(damage.maxRight, 'improveDamageLevel', right, hero), 'improveDamagePercent', right, hero);
  }

  if (left && left.durability !== 0) {
    const percent = durabilityPercent(left);
    let weaponMin = plusSpecial(left.values.damageMin, 'improveDamage', left, hero);
    let weaponMax = plusSpecial(left.values.damageMax, 'improveDamage', left, hero);
    weaponMin = word(weaponMin - word(weaponMin * percent));
    weaponMax = word(weaponMax - word(weaponMax * percent));
    damage.minLeft = word(damage.minLeft + weaponMin); damage.maxLeft = word(damage.maxLeft + weaponMax);
    damage.minLeft = plusSpecialPercent(plusSpecial(damage.minLeft, 'improveDamageLevel', left, hero), 'improveDamagePercent', left, hero);
    damage.maxLeft = plusSpecialPercent(plusSpecial(damage.maxLeft, 'improveDamageLevel', left, hero), 'improveDamagePercent', left, hero);
  }

  const amulet = equipment[Slot.AMULET];
  if (amulet && amulet.durability !== 0) {
    const raise = (value: number) => plusSpecialPercent(plusSpecial(value, 'improveDamageLevel', amulet, hero), 'improveDamagePercent', amulet, hero);
    damage.minRight = raise(damage.minRight); damage.maxRight = raise(damage.maxRight);
    damage.minLeft = raise(damage.minLeft); damage.maxLeft = raise(damage.maxLeft);
  }
  for (const ring of [equipment[Slot.RING_RIGHT], equipment[Slot.RING_LEFT]]) {
    if (!ring || ring.durability === 0) continue;
    const raise = (value: number) => plusSpecialPercent(value, 'improveDamagePercent', ring, hero);
    damage.minRight = raise(damage.minRight); damage.maxRight = raise(damage.maxRight);
    damage.minLeft = raise(damage.minLeft); damage.maxLeft = raise(damage.maxLeft);
  }

  // Bolts and arrows above +0 raise the bow's damage.
  if (isBowCrossbowOrAmmunition(right) && isBowCrossbowOrAmmunition(left)) {
    if (typeOf(left) === ITEM.BOLT && (left?.level ?? 0) >= 1) {
      const factor = ((left?.level ?? 0) * 2 + 1) * 0.01;
      damage.minRight = word(damage.minRight + word(damage.minRight * factor + 1));
      damage.maxRight = word(damage.maxRight + word(damage.maxRight * factor + 1));
    } else if (typeOf(right) === ITEM.ARROWS && (right?.level ?? 0) >= 1) {
      const factor = ((right?.level ?? 0) * 2 + 1) * 0.01;
      damage.minLeft = word(damage.minLeft + word(damage.minLeft * factor + 1));
      damage.maxLeft = word(damage.maxLeft + word(damage.maxLeft * factor + 1));
    }
  }
  return damage;
}

// CalculateMagicDamage.
function calculateMagicDamage(hero: HeroStats, equipment: WornEquipment): { min: number; max: number } {
  let min = Math.trunc(hero.energy / 9), max = Math.trunc(hero.energy / 4);
  const right = equipment[Slot.RIGHT], left = equipment[Slot.LEFT];
  const wing = equipment[Slot.WING];
  if (wing) {
    const percent = calcDurabilityPercent(wing.durability, wing.values.baseDurability, wing.level, 0, 0);
    const value = plusSpecial(0, 'improveMagic', wing, hero);
    min = word(min + value - word(value * percent));
    max = word(max + value - word(value * percent));
  }
  // The option of the damage kind its wizardry takes: a magic sword's damage option, else its wizardry option.
  const optionOf = (item: WornItem | null) => plusSpecial(0, item?.values.raisesWizardry ? 'improveDamage' : 'improveMagic', item, hero);
  const raiseByLevel = (item: WornItem | null) => {
    min = plusSpecialPercent(plusSpecial(min, 'improveMagicLevel', item, hero), 'improveMagicPercent', item, hero);
    max = plusSpecialPercent(plusSpecial(max, 'improveMagicLevel', item, hero), 'improveMagicPercent', item, hero);
  };
  if (right && right.durability !== 0) {
    const percent = durabilityPercent(right);
    const value = optionOf(right);
    min = word(min + value - word(value * percent));
    max = word(max + value - word(value * percent));
    raiseByLevel(right);
  }
  if (left && left.durability !== 0 && hero.classIndex !== BaseClass.SUMMONER) {
    const percent = durabilityPercent(left);
    if (isWeaponOrAmmunition(left)) {
      const value = optionOf(left);
      if (hero.classIndex !== BaseClass.DARK) {
        min = word(min + value - word(value * percent));
        max = word(max + value - word(value * percent));
      }
      raiseByLevel(left);
    }
    // The desktop adds the right weapon's option again with the left's wear, and the left's level options again.
    const value = optionOf(right);
    if (hero.classIndex !== BaseClass.DARK) {
      min = word(min + value - word(value * percent));
      max = word(max + value - word(value * percent));
    }
    raiseByLevel(left);
  }
  const amulet = equipment[Slot.AMULET];
  if (amulet && amulet.durability !== 0) raiseByLevel(amulet);
  for (const ring of [equipment[Slot.RING_RIGHT], equipment[Slot.RING_LEFT]]) {
    if (!ring || ring.durability === 0) continue;
    min = plusSpecialPercent(min, 'improveMagicPercent', ring, hero);
    max = plusSpecialPercent(max, 'improveMagicPercent', ring, hero);
  }
  return { min, max };
}

// CalculateCurseDamage (the Summoner only).
function calculateCurseDamage(hero: HeroStats, equipment: WornEquipment): { min: number; max: number } {
  if (hero.classIndex !== BaseClass.SUMMONER) return { min: 0, max: 0 };
  let min = Math.trunc(hero.energy / 9), max = Math.trunc(hero.energy / 4);
  const wing = equipment[Slot.WING];
  if (wing) {
    const percent = calcDurabilityPercent(wing.durability, wing.values.baseDurability, wing.level, 0, 0);
    let value = plusSpecial(0, 'improveCurse', wing, hero);
    value -= word(value * percent);
    min = word(min + value); max = word(max + value);
  }
  const left = equipment[Slot.LEFT];
  if (left && left.durability !== 0) {
    const percent = durabilityPercent(left);
    const value = plusSpecial(0, 'improveCurse', left, hero);
    min = word(min + value - word(value * percent));
    max = word(max + value - word(value * percent));
    min = plusSpecialPercent(plusSpecial(min, 'improveMagicLevel', left, hero), 'improveMagicPercent', left, hero);
    max = plusSpecialPercent(plusSpecial(max, 'improveMagicLevel', left, hero), 'improveMagicPercent', left, hero);
  }
  return { min, max };
}

// CalculateAttackRating.
function calculateAttackRating(hero: HeroStats, charisma: number): number {
  const { level, strength, agility: dexterity } = hero;
  const d = (a: number, b: number) => Math.trunc(a / b);
  if (hero.classIndex === BaseClass.DARK_LORD) return word(level * 5 + d(dexterity * 5, 2) + d(strength, 6) + d(charisma, 10));
  if (hero.classIndex === BaseClass.RAGE_FIGHTER) return word(level * 3 + d(dexterity * 5, 4) + d(strength, 6));
  return word(level * 5 + d(dexterity * 3, 2) + d(strength, 4));
}

// CalculateAttackRatingPK.
function calculateAttackRatingPK(hero: HeroStats): number {
  const { level, agility: dexterity } = hero;
  const byClass: Record<number, number> = {
    [BaseClass.KNIGHT]: level * 3 + dexterity * 4.5, [BaseClass.DARK_LORD]: level * 3 + dexterity * 4,
    [BaseClass.ELF]: level * 3 + dexterity * 0.6, [BaseClass.DARK]: level * 3 + dexterity * 3.5,
    [BaseClass.SUMMONER]: level * 3 + dexterity * 3.5, [BaseClass.WIZARD]: level * 3 + dexterity * 4,
    [BaseClass.RAGE_FIGHTER]: level * 2.6 + dexterity * 3.6,
  };
  return word(Math.fround(byClass[hero.classIndex] ?? 0));
}

// CalculateSuccessfulBlockingPK.
function calculateSuccessfulBlockingPK(hero: HeroStats): number {
  const { level, agility: dexterity } = hero;
  const byClass: Record<number, number> = {
    [BaseClass.KNIGHT]: level * 2 + dexterity * 0.5, [BaseClass.DARK_LORD]: level * 2 + dexterity * 0.5,
    [BaseClass.ELF]: level * 2 + dexterity * 0.1, [BaseClass.DARK]: level * 2 + dexterity * 0.25,
    [BaseClass.WIZARD]: level * 2 + dexterity * 0.25, [BaseClass.SUMMONER]: level * 2 + dexterity * 0.5,
    [BaseClass.RAGE_FIGHTER]: level * 1.5 + dexterity * 0.2,
  };
  return word(Math.fround(byClass[hero.classIndex] ?? 0));
}

// CalculateSuccessfulBlocking.
function calculateSuccessfulBlocking(hero: HeroStats, equipment: WornEquipment): number {
  const dexterity = hero.agility;
  const divisor = hero.classIndex === BaseClass.ELF || hero.classIndex === BaseClass.SUMMONER ? 4
    : hero.classIndex === BaseClass.DARK_LORD ? 7 : hero.classIndex === BaseClass.RAGE_FIGHTER ? 10 : 3;
  let blocking = Math.trunc(dexterity / divisor);
  const left = equipment[Slot.LEFT];
  if (left && left.durability !== 0) {
    const percent = durabilityPercent(left);
    blocking = word(blocking + left.values.blockRate - word(left.values.blockRate * percent));
    const option = plusSpecial(0, 'improveBlocking', left, hero);
    blocking = word(blocking + option - word(option * percent));
    blocking = plusSpecialPercent(blocking, 'improveBlockingPercent', left, hero);
  }
  for (const slot of [Slot.HELM, Slot.ARMOR, Slot.PANTS, Slot.GLOVES, Slot.BOOTS, Slot.RING_LEFT, Slot.RING_RIGHT]) {
    blocking = plusSpecialPercent(blocking, 'improveBlockingPercent', equipment[slot], hero);
  }
  return blocking;
}

/** FullSetOf for the hero: the lowest level of a full set of +9 or more (0 without one), and whether it adds defense. */
export function fullSetLevel(hero: HeroStats, equipment: WornEquipment): { levelSet: number; addDefense: boolean } {
  const end = hero.classIndex === BaseClass.DARK ? Slot.ARMOR : Slot.HELM;
  const counted = (slot: number) => !(hero.classIndex === BaseClass.RAGE_FIGHTER && slot === Slot.GLOVES);
  let success = true;
  let levelSet = 0;
  for (let slot: number = Slot.BOOTS; slot >= end; slot--) {
    if (counted(slot) && isEmpty(equipment[slot])) { success = false; break; }
  }
  if (success) {
    const number = itemNumber(equipment[Slot.BOOTS]);
    let lowest = equipment[Slot.BOOTS]?.level ?? 0;
    for (let slot: number = Slot.BOOTS; slot >= end; slot--) {
      if (!counted(slot)) continue;
      const level = equipment[slot]?.level ?? 0;
      if (level < 9 || number !== itemNumber(equipment[slot])) { levelSet = 0; success = false; break; }
      if (lowest >= level) { lowest = level; levelSet = level; }
    }
  }
  let addDefense = true;
  if (hero.classIndex === BaseClass.DARK && success && !GLADIATOR_SET_ARMORS.includes(typeOf(equipment[Slot.ARMOR]))) {
    addDefense = false;
  }
  return { levelSet, addDefense };
}

// CalculateDefense.
function calculateDefense(hero: HeroStats, equipment: WornEquipment): number {
  const dexterity = hero.agility;
  const divisor = hero.classIndex === BaseClass.ELF ? 10
    : hero.classIndex === BaseClass.KNIGHT || hero.classIndex === BaseClass.SUMMONER ? 3
      : hero.classIndex === BaseClass.WIZARD ? 4 : hero.classIndex === BaseClass.DARK_LORD ? 7
        : hero.classIndex === BaseClass.RAGE_FIGHTER ? 8 : 5;
  let defense = Math.trunc(dexterity / divisor);
  let worn = 0;
  for (let slot: number = Slot.LEFT; slot <= Slot.WING; slot++) {
    const item = equipment[slot];
    if (!item || item.durability === 0) continue;
    let value = plusSpecial(item.values.defense, 'improveDefense', item, hero);
    value -= word(value * durabilityPercent(item, slot === Slot.WING));
    worn = word(worn + value);
  }
  defense = word(defense + worn);
  const { levelSet, addDefense } = fullSetLevel(hero, equipment);
  if (addDefense) {
    const bonus = [0.05, 0.1, 0.15, 0.2, 0.25, 0.3][levelSet - 10] ?? 0;
    defense = word(defense + word(defense * Math.fround(bonus)));
  }
  return defense;
}

/** CalculateAll: the hero's attack, wizardry, curse, rates and defense from the stats and the worn items. */
export function calculateAll(hero: HeroStats, equipment: WornEquipment): CharacterMachine {
  // The Cape of Lord's command option.
  const wing = equipment[Slot.WING];
  const addCharisma = typeOf(wing) === ITEM.CAPE_OF_LORD ? plusSpecial(0, 'improveCharisma', wing, hero) : 0;
  const damage = calculateDamage(hero, equipment);
  const magic = calculateMagicDamage(hero, equipment);
  const curse = calculateCurseDamage(hero, equipment);
  return {
    addCharisma,
    attackDamageMinRight: damage.minRight, attackDamageMaxRight: damage.maxRight,
    attackDamageMinLeft: damage.minLeft, attackDamageMaxLeft: damage.maxLeft,
    magicDamageMin: magic.min, magicDamageMax: magic.max,
    curseDamageMin: curse.min, curseDamageMax: curse.max,
    attackRating: calculateAttackRating(hero, hero.leadership + addCharisma),
    attackRatingPK: calculateAttackRatingPK(hero),
    successfulBlocking: calculateSuccessfulBlocking(hero, equipment),
    successfulBlockingPK: calculateSuccessfulBlockingPK(hero),
    defense: calculateDefense(hero, equipment),
  };
}

/** GetAttackDamage: the damage the character window shows, and whether two weapons or upgraded ammunition count. */
export function attackDamage(hero: HeroStats, equipment: WornEquipment, machine: CharacterMachine):
  { min: number; max: number; boosted: boolean } {
  const right = equipment[Slot.RIGHT], left = equipment[Slot.LEFT];
  const bow = bowType(equipment);
  let min: number, max: number;
  if (bow === 'crossbow') { min = machine.attackDamageMinRight; max = machine.attackDamageMaxRight; }
  else if (bow === 'bow' || isEmpty(right) || isStaffOrBook(right)) { min = machine.attackDamageMinLeft; max = machine.attackDamageMaxLeft; }
  else { min = machine.attackDamageMinRight; max = machine.attackDamageMaxRight; }
  let boosted = false;
  const d = (a: number, b: number) => Math.trunc(a / b);
  if (hero.classIndex === BaseClass.KNIGHT || hero.classIndex === BaseClass.DARK) {
    if (isWeaponOrAmmunition(left) && isWeaponOrAmmunition(right)) {
      boosted = true;
      min = d(machine.attackDamageMinRight * 55, 100) + d(machine.attackDamageMinLeft * 55, 100);
      max = d(machine.attackDamageMaxRight * 55, 100) + d(machine.attackDamageMaxLeft * 55, 100);
    }
  } else if (hero.classIndex === BaseClass.ELF) {
    if (isBowCrossbowOrAmmunition(right) && isBowCrossbowOrAmmunition(left)
      && ((typeOf(left) === ITEM.BOLT && (left?.level ?? 0) >= 1) || (typeOf(right) === ITEM.ARROWS && (right?.level ?? 0) >= 1))) {
      boosted = true;
    }
  } else if (hero.classIndex === BaseClass.RAGE_FIGHTER) {
    if (isSwordAxeMaceOrGlove(left) && isSwordAxeMaceOrGlove(right)) {
      boosted = true;
      min = d((machine.attackDamageMinRight + machine.attackDamageMinLeft) * 60, 100);
      max = d((machine.attackDamageMaxRight + machine.attackDamageMaxLeft) * 65, 100);
    }
  }
  return { min, max, boosted };
}

/** The skill table's part a skill adds (SKILL_ATTRIBUTE::Damage); null for no skill. */
export interface SkillDamage { number: number; damage: number }

/** GetMagicSkillDamage: the wizardry damage with the current skill. */
export function magicSkillDamage(machine: CharacterMachine, skill: SkillDamage | null): { min: number; max: number } {
  if (skill && skill.number >= SUMMON_EXPLOSION && skill.number <= SUMMON_REQUIEM) {
    return { min: machine.magicDamageMin, max: machine.magicDamageMax };
  }
  const damage = skill?.damage ?? 0;
  return { min: machine.magicDamageMin + damage, max: machine.magicDamageMax + damage + Math.trunc(damage / 2) };
}

/** GetCurseSkillDamage: the curse damage with the current skill (the Summoner's). */
export function curseSkillDamage(machine: CharacterMachine, skill: SkillDamage | null): { min: number; max: number } {
  if (skill && skill.number >= SUMMON_EXPLOSION && skill.number <= SUMMON_REQUIEM) {
    return { min: machine.curseDamageMin + skill.damage, max: machine.curseDamageMax + skill.damage + Math.trunc(skill.damage / 2) };
  }
  return { min: machine.curseDamageMin, max: machine.curseDamageMax };
}

// ---------- the window's numbers (CCharacterInfoWindow::BuildAttributeLines) ----------

/** Where a line's number comes from: the theme colours "boosted" (an item adds) and "buffed" (a consumable or pet). */
export type LineSource = 'base' | 'boosted' | 'buffed';

/** What the character window shows below the stats, as numbers. */
export interface CharacterSheet {
  /** The hero's command with what the Cape of Lord adds. */
  charisma: number;
  addCharisma: number;
  damage: { min: number; max: number; rating: number; source: LineSource };
  /** The minimum and maximum the damage line shows, for its hint. */
  damageRange: { minimum: number; maximum: number };
  attackRatingPK: number;
  /** The guardian options' and harmony's part of the PvP attack rate (0: none). */
  attackRatingPKBonus: number;
  defense: { value: number; rate: number; fullSet: boolean; fullSetRate: number; fullSetBase: number };
  defenseRatePK: number;
  defenseRatePKBonus: number;
  attackSpeedSource: LineSource;
  lifeSource: LineSource;
  meleeDamage: number | null;
  magic: { min: number; max: number; staffBonus: number | null } | null;
  curse: { min: number; max: number; bookBonus: number | null } | null;
  skillDamage: number | null;
  divineDamage: number | null;
  areaDamage: number | null;
  /** The critical damage rate: luck's per cent and how many worn items have luck. */
  criticalRate: { luck: number; luckItems: number };
  /** Critical damage the harmony options add. */
  criticalDamage: { harmony: number };
  criticalHit: number;
  excellentHit: number;
}

function transformationRingBonus(equipment: WornEquipment, values: Partial<Record<number, number>>): number {
  let bonus = 0;
  for (const ring of [equipment[Slot.RING_LEFT], equipment[Slot.RING_RIGHT]]) {
    bonus = Math.max(bonus, values[typeOf(ring)] ?? 0);
  }
  return bonus;
}

/** The numbers of the window's lines (BuildAttributeLines and BuildCriticalHits); `skill` is the current skill. */
export function characterSheet(hero: HeroStats, equipment: WornEquipment, skill: SkillDamage | null): CharacterSheet {
  const machine = calculateAll(hero, equipment);
  const right = equipment[Slot.RIGHT], left = equipment[Slot.LEFT];
  const helper = equipment[Slot.HELPER];
  const helperType = typeOf(helper);
  const d = (a: number, b: number) => Math.trunc(a / b);

  // Strength: the damage and the attack rates.
  const attack = attackDamage(hero, equipment, machine);
  let minimum = attack.min, maximum = attack.max;
  let minIndex = 0, maxIndex = 0, attackPowerRate = 0;
  for (const weapon of [right, left]) {
    if (!weapon) continue;
    minIndex += weapon.values.harmony.minAttack;
    maxIndex += weapon.values.harmony.maxAttack;
    attackPowerRate += weapon.values.harmony.attackRate;
  }
  let defenseRateBonus = 0, guardianAttack = 0, guardianDefense = 0, harmonyDefense = 0, harmonyMagic = 0;
  for (let slot = 0; slot < EQUIPMENT_COUNT; slot++) {
    const item = equipment[slot];
    if (!item) continue;
    if (slot >= Slot.LEFT) defenseRateBonus += item.values.harmony.defenseRate;
    guardianAttack += item.values.guardianAttackRate;
    guardianDefense += item.values.guardianDefenseRate;
    harmonyDefense += item.values.harmony.defense;
    harmonyMagic += item.values.harmony.magicalPower;
  }
  const ringAttack = transformationRingBonus(equipment, {
    [ITEM.CHRISTMAS_TRANSFORMATION_RING]: 20, [ITEM.PANDA_TRANSFORMATION_RING]: 30, [ITEM.SKELETON_TRANSFORMATION_RING]: 40,
  });
  minimum += ringAttack; maximum += ringAttack;
  if (helper) {
    if (helperType === ITEM.HORN_OF_FENRIR && helper.excellent === 0x04) { minimum += d(hero.level, 12); maximum += d(hero.level, 12); }
    const factor = helperType === ITEM.DEMON ? 0.4 : helperType === ITEM.PET_SKELETON ? 0.2 : helperType === ITEM.IMP ? 0.3 : 0;
    if (factor) { minimum += Math.trunc(minimum * Math.fround(factor)); maximum += Math.trunc(maximum * Math.fround(factor)); }
  }
  const shownMaximum = maximum + maxIndex;
  const shownMinimum = minimum + minIndex >= shownMaximum ? shownMaximum : minimum + minIndex;

  // Agility: the defense, its rate (and the full set's), the speed and the PvP defense rate.
  const fullSet = wearsDefenseSet(hero, equipment);
  let changeRing = 0;
  if (typeOf(equipment[Slot.RING_LEFT]) === ITEM.ELITE_TRANSFER_SKELETON_RING
    || typeOf(equipment[Slot.RING_RIGHT]) === ITEM.ELITE_TRANSFER_SKELETON_RING) {
    changeRing = d(machine.defense + harmonyDefense, 10);
  }
  if (helperType === ITEM.PET_PANDA) changeRing += 50;
  if (helperType === ITEM.PET_UNICORN) changeRing += 50;
  const helperWorks = helper !== null && helper.durability !== 0 && isRequireEquipItem(helper, hero, machine.addCharisma);

  // Energy: wizardry, curse and the skill damage lines.
  const wizardryClass = hero.classIndex === BaseClass.WIZARD || hero.classIndex === BaseClass.DARK || hero.classIndex === BaseClass.SUMMONER;
  let magic: CharacterSheet['magic'] = null;
  if (wizardryClass) {
    const initial = magicSkillDamage(machine, skill);
    let min = initial.min, max = initial.max;
    const ring = transformationRingBonus(equipment, {
      [ITEM.CHRISTMAS_TRANSFORMATION_RING]: 20, [ITEM.PANDA_TRANSFORMATION_RING]: 30, [ITEM.SKELETON_TRANSFORMATION_RING]: 40,
    });
    min += ring; max += ring;
    if (helper) {
      if (helperType === ITEM.HORN_OF_FENRIR && helper.excellent === 0x04) { min += d(hero.level, 25); max += d(hero.level, 25); }
      const factor = helperType === ITEM.DEMON ? 0.4 : helperType === ITEM.PET_SKELETON ? 0.2 : helperType === ITEM.IMP ? 0.3 : 0;
      if (factor) { min += Math.trunc(min * Math.fround(factor)); max += Math.trunc(max * Math.fround(factor)); }
    }
    let staffBonus: number | null = null;
    if (right && (isStaffOrBook(right) || hasTag(right, 'magicSword'))) {
      let magicPercent = Math.fround(right.values.magicPower / 100);
      const percent = calcDurabilityPercent(right.durability, right.values.baseMagicDurability, right.level, right.excellent, right.ancient);
      magicPercent = Math.fround(magicPercent - magicPercent * percent);
      staffBonus = Math.trunc((initial.max + harmonyMagic) * magicPercent);
    }
    magic = { min: min + harmonyMagic, max: max + harmonyMagic, staffBonus };
  }
  let curse: CharacterSheet['curse'] = null;
  if (hero.classIndex === BaseClass.SUMMONER) {
    const initial = curseSkillDamage(machine, skill);
    let min = initial.min, max = initial.max;
    const ring = transformationRingBonus(equipment, { [ITEM.PANDA_TRANSFORMATION_RING]: 30, [ITEM.SKELETON_TRANSFORMATION_RING]: 40 });
    min += ring; max += ring;
    const factor = helperType === ITEM.DEMON ? 0.4 : helperType === ITEM.PET_SKELETON ? 0.2 : 0;
    if (factor) { min += Math.trunc(min * Math.fround(factor)); max += Math.trunc(max * Math.fround(factor)); }
    let bookBonus: number | null = null;
    if (left && hasTag(left, 'summonerBook')) {
      let cursePercent = Math.fround(left.values.magicPower / 100);
      const percent = calcDurabilityPercent(left.durability, left.values.baseMagicDurability, left.level, left.excellent, left.ancient);
      cursePercent = Math.fround(cursePercent - cursePercent * percent);
      bookBonus = Math.trunc(max * cursePercent);
    }
    curse = { min, max, bookBonus };
  }
  const energy = hero.energy;
  const skillDamage = hero.classIndex === BaseClass.KNIGHT ? 200 + d(energy, 10)
    : hero.classIndex === BaseClass.DARK ? 200 : hero.classIndex === BaseClass.DARK_LORD ? 200 + d(energy, 20) : null;

  // The critical hits (GetHeroCriticalDamageRate, GetHeroCriticalDamageBonus): luck and harmony.
  let luck = 0, luckItems = 0, harmonyCritical = 0;
  for (const item of equipment) {
    const value = plusSpecial(0, 'luck', item, hero);
    if (value > 0) { luckItems++; luck += value; }
    if (areItemOptionsActive(item, hero)) harmonyCritical += item.values.harmony.criticalDamage;
  }

  return {
    charisma: hero.leadership + machine.addCharisma,
    addCharisma: machine.addCharisma,
    damage: { min: shownMinimum, max: shownMaximum, rating: machine.attackRating, source: attack.boosted ? 'boosted' : 'base' },
    damageRange: { minimum: Math.min(minimum + minIndex, shownMaximum), maximum: shownMaximum },
    attackRatingPK: machine.attackRatingPK,
    attackRatingPKBonus: guardianAttack + attackPowerRate,
    defense: {
      value: machine.defense + harmonyDefense + changeRing,
      rate: machine.successfulBlocking,
      fullSet,
      fullSetRate: d(machine.successfulBlocking, 10),
      fullSetBase: d(machine.defense + changeRing, 10),
    },
    defenseRatePK: machine.successfulBlockingPK,
    defenseRatePKBonus: guardianDefense + defenseRateBonus,
    attackSpeedSource: helperWorks && (helperType === ITEM.DEMON || helperType === ITEM.PET_SKELETON) ? 'buffed' : 'base',
    lifeSource: helperWorks && helperType === ITEM.SPIRIT_OF_GUARDIAN ? 'buffed' : 'base',
    meleeDamage: hero.classIndex === BaseClass.RAGE_FIGHTER ? 50 + d(hero.vitality, 10) : null,
    magic,
    curse,
    skillDamage,
    divineDamage: hero.classIndex === BaseClass.RAGE_FIGHTER ? 50 + d(energy, 10) : null,
    areaDamage: hero.classIndex === BaseClass.RAGE_FIGHTER ? 100 + (d(hero.agility, 8) + d(energy, 10)) : null,
    criticalRate: { luck, luckItems },
    criticalDamage: { harmony: harmonyCritical },
    criticalHit: shownMaximum + harmonyCritical,
    excellentHit: Math.trunc(shownMaximum * EXCELLENT_DAMAGE_FACTOR) + harmonyCritical,
  };
}

// BuildAttributeLines' bDexSuccess: a whole, unbroken armour set of one kind (the Magic Gladiator's without a helm and
// with one of its own armours, the Rage Fighter's without gloves) adds a tenth of the defense rate.
function wearsDefenseSet(hero: HeroStats, equipment: WornEquipment): boolean {
  for (let slot: number = Slot.HELM; slot <= Slot.BOOTS; slot++) {
    const item = equipment[slot];
    const mayLack = (hero.classIndex === BaseClass.DARK && slot === Slot.HELM)
      || (hero.classIndex === BaseClass.RAGE_FIGHTER && slot === Slot.GLOVES);
    if ((!item && !mayLack) || (item && item.durability <= 0)) return false;
  }
  let number: number;
  if (hero.classIndex === BaseClass.DARK) {
    if (!GLADIATOR_SET_ARMORS.includes(typeOf(equipment[Slot.ARMOR]))) return false;
    number = itemNumber(equipment[Slot.ARMOR]);
  } else {
    number = itemNumber(equipment[Slot.HELM]);
  }
  for (let slot: number = Slot.ARMOR; slot <= Slot.BOOTS; slot++) {
    if (hero.classIndex === BaseClass.RAGE_FIGHTER && slot === Slot.GLOVES) continue;
    if (number !== itemNumber(equipment[slot])) return false;
  }
  return true;
}
