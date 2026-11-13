// The auto play (helper) settings, as the desktop keeps them (MUHelper::ConfigData,
// MuHelperData.h). The server stores them per character (MuHelperConfigurationData /
// MuHelperSaveDataRequest, 257 bytes; see decodeAutoPlayConfig / encodeAutoPlayConfig).

export const SkillActivation = {
  ALWAYS: 0x00, ON_TIMER: 0x01, ON_CONDITION: 0x02,
  ON_MOBS_NEARBY: 0x04, ON_MOBS_ATTACKING: 0x08,
  ON_MORE_THAN_TWO_MOBS: 0x10, ON_MORE_THAN_THREE_MOBS: 0x20, ON_MORE_THAN_FOUR_MOBS: 0x40, ON_MORE_THAN_FIVE_MOBS: 0x80,
} as const;

export const PetAttackMode = { CEASE: 0, AUTO: 1, TOGETHER: 2 } as const;

export interface AutoPlayConfig {
  huntingRange: number;
  longRangeCounterAttack: boolean;
  returnToOriginalPosition: boolean;
  maxSecondsAway: number;
  /** The basic skill and the two activation skills (skill numbers, 0 for none). */
  skills: [number, number, number];
  /** SkillActivation flags of each skill. */
  skillConditions: [number, number, number];
  /** Seconds between casts for ON_TIMER. */
  skillIntervals: [number, number, number];
  useCombo: boolean;
  buffs: [number, number, number];
  buffDuration: boolean;
  buffDurationParty: boolean;
  buffCastInterval: number;
  autoHeal: boolean;
  healThreshold: number;
  supportParty: boolean;
  autoHealParty: boolean;
  healPartyThreshold: number;
  useHealPotion: boolean;
  potionThreshold: number;
  useDrainLife: boolean;
  useDarkRaven: boolean;
  darkRavenMode: number;
  repairItem: boolean;
  obtainingRange: number;
  pickAllItems: boolean;
  pickSelectItems: boolean;
  pickJewel: boolean;
  pickZen: boolean;
  pickAncient: boolean;
  pickExcellent: boolean;
  pickExtraItems: boolean;
  extraItems: string[];
  useSelfDefense: boolean;
  autoAcceptFriend: boolean;
  autoAcceptGuild: boolean;
  fallbackBasicAttack: boolean;
}

export function defaultAutoPlayConfig(): AutoPlayConfig {
  return {
    huntingRange: 0, longRangeCounterAttack: false, returnToOriginalPosition: false, maxSecondsAway: 0,
    skills: [0, 0, 0], skillConditions: [0, 0, 0], skillIntervals: [0, 0, 0], useCombo: false,
    buffs: [0, 0, 0], buffDuration: false, buffDurationParty: false, buffCastInterval: 0,
    autoHeal: false, healThreshold: 0, supportParty: false, autoHealParty: false, healPartyThreshold: 0,
    useHealPotion: false, potionThreshold: 0, useDrainLife: false, useDarkRaven: false, darkRavenMode: 0,
    repairItem: false, obtainingRange: 0, pickAllItems: false, pickSelectItems: false, pickJewel: false,
    pickZen: false, pickAncient: false, pickExcellent: false, pickExtraItems: false, extraItems: [],
    useSelfDefense: false, autoAcceptFriend: false, autoAcceptGuild: false, fallbackBasicAttack: true,
  };
}

/** The settings the desktop puts in place on entering the game, before the server's copy arrives
 *  (CMuHelperConfigWindow::Reset, also the settings window's "Initialization"). */
export function initialAutoPlayConfig(): AutoPlayConfig {
  return {
    ...defaultAutoPlayConfig(),
    huntingRange: 6, maxSecondsAway: 10, returnToOriginalPosition: true,
    buffDuration: true, buffDurationParty: true, healThreshold: 60, potionThreshold: 40, healPartyThreshold: 60,
    darkRavenMode: PetAttackMode.CEASE, obtainingRange: 8,
  };
}

/** A deep copy (the settings windows edit a staged copy, as the desktop's StagedConfig()). */
export function cloneAutoPlayConfig(config: AutoPlayConfig): AutoPlayConfig {
  return {
    ...config,
    skills: [...config.skills], skillConditions: [...config.skillConditions], skillIntervals: [...config.skillIntervals],
    buffs: [...config.buffs], extraItems: [...config.extraItems],
  };
}

// ---------- the 257-byte blob (PRECEIVE_MUHELPER_DATA, #pragma pack(1), little endian) ----------
//
//  0      DataStartMarker
//  1      bits 3-7: JewelOrGem, SetItem, ExcellentItem, Zen, AddExtraItem
//  2      HuntingRange (low nibble), ObtainRange (high nibble)
//  3      DistanceMin (WORD, seconds away)        5  BasicSkill1          7  ActivationSkill1
//  9      DelayMinSkill1                          11 ActivationSkill2     13 DelayMinSkill2
//  15     CastingBuffMin                          17/19/21 BuffSkill0/1/2NumberID
//  23     HPStatusAutoPotion (low), HPStatusAutoHeal (high)      (value * 10 = percent)
//  24     HPStatusOfPartyMembers (low), HPStatusDrainLife (high)
//  25     bits: AutoPotion, AutoHeal, DrainLife, LongDistanceAttack, OriginalPosition, Combo, Party, PreferenceOfPartyHeal
//  26     bits: BuffDurationforAllPartyMembers, UseDarkSpirits, BuffDuration, Skill1Delay, Skill1Con, Skill1PreCon, Skill1SubCon (2)
//  27     bits: Skill2Delay, Skill2Con, Skill2PreCon, Skill2SubCon (2), RepairItem, PickAllNearItems, PickSelectedItems
//  28     PetAttack
//  29     bits 0-3: UseSelfDefense, AutoAcceptFriend, AutoAcceptGuild, FallbackBasicAttack (client-local; the server echoes them)
//  30-64  unused
//  65     ExtraItems[12][15] (multibyte text, NUL-padded)
//  245-256 unused (the desktop sends the 245-byte struct; the rest stays 0)

export const AUTO_PLAY_DATA_LENGTH = 257;
const EXTRA_ITEMS_OFFSET = 65;
const EXTRA_ITEM_SLOTS = 12;
const EXTRA_ITEM_BYTES = 15;

const SUB_CONDITIONS = [
  SkillActivation.ON_MORE_THAN_TWO_MOBS, SkillActivation.ON_MORE_THAN_THREE_MOBS,
  SkillActivation.ON_MORE_THAN_FOUR_MOBS, SkillActivation.ON_MORE_THAN_FIVE_MOBS,
] as const;

const bit = (value: number, index: number): boolean => ((value >> index) & 1) !== 0;
const flag = (value: boolean, index: number): number => (value ? 1 << index : 0);
// A C++ (int / 10) for the 4-bit percent fields.
const tenths = (percent: number): number => Math.trunc(percent / 10) & 0x0f;

/** Compares strings by code point, as std::set<std::wstring> orders them. */
function compareCodePoints(a: string, b: string): number {
  const x = Array.from(a, (c) => c.codePointAt(0)!), y = Array.from(b, (c) => c.codePointAt(0)!);
  for (let i = 0; i < Math.min(x.length, y.length); i++) if (x[i] !== y[i]) return x[i] - y[i];
  return x.length - y.length;
}

/** The extra item names as the desktop's std::set keeps them: unique, in code point order. */
export function normalizeExtraItems(names: Iterable<string>): string[] {
  return [...new Set(names)].sort(compareCodePoints);
}

// wcstombs(dest, name, 15) in a UTF-8 locale, then the desktop's check: a failed conversion or one
// that filled all 15 bytes leaves the slot empty; a name cut before a character that no longer
// fits keeps the part that did.
function encodeExtraItem(name: string): Uint8Array | null {
  const encoder = new TextEncoder();
  const out: number[] = [];
  for (const char of name) {
    const code = char.codePointAt(0)!;
    if (code >= 0xd800 && code <= 0xdfff) return null; // a lone surrogate has no multibyte form
    const bytes = encoder.encode(char);
    if (out.length + bytes.length > EXTRA_ITEM_BYTES) break;
    out.push(...bytes);
  }
  return out.length === EXTRA_ITEM_BYTES ? null : Uint8Array.from(out);
}

/** ConfigDataSerDe::Deserialize: the settings from the server's 257 bytes (MuHelperConfigurationData.HelperData). */
export function decodeAutoPlayConfig(bytes: Uint8Array): AutoPlayConfig {
  const b = new Uint8Array(AUTO_PLAY_DATA_LENGTH);
  b.set(bytes.subarray(0, AUTO_PLAY_DATA_LENGTH));
  const word = (at: number) => b[at] | (b[at + 1] << 8);
  const config = defaultAutoPlayConfig();

  config.huntingRange = b[2] & 0x0f;
  config.maxSecondsAway = word(3);
  config.longRangeCounterAttack = bit(b[25], 3);
  config.returnToOriginalPosition = bit(b[25], 4);

  config.skills = [word(5), word(7), word(11)];
  config.skillIntervals = [0, word(9), word(13)];
  // The desktop always sets one pre-condition and one sub-condition, even when the bits are 0.
  const condition = (delay: boolean, con: boolean, preCon: boolean, subCon: number) =>
    (delay ? SkillActivation.ON_TIMER : 0) | (con ? SkillActivation.ON_CONDITION : 0)
    | (preCon ? SkillActivation.ON_MOBS_ATTACKING : SkillActivation.ON_MOBS_NEARBY) | SUB_CONDITIONS[subCon];
  config.skillConditions = [
    0,
    condition(bit(b[26], 3), bit(b[26], 4), bit(b[26], 5), (b[26] >> 6) & 0x03),
    condition(bit(b[27], 0), bit(b[27], 1), bit(b[27], 2), (b[27] >> 3) & 0x03),
  ];
  config.useCombo = bit(b[25], 5);

  config.buffs = [word(17), word(19), word(21)];
  config.buffDuration = bit(b[26], 2);
  config.buffDurationParty = bit(b[26], 0);
  config.buffCastInterval = word(15);

  config.autoHeal = bit(b[25], 1);
  config.healThreshold = (b[23] >> 4) * 10;
  config.useDrainLife = bit(b[25], 2);
  config.useHealPotion = bit(b[25], 0);
  config.potionThreshold = (b[23] & 0x0f) * 10;
  config.supportParty = bit(b[25], 6);
  config.autoHealParty = bit(b[25], 7);
  config.healPartyThreshold = (b[24] & 0x0f) * 10;

  config.useDarkRaven = bit(b[26], 1);
  config.darkRavenMode = b[28];
  config.repairItem = bit(b[27], 5);

  config.obtainingRange = b[2] >> 4;
  config.pickAllItems = bit(b[27], 6);
  config.pickSelectItems = bit(b[27], 7);
  config.pickZen = bit(b[1], 6);
  config.pickJewel = bit(b[1], 3);
  config.pickExcellent = bit(b[1], 5);
  config.pickAncient = bit(b[1], 4);
  config.pickExtraItems = bit(b[1], 7);

  // mbstowcs of each 15-byte field (which need not end in a NUL); an invalid one is skipped.
  const decoder = new TextDecoder('utf-8', { fatal: true });
  const names: string[] = [];
  for (let i = 0; i < EXTRA_ITEM_SLOTS; i++) {
    const field = b.subarray(EXTRA_ITEMS_OFFSET + i * EXTRA_ITEM_BYTES, EXTRA_ITEMS_OFFSET + (i + 1) * EXTRA_ITEM_BYTES);
    const end = field.indexOf(0);
    const text = field.subarray(0, end < 0 ? EXTRA_ITEM_BYTES : end);
    if (!text.length) continue;
    try { names.push(decoder.decode(text)); } catch { /* not valid multibyte text */ }
  }
  config.extraItems = normalizeExtraItems(names);

  config.useSelfDefense = bit(b[29], 0);
  config.autoAcceptFriend = bit(b[29], 1);
  config.autoAcceptGuild = bit(b[29], 2);
  config.fallbackBasicAttack = bit(b[29], 3);
  return config;
}

/** ConfigDataSerDe::Serialize: the 257 bytes for MuHelperSaveDataRequest.HelperData. */
export function encodeAutoPlayConfig(config: AutoPlayConfig): Uint8Array {
  const b = new Uint8Array(AUTO_PLAY_DATA_LENGTH);
  const setWord = (at: number, value: number) => { b[at] = value & 0xff; b[at + 1] = (value >> 8) & 0xff; };

  b[1] = flag(config.pickJewel, 3) | flag(config.pickAncient, 4) | flag(config.pickExcellent, 5)
    | flag(config.pickZen, 6) | flag(config.pickExtraItems, 7);
  b[2] = (config.huntingRange & 0x0f) | ((config.obtainingRange & 0x0f) << 4);
  setWord(3, Math.min(Math.max(Math.trunc(config.maxSecondsAway), 0), 0xffff));
  setWord(5, config.skills[0]);
  setWord(7, config.skills[1]);
  setWord(9, config.skillIntervals[1]);
  setWord(11, config.skills[2]);
  setWord(13, config.skillIntervals[2]);
  setWord(15, config.buffCastInterval);
  setWord(17, config.buffs[0]);
  setWord(19, config.buffs[1]);
  setWord(21, config.buffs[2]);

  b[23] = tenths(config.potionThreshold) | (tenths(config.healThreshold) << 4);
  // The desktop writes the heal threshold as the drain-life one too.
  b[24] = tenths(config.healPartyThreshold) | (tenths(config.healThreshold) << 4);
  b[25] = flag(config.useHealPotion, 0) | flag(config.autoHeal, 1) | flag(config.useDrainLife, 2)
    | flag(config.longRangeCounterAttack, 3) | flag(config.returnToOriginalPosition, 4) | flag(config.useCombo, 5)
    | flag(config.supportParty, 6) | flag(config.autoHealParty, 7);

  // Timer, condition, pre-condition (nearby 0, attacking 1) and sub-condition (more than 2..5 mobs: 0..3).
  const conditionBits = (bits: number) => {
    const preCon = bits & SkillActivation.ON_MOBS_NEARBY ? 0 : bits & SkillActivation.ON_MOBS_ATTACKING ? 1 : 0;
    const subCon = Math.max(0, SUB_CONDITIONS.findIndex((c) => (bits & c) !== 0));
    return { delay: (bits & SkillActivation.ON_TIMER) !== 0, con: (bits & SkillActivation.ON_CONDITION) !== 0, preCon, subCon };
  };
  const skill1 = conditionBits(config.skillConditions[1]);
  const skill2 = conditionBits(config.skillConditions[2]);
  b[26] = flag(config.buffDurationParty, 0) | flag(config.useDarkRaven, 1) | flag(config.buffDuration, 2)
    | flag(skill1.delay, 3) | flag(skill1.con, 4) | (skill1.preCon << 5) | (skill1.subCon << 6);
  b[27] = flag(skill2.delay, 0) | flag(skill2.con, 1) | (skill2.preCon << 2) | (skill2.subCon << 3)
    | flag(config.repairItem, 5) | flag(config.pickAllItems, 6) | flag(config.pickSelectItems, 7);
  b[28] = config.darkRavenMode & 0xff;
  b[29] = flag(config.useSelfDefense, 0) | flag(config.autoAcceptFriend, 1) | flag(config.autoAcceptGuild, 2)
    | flag(config.fallbackBasicAttack, 3);

  normalizeExtraItems(config.extraItems).slice(0, EXTRA_ITEM_SLOTS).forEach((name, i) => {
    const bytes = encodeExtraItem(name);
    if (bytes) b.set(bytes, EXTRA_ITEMS_OFFSET + i * EXTRA_ITEM_BYTES);
  });
  return b;
}
