// The item tooltip of the inventory, as the desktop client writes it (Engine/Object/ZzzInventory.cpp,
// BuildItemTooltipText): the lines of the item's tooltip layout (Data/Items/Tooltips/Tooltip.json) with the parts
// that stay code between their sections (the name, the options, the set and socket lines). The numbers come from
// the item stat file (Data/Items/Stats/ItemStats.json, GameLogic/Items/ItemStats.cpp) and the option files
// (Data/Items/Options), the ancient sets, Jewel of Harmony and socket options from the client's .bmd files.
//
// tooltipLines(item, character) gives the rows: `color` is the text color the desktop tooltip draws (white, blue,
// red, yellow, green, gray, purple, redPurple, violet, orange), `background` the highlight behind a darkRed,
// darkBlue, darkYellow or greenBlue line (null for the others), `spacer` a blank row (the desktop's "\n" rows, drawn
// at half a line's height).

import { loadItemCatalog, itemDefinition } from './items.js';
import type { ItemDefinition, LocalizedText } from './items.js';
import type { Item } from './inventory.js';
import { loadMonsterCatalog } from './monsterData.js';
import type { MonsterCatalog } from './monsterData.js';
import { buxConvert } from '../formats.js';

/** A row of the tooltip. */
export interface TooltipLine {
  text: string;
  color: string;
  bold: boolean;
  /** The highlight behind the text: darkRed, darkBlue, darkYellow or greenBlue; null for none. */
  background: string | null;
  /** A blank row. */
  spacer: boolean;
}

/** What the tooltip reads of the character; a stat left out shows no "lacking" line for it. */
export interface TooltipCharacter {
  /** 0 Dark Wizard, 1 Dark Knight, 2 Elf, 3 Magic Gladiator, 4 Dark Lord, 5 Summoner, 6 Rage Fighter. */
  classIndex?: number;
  /** 1 to 3: the first, second or third class (1 when left out). */
  classLevel?: number;
  level?: number;
  strength?: number;
  agility?: number;
  vitality?: number;
  energy?: number;
  leadership?: number;
  /** For the socket options that grow with the maximum life or mana. */
  maxLife?: number;
  maxMana?: number;
}

const ITEMS_PER_GROUP = 512;
const LEVEL_COUNT = 16;
const MAX_VALUE = 65535;
const MAX_SOCKETS = 5;
const SOCKET_EMPTY = 0xfe;
const MAX_SOCKET_OPTION = 50;
const MAX_DESCRIPTION_ROWS = 32;
const MAX_ADDED_ROWS = 16;

const CLASS_KEYS = ['darkWizard', 'darkKnight', 'fairyElf', 'magicGladiator', 'darkLord', 'summoner', 'rageFighter'];
// A class at the level an item needs of it; null for a level the class skips (ClassLineName).
const CLASS_NAMES: (string | null)[][] = [
  ['Dark Wizard', 'Soul Master', 'Grand Master'],
  ['Dark Knight', 'Blade Knight', 'Blade Master'],
  ['Elf', 'Muse Elf', 'High Elf'],
  ['Magic Gladiator', null, 'Dual Master'],
  ['Dark Lord', null, 'Lord Emperor'],
  ['Summoner', 'Bloody Summoner', 'Dimension Master'],
  ['Rage Fighter', null, 'Fist Master'],
];
const RESISTANCE_KEYS = ['ice', 'poison', 'lightning', 'fire', 'earth', 'wind', 'water'];
const RESISTANCE_VALUES = RESISTANCE_KEYS.map((key) => `${key}Resistance`);
const REQUIREMENT_STATS = ['strength', 'dexterity', 'energy', 'vitality', 'leadership'] as const;
type RequirementStat = (typeof REQUIREMENT_STATS)[number];
const STAT_KEYS = ['damage', 'magicPower', 'blockRate', 'defense', 'magicDefense'] as const;
type StatKey = (typeof STAT_KEYS)[number];
// The text values; every other built-in value is a number (ItemDescriptionPlaceholders.cpp).
const TEXT_VALUES = new Set(['name', 'className', 'expireDate', 'addOptionDuration']);
const BUILT_IN_VALUES = new Set(['level', 'damageMin', 'damageMax', 'defense', 'magicDefense', 'blockRate', 'attackSpeed',
  'walkSpeed', 'magicPower', 'durability', 'maxDurability', ...RESISTANCE_VALUES, 'requiredLevel', 'requiredStrength',
  'requiredDexterity', 'requiredVitality', 'requiredEnergy', 'requiredLeadership', 'missingLevel', 'missingStrength',
  'missingDexterity', 'missingVitality', 'missingEnergy', 'missingLeadership', 'addOptionValue1', 'addOptionValue2',
  'addOptionDuration', 'heroLevel', 'levelPlusOne', 'name', 'excellentFlags', 'bundleCount', 'className', 'expireDate']);
const STATES = new Set(['hasDamage', 'skillBook', 'twoHanded', 'excellent', 'ancient', 'damageHarmonyBonus',
  'defenseHarmonyBonus', 'armour', 'strengthReduced', 'dexterityReduced', 'strengthRequired', 'dexterityRequired',
  'wizardryWeapon', 'hasDurability', 'timed', 'hasOptions', 'socketItem', 'upperTierWings', 'skillLuckOrOption',
  'setItem', 'allClasses', 'classUsable', 'boots', 'gloves', 'expired']);

// The tooltip's colors as drawn (UI tooltip.rcss): the highlight colors are white text on a background.
const HIGHLIGHTS: Record<string, string> = { darkRed: 'white', darkBlue: 'white', darkYellow: 'white', greenBlue: 'green' };

// The desktop's English UI texts the code lines use (Localization/Game.en.resx).
const T = {
  excellent: 'Excellent',
  setItemOptionInfo: 'Set Item option info',
  socketItemOptionInfo: 'Socket Item option info',
  bonusSocketOption: 'Bonus socket option',
  noItemApplication: 'No item application',
  socket: (index: number, text: string) => `Socket ${index}: ${text}`,
  socketCategories: [null, 'Fire', 'Water', 'Ice', 'Wind', 'Lightning', 'Earth', 'Unique'] as (string | null)[],
  ancientBonus: [null, 'Increase strength +', 'Increase agility +', 'Increase energy +', 'Increase stamina +'] as (string | null)[],
  increaseAttributeDamage: 'Increase attribute damage',
  reinforcementOptionError: 'Reinforcement option error',
  sendScreenshots: 'Send screenshots with the report.',
  guardianOptions: [null, 'Attack sucess rate increase +%d', 'Additional Damage +%d', 'Defense success rate increase +%d',
    'Defensive skill +%d', 'Max. HP increase +%d', 'Max. SD increase +%d', 'SD auto recovery',
    'SD recovery rate increase +%d%%'] as (string | null)[],
  day: 'Day', hour: 'Hour', minute: 'Minute', second: 'Second', lessThanAMinute: 'Less than 1 minutes',
};

// The options the code still adds (SetItemAttributes, CalcWingOptions), by their AT_ number's meaning.
const CODE_OPTION_TEXTS = {
  improveDamage: 'Additional Dmg +%d',
  improveMagic: 'Additional Wizardry Dmg +%d',
  improveCurse: 'Additional Curse Spell +%d',
  improveDefense: 'Additional defense +%d',
  lifeRegeneration: 'Automatic HP recovery %d%%',
  improveHpMax: 'HP +%d increased',
  improveMpMax: 'Mana +%d increased',
  onePercentDamage: "Ignor opponent's defensive power by %d%%",
  improveAgMax: 'Max AG +%d increased',
  improveAttackSpeed: 'Increase Attacking(Wizardry)speed +%d',
  improveCharisma: 'Increase command +%d',
  damageReflection: "Return's the enemy's attack power in %d%%",
  recoverFullLife: 'Complete recovery of life in %d%% rate',
  recoverFullMana: 'Complete recover of Mana in %d%% rate',
  damageAbsorb: 'Absorb %d%% additional damage',
  improveMagicPercent: 'Increase Wizardry Dmg +%d%%',
  improveDamagePercent: 'Increase Damage +%d%%',
};
type CodeOption = keyof typeof CODE_OPTION_TEXTS;

// Socket items (CSocketItemMgr::IsSocketItem): weapons and shields by group, and the armour sets 45 to 53.
const SOCKET_ITEMS: Record<number, number[]> = { 0: [26, 27, 28], 2: [16, 17], 4: [23], 5: [30, 31, 32], 6: [17, 18, 19, 20] };
// Bundled jewels (COMGEM::isCompiledGem), group 12.
const JEWEL_BUNDLES = [30, 31, 136, 137, 138, 139, 140, 141, 142, 143];

// ---------- the data files' shapes ----------

type Text = string | LocalizedText;

/** A tooltip line of a layout, an item's description, or a line an item adds to its layout. */
interface TooltipLineData {
  id?: string;
  section?: string;
  text?: Text;
  when?: string[];
  color?: string;
  colors?: { when: string[]; color: string }[];
  bold?: boolean;
  values?: Record<string, string>;
  forEachClass?: boolean;
  /** [lowest, highest] item level (description and name lines). */
  levels?: [number, number];
  /** "options": a description line drawn after the options. */
  after?: string;
}

interface LineAddition<TLine> { after: string; lines: TLine[] }

interface LayoutData {
  name: string;
  lines?: TooltipLineData[];
  base?: string;
  hide?: string[];
  add?: LineAddition<TooltipLineData>[];
}

interface TooltipFile { texts: Record<string, LocalizedText>; layouts: LayoutData[] }
interface OptionTextsFile { texts: Record<string, LocalizedText> }

interface OptionValueData {
  values?: number | number[];
  per?: { attribute: string; divisor?: number; times?: number }[];
}

interface OptionData {
  number?: number;
  attribute: string;
  add?: OptionValueData;
  percent?: OptionValueData;
  text?: string | string[];
}

interface OptionDefinitionData { name: string; type: string; options: OptionData[] }
interface OptionsFile { definitions: OptionDefinitionData[] }

type TermData = 'none' | { times?: number; add?: number; dropLevelDivisor?: number; afterLevelBonus?: boolean };
type FormulaData = 'base' | { add?: number; levelWeight?: number; percent?: number; basis?: string };

interface StatSectionData { table?: string; excellent?: TermData; ancient?: TermData; perLevelAfterHalving?: number }

type RequirementSectionData = Partial<Record<RequirementStat, FormulaData>> & {
  levelPerItemLevel?: number;
  excellentLevelAdd?: number;
  strengthPerOptionLevel?: number;
};

type ProfileData = Partial<Record<StatKey, StatSectionData>> & { name?: string; requirements?: RequirementSectionData };

interface ItemStatsFile {
  dropLevelBonus?: { excellent?: number; ancient?: number };
  tables: { name: string; values?: number | number[]; perLevel?: number }[];
  default: ProfileData;
  profiles: (ProfileData & { name: string })[];
}

/** The fields of an item definition the tooltip reads besides the typed ones (docs/item-data.md). */
interface TooltipDefinition extends ItemDefinition {
  magicDurability?: number;
  blockRate?: number;
  magicDefense?: number;
  walkSpeed?: number;
  magicPower?: number;
  attackType?: number;
  statProfile?: string;
  excellentBonus?: number;
  excellentRaisesLevel?: boolean;
  requirementsByLevel?: Partial<Record<string, number | number[]>>;
  resistances?: Partial<Record<string, number>>;
  description?: TooltipLineData[];
  nameLines?: TooltipLineData[];
  levelValues?: Record<string, Text[]>;
  tooltipLayout?: string;
  tooltipLayoutChanges?: { hide?: string[]; add?: LineAddition<TooltipLineData>[] };
}

// ---------- the data as read ----------

interface StatTerm { times: number; add: number; dropLevelDivisor: number; afterLevelBonus: boolean }
interface StatTerms { table: string | null; excellent: StatTerm | null; ancient: StatTerm | null; perLevelAfterHalving: number }
type Formula = { keepBase: true } | { keepBase: false; add: number; levelWeight: number; percent: number; levelRequirementBasis: boolean };

type StatProfile = Record<StatKey, StatTerms> & {
  requirements: Record<RequirementStat, Formula> & {
    levelPerItemLevel: number;
    excellentLevelAdd: number;
    strengthPerOptionLevel: number;
  };
};

interface ItemStats {
  tables: Map<string, { values: number[]; perLevel: number }>;
  profiles: Map<string, StatProfile>;
  defaultProfile: StatProfile;
  excellentDropLevelBonus: number;
  ancientDropLevelBonus: number;
}

interface AncientSet { name: string; optionCount: number }
interface HarmonyOption { name: string; levels: number[] }
interface SocketOption { id: number; category: number; name: string; type: number; values: number[] }
interface AddOption { option1: number; value1: number; option2: number; value2: number; time: number }

interface TooltipData {
  texts: Record<string, LocalizedText>;
  layouts: Map<string, LayoutData>;
  optionTexts: Record<string, LocalizedText>;
  stats: ItemStats;
  options: Map<string, OptionDefinitionData>;
  sets: { types: Uint8Array[]; sets: AncientSet[] } | null;
  harmony: HarmonyOption[] | null;
  /** The socket options, the mix set bonus options and the equipment set bonus options. */
  sockets: SocketOption[][] | null;
  addOptions: ((type: number) => AddOption) | null;
  monsters: MonsterCatalog | null;
}

let data: TooltipData | null = null;
let loading: Promise<TooltipData> | null = null;

function loaded(): TooltipData {
  if (!data) throw new Error('loadTooltipData() has not finished');
  return data;
}

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${url}`);
  return response.json() as Promise<T>;
}

// The .bmd files are optional: without one its lines are left out.
async function fetchBytes(url: string): Promise<Uint8Array | null> {
  try {
    const response = await fetch(url);
    if (!response.ok) return null;
    return new Uint8Array(await response.arrayBuffer());
  } catch {
    return null;
  }
}

/** The data the tooltips need, with the item catalog; loads it once. */
export function loadTooltipData(): Promise<TooltipData> {
  loading ??= load().catch((error: unknown) => { loading = null; throw error; });
  return loading;
}

async function load(): Promise<TooltipData> {
  const [, tooltip, optionTexts, stats, excellent, luck, option, setTypes, setOptions, harmony, sockets, addOptions,
    monsters] = await Promise.all([
    loadItemCatalog(),
    fetchJson<TooltipFile>('/data/Items/Tooltips/Tooltip.json'),
    fetchJson<OptionTextsFile>('/data/Items/Tooltips/OptionTexts.json'),
    fetchJson<ItemStatsFile>('/data/Items/Stats/ItemStats.json'),
    fetchJson<OptionsFile>('/data/Items/Options/Excellent.json'),
    fetchJson<OptionsFile>('/data/Items/Options/Luck.json'),
    fetchJson<OptionsFile>('/data/Items/Options/Option.json'),
    fetchBytes('/data/Local/ItemSetType.bmd'),
    fetchBytes('/data/Local/Eng/ItemSetOption_Eng.bmd'),
    fetchBytes('/data/Local/Eng/JewelOfHarmonyOption_Eng.bmd'),
    fetchBytes('/data/Local/Eng/SocketItem_Eng.bmd'),
    fetchBytes('/data/Local/ItemAddOption.bmd'),
    loadMonsterCatalog().catch(() => null),
  ]);
  data = {
    texts: tooltip.texts,
    layouts: new Map(tooltip.layouts.map((layout) => [layout.name, layout])),
    optionTexts: optionTexts.texts,
    stats: readItemStats(stats),
    options: new Map([...excellent.definitions, ...luck.definitions, ...option.definitions].map((d) => [d.name, d])),
    sets: readItemSets(setTypes, setOptions),
    harmony: readHarmonyOptions(harmony),
    sockets: readSocketOptions(sockets),
    addOptions: readAddOptions(addOptions),
    monsters,
  };
  return data;
}

// ---------- text helpers ----------

const english = (text: Text | undefined): string => (typeof text === 'string' ? text : text?.en ?? '');

// "%d" and "%%" of a desktop UI text.
function format(text: string, ...numbers: number[]): string {
  return text.replace(/%d|%%/g, (code) => (code === '%%' ? '%' : String(numbers.shift() ?? 0)));
}

// A line's text: a shared text of Tooltip.json ("@name"; null when there's none of that name) or its own.
function sharedOrOwnText(text: Text | undefined): string | null {
  if (typeof text === 'string' && text.startsWith('@')) {
    const shared = loaded().texts[text.slice(1)];
    return shared === undefined ? null : english(shared);
  }
  return english(text);
}

function line(text: string, color = 'white', bold = false): TooltipLine {
  const textColor = HIGHLIGHTS[color];
  return { text, color: textColor ?? color, bold, background: textColor ? color : null, spacer: false };
}

const spacer = (): TooltipLine => ({ text: '', color: 'white', bold: false, background: null, spacer: true });

// ---------- binary data (.bmd) ----------

// One record after another, each encrypted on its own (BuxConvert per record).
function records(bytes: Uint8Array, size: number, count: number): Uint8Array[] {
  const result: Uint8Array[] = [];
  for (let index = 0; index < count && (index + 1) * size <= bytes.length; index++) {
    result.push(buxConvert(bytes.slice(index * size, (index + 1) * size)));
  }
  return result;
}

function cString(bytes: Uint8Array, start: number, length: number): string {
  const end = bytes.subarray(start, start + length).indexOf(0);
  return new TextDecoder().decode(bytes.subarray(start, end < 0 ? start + length : start + end));
}

const view = (bytes: Uint8Array) => new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
const int32 = (bytes: Uint8Array, at: number) => view(bytes).getInt32(at, true);
const uint16 = (bytes: Uint8Array, at: number) => view(bytes).getUint16(at, true);
const uint32 = (bytes: Uint8Array, at: number) => view(bytes).getUint32(at, true);

// ItemSetType.bmd (4 bytes per item type: the set of each of its two ancient kinds) and ItemSetOption_Eng.bmd (the
// sets: name and option count), CSItemOption::OpenItemSetType/OpenItemSetOption.
function readItemSets(types: Uint8Array | null, options: Uint8Array | null): TooltipData['sets'] {
  if (!types || !options) return null;
  const sets = records(options, 110, 64).map((entry) => ({ name: cString(entry, 0, 64), optionCount: entry[92] ?? 0 }));
  return { types: records(types, 4, 16 * ITEMS_PER_GROUP), sets };
}

// JewelOfHarmonyOption_Eng.bmd: 3 item kinds of 10 options, each a name and its value at the 14 levels.
function readHarmonyOptions(bytes: Uint8Array | null): HarmonyOption[] | null {
  if (!bytes || bytes.length < 30 * 180) return null;
  const all = buxConvert(bytes.slice(0, 30 * 180));
  const options: HarmonyOption[] = [];
  for (let index = 0; index < 30; index++) {
    const at = index * 180;
    const levels = Array.from({ length: 14 }, (_, level) => int32(all, at + 68 + level * 4));
    options.push({ name: cString(all, at + 4, 60), levels });
  }
  return options;
}

// SocketItem_Eng.bmd: the socket options, the mix set bonus options and the equipment set bonus options, 50 each.
function readSocketOptions(bytes: Uint8Array | null): SocketOption[][] | null {
  if (!bytes || bytes.length < 3 * MAX_SOCKET_OPTION * 104) return null;
  const entries = records(bytes, 104, 3 * MAX_SOCKET_OPTION).map((entry) => ({
    id: int32(entry, 0),
    category: int32(entry, 4),
    name: cString(entry, 8, 64),
    type: entry[72] ?? 0,
    values: [0, 1, 2, 3, 4].map((level) => int32(entry, 76 + level * 4)),
  }));
  return [0, 1, 2].map((kind) => entries.slice(kind * MAX_SOCKET_OPTION, (kind + 1) * MAX_SOCKET_OPTION));
}

// ItemAddOption.bmd: per item type two option types and values, and a duration in seconds (16 bytes each).
function readAddOptions(bytes: Uint8Array | null): ((type: number) => AddOption) | null {
  if (!bytes || bytes.length < 16 * 16 * ITEMS_PER_GROUP) return null;
  const all = buxConvert(bytes.slice(0, 16 * 16 * ITEMS_PER_GROUP));
  return (type) => {
    const at = type * 16;
    return { option1: all[at] ?? 0, value1: uint16(all, at + 2), option2: all[at + 4] ?? 0, value2: uint16(all, at + 6),
      time: uint32(all, at + 12) };
  };
}

// ---------- item stats (ItemStats.json, GameLogic/Items/ItemStats.cpp) ----------

function perLevelValues(json: number | number[] | undefined): number[] {
  if (typeof json === 'number') return new Array<number>(LEVEL_COUNT).fill(json);
  return Array.isArray(json) ? json.slice(0, LEVEL_COUNT) : new Array<number>(LEVEL_COUNT).fill(0);
}

function readTerm(json: TermData | undefined, term: StatTerm | null): StatTerm | null {
  if (json === undefined) return term;
  if (json === 'none') return null;
  return { times: json.times ?? 0, add: json.add ?? 0, dropLevelDivisor: json.dropLevelDivisor ?? 0,
    afterLevelBonus: json.afterLevelBonus ?? false };
}

function readFormula(json: FormulaData): Formula {
  if (json === 'base') return { keepBase: true };
  return { keepBase: false, add: json.add ?? 0, levelWeight: json.levelWeight ?? 0, percent: json.percent ?? 0,
    levelRequirementBasis: json.basis === 'levelRequirement' };
}

// A profile laid over the one below it: what it writes replaces that.
function layProfile(below: StatProfile, json: ProfileData): StatProfile {
  const profile = structuredClone(below);
  for (const stat of STAT_KEYS) {
    const section = json[stat];
    if (!section) continue;
    const terms = profile[stat];
    if (section.table !== undefined) terms.table = section.table;
    terms.excellent = readTerm(section.excellent, terms.excellent);
    terms.ancient = readTerm(section.ancient, terms.ancient);
    if (section.perLevelAfterHalving !== undefined) terms.perLevelAfterHalving = section.perLevelAfterHalving;
  }
  const requirements = json.requirements ?? {};
  if (requirements.levelPerItemLevel !== undefined) profile.requirements.levelPerItemLevel = requirements.levelPerItemLevel;
  if (requirements.excellentLevelAdd !== undefined) profile.requirements.excellentLevelAdd = requirements.excellentLevelAdd;
  if (requirements.strengthPerOptionLevel !== undefined) {
    profile.requirements.strengthPerOptionLevel = requirements.strengthPerOptionLevel;
  }
  for (const stat of REQUIREMENT_STATS) {
    const formula = requirements[stat];
    if (formula !== undefined) profile.requirements[stat] = readFormula(formula);
  }
  return profile;
}

function readItemStats(json: ItemStatsFile): ItemStats {
  const tables = new Map(json.tables.map((table) => [table.name,
    { values: perLevelValues(table.values), perLevel: table.perLevel ?? 0 }]));
  const terms = (): StatTerms => ({ table: null, excellent: null, ancient: null, perLevelAfterHalving: 0 });
  const empty: StatProfile = {
    damage: terms(), magicPower: terms(), blockRate: terms(), defense: terms(), magicDefense: terms(),
    requirements: {
      levelPerItemLevel: 0, excellentLevelAdd: 0, strengthPerOptionLevel: 0,
      strength: { keepBase: true }, dexterity: { keepBase: true }, energy: { keepBase: true },
      vitality: { keepBase: true }, leadership: { keepBase: true },
    },
  };
  const defaultProfile = layProfile(empty, json.default);
  const profiles = new Map([['default', defaultProfile]]);
  for (const profile of json.profiles) profiles.set(profile.name, layProfile(defaultProfile, profile));
  return { tables, profiles, defaultProfile, excellentDropLevelBonus: json.dropLevelBonus?.excellent ?? 0,
    ancientDropLevelBonus: json.dropLevelBonus?.ancient ?? 0 };
}

function tableBonus(name: string, level: number): number {
  const table = loaded().stats.tables.get(name);
  if (!table) return 0;
  return (table.values[Math.min(Math.max(level, 0), LEVEL_COUNT - 1)] ?? 0) + table.perLevel * Math.max(level, 0);
}

const limit = (value: number) => Math.min(Math.max(value, 0), MAX_VALUE);
const dropLevelPart = (term: StatTerm, dropLevel: number) =>
  (term.dropLevelDivisor > 0 ? Math.floor(dropLevel / term.dropLevelDivisor) : 0);

interface StatContext { level: number; dropLevel: number; excellent: boolean; ancient: boolean; fixedExcellentBonus: number }

// base + excellent part + ancient part + level bonus; every / divides whole numbers from left to right.
function addUpStat(terms: StatTerms, base: number, excellentBase: number, fixedBonusCounts: boolean, context: StatContext): number {
  if (base === 0) return 0;
  let excellent = 0;
  if (context.excellent && context.dropLevel > 0 && terms.excellent) {
    excellent = fixedBonusCounts && context.fixedExcellentBonus !== 0
      ? context.fixedExcellentBonus
      : Math.floor(excellentBase * terms.excellent.times / context.dropLevel) + terms.excellent.add
        + dropLevelPart(terms.excellent, context.dropLevel);
  }
  const levelBonus = terms.table ? tableBonus(terms.table, context.level) : 0;
  let ancient = 0;
  if (context.ancient && terms.ancient) {
    const dropLevel = context.dropLevel + loaded().stats.ancientDropLevelBonus;
    const value = base + excellent + (terms.ancient.afterLevelBonus ? levelBonus : 0);
    ancient = (dropLevel > 0 ? Math.floor(value * terms.ancient.times / dropLevel) : 0) + terms.ancient.add
      + dropLevelPart(terms.ancient, dropLevel);
  }
  return limit(base + excellent + ancient + levelBonus);
}

function statRequirement(formula: Formula, base: number, dropLevel: number, levelRequirement: number, level: number): number {
  if (base === 0) return 0;
  if (formula.keepBase) return base;
  if (formula.levelRequirementBasis && levelRequirement > 0) {
    return limit(formula.add + Math.floor(base * levelRequirement * formula.percent / 100));
  }
  const weighted = Math.min(base * (dropLevel + level * formula.levelWeight), 100 * (MAX_VALUE + 1));
  return limit(formula.add + Math.floor(weighted * formula.percent / 100));
}

function requirementByLevel(definition: TooltipDefinition | null, key: string, level: number): number | undefined {
  const values = definition?.requirementsByLevel?.[key];
  return values === undefined ? undefined : perLevelValues(values)[level] ?? 0;
}

interface Attribute {
  name: string;
  level: number;
  durability: number;
  magicDurability: number;
  damageMin: number;
  damageMax: number;
  blockRate: number;
  defense: number;
  magicDefense: number;
  attackSpeed: number;
  walkSpeed: number;
  magicPower: number;
  attackType: number;
  twoHanded: boolean;
  requirements: Record<'level' | RequirementStat, number>;
  classes: number[];
  resistances: number[];
}

interface Stats {
  damageMin: number;
  damageMax: number;
  magicPower: number;
  blockRate: number;
  defense: number;
  magicDefense: number;
  require: Record<'level' | RequirementStat, number>;
}

// What the item has of the stats and requirements (ComputeItemStats).
function computeStats(item: Item, definition: TooltipDefinition | null, attribute: Attribute, excellent: boolean): Stats {
  const { stats } = loaded();
  const profile = stats.profiles.get(definition?.statProfile ?? '') ?? stats.defaultProfile;
  const ancient = item.ancientSet > 0;
  const context: StatContext = { level: item.level, dropLevel: attribute.level, excellent: excellent || ancient, ancient,
    fixedExcellentBonus: definition?.excellentBonus ?? 0 };
  let magicPower = 0;
  if (attribute.magicPower > 0) {
    const sum = addUpStat(profile.magicPower, attribute.magicPower, attribute.magicPower, true, context);
    magicPower = limit(Math.floor(sum / 2) + profile.magicPower.perLevelAfterHalving * item.level);
  }

  const terms = profile.requirements;
  const levelIndex = Math.min(Math.max(item.level, 0), LEVEL_COUNT - 1);
  const levelByLevel = requirementByLevel(definition, 'level', levelIndex);
  let requireLevel = 0;
  if (levelByLevel !== undefined) requireLevel = levelByLevel;
  else if (attribute.requirements.level > 0) requireLevel = limit(attribute.requirements.level + terms.levelPerItemLevel * item.level);
  const raisesLevel = definition?.excellentRaisesLevel ?? true;
  if (excellent && requireLevel > 0 && raisesLevel) requireLevel = limit(requireLevel + terms.excellentLevelAdd);
  const levelBasis = levelByLevel ?? attribute.requirements.level;
  // The drop level's excellent bonus reads the excellent byte alone.
  const dropLevel = attribute.level + (item.excellent > 0 ? stats.excellentDropLevelBonus
    : ancient ? stats.ancientDropLevelBonus : 0);
  const statOf = (stat: RequirementStat) => requirementByLevel(definition, stat, levelIndex)
    ?? statRequirement(terms[stat], attribute.requirements[stat], dropLevel, levelBasis, item.level);
  const require = {
    level: requireLevel,
    strength: limit(statOf('strength') + terms.strengthPerOptionLevel * additionalOptionLevel(item, definition)),
    dexterity: statOf('dexterity'),
    energy: statOf('energy'),
    vitality: statOf('vitality'),
    leadership: statOf('leadership'),
  };
  return {
    damageMin: addUpStat(profile.damage, attribute.damageMin, attribute.damageMin, true, context),
    damageMax: addUpStat(profile.damage, attribute.damageMax, attribute.damageMin, true, context),
    magicPower,
    blockRate: Math.min(addUpStat(profile.blockRate, attribute.blockRate, attribute.blockRate, false, context), 255),
    defense: addUpStat(profile.defense, attribute.defense, attribute.defense, false, context),
    magicDefense: addUpStat(profile.magicDefense, attribute.magicDefense, attribute.magicDefense, false, context),
    require,
  };
}

// ---------- options (Data/Items/Options, GameLogic/Items/ItemOptions.cpp) ----------

const TAKE_ORDER = ['luck', 'option', 'excellent'];

// The definitions the item links, one per type, in the order the game takes them (ItemOptionDatabase::ResolveLinks).
function optionLinks(definition: TooltipDefinition | null): OptionDefinitionData[] {
  const { options } = loaded();
  const order = (name: string) => {
    const found = options.get(name);
    return found ? TAKE_ORDER.indexOf(found.type) : TAKE_ORDER.length;
  };
  const names = [...(definition?.options ?? [])].sort((left, right) => order(left) - order(right));
  const links: OptionDefinitionData[] = [];
  for (const name of names) {
    const found = options.get(name);
    if (found && !links.some((link) => link.type === found.type)) links.push(found);
  }
  return links;
}

const optionNumber = (option: OptionData) => option.number ?? 0;
const isExcellentBitSet = (flags: number, option: OptionData) => optionNumber(option) >= 1 && optionNumber(option) <= 6
  && ((flags >> (optionNumber(option) - 1)) & 1) !== 0;

function hasExcellentOptions(links: OptionDefinitionData[], flags: number): boolean {
  if (!flags) return false;
  return links.some((link) => link.type === 'excellent' && link.options.some((option) => isExcellentBitSet(flags, option)));
}

// The level the additional option counts at (CountAdditionalOptionLevel).
function additionalOptionLevel(item: Item, definition: TooltipDefinition | null): number {
  if (item.optionLevel <= 0) return 0;
  let linksOption = false;
  for (const link of optionLinks(definition)) {
    if (link.type !== 'option') continue;
    linksOption = true;
    for (const option of link.options) {
      if (optionNumber(option) !== item.optionType) continue;
      const values = (option.add ?? option.percent)?.values;
      return Array.isArray(values) ? Math.min(item.optionLevel, values.length) : item.optionLevel;
    }
  }
  return !linksOption && definition?.slot === 'wings' ? item.optionLevel : 0;
}

// The value of an option at a level: the level's of a list (from 1), else the constant, and the hero terms.
function optionValue(option: OptionData, level: number, character: TooltipCharacter | null): number {
  const value = option.add ?? option.percent ?? {};
  const values = value.values;
  let result: number;
  if (Array.isArray(values)) result = values.length && level >= 1 ? values[Math.min(level - 1, values.length - 1)] ?? 0 : 0;
  else result = values ?? 0;
  if (!value.per?.length) return result;
  for (const term of value.per) {
    // The client reads the hero's level; the master level isn't known here and counts as 0.
    const attribute = term.attribute === 'level' || term.attribute === 'totalLevel' ? character?.level ?? 0 : 0;
    result += term.divisor ? Math.floor(attribute / term.divisor) : attribute * (term.times ?? 0);
  }
  return limit(result);
}

const optionTextNames = (option: OptionData) => (Array.isArray(option.text) ? option.text : option.text ? [option.text] : []);

/** An option slot: the texts of a data option, a code option, or the skill (whose line needs the skill list). */
type OptionSlot =
  | { kind: 'data'; texts: string[]; value: number; divisor: number }
  | { kind: 'code'; option: CodeOption; value: number }
  | { kind: 'skill'; skill: number };

// The option slots of the item in the order SetItemAttributes adds them.
function optionSlots(item: Item, definition: TooltipDefinition | null, character: TooltipCharacter | null): OptionSlot[] {
  const slots: OptionSlot[] = [];
  const type = item.group * ITEMS_PER_GROUP + item.number;
  const code = (option: CodeOption, value: number) => slots.push({ kind: 'code', option, value });
  const links = optionLinks(definition);
  const addLinked = (kind: string) => {
    const turnedOn = kind === 'luck' ? item.luck : kind === 'option' ? item.optionLevel > 0 : item.excellent !== 0;
    if (!turnedOn) return;
    for (const link of links) {
      if (link.type !== kind) continue;
      // Excellent options from the highest number down (ItemOptionDatabase's option order).
      const options = kind === 'excellent'
        ? [...link.options].sort((left, right) => optionNumber(right) - optionNumber(left)) : link.options;
      for (const option of options) {
        if (kind === 'option' && optionNumber(option) !== item.optionType) continue;
        if (kind === 'excellent' && !isExcellentBitSet(item.excellent, option)) continue;
        const level = kind === 'option' ? item.optionLevel : 1;
        const divisor = (option.add ?? option.percent)?.per?.find((term) => (term.divisor ?? 0) > 0)?.divisor ?? 0;
        slots.push({ kind: 'data', texts: optionTextNames(option), value: optionValue(option, level, character), divisor });
      }
    }
  };

  addWingOptions(item, definition, code);
  if (item.skill && definition?.skill) slots.push({ kind: 'skill', skill: definition.skill });
  addLinked('luck');
  addLinked('option');
  if (item.optionLevel && !links.some((link) => link.type === 'option')) addCodeAdditionalOption(item, type, code);
  addLinked('excellent');
  if (type === 13 * ITEMS_PER_GROUP + 20 && (item.level === 0 || item.level === 3)) {
    code('improveMagicPercent', 10); code('improveDamagePercent', 10); code('improveAttackSpeed', 10);
  } else if (type === 13 * ITEMS_PER_GROUP + 107) {
    code('improveMagicPercent', 15); code('improveDamagePercent', 15); code('improveAttackSpeed', 10);
  }
  return slots;
}

type AddCode = (option: CodeOption, value: number) => void;

// CalcWingOptions: the second and third tier wings' and the capes' options of the excellent byte.
function addWingOptions(item: Item, definition: TooltipDefinition | null, code: AddCode): void {
  const bit = (index: number) => ((item.excellent >> index) & 1) !== 0;
  const tier = definition?.wingTier;
  const cape = definition?.tags?.includes('cape') ?? false;
  const capeOfLord = item.group === 13 && item.number === 30;
  const capeOfFighter = item.group === 12 && item.number === 49;
  if (tier === 'second' && !cape) {
    if (bit(0)) code('improveHpMax', 50 + item.level * 5);
    if (bit(1)) code('improveMpMax', 50 + item.level * 5);
    if (bit(2)) code('onePercentDamage', 3);
    if (bit(3)) code('improveAgMax', 50);
    if (bit(4)) code('improveAttackSpeed', 5);
  } else if (capeOfLord || capeOfFighter) {
    if (bit(0)) code('improveHpMax', 50 + item.level * 5);
    if (bit(1)) code('improveMpMax', 50 + item.level * 5);
    if (bit(2)) code('onePercentDamage', 3);
    if (bit(3) && !capeOfFighter) code('improveCharisma', 10 + item.level * 5);
  } else if (tier === 'third') {
    if (bit(0)) code('onePercentDamage', 5);
    if (bit(1)) code('damageReflection', 5);
    if (bit(2)) code('recoverFullLife', 5);
    if (bit(3)) code('recoverFullMana', 5);
  }
}

// The Horn of Dinorant's and the wings' additional options, still code in the desktop client.
function addCodeAdditionalOption(item: Item, type: number, code: AddCode): void {
  const level = item.optionLevel;
  const wing = (number: number) => type === 12 * ITEMS_PER_GROUP + number;
  // The option by option type, for the wings that choose: 'life' regenerates level %, the others add level x 4.
  const byType = (choices: Partial<Record<number, CodeOption | 'life'>>) => {
    const choice = choices[item.optionType];
    if (choice === 'life') code('lifeRegeneration', level);
    else if (choice) code(choice, level * 4);
  };
  if (type === 13 * ITEMS_PER_GROUP + 3) {
    if (level & 1) code('damageAbsorb', 5);
    if (level & 2) code('improveAgMax', 50);
    if (level & 4) code('improveAttackSpeed', 5);
  } else if (wing(0)) code('lifeRegeneration', level);
  else if (wing(1) || wing(41)) code('improveMagic', level * 4);
  else if (wing(2)) code('improveDamage', level * 4);
  else if (wing(3)) byType({ 2: 'life', 0: 'improveDamage' });
  else if (wing(4)) byType({ 0: 'life', 2: 'improveMagic' });
  else if (wing(5) || wing(49)) byType({ 2: 'improveDamage', 0: 'life' });
  else if (wing(6)) byType({ 2: 'improveDamage', 0: 'improveMagic' });
  else if (type === 13 * ITEMS_PER_GROUP + 30) code('improveDamage', level * 4);
  else if (wing(42)) byType({ 2: 'improveMagic', 0: 'improveCurse' });
  else if (wing(36) || wing(38) || wing(40) || wing(50)) byType({ 0: 'life', 2: 'improveDefense', 3: 'improveDamage' });
  else if (wing(37)) byType({ 0: 'life', 2: 'improveDefense', 3: 'improveMagic' });
  else if (wing(39)) byType({ 0: 'life', 2: 'improveMagic', 3: 'improveDamage' });
  else if (wing(43)) byType({ 0: 'life', 2: 'improveCurse', 3: 'improveMagic' });
}

// An option text of OptionTexts.json with its {value} and {divisor}; other placeholders stay as written.
function optionText(name: string, value: number, divisor: number): string | null {
  const text = loaded().optionTexts[name];
  if (text === undefined) return null;
  return english(text).replace(/\{(value|divisor)\}/g, (_, which: string) => String(which === 'value' ? value : divisor));
}

// ---------- Jewel of Harmony and sockets ----------

// The harmony kind of an item: weapons (groups 0-4), staffs (5), armour and shields (6-11).
const harmonyKind = (group: number): number | null => (group <= 4 ? 0 : group === 5 ? 1 : group <= 11 ? 2 : null);
const isHarmonyOption = (kind: number, option: number) => option >= 0 && option < (kind === 0 ? 11 : 9);
const harmonyOption = (kind: number, option: number) => loaded().harmony?.[kind * 10 + option - 1] ?? null;

interface HarmonyBonus { strength?: number; dexterity?: number; minAttack?: number; maxAttack?: number; defense?: number }

// The harmony bonus of one kind (0 requirements, 1 attack, 2 defense), JewelHarmonyInfo::GetStrengthenCapability.
function harmonyBonus(item: Item, which: number): HarmonyBonus {
  const option = item.harmony >> 4;
  const level = item.harmony & 0x0f;
  const kind = harmonyKind(item.group);
  if (option === 0 || item.level < level || kind === null || !isHarmonyOption(kind, option)) return {};
  const value = harmonyOption(kind, option)?.levels[level] ?? 0;
  if (which === 0 && kind === 0) return option === 3 ? { strength: value } : option === 4 ? { dexterity: value } : {};
  if (which === 0 && kind === 1) return option === 2 ? { strength: value } : option === 3 ? { dexterity: value } : {};
  if (which === 1 && kind === 0) {
    return { minAttack: option === 1 || option === 5 ? value : 0, maxAttack: option === 2 || option === 5 ? value : 0 };
  }
  if (which === 2 && kind === 2) return { defense: option === 1 ? value : 0 };
  return {};
}

const isSocketItem = (item: Item) => (SOCKET_ITEMS[item.group]?.includes(item.number) ?? false)
  || (item.group >= 7 && item.group <= 11 && item.number >= 45 && item.number <= 53);

interface Socket { seed: number; sphereLevel: number }

function socketsOf(item: Item): (Socket | null)[] {
  return item.sockets.slice(0, MAX_SOCKETS).map((byte) => (byte === SOCKET_EMPTY ? null
    : { seed: byte % MAX_SOCKET_OPTION, sphereLevel: Math.floor(byte / MAX_SOCKET_OPTION) + 1 }));
}

// CSocketItemMgr::CalcSocketOptionValue: types 3 to 5 grow with the hero's level, maximum life or mana.
function socketOptionValue(type: number, value: number, character: TooltipCharacter | null): number {
  if (type === 1 || type === 2) return Math.trunc(value);
  const of = type === 3 ? character?.level : type === 4 ? character?.maxLife : type === 5 ? character?.maxMana : null;
  if (of === null || !value) return 0;
  return Math.trunc((of ?? 0) / value);
}

const socketValueText = (type: number, value: number, character: TooltipCharacter | null) =>
  `+${socketOptionValue(type, value, character)}${type === 2 ? '%' : ''}`;

function socketReduction(socket: Socket, character: TooltipCharacter | null): number {
  const info = loaded().sockets?.[0]?.[socket.seed];
  return info ? socketOptionValue(info.type, info.values[socket.sphereLevel - 1] ?? 0, character) : 0;
}

// ---------- ancient sets ----------

// The ancient set at `index` of the item's set record; reads past its two kinds into the record as the client does.
function setOf(item: Item, index: number): AncientSet | null {
  const sets = loaded().sets;
  const number = sets?.types[item.group * ITEMS_PER_GROUP + item.number]?.[index];
  if (!sets || number === undefined || number === 0 || number === 255) return null;
  return sets.sets[number] ?? null;
}

// CSItemOption::GetSetItemName: the set's name and a space, for the item's ancient kind.
function setItemName(item: Item): string | null {
  const kind = item.ancientSet % 4;
  const set = kind > 0 ? setOf(item, kind - 1) : null;
  return set ? `${set.name} ` : null;
}

// ---------- values (Engine/Object/ItemTooltipValues.cpp) ----------

function attributeOf(definition: TooltipDefinition | null): Attribute {
  const requirements = definition?.requirements ?? {};
  return {
    name: english(definition?.name),
    level: definition?.level ?? 0,
    durability: definition?.durability ?? 0,
    magicDurability: definition?.magicDurability ?? 0,
    damageMin: definition?.damageMin ?? 0,
    damageMax: definition?.damageMax ?? 0,
    blockRate: definition?.blockRate ?? 0,
    defense: definition?.defense ?? 0,
    magicDefense: definition?.magicDefense ?? 0,
    attackSpeed: definition?.attackSpeed ?? 0,
    walkSpeed: definition?.walkSpeed ?? 0,
    magicPower: definition?.magicPower ?? 0,
    attackType: definition?.attackType ?? 0,
    twoHanded: definition?.twoHanded ?? false,
    requirements: { level: requirements.level ?? 0, strength: requirements.strength ?? 0,
      dexterity: requirements.dexterity ?? 0, energy: requirements.energy ?? 0, vitality: requirements.vitality ?? 0,
      leadership: requirements.leadership ?? 0 },
    classes: CLASS_KEYS.map((key) => definition?.classRequirements?.[key] ?? 0),
    resistances: RESISTANCE_KEYS.map((key) => definition?.resistances?.[key] ?? 0),
  };
}

// CalcMaxDurability.
function maxDurability(item: Item, definition: TooltipDefinition | null, attribute: Attribute, excellent: boolean): number {
  let value = attribute.magicDurability > 0 ? attribute.magicDurability : attribute.durability;
  const scrollOfBloodOrLater = item.group * ITEMS_PER_GROUP + item.number >= 13 * ITEMS_PER_GROUP + 51;
  for (let i = 0; i < item.level && !scrollOfBloodOrLater; i++) {
    if (i >= 14) value = Math.min(value + 8, 255);
    else if (i >= 13) value = Math.min(value + 7, 255);
    else value += i >= 12 ? 6 : i >= 11 ? 5 : i >= 10 ? 4 : i >= 9 ? 3 : i >= 4 ? 2 : 1;
  }
  const tags = definition?.tags ?? [];
  if (tags.includes('darkLordPet')) value = 255;
  if (item.ancientSet > 0) value += 20;
  else if (excellent && !tags.includes('divineArchangelWeapon')) value += 15;
  if (tags.includes('luckyItem')) value = 255;
  return value & 0xffff;
}

// The duration text of g_StringTime, in seconds.
function durationText(seconds: number): string {
  const day = Math.floor(seconds / 86400);
  const hour = Math.floor((seconds - day * 86400) / 3600);
  const minute = Math.floor((seconds - hour * 3600 - day * 86400) / 60);
  const second = seconds % 60;
  if (day) return `${day} ${T.day} ${hour} ${T.hour} ${minute} ${T.minute} ${second} ${T.second}`;
  if (hour) return `${hour} ${T.hour} ${minute} ${T.minute} ${second} ${T.second}`;
  if (minute) return `${minute} ${T.minute} ${second} ${T.second}`;
  return T.lessThanAMinute;
}

interface Requirement { required: number; missing: number; reduced: boolean; asked: boolean }

// A requirement and what the character lacks of it; nothing is missing for a stat the character doesn't give.
function requirement(required: number, characterValue: number | undefined, reduction: number): Requirement {
  const needed = required - reduction;
  const missing = characterValue !== undefined && characterValue < needed ? needed - characterValue : 0;
  return { required: needed, missing, reduced: reduction !== 0, asked: required !== 0 };
}

/** What the tooltip's conditions and placeholders read. */
interface TooltipContext {
  level: number;
  character: TooltipCharacter | null;
  tags: string[];
  values: Record<string, number | string>;
  states: Record<string, boolean>;
  attribute: Attribute;
  excellent: boolean;
  type: number;
}

function computeContext(item: Item, definition: TooltipDefinition | null, character: TooltipCharacter | null,
  slots: OptionSlot[]): TooltipContext {
  const type = item.group * ITEMS_PER_GROUP + item.number;
  const attribute = attributeOf(definition);
  const excellent = hasExcellentOptions(optionLinks(definition), item.excellent);
  const stats = computeStats(item, definition, attribute, excellent);
  const tags = definition?.tags ?? [];
  const values: Record<string, number | string> = {};
  const states: Record<string, boolean> = {};

  states.skillBook = item.group === 15;
  states.twoHanded = attribute.twoHanded;
  states.armour = item.group >= 7 && item.group <= 11;
  states.wizardryWeapon = definition?.weaponKind === 'staff' || definition?.weaponKind === 'book' || tags.includes('magicSword');
  states.hasDurability = (Boolean(attribute.durability || attribute.magicDurability) && item.group !== 12 && item.group < 14)
    || (item.group === 12 && (item.number <= 6 || (item.number >= 36 && item.number <= 43) || item.number === 49 || item.number === 50));
  states.timed = false;
  states.expired = false;
  states.hasOptions = slots.length > 0;
  states.excellent = excellent;
  states.ancient = item.ancientSet > 0;
  states.socketItem = isSocketItem(item);
  states.upperTierWings = definition?.wingTier === 'second' || definition?.wingTier === 'third';
  states.skillLuckOrOption = item.skill || item.luck
    || additionalOptionLevel(item, definition) > 0 || (type === 13 * ITEMS_PER_GROUP + 3 && item.optionLevel > 0);
  states.setItem = setItemName(item) !== null;
  states.allClasses = attribute.classes.every((level) => level === 1);
  states.boots = item.group === 11;
  states.gloves = item.group === 10;
  states.classUsable = false;

  values.heroLevel = character?.level ?? 0;
  values.name = attribute.name;
  values.levelPlusOne = item.level + 1;
  values.excellentFlags = item.excellent & 63;
  values.magicDefense = stats.magicDefense;
  values.blockRate = stats.blockRate;
  values.attackSpeed = attribute.attackSpeed;
  values.walkSpeed = attribute.walkSpeed;
  values.magicPower = stats.magicPower;
  values.durability = item.durability;
  RESISTANCE_VALUES.forEach((name, index) => { values[name] = attribute.resistances[index] ? item.level + 1 : 0; });
  values.expireDate = '';
  values.className = '';
  values.bundleCount = item.group === 12 && JEWEL_BUNDLES.includes(item.number) ? (item.level + 1) * 10 : 0;
  const addOption = loaded().addOptions?.(type);
  values.addOptionValue1 = addOption?.value1 ?? 0;
  values.addOptionValue2 = addOption?.value2 ?? 0;
  values.addOptionDuration = addOption ? durationText(addOption.time) : '';

  // The damage, with a Jewel of Harmony's attack bonus; a bonus lifting the minimum shows the maximum twice.
  states.hasDamage = false;
  states.damageHarmonyBonus = false;
  values.damageMin = 0;
  values.damageMax = 0;
  if (stats.damageMin !== 0) {
    const harmony = harmonyBonus(item, 1);
    const minBonus = harmony.minAttack ?? 0;
    const maxBonus = harmony.maxAttack ?? 0;
    states.damageHarmonyBonus = minBonus !== 0 || maxBonus !== 0;
    values.damageMax = stats.damageMax + maxBonus;
    values.damageMin = Math.min(stats.damageMin + minBonus, stats.damageMax + maxBonus);
    states.hasDamage = stats.damageMin > 0;
  }
  const defenseBonus = harmonyBonus(item, 2).defense ?? 0;
  values.defense = stats.defense + defenseBonus;
  states.defenseHarmonyBonus = defenseBonus !== 0;
  values.maxDurability = maxDurability(item, definition, attribute, excellent);

  // The Jewel of Harmony's and the sockets' strength and agility reductions.
  const harmony = harmonyBonus(item, 0);
  let strengthReduction = harmony.strength ?? 0;
  let dexterityReduction = harmony.dexterity ?? 0;
  for (const socket of socketsOf(item)) {
    if (socket?.seed === 38) strengthReduction += socketReduction(socket, character);
    else if (socket?.seed === 39) dexterityReduction += socketReduction(socket, character);
  }
  const requirements: Record<string, Requirement> = {
    Level: requirement(stats.require.level, character?.level, 0),
    Strength: requirement(stats.require.strength, character?.strength, strengthReduction),
    Dexterity: requirement(stats.require.dexterity, character?.agility, dexterityReduction),
    Vitality: requirement(stats.require.vitality, character?.vitality, 0),
    Energy: requirement(stats.require.energy, character?.energy, 0),
    Leadership: requirement(stats.require.leadership, character?.leadership, 0),
  };
  for (const [name, value] of Object.entries(requirements)) {
    values[`required${name}`] = value.required;
    values[`missing${name}`] = value.missing;
  }
  states.strengthReduced = requirements.Strength?.reduced ?? false;
  states.dexterityReduced = requirements.Dexterity?.reduced ?? false;
  states.strengthRequired = requirements.Strength?.asked ?? false;
  states.dexterityRequired = requirements.Dexterity?.asked ?? false;

  return { level: item.level, character, tags, values, states, attribute, excellent, type };
}

// ---------- conditions and texts (ItemTooltipConditions.cpp, ItemTooltipText.cpp) ----------

const COMPARISONS: [string, (a: number, b: number) => boolean][] = [['!=', (a, b) => a !== b],
  ['<=', (a, b) => a <= b], ['>=', (a, b) => a >= b], ['=', (a, b) => a === b], ['<', (a, b) => a < b],
  ['>', (a, b) => a > b]];

// The number a condition compares: {level} or a built-in number value; nothing for a text value or another name.
function conditionValue(name: string, context: TooltipContext): number | undefined {
  if (name === 'level') return context.level;
  if (TEXT_VALUES.has(name) || !BUILT_IN_VALUES.has(name)) return undefined;
  const value = context.values[name];
  return typeof value === 'number' ? value : undefined;
}

function conditionHolds(condition: string, context: TooltipContext): boolean {
  const negated = condition.startsWith('!');
  const name = negated ? condition.slice(1) : condition;
  let holds: boolean;
  if (name.startsWith('tag:')) {
    holds = context.tags.includes(name.slice(4));
  } else if (name.startsWith('class:')) {
    const classIndex = context.character?.classIndex;
    holds = classIndex !== undefined && CLASS_KEYS[classIndex] === name.slice(6);
  } else {
    const nameEnd = name.search(/[^A-Za-z0-9]/);
    const valueName = nameEnd < 0 ? name : name.slice(0, nameEnd);
    const comparison = nameEnd < 0 ? '' : name.slice(nameEnd);
    if (comparison) {
      const found = COMPARISONS.find(([symbol]) => comparison.startsWith(symbol));
      const value = conditionValue(valueName, context);
      holds = found !== undefined && value !== undefined && found[1](value, Number(comparison.slice(found[0].length)));
    } else if (STATES.has(valueName)) {
      holds = context.states[valueName] ?? false;
    } else {
      const value = conditionValue(valueName, context);
      holds = value !== undefined && value !== 0;
    }
  }
  return holds !== negated;
}

const conditionsHold = (when: string[] | undefined, context: TooltipContext) =>
  (when ?? []).every((condition) => conditionHolds(condition, context));

function lineColor(rules: TooltipLineData['colors'], color: string | undefined, context: TooltipContext): string {
  for (const rule of rules ?? []) {
    if (conditionsHold(rule.when, context)) return rule.color;
  }
  return color ?? 'white';
}

interface PlaceholderSpan { begin: number; end: number; name: string }

// The placeholders of a text, up to the first problem of its braces (FindPlaceholders); `stop` is where that is.
function placeholders(text: string): { spans: PlaceholderSpan[]; stop: number | null } {
  const spans: PlaceholderSpan[] = [];
  let position = 0;
  while (position < text.length) {
    const brace = text.slice(position).search(/[{}]/);
    if (brace < 0) break;
    const begin = position + brace;
    if (text[begin] === '}') return { spans, stop: begin };
    const close = text.slice(begin + 1).search(/[{}]/);
    if (close < 0 || text[begin + 1 + close] === '{') return { spans, stop: begin };
    const name = text.slice(begin + 1, begin + 1 + close);
    const checked = name.startsWith('table:') ? name.slice(6) : name;
    const valid = name.startsWith('item:') ? itemReference(name) !== null : /^[A-Za-z0-9]+$/.test(checked);
    if (!valid) return { spans, stop: begin };
    spans.push({ begin, end: begin + 2 + close, name });
    position = begin + 2 + close;
  }
  return { spans, stop: null };
}

const levelsHold = (levels: [number, number] | undefined, level: number) => !levels || (level >= levels[0] && level <= levels[1]);

// {item:group,number[+level][:case]}: the item's English name, at a level as its name line or its standard name.
function itemReference(name: string): string | null {
  const match = /^item:(\d+),(\d+)(?:\+(\d+))?(?::[a-z]+)?$/.exec(name);
  if (!match) return null;
  const definition = itemDefinition(Number(match[1]), Number(match[2])) as TooltipDefinition | null;
  if (!definition) return null;
  if (match[3] === undefined) return english(definition.name);
  const level = Number(match[3]);
  if (level >= LEVEL_COUNT) return null;
  const nameLine = (definition.nameLines ?? []).find((entry) => levelsHold(entry.levels, level));
  const text = nameLine?.text !== undefined ? sharedOrOwnText(nameLine.text) : null;
  if (text) return text.replace(/\{name\}/g, english(definition.name)).replace(/\{level\}/g, String(level));
  return english(definition.name) + (level > 0 ? ` +${level}` : '');
}

// The text with its placeholders filled in (TooltipText::Compile and Write): a line's "values" first, then a game
// data value, the item's level value, a built-in value and a table. A placeholder without a value, and the rest of
// the text after it, stay as written.
function fillText(text: string, bound: Record<string, string> | undefined, definition: TooltipDefinition | null,
  context: TooltipContext): string {
  const { spans, stop } = placeholders(text);
  const names = spans.map((span) => (span.name.startsWith('item:') ? itemReference(span.name) : null));
  const restFrom = (index: number) => {
    let rest = '';
    let position = spans[index]?.begin ?? text.length;
    spans.forEach((span, i) => {
      const name = names[i];
      if (i < index || name === null || name === undefined) return;
      rest += text.slice(position, span.begin) + name;
      position = span.end;
    });
    return rest + text.slice(position);
  };
  let out = '';
  let position = 0;
  for (const [index, span] of spans.entries()) {
    out += text.slice(position, span.begin);
    position = span.end;
    const name = names[index];
    if (name !== null && name !== undefined) {
      out += name;
      continue;
    }
    const value = placeholderValue(bound?.[span.name] ?? span.name, definition, context);
    if (value === undefined) return out + restFrom(index);
    out += value;
  }
  return out + text.slice(position);
}

function placeholderValue(name: string, definition: TooltipDefinition | null, context: TooltipContext): string | undefined {
  const gameData = /^(skillMana|skillName|monsterName):(\d{1,4})$/.exec(name);
  if (gameData) {
    // The skill list isn't loaded in the web client: such a line shows as written from there on.
    if (gameData[1] !== 'monsterName') return undefined;
    return loaded().monsters?.byNumber.get(Number(gameData[2]))?.name?.en;
  }
  const levelValues = definition?.levelValues?.[name];
  if (levelValues) {
    const value = levelValues[context.level];
    return value === undefined ? undefined : sharedOrOwnText(value) ?? '';
  }
  if (name === 'level') return String(context.level);
  if (BUILT_IN_VALUES.has(name)) return String(context.values[name] ?? '');
  const table = name.startsWith('table:') ? name.slice(6) : null;
  if (table !== null && loaded().stats.tables.has(table)) return String(tableBonus(table, context.level));
  return undefined;
}

// ---------- the tooltip ----------

/** A line of an item's layout as built: a line of the tooltip file, or one the item adds itself. */
type BuiltLine = { layout: TooltipLineData } | { item: TooltipLineData };

function applyChanges(lines: BuiltLine[], hide: string[] = [], additions: LineAddition<TooltipLineData>[] = [],
  makeLine: (entry: TooltipLineData) => BuiltLine): BuiltLine[] {
  const idOf = (entry: BuiltLine) => ('layout' in entry ? entry.layout.id : undefined);
  const changed: BuiltLine[] = [];
  for (const entry of lines) {
    const id = idOf(entry);
    if (id && hide.includes(id)) continue;
    changed.push(entry);
    if (!id) continue;
    for (const addition of additions) {
      if (addition.after === id) changed.push(...addition.lines.map(makeLine));
    }
  }
  return changed;
}

function buildLayout(name: string, depth = 0): BuiltLine[] | null {
  const { layouts } = loaded();
  const layout = layouts.get(name);
  if (!layout || depth > layouts.size) return null;
  if (!layout.base) return (layout.lines ?? []).map((entry) => ({ layout: entry }));
  const lines = buildLayout(layout.base, depth + 1);
  return lines && applyChanges(lines, layout.hide, layout.add, (entry) => ({ layout: entry }));
}

// The item's layout as built: the one it names (the default one without) and its own changes.
function itemLayout(definition: TooltipDefinition | null): BuiltLine[] {
  const lines = buildLayout(definition?.tooltipLayout ?? 'default') ?? [];
  const changes = definition?.tooltipLayoutChanges;
  return changes ? applyChanges(lines, changes.hide, changes.add, (entry) => ({ item: entry })) : lines;
}

class TooltipWriter {
  readonly lines: TooltipLine[] = [];
  private readonly layout: BuiltLine[];
  private descriptionRows = MAX_DESCRIPTION_ROWS;
  private addedRows = MAX_ADDED_ROWS;

  constructor(private readonly definition: TooltipDefinition | null, private readonly context: TooltipContext) {
    this.layout = itemLayout(definition);
  }

  // The layout's lines after the section `after` (before the first section for null), up to the next section.
  writeLayoutLines(after: string | null): void {
    const sectionOf = (entry: BuiltLine | undefined) => (entry && 'layout' in entry ? entry.layout.section : undefined);
    let index = 0;
    if (after !== null) {
      while (index < this.layout.length && sectionOf(this.layout[index]) !== after) index++;
      index++;
    }
    for (; index < this.layout.length && !sectionOf(this.layout[index]); index++) {
      const entry = this.layout[index];
      if (!entry) break;
      if ('item' in entry) this.addedRows = this.writeOwnLine(entry.item, this.addedRows);
      else if (entry.layout.forEachClass) this.writeClassRows(entry.layout);
      else this.writeLayoutRow(entry.layout, this.context);
    }
  }

  private writeLayoutRow(layoutLine: TooltipLineData, context: TooltipContext): void {
    if (!conditionsHold(layoutLine.when, context)) return;
    const text = sharedOrOwnText(layoutLine.text);
    const filled = text ? fillText(text, layoutLine.values, null, context) : '';
    if (!filled) {
      this.lines.push(spacer());
      return;
    }
    this.lines.push(line(filled, lineColor(layoutLine.colors, layoutLine.color, context), layoutLine.bold ?? false));
  }

  // A line once for each class the item names, white for the character's class, dark red for the others.
  private writeClassRows(layoutLine: TooltipLineData): void {
    const character = this.context.character;
    const heroClass = character?.classIndex ?? -1;
    const heroClassLevel = character?.classLevel ?? 1;
    this.context.attribute.classes.forEach((needed, characterClass) => {
      if (!needed) return;
      const className = (CLASS_NAMES[characterClass] ?? []).slice(Math.max(needed, 1) - 1).find((name) => name !== null);
      if (!className) return;
      const states = { ...this.context.states, classUsable: characterClass === heroClass && needed <= heroClassLevel };
      this.writeLayoutRow(layoutLine, { ...this.context, states, values: { ...this.context.values, className } });
    });
  }

  // A line of the item's own (its description, or one it adds to its layout); returns the rows left.
  private writeOwnLine(ownLine: TooltipLineData, rowsLeft: number): number {
    const context = this.context;
    if (!levelsHold(ownLine.levels, context.level) || !conditionsHold(ownLine.when, context)) return rowsLeft;
    const text = sharedOrOwnText(ownLine.text) ?? '';
    const filled = text ? fillText(text, ownLine.values, this.definition, context) : '';
    // The desktop also wraps a long line to its font's width; here a text breaks only at its own line breaks.
    const pieces = filled ? filled.split('\n') : [''];
    const color = lineColor(ownLine.colors, ownLine.color, context);
    for (const piece of pieces) {
      if (rowsLeft <= 0) break;
      this.lines.push(piece ? line(piece, color, ownLine.bold ?? false) : spacer());
      rowsLeft--;
    }
    return rowsLeft;
  }

  writeDescription(afterOptions: boolean): void {
    for (const ownLine of this.definition?.description ?? []) {
      if ((ownLine.after === 'options') === afterOptions) this.descriptionRows = this.writeOwnLine(ownLine, this.descriptionRows);
    }
  }
}

// The item's name and its color (GetItemTooltipName): the first of its name lines whose levels and conditions hold,
// else its standard name.
function itemName(item: Item, definition: TooltipDefinition | null, context: TooltipContext): { name: string; color: string } {
  let name = String(context.values.name ?? '');
  const setName = setItemName(item);
  if (setName) name = setName + name;
  if (context.excellent) name = `${T.excellent} ${name}`;
  if (item.level > 0) name += ` +${item.level}`;
  let color = 'white';
  if (item.ancientSet > 0) color = 'greenBlue';
  else if (context.states.socketItem) color = 'violet';
  else if (context.excellent) color = 'green';
  else if (item.level >= 7) color = 'yellow';
  else if (context.states.hasOptions) color = 'blue';

  const nameLine = (definition?.nameLines ?? []).find((entry) => levelsHold(entry.levels, item.level)
    && conditionsHold(entry.when, context));
  if (!nameLine) return { name, color };
  if (nameLine.text !== undefined) {
    const text = sharedOrOwnText(nameLine.text);
    const filled = text ? fillText(text, nameLine.values, definition, context) : '';
    if (filled) name = filled;
  }
  return { name, color: lineColor(nameLine.colors, nameLine.color ?? color, context) };
}

// The Jewel of Harmony's line, or the desktop's error lines for an option it doesn't know.
function harmonyLines(item: Item, lines: TooltipLine[]): void {
  const option = item.harmony >> 4;
  const level = item.harmony & 0x0f;
  const kind = harmonyKind(item.group);
  if (isSocketItem(item) || option === 0 || kind === null) return;
  lines.push(spacer());
  const info = isHarmonyOption(kind, option) ? harmonyOption(kind, option) : null;
  if (info) {
    const percent = kind === 2 && option === 7 ? '%' : '';
    lines.push(line(`${info.name} +${info.levels[level] ?? 0}${percent}`, item.level >= level ? 'yellow' : 'gray', true));
  } else {
    lines.push(line(`${T.reinforcementOptionError} : ${kind} ${option} ${level}`, 'darkRed', true));
    lines.push(line(T.sendScreenshots, 'darkRed', true));
  }
  lines.push(spacer());
}

function optionLines(item: Item, context: TooltipContext, slots: OptionSlot[], lines: TooltipLine[]): void {
  if (item.guardian) {
    const addOption = loaded().addOptions?.(context.type);
    lines.push(spacer());
    if (addOption) {
      for (const [option, value] of [[addOption.option1, addOption.value1], [addOption.option2, addOption.value2]] as const) {
        const text = T.guardianOptions[option];
        if (text) lines.push(line(format(text, value), 'redPurple', true));
      }
    }
    lines.push(spacer());
  }
  harmonyLines(item, lines);

  // The ancient bonus option, by the item's attack type (CSItemOption::RenderDefaultOptionText).
  const bonus = T.ancientBonus[context.attribute.attackType];
  if (item.ancientBonus > 0 && bonus) {
    lines.push(line(`${bonus}${item.ancientBonus * 5}`, 'blue'));
    const n = item.number;
    if (item.group === 13 && ((n >= 8 && n <= 9) || (n >= 12 && n <= 13) || (n >= 21 && n <= 27))) {
      lines.push(line(T.increaseAttributeDamage, 'blue'));
    }
  }

  for (const slot of slots) {
    if (slot.kind === 'data') {
      for (const name of slot.texts) {
        const text = optionText(name, slot.value, slot.divisor);
        if (text !== null) lines.push(line(text, 'blue'));
      }
    } else if (slot.kind === 'code') {
      lines.push(line(format(CODE_OPTION_TEXTS[slot.option], slot.value), 'blue'));
    }
  }
}

// The ancient set's header (CSItemOption::RenderSetOptionListInItem). Its options are listed only for a set the
// character wears, which the tooltip isn't told.
function setLines(item: Item, lines: TooltipLine[]): void {
  if (item.ancientSet === 0 || item.ancientSet > 4) return;
  const set = setOf(item, item.ancientSet - 1);
  if (!set || set.optionCount >= 255) return;
  lines.push(spacer(), line(T.setItemOptionInfo, 'yellow'), spacer(), spacer(), spacer(), spacer());
}

function socketLines(item: Item, character: TooltipCharacter | null, lines: TooltipLine[]): void {
  const options = loaded().sockets;
  const list = socketsOf(item);
  if (list.length === 0 || !options) return;
  lines.push(spacer(), line(T.socketItemOptionInfo, 'purple'), spacer());
  list.forEach((socket, index) => {
    const info = socket ? options[0]?.[socket.seed] : undefined;
    if (!socket || !info) {
      lines.push(line(T.socket(index + 1, T.noItemApplication), 'gray'));
      return;
    }
    const value = socketValueText(info.type, info.values[socket.sphereLevel - 1] ?? 0, character);
    lines.push(line(T.socket(index + 1, `${T.socketCategories[info.category] ?? ''}(${info.name} ${value})`), 'blue'));
  });
  const bonus = item.socketBonus < MAX_SOCKET_OPTION ? options[1]?.[item.socketBonus] : undefined;
  if (bonus && (bonus.values[0] ?? 0) > 0) {
    lines.push(spacer(), line(T.bonusSocketOption, 'purple'), spacer());
    lines.push(line(`${bonus.name} ${socketValueText(bonus.type, bonus.values[0] ?? 0, character)}`, 'blue'));
  }
}

/** The lines of the item's inventory tooltip for the character (after loadTooltipData has finished). */
export function tooltipLines(item: Item, character: TooltipCharacter | null = null): TooltipLine[] {
  loaded();
  const definition = itemDefinition(item.group, item.number) as TooltipDefinition | null;
  const slots = optionSlots(item, definition, character);
  const context = computeContext(item, definition, character, slots);
  const writer = new TooltipWriter(definition, context);

  writer.writeLayoutLines(null);
  const { name, color } = itemName(item, definition, context);
  writer.lines.push(line(name, color, true));
  writer.writeLayoutLines('name');
  writer.writeLayoutLines('itemCases');
  writer.writeDescription(false);
  writer.writeLayoutLines('description');
  writer.writeLayoutLines('classes');
  optionLines(item, context, slots, writer.lines);
  writer.writeLayoutLines('options');
  writer.writeDescription(true);
  writer.writeLayoutLines('afterOptions');
  setLines(item, writer.lines);
  socketLines(item, character, writer.lines);
  return writer.lines;
}

// ---------------------------------------------------------------------------
// For other item texts (the ground labels): the item's name line and its conditions.
// ---------------------------------------------------------------------------

function contextOf(item: Item): { definition: TooltipDefinition | null; context: TooltipContext } {
  loaded();
  const definition = itemDefinition(item.group, item.number) as TooltipDefinition | null;
  return { definition, context: computeContext(item, definition, null, optionSlots(item, definition, null)) };
}

/** The item's name as windows and messages write it (GetItemNameText): with "Excellent", the set and the level. */
export function itemNameText(item: Item): string {
  const { definition, context } = contextOf(item);
  return itemName(item, definition, context).name;
}

/** Whether all of a rule's conditions ("excellent", "level>=7", "tag:…", …) hold for the item. */
export function itemConditionsHold(item: Item, when: readonly string[] | undefined): boolean {
  const { context } = contextOf(item);
  return (when ?? []).every((condition) => conditionHolds(condition, context));
}

/** The name of the item's ancient set, to put in front of its name (GetSetItemName), or null. */
export function itemSetName(item: Item): string | null {
  loaded();
  return setItemName(item);
}

// ---------------------------------------------------------------------------
// For the character window's sums (CHARACTER_MACHINE::CalculateAll): what a worn item gives.
// ---------------------------------------------------------------------------

/**
 * An option slot's effect as the stat code reads it (the AT_ numbers of ItemOptionEffects.cpp and of the code
 * options): `improveDamage` AT_IMPROVE_DAMAGE, `improveDamageLevel` AT_IMPROVE_DAMAGE_LEVEL, `improveDamagePercent`
 * AT_IMPROVE_DAMAGE_PERCENT, the same for `improveMagic…`, `improveCurse`, `improveBlocking`, `improveBlockingPercent`,
 * `improveDefense`, `luck` (AT_LUCK, the critical damage rate), `improveCharisma` AT_SET_OPTION_IMPROVE_CHARISMA.
 */
export interface ItemOptionEffect { effect: string; value: number }

/** The Jewel of Harmony's part (JewelHarmonyInfo::GetStrengthenCapability), zero where it gives nothing. */
export interface ItemHarmonyValues {
  /** Index 0: the requirements it lowers. */
  strength: number; dexterity: number;
  /** Index 1 (weapons and staffs). */
  minAttack: number; maxAttack: number; magicalPower: number; attackRate: number; criticalDamage: number;
  /** Index 2 (shields and armour). */
  defense: number; defenseRate: number;
}

/** What an item gives the character's stats (SetItemAttributes' values, its option slots and its harmony option). */
export interface ItemCombatValues {
  /** group * 512 + number (ITEM::Type). */
  type: number;
  weaponKind: string | null;
  tags: string[];
  wingTier: string | null;
  /** ITEM_ATTRIBUTE's Durability and MagicDur, for CalcDurabilityPercent. */
  baseDurability: number;
  baseMagicDurability: number;
  damageMin: number;
  damageMax: number;
  magicPower: number;
  blockRate: number;
  defense: number;
  require: Record<'level' | RequirementStat, number>;
  /** RequireClass by base class: the class step it needs, 0 when the class can't wear it. */
  classes: number[];
  options: ItemOptionEffect[];
  /** Its option is a magic sword's (baseDamageBonus): the damage option counts for wizardry too. */
  raisesWizardry: boolean;
  harmony: ItemHarmonyValues;
  /** The guardian (380) option's attack and defense rates (ItemAddOption.bmd options 1 and 3), when it has one. */
  guardianAttackRate: number;
  guardianDefenseRate: number;
}

// The data options the stat code reads, by type and attribute (ItemOptionEffects.cpp, the ones that write a value).
const DATA_OPTION_EFFECTS: Record<string, string> = {
  'luck:criticalDamageChance': 'luck',
  'option:physicalBaseDmg': 'improveDamage', 'option:baseDamageBonus': 'improveDamage',
  'option:wizardryBaseDmg': 'improveMagic', 'option:curseBaseDmg': 'improveCurse',
  'option:defenseRatePvm': 'improveBlocking', 'option:defenseBase': 'improveDefense',
  'excellent:defenseRatePvm': 'improveBlockingPercent',
  'excellent:physicalBaseDmg': 'improveDamageLevel', 'excellent:wizardryBaseDmg': 'improveMagicLevel',
  'excellent:physicalBaseDmgIncrease': 'improveDamagePercent', 'excellent:wizardryBaseDmgIncrease': 'improveMagicPercent',
  'excellent:attackSpeedAny': 'improveAttackSpeed',
};

function harmonyValues(item: Item): ItemHarmonyValues {
  const values: ItemHarmonyValues = { strength: 0, dexterity: 0, minAttack: 0, maxAttack: 0, magicalPower: 0,
    attackRate: 0, criticalDamage: 0, defense: 0, defenseRate: 0 };
  const option = item.harmony >> 4;
  const level = item.harmony & 0x0f;
  const kind = harmonyKind(item.group);
  if (option === 0 || item.level < level || kind === null || !isHarmonyOption(kind, option)) return values;
  const value = harmonyOption(kind, option)?.levels[level] ?? 0;
  if (kind === 0) {
    if (option === 3) values.strength = value;
    if (option === 4) values.dexterity = value;
    if (option === 1 || option === 5) values.minAttack = value;
    if (option === 2 || option === 5) values.maxAttack = value;
    if (option === 8) values.attackRate = value;
    if (option === 6) values.criticalDamage = value;
  } else if (kind === 1) {
    if (option === 2) values.strength = value;
    if (option === 3) values.dexterity = value;
    if (option === 1) values.magicalPower = value;
    if (option === 7) values.attackRate = value;
    if (option === 5) values.criticalDamage = value;
  } else {
    if (option === 1) values.defense = value;
    if (option === 6) values.defenseRate = value;
  }
  return values;
}

function guardianRates(item: Item, type: number): { guardianAttackRate: number; guardianDefenseRate: number } {
  const add = item.guardian ? loaded().addOptions?.(type) : undefined;
  const rate = (option: number) => (add ? (add.option1 === option ? add.value1 : 0) + (add.option2 === option ? add.value2 : 0) : 0);
  return { guardianAttackRate: rate(1), guardianDefenseRate: rate(3) };
}

/** What the item gives the character's stats (after loadTooltipData has finished); `level` is the hero's level. */
export function itemCombatValues(item: Item, level: number): ItemCombatValues {
  loaded();
  const definition = itemDefinition(item.group, item.number) as TooltipDefinition | null;
  const attribute = attributeOf(definition);
  const links = optionLinks(definition);
  const excellent = hasExcellentOptions(links, item.excellent);
  const stats = computeStats(item, definition, attribute, excellent);
  const character: TooltipCharacter = { level };
  const options: ItemOptionEffect[] = [];
  let raisesWizardry = false;
  // In SetItemAttributes' order: the wings' options, luck, the additional option, excellent, the rings'.
  const code = (option: CodeOption, value: number) => options.push({ effect: option, value });
  addWingOptions(item, definition, code);
  for (const kind of TAKE_ORDER) {
    const turnedOn = kind === 'luck' ? item.luck : kind === 'option' ? item.optionLevel > 0 : item.excellent !== 0;
    if (turnedOn) {
      for (const link of links) {
        if (link.type !== kind) continue;
        for (const option of link.options) {
          if (kind === 'option' && optionNumber(option) !== item.optionType) continue;
          if (kind === 'excellent' && !isExcellentBitSet(item.excellent, option)) continue;
          const effect = DATA_OPTION_EFFECTS[`${kind}:${option.attribute}`];
          if (!effect) continue;
          if (option.attribute === 'baseDamageBonus') raisesWizardry = true;
          options.push({ effect, value: optionValue(option, kind === 'option' ? item.optionLevel : 1, character) });
        }
      }
    }
    if (kind === 'option' && item.optionLevel && !links.some((link) => link.type === 'option')) {
      addCodeAdditionalOption(item, item.group * ITEMS_PER_GROUP + item.number, code);
    }
  }
  const type = item.group * ITEMS_PER_GROUP + item.number;
  if (type === 13 * ITEMS_PER_GROUP + 20 && (item.level === 0 || item.level === 3)) {
    code('improveMagicPercent', 10); code('improveDamagePercent', 10); code('improveAttackSpeed', 10);
  } else if (type === 13 * ITEMS_PER_GROUP + 107) {
    code('improveMagicPercent', 15); code('improveDamagePercent', 15); code('improveAttackSpeed', 10);
  }
  return {
    type,
    weaponKind: definition?.weaponKind ?? null,
    tags: definition?.tags ?? [],
    wingTier: definition?.wingTier ?? null,
    baseDurability: attribute.durability,
    baseMagicDurability: attribute.magicDurability,
    damageMin: stats.damageMin,
    damageMax: stats.damageMax,
    magicPower: stats.magicPower,
    blockRate: stats.blockRate,
    defense: stats.defense,
    require: stats.require,
    classes: attribute.classes,
    options,
    raisesWizardry,
    harmony: harmonyValues(item),
    ...guardianRates(item, type),
  };
}
