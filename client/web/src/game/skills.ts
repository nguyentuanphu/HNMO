// The character's skills as the desktop client keeps them: the skill data (Data/Local/Eng/
// skill_eng.bmd, SkillAttribute), the learned skills by list index from the server (ReceiveMagicList:
// SkillListUpdate, SkillAdded, SkillRemoved), the skill hotkeys 1-9 and 0 (the key configuration's
// first 20 bytes, CSkillList) and the current skill (Hero->CurrentSkill), and the skill icons
// (UI::Skills::Icon::ResolveSkillIcon, the sprites of skill_icons.rcss).

import { fetchBytes } from '../bmd.js';
import { isNewSkill, loadNewSkills, NEW_SKILL_LOOKS } from './newSkills.js';
import { registerHueShiftedSprite } from '../ui/rml.js';
import type { Connection } from '../net/connection.js';

const RECORD = 108; // SKILL_ATTRIBUTE_FILE: 50 byte name, fields with MSVC alignment
const MAX_SKILLS = 650;
const BUX = [0xfc, 0xcf, 0xab];

/** A skill's data (SKILL_ATTRIBUTE). */
export interface SkillData {
  name: string;
  level: number; damage: number; mana: number; ability: number; distance: number; delay: number;
  energy: number; leadership: number; masteryType: number; useType: number; brand: number; killCount: number;
  classes: number[]; icon: number; strength: number; dexterity: number;
}

let skillData: SkillData[] | null = null;

export async function loadSkillData(): Promise<SkillData[]> {
  if (skillData) return skillData;
  const bytes = await fetchBytes('/data/Local/Eng/skill_eng.bmd');
  const decoder = new TextDecoder();
  const list: SkillData[] = [];
  for (let i = 0; i < MAX_SKILLS && (i + 1) * RECORD <= bytes.length; i++) {
    const r = new Uint8Array(RECORD);
    for (let j = 0; j < RECORD; j++) r[j] = bytes[i * RECORD + j] ^ BUX[j % 3];
    const v = new DataView(r.buffer);
    const end = r.indexOf(0);
    list.push({
      name: decoder.decode(r.subarray(0, end >= 0 && end < 50 ? end : 50)),
      level: v.getUint16(50, true), damage: v.getUint16(52, true), mana: v.getUint16(54, true), ability: v.getUint16(56, true),
      distance: v.getUint32(60, true), delay: v.getInt32(64, true), energy: v.getInt32(68, true), leadership: v.getUint16(72, true),
      masteryType: r[74], useType: r[75], brand: v.getUint32(76, true), killCount: r[80], classes: [...r.subarray(84, 91)], icon: v.getUint16(92, true),
      strength: v.getInt32(96, true), dexterity: v.getInt32(100, true),
    });
  }
  // HNMO's own skills (700-727) from their list.
  for (const [number, data] of await loadNewSkills().catch(() => new Map<number, SkillData>())) list[number] = data;
  skillData = list;
  return list;
}

export function skillInfo(skill: number): SkillData | null {
  return skillData?.[skill] ?? null;
}

// Skill numbers the HUD treats specially (_enum.h).
export const SkillNumber = {
  TELEPORT: 6, TELEPORT_ALLY: 15, SPIRAL_SLASH: 57, STUN: 67, REMOVAL_BUFF: 72, PLASMA_STORM_FENRIR: 76,
  PET_COMMAND_DEFAULT: 120, PET_COMMAND_END: 124,
  ALICE_DRAINLIFE: 214, ALICE_THORNS: 217, ALICE_BERSERKER: 218, ALICE_SLEEP: 219, ALICE_BLIND: 220,
  ALICE_WEAKNESS: 221, ALICE_ENERVATION: 222, SUMMON_EXPLOSION: 223, SUMMON_REQUIEM: 224, SUMMON_POLLUTION: 225,
  LIGHTNING_SHOCK: 230, STRIKE_OF_DESTRUCTION: 232, EXPANSION_OF_WIZARDRY: 233, RECOVER: 234, MULTI_SHOT: 235,
  FLAME_STRIKE: 236, GIGANTIC_STORM: 237, CHAOTIC_DISEIER: 238, KILLING_BLOW: 260, MASTER_BEGIN: 300,
} as const;
export const SkillUseType = { NONE: 0, MASTER: 1, BRAND: 2, MASTERLEVEL: 3, MASTERACTIVE: 4 } as const;

const isHiddenFromList = (skill: number) => skill >= SkillNumber.STUN && skill <= SkillNumber.REMOVAL_BUFF;

// ResolveSkillIcon + IconSpriteName: the sprite of skill_icons.rcss for a skill, '' for none.
// Master skills (their own atlas) aren't shown yet.
export function skillIconSprite(skill: number, usable: boolean): string {
  if (skill === 0) return '';
  const S = SkillNumber;
  const data = skillInfo(skill);
  const cell = (atlas: string, column: number, row: number) => `skill-icon-${atlas}-${usable ? 'lit' : 'grey'}-${column}-${row}`;
  if (isNewSkill(skill)) {
    // A game icon in another colour (registered as its own sprite for the decorators).
    const look = NEW_SKILL_LOOKS[skill];
    const base = look ? skillIconSprite(look.icon.from, usable) : '';
    if (!base || !look) return '';
    const name = `skill-icon-hnmo-${skill}-${usable ? 'lit' : 'grey'}`;
    registerHueShiftedSprite(name, base, look.icon.hue);
    return name;
  }
  if (skill >= S.MASTER_BEGIN) return '';
  if (skill >= S.PET_COMMAND_DEFAULT && skill <= S.PET_COMMAND_END) {
    const command = skill - S.PET_COMMAND_DEFAULT;
    return cell('command', command % 8, Math.floor(command / 8));
  }
  if (skill === S.PLASMA_STORM_FENRIR) return cell('command', 4, 0);
  if (skill >= S.ALICE_DRAINLIFE && skill <= S.ALICE_THORNS) return cell('skill2', (skill - S.ALICE_DRAINLIFE) % 8, 3);
  if (skill >= S.ALICE_SLEEP && skill <= S.ALICE_BLIND) return cell('skill2', (skill - S.ALICE_SLEEP + 4) % 8, 3);
  if (skill === S.ALICE_BERSERKER) return cell('skill2', 10, 3);
  if (skill >= S.ALICE_WEAKNESS && skill <= S.ALICE_ENERVATION) return cell('skill2', skill - S.ALICE_WEAKNESS + 8, 3);
  if (skill >= S.SUMMON_EXPLOSION && skill <= S.SUMMON_REQUIEM) return cell('skill2', (skill - S.SUMMON_EXPLOSION + 6) % 8, 3);
  if (skill === S.SUMMON_POLLUTION) return cell('skill2', 11, 3);
  if (skill === S.STRIKE_OF_DESTRUCTION) return cell('skill2', 7, 2);
  if (skill === S.CHAOTIC_DISEIER) return cell('skill2', 3, 8);
  if (skill === S.RECOVER) return cell('skill2', 9, 2);
  if (skill === S.MULTI_SHOT) return cell('skill2', 0, 8);
  if (skill === S.FLAME_STRIKE) return cell('skill2', 1, 8);
  if (skill === S.GIGANTIC_STORM) return cell('skill2', 2, 8);
  if (skill === S.LIGHTNING_SHOCK) return cell('skill2', 2, 3);
  if (skill === S.EXPANSION_OF_WIZARDRY) return cell('skill2', 8, 2);
  if (data?.useType === SkillUseType.MASTERACTIVE) return cell('skill2', data.icon % 12, Math.floor(data.icon / 12) + 4);
  if (skill >= S.KILLING_BLOW) return cell('skill3', (skill - S.KILLING_BLOW) % 12, Math.floor((skill - S.KILLING_BLOW) / 12));
  if (skill >= S.SPIRAL_SLASH) return cell('skill2', (skill - S.SPIRAL_SLASH) % 8, Math.floor((skill - S.SPIRAL_SLASH) / 8));
  return cell('skill1', (skill - 1) % 8, Math.floor((skill - 1) / 8));
}

/** The character's stats the skill requirements are checked against. */
export interface SkillStats {
  level: number; strength: number; agility: number; energy: number; leadership: number;
}

// GetSkillInformation_Energy: the energy a skill needs.
export function requiredEnergy(skill: number, classIndex: number): number {
  const data = skillInfo(skill);
  if (!data || data.energy === 0) return 0;
  if (data.level === 0) return data.energy;
  if (classIndex === 1) return 10 + Math.floor(data.energy * data.level * 4 / 100); // Dark Knight
  if (skill === SkillNumber.SUMMON_EXPLOSION || skill === SkillNumber.SUMMON_REQUIEM) return 20 + Math.floor(data.energy * data.level * 3 / 100);
  return 20 + Math.floor(data.energy * data.level * 4 / 100);
}

// CSkillManager::AreSkillAttributeRequirementsMet: level, strength, agility, energy, leadership.
export function skillRequirementsMet(skill: number, stats: SkillStats, classIndex: number): boolean {
  const data = skillInfo(skill);
  if (!data) return true;
  return data.level <= stats.level && data.strength <= stats.strength && data.dexterity <= stats.agility
    && requiredEnergy(skill, classIndex) <= stats.energy && data.leadership <= stats.leadership;
}

const HOTKEY_COUNT = 10;
const FREE_HOTKEY = 0xffff;
const MAX_MAGIC = 64;

/** The learned skills, the hotkeys 0-9 and the current skill. */
export class SkillList {
  /** List index -> skill number (CharacterAttribute->Skill). */
  readonly skills = new Map<number, number>();
  readonly levels = new Map<number, number>();
  /** Hotkey 0-9 -> list index, -1 for none (m_iHotKeySkillType). */
  readonly hotkeys: number[] = Array(HOTKEY_COUNT).fill(-1);
  /** The list index of the skill used on a right click (Hero->CurrentSkill). */
  current = 0;
  /** The hotkey row shows 6-9 and 0 instead of 1-5 (m_bHotKeySkillListUp). */
  listUp = false;
  /** Skill numbers of the hotkeys from the key configuration, until the skills they name are known. */
  private pendingHotkeys: number[] | null = null;
  private readonly listeners = new Set<() => void>();

  constructor(connection: Connection, private readonly classIndex: number, private readonly saveHotkeys: (numbers: number[]) => void) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(): void {
    for (const listener of this.listeners) listener();
  }

  /** How many skills the list holds (CharacterAttribute->SkillNumber). */
  get count(): number {
    let count = 0;
    for (const skill of this.skills.values()) if (skill !== 0) count++;
    return count;
  }

  skillAt(index: number): number {
    return this.skills.get(index) ?? 0;
  }

  // ReceiveMagicList (C1 F3 11).
  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xf3 || bytes[3] !== 0x11) return;
    const value = bytes[4], listType = bytes[5];
    const entry = (at: number) => ({ index: bytes[at], skill: bytes[at + 1] | (bytes[at + 2] << 8), level: bytes[at + 3] });
    if (value === 0xff) {
      const { index } = entry(6);
      this.skills.delete(index); this.levels.delete(index);
    } else if (value === 0xfe) {
      const { index, skill, level } = entry(6);
      this.skills.set(index, skill); this.levels.set(index, level);
    } else if (listType === 0x02) {
      for (let i = 0; i < value; i++) { const { index } = entry(6); this.skills.delete(index); this.levels.delete(index); }
    } else {
      if (listType === 0x00) { this.skills.clear(); this.levels.clear(); }
      for (let i = 0; i < value && 6 + i * 4 + 3 < bytes.length; i++) {
        const { index, skill, level } = entry(6 + i * 4);
        this.skills.set(index, skill); this.levels.set(index, level);
      }
      if (this.classIndex === 4) { // Dark Lord: the raven's commands
        for (let skill = SkillNumber.PET_COMMAND_DEFAULT; skill < SkillNumber.PET_COMMAND_END; skill++) this.skills.set(skill, skill);
      }
    }
    // Keep the current skill on something real.
    const count = this.count;
    if (this.current >= count || count === 1 || this.skillAt(this.current) === 0) this.current = 0;
    for (let i = 0; i < MAX_SKILLS && isHiddenFromList(this.skillAt(this.current)); i++) this.current++;
    if (this.pendingHotkeys) this.applyHotkeys(this.pendingHotkeys);
    this.changed();
  }

  /** The skill hotkeys of the key configuration (first 20 bytes, each skill number high byte first). */
  applyKeyConfiguration(configuration: Uint8Array): void {
    const numbers: number[] = [];
    for (let i = 0; i < HOTKEY_COUNT; i++) numbers.push((configuration[i * 2] << 8) | configuration[i * 2 + 1]);
    this.pendingHotkeys = numbers;
    this.applyHotkeys(numbers);
    this.changed();
  }

  private applyHotkeys(numbers: number[]): void {
    let all = true;
    numbers.forEach((number, hotkey) => {
      if (number === FREE_HOTKEY || number === 0) { this.hotkeys[hotkey] = -1; return; }
      const index = [...this.skills].find(([, skill]) => skill === number)?.[0];
      if (index === undefined) { all = false; return; }
      this.hotkeys[hotkey] = index;
    });
    if (all) this.pendingHotkeys = null;
  }

  // SetHotKey: a skill sits on one hotkey only. Saved at once, as the desktop does (SaveOptionsIfInGame).
  setHotkey(hotkey: number, index: number): boolean {
    if (this.hotkeys[hotkey] === index) return false;
    const previous = this.hotkeys.indexOf(index);
    if (previous >= 0) this.hotkeys[previous] = -1;
    this.hotkeys[hotkey] = index;
    this.pendingHotkeys = null;
    this.saveHotkeys(this.hotkeys.map((i) => (i < 0 ? FREE_HOTKEY : this.skillAt(i))));
    this.updateListUp();
    this.changed();
    return true;
  }

  // UseHotKey: the number keys pick the current skill.
  useHotkey(hotkey: number, hasPet = false): void {
    const index = this.hotkeys[hotkey];
    if (index === -1) return;
    if (index >= SkillNumber.PET_COMMAND_DEFAULT && index < SkillNumber.PET_COMMAND_END && !hasPet) return;
    if (this.skillAt(index) === 0) return;
    this.current = index;
    this.updateListUp();
    this.changed();
  }

  select(index: number): void {
    this.current = index;
    this.updateListUp();
    this.changed();
  }

  // CSkillList::Update: the row shows the half (1-5 or 6-0) the current skill's hotkey is in.
  private updateListUp(): void {
    const hotkey = this.hotkeys.indexOf(this.current);
    if (hotkey >= 0) this.listUp = hotkey === 0 || hotkey > 5;
  }

  /** The hotkey (0-9) of a list index, -1 for none (GetSkillHotKeyNumber). */
  hotkeyOf(index: number): number {
    return this.hotkeys.indexOf(index);
  }

  // GetHotKeySlotNumber: the hotkey a slot of the row shows (1-5, or 6-9 and 0), -1 for none.
  slotHotkey(slot: number, hasPet = false): number {
    if (slot < 0 || slot >= 5 || this.count === 0) return -1;
    let hotkey = (this.listUp ? 6 : 1) + slot;
    if (hotkey === 10) hotkey = 0;
    const index = this.hotkeys[hotkey];
    if (index === -1) return -1;
    if (index >= SkillNumber.PET_COMMAND_DEFAULT && index < SkillNumber.PET_COMMAND_END && !hasPet) return -1;
    return hotkey;
  }

  // RebuildGridSnapshot: the list indexes the skill popup shows, in order.
  gridIndexes(): number[] {
    const result: number[] = [];
    for (let i = 0; i < MAX_MAGIC; i++) {
      const skill = this.skillAt(i);
      if (skill === 0 || isHiddenFromList(skill)) continue;
      const useType = skillInfo(skill)?.useType ?? 0;
      if (useType === SkillUseType.MASTER || useType === SkillUseType.MASTERLEVEL) continue;
      result.push(i);
    }
    return result;
  }
}

/** What the grey-icon checks look at: the hands, the pet slot and the stats with their bonuses. */
export interface SkillEquipment {
  right: { group: number; number: number } | null;
  left: { group: number; number: number } | null;
  helper: { group: number; number: number; durability: number } | null;
  strength: number;
  agility: number;
}

const TWISTING_SLASH = 41, RAGEFUL_BLOW = 42, DEATH_STAB = 43, IMPALE = 47, ICE_ARROW = 51, FIRE_SLASH = 55,
  POWER_SLASH = 56, EARTHSHAKE = 62, BLOCKING = 18, SLASH = 23;

// IsHudSkillUsable's equipment checks (the desktop greys the icon when they fail): melee skills
// need a weapon that isn't a staff or book, Multi-Shot a bow with arrows or a crossbow with bolts,
// Earthshake a living dark horse, Impale a horn mount and a spear, and riding a horn mount rules
// out the knight's basic skills. The buff, party, map and pet checks come with those features.
export function skillUsableWithEquipment(skill: number, equipment: SkillEquipment, kindOf: (item: { group: number; number: number }) => string | undefined): boolean {
  const { right, left, helper } = equipment;
  const kind = (item: { group: number; number: number } | null) => (item ? kindOf(item) : undefined);
  const staffOrBook = (item: { group: number; number: number } | null) => kind(item) === 'staff' || kind(item) === 'book';
  const melee = right !== null && !staffOrBook(right) && !staffOrBook(left);
  const hornMount = helper !== null && helper.group === 13 && [2, 3, 37].includes(helper.number);
  if (skill === IMPALE && (!hornMount || (kind(left) !== 'spear' && kind(right) !== 'spear'))) return false;
  if (hornMount && ((skill >= BLOCKING && skill <= SLASH) || skill === POWER_SLASH)) return false;
  if (skill === EARTHSHAKE && !(helper && helper.group === 13 && helper.number === 4 && helper.durability > 0)) return false;
  if (skill === FIRE_SLASH && (equipment.strength < 596 || !melee)) return false;
  if (skill === ICE_ARROW && equipment.agility < 646) return false;
  if ((skill === TWISTING_SLASH || skill === RAGEFUL_BLOW || skill === DEATH_STAB || skill === SkillNumber.FLAME_STRIKE) && !melee) return false;
  if (skill === SkillNumber.MULTI_SHOT) {
    const bow = kind(left) === 'bow' && right?.group === 4 && right.number === 15;
    const crossbow = kind(right) === 'crossbow' && left?.group === 4 && left.number === 7;
    if (!bow && !crossbow) return false;
  }
  return true;
}

/**
 * The skills' cooldowns (CharacterAttribute->SkillDelay, CheckSkillDelay / CalcSkillDelay): a skill
 * whose data has a delay starts it when it's cast and can't be cast again until it ran out; the
 * skill bar's wipe shows the part that's left (ComputeSkillCooldownFraction).
 */
export class SkillCooldowns {
  private readonly until = new Map<number, { end: number; length: number }>();

  /** CheckSkillDelay: false while the skill is cooling down; otherwise starts its delay (if any). */
  tryStart(skill: number, delayMs: number, now = performance.now()): boolean {
    if (delayMs <= 0) return true;
    if (this.remaining(skill, now) > 0) return false;
    this.until.set(skill, { end: now + delayMs, length: delayMs });
    return true;
  }

  remaining(skill: number, now = performance.now()): number {
    const entry = this.until.get(skill);
    if (!entry) return 0;
    const left = entry.end - now;
    if (left <= 0) { this.until.delete(skill); return 0; }
    return left;
  }

  /** The part of the delay still to run, 0..1 (the wipe's height). */
  fraction(skill: number, now = performance.now()): number {
    const entry = this.until.get(skill);
    return entry ? Math.min(1, this.remaining(skill, now) / entry.length) : 0;
  }

  get active(): boolean { return this.until.size > 0; }
}

export const skillCooldowns = new SkillCooldowns();
