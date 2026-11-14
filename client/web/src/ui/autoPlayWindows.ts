// The auto play settings windows, as the desktop client's helper windows: the settings window
// (mu_helper_config.rml, CMuHelperConfigWindow) with its three tabs, the detail panel beside it
// (mu_helper_detail.rml, CMuHelperDetailWindow: a skill's activation condition, the recovery
// thresholds, party support) and the skill picker flyout (mu_helper_skill_picker.rml,
// CMuHelperSkillPicker).
//
// All three edit one staged copy of the settings (the desktop's StagedConfig()), kept between
// openings and sent on Save. As on the desktop, the checkboxes, the range steppers, the radios and
// the extra-item list write the staged copy at once; the six skill slots and the number fields are
// held by the settings window and written by its Save; the detail panel's gauges and buff interval
// are held by the panel and written by its own Save.

import { openDocument, prepareDocument, REFERENCE_HEIGHT } from './rml.js';
import type { RmlDocument, RmlModel } from './rml.js';
import { loadSkillData, skillIconSprite, skillInfo, SkillNumber, SkillUseType } from '../game/skills.js';
import type { SkillList } from '../game/skills.js';
import { BaseClass } from '../game/characterCalc.js';
import {
  cloneAutoPlayConfig, initialAutoPlayConfig, normalizeExtraItems, PetAttackMode, SkillActivation,
} from '../game/autoPlayConfig.js';
import type { AutoPlayConfig } from '../game/autoPlayConfig.js';
import { nativeTextPx } from './nativeText.js';

const PANEL_WIDTH = 190; // the docked panels' width in reference px
const SKILL_SLOTS = 6; // attack skills 0-2, buffs 3-5
const MAX_HUNTING_RANGE = 6;
const MAX_OBTAINING_RANGE = 8;
const MAX_NUMBER_DIGITS = 3; // UI::MuHelper::MaxNumberDigits
const MAX_ITEM_NAME = 50;
const MAX_MAGIC = 64; // the learned skill list's length (CharacterAttribute->Skill)
const GAUGE_MAX_LEVEL = 10;
const GAUGE_WIDTH = 124; // .mh-gauge's width in reference px
const GAUGE_ORIGIN_INSET = 1;

// The detail panel's pages (EMuHelperDetailPage).
const Page = {
  NONE: -1, SKILL2: 2, SKILL3: 3, POTION_ELF: 4, POTION_SUMMONER: 5, POTION: 6, PARTY: 7, PARTY_ELF: 8,
} as const;

const isSkillPage = (page: number) => page === Page.SKILL2 || page === Page.SKILL3;
const isPotionPage = (page: number) => page === Page.POTION || page === Page.POTION_ELF || page === Page.POTION_SUMMONER;
const isPartyPage = (page: number) => page === Page.PARTY || page === Page.PARTY_ELF;
// The skillConditions entry a skill page edits: 1 for the second skill, 2 for the third.
const conditionIndexOf = (page: number) => (page === Page.SKILL2 ? 1 : 2);

const PRE_CONDITIONS = SkillActivation.ON_MOBS_NEARBY | SkillActivation.ON_MOBS_ATTACKING;
const SUB_CONDITION_BITS = [
  SkillActivation.ON_MORE_THAN_TWO_MOBS, SkillActivation.ON_MORE_THAN_THREE_MOBS,
  SkillActivation.ON_MORE_THAN_FOUR_MOBS, SkillActivation.ON_MORE_THAN_FIVE_MOBS,
] as const;
const SUB_CONDITIONS = SUB_CONDITION_BITS.reduce((all, bits) => all | bits, 0);

// Skill numbers the picker sorts (_enum.h): buffs, heals and the elf's defense.
const BUFF_SKILLS = new Set([
  48, 356, 360, // swell life
  77, 441, // infinity arrow
  27, 417, 423, // defense
  28, 420, 422, // attack
  16, 403, 404, // soul barrier
  SkillNumber.EXPANSION_OF_WIZARDRY, 380, 383,
  64, 511, 515, 517, // add critical
  SkillNumber.ALICE_BERSERKER, 469, SkillNumber.ALICE_THORNS,
  266, 267, 268, 573, 572, 569, // the rage fighter's party buffs
]);
const HEALING_SKILLS = new Set([26, 413, SkillNumber.ALICE_DRAINLIFE, 458]);
const DEFENSE_SKILLS = new Set([27, 417]);
const isBuffSkill = (skill: number) => BUFF_SKILLS.has(skill);
const isAttackSkill = (skill: number) => !isBuffSkill(skill) && !DEFENSE_SKILLS.has(skill) && !HEALING_SKILLS.has(skill);

/** Which of the settings window's class controls a class sees (UI::MuHelper::ResolveClassFeatures). */
export interface AutoPlayClassFeatures {
  skill3: boolean; combo: boolean; pet: boolean; party: boolean; autoHeal: boolean; drainLife: boolean;
  potionSummoner: boolean; potionPage: number; partyPage: number;
}

export function autoPlayClassFeatures(classIndex: number): AutoPlayClassFeatures {
  const f: AutoPlayClassFeatures = {
    skill3: false, combo: false, pet: false, party: false, autoHeal: false, drainLife: false,
    potionSummoner: false, potionPage: Page.POTION, partyPage: Page.NONE,
  };
  switch (classIndex) {
    case BaseClass.KNIGHT: f.skill3 = true; f.combo = true; break;
    case BaseClass.WIZARD: f.skill3 = true; f.party = true; f.partyPage = Page.PARTY; break;
    case BaseClass.ELF:
      f.skill3 = true; f.party = true; f.partyPage = Page.PARTY_ELF; f.autoHeal = true; f.potionPage = Page.POTION_ELF;
      break;
    case BaseClass.DARK: case BaseClass.RAGE_FIGHTER: f.skill3 = true; break;
    case BaseClass.DARK_LORD: f.pet = true; break;
    case BaseClass.SUMMONER:
      f.skill3 = true; f.drainLife = true; f.potionSummoner = true; f.potionPage = Page.POTION_SUMMONER;
      break;
    default: break;
  }
  return f;
}

// A skill condition needs a pre-condition and a sub-condition, or the skill never fires
// (UI::MuHelper::EnsureConditionDefaults).
function ensureConditionDefaults(bits: number): number {
  let result = bits;
  if ((result & PRE_CONDITIONS) === 0) result |= SkillActivation.ON_MOBS_NEARBY;
  if ((result & SUB_CONDITIONS) === 0) result |= SkillActivation.ON_MORE_THAN_TWO_MOBS;
  return result;
}

// Timer and condition exclude each other; either click clears the other first.
function setConditionMode(bits: number, timer: boolean, enable: boolean): number {
  const cleared = bits & ~(timer ? SkillActivation.ON_CONDITION : SkillActivation.ON_TIMER);
  const bit = timer ? SkillActivation.ON_TIMER : SkillActivation.ON_CONDITION;
  return enable ? cleared | bit : cleared & ~bit;
}

// A number field as the desktop reads it: digits only (a paste skips the filter), then the whole
// string as a number, an empty one as 0.
function parseNumberField(text: string): number {
  const digits = text.replace(/\D/g, '');
  return digits ? Number.parseInt(digits, 10) : 0;
}

// A skill's icon as a decorator value; always the lit icon, as the settings show a choice.
function skillDecorator(skill: number): string {
  if (skill <= 0) return 'none';
  const sprite = skillIconSprite(skill, true);
  return sprite ? `image(${sprite})` : 'none';
}

const uiScale = () => window.innerHeight / REFERENCE_HEIGHT;
const textPx = (_scale: number) => nativeTextPx();

interface ConfigLabels {
  title: string; tab_hunting: string; tab_obtaining: string; tab_other: string;
  range: string; distance: string; basic_skill: string; activation_skill1: string; activation_skill2: string; seconds: string;
  extension_used: string; extension_none: string;
  potion: string; long_distance: string; orig_position: string; delay: string; condition: string; combo: string;
  fallback: string; buff_duration: string; use_pet: string; party: string; auto_heal: string; drain_life: string;
  raven_cease: string; raven_auto: string; raven_together: string;
  repair: string; pick_all: string; pick_selected: string; pick_jewel: string; pick_ancient: string; pick_zen: string;
  pick_excellent: string; pick_extra: string; auto_friend: string; auto_guild: string; auto_defend: string;
  setting: string; add: string; remove: string; save: string; init: string; close_tip: string;
}

interface ConfigChecks {
  potion: boolean; long_distance: boolean; orig_position: boolean;
  skill2_delay: boolean; skill2_condition: boolean; skill3_delay: boolean; skill3_condition: boolean;
  combo: boolean; fallback: boolean; buff_duration: boolean; use_pet: boolean; party: boolean;
  auto_heal: boolean; drain_life: boolean; repair: boolean; pick_all: boolean; pick_selected: boolean;
  pick_jewel: boolean; pick_ancient: boolean; pick_zen: boolean; pick_excellent: boolean; pick_extra: boolean;
  auto_friend: boolean; auto_guild: boolean; auto_defend: boolean;
}

interface ExtraItemRow { name: string; selected: boolean; index: number }

/** mu_helper_config.rml's data model (MuHelperConfigRmlModel). */
interface ConfigModel extends RmlModel {
  text_px: number;
  active_tab: number;
  hunt_range: number;
  pick_range: number;
  raven_mode: number;
  features: { skill3: boolean; combo: boolean; pet: boolean; party: boolean; auto_heal: boolean; drain_life: boolean; potion_summoner: boolean };
  checks: ConfigChecks;
  slots: { s0: string; s1: string; s2: string; s3: string; s4: string; s5: string };
  distance_time: string;
  skill2_delay: string;
  skill3_delay: string;
  item_name: string;
  extra_items: ExtraItemRow[];
  labels: ConfigLabels;
  muhelper_tab: (tab: number) => void;
  muhelper_toggle: (key: string) => void;
  muhelper_raven_mode: (mode: number) => void;
  muhelper_button: (key: string) => void;
  muhelper_slot: (slot: number) => void;
  muhelper_slot_clear: (slot: number) => void;
  muhelper_extra_select: (index: number) => void;
}

interface DetailLabels {
  title_activation: string; title_recovery: string; title_party: string;
  pane_precon: string; pane_subcon: string; pane_auto_potion: string; pane_auto_heal: string; pane_drain_life: string;
  pane_buff_support: string; pane_heal_support: string; hp_status: string; hp_status_party: string;
  precon_hunt_range: string; precon_attacking: string;
  subcon_two: string; subcon_three: string; subcon_four: string; subcon_five: string;
  party_heal: string; party_duration: string; time_space: string; save: string; init: string; close_tip: string;
}

/** mu_helper_detail.rml's data model (MuHelperDetailRmlModel). */
interface DetailModel extends RmlModel {
  text_px: number;
  page: number;
  precon: number;
  subcon: number;
  potion_level: number;
  heal_level: number;
  party_heal_level: number;
  party_heal: boolean;
  party_duration: boolean;
  buff_interval: string;
  labels: DetailLabels;
  muhelper_detail_precon: (index: number) => void;
  muhelper_detail_subcon: (index: number) => void;
  muhelper_detail_gauge: (gauge: number) => void;
  muhelper_detail_party_heal: () => void;
  muhelper_detail_party_duration: () => void;
  muhelper_detail_save: () => void;
  muhelper_detail_init: () => void;
  muhelper_detail_close: () => void;
}

/** mu_helper_skill_picker.rml's data model (MuHelperSkillPickerRmlModel). */
interface PickerModel extends RmlModel {
  origin_x: number;
  origin_y: number;
  scale: number;
  attack: boolean;
  entries: { decorator: string }[];
  mhsp_pick: (index: number) => void;
}

// The windows' texts (the desktop's English strings).
const CONFIG_LABELS: ConfigLabels = {
  title: 'Auto Play', tab_hunting: 'Hunting', tab_obtaining: 'Obtaining', tab_other: 'Other Settings',
  range: 'Range', distance: 'Distance', basic_skill: 'Basic Skill', activation_skill1: 'Activation Skill 1',
  activation_skill2: 'Activation Skill 2', seconds: 's',
  extension_used: 'Used Extension function', extension_none: 'No Extension Function Being Used',
  potion: 'Potion', long_distance: 'Long-Distance Counter Attack', orig_position: 'Original Position', delay: 'Delay',
  condition: 'Con', combo: 'Combo', fallback: 'Basic Attack Fallback', buff_duration: 'Buff Duration',
  use_pet: 'Use Dark Spirits', party: 'Party', auto_heal: 'Auto Heal', drain_life: 'Drain Life',
  raven_cease: 'Cease Attack', raven_auto: 'Auto Attack', raven_together: 'Attack Together',
  repair: 'Repair Item', pick_all: 'Pick All Near Items', pick_selected: 'Pick Selected Items', pick_jewel: 'Jewel/Gem',
  pick_ancient: 'Set Item', pick_zen: 'Zen', pick_excellent: 'Excellent Item', pick_extra: 'Add Extra Item',
  auto_friend: 'Auto accept - Friend', auto_guild: 'Auto accept - Guild Member', auto_defend: 'PVP Counterattack',
  setting: 'Setting', add: 'Add', remove: 'Delete', save: 'Save Setting', init: 'Initialization', close_tip: 'Close',
};

const DETAIL_LABELS: DetailLabels = {
  title_activation: 'Activation Skill', title_recovery: 'Auto Recovery', title_party: 'Party',
  pane_precon: 'Pre-con', pane_subcon: 'Sub-con', pane_auto_potion: 'Auto Potion', pane_auto_heal: 'Auto Heal',
  pane_drain_life: 'Drain Life', pane_buff_support: 'Buff Support', pane_heal_support: 'Heal Support',
  hp_status: 'HP Status', hp_status_party: 'HP Status of Party Members',
  precon_hunt_range: 'Monster Within Hunting range', precon_attacking: 'Monster Attacking Me',
  subcon_two: 'More Than 2 Mobs', subcon_three: 'More Than 3 Mobs', subcon_four: 'More than 4 mobs', subcon_five: 'More than 5 mobs',
  party_heal: 'Preference of Party Heal', party_duration: 'Buff Duration for All Party Members',
  time_space: 'Time Space of Casting Buff', save: 'Save Setting', init: 'Initialization', close_tip: 'Close',
};

const COMBO_NEEDS_SKILLS = 'In order to use Combo Skill, Basic Skill and Activation Skill should be registered first';

export interface AutoPlaySettingsOptions {
  parent?: HTMLElement;
  skills: SkillList;
  /** The character's base class (BaseClass). */
  classIndex: number;
  /** The settings in use (the server's copy, or the ones in place on entering the game). */
  config: () => AutoPlayConfig;
  /** Save Setting: the settings to use and to send to the server (CMuHelper::Save). */
  onSave: (config: AutoPlayConfig) => void;
  /** Initialization: the reset settings, which the desktop puts in use at once without saving (CMuHelper::Load). */
  onApply?: (config: AutoPlayConfig) => void;
  /** A message for the system log (the combo's "register the skills first"). */
  onMessage?: (text: string) => void;
  /** Reference px of panels docked at the right edge before the settings window. */
  dockRight?: () => number;
}

export class AutoPlaySettingsWindow {
  private readonly options: AutoPlaySettingsOptions;
  private readonly parent: HTMLElement;

  // The staged settings and where they came from (a new config() object is loaded on opening).
  private staged: AutoPlayConfig;
  private loadedFrom: AutoPlayConfig | null = null;

  // The settings window's own state (CMuHelperConfigWindow).
  private activeTab = 0;
  private selectedSlot = 0;
  private readonly selectedSkills: number[] = Array<number>(SKILL_SLOTS).fill(-1);
  private selectedExtraItem = '';
  private lastMouseButton = 0;

  // The detail panel's state (CMuHelperDetailWindow).
  private detailPage: number = Page.NONE;
  private detailVisible = false;
  private potionLevel = 0;
  private healLevel = 0;
  private partyHealLevel = 0;

  // The skill picker's state (CMuHelperSkillPicker).
  private pickerVisible = false;
  private pickerAttack = false;
  private pickerSkills: number[] = [];

  private readonly configModel: ConfigModel;
  private readonly detailModel: DetailModel;
  private readonly pickerModel: PickerModel;
  private configDoc: RmlDocument | null = null;
  private detailDoc: RmlDocument | null = null;
  private pickerDoc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private readonly stops: (() => void)[] = [];

  constructor(options: AutoPlaySettingsOptions) {
    this.options = options;
    this.parent = options.parent ?? document.body;
    this.staged = cloneAutoPlayConfig(initialAutoPlayConfig());
    const features = autoPlayClassFeatures(options.classIndex);
    this.configModel = {
      text_px: 12, active_tab: 0, hunt_range: 0, pick_range: 0, raven_mode: 0,
      features: {
        skill3: features.skill3, combo: features.combo, pet: features.pet, party: features.party,
        auto_heal: features.autoHeal, drain_life: features.drainLife, potion_summoner: features.potionSummoner,
      },
      checks: this.checks(),
      slots: { s0: 'none', s1: 'none', s2: 'none', s3: 'none', s4: 'none', s5: 'none' },
      distance_time: '0', skill2_delay: '0', skill3_delay: '0', item_name: '', extra_items: [],
      labels: CONFIG_LABELS,
      muhelper_tab: (tab) => this.selectTab(tab),
      muhelper_toggle: (key) => this.toggleCheck(key),
      muhelper_raven_mode: (mode) => { this.staged.darkRavenMode = Number.isInteger(mode) ? mode : PetAttackMode.CEASE; },
      muhelper_button: (key) => this.button(key),
      muhelper_slot: (slot) => this.slotClick(slot),
      // Mouseup comes for every button; the right one clears a slot.
      muhelper_slot_clear: (slot) => { if (this.lastMouseButton === 2) this.slotClear(slot); },
      muhelper_extra_select: (index) => {
        const row = this.configModel.extra_items[index];
        if (row) this.selectedExtraItem = row.name;
      },
    };
    this.detailModel = {
      text_px: 12, page: Page.NONE, precon: -1, subcon: -1, potion_level: 0, heal_level: 0, party_heal_level: 0,
      party_heal: false, party_duration: false, buff_interval: '0', labels: DETAIL_LABELS,
      muhelper_detail_precon: (index) => this.setPreCondition(index),
      muhelper_detail_subcon: (index) => this.setSubCondition(index),
      muhelper_detail_gauge: () => { /* the gauges take the pointer themselves (bindGauges) */ },
      muhelper_detail_party_heal: () => { this.staged.autoHealParty = !this.staged.autoHealParty; },
      muhelper_detail_party_duration: () => { this.staged.buffDurationParty = !this.staged.buffDurationParty; },
      muhelper_detail_save: () => { this.detailSave(); this.hideDetail(); },
      muhelper_detail_init: () => this.detailReset(),
      muhelper_detail_close: () => this.hideDetail(),
    };
    this.pickerModel = {
      origin_x: 0, origin_y: 0, scale: 1, attack: false, entries: [],
      mhsp_pick: (index) => this.pick(index),
    };
    for (const name of ['mu_helper_config', 'mu_helper_detail', 'mu_helper_skill_picker']) {
      prepareDocument(name).catch((error: unknown) => console.warn('Interface', error));
    }
  }

  get isOpen(): boolean {
    return this.configDoc !== null;
  }

  async toggle(): Promise<void> {
    if (this.isOpen) this.close();
    else await this.open();
  }

  open(): Promise<void> {
    if (this.configDoc) return Promise.resolve();
    this.opening ??= this.openDocuments().finally(() => { this.opening = null; });
    return this.opening;
  }

  /** The settings the server sent (CMuHelperConfigWindow::LoadSavedConfig): replaces the staged copy. */
  load(config: AutoPlayConfig): void {
    this.loadedFrom = config;
    this.staged = cloneAutoPlayConfig(config);
    this.staged.extraItems = normalizeExtraItems(this.staged.extraItems);
    this.applyConfig();
    this.refresh();
  }

  close(): void {
    if (!this.configDoc) return;
    this.blurField();
    for (const stop of this.stops.splice(0)) stop();
    this.configDoc.close();
    this.detailDoc?.close();
    this.pickerDoc?.close();
    this.configDoc = this.detailDoc = this.pickerDoc = null;
    this.detailVisible = false;
    this.pickerVisible = false;
  }

  private async openDocuments(): Promise<void> {
    await loadSkillData();
    const current = this.options.config();
    if (current !== this.loadedFrom) this.load(current);
    const scale = uiScale();
    const parent = this.parent;
    const [configDoc, detailDoc, pickerDoc] = await Promise.all([
      openDocument('mu_helper_config', this.configModel, { parent, scale }),
      openDocument('mu_helper_detail', this.detailModel, { parent, scale }),
      openDocument('mu_helper_skill_picker', this.pickerModel, { parent, scale }),
    ]);
    if (this.configDoc) { configDoc.close(); detailDoc.close(); pickerDoc.close(); return; }
    this.configDoc = configDoc;
    this.detailDoc = detailDoc;
    this.pickerDoc = pickerDoc;
    // The flyout stays above the docked panels (the desktop's stacking order 5.2 over 3.4).
    pickerDoc.host.style.zIndex = '1';

    // Every event refreshes all three: they show the one staged copy.
    for (const doc of [configDoc, detailDoc, pickerDoc]) {
      const model = doc === configDoc ? this.configModel : doc === detailDoc ? this.detailModel : this.pickerModel;
      model.__update = () => this.refresh();
    }
    for (const doc of [configDoc, detailDoc]) this.preparePanel(doc);
    this.bindGauges(detailDoc);

    const configRoot = configDoc.root;
    configRoot.addEventListener('mouseup', (event) => { this.lastMouseButton = event.button; }, true);
    configRoot.querySelector('#extra_list')?.addEventListener('wheel', (event) => event.stopPropagation());
    for (const id of ['distance_time', 'skill2_delay', 'skill3_delay']) configDoc.$(`#${id}`)?.setAttribute('maxlength', String(MAX_NUMBER_DIGITS));
    configDoc.$('#item_name')?.setAttribute('maxlength', String(MAX_ITEM_NAME));
    for (const id of ['buff_interval', 'buff_interval_elf']) detailDoc.$(`#${id}`)?.setAttribute('maxlength', String(MAX_NUMBER_DIGITS));

    const keydown = (event: KeyboardEvent) => { if (event.key === 'Escape') this.escape(); };
    const resize = () => this.place();
    window.addEventListener('keydown', keydown);
    window.addEventListener('resize', resize);
    this.stops.push(() => window.removeEventListener('keydown', keydown), () => window.removeEventListener('resize', resize));

    this.place();
  }

  // Pointer and key handling the themes leave to the game.
  private preparePanel(doc: RmlDocument): void {
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
      panel.addEventListener('contextmenu', (event) => event.preventDefault());
    }
    // The numeric fields take digits only (RmlNumericInputFilter); this runs before the field's binding.
    doc.root.addEventListener('input', (event) => {
      const field = event.target;
      if (!(field instanceof HTMLInputElement) || !field.classList.contains('text-field--numeric')) return;
      const digits = field.value.replace(/\D/g, '');
      if (digits !== field.value) field.value = digits;
    }, true);
    // Typing in a field stays in the field (the game's hotkeys see the document's host as the target);
    // Escape still closes the window.
    for (const type of ['keydown', 'keyup', 'keypress'] as const) {
      doc.root.addEventListener(type, (event) => {
        if (!(event.target instanceof HTMLInputElement)) return;
        event.stopPropagation();
        if (type === 'keydown' && event.key === 'Escape') this.escape();
      });
    }
  }

  // The gauges: a press or drag sets the level from the pointer, the wheel steps it (ApplyLevelGaugeEvent).
  private bindGauges(doc: RmlDocument): void {
    const gauges: Record<string, number> = { gauge_potion: 0, gauge_heal: 1, gauge_party_heal: 2 };
    for (const hit of doc.root.querySelectorAll<HTMLElement>('.level-gauge-hit')) {
      const bar = hit.parentElement;
      const gauge = bar ? gauges[bar.id] : undefined;
      if (!bar || gauge === undefined) continue;
      const fromPointer = (event: PointerEvent) => {
        const rect = bar.getBoundingClientRect();
        if (rect.width <= 0) return;
        const x = (event.clientX - rect.left) * (GAUGE_WIDTH / rect.width) - GAUGE_ORIGIN_INSET;
        this.setGauge(gauge, x < 0 ? 0 : Math.floor((GAUGE_MAX_LEVEL * x) / GAUGE_WIDTH) + 1);
      };
      hit.addEventListener('pointerdown', (event) => {
        if (event.button !== 0) return;
        hit.setPointerCapture(event.pointerId);
        fromPointer(event);
      });
      hit.addEventListener('pointermove', (event) => {
        if ((event.buttons & 1) !== 0 && hit.hasPointerCapture(event.pointerId)) fromPointer(event);
      });
      hit.addEventListener('wheel', (event) => {
        event.preventDefault();
        event.stopPropagation();
        if (event.deltaY === 0) return;
        const level = gauge === 0 ? this.potionLevel : gauge === 1 ? this.healLevel : this.partyHealLevel;
        this.setGauge(gauge, level + (event.deltaY < 0 ? 1 : -1));
      }, { passive: false });
    }
  }

  private setGauge(gauge: number, value: number): void {
    if (!this.detailVisible) return;
    const level = Math.min(Math.max(value, 0), GAUGE_MAX_LEVEL);
    const page = this.detailPage;
    if (gauge === 0 && isPotionPage(page)) this.potionLevel = level;
    else if (gauge === 1 && (page === Page.POTION_ELF || page === Page.POTION_SUMMONER)) this.healLevel = level;
    else if (gauge === 2 && page === Page.PARTY_ELF) this.partyHealLevel = level;
    else return;
    this.refresh();
  }

  /** Docks the settings window at the right edge and the detail panel beside it; the flyout follows. */
  place(): void {
    if (!this.configDoc || !this.detailDoc || !this.pickerDoc) return;
    const scale = uiScale();
    const right = this.options.dockRight?.() ?? 0;
    const dock = (doc: RmlDocument, column: number) => {
      const panel = doc.$('#panel');
      if (!panel) return;
      Object.assign(panel.style, {
        left: `${window.innerWidth - (right + (column + 1) * PANEL_WIDTH) * scale}px`, top: '0px',
        transform: `scale(${scale})`, transformOrigin: 'left top',
      });
      doc.setScale(scale);
    };
    dock(this.configDoc, 0);
    dock(this.detailDoc, 1);
    this.pickerDoc.setScale(scale);
    this.configModel.text_px = this.detailModel.text_px = textPx(scale);
    this.refresh();
  }

  // ---------- the settings window ----------

  private checks(): ConfigChecks {
    const c = this.staged;
    const conditions = c.skillConditions;
    return {
      potion: c.useHealPotion, long_distance: c.longRangeCounterAttack, orig_position: c.returnToOriginalPosition,
      skill2_delay: (conditions[1] & SkillActivation.ON_TIMER) !== 0,
      skill2_condition: (conditions[1] & SkillActivation.ON_CONDITION) !== 0,
      skill3_delay: (conditions[2] & SkillActivation.ON_TIMER) !== 0,
      skill3_condition: (conditions[2] & SkillActivation.ON_CONDITION) !== 0,
      combo: c.useCombo, fallback: c.fallbackBasicAttack, buff_duration: c.buffDuration, use_pet: c.useDarkRaven,
      party: c.supportParty, auto_heal: c.autoHeal, drain_life: c.useDrainLife, repair: c.repairItem,
      pick_all: c.pickAllItems, pick_selected: c.pickSelectItems, pick_jewel: c.pickJewel, pick_ancient: c.pickAncient,
      pick_zen: c.pickZen, pick_excellent: c.pickExcellent, pick_extra: c.pickExtraItems,
      auto_friend: c.autoAcceptFriend, auto_guild: c.autoAcceptGuild, auto_defend: c.useSelfDefense,
    };
  }

  private selectTab(tab: number): void {
    if (!Number.isInteger(tab) || tab < 0 || tab > 2 || tab === this.activeTab) return;
    this.blurField();
    this.activeTab = tab;
  }

  private toggleCheck(key: string): void {
    const c = this.staged;
    switch (key) {
      case 'potion': c.useHealPotion = !c.useHealPotion; break;
      case 'long_distance': c.longRangeCounterAttack = !c.longRangeCounterAttack; break;
      case 'orig_position': c.returnToOriginalPosition = !c.returnToOriginalPosition; break;
      case 'skill2_delay': case 'skill3_delay': case 'skill2_condition': case 'skill3_condition': {
        const index = key.startsWith('skill2') ? 1 : 2;
        const timer = key.endsWith('delay');
        const enable = (c.skillConditions[index] & (timer ? SkillActivation.ON_TIMER : SkillActivation.ON_CONDITION)) === 0;
        c.skillConditions[index] = setConditionMode(c.skillConditions[index], timer, enable);
        // Choosing a timer, or dropping the condition, closes the condition's page.
        if (timer === enable) this.hideDetail();
        if (!timer && enable) c.skillConditions[index] = ensureConditionDefaults(c.skillConditions[index]);
        break;
      }
      case 'combo':
        if (!c.useCombo && (this.selectedSkills[0] <= 0 || this.selectedSkills[1] <= 0 || this.selectedSkills[2] <= 0)) {
          this.options.onMessage?.(COMBO_NEEDS_SKILLS);
          return;
        }
        c.useCombo = !c.useCombo;
        break;
      case 'fallback': c.fallbackBasicAttack = !c.fallbackBasicAttack; break;
      case 'buff_duration': c.buffDuration = !c.buffDuration; break;
      case 'use_pet': c.useDarkRaven = !c.useDarkRaven; break;
      case 'party': c.supportParty = !c.supportParty; break;
      case 'auto_heal': c.autoHeal = !c.autoHeal; break;
      case 'drain_life': c.useDrainLife = !c.useDrainLife; break;
      case 'repair': c.repairItem = !c.repairItem; break;
      // All items and selected items exclude each other.
      case 'pick_all': c.pickAllItems = !c.pickAllItems; if (c.pickAllItems) c.pickSelectItems = false; break;
      case 'pick_selected': c.pickSelectItems = !c.pickSelectItems; if (c.pickSelectItems) c.pickAllItems = false; break;
      case 'pick_jewel': c.pickJewel = !c.pickJewel; break;
      case 'pick_ancient': c.pickAncient = !c.pickAncient; break;
      case 'pick_zen': c.pickZen = !c.pickZen; break;
      case 'pick_excellent': c.pickExcellent = !c.pickExcellent; break;
      case 'pick_extra': c.pickExtraItems = !c.pickExtraItems; break;
      case 'auto_friend': c.autoAcceptFriend = !c.autoAcceptFriend; break;
      case 'auto_guild': c.autoAcceptGuild = !c.autoAcceptGuild; break;
      case 'auto_defend': c.useSelfDefense = !c.useSelfDefense; break;
      default: break;
    }
  }

  private button(key: string): void {
    const c = this.staged;
    const features = autoPlayClassFeatures(this.options.classIndex);
    switch (key) {
      case 'hunt_add': c.huntingRange = Math.min(c.huntingRange + 1, MAX_HUNTING_RANGE); break;
      case 'hunt_minus': c.huntingRange = Math.max(c.huntingRange - 1, 0); break;
      case 'pick_add': c.obtainingRange = Math.min(c.obtainingRange + 1, MAX_OBTAINING_RANGE); break;
      case 'pick_minus': c.obtainingRange = Math.max(c.obtainingRange - 1, 1); break;
      case 'add_item': this.addExtraItem(); break;
      case 'delete_item': this.removeExtraItem(); break;
      case 'skill2_setting': case 'skill3_setting': {
        // Opening a skill's condition page also switches the skill to "on condition".
        const second = key === 'skill2_setting';
        const index = second ? 1 : 2;
        c.skillConditions[index] = setConditionMode(c.skillConditions[index], false, true);
        this.toggleDetail(second ? Page.SKILL2 : Page.SKILL3);
        break;
      }
      case 'potion_setting': this.toggleDetail(features.potionPage); break;
      case 'party_setting': if (features.party) this.toggleDetail(features.partyPage); break;
      case 'save': this.save(); this.close(); break;
      case 'init': this.initConfig(); break;
      case 'close': this.close(); break;
      default: break;
    }
  }

  private slotClick(slot: number): void {
    if (!Number.isInteger(slot) || slot < 0 || slot >= SKILL_SLOTS) return;
    const previous = this.selectedSlot;
    const wasOpen = this.pickerVisible;
    this.selectedSlot = slot;
    this.preparePicker(slot < 3);
    // A second click on the same slot closes the flyout.
    this.pickerVisible = !(slot === previous && wasOpen);
  }

  private slotClear(slot: number): void {
    if (!Number.isInteger(slot) || slot < 0 || slot >= SKILL_SLOTS) return;
    this.selectedSkills[slot] = -1;
    this.staged.useCombo = false;
  }

  // The skill picker's choice for the slot that opened it (CMuHelperConfigWindow::AssignSkill).
  private assignSkill(skill: number): void {
    const slot = this.selectedSlot;
    if (slot < 0 || slot >= SKILL_SLOTS) return;
    const assignedAt = this.selectedSkills.indexOf(skill);
    if (assignedAt < 0) {
      this.selectedSkills[slot] = skill;
      if (slot < 3) this.staged.skills[slot] = skill;
      else this.staged.buffs[slot - 3] = skill;
    } else {
      // Already in another slot: it moves here.
      this.selectedSkills[assignedAt] = -1;
      this.selectedSkills[slot] = skill;
      this.staged.useCombo = false;
    }
  }

  private addExtraItem(): void {
    const name = this.configModel.item_name;
    if (!name) return;
    this.staged.extraItems = normalizeExtraItems([...this.staged.extraItems, name]);
    this.selectedExtraItem = name;
    this.configModel.item_name = '';
    this.blurField();
  }

  private removeExtraItem(): void {
    const items = this.staged.extraItems;
    const at = items.indexOf(this.selectedExtraItem);
    if (!this.selectedExtraItem || at < 0) return;
    items.splice(at, 1);
    // The entry after it in the set takes the selection.
    this.selectedExtraItem = items[at] ?? '';
  }

  // CMuHelperConfigWindow::ApplyConfig, without loading the settings into the running auto play.
  private applyConfig(): void {
    const c = this.staged;
    this.detailApplySavedConfig();
    for (let i = 0; i < 3; i++) {
      this.selectedSkills[i] = c.skills[i] || -1;
      this.selectedSkills[3 + i] = c.buffs[i] || -1;
    }
    this.configModel.distance_time = String(c.maxSecondsAway);
    this.configModel.skill2_delay = String(c.skillIntervals[1]);
    this.configModel.skill3_delay = String(c.skillIntervals[2]);
  }

  // The "Initialization" button: Reset, then the detail panel's own InitConfig.
  private initConfig(): void {
    const reset = initialAutoPlayConfig();
    reset.fallbackBasicAttack = this.staged.fallbackBasicAttack;
    reset.useSelfDefense = this.staged.useSelfDefense;
    reset.autoAcceptFriend = this.staged.autoAcceptFriend;
    reset.autoAcceptGuild = this.staged.autoAcceptGuild;
    this.staged = reset;
    this.selectedExtraItem = '';
    this.applyConfig();
    this.options.onApply?.(cloneAutoPlayConfig(this.staged));
    this.detailInitConfig();
  }

  private save(): void {
    const c = this.staged;
    c.maxSecondsAway = parseNumberField(this.configModel.distance_time);
    c.skillIntervals[1] = parseNumberField(this.configModel.skill2_delay);
    c.skillIntervals[2] = parseNumberField(this.configModel.skill3_delay);
    for (let i = 0; i < 3; i++) {
      c.skills[i] = Math.max(this.selectedSkills[i], 0);
      c.buffs[i] = Math.max(this.selectedSkills[3 + i], 0);
    }
    this.options.onSave(cloneAutoPlayConfig(c));
  }

  // ---------- the detail panel ----------

  /** Opens a page, or closes the panel when it already shows that page (CMuHelperDetailWindow::Toggle). */
  private toggleDetail(page: number): void {
    const previous = this.detailPage;
    this.detailPage = page;
    if (this.detailVisible && page === previous) {
      this.detailPage = Page.NONE;
      this.hideDetail();
      return;
    }
    const c = this.staged;
    if (isSkillPage(page)) {
      const index = conditionIndexOf(page);
      c.skillConditions[index] = ensureConditionDefaults(c.skillConditions[index]);
    } else if (isPotionPage(page)) {
      this.potionLevel = Math.trunc(c.potionThreshold / 10);
      this.healLevel = Math.trunc(c.healThreshold / 10);
    } else if (isPartyPage(page)) {
      this.partyHealLevel = Math.trunc(c.healPartyThreshold / 10);
      // Reseeded on every open of a party page, dropping anything typed but not saved.
      this.detailModel.buff_interval = String(c.buffCastInterval);
    }
    this.detailVisible = true;
  }

  private hideDetail(): void {
    if (this.detailVisible) this.blurField(this.detailDoc);
    this.detailVisible = false;
  }

  private detailSave(): void {
    const c = this.staged;
    c.buffCastInterval = parseNumberField(this.detailModel.buff_interval);
    c.potionThreshold = this.potionLevel * 10;
    c.healThreshold = this.healLevel * 10;
    c.healPartyThreshold = this.partyHealLevel * 10;
  }

  private detailApplySavedConfig(): void {
    const c = this.staged;
    this.potionLevel = Math.trunc(c.potionThreshold / 10);
    this.healLevel = Math.trunc(c.healThreshold / 10);
    this.partyHealLevel = Math.trunc(c.healPartyThreshold / 10);
    this.detailModel.buff_interval = String(c.buffCastInterval);
  }

  // The settings window's "Initialization" for the panel's values.
  private detailInitConfig(): void {
    const c = this.staged;
    c.potionThreshold = 40;
    c.healThreshold = 60;
    c.buffCastInterval = 0;
    c.healPartyThreshold = 60;
    c.autoHealParty = false;
    c.buffDurationParty = false;
    this.detailModel.buff_interval = String(c.buffCastInterval);
  }

  // The panel's own "Initialization": resets only the page it shows.
  private detailReset(): void {
    const c = this.staged;
    if (isSkillPage(this.detailPage)) {
      c.skillConditions[conditionIndexOf(this.detailPage)] = SkillActivation.ON_MOBS_NEARBY | SkillActivation.ON_MORE_THAN_TWO_MOBS;
    } else if (isPotionPage(this.detailPage)) {
      c.potionThreshold = 0;
      c.healThreshold = 0;
      this.potionLevel = 0;
      this.healLevel = 0;
    }
  }

  private setPreCondition(index: number): void {
    if (!isSkillPage(this.detailPage) || (index !== 0 && index !== 1)) return;
    const at = conditionIndexOf(this.detailPage);
    const conditions = this.staged.skillConditions;
    conditions[at] = (conditions[at] & ~PRE_CONDITIONS) | (index === 0 ? SkillActivation.ON_MOBS_NEARBY : SkillActivation.ON_MOBS_ATTACKING);
  }

  private setSubCondition(index: number): void {
    if (!isSkillPage(this.detailPage) || !Number.isInteger(index) || index < 0 || index > 3) return;
    const at = conditionIndexOf(this.detailPage);
    const conditions = this.staged.skillConditions;
    conditions[at] = (conditions[at] & ~SUB_CONDITIONS) | SUB_CONDITION_BITS[index];
  }

  // ---------- the skill picker ----------

  // The learned skills the slot takes, in list order (PrepareSkillsToRender).
  private preparePicker(attack: boolean): void {
    this.pickerAttack = attack;
    const skills: number[] = [];
    for (let i = 0; i < MAX_MAGIC; i++) {
      const skill = this.options.skills.skillAt(i);
      if (skill === 0 || (skill >= SkillNumber.STUN && skill <= SkillNumber.REMOVAL_BUFF)) continue;
      const useType = skillInfo(skill)?.useType;
      if (useType === SkillUseType.MASTER || useType === SkillUseType.MASTERLEVEL) continue;
      if (attack ? isAttackSkill(skill) : isBuffSkill(skill)) skills.push(skill);
    }
    this.pickerSkills = skills;
  }

  private pick(index: number): void {
    if (!this.pickerVisible || !Number.isInteger(index) || index < 0 || index >= this.pickerSkills.length) return;
    this.assignSkill(this.pickerSkills[index]);
    this.pickerVisible = false;
  }

  // ---------- views ----------

  // Escape: the flyout first, then the detail panel, then the whole window.
  private escape(): void {
    if (!this.isOpen) return;
    if (this.pickerVisible) this.pickerVisible = false;
    else if (this.detailVisible) this.hideDetail();
    else { this.close(); return; }
    this.refresh();
  }

  private blurField(doc: RmlDocument | null = null): void {
    for (const d of doc ? [doc] : [this.configDoc, this.detailDoc]) {
      const root = d?.root.getRootNode();
      if (root instanceof ShadowRoot && root.activeElement instanceof HTMLElement) root.activeElement.blur();
    }
  }

  // Fills the three models from the staged settings and redraws (the desktop's SyncRmlModel).
  private refresh(): void {
    const c = this.staged;
    const m = this.configModel;
    m.active_tab = this.activeTab;
    m.hunt_range = c.huntingRange;
    m.pick_range = c.obtainingRange;
    m.raven_mode = c.darkRavenMode;
    m.checks = this.checks();
    const [s0, s1, s2, s3, s4, s5] = this.selectedSkills.map(skillDecorator);
    m.slots = { s0, s1, s2, s3, s4, s5 };
    // Newest-looking first, as the desktop's list shows the saved set.
    m.extra_items = [...c.extraItems].reverse().map((name, index) => ({ name, selected: name === this.selectedExtraItem, index }));

    const d = this.detailModel;
    d.page = this.detailPage;
    let precon = -1, subcon = -1;
    if (isSkillPage(this.detailPage)) {
      const bits = c.skillConditions[conditionIndexOf(this.detailPage)];
      if (bits & SkillActivation.ON_MOBS_NEARBY) precon = 0;
      else if (bits & SkillActivation.ON_MOBS_ATTACKING) precon = 1;
      subcon = SUB_CONDITION_BITS.findIndex((bit) => (bits & bit) !== 0);
    }
    d.precon = precon;
    d.subcon = subcon;
    d.potion_level = this.potionLevel;
    d.heal_level = this.healLevel;
    d.party_heal_level = this.partyHealLevel;
    d.party_heal = c.autoHealParty;
    d.party_duration = c.buffDurationParty;

    const p = this.pickerModel;
    p.attack = this.pickerAttack;
    p.entries = this.pickerSkills.map((skill) => ({ decorator: skillDecorator(skill) }));

    const { configDoc, detailDoc, pickerDoc } = this;
    if (!configDoc || !detailDoc || !pickerDoc) return;
    detailDoc.host.style.display = this.detailVisible ? '' : 'none';
    pickerDoc.host.style.display = this.pickerVisible ? '' : 'none';
    // The flyout hangs off the settings window's drawn top left, at its scale.
    const panel = configDoc.$('#panel');
    if (panel && this.pickerVisible) {
      const at = panel.getBoundingClientRect();
      const origin = pickerDoc.host.getBoundingClientRect();
      p.origin_x = at.left - origin.left;
      p.origin_y = at.top - origin.top;
      p.scale = at.width / PANEL_WIDTH || uiScale();
    }
    configDoc.update();
    detailDoc.update();
    pickerDoc.update();
    this.nameSkills(configDoc, pickerDoc);
  }

  // Skill names on hover, for skills whose icon the web client doesn't draw yet (master skills).
  private nameSkills(configDoc: RmlDocument, pickerDoc: RmlDocument): void {
    const name = (skill: number) => (skill > 0 ? skillInfo(skill)?.name ?? '' : '');
    for (let slot = 0; slot < SKILL_SLOTS; slot++) {
      const element = configDoc.$(`#slot_${slot}`);
      if (element) element.title = name(this.selectedSkills[slot]);
    }
    pickerDoc.root.querySelectorAll<HTMLElement>('.mhsp-entry').forEach((entry, i) => {
      entry.title = name(this.pickerSkills[i] ?? 0);
    });
  }
}
