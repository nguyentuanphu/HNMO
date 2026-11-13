// Combat presentation as the desktop client shows it: which animation a player or a
// monster plays for an attack, a hit or a death, how fast the player model's actions
// play, and the damage numbers that float over a target. Each part names the desktop
// client function it follows.

import { itemDefinition } from './items.js';

/**
 * Animation speeds in the desktop data are keys per frame at this many frames per
 * second. Mirrors FRAMES_PER_SECOND in monsterData.ts (not exported there).
 */
export const ANIMATION_FRAMES_PER_SECOND = 25;

// ---------------------------------------------------------------------------
// Player model actions (the desktop's PLAYER_* enum, Core/Globals/_enum.h, without
// the optional riding-skill block, which the desktop build leaves out). The keys are
// the desktop names without "PLAYER_"; PLAYER_ATTACK_END and PLAYER_FLY_RIDE share 74.
// ---------------------------------------------------------------------------
export const PlayerActions = {
  SET: 0,
  STOP_MALE: 1,
  STOP_FEMALE: 2,
  STOP_SUMMONER: 3,
  STOP_SWORD: 4,
  STOP_TWO_HAND_SWORD: 5,
  STOP_SPEAR: 6,
  STOP_SCYTHE: 7,
  STOP_BOW: 8,
  STOP_CROSSBOW: 9,
  STOP_WAND: 10,
  STOP_FLY: 11,
  STOP_FLY_CROSSBOW: 12,
  STOP_RIDE: 13,
  STOP_RIDE_WEAPON: 14,
  WALK_MALE: 15,
  WALK_FEMALE: 16,
  WALK_SWORD: 17,
  WALK_TWO_HAND_SWORD: 18,
  WALK_SPEAR: 19,
  WALK_SCYTHE: 20,
  WALK_BOW: 21,
  WALK_CROSSBOW: 22,
  WALK_WAND: 23,
  WALK_SWIM: 24,
  RUN: 25,
  RUN_SWORD: 26,
  RUN_TWO_SWORD: 27,
  RUN_TWO_HAND_SWORD: 28,
  RUN_SPEAR: 29,
  RUN_BOW: 30,
  RUN_CROSSBOW: 31,
  RUN_WAND: 32,
  RUN_SWIM: 33,
  FLY: 34,
  FLY_CROSSBOW: 35,
  RUN_RIDE: 36,
  RUN_RIDE_WEAPON: 37,
  ATTACK_FIST: 38,
  ATTACK_SWORD_RIGHT1: 39,
  ATTACK_SWORD_RIGHT2: 40,
  ATTACK_SWORD_LEFT1: 41,
  ATTACK_SWORD_LEFT2: 42,
  ATTACK_TWO_HAND_SWORD1: 43,
  ATTACK_TWO_HAND_SWORD2: 44,
  ATTACK_TWO_HAND_SWORD3: 45,
  ATTACK_SPEAR1: 46,
  ATTACK_SCYTHE1: 47,
  ATTACK_SCYTHE2: 48,
  ATTACK_SCYTHE3: 49,
  ATTACK_BOW: 50,
  ATTACK_CROSSBOW: 51,
  ATTACK_FLY_BOW: 52,
  ATTACK_FLY_CROSSBOW: 53,
  ATTACK_RIDE_SWORD: 54,
  ATTACK_RIDE_TWO_HAND_SWORD: 55,
  ATTACK_RIDE_SPEAR: 56,
  ATTACK_RIDE_SCYTHE: 57,
  ATTACK_RIDE_BOW: 58,
  ATTACK_RIDE_CROSSBOW: 59,
  ATTACK_SKILL_SWORD1: 60,
  ATTACK_SKILL_SWORD2: 61,
  ATTACK_SKILL_SWORD3: 62,
  ATTACK_SKILL_SWORD4: 63,
  ATTACK_SKILL_SWORD5: 64,
  ATTACK_SKILL_WHEEL: 65,
  ATTACK_SKILL_FURY_STRIKE: 66,
  SKILL_VITALITY: 67,
  SKILL_RIDER: 68,
  SKILL_RIDER_FLY: 69,
  ATTACK_SKILL_SPEAR: 70,
  ATTACK_DEATHSTAB: 71,
  SKILL_HELL_BEGIN: 72,
  SKILL_HELL_START: 73,
  ATTACK_END: 74,
  FLY_RIDE: 74,
  FLY_RIDE_WEAPON: 75,
  DARKLORD_STAND: 76,
  DARKLORD_WALK: 77,
  STOP_RIDE_HORSE: 78,
  RUN_RIDE_HORSE: 79,
  ATTACK_STRIKE: 80,
  ATTACK_TELEPORT: 81,
  ATTACK_RIDE_STRIKE: 82,
  ATTACK_RIDE_TELEPORT: 83,
  ATTACK_RIDE_HORSE_SWORD: 84,
  ATTACK_RIDE_ATTACK_FLASH: 85,
  ATTACK_RIDE_ATTACK_MAGIC: 86,
  ATTACK_DARKHORSE: 87,
  IDLE1_DARKHORSE: 88,
  IDLE2_DARKHORSE: 89,
  FENRIR_ATTACK: 90,
  FENRIR_ATTACK_DARKLORD_AQUA: 91,
  FENRIR_ATTACK_DARKLORD_STRIKE: 92,
  FENRIR_ATTACK_DARKLORD_SWORD: 93,
  FENRIR_ATTACK_DARKLORD_TELEPORT: 94,
  FENRIR_ATTACK_DARKLORD_FLASH: 95,
  FENRIR_ATTACK_TWO_SWORD: 96,
  FENRIR_ATTACK_MAGIC: 97,
  FENRIR_ATTACK_CROSSBOW: 98,
  FENRIR_ATTACK_SPEAR: 99,
  FENRIR_ATTACK_ONE_SWORD: 100,
  FENRIR_ATTACK_BOW: 101,
  FENRIR_SKILL: 102,
  FENRIR_SKILL_TWO_SWORD: 103,
  FENRIR_SKILL_ONE_RIGHT: 104,
  FENRIR_SKILL_ONE_LEFT: 105,
  FENRIR_DAMAGE: 106,
  FENRIR_DAMAGE_TWO_SWORD: 107,
  FENRIR_DAMAGE_ONE_RIGHT: 108,
  FENRIR_DAMAGE_ONE_LEFT: 109,
  FENRIR_RUN: 110,
  FENRIR_RUN_TWO_SWORD: 111,
  FENRIR_RUN_ONE_RIGHT: 112,
  FENRIR_RUN_ONE_LEFT: 113,
  FENRIR_RUN_MAGOM: 114,
  FENRIR_RUN_TWO_SWORD_MAGOM: 115,
  FENRIR_RUN_ONE_RIGHT_MAGOM: 116,
  FENRIR_RUN_ONE_LEFT_MAGOM: 117,
  FENRIR_RUN_ELF: 118,
  FENRIR_RUN_TWO_SWORD_ELF: 119,
  FENRIR_RUN_ONE_RIGHT_ELF: 120,
  FENRIR_RUN_ONE_LEFT_ELF: 121,
  FENRIR_STAND: 122,
  FENRIR_STAND_TWO_SWORD: 123,
  FENRIR_STAND_ONE_RIGHT: 124,
  FENRIR_STAND_ONE_LEFT: 125,
  FENRIR_WALK: 126,
  FENRIR_WALK_TWO_SWORD: 127,
  FENRIR_WALK_ONE_RIGHT: 128,
  FENRIR_WALK_ONE_LEFT: 129,
  ATTACK_BOW_UP: 130,
  ATTACK_CROSSBOW_UP: 131,
  ATTACK_FLY_BOW_UP: 132,
  ATTACK_FLY_CROSSBOW_UP: 133,
  ATTACK_RIDE_BOW_UP: 134,
  ATTACK_RIDE_CROSSBOW_UP: 135,
  ATTACK_ONE_FLASH: 136,
  ATTACK_RUSH: 137,
  ATTACK_DEATH_CANNON: 138,
  ATTACK_REMOVAL: 139,
  ATTACK_STUN: 140,
  HIGH_SHOCK: 141,
  STOP_TWO_HAND_SWORD_TWO: 142,
  WALK_TWO_HAND_SWORD_TWO: 143,
  RUN_TWO_HAND_SWORD_TWO: 144,
  ATTACK_TWO_HAND_SWORD_TWO: 145,
  SKILL_HAND1: 146,
  SKILL_HAND2: 147,
  SKILL_WEAPON1: 148,
  SKILL_WEAPON2: 149,
  SKILL_ELF1: 150,
  SKILL_TELEPORT: 151,
  SKILL_FLASH: 152,
  SKILL_INFERNO: 153,
  SKILL_HELL: 154,
  RIDE_SKILL: 155,
  SKILL_SLEEP: 156,
  SKILL_SLEEP_UNI: 157,
  SKILL_SLEEP_DINO: 158,
  SKILL_SLEEP_FENRIR: 159,
  SKILL_CHAIN_LIGHTNING: 160,
  SKILL_CHAIN_LIGHTNING_UNI: 161,
  SKILL_CHAIN_LIGHTNING_DINO: 162,
  SKILL_CHAIN_LIGHTNING_FENRIR: 163,
  SKILL_LIGHTNING_ORB: 164,
  SKILL_LIGHTNING_ORB_UNI: 165,
  SKILL_LIGHTNING_ORB_DINO: 166,
  SKILL_LIGHTNING_ORB_FENRIR: 167,
  SKILL_DRAIN_LIFE: 168,
  SKILL_DRAIN_LIFE_UNI: 169,
  SKILL_DRAIN_LIFE_DINO: 170,
  SKILL_DRAIN_LIFE_FENRIR: 171,
  SKILL_SUMMON: 172,
  SKILL_SUMMON_UNI: 173,
  SKILL_SUMMON_DINO: 174,
  SKILL_SUMMON_FENRIR: 175,
  SKILL_BLOW_OF_DESTRUCTION: 176,
  SKILL_SWELL_OF_MP: 177,
  SKILL_MULTISHOT_BOW_STAND: 178,
  SKILL_MULTISHOT_BOW_FLYING: 179,
  SKILL_MULTISHOT_CROSSBOW_STAND: 180,
  SKILL_MULTISHOT_CROSSBOW_FLYING: 181,
  SKILL_RECOVERY: 182,
  SKILL_GIGANTICSTORM: 183,
  SKILL_FLAMESTRIKE: 184,
  SKILL_LIGHTNING_SHOCK: 185,
  DEFENSE1: 186,
  GREETING1: 187,
  GREETING_FEMALE1: 188,
  GOODBYE1: 189,
  GOODBYE_FEMALE1: 190,
  CLAP1: 191,
  CLAP_FEMALE1: 192,
  CHEER1: 193,
  CHEER_FEMALE1: 194,
  DIRECTION1: 195,
  DIRECTION_FEMALE1: 196,
  GESTURE1: 197,
  GESTURE_FEMALE1: 198,
  UNKNOWN1: 199,
  UNKNOWN_FEMALE1: 200,
  CRY1: 201,
  CRY_FEMALE1: 202,
  AWKWARD1: 203,
  AWKWARD_FEMALE1: 204,
  SEE1: 205,
  SEE_FEMALE1: 206,
  WIN1: 207,
  WIN_FEMALE1: 208,
  SMILE1: 209,
  SMILE_FEMALE1: 210,
  SLEEP1: 211,
  SLEEP_FEMALE1: 212,
  COLD1: 213,
  COLD_FEMALE1: 214,
  AGAIN1: 215,
  AGAIN_FEMALE1: 216,
  RESPECT1: 217,
  SALUTE1: 218,
  SCISSORS: 219,
  ROCK: 220,
  PAPER: 221,
  HUSTLE: 222,
  PROVOCATION: 223,
  LOOK_AROUND: 224,
  CHEERS: 225,
  KOREA_HANDCLAP: 226,
  POINT_DANCE: 227,
  RUSH1: 228,
  COME_UP: 229,
  SHOCK: 230,
  DIE1: 231,
  DIE2: 232,
  SIT1: 233,
  SIT2: 234,
  SIT_FEMALE1: 235,
  SIT_FEMALE2: 236,
  HEALING1: 237,
  HEALING_FEMALE1: 238,
  POSE1: 239,
  POSE_FEMALE1: 240,
  JACK_1: 241,
  JACK_2: 242,
  SANTA_1: 243,
  SANTA_2: 244,
  CHANGE_UP: 245,
  RECOVER_SKILL: 246,
  SKILL_THRUST: 247,
  SKILL_STAMP: 248,
  SKILL_GIANTSWING: 249,
  SKILL_DARKSIDE_READY: 250,
  SKILL_DARKSIDE_ATTACK: 251,
  SKILL_DRAGONKICK: 252,
  SKILL_DRAGONLORE: 253,
  SKILL_PHOENIX_SHOT: 254,
  SKILL_ATT_UP_OURFORCES: 255,
  SKILL_HP_UP_OURFORCES: 256,
  RAGE_UNI_ATTACK: 257,
  RAGE_UNI_ATTACK_ONE_RIGHT: 258,
  RAGE_UNI_RUN: 259,
  RAGE_UNI_RUN_ONE_RIGHT: 260,
  RAGE_UNI_STOP_ONE_RIGHT: 261,
  RAGE_FENRIR: 262,
  RAGE_FENRIR_TWO_SWORD: 263,
  RAGE_FENRIR_ONE_RIGHT: 264,
  RAGE_FENRIR_ONE_LEFT: 265,
  RAGE_FENRIR_WALK: 266,
  RAGE_FENRIR_WALK_ONE_RIGHT: 267,
  RAGE_FENRIR_WALK_ONE_LEFT: 268,
  RAGE_FENRIR_WALK_TWO_SWORD: 269,
  RAGE_FENRIR_RUN: 270,
  RAGE_FENRIR_RUN_TWO_SWORD: 271,
  RAGE_FENRIR_RUN_ONE_RIGHT: 272,
  RAGE_FENRIR_RUN_ONE_LEFT: 273,
  RAGE_FENRIR_STAND: 274,
  RAGE_FENRIR_STAND_TWO_SWORD: 275,
  RAGE_FENRIR_STAND_ONE_RIGHT: 276,
  RAGE_FENRIR_STAND_ONE_LEFT: 277,
  RAGE_FENRIR_DAMAGE: 278,
  RAGE_FENRIR_DAMAGE_TWO_SWORD: 279,
  RAGE_FENRIR_DAMAGE_ONE_RIGHT: 280,
  RAGE_FENRIR_DAMAGE_ONE_LEFT: 281,
  RAGE_FENRIR_ATTACK_RIGHT: 282,
  STOP_RAGEFIGHTER: 283,} as const;
export type PlayerActionName = keyof typeof PlayerActions;
/** The number of player model actions (MAX_PLAYER_ACTION). */
export const MAX_PLAYER_ACTION = 284;

// ---------------------------------------------------------------------------
// Monster model actions (MONSTER01_*, Core/Globals/_define.h).
// ---------------------------------------------------------------------------
export const MonsterActions = {
  STOP1: 0,
  STOP2: 1,
  WALK: 2,
  ATTACK1: 3,
  ATTACK2: 4,
  SHOCK: 5,
  DIE: 6,
  APPEAR: 7,
  ATTACK3: 8,
  ATTACK4: 9,
  RUN: 10,
  ATTACK5: 11,
} as const;

/** The names Monsters.json uses for monster actions (Data::Monsters AnimationNames), by number. */
export const MONSTER_ACTION_NAMES = ['stop1', 'stop2', 'walk', 'attack1', 'attack2', 'shock', 'die', 'appear', 'attack3', 'attack4', 'run', 'attack5'] as const;

// ---------------------------------------------------------------------------
// The Animation byte of the ObjectAnimation packet (0x18): the desktop's AT_* values
// (enum OptionType, Core/Globals/_enum.h). The server sends ATTACK1 (120) for monster
// attacks.
// ---------------------------------------------------------------------------
export const ServerAnimation = {
  ATTACK1: 120,
  ATTACK2: 121,
  STAND1: 122,
  STAND2: 123,
  MOVE1: 124,
  MOVE2: 125,
  DAMAGE1: 126,
  DIE1: 127,
  SIT1: 128,
  POSE1: 129,
  HEALING1: 130,
  GREETING1: 131,
  GOODBYE1: 132,
  CLAP1: 133,
  GESTURE1: 134,
  DIRECTION1: 135,
  UNKNOWN1: 136,
  CRY1: 137,
  CHEER1: 138,
  AWKWARD1: 139,
  SEE1: 140,
  WIN1: 141,
  SMILE1: 142,
  SLEEP1: 143,
  COLD1: 144,
  AGAIN1: 145,
  RESPECT1: 146,
  SALUTE1: 147,
  RUSH1: 148,
  SCISSORS: 149,
  ROCK: 150,
  PAPER: 151,
  HUSTLE: 152,
  PROVOCATION: 153,
  LOOK_AROUND: 154,
  CHEERS: 155,
  JACK1: 156,
  JACK2: 157,
  SANTA1_1: 158,
  SANTA1_2: 159,
  SANTA1_3: 160,
  SANTA2_1: 161,
  SANTA2_2: 162,
  SANTA2_3: 163,
  RAGEBUFF_1: 164,
  RAGEBUFF_2: 165,} as const;

// ---------------------------------------------------------------------------
// Equipment as the desktop's animation choices see it.
// ---------------------------------------------------------------------------

/** Character classes by the desktop's base class number. */
export const CharacterClass = { DARK_WIZARD: 0, DARK_KNIGHT: 1, ELF: 2, MAGIC_GLADIATOR: 3, DARK_LORD: 4, SUMMONER: 5, RAGE_FIGHTER: 6 } as const;

/**
 * An item in a hand. `twoHanded` and `weaponKind` come from the item's definition
 * (Data/Items/GroupNN_*.json) when left out; give them when the item catalog is not
 * loaded.
 */
export interface HandItem {
  group: number;
  number: number;
  twoHanded?: boolean;
  weaponKind?: string;
}

export type Mount = 'none' | 'fenrir' | 'darkHorse' | 'uniria' | 'dinorant';

export interface AttackLookInput {
  /** 0 DW, 1 DK, 2 Elf, 3 MG, 4 DL, 5 Summoner, 6 RF. */
  classIndex: number;
  rightHand: HandItem | null;
  leftHand: HandItem | null;
  mount: Mount;
  safeZone: boolean;
  female?: boolean;
  /** Wings are worn: bows and crossbows then use their flying attack. */
  wings?: boolean;
}

/** A random number in [0, 1), as Math.random; the desktop draws some animations at random. */
export type RandomSource = () => number;

const ItemGroups = { SWORD: 0, AXE: 1, MACE: 2, SPEAR: 3, BOW: 4, STAFF: 5 } as const;

// Two-handed swords with their own stance and swing (MODEL_DARK_REIGN_BLADE,
// MODEL_RUNE_BLADE, MODEL_EXPLOSION_BLADE, MODEL_SWORD_DANCER).
const TWO_SWORD_TWO_NUMBERS: readonly number[] = [21, 31, 23, 25];
// MODEL__SPEAR and MODEL_DRAGON_LANCE: the spears with the thrust animation.
const THRUST_SPEAR_NUMBERS: readonly number[] = [1, 2];
// MODEL_DOUBLE_POLEAXE: group 3 numbers from here on count as scythes when mounted.
const DOUBLE_POLEAXE_NUMBER = 5;

function weaponKindOf(item: HandItem | null): string | undefined {
  if (!item) return undefined;
  return item.weaponKind ?? itemDefinition(item.group, item.number)?.weaponKind;
}

// ItemAttribute[...].TwoHand, which the desktop fills from the definition's twoHanded.
function isTwoHanded(item: HandItem): boolean {
  return item.twoHanded ?? itemDefinition(item.group, item.number)?.twoHanded ?? false;
}

// MODEL_SWORD <= type < MODEL_MACE + MAX_ITEM_INDEX: swords (and fists), axes, maces.
function isSwordAxeOrMace(item: HandItem | null): item is HandItem {
  return item !== null && item.group >= ItemGroups.SWORD && item.group <= ItemGroups.MACE;
}

// The item's number when it is of this group, else null (a model type range check).
function numberInGroup(item: HandItem | null, group: number): number | null {
  return item !== null && item.group === group ? item.number : null;
}

function rand2(rand: RandomSource): number {
  return Math.floor(rand() * 2) % 2;
}

export const BowType = { NONE: 0, BOW: 1, CROSSBOW: 2 } as const;
export type BowType = (typeof BowType)[keyof typeof BowType];

/** CCharacterManager::GetEquipedBowType: a bow in the left hand, or a crossbow in the right. */
export function equippedBowType(input: Pick<AttackLookInput, 'rightHand' | 'leftHand'>): BowType {
  if (weaponKindOf(input.leftHand) === 'bow') return BowType.BOW;
  if (weaponKindOf(input.rightHand) === 'crossbow') return BowType.CROSSBOW;
  return BowType.NONE;
}

// ---------------------------------------------------------------------------
// The attack animation (SetPlayerAttack, Engine/Object/ZzzCharacter.cpp).
// ---------------------------------------------------------------------------

/**
 * The action a player plays for a plain attack. `swing` is the character's swing
 * counter (the desktop's SwordCount, which counts up by one per attack) for weapons
 * whose swings alternate. A left-hand-only sword and a staff draw their swing at
 * random, as the desktop does.
 */
export function playerAttackAction(input: AttackLookInput, swing: number, rand: RandomSource = Math.random): number {
  const A = PlayerActions;
  const right = input.rightHand;
  const left = input.leftHand;
  const bow = equippedBowType(input);
  const count = Math.max(0, Math.trunc(swing));
  const mounted = !input.safeZone;

  if (input.mount === 'fenrir' && mounted) {
    let action: number;
    if ((numberInGroup(right, ItemGroups.SPEAR) ?? DOUBLE_POLEAXE_NUMBER) < DOUBLE_POLEAXE_NUMBER) action = A.FENRIR_ATTACK_SPEAR;
    else if (bow === BowType.BOW) action = A.FENRIR_ATTACK_BOW;
    else if (bow === BowType.CROSSBOW) action = A.FENRIR_ATTACK_CROSSBOW;
    else if (right && left) action = A.FENRIR_ATTACK_TWO_SWORD;
    else if (right && !left) action = A.FENRIR_ATTACK_ONE_SWORD;
    else if (!right && left && input.classIndex === CharacterClass.RAGE_FIGHTER) action = A.RAGE_FENRIR_ATTACK_RIGHT;
    else if (!right && left) action = A.FENRIR_ATTACK_ONE_SWORD;
    else action = A.FENRIR_ATTACK;
    if (input.classIndex === CharacterClass.DARK_LORD) action = A.FENRIR_ATTACK_DARKLORD_SWORD;
    return action;
  }

  if (input.mount === 'darkHorse' && mounted) return A.ATTACK_RIDE_HORSE_SWORD;

  if ((input.mount === 'uniria' || input.mount === 'dinorant') && mounted) {
    if ((numberInGroup(right, ItemGroups.SPEAR) ?? DOUBLE_POLEAXE_NUMBER) < DOUBLE_POLEAXE_NUMBER) return A.ATTACK_RIDE_SPEAR;
    if (numberInGroup(right, ItemGroups.SPEAR) !== null) return A.ATTACK_RIDE_SCYTHE;
    if (bow === BowType.BOW) return A.ATTACK_RIDE_BOW;
    if (bow === BowType.CROSSBOW) return A.ATTACK_RIDE_CROSSBOW;
    const rageFighter = input.classIndex === CharacterClass.RAGE_FIGHTER;
    if (!right) return rageFighter ? A.RAGE_UNI_ATTACK : A.ATTACK_RIDE_SWORD;
    // The desktop picks the one-right attack for a two-handed weapon here, as written.
    if (rageFighter) return isTwoHanded(right) ? A.RAGE_UNI_ATTACK_ONE_RIGHT : A.RAGE_UNI_ATTACK;
    return isTwoHanded(right) ? A.ATTACK_RIDE_TWO_HAND_SWORD : A.ATTACK_RIDE_SWORD;
  }

  if (!right && !left) return A.ATTACK_FIST;

  if (isSwordAxeOrMace(right)) {
    if (!isTwoHanded(right)) {
      if (isSwordAxeOrMace(left)) {
        switch (count % 4) {
          case 0: return A.ATTACK_SWORD_RIGHT1;
          case 1: return A.ATTACK_SWORD_LEFT1;
          case 2: return A.ATTACK_SWORD_RIGHT1 + 1;
          default: return A.ATTACK_SWORD_LEFT1 + 1;
        }
      }
      return A.ATTACK_SWORD_RIGHT1 + (count % 2);
    }
    if (right.group === ItemGroups.SWORD && TWO_SWORD_TWO_NUMBERS.includes(right.number)) return A.ATTACK_TWO_HAND_SWORD_TWO;
    return A.ATTACK_TWO_HAND_SWORD1 + (count % 3);
  }
  if (isSwordAxeOrMace(left)) return A.ATTACK_SWORD_LEFT1 + rand2(rand);
  if (right && numberInGroup(right, ItemGroups.STAFF) !== null) {
    return isTwoHanded(right) ? A.SKILL_WEAPON1 + rand2(rand) : A.ATTACK_SWORD_RIGHT1 + rand2(rand);
  }
  if (THRUST_SPEAR_NUMBERS.includes(numberInGroup(right, ItemGroups.SPEAR) ?? -1)) return A.ATTACK_SPEAR1;
  if (numberInGroup(right, ItemGroups.SPEAR) !== null) return A.ATTACK_SCYTHE1 + (count % 3);
  if (bow === BowType.BOW) return input.wings ? A.ATTACK_FLY_BOW : A.ATTACK_BOW;
  if (bow === BowType.CROSSBOW) return input.wings ? A.ATTACK_FLY_CROSSBOW : A.ATTACK_CROSSBOW;
  return A.ATTACK_FIST;
}

// Plain-attack reach in cells (BasicAttackRange*, GameLogic/Automation/Attack.h).
export const ATTACK_RANGE_DEFAULT = 1.8;
export const ATTACK_RANGE_SPEAR = 2.2;
export const ATTACK_RANGE_BOW = 6;

/**
 * How close a plain attack must be, in cells (Action, MOVEMENT_ATTACK, in
 * Engine/Object/ZzzInterface.cpp, and EquippedAttackRange): a bow or crossbow 6, a
 * spear (any weapon of kind "spear") in the right hand 2.2, anything else 1.8. The
 * desktop has no class-specific range; out of range, an elf walks closer and the
 * other classes stop.
 */
export function attackRange(input: Pick<AttackLookInput, 'rightHand' | 'leftHand'>): number {
  if (equippedBowType(input) !== BowType.NONE) return ATTACK_RANGE_BOW;
  return weaponKindOf(input.rightHand) === 'spear' ? ATTACK_RANGE_SPEAR : ATTACK_RANGE_DEFAULT;
}

// ---------------------------------------------------------------------------
// Getting hit and dying.
// ---------------------------------------------------------------------------

// Actions SetPlayerShock does not interrupt.
const SHOCK_PROOF_ACTIONS: readonly number[] = [
  PlayerActions.ATTACK_SKILL_FURY_STRIKE, PlayerActions.SKILL_VITALITY, PlayerActions.SKILL_HELL_BEGIN,
  PlayerActions.SKILL_ATT_UP_OURFORCES, PlayerActions.SKILL_HP_UP_OURFORCES, PlayerActions.SKILL_GIANTSWING,
  PlayerActions.SKILL_DRAGONLORE,
];

/**
 * The action a hit player plays (SetPlayerShock, with SetAction_Fenrir_Damage), or
 * null when it plays none: no damage, dead, on a Uniria, Dinorant or Dark Horse (also
 * in a safe zone), or during one of a few skills. On a Fenrir the safe zone does not
 * matter either.
 */
export function playerShockAction(
  input: Pick<AttackLookInput, 'classIndex' | 'rightHand' | 'leftHand' | 'mount'>,
  damage: number,
  currentAction?: number,
  dead = false,
): number | null {
  if (dead) return null;
  if (input.mount === 'uniria' || input.mount === 'dinorant' || input.mount === 'darkHorse') return null;
  if (currentAction !== undefined && SHOCK_PROOF_ACTIONS.includes(currentAction)) return null;
  if (damage <= 0) return null;
  if (input.mount !== 'fenrir') return PlayerActions.SHOCK;
  const right = input.rightHand !== null;
  const left = input.leftHand !== null;
  if (input.classIndex === CharacterClass.RAGE_FIGHTER) {
    if (right && left) return PlayerActions.RAGE_FENRIR_DAMAGE_TWO_SWORD;
    if (right) return PlayerActions.RAGE_FENRIR_DAMAGE_ONE_RIGHT;
    if (left) return PlayerActions.RAGE_FENRIR_DAMAGE_ONE_LEFT;
    return PlayerActions.RAGE_FENRIR_DAMAGE;
  }
  if (right && left) return PlayerActions.FENRIR_DAMAGE_TWO_SWORD;
  if (right) return PlayerActions.FENRIR_DAMAGE_ONE_RIGHT;
  if (left) return PlayerActions.FENRIR_DAMAGE_ONE_LEFT;
  return PlayerActions.FENRIR_DAMAGE;
}

/**
 * The action a hit monster plays (SetPlayerShock, monster branch), or null: no
 * damage, dead, or in its first two attacks, which a hit does not interrupt.
 */
export function monsterShockAction(damage: number, currentAction: number, dead = false): number | null {
  if (dead || damage <= 0) return null;
  if (currentAction >= MonsterActions.ATTACK1 && currentAction <= MonsterActions.ATTACK2) return null;
  return MonsterActions.SHOCK;
}

/**
 * Whether an ObjectHit makes the target flinch (ReceiveAttackDamage): always when
 * the packet's hit-shock bit (the ObjectId's top bit) is set; otherwise never for the
 * own character and half of the time for anyone else (the desktop leaves out one
 * Illusion of Kundun stage, monster 7 of that set).
 */
export function hitPlaysShock(hit: { hitShock: boolean; toHero: boolean }, rand: RandomSource = Math.random): boolean {
  if (hit.hitShock) return true;
  if (hit.toHero) return false;
  return rand2(rand) === 0;
}

/**
 * The death action (SetPlayerDie): players PLAYER_DIE1, monsters MONSTER01_DIE. (A few
 * monster models instead burst into bones or stones, or play STOP2; the desktop picks
 * those by model.)
 */
export function deathAction(isPlayer: boolean): number {
  return isPlayer ? PlayerActions.DIE1 : MonsterActions.DIE;
}

/**
 * After a kill packet the target is marked dead and, unless it already plays its
 * death (it does when someone else killed it), plays it once this time has passed:
 * the desktop's Dead counter goes from 1 to 15 frames (MoveCharacter). When the own
 * character landed the killing blow, the death starts when that swing hits, or at the
 * latest after this delay.
 */
export const DEATH_DELAY_SECONDS = 14 / ANIMATION_FRAMES_PER_SECOND;

// ---------------------------------------------------------------------------
// Monster attacks (GameLogic::Monsters::Attacks::Play, ChooseMonsterAttack,
// NextGenericAttack, DrawAttack).
// ---------------------------------------------------------------------------

/** A monster's "attacks" entry of Monsters.json (Data::Monsters::MonsterAttacks). */
export interface MonsterAttacksData {
  /** Monster skill number to action name ("attack2", "appear", …, or "none"). */
  monsterSkills?: Record<string, string>;
  /** Player skill number (the attack's skill) to action name. */
  skills?: Record<string, string>;
  /** One action name, "none", "generic", or a list of two or more played in turn. Default "attack1". */
  otherwise?: string | string[];
  otherwiseRandom?: 'rand' | 'randFps' | 'bali';
  clearSkill?: 'never' | 'onMatch' | 'onNoMatch' | 'always' | 'afterAttack';
  [field: string]: unknown;
}

/** What the desktop keeps per monster for choosing attacks. Updated by monsterAttackAction. */
export interface MonsterAttackState {
  /** SwordCount: counts the generic pattern's attacks. */
  swing: number;
  /** AttackRotation: the next entry of an `otherwise` list. */
  rotation: number;
  /** MonsterSkill of the attack being shown (from a monster skill packet), -1 for none. */
  monsterSkill: number;
  /** Skill of the attack (a player skill number), 0 for a plain attack. */
  skill: number;
}

export function newMonsterAttackState(): MonsterAttackState {
  return { swing: 0, rotation: 0, monsterSkill: -1, skill: 0 };
}

const NO_ANIMATION = -1;

function monsterActionByName(name: string): number | null {
  if (name === 'none') return NO_ANIMATION;
  const index = (MONSTER_ACTION_NAMES as readonly string[]).indexOf(name);
  if (index >= 0) return index;
  // FindMonsterAnimation also takes a plain number past the named actions.
  return /^\d+$/.test(name) && Number(name) >= MONSTER_ACTION_NAMES.length ? Number(name) : null;
}

function findEntry(entries: Record<string, string> | undefined, skill: number): number | null {
  const name = entries?.[String(skill)];
  return name === undefined ? null : monsterActionByName(name);
}

/** NextGenericAttack: attack 1 on every third attack, attack 2 otherwise. */
export function nextGenericMonsterAttack(state: MonsterAttackState): number {
  const action = state.swing % 3 === 0 ? MonsterActions.ATTACK1 : MonsterActions.ATTACK2;
  state.swing = (state.swing + 1) & 0xff; // an 8-bit counter in the desktop
  return action;
}

function drawMonsterAttack(random: 'rand' | 'randFps' | 'bali', rand: RandomSource): number {
  switch (random) {
    case 'rand':
      return MonsterActions.ATTACK1 + rand2(rand);
    case 'randFps':
      // rand_fps_check(2): one chance in two per reference frame.
      return rand2(rand) === 0 ? MonsterActions.ATTACK1 : MonsterActions.ATTACK2;
    case 'bali': {
      const draw = Math.floor(rand() * 8);
      if (draw > 2) return MonsterActions.ATTACK1 + rand2(rand);
      return draw > 0 ? MonsterActions.ATTACK3 : MonsterActions.ATTACK4;
    }
  }
}

/**
 * The action a monster plays for an attack (SetPlayerAttack's monster branch): from
 * its "attacks" data when it has one, else the generic pattern. Returns null when the
 * data says "none" (the monster keeps its current action). Updates `state` (swing
 * counter, rotation, and the monster skill it clears) as the desktop does. Set
 * state.monsterSkill and pass fromSkillPacket for the attack a monster skill packet
 * starts. Map-specific
 * variants ("mapVariants") are the caller's to resolve; the battle castle guards and a
 * few map bosses the desktop handles in code are not covered.
 */
export function monsterAttackAction(
  attacks: MonsterAttacksData | null | undefined,
  state: MonsterAttackState,
  rand: RandomSource = Math.random,
  fromSkillPacket = false,
): number | null {
  if (!attacks) return nextGenericMonsterAttack(state);
  const clear = attacks.clearSkill ?? 'never';
  // Play(): with "afterAttack", an attack its skill packet did not start is a normal
  // attack, and the skill is cleared before choosing.
  if (clear === 'afterAttack' && !fromSkillPacket) state.monsterSkill = -1;
  let entry = findEntry(attacks.monsterSkills, state.monsterSkill);
  if (entry === null) entry = findEntry(attacks.skills, state.skill);
  let animation = NO_ANIMATION;
  if (entry !== null) {
    animation = entry;
  } else if (attacks.otherwiseRandom) {
    animation = drawMonsterAttack(attacks.otherwiseRandom, rand);
  } else {
    const otherwise = attacks.otherwise ?? 'attack1';
    if (otherwise === 'generic') {
      animation = nextGenericMonsterAttack(state);
    } else if (Array.isArray(otherwise) && otherwise.length > 1) {
      const count = otherwise.length;
      const name = otherwise[((state.rotation % count) + count) % count]!;
      animation = monsterActionByName(name) ?? MonsterActions.ATTACK1;
      state.rotation = (state.rotation + 1) % count;
    } else {
      const name = Array.isArray(otherwise) ? otherwise[0] : otherwise;
      animation = name === undefined ? NO_ANIMATION : monsterActionByName(name) ?? MonsterActions.ATTACK1;
    }
  }
  const clearNow =
    clear === 'always' || (clear === 'onMatch' && entry !== null) || (clear === 'onNoMatch' && entry === null) ||
    (clear === 'afterAttack' && animation === NO_ANIMATION);
  if (clearNow) state.monsterSkill = -1;
  return animation === NO_ANIMATION ? null : animation;
}

/** OnAttackEnded: with clearSkill "afterAttack" the monster skill ends with the attack. */
export function monsterAttackEnded(attacks: MonsterAttacksData | null | undefined, state: MonsterAttackState): void {
  if (attacks?.clearSkill === 'afterAttack') state.monsterSkill = -1;
}

// ---------------------------------------------------------------------------
// The ObjectAnimation packet (0x18, ReceiveAction in Network/Server/WSclient.cpp).
// ---------------------------------------------------------------------------

/**
 * What to do with an ObjectAnimation:
 * - ignore: nothing (a player on a Dark Horse outside a safe zone);
 * - stop: play the character's standing action (SetPlayerStop);
 * - attack: an attack with this action toward the packet's target, or null when a
 *   monster's attack data says "none";
 * - play: this action. The desktop's SetAction ignores an action the model does not
 *   have, and does not restart an action that is already playing.
 * In every case but ignore, the desktop also turns the character to the packet's
 * direction, stops its movement and puts it at its target cell.
 */
export type AnimationReaction =
  | { kind: 'ignore' }
  | { kind: 'stop' }
  | { kind: 'attack'; action: number | null }
  | { kind: 'play'; action: number };

// The emotes and other actions of ReceiveAction's switch; female variants where the
// desktop has them.
function receivedOtherAction(animation: number, female: boolean): number {
  const S = ServerAnimation;
  const A = PlayerActions;
  const pick = (male: number, woman: number): number => (female ? woman : male);
  switch (animation) {
    case S.SIT1: return pick(A.SIT1, A.SIT_FEMALE1);
    case S.POSE1: return pick(A.POSE1, A.POSE_FEMALE1);
    case S.HEALING1: return pick(A.HEALING1, A.HEALING_FEMALE1);
    case S.GREETING1: return pick(A.GREETING1, A.GREETING_FEMALE1);
    case S.GOODBYE1: return pick(A.GOODBYE1, A.GOODBYE_FEMALE1);
    case S.CLAP1: return pick(A.CLAP1, A.CLAP_FEMALE1);
    case S.GESTURE1: return pick(A.GESTURE1, A.GESTURE_FEMALE1);
    case S.DIRECTION1: return pick(A.DIRECTION1, A.DIRECTION_FEMALE1);
    case S.UNKNOWN1: return pick(A.UNKNOWN1, A.UNKNOWN_FEMALE1);
    case S.CRY1: return pick(A.CRY1, A.CRY_FEMALE1);
    case S.AWKWARD1: return pick(A.AWKWARD1, A.AWKWARD_FEMALE1);
    case S.SEE1: return pick(A.SEE1, A.SEE_FEMALE1);
    case S.CHEER1: return pick(A.CHEER1, A.CHEER_FEMALE1);
    case S.WIN1: return pick(A.WIN1, A.WIN_FEMALE1);
    case S.SMILE1: return pick(A.SMILE1, A.SMILE_FEMALE1);
    case S.SLEEP1: return pick(A.SLEEP1, A.SLEEP_FEMALE1);
    case S.COLD1: return pick(A.COLD1, A.COLD_FEMALE1);
    case S.AGAIN1: return pick(A.AGAIN1, A.AGAIN_FEMALE1);
    case S.RESPECT1: return A.RESPECT1;
    case S.SALUTE1: return A.SALUTE1;
    case S.RUSH1: return A.RUSH1;
    case S.SCISSORS: return A.SCISSORS;
    case S.ROCK: return A.ROCK;
    case S.PAPER: return A.PAPER;
    case S.HUSTLE: return A.HUSTLE;
    // AT_DAMAGE1 sets MONSTER01_SHOCK (5) on any model, players included.
    case S.DAMAGE1: return MonsterActions.SHOCK;
    case S.PROVOCATION: return A.PROVOCATION;
    case S.LOOK_AROUND: return A.LOOK_AROUND;
    case S.CHEERS: return A.CHEERS;
    case S.JACK1: return A.JACK_1;
    case S.JACK2: return A.JACK_2;
    case S.SANTA1_1: case S.SANTA1_2: case S.SANTA1_3: return A.SANTA_1;
    case S.SANTA2_1: case S.SANTA2_2: case S.SANTA2_3: return A.SANTA_2;
    case S.RAGEBUFF_1: return A.SKILL_ATT_UP_OURFORCES;
    case S.RAGEBUFF_2: return A.SKILL_HP_UP_OURFORCES;
    // AT_SKILL_BLOCKING (a skill number) plays PLAYER_DEFENSE1; any other value is set as is.
    default: return animation === SKILL_BLOCKING ? A.DEFENSE1 : animation;
  }
}

/** AT_SKILL_BLOCKING, the skill number of the defense skill. */
export const SKILL_BLOCKING = 18;

/**
 * ReceiveAction for a player: the reaction to an ObjectAnimation. `swing` and `rand`
 * go to playerAttackAction for an attack.
 */
export function playerAnimationReaction(
  animation: number,
  look: AttackLookInput,
  swing: number,
  rand: RandomSource = Math.random,
): AnimationReaction {
  if (!look.safeZone && look.mount === 'darkHorse') return { kind: 'ignore' };
  if (animation === ServerAnimation.STAND1 || animation === ServerAnimation.STAND2) return { kind: 'stop' };
  if (animation === ServerAnimation.ATTACK1 || animation === ServerAnimation.ATTACK2) {
    return { kind: 'attack', action: playerAttackAction(look, swing, rand) };
  }
  return { kind: 'play', action: receivedOtherAction(animation, look.female ?? false) };
}

/**
 * ReceiveAction for a monster or NPC: attacks follow monsterAttackAction (with the
 * monster's "attacks" data, updating `state`); the other values map as for players
 * (the desktop runs the same switch, and a model without that action ignores it).
 */
export function monsterAnimationReaction(
  animation: number,
  attacks: MonsterAttacksData | null | undefined,
  state: MonsterAttackState,
  rand: RandomSource = Math.random,
): AnimationReaction {
  if (animation === ServerAnimation.STAND1 || animation === ServerAnimation.STAND2) return { kind: 'stop' };
  if (animation === ServerAnimation.ATTACK1 || animation === ServerAnimation.ATTACK2) {
    // StartReceivedAttack clears the attack's skill after choosing.
    const action = monsterAttackAction(attacks, state, rand);
    state.skill = 0;
    return { kind: 'attack', action };
  }
  return { kind: 'play', action: receivedOtherAction(animation, false) };
}

// ---------------------------------------------------------------------------
// Damage numbers (ReceiveAttackDamage in Network/Server/WSclient.cpp; CreatePoint,
// MovePoints and RenderPoints in Render/Effects/ZzzEffectPoint.cpp).
// ---------------------------------------------------------------------------

/** The Kind of ObjectHit / ObjectHitExtended (DamageKind in ServerToClientPackets.xml). */
export const DamageKind = {
  NORMAL_RED: 0,
  IGNORE_DEFENSE_CYAN: 1,
  EXCELLENT_LIGHT_GREEN: 2,
  CRITICAL_BLUE: 3,
  REFLECTED_LIGHT_PINK: 4,
  POISON_DARK_GREEN: 5,
  DARK_PINK: 6,
  WHITE: 7,
} as const;

/**
 * One number (or MISS) to float over the target. `heightOffset` is where it starts
 * above the target's position, in the desktop's world units (100 to a cell);
 * `scale` is the glyph size in the same units (each digit is scale × scale).
 */
export interface DamageText {
  text: string;
  color: [number, number, number];
  scale: number;
  heightOffset: number;
}

export interface DamageHit {
  kind: number;
  healthDamage: number;
  shieldDamage: number;
  isDoubleDamage: boolean;
  /** Triple or combo damage (the desktop's bComboEnable). */
  isTripleDamage: boolean;
  /** The hit is on the own character. */
  toHero: boolean;
  /** The ObjectId's top bit (hit shock): the desktop then shows every number plain red. */
  hitShock?: boolean;
}

/** CreatePoint puts every number this far above the position it is given. */
export const DAMAGE_TEXT_BASE_HEIGHT = 140;
/** The shield damage number starts this much higher than the health damage. */
export const SHIELD_DAMAGE_EXTRA_HEIGHT = 25;
/** The stacked copies of a double or triple hit start this much higher each. */
export const DAMAGE_STACK_STEP_HEIGHT = 10;
/** CreatePoint's default size. */
export const DAMAGE_TEXT_SCALE = 15;
/** Critical, excellent, ignore-defense and triple/combo hits start at this size. */
export const DAMAGE_TEXT_BIG_SCALE = 50;
/** MISS is a fixed sprite of this size (RenderNumber, value -1), whatever the scale. */
export const MISS_TEXT_WIDTH = 45;
export const MISS_TEXT_HEIGHT = 20;
export const MISS_TEXT = 'MISS';

const SHIELD_DAMAGE_COLOR: [number, number, number] = [0.8, 1, 0];
const KILL_DAMAGE_COLOR: [number, number, number] = [1, 0.6, 0];

function clampColor(r: number, g: number, b: number): [number, number, number] {
  const c = (v: number): number => Math.min(1, Math.max(0, v));
  return [c(r), c(g), c(b)];
}

/**
 * The numbers the desktop shows for an ObjectHit (ReceiveAttackDamage), in the order
 * it creates them:
 * - hit shock: the health damage in red (also "0"), then the shield damage;
 * - no damage: MISS, white on the own character, grey on others;
 * - otherwise by kind: normal red on the own character and orange on others,
 *   ignore defense cyan, excellent light green, critical blue (these three big),
 *   reflected pink, poison green, dark pink violet, anything else white. A double hit
 *   adds two darker copies below the number, a triple (combo) hit three, all big;
 * - then the shield damage in yellow-green, when there is any.
 */
export function damageTexts(hit: DamageHit): DamageText[] {
  const texts: DamageText[] = [];
  const health = hit.healthDamage;
  const shield = (): void => {
    if (hit.shieldDamage > 0) {
      texts.push({
        text: String(hit.shieldDamage),
        color: [...SHIELD_DAMAGE_COLOR],
        scale: DAMAGE_TEXT_SCALE,
        heightOffset: DAMAGE_TEXT_BASE_HEIGHT + SHIELD_DAMAGE_EXTRA_HEIGHT,
      });
    }
  };

  if (hit.hitShock) {
    texts.push({ text: String(health), color: [1, 0, 0], scale: DAMAGE_TEXT_SCALE, heightOffset: DAMAGE_TEXT_BASE_HEIGHT });
    shield();
    return texts;
  }

  if (health === 0) {
    const color: [number, number, number] = hit.toHero ? [1, 1, 1] : [0.5, 0.5, 0.5];
    texts.push({ text: MISS_TEXT, color, scale: DAMAGE_TEXT_SCALE, heightOffset: DAMAGE_TEXT_BASE_HEIGHT });
    shield();
    return texts;
  }

  let scale = DAMAGE_TEXT_SCALE;
  let light: [number, number, number];
  switch (hit.kind) {
    case DamageKind.NORMAL_RED: light = hit.toHero ? [1, 0, 0] : [1, 0.6, 0]; break;
    case DamageKind.IGNORE_DEFENSE_CYAN: scale = DAMAGE_TEXT_BIG_SCALE; light = [0, 1, 1]; break;
    case DamageKind.EXCELLENT_LIGHT_GREEN: scale = DAMAGE_TEXT_BIG_SCALE; light = [0, 1, 0.6]; break;
    case DamageKind.CRITICAL_BLUE: scale = DAMAGE_TEXT_BIG_SCALE; light = [0, 0.6, 1]; break;
    case DamageKind.REFLECTED_LIGHT_PINK: light = [1, 0, 1]; break;
    case DamageKind.POISON_DARK_GREEN: light = [0, 1, 0]; break;
    case DamageKind.DARK_PINK: light = [0.7, 0.4, 1]; break;
    default: light = [1, 1, 1]; break;
  }
  if (hit.isTripleDamage) scale = DAMAGE_TEXT_BIG_SCALE;

  const text = String(health);
  const [r, g, b] = light;
  if (hit.isTripleDamage || hit.isDoubleDamage) {
    // The darker copies, from the bottom up; the triple's top copy is full colour.
    texts.push({ text, color: clampColor(r - 0.4, g - 0.4, b - 0.4), scale, heightOffset: DAMAGE_TEXT_BASE_HEIGHT });
    texts.push({
      text,
      color: clampColor(r - 0.2, g - 0.2, b - 0.2),
      scale: scale + 5,
      heightOffset: DAMAGE_TEXT_BASE_HEIGHT + DAMAGE_STACK_STEP_HEIGHT,
    });
    if (hit.isTripleDamage) {
      texts.push({ text, color: [r, g, b], scale: scale + 10, heightOffset: DAMAGE_TEXT_BASE_HEIGHT + 2 * DAMAGE_STACK_STEP_HEIGHT });
    }
  }
  texts.push({ text, color: [r, g, b], scale, heightOffset: DAMAGE_TEXT_BASE_HEIGHT });
  shield();
  return texts;
}

/**
 * The number shown when an experience/kill packet carries the last hit's damage
 * (ReceiveDieExp and the extended kill packet): orange at the default size, or none
 * for no damage.
 */
export function killDamageText(damageOfLastHit: number): DamageText | null {
  if (damageOfLastHit <= 0) return null;
  return { text: String(damageOfLastHit), color: [...KILL_DAMAGE_COLOR], scale: DAMAGE_TEXT_SCALE, heightOffset: DAMAGE_TEXT_BASE_HEIGHT };
}

/** A number's upward speed when created, in world units per desktop frame (Gravity). */
export const DAMAGE_TEXT_START_SPEED = 10;
/** How much the upward speed drops each frame. The number disappears when it reaches 0. */
export const DAMAGE_TEXT_DECELERATION = 0.3;
/** How much a number shrinks each frame, down to DAMAGE_TEXT_SCALE. */
export const DAMAGE_TEXT_SHRINK_PER_FRAME = 5;
/** Its opacity is the upward speed × this (so fully opaque until the speed falls below 2.5). */
export const DAMAGE_TEXT_ALPHA_PER_SPEED = 0.4;
/** Frames a number lives: the speed is gone after 34 steps of 0.3. */
export const DAMAGE_TEXT_LIFETIME_FRAMES = Math.ceil(DAMAGE_TEXT_START_SPEED / DAMAGE_TEXT_DECELERATION);
export const DAMAGE_TEXT_LIFETIME_SECONDS = DAMAGE_TEXT_LIFETIME_FRAMES / ANIMATION_FRAMES_PER_SECOND;

export interface DamageTextMotion {
  /** How far it has risen above its start, in world units. */
  rise: number;
  /** 0..1 */
  alpha: number;
  scale: number;
  alive: boolean;
}

/**
 * Where a number is after `elapsedSeconds` (MovePoints, at 25 frames per second, made
 * continuous): it rises at 10 units per frame, slowing by 0.3 each frame, fades out
 * over its last frames and shrinks by 5 per frame down to 15.
 */
export function damageTextMotion(elapsedSeconds: number, startScale: number): DamageTextMotion {
  const frames = Math.max(0, elapsedSeconds * ANIMATION_FRAMES_PER_SECOND);
  const speed = DAMAGE_TEXT_START_SPEED - DAMAGE_TEXT_DECELERATION * frames;
  const rise = DAMAGE_TEXT_START_SPEED * frames - (DAMAGE_TEXT_DECELERATION * frames * (frames - 1)) / 2;
  return {
    rise: Math.max(0, rise),
    alpha: Math.min(1, Math.max(0, speed * DAMAGE_TEXT_ALPHA_PER_SPEED)),
    scale: Math.max(Math.min(startScale, DAMAGE_TEXT_SCALE), startScale - DAMAGE_TEXT_SHRINK_PER_FRAME * frames),
    alive: speed > 0,
  };
}

// ---------------------------------------------------------------------------
// Player animation speeds: the base table (OpenPlayers' PlaySpeed values in
// Engine/Object/ZzzOpenData.cpp), the attack and magic speed scaling (SetAttackSpeed)
// and the walking speeds (SetPlayerWalk) in Engine/Object/ZzzCharacter.cpp, and the
// changes within an action (CharacterAnimation).
// ---------------------------------------------------------------------------

export interface PlayerSpeedOptions {
  /** The attack speed stat. The desktop uses the own character's for every player model. */
  attackSpeed: number;
  /** The magic speed stat (same caveat). */
  magicSpeed: number;
  classIndex: number;
  /** Freeze debuff: walking at half speed. */
  frozen?: boolean;
  /** Blow of Destruction debuff: walking at a third of the speed. */
  blowOfDestruction?: boolean;
  /** The current frame in the action, for the actions that slow down partway. */
  frame?: number;
  /** For a Dark Lord's flash: party members slow it more. */
  partyMember?: boolean;
}

let baseSpeeds: number[] | null = null;

// OpenPlayers: the speeds set once when the player model loads. Actions it does not
// set keep 0 in the desktop (they do not animate) unless SetAttackSpeed sets them.
function basePlayerSpeeds(): number[] {
  if (baseSpeeds) return baseSpeeds;
  const A = PlayerActions;
  const s: number[] = new Array<number>(MAX_PLAYER_ACTION).fill(0);
  const range = (from: number, to: number, value: number): void => {
    for (let i = from; i <= to; i++) s[i] = value;
  };
  range(A.STOP_MALE, A.STOP_RIDE_WEAPON, 0.28);
  s[A.STOP_SWORD] = 0.26;
  s[A.STOP_TWO_HAND_SWORD] = 0.24;
  s[A.STOP_SPEAR] = 0.24;
  s[A.STOP_BOW] = 0.22;
  s[A.STOP_CROSSBOW] = 0.22;
  s[A.STOP_SUMMONER] = 0.24;
  s[A.STOP_WAND] = 0.3;
  range(A.WALK_MALE, A.RUN_RIDE_WEAPON, 0.3);
  s[A.WALK_WAND] = 0.44;
  s[A.RUN_WAND] = 0.76;
  s[A.WALK_SWIM] = 0.35;
  s[A.RUN_SWIM] = 0.35;
  range(A.DEFENSE1, A.SHOCK, 0.32);
  range(A.DIE1, A.DIE2, 0.45);
  range(A.SIT1, MAX_PLAYER_ACTION - 1, 0.4);
  s[A.SHOCK] = 0.4;
  s[A.SEE1] = 0.28;
  s[A.SEE_FEMALE1] = 0.28;
  s[A.HEALING1] = 0.2;
  s[A.HEALING_FEMALE1] = 0.2;
  s[A.JACK_1] = 0.38;
  s[A.JACK_2] = 0.38;
  s[A.SANTA_1] = 0.34;
  s[A.SANTA_2] = 0.3;
  s[A.SKILL_RIDER] = 0.2;
  s[A.SKILL_RIDER_FLY] = 0.2;
  s[A.STOP_TWO_HAND_SWORD_TWO] = 0.24;
  s[A.WALK_TWO_HAND_SWORD_TWO] = 0.3;
  s[A.RUN_TWO_HAND_SWORD_TWO] = 0.3;
  s[A.ATTACK_TWO_HAND_SWORD_TWO] = 0.24;
  s[A.ATTACK_DEATHSTAB] = 0.45;
  s[A.DARKLORD_STAND] = 0.3;
  s[A.DARKLORD_WALK] = 0.3;
  s[A.STOP_RIDE_HORSE] = 0.3;
  s[A.RUN_RIDE_HORSE] = 0.3;
  s[A.ATTACK_STRIKE] = 0.2;
  s[A.ATTACK_TELEPORT] = 0.28;
  s[A.ATTACK_RIDE_STRIKE] = 0.3;
  s[A.ATTACK_RIDE_TELEPORT] = 0.3;
  s[A.ATTACK_RIDE_HORSE_SWORD] = 0.28;
  s[A.ATTACK_RIDE_ATTACK_FLASH] = 0.3;
  s[A.ATTACK_RIDE_ATTACK_MAGIC] = 0.3;
  s[A.ATTACK_DARKHORSE] = 0.2;
  s[A.IDLE1_DARKHORSE] = 1;
  s[A.IDLE2_DARKHORSE] = 1;
  range(A.FENRIR_ATTACK, A.FENRIR_WALK_ONE_LEFT, 0.45);
  range(A.FENRIR_RUN, A.FENRIR_RUN_ONE_LEFT_ELF, 0.71);
  range(A.FENRIR_STAND, A.FENRIR_STAND_ONE_LEFT, 0.4);
  s[A.FENRIR_ATTACK_MAGIC] = 0.3;
  s[A.FENRIR_ATTACK_DARKLORD_STRIKE] = 0.3;
  s[A.FENRIR_ATTACK_DARKLORD_TELEPORT] = 0.3;
  s[A.FENRIR_ATTACK_DARKLORD_SWORD] = 0.28;
  s[A.FENRIR_ATTACK_DARKLORD_FLASH] = 0.3;
  s[A.HIGH_SHOCK] = 0.3;
  for (let i = A.RAGE_FENRIR; i <= A.RAGE_FENRIR_ATTACK_RIGHT; i++) {
    s[i] = i >= A.RAGE_FENRIR_TWO_SWORD && i <= A.RAGE_FENRIR_ONE_LEFT ? 0.225 : 0.45;
  }
  range(A.RAGE_FENRIR_STAND_TWO_SWORD, A.RAGE_FENRIR_STAND_ONE_LEFT, 0.2);
  s[A.RAGE_FENRIR_STAND] = 0.21;
  range(A.RAGE_FENRIR_RUN, A.RAGE_FENRIR_RUN_ONE_LEFT, 0.355);
  s[A.RAGE_UNI_RUN] = 0.3;
  s[A.RAGE_UNI_ATTACK_ONE_RIGHT] = 0.2;
  s[A.RAGE_UNI_STOP_ONE_RIGHT] = 0.18;
  s[A.STOP_RAGEFIGHTER] = 0.16;
  baseSpeeds = s;
  return s;
}

// SetAttackSpeed: the attack and skill actions, scaled by the attack and magic speed.
function attackSpeedOverride(action: number, attackSpeed: number, magicSpeed: number): number | undefined {
  const A = PlayerActions;
  const attack1 = attackSpeed * 0.004;
  const magic1 = magicSpeed * 0.004;
  const magic2 = magicSpeed * 0.002;
  const rage = attackSpeed * 0.002;
  const between = (from: number, to: number): boolean => action >= from && action <= to;
  switch (action) {
    case A.ATTACK_FIST: return 0.6 + attack1;
    case A.ATTACK_SKILL_SWORD1: case A.ATTACK_SKILL_SWORD2: case A.ATTACK_SKILL_SWORD4: return 0.3 + attack1;
    case A.ATTACK_SKILL_SWORD3: return 0.27 + attack1;
    case A.ATTACK_SKILL_SWORD5: case A.ATTACK_SKILL_WHEEL: return 0.24 + attack1;
    case A.ATTACK_DEATHSTAB: return 0.25 + attack1;
    case A.ATTACK_SKILL_SPEAR: case A.SKILL_RIDER: case A.SKILL_RIDER_FLY: return 0.3 + attack1;
    case A.ATTACK_TWO_HAND_SWORD_TWO: return 0.25 + attack1;
    case A.SKILL_ELF1: return 0.25 + magic1;
    case A.SKILL_TELEPORT: return 0.3 + magic2;
    case A.SKILL_FLASH: return 0.4 + magic2;
    case A.SKILL_INFERNO: return 0.6 + magic2;
    case A.SKILL_HELL: return 0.5 + magic2;
    case A.RIDE_SKILL: return 0.3 + magic2;
    case A.SKILL_HELL_BEGIN: return 0.5 + magic2;
    case A.ATTACK_STRIKE: return 0.25 + attack1;
    case A.ATTACK_RIDE_STRIKE: return 0.2 + attack1;
    case A.ATTACK_RIDE_HORSE_SWORD: return 0.25 + attack1;
    case A.ATTACK_RIDE_ATTACK_FLASH: return 0.4 + magic2;
    case A.ATTACK_RIDE_ATTACK_MAGIC: return 0.3 + magic2;
    case A.FENRIR_ATTACK: return 0.25 + attack1;
    case A.FENRIR_ATTACK_DARKLORD_STRIKE: return 0.2 + attack1;
    case A.FENRIR_ATTACK_DARKLORD_SWORD: return 0.25 + attack1;
    case A.FENRIR_ATTACK_DARKLORD_FLASH: return 0.4 + magic2;
    case A.FENRIR_ATTACK_TWO_SWORD: return 0.25 + attack1;
    case A.FENRIR_ATTACK_MAGIC: return 0.37 + magic2;
    case A.FENRIR_ATTACK_CROSSBOW: return 0.3 + attack1;
    case A.FENRIR_ATTACK_SPEAR: case A.FENRIR_ATTACK_ONE_SWORD: return 0.25 + attack1;
    case A.FENRIR_ATTACK_BOW: return 0.3 + attack1;
    case A.ATTACK_ONE_FLASH: return 0.4 + attack1;
    case A.ATTACK_RUSH: return 0.3 + attack1;
    case A.ATTACK_DEATH_CANNON: return 0.2 + attack1;
    case A.SKILL_SLEEP: case A.SKILL_SLEEP_UNI: case A.SKILL_SLEEP_DINO: case A.SKILL_SLEEP_FENRIR: return 0.3 + magic2;
    case A.SKILL_LIGHTNING_ORB: return 0.4 + magic2;
    case A.SKILL_LIGHTNING_ORB_UNI: case A.SKILL_LIGHTNING_ORB_DINO: case A.SKILL_LIGHTNING_ORB_FENRIR: return 0.25 + magic2;
    case A.SKILL_CHAIN_LIGHTNING: return 0.25 + magic2;
    case A.SKILL_CHAIN_LIGHTNING_UNI: case A.SKILL_CHAIN_LIGHTNING_DINO: case A.SKILL_CHAIN_LIGHTNING_FENRIR: return 0.15 + magic2;
    case A.SKILL_DRAIN_LIFE: case A.SKILL_DRAIN_LIFE_UNI: case A.SKILL_DRAIN_LIFE_DINO: case A.SKILL_DRAIN_LIFE_FENRIR: return 0.25 + magic2;
    case A.SKILL_GIGANTICSTORM: return 0.55 + magic1;
    case A.SKILL_FLAMESTRIKE: return 0.69 + magic2;
    case A.SKILL_LIGHTNING_SHOCK: return 0.35 + magic2;
    case A.SKILL_SUMMON: case A.SKILL_SUMMON_UNI: case A.SKILL_SUMMON_DINO: case A.SKILL_SUMMON_FENRIR: return 0.25;
    case A.SKILL_BLOW_OF_DESTRUCTION: return 0.3;
    case A.RECOVER_SKILL: return 0.33;
    case A.SKILL_SWELL_OF_MP: return 0.2;
    case A.ATTACK_SKILL_FURY_STRIKE: return 0.38;
    case A.SKILL_VITALITY: return 0.34;
    case A.SKILL_HELL_START: return 0.3;
    case A.ATTACK_TELEPORT: return 0.28;
    case A.ATTACK_RIDE_TELEPORT: return 0.3;
    case A.ATTACK_DARKHORSE: return 0.3;
    case A.FENRIR_ATTACK_DARKLORD_TELEPORT: return 0.3;
    case A.ATTACK_REMOVAL: return 0.28;
    case A.SKILL_THRUST: case A.SKILL_STAMP: case A.SKILL_GIANTSWING: case A.SKILL_DRAGONKICK: return 0.4 + rage;
    case A.SKILL_DARKSIDE_READY: case A.SKILL_DARKSIDE_ATTACK: case A.SKILL_DRAGONLORE: return 0.3 + rage;
    case A.SKILL_ATT_UP_OURFORCES: case A.SKILL_HP_UP_OURFORCES: return 0.35;
    case A.RAGE_FENRIR_ATTACK_RIGHT: return 0.25 + rage;
  }
  // The ranges, after the single values as in SetAttackSpeed (none of them overlap these).
  if (between(A.ATTACK_BOW, A.ATTACK_FLY_CROSSBOW) || between(A.ATTACK_RIDE_BOW, A.ATTACK_RIDE_CROSSBOW)) return 0.3 + attack1;
  if (between(A.ATTACK_SWORD_RIGHT1, A.ATTACK_RIDE_CROSSBOW)) return 0.25 + attack1;
  if (between(A.SKILL_HAND1, A.SKILL_WEAPON2)) return 0.29 + magic2;
  if (between(A.ATTACK_BOW_UP, A.ATTACK_RIDE_CROSSBOW_UP)) return 0.3 + attack1;
  return undefined;
}

// SetPlayerWalk: the walking and running speeds, set whenever a character starts to walk.
function walkSpeedOverride(action: number, opts: PlayerSpeedOptions): number | undefined {
  const A = PlayerActions;
  const rageFighter = opts.classIndex === CharacterClass.RAGE_FIGHTER;
  const slow = (value: number): number => (opts.frozen ? value * 0.5 : opts.blowOfDestruction ? value * 0.33 : value);
  if (action >= A.WALK_MALE && action <= A.WALK_CROSSBOW) return slow(rageFighter ? 0.32 : 0.33);
  if (action >= A.RUN && action <= A.RUN_RIDE_WEAPON) return slow(rageFighter ? 0.28 : 0.34);
  switch (action) {
    case A.RAGE_UNI_RUN: case A.RAGE_UNI_RUN_ONE_RIGHT: return 0.34;
    case A.CHANGE_UP: return 0.049;
    case A.RUN_RIDE_HORSE: case A.DARKLORD_WALK: return slow(0.33);
    case A.WALK_WAND: return 0.44;
    case A.RUN_WAND: return 0.76;
  }
  return undefined;
}

/**
 * The player model's speed for an action in keys per desktop frame (its PlaySpeed),
 * with CharacterAnimation's changes partway through some actions when `frame` is given.
 */
export function playerActionPlaySpeed(action: number, opts: PlayerSpeedOptions): number {
  if (action < 0 || action >= MAX_PLAYER_ACTION) return 0;
  const A = PlayerActions;
  let speed =
    walkSpeedOverride(action, opts) ?? attackSpeedOverride(action, opts.attackSpeed, opts.magicSpeed) ?? basePlayerSpeeds()[action] ?? 0;
  if (speed < 0) speed = 0;
  const frame = opts.frame;
  if (frame !== undefined) {
    if (action === A.SKILL_VITALITY && frame > 6) {
      speed /= 2;
    } else if ((action === A.ATTACK_TELEPORT || action === A.ATTACK_RIDE_TELEPORT || action === A.FENRIR_ATTACK_DARKLORD_TELEPORT) && frame > 5.5) {
      speed /= 10;
    } else if (
      opts.classIndex === CharacterClass.DARK_LORD &&
      (action === A.SKILL_FLASH || action === A.ATTACK_RIDE_ATTACK_FLASH || action === A.FENRIR_ATTACK_DARKLORD_FLASH) &&
      frame > 1 && frame < 3
    ) {
      speed /= opts.partyMember ? 8 : 2;
    }
  }
  if (action === A.SKILL_HELL_BEGIN) speed /= 2;
  return speed;
}

/** The player model's speed for an action in keys per second. */
export function playerActionSpeed(action: number, opts: PlayerSpeedOptions): number {
  return playerActionPlaySpeed(action, opts) * ANIMATION_FRAMES_PER_SECOND;
}

// ---------------------------------------------------------------------------
// Which actions repeat (Action_t::Loop, BMD::PlayAnimation, PlayerStopAnimationSetting
// and EtcStopAnimationSetting in Engine/Object/ZzzCharacter.cpp).
// ---------------------------------------------------------------------------

/**
 * - loop: plays over and over (standing, walking, running, sitting, poses);
 * - once: plays once, then the character goes back to standing (attacks, skills,
 *   getting hit, emotes);
 * - hold: plays once and stays on its last frame (dying).
 */
export type ActionPlayback = 'loop' | 'once' | 'hold';

/**
 * PlayerStopAnimationSetting: whether the desktop puts the player back to standing
 * (SetPlayerStop) when this action reaches its end. True for standing actions too,
 * where it picks the standing action again.
 */
export function playerActionReturnsToStop(action: number): boolean {
  const A = PlayerActions;
  const between = (from: number, to: number): boolean => action >= from && action <= to;
  if (action === A.DIE1 || action === A.DIE2) return false;
  return (
    action < A.WALK_MALE ||
    between(A.PROVOCATION, A.CHEERS) ||
    between(A.IDLE1_DARKHORSE, A.IDLE2_DARKHORSE) ||
    (between(A.SKILL_THRUST, A.RAGE_FENRIR_ATTACK_RIGHT) &&
      !between(A.RAGE_FENRIR_RUN, A.RAGE_FENRIR_RUN_ONE_LEFT) &&
      !between(A.RAGE_UNI_RUN, A.RAGE_UNI_STOP_ONE_RIGHT)) ||
    action === A.STOP_RAGEFIGHTER ||
    (between(A.ATTACK_FIST, A.SHOCK) &&
      action !== A.WALK_TWO_HAND_SWORD_TWO && action !== A.RUN_TWO_HAND_SWORD_TWO &&
      action !== A.FLY_RIDE && action !== A.FLY_RIDE_WEAPON &&
      action !== A.SKILL_HELL_BEGIN &&
      action !== A.DARKLORD_WALK && action !== A.RUN_RIDE_HORSE &&
      !between(A.FENRIR_RUN, A.FENRIR_RUN_ONE_LEFT_ELF) &&
      action !== A.RECOVER_SKILL)
  );
}

// Standing and moving actions: going back to standing re-picks the same kind of
// action, so they repeat.
function isStandOrMoveAction(action: number): boolean {
  const A = PlayerActions;
  const between = (from: number, to: number): boolean => action >= from && action <= to;
  return (
    between(A.STOP_MALE, A.RUN_RIDE_WEAPON) ||
    between(A.FLY_RIDE, A.RUN_RIDE_HORSE) ||
    between(A.FENRIR_RUN, A.FENRIR_WALK_ONE_LEFT) ||
    between(A.STOP_TWO_HAND_SWORD_TWO, A.RUN_TWO_HAND_SWORD_TWO) ||
    between(A.RAGE_UNI_RUN, A.RAGE_UNI_STOP_ONE_RIGHT) ||
    between(A.RAGE_FENRIR_WALK, A.RAGE_FENRIR_STAND_ONE_LEFT) ||
    action === A.STOP_RAGEFIGHTER
  );
}

/** How a player model action plays (see ActionPlayback). */
export function playerActionPlayback(action: number): ActionPlayback {
  const A = PlayerActions;
  // Action_t::Loop is set for these: the frame stops at the end. The hell skill's
  // start also has it, but CharacterAnimation restarts it, so it repeats.
  if (action === A.DIE1 || action === A.DIE2 || action === A.COME_UP) return 'hold';
  if (isStandOrMoveAction(action)) return 'loop';
  return playerActionReturnsToStop(action) ? 'once' : 'loop';
}

/**
 * How a monster model action plays (EtcStopAnimationSetting): its second idle, getting
 * hit and attacks 1 to 5 play once and go back to STOP1. `holdLastFrame` is the
 * model's list of that name in Models.json: those actions stay on their last frame.
 * Everything else repeats, including DIE for models without it in holdLastFrame (the
 * desktop's frame wraps; the body is removed when the server takes it out of view).
 */
export function monsterActionPlayback(action: number, holdLastFrame?: readonly string[]): ActionPlayback {
  const name = MONSTER_ACTION_NAMES[action];
  if (name !== undefined && holdLastFrame?.includes(name)) return 'hold';
  const M = MonsterActions;
  switch (action) {
    case M.STOP2: case M.SHOCK: case M.ATTACK1: case M.ATTACK2: case M.ATTACK3: case M.ATTACK4: case M.ATTACK5:
      return 'once';
    default:
      return 'loop';
  }
}
