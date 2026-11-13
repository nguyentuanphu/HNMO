// The master skill tree (master_level.rml with master_level_band.rml under the HUD), as the desktop
// client's CMasterLevel (UI/HUD/MasterLevel.cpp) shows it: the class's tree in its three columns, each node's
// box, lit or grey icon, arrow and level, the header (class, master level, level points, the master
// experience in percent with its hint) and the points spent per column; a node's hint (BuildNodeHintLines),
// a press asks whether to strengthen the skill and sends AddMasterSkillPoint after the desktop's checks
// (CheckAttributeArea); the server's answer updates the tree, the skill list and the HUD. A opens and closes
// it (HotKey), Escape and the close button close it. setupMasterLevel wires it into the game.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { ItemTooltipView } from './itemTooltipView.js';
import { confirmInGame, messageInGame } from './confirmDialog.js';
import type { Connection } from '../net/connection.js';
import type { CharacterStats } from '../game/stats.js';
import type { Inventory } from '../game/inventory.js';
import type { TooltipLine } from '../game/itemTooltip.js';
import { itemDefinition } from '../game/items.js';
import { loadSkillData, masterIconSprite, skillInfo, type SkillList } from '../game/skills.js';
import {
  loadMasterTreeData, MasterLevel, masterLevelStart, MASTER_SKILL_BEGIN, MASTER_SKILL_CATEGORIES, MASTER_SKILL_END,
  type MasterTreeNode,
} from '../game/masterLevel.js';
import type { Hud } from './hud.js';
import type { CharacterWindow } from './characterWindow.js';

// The desktop's texts (Localization/Game.en.resx).
const T = {
  MasterLevelD: 'Master level: %d', LevelPointD: 'Level point: %d', EXP62f: 'EXP: %6.2f%%', Close: 'Close',
  NextLevel: '# #Next Level:#', Requirements: '# #Requirements:#',
  CantRaise: "You can't raise any more levels.", WearEquipment: 'You need to wear the required equipment to level up this skill.',
  MeetRequirements: 'You must meet all skill requirements.', Strengthen: 'Would you like to strengthen the skill?',
  PointRequirementD: 'Master level point requirement: %d',
};

// MasterTreeHeaderTexts by base class: the master class's name and the columns' point formats.
const HEADERS: { name: string; columns: [string, string, string] }[] = [
  { name: 'Grand Master', columns: ['Peace: %d', 'Wisdom: %d', 'Overcome: %d'] },
  { name: 'Blade Master', columns: ['Protection: %d', 'Bravery: %d', 'Anger: %d'] },
  { name: 'High Elf', columns: ['Blessing: %d', 'Salvation: %d', 'Storm: %d'] },
  { name: 'Dual Master', columns: ['Solidity: %d', 'Fighting Spirit: %d', 'Ultimatum: %d'] },
  { name: 'Lord Emperor', columns: ['Determination: %d', 'Justice: %d', 'Conquer: %d'] },
  { name: 'Dimension Master', columns: ['Guardian: %d', 'Chaos: %d', 'Honor: %d'] },
  { name: 'Fist Master', columns: ['Willpower: %d', 'Determination: %d', 'Destruction: %d'] },
];

const COLORS = ['white', 'blue', 'red', 'yellow', 'green']; // TEXT_COLOR_WHITE … TEXT_COLOR_GREEN
const HINT_WIDTH = 150;        // DivideStringByPixel's pixels a line
const NODE_HINT_FLIP_TOP = 300; // kNodeHintFlipTop: lower nodes show their hint above

/** A printf-style format (%d, %i, %u, %f with width and precision, %%). */
export function formatC(format: string, ...values: number[]): string {
  let next = 0;
  return format.replace(/%(-?)(\d*)(?:\.(\d+))?(I64d|lld|llu|ld|lu|[diuf%])/g, (all, left: string, width: string, precision: string | undefined, kind: string) => {
    if (kind === '%') return '%';
    const value = values[next++] ?? 0;
    const textValue = kind === 'f' ? value.toFixed(precision !== undefined ? Number(precision) : 6) : String(Math.trunc(value));
    const size = Number(width || 0);
    return left ? textValue.padEnd(size) : textValue.padStart(size);
  });
}

let measure: CanvasRenderingContext2D | null = null;
// DivideStringByPixel: '#' separates lines (empty ones are skipped), each gets a leading space and wraps at the width.
function divide(text: string): string[] {
  measure ??= document.createElement('canvas').getContext('2d');
  if (measure) measure.font = '12px Tahoma, Arial, sans-serif';
  const width = (s: string) => (measure ? measure.measureText(s).width : s.length * 6);
  const lines: string[] = [];
  for (const token of text.split('#')) {
    if (!token) continue;
    let line = '';
    for (const word of ` ${token}`.split(/(?<= )/)) {
      if (line && width(line + word) > HINT_WIDTH) { lines.push(line); line = word; } else line += word;
    }
    lines.push(line);
  }
  return lines;
}

interface NodeEntry { id: number; column: number; slot: number; rank: number; icon: string; usable: boolean; arrow: number; level_text: string }

interface MasterLevelModel extends RmlModel {
  text_px: number;
  class_name_text: string; master_level_text: string; level_point_text: string; experience_text: string;
  column_text_0: string; column_text_1: string; column_text_2: string; close_hint: string;
  nodes: NodeEntry[];
  master_node_press: (id: number) => void;
  master_node_hover: (id: number) => void;
  master_experience_hover: (on: number) => void;
  master_close: () => void;
}

export interface MasterLevelWindowOptions {
  connection: Connection;
  master: MasterLevel;
  /** 0 Dark Wizard … 6 Rage Fighter. */
  classIndex: number;
  parent?: HTMLElement;
  click?: () => void;
  /** HideAllGroupA: the windows the tree closes when it opens. */
  closeOthers?: () => void;
}

export class MasterLevelWindow {
  private readonly parent: HTMLElement;
  private readonly tooltip: ItemTooltipView;
  private readonly model: MasterLevelModel;
  private doc: RmlDocument | null = null;
  private band: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private hovered = -1;
  private experienceHovered = false;

  constructor(private readonly options: MasterLevelWindowOptions) {
    this.parent = options.parent ?? document.body;
    this.tooltip = new ItemTooltipView(this.parent, () => null);
    this.model = {
      text_px: 12, class_name_text: '', master_level_text: '', level_point_text: '', experience_text: '',
      column_text_0: '', column_text_1: '', column_text_2: '', close_hint: T.Close, nodes: [],
      master_node_press: (id) => this.press(id),
      master_node_hover: (id) => { this.hovered = id; this.showHints(); },
      master_experience_hover: (on) => { this.experienceHovered = on !== 0; this.showHints(); },
      master_close: () => this.close(),
    };
    options.master.onChange(() => this.refresh());
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.target instanceof Element && event.target.closest('input, textarea')) return;
      if (event.key === 'Escape' && this.isOpen) { this.close(); this.options.click?.(); }
    });
  }

  get isOpen(): boolean {
    return this.doc !== null;
  }

  async toggle(): Promise<void> {
    if (this.isOpen) this.close();
    else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc || !this.options.master.isMaster) return;
    this.opening ??= this.openDocument().finally(() => { this.opening = null; });
    await this.opening;
  }

  private async openDocument(): Promise<void> {
    await Promise.all([prepareDocument('master_level'), prepareDocument('master_level_band'), loadMasterTreeData(), loadSkillData()]);
    if (this.doc) return;
    this.options.closeOthers?.();
    this.refresh(false);
    const band = await openDocument('master_level_band', {}, { parent: this.parent });
    band.host.style.zIndex = '19'; // under the HUD (20), as the desktop shows it behind the bottom HUD
    const doc = await openDocument('master_level', this.model, { parent: this.parent });
    doc.host.style.zIndex = '30';
    this.doc = doc;
    this.band = band;
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
      panel.addEventListener('contextmenu', (event) => event.preventDefault());
      panel.addEventListener('mouseover', (event) => this.showButtonHint(event));
      panel.addEventListener('mouseout', (event) => {
        if (event.target instanceof Element && event.target.closest('[data-hint]')) this.tooltip.hide();
      });
    }
    this.place();
    this.refresh();
  }

  close(): void {
    if (!this.doc) return;
    this.tooltip.hide();
    this.doc.close();
    this.band?.close();
    this.doc = null;
    this.band = null;
    this.hovered = -1;
    this.experienceHovered = false;
  }

  // The 640 x 480 reference screen at the HUD's scale; the theme stands the tree on its bottom, centred.
  place(): void {
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    for (const doc of [this.doc, this.band]) {
      if (!doc) continue;
      doc.setScale(scale);
      Object.assign(doc.root.style, {
        width: `${window.innerWidth / scale}px`, height: `${REFERENCE_HEIGHT}px`, transform: `scale(${scale})`,
        transformOrigin: 'left top', pointerEvents: 'none',
      });
      doc.root.style.setProperty('--hud-scale', '1');
    }
    this.model.text_px = 12;
    this.doc?.update();
  }

  // SyncHeaderTexts + RebuildNodeModel.
  refresh(update = true): void {
    const master = this.options.master;
    const m = this.model;
    const header = HEADERS[this.options.classIndex] ?? HEADERS[0];
    m.class_name_text = header.name;
    m.master_level_text = formatC(T.MasterLevelD, master.level);
    m.level_point_text = formatC(T.LevelPointD, master.points);
    if (master.nextExperience !== 0) {
      const start = masterLevelStart(master.level);
      m.experience_text = formatC(T.EXP62f, (master.experience - start) / (master.nextExperience - start) * 100);
    } else m.experience_text = '';
    m.column_text_0 = formatC(header.columns[0], master.categoryPoints[0]);
    m.column_text_1 = formatC(header.columns[1], master.categoryPoints[1]);
    m.column_text_2 = formatC(header.columns[2], master.categoryPoints[2]);
    // In tree order: the rank check reads the levels the earlier ranks recorded on the way.
    const nodes: NodeEntry[] = [];
    for (const node of master.orderedNodes()) {
      if (node.group >= MASTER_SKILL_CATEGORIES) continue;
      const usable = master.isNodeUsable(node);
      nodes.push({
        id: node.index & 0xff, column: node.group, slot: ((node.index & 0xff) - 1) % 4, rank: master.rankOf(node),
        icon: `image(${masterIconSprite(skillInfo(node.skill)?.icon ?? 0, usable)})`, usable, arrow: node.arrow,
        level_text: String(master.skillLevel(node.skill)),
      });
    }
    m.nodes = nodes;
    if (update && this.doc) {
      this.doc.update();
      this.showHints();
    }
  }

  // OnNodePressed → CheckAttributeArea.
  private press(id: number): void {
    const master = this.options.master;
    const node = master.nodes.get(id);
    if (!node || node.group >= MASTER_SKILL_CATEGORIES) return;
    this.options.click?.();
    this.tooltip.hide();
    const level = master.skillLevel(node.skill);
    // CheckSkillPoint (both refusals show the same text on the desktop).
    if (level >= node.maxLevel || master.points < node.requiredPoints) { void messageInGame(T.CantRaise); return; }
    if (!master.weaponSkillEquipped(node.skill)) { void messageInGame(T.WearEquipment); return; }
    if (!master.checkParentSkill(node) || !master.checkRankPoint(node.group, master.rankOf(node), level)
      || !master.checkBeforeSkill(node.skill, level)) {
      void messageInGame(T.MeetRequirements);
      return;
    }
    void confirmInGame(`${T.Strengthen}\n${formatC(T.PointRequirementD, node.requiredPoints)}`).then((yes) => {
      if (yes) this.options.connection.send('ClientToServer', 'AddMasterSkillPoint', { SkillId: node.skill });
    });
  }

  private showHints(): void {
    if (!this.doc) return;
    if (this.hovered >= 0 && this.showNodeHint(this.hovered)) return;
    if (this.experienceHovered) { this.showExperienceHint(); return; }
    this.tooltip.hide();
  }

  // ShowExperienceHint: "%I64d / %I64d" under the experience text.
  private showExperienceHint(): void {
    const area = this.doc?.$('#experience_area');
    if (!area) return;
    const master = this.options.master;
    const box = area.getBoundingClientRect();
    void this.tooltip.showLines([line(`${master.experience} / ${master.nextExperience}`, 0)],
      { left: box.left, top: box.top, width: box.width, height: box.height }, area);
  }

  // ShowNodeHint: under the icon, above it for the tree's lower part.
  private showNodeHint(id: number): boolean {
    const master = this.options.master;
    const node = master.nodes.get(id);
    const element = this.doc?.$(`#node_${id}`);
    const icon = element?.querySelector('.node-icon');
    const panel = this.doc?.$('#panel');
    if (!node || !element || !icon || !panel) return false;
    const lines = this.hintLines(node);
    if (!lines) return false;
    const box = icon.getBoundingClientRect(), panelBox = panel.getBoundingClientRect();
    const scale = panelBox.height / 428;
    const above = scale > 0 && (element.getBoundingClientRect().top - panelBox.top) / scale > NODE_HINT_FLIP_TOP;
    void this.tooltip.showLines(lines, { left: box.left, top: box.top, width: box.width, height: box.height }, element, { above });
    return true;
  }

  /** BuildNodeHintLines: the name, rank and level, what it does now and next, what it needs. */
  hintLines(node: MasterTreeNode): TooltipLine[] | null {
    const master = this.options.master;
    const tooltip = master.tooltips.get(node.skill);
    if (!tooltip) return null;
    const data = skillInfo(node.skill);
    const info = master.skills.get(node.skill);
    const level = info?.level ?? 0;
    const rank = data?.rank ?? 1;
    const lines: TooltipLine[] = [{ ...line(data?.name ?? '', 3), bold: true }];
    lines.push(line(formatC(tooltip.info[0], rank, level, node.maxLevel), 0));
    const add = (text: string, color: number) => { for (const part of divide(text)) lines.push(line(part, color)); };
    add(node.defaultValue === -1 ? formatC(tooltip.info[1]) : formatC(tooltip.info[1], level !== 0 ? info?.value ?? 0 : node.defaultValue), 0);
    if (level !== 0 && level < node.maxLevel) {
      add(T.NextLevel, 4);
      add(formatC(tooltip.info[1], info?.nextValue ?? 0), 0);
    }
    if (level < node.maxLevel) {
      add(T.Requirements, 1);
      add(formatC(tooltip.info[2], node.requiredPoints), node.requiredPoints <= master.points ? 0 : 2);
    }
    add(formatC(tooltip.info[3]), master.checkBeforeSkill(node.skill, level) ? 0 : 2);
    if (level < node.maxLevel && rank !== 1) {
      add(formatC(tooltip.info[4]), master.checkRankPoint(node.group, rank, level) ? 0 : 2);
      node.requires.forEach((required, i) => {
        if (required < MASTER_SKILL_BEGIN || required > MASTER_SKILL_END) return;
        // The desktop compares the required skill's value (not its level) with 10.
        add(formatC(tooltip.info[5 + i]), (master.skills.get(required)?.value ?? 0) < 10 ? 2 : 0);
      });
    }
    return lines;
  }

  // The close button's hint (data-hint).
  private showButtonHint(event: MouseEvent): void {
    const target = event.target instanceof Element ? event.target.closest('[data-hint]') : null;
    const text = target?.getAttribute('data-hint');
    if (!target || !text) return;
    const box = target.getBoundingClientRect();
    void this.tooltip.showLines([line(text, 0)], { left: box.left, top: box.top, width: box.width, height: box.height }, target);
  }
}

function line(text: string, color: number): TooltipLine {
  return { text, color: COLORS[color] ?? 'white', bold: false, background: null, spacer: false };
}

export interface MasterLevelSetup {
  connection: Connection;
  /** The server's class number (CharacterListEntry.classNumber) and the base class (0-6). */
  classNumber: number;
  classIndex: number;
  stats: CharacterStats;
  skills: SkillList;
  inventory: Inventory;
  hud: Hud;
  characterWindow: CharacterWindow;
  /** Packets that came before this was set up (the master stats and skill list come on entering). */
  earlyPackets?: Uint8Array[];
  click?: () => void;
  closeOthers?: () => void;
}

/** Wires the master level into the game: its packets, the tree window (A, the character window's button), the HUD's experience bar. */
export function setupMasterLevel(setup: MasterLevelSetup): { master: MasterLevel; window: MasterLevelWindow } {
  const { connection, skills, inventory, stats, hud } = setup;
  const master = new MasterLevel(connection, setup.classNumber, {
    knowsSkill: (skill) => [...skills.skills.values()].includes(skill),
    // ITEM::Special of the hands: the skill a weapon with the skill option brings.
    weaponSkills: () => [0, 1].flatMap((slot) => {
      const item = inventory.items.get(slot);
      const skill = item?.skill ? itemDefinition(item.group, item.number)?.skill : undefined;
      return typeof skill === 'number' && skill > 0 ? [skill] : [];
    }),
  }, (skill, level) => skills.learnMasterSkill(skill, level));
  for (const bytes of setup.earlyPackets ?? []) master.handle(bytes);
  const window = new MasterLevelWindow({
    connection, master, classIndex: setup.classIndex, click: setup.click, closeOthers: setup.closeOthers,
  });
  skills.onChange(() => { if (window.isOpen) window.refresh(); });
  inventory.onChange(() => { if (window.isOpen) window.refresh(); });
  setup.characterWindow.setMasterLevel(() => master.isMaster, () => { setup.characterWindow.close(); void window.toggle(); });
  hud.setMasterExperience(() => (master.experienceActive(stats.level)
    ? { experience: master.experience, start: masterLevelStart(master.level), next: master.nextExperience } : null));
  master.onChange(() => hud.refresh());
  globalThis.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() !== 'a' || event.ctrlKey || event.altKey || event.metaKey) return;
    event.preventDefault();
    if (master.isMaster) void window.toggle();
    setup.click?.();
  });
  return { master, window };
}
