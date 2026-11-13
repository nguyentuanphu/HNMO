// The master level and the master skill tree, as the desktop client keeps them (UI/HUD/MasterLevel.cpp,
// Master_Level_Data and CharacterAttribute->MasterSkillInfo): the tree data (Data/Local/MasterSkillTreeData.bmd)
// and its tooltips (Data/Local/Eng/MasterSkillTooltip_eng.bmd), the master level, experience and points from
// the server (MasterStatsUpdate F3 50, MasterCharacterLevelUpdate F3 51, the master experience of
// ExperienceGainedExtended), the learned master skills (MasterSkillList F3 53) and the answer to a learn
// request (MasterSkillLevelUpdate F3 52), and the tree's rules (CheckParentSkill, CheckRankPoint,
// CheckBeforeSkill, CheckSkillPoint, IsNonWeaponSkillOrIsSkillEquipped).

import { fetchBytes } from '../bmd.js';
import { read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import { masterSkillToBaseSkill, skillInfo, SkillUseType } from './skills.js';

const BUX = [0xfc, 0xcf, 0xab];
const MAX_MASTER_SKILL_DATA = 512;
const TREE_RECORD = 24;     // _MASTER_SKILLTREE_DATA
const TOOLTIP_RECORD = 616; // _MASTER_SKILL_TOOLTIP_FILE: int, WORD, 64 + 256 + 32 + 4 x 64 chars, padded to 4
export const MASTER_SKILL_BEGIN = 300;
export const MASTER_SKILL_END = 608;
export const MASTER_SKILL_CATEGORIES = 3;
export const MASTER_SKILL_LEVEL_REQ_FOR_NEXT_RANK = 10;
const MAX_MASTER_TREE_RANK = 10;
/** Master levels unlock at this character level (kMasterExperienceUnlockLevel). */
export const MASTER_EXPERIENCE_UNLOCK_LEVEL = 400;
const EXPERIENCE_TYPE_MASTER = 2; // AddResult.Master

/** One node of the tree (_MASTER_SKILLTREE_DATA). */
export interface MasterTreeNode {
  /** The node's place in the tree, 1-based (four nodes a rank). */
  index: number;
  /** The classes the node is for (MASTER_SKILL_TREE_CLASS bits). */
  classCode: number;
  /** The tree column (0-2). */
  group: number;
  requiredPoints: number;
  maxLevel: number;
  /** The arrow drawn from the node (0: none). */
  arrow: number;
  requires: [number, number];
  skill: number;
  /** The value the tooltip shows before the first point (-1: the text has no value). */
  defaultValue: number;
}

/** A skill's tooltip texts (_MASTER_SKILL_TOOLTIP): Info1 … Info7. */
export interface MasterTooltip {
  skill: number;
  classCode: number;
  info: string[];
}

export interface MasterTreeData {
  nodes: MasterTreeNode[];
  tooltips: MasterTooltip[];
}

const decode = (bytes: Uint8Array, at: number, size: number): DataView => {
  const record = new Uint8Array(size);
  for (let j = 0; j < size; j++) record[j] = bytes[at + j] ^ BUX[j % 3];
  return new DataView(record.buffer);
};

const decoder = new TextDecoder();
const text = (view: DataView, at: number, size: number): string => {
  const bytes = new Uint8Array(view.buffer, at, size);
  const end = bytes.indexOf(0);
  return decoder.decode(end >= 0 ? bytes.subarray(0, end) : bytes);
};

let treeData: Promise<MasterTreeData> | null = null;

// OpenMasterSkillTreeData and OpenMasterSkillTooltip: each record BuxConvert'ed, the lists end at the first empty one.
export function loadMasterTreeData(): Promise<MasterTreeData> {
  treeData ??= (async () => {
    const [tree, tooltip] = await Promise.all([fetchBytes('/data/Local/MasterSkillTreeData.bmd'), fetchBytes('/data/Local/Eng/MasterSkillTooltip_eng.bmd')]);
    const nodes: MasterTreeNode[] = [];
    for (let i = 0; i < MAX_MASTER_SKILL_DATA && (i + 1) * TREE_RECORD <= tree.length; i++) {
      const v = decode(tree, i * TREE_RECORD, TREE_RECORD);
      const index = v.getUint16(0, true);
      if (index === 0) break;
      nodes.push({
        index, classCode: v.getUint16(2, true), group: v.getUint8(4), requiredPoints: v.getUint8(5), maxLevel: v.getUint8(6),
        arrow: v.getUint8(7), requires: [v.getInt32(8, true), v.getInt32(12, true)], skill: v.getInt32(16, true),
        defaultValue: v.getFloat32(20, true),
      });
    }
    const tooltips: MasterTooltip[] = [];
    for (let i = 0; i < MAX_MASTER_SKILL_DATA && (i + 1) * TOOLTIP_RECORD <= tooltip.length; i++) {
      const v = decode(tooltip, i * TOOLTIP_RECORD, TOOLTIP_RECORD);
      const skill = v.getInt32(0, true);
      if (skill === 0) break;
      tooltips.push({
        skill, classCode: v.getUint16(4, true),
        info: [text(v, 6, 64), text(v, 70, 256), text(v, 326, 32), text(v, 358, 64), text(v, 422, 64), text(v, 486, 64), text(v, 550, 64)],
      });
    }
    return { nodes, tooltips };
  })();
  return treeData;
}

/** MASTER_SKILL_TREE_CLASS of a master class (SetMasterType), by the server's class number; 0 for none. */
export function masterTreeClass(classNumber: number): number {
  switch (classNumber) {
    case 3: return 2;   // Grand Master
    case 7: return 1;   // Blade Master
    case 11: return 4;  // High Elf
    case 13: return 16; // Duel Master
    case 17: return 32; // Lord Emperor
    case 23: return 8;  // Dimension Master
    case 25: return 64; // Fist Master
    default: return 0;
  }
}

/** CCharacterManager::IsMasterLevel (IsThirdClass): the master classes, by the server's class number. */
export function isMasterClass(classNumber: number): boolean {
  return masterTreeClass(classNumber) !== 0;
}

// GetMasterLowerBound: the total experience a master level starts at (master levels go on from the 400 normal ones).
export function masterLevelStart(masterLevel: number): number {
  const total = masterLevel + 400;
  const over = total - 255;
  const curve = (9 + total) * total * total * 10 + (9 + over) * over * over * 1000;
  return Math.trunc((curve - 3892250000) / 2);
}

/** A learned master skill (CSkillTreeInfo): its level and the values the tooltip shows. */
export interface MasterSkillInfo { level: number; value: number; nextValue: number }

/** What the tree checks against: the hero's learned skills and the weapons in hand. */
export interface MasterHero {
  /** Whether the hero's skill list holds a skill (CharacterAttribute->Skill). */
  knowsSkill: (skill: number) => boolean;
  /** The skills the weapons in both hands bring (ITEM::Special of the hands). */
  weaponSkills: () => number[];
}

export type MasterChange = 'stats' | 'level' | 'skills' | 'learned' | 'experience';

export class MasterLevel {
  /** Master_Level_Data. */
  level = 0;
  experience = 0;
  nextExperience = 0;
  points = 0;
  maxPoints = 0;
  maxLife = 0; maxMana = 0; maxShield = 0; maxAbility = 0;
  /** The tree's class (MASTER_SKILL_TREE_CLASS), 0 until the master skill list came. */
  classCode = 0;
  /** The tree's nodes for the class, by their index (map_masterData). */
  readonly nodes = new Map<number, MasterTreeNode>();
  /** The tooltips for the class, by skill (map_masterSkillToolTip). */
  readonly tooltips = new Map<number, MasterTooltip>();
  /** MasterSkillInfo, by skill number. */
  readonly skills = new Map<number, MasterSkillInfo>();
  /** The points spent per column (CategoryPoint). */
  readonly categoryPoints = [0, 0, 0];
  /** The highest skill level seen per column and rank (skillPoint, filled while the tree is checked). */
  private readonly rankLevels: number[][] = [0, 1, 2].map(() => Array<number>(MAX_MASTER_TREE_RANK + 1).fill(0));
  private data: MasterTreeData | null = null;
  private readonly waiting: Uint8Array[] = [];
  private readonly listeners = new Set<(change: MasterChange) => void>();

  constructor(connection: Connection, private readonly classNumber: number, private readonly hero: MasterHero,
    private readonly learned?: (skill: number, level: number) => void) {
    connection.onPacket((bytes) => this.handle(bytes));
    void loadMasterTreeData().then((data) => {
      this.data = data;
      for (const bytes of this.waiting.splice(0)) this.handle(bytes);
    }).catch((error: unknown) => console.warn('Master skill tree', error));
  }

  get isMaster(): boolean {
    return isMasterClass(this.classNumber);
  }

  /** IsMasterExperienceActive: the HUD's experience bar shows the master experience. */
  experienceActive(characterLevel: number): boolean {
    return this.isMaster && characterLevel >= MASTER_EXPERIENCE_UNLOCK_LEVEL;
  }

  onChange(listener: (change: MasterChange) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(change: MasterChange): void {
    for (const listener of this.listeners) listener(change);
  }

  skillLevel(skill: number): number {
    return this.skills.get(skill)?.level ?? 0;
  }

  handle(bytes: Uint8Array): void {
    const c1 = bytes[0] === 0xc1, c2 = bytes[0] === 0xc2;
    if (c1 && bytes[2] === 0xf3 && bytes[3] === 0x50) {
      // Receive_Master_Level_Exp.
      const extended = bytes[1] >= 40;
      const p = read('ServerToClient', extended ? 'MasterStatsUpdateExtended' : 'MasterStatsUpdate', bytes);
      const n = (name: string) => (typeof p[name] === 'number' ? p[name] as number : 0);
      this.level = n('MasterLevel'); this.experience = n('MasterExperience'); this.nextExperience = n('MasterExperienceOfNextLevel');
      this.points = n('MasterLevelUpPoints');
      this.maxLife = n('MaximumHealth'); this.maxMana = n('MaximumMana'); this.maxShield = n('MaximumShield'); this.maxAbility = n('MaximumAbility');
      this.changed('stats');
    } else if (c1 && bytes[2] === 0xf3 && bytes[3] === 0x51) {
      // Receive_Master_LevelUp: the new level and points; the next level's experience follows from the curve.
      const extended = bytes[1] >= 28;
      const p = read('ServerToClient', extended ? 'MasterCharacterLevelUpdateExtended' : 'MasterCharacterLevelUpdate', bytes);
      const n = (name: string) => (typeof p[name] === 'number' ? p[name] as number : 0);
      this.level = n('MasterLevel'); this.points = n('CurrentMasterPoints'); this.maxPoints = n('MaximumMasterPoints');
      this.maxLife = n('MaximumHealth'); this.maxMana = n('MaximumMana'); this.maxShield = n('MaximumShield'); this.maxAbility = n('MaximumAbility');
      // CalulateMasterLevelNextExperience.
      this.experience = Math.max(this.experience, masterLevelStart(this.level));
      this.nextExperience = masterLevelStart(this.level + 1);
      this.changed('level');
    } else if (bytes[0] === 0xc3 && bytes[2] === 0x16 && bytes[3] === EXPERIENCE_TYPE_MASTER && bytes.length >= 8) {
      // The master part of ExperienceGainedExtended: clamped to the level's range.
      const added = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength).getUint32(4, true);
      const lower = masterLevelStart(this.level), upper = Math.max(lower, this.nextExperience);
      this.experience = Math.min(upper, Math.max(lower, Math.min(Math.max(this.experience, lower), upper) + added));
      this.changed('experience');
    } else if (c2 && bytes[3] === 0xf3 && bytes[4] === 0x53) {
      if (!this.data) { this.waiting.push(bytes); return; }
      this.receiveSkillList(bytes);
    } else if (c1 && bytes[2] === 0xf3 && bytes[3] === 0x52) {
      if (!this.data) { this.waiting.push(bytes); return; }
      this.receiveLearned(bytes);
    }
  }

  // Receive_Master_SetSkillList → ReplaceMasterSkills: the class's tree, the points and the learned skills.
  private receiveSkillList(bytes: Uint8Array): void {
    const p = read('ServerToClient', 'MasterSkillList', bytes);
    this.setMasterType();
    this.skills.clear();
    this.categoryPoints.fill(0);
    for (const row of this.rankLevels) row.fill(0);
    const entries = Array.isArray(p.Skills) ? p.Skills as Record<string, unknown>[] : [];
    for (const entry of entries) {
      const node = this.nodes.get(Number(entry.MasterSkillIndex));
      if (!node) continue;
      const level = Number(entry.Level);
      this.skills.set(node.skill, { level, value: Number(entry.DisplayValue), nextValue: Number(entry.DisplayValueOfNextLevel) });
      if (node.group < MASTER_SKILL_CATEGORIES) this.categoryPoints[node.group] += level;
    }
    this.changed('skills');
  }

  // Receive_Master_LevelGetSkill: the skill goes into the skill list, the tree takes its level (SkillUpgrade).
  private receiveLearned(bytes: Uint8Array): void {
    const p = read('ServerToClient', 'MasterSkillLevelUpdate', bytes);
    const skill = Number(p.MasterSkillNumber);
    const level = Number(p.Level);
    if (p.Success === true && skill > 0) {
      this.learned?.(skill, level);
      const node = this.nodes.get(Number(p.MasterSkillIndex));
      if (node) {
        const old = this.skillLevel(node.skill);
        this.skills.set(node.skill, { level, value: Number(p.DisplayValue), nextValue: Number(p.DisplayValueOfNextLevel) });
        if (node.group < MASTER_SKILL_CATEGORIES) this.categoryPoints[node.group] += level - old;
      }
    }
    this.points = Number(p.MasterLevelUpPoints);
    this.changed('learned');
  }

  // SetMasterType: the nodes and tooltips of the hero's tree.
  private setMasterType(): void {
    this.classCode = masterTreeClass(this.classNumber);
    this.nodes.clear();
    this.tooltips.clear();
    if (!this.data) return;
    for (const node of this.data.nodes) {
      if ((this.classCode & node.classCode) === 0) continue;
      if (this.nodes.has(node.index & 0xff)) break;
      this.nodes.set(node.index & 0xff, node);
    }
    for (const tooltip of this.data.tooltips) {
      if ((this.classCode & tooltip.classCode) === 0) continue;
      if (this.tooltips.has(tooltip.skill)) break;
      this.tooltips.set(tooltip.skill, tooltip);
    }
  }

  /** The nodes in tree order (by index, as std::map keeps them). */
  orderedNodes(): MasterTreeNode[] {
    return [...this.nodes.values()].sort((a, b) => (a.index & 0xff) - (b.index & 0xff));
  }

  /** The skill's rank in its column (SKILL_ATTRIBUTE::SkillRank). */
  rankOf(node: MasterTreeNode): number {
    return skillInfo(node.skill)?.rank ?? 1;
  }

  // CheckParentSkill: the required master skills at level 10 or more.
  checkParentSkill(node: MasterTreeNode): boolean {
    for (const required of node.requires) {
      if (required === 0) return true;
      if (required < MASTER_SKILL_BEGIN || required > MASTER_SKILL_END) return true;
      if (this.skillLevel(required) < MASTER_SKILL_LEVEL_REQ_FOR_NEXT_RANK) return false;
    }
    return true;
  }

  // CheckRankPoint: records the level seen for the column's rank; above rank 1, the previous rank needs a skill at 10.
  checkRankPoint(group: number, rank: number, skillLevel: number): boolean {
    const row = this.rankLevels[group];
    if (!row) return false;
    if ((row[rank] ?? 0) < skillLevel) row[rank] = skillLevel;
    if (rank === 1) return true;
    return (row[rank - 1] ?? 0) >= MASTER_SKILL_LEVEL_REQ_FOR_NEXT_RANK;
  }

  // CheckBeforeSkill: a first point needs the skill the master skill strengthens (SkillBrand) in the skill list.
  checkBeforeSkill(skill: number, skillLevel: number): boolean {
    if (skillLevel !== 0) return true;
    const brand = skillInfo(skill)?.brand ?? 0;
    if (brand === 0) return true;
    const brandData = skillInfo(brand);
    if (!brandData) return false;
    if (brandData.useType === SkillUseType.MASTERACTIVE) return true;
    return this.hero.knowsSkill(brand);
  }

  // CSItemOption::IsNonWeaponSkillOrIsSkillEquipped: the weapon skills need a weapon in hand that brings them.
  weaponSkillEquipped(skill: number): boolean {
    const base = masterSkillToBaseSkill(skill);
    const weaponSkills = [56, 24, 19, 20, 21, 22, 23]; // Power Slash, Triple Shot, Falling Slash, Lunge, Uppercut, Cyclone, Slash
    if (!weaponSkills.includes(base)) return true;
    return this.hero.weaponSkills().includes(base);
  }

  // IsNodeUsable: the lit icon and white level.
  isNodeUsable(node: MasterTreeNode): boolean {
    const level = this.skillLevel(node.skill);
    return this.checkParentSkill(node) && this.checkRankPoint(node.group, this.rankOf(node), level)
      && this.checkBeforeSkill(node.skill, level) && this.weaponSkillEquipped(node.skill);
  }
}
