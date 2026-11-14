// The character window (character_info.rml), as the desktop client's CCharacterInfoWindow
// (UI/Character/CharacterInfoWindow.cpp): the name, class and server, the level, experience and points to spend,
// the five stats with the lines below them (damage and rates, defense, speed, life, mana, wizardry, curse and
// skill damage, by class; game/characterCalc.ts works the numbers out), the stats' "+" buttons that spend a point
// (IncreaseCharacterStatPoint; game/stats.ts follows the server's answer), the hints of the damage and critical
// damage rate lines, and the close buttons.

import { openDocument, prepareDocument, REFERENCE_HEIGHT } from './rml.js';
import type { RmlDocument, RmlModel } from './rml.js';
import { ItemTooltipView } from './itemTooltipView.js';
import type { Connection } from '../net/connection.js';
import type { CharacterStats } from '../game/stats.js';
import { EQUIPMENT_SLOTS, type Inventory } from '../game/inventory.js';
import { itemCombatValues, loadTooltipData, type TooltipLine } from '../game/itemTooltip.js';
import { loadSkillData, skillInfo } from '../game/skills.js';
import { BaseClass, characterSheet, type CharacterSheet, type HeroStats, type LineSource, type WornItem } from '../game/characterCalc.js';
import { nativeTextPx } from './nativeText.js';

const PANEL_WIDTH = 190; // CHAINFO_WINDOW_WIDTH
const PVP_NEUTRAL = 3;   // CHARACTER::PK of a player who killed no one
const REFRESH_MS = 100;  // the desktop syncs every frame; the class/server crossfade needs a steady tick

// GetCharacterClassText, by base class and class step (1 to 3).
const CLASS_NAMES: string[][] = [
  ['Dark Wizard', 'Soul Master', 'Grand Master'],
  ['Dark Knight', 'Blade Knight', 'Blade Master'],
  ['Elf', 'Muse Elf', 'High Elf'],
  ['Magic Gladiator', 'Magic Gladiator', 'Dual Master'],
  ['Dark Lord', 'Dark Lord', 'Lord Emperor'],
  ['Summoner', 'Bloody Summoner', 'Dimension Master'],
  ['Rage Fighter', 'Rage Fighter', 'Fist Master'],
];

// The desktop's texts (Localization/Game.en.resx), by their accessor names.
const T = {
  STR: 'STR', AGI: 'AGI', STA: 'STA', ENG: 'ENG', Command: 'Command',
  LevelUResetsU: 'Level: %d | Resets: %d', EXPI64dI64d: 'EXP:%d / %d', PointD: 'Point: %d', DD1907: '[+]:%d%%|[-]:%d%%',
  StatPointsAdded: 'Added', StatPointsRemoved: 'Removed',
  DmgRateDDD: 'Dmg(rate): %d~%d (%d)', DmgDD: 'Dmg: %d~%d', AttackRateDD: 'Attack rate: %d (+%d)', AttackRateD: 'Attack rate: %d',
  DefenseRateDDD: 'Defense (rate):%d (%d +%d)', DefenseRateDD: 'Defense (rate):%d (%d)', DefenseDD: 'Defense: %d (+%d)',
  DefenseD: 'Defense: %d', AttackSpeedD: 'Attack speed: %d', DefenseRateDD2110: 'Defense rate: %d (+%d)',
  DefenseRateD: 'Defense rate: %d', HPDD: 'HP: %d / %d', MeleeDamageD: 'Melee Damage: %d%%', ManaDD: 'Mana: %d / %d',
  WizardryDmgDDD: 'Wizardry Dmg: %d~%d (+%d)', WizardryDmgDD216: 'Wizardry Dmg: %d~%d',
  CurseSpellDDD: ' Curse Spell: %d ~ %d(+%d)', CurseSpellDD1694: ' Curse Spell: %d ~ %d', SkillDamageD: 'Skill Damage: %d%%',
  DivineDamageRoarSlasherD: 'Divine Damage (Roar, Slasher): %d%%', AOEDamageDarkSideD: 'AOE Damage (Dark Side): %d%%',
  CriticalDamageRateD: 'Critical damage rate: %d%%', CriticalDamageRate: 'Critical damage rate', LuckDD: 'Luck (×%d): %d%%',
  TotalD: 'Total: %d%%', Damage: 'Damage', NormalHitDD: 'Normal hit: %d~%d', CriticalHitD: 'Critical hit: %d',
  ExcellentHitD: 'Excellent hit: %d', BeforeTheTargetSDefense: "before the target's defense", CriticalDamage: 'Critical damage',
  CriticalDamageFromHarmonyOptionsD: 'Critical damage from harmony options: +%d', CriticalDamageTotalD: 'Critical damage total: +%d',
  CloseS: 'Close (%s)', Quest: 'Quest', Pet: 'Pet', MasterSkillTreeA: 'Master skill tree (A)',
};

function format(text: string, ...values: number[]): string {
  let index = 0;
  return text.replace(/%%|%d/g, (token) => (token === '%%' ? '%' : String(values[index++] ?? 0)));
}

// The hint a line shows while the pointer is over it (LineHint).
const NO_HINT = -1, DAMAGE_HINT = 0, CRITICAL_DAMAGE_RATE_HINT = 1;

interface StatLine { text: string; source: LineSource; hint: number }
// RmlUi's arrays have a size; the RML reads vit_lines.size.
type StatLines = StatLine[] & { size: number };
const lines = (list: StatLine[]): StatLines => Object.assign(list, { size: list.length });
const statLine = (text: string, source: LineSource = 'base', hint = NO_HINT): StatLine => ({ text, source, hint });

/** character_info.rml's data model (CharacterInfoRmlModel). */
export interface CharacterInfoModel extends RmlModel {
  text_px: number;
  can_level_up: boolean;
  show_charisma: boolean;
  name_text: string;
  pk_level: number;
  classname_text: string;
  servername_text: string;
  classname_opacity: number;
  servername_opacity: number;
  level_text: string;
  levelup_point_text: string;
  exp_text: string;
  point_probability_text: string;
  point_text: string;
  str_label: string; agi_label: string; vit_label: string; ene_label: string; cmd_label: string;
  str_value_text: string; str_value_source: string;
  agi_value_text: string; agi_value_source: string;
  vit_value_text: string; vit_value_source: string;
  ene_value_text: string; ene_value_source: string;
  cmd_value_text: string; cmd_value_source: string;
  str_lines: StatLines; agi_lines: StatLines; vit_lines: StatLines; ene_lines: StatLines;
  critical_damage_rate_line: StatLine;
  master_level_enabled: boolean;
  exit_tooltip: string; quest_tooltip: string; pet_tooltip: string; masterlevel_tooltip: string;
  chainfo_increase_stat: (stat: number) => void;
  window_close: () => void;
  chainfo_click_quest: () => void;
  chainfo_click_pet: () => void;
  chainfo_click_masterlevel: () => void;
  chainfo_line_hover: (hint: number) => void;
  chainfo_line_leave: () => void;
}

// The fruit points' chance texts (BuildTableTexts): 100% up to 10 points, then less the more were used.
function fruitChance(used: number, maximum: number): number {
  if (used <= 10) return 100;
  const share = maximum !== 0 ? Math.trunc(used * 100 / maximum) : 0;
  return share <= 10 ? 70 : share <= 30 ? 60 : share <= 50 ? 50 : 40;
}

export interface CharacterWindowOptions {
  connection: Connection;
  stats: CharacterStats;
  inventory: Inventory;
  /** 0 Dark Wizard … 6 Rage Fighter. */
  classIndex: number;
  name: string;
  parent?: HTMLElement;
  /** The class step, 1 to 3 (the second and third class); 1 when left out. */
  classLevel?: number;
  /** The server's name, which crossfades with the class name; none: the class name stays. */
  serverName?: string;
  /** The hero's outlaw level (CHARACTER::PK, the character information's HeroState); 3 (neutral) when left out. */
  pkLevel?: () => number;
  /** The skill number of the current skill (CharacterAttribute->Skill[Hero->CurrentSkill]), for the wizardry line. */
  currentSkill?: () => number;
  /** Reference px between the screen's right edge and the window (0: docked at the right edge). */
  dockRight?: () => number;
  /** The Quest, Pet and Master Skill Tree buttons (the desktop opens those windows). */
  onQuest?: () => void;
  onPet?: () => void;
  onMasterLevel?: () => void;
  /** Called after the window opened or closed, so other docked windows can make room. */
  onToggle?: (open: boolean) => void;
}

export class CharacterWindow {
  private readonly options: CharacterWindowOptions;
  private readonly parent: HTMLElement;
  private readonly tooltip: ItemTooltipView;
  private readonly model: CharacterInfoModel;
  private doc: RmlDocument | null = null;
  private panel: HTMLElement | null = null;
  private opening: Promise<void> | null = null;
  private timer: number | null = null;
  private stops: (() => unknown)[] = [];
  private sheet: CharacterSheet | null = null;
  private hovered = NO_HINT;
  private hoverTarget: Element | null = null;
  private masterLevelEnabled: () => boolean = () => false;

  constructor(options: CharacterWindowOptions) {
    this.options = options;
    this.parent = options.parent ?? document.body;
    this.tooltip = new ItemTooltipView(this.parent, () => null);
    const empty = lines([]);
    this.model = {
      text_px: 12, can_level_up: false, show_charisma: options.classIndex === BaseClass.DARK_LORD,
      name_text: options.name, pk_level: PVP_NEUTRAL, classname_text: '', servername_text: options.serverName ?? '',
      classname_opacity: 1, servername_opacity: 0,
      level_text: '', levelup_point_text: '', exp_text: '', point_probability_text: '', point_text: '',
      str_label: T.STR, agi_label: T.AGI, vit_label: T.STA, ene_label: T.ENG, cmd_label: T.Command,
      str_value_text: '', str_value_source: 'base', agi_value_text: '', agi_value_source: 'base',
      vit_value_text: '', vit_value_source: 'base', ene_value_text: '', ene_value_source: 'base',
      cmd_value_text: '', cmd_value_source: 'base',
      str_lines: empty, agi_lines: lines([]), vit_lines: lines([]), ene_lines: lines([]),
      critical_damage_rate_line: statLine('', 'base', CRITICAL_DAMAGE_RATE_HINT),
      master_level_enabled: false,
      exit_tooltip: format(T.CloseS).replace('%s', 'C'), quest_tooltip: `${T.Quest}(T)`, pet_tooltip: T.Pet,
      masterlevel_tooltip: T.MasterSkillTreeA,
      chainfo_increase_stat: (stat) => this.increaseStat(stat),
      window_close: () => this.close(),
      chainfo_click_quest: () => this.options.onQuest?.(),
      chainfo_click_pet: () => this.options.onPet?.(),
      chainfo_click_masterlevel: () => { if (this.model.master_level_enabled) this.options.onMasterLevel?.(); },
      chainfo_line_hover: (hint) => { this.hovered = hint; this.showHint(); },
      chainfo_line_leave: () => { this.hovered = NO_HINT; this.tooltip.hide(); },
    };
    // Get the document and data ready in the background, so it opens at once.
    prepareDocument('character_info').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) this.close();
    });
    window.addEventListener('resize', () => this.place());
  }

  /** The Master Skill Tree button: lit for a master class (IsMasterLevel), opens the tree (RmlClickMasterLevel). */
  setMasterLevel(enabled: () => boolean, open: () => void): void {
    this.masterLevelEnabled = enabled;
    this.options.onMasterLevel = open;
    if (this.doc) this.refresh();
  }

  get isOpen(): boolean {
    return this.doc !== null;
  }

  async toggle(): Promise<void> {
    if (this.isOpen) this.close();
    else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.openDocument().finally(() => { this.opening = null; });
    await this.opening;
  }

  private async openDocument(): Promise<void> {
    await Promise.all([prepareDocument('character_info'), loadTooltipData(), loadSkillData()]);
    if (this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.refresh(false);
    const doc = await openDocument('character_info', this.model, { parent: this.parent, scale });
    this.doc = doc;
    const panel = doc.$('#panel');
    this.panel = panel;
    if (panel) {
      // The theme leaves pointer events to the game (the desktop does its hit tests in C++).
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
      panel.addEventListener('mouseover', (event) => { this.hoverTarget = event.target instanceof Element ? event.target : null; }, true);
      panel.addEventListener('mouseover', (event) => this.showButtonHint(event));
      panel.addEventListener('mouseout', (event) => {
        if (event.target instanceof Element && event.target.closest('[data-hint]')) this.tooltip.hide();
      });
    }
    const { stats, inventory } = this.options;
    this.stops = [stats.onChange(() => this.refresh()), inventory.onChange(() => this.refresh())];
    this.timer = window.setInterval(() => this.refresh(), REFRESH_MS);
    this.place();
    this.options.onToggle?.(true);
  }

  close(): void {
    if (!this.doc) return;
    this.tooltip.hide();
    if (this.timer !== null) window.clearInterval(this.timer);
    this.timer = null;
    for (const stop of this.stops) stop();
    this.stops = [];
    this.doc.close();
    this.doc = null;
    this.panel = null;
    this.hovered = NO_HINT;
    this.options.onToggle?.(false);
  }

  /** Docks the window at the right edge (the workspace's dock-right column, where it is the first window). */
  place(): void {
    if (!this.doc || !this.panel) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    const right = this.options.dockRight?.() ?? 0;
    Object.assign(this.panel.style, {
      left: `${window.innerWidth - (right + PANEL_WIDTH) * scale}px`, top: '0px', transform: `scale(${scale})`,
      transformOrigin: 'left top',
    });
    this.doc.setScale(scale);
    this.model.text_px = nativeTextPx();
    this.doc.update();
  }

  // RmlClickIncreaseStat: one point to a stat (the Dark Lord's command too), while there are points to spend.
  private increaseStat(stat: number): void {
    if (this.options.stats.levelUpPoints <= 0) return;
    const count = this.options.classIndex === BaseClass.DARK_LORD ? 5 : 4;
    if (!Number.isInteger(stat) || stat < 0 || stat >= count) return;
    this.options.connection.send('ClientToServer', 'IncreaseCharacterStatPoint', { StatType: stat });
  }

  private hero(): HeroStats {
    const { stats, classIndex } = this.options;
    return {
      classIndex, classLevel: this.options.classLevel ?? 1, level: stats.level, strength: stats.strength,
      agility: stats.agility, vitality: stats.vitality, energy: stats.energy, leadership: stats.leadership,
    };
  }

  private equipment(level: number): (WornItem | null)[] {
    const worn: (WornItem | null)[] = [];
    for (let slot = 0; slot < EQUIPMENT_SLOTS; slot++) {
      const item = this.options.inventory.items.get(slot);
      worn.push(item ? {
        level: item.level, durability: item.durability, excellent: item.excellent, ancient: item.ancientSet,
        values: itemCombatValues(item, level),
      } : null);
    }
    return worn;
  }

  // SyncRmlModel: the texts and lines from the stats and the worn items.
  private refresh(update = true): void {
    const { stats, classIndex } = this.options;
    const model = this.model;
    const hero = this.hero();
    const skillNumber = this.options.currentSkill?.() ?? 0;
    const skill = skillNumber > 0 ? { number: skillNumber, damage: skillInfo(skillNumber)?.damage ?? 0 } : null;
    const sheet = characterSheet(hero, this.equipment(hero.level), skill);
    this.sheet = sheet;

    model.can_level_up = stats.levelUpPoints > 0;
    model.master_level_enabled = this.masterLevelEnabled();
    model.pk_level = this.options.pkLevel?.() ?? PVP_NEUTRAL;
    const names = CLASS_NAMES[classIndex] ?? CLASS_NAMES[0];
    model.classname_text = `(${names[Math.min(Math.max(this.options.classLevel ?? 1, 1), 3) - 1]})`;
    if (this.options.serverName) {
      // RenderSubjectTexts' sine-wave blend of the class name and the server name.
      const alpha = Math.sin(performance.now() * 0.001) + 1;
      model.classname_opacity = (127 * (2 - alpha)) / 255;
      model.servername_opacity = (127 * alpha) / 255;
    }

    // BuildTableTexts.
    model.level_text = format(T.LevelUResetsU, stats.level, stats.resets);
    model.levelup_point_text = stats.levelUpPoints > 0 ? format(T.PointD, stats.levelUpPoints) : '';
    model.exp_text = format(T.EXPI64dI64d, stats.experience, stats.nextExperience);
    model.point_probability_text = format(T.DD1907, fruitChance(stats.usedFruitPoints, stats.maxFruitPoints),
      fruitChance(stats.usedNegativeFruitPoints, stats.maxNegativeFruitPoints));
    const fruits = stats.level > 9
      ? [stats.usedFruitPoints, stats.maxFruitPoints, stats.usedNegativeFruitPoints === 0 ? 0 : -stats.usedNegativeFruitPoints,
        -stats.maxNegativeFruitPoints]
      : [0, 0, 0, 0];
    model.point_text = `${T.StatPointsAdded} ${fruits[0]}/${fruits[1]} | ${T.StatPointsRemoved} ${fruits[2]}/${fruits[3]}`;

    // BuildAttributeLines.
    model.str_value_text = String(stats.strength);
    model.agi_value_text = String(stats.agility);
    model.vit_value_text = String(stats.vitality);
    model.ene_value_text = String(stats.energy);
    model.cmd_value_text = String(sheet.charisma);
    model.cmd_value_source = sheet.addCharisma !== 0 ? 'boosted' : 'base';

    const { damage } = sheet;
    const strLines = [statLine(damage.rating > 0 ? format(T.DmgRateDDD, damage.min, damage.max, damage.rating)
      : format(T.DmgDD, damage.min, damage.max), damage.source, DAMAGE_HINT)];
    if (sheet.attackRatingPK > 0) {
      strLines.push(statLine(sheet.attackRatingPKBonus !== 0 ? format(T.AttackRateDD, sheet.attackRatingPK, sheet.attackRatingPKBonus)
        : format(T.AttackRateD, sheet.attackRatingPK), damage.source));
    }
    model.str_lines = lines(strLines);

    const { defense } = sheet;
    let defenseText: string;
    if (defense.fullSet) {
      defenseText = defense.rate > 0 ? format(T.DefenseRateDDD, defense.value, defense.rate, defense.fullSetRate)
        : format(T.DefenseDD, defense.value, defense.fullSetBase);
    } else {
      defenseText = defense.rate > 0 ? format(T.DefenseRateDD, defense.value, defense.rate) : format(T.DefenseD, defense.value);
    }
    const wizardSpeed = classIndex === BaseClass.WIZARD || classIndex === BaseClass.SUMMONER;
    model.agi_lines = lines([
      statLine(defenseText),
      statLine(format(T.AttackSpeedD, wizardSpeed ? stats.magicSpeed : stats.attackSpeed), sheet.attackSpeedSource),
      statLine(sheet.defenseRatePKBonus !== 0 ? format(T.DefenseRateDD2110, sheet.defenseRatePK, sheet.defenseRatePKBonus)
        : format(T.DefenseRateD, sheet.defenseRatePK)),
    ]);

    const vitLines = [statLine(format(T.HPDD, stats.life, stats.maxLife), sheet.lifeSource)];
    if (sheet.meleeDamage !== null) vitLines.push(statLine(format(T.MeleeDamageD, sheet.meleeDamage)));
    model.vit_lines = lines(vitLines);

    const eneLines = [statLine(format(T.ManaDD, stats.mana, stats.maxMana))];
    if (sheet.magic) {
      const { min, max, staffBonus } = sheet.magic;
      eneLines.push(statLine(staffBonus !== null ? format(T.WizardryDmgDDD, min, max, staffBonus) : format(T.WizardryDmgDD216, min, max)));
    }
    if (sheet.curse) {
      const { min, max, bookBonus } = sheet.curse;
      eneLines.push(statLine(bookBonus !== null ? format(T.CurseSpellDDD, min, max, bookBonus) : format(T.CurseSpellDD1694, min, max)));
    }
    if (sheet.skillDamage !== null) eneLines.push(statLine(format(T.SkillDamageD, sheet.skillDamage)));
    if (sheet.divineDamage !== null) eneLines.push(statLine(format(T.DivineDamageRoarSlasherD, sheet.divineDamage)));
    if (sheet.areaDamage !== null) eneLines.push(statLine(format(T.AOEDamageDarkSideD, sheet.areaDamage)));
    model.ene_lines = lines(eneLines);

    // BuildCriticalHits: the critical damage rate line under the strength's lines.
    const rate = sheet.criticalRate.luck;
    model.critical_damage_rate_line = statLine(format(T.CriticalDamageRateD, rate), rate > 0 ? 'boosted' : 'base',
      CRITICAL_DAMAGE_RATE_HINT);

    if (update && this.doc) {
      this.doc.update();
      if (this.hovered !== NO_HINT) this.showHint();
    }
  }

  // The hint of the line under the pointer (BuildDamageHint, BuildCriticalDamageRateHint).
  private showHint(): void {
    const sheet = this.sheet;
    const target = this.hoverTarget;
    if (!sheet || !target || this.hovered === NO_HINT) return;
    const row = (text: string, color = 'white', bold = false): TooltipLine => ({ text, color, bold, background: null, spacer: false });
    const hint: TooltipLine[] = [];
    if (this.hovered === DAMAGE_HINT) {
      const { minimum, maximum } = sheet.damageRange;
      hint.push(row(T.Damage, 'yellow', true), row(format(T.NormalHitDD, minimum, maximum)),
        row(format(T.CriticalHitD, sheet.criticalHit)), row(format(T.ExcellentHitD, sheet.excellentHit)),
        row(T.BeforeTheTargetSDefense, 'gray'));
      if (sheet.criticalDamage.harmony > 0) {
        hint.push({ ...row(''), spacer: true }, row(T.CriticalDamage, 'yellow', true),
          row(format(T.CriticalDamageFromHarmonyOptionsD, sheet.criticalDamage.harmony)),
          row(format(T.CriticalDamageTotalD, sheet.criticalDamage.harmony), 'white', true));
      }
    } else {
      const { luck, luckItems } = sheet.criticalRate;
      hint.push(row(T.CriticalDamageRate, 'yellow', true));
      if (luck > 0) hint.push(row(format(T.LuckDD, luckItems, luck)));
      hint.push(row(format(T.TotalD, luck), 'white', true));
    }
    const box = target.getBoundingClientRect();
    void this.tooltip.showLines(hint, { left: box.left, top: box.top, width: box.width, height: box.height }, this.model);
  }

  // The buttons' hints (data-hint: close, quest, pet, master skill tree).
  private showButtonHint(event: MouseEvent): void {
    const target = event.target instanceof Element ? event.target.closest('[data-hint]') : null;
    const text = target?.getAttribute('data-hint');
    if (!target || !text) return;
    const box = target.getBoundingClientRect();
    void this.tooltip.showLines([{ text, color: 'white', bold: false, background: null, spacer: false }],
      { left: box.left, top: box.top, width: box.width, height: box.height }, target);
  }
}
