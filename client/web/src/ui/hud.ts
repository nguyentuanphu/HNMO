// The HUD at the bottom of the screen (main_frame.rml), as the desktop client's
// CMainFrameWindow fills it: life, mana, shield (SD) and ability (AG) gauges with their numbers,
// the experience bar (its digit and the progress within the current tenth), and the menu
// buttons. Skill and potion slots come with the skills.

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { experienceForLevel, type CharacterStats } from '../game/stats.js';


interface HudModel extends RmlModel {
  hp_fraction: number; mp_fraction: number; sd_fraction: number; ag_fraction: number; exp_fraction: number;
  hp_text: string; mp_text: string; sd_text: string; ag_text: string;
  hp_current_text: string; mp_current_text: string; sd_current_text: string; ag_current_text: string;
  hp_tooltip: string; mp_tooltip: string; sd_tooltip: string; ag_tooltip: string; exp_tooltip: string; exp_digit: string;
  poisoned: boolean;
  [key: string]: unknown;
}

export interface HudActions {
  inventory: () => void;
  character?: () => void;
  menu?: () => void;
  friends?: () => void;
  cashShop?: () => void;
}

const clamp = (value: number, max: number) => Math.min(Math.max(0, value), Math.max(1, max));

export class Hud {
  private doc: RmlDocument | null = null;
  private readonly model: HudModel;

  constructor(private readonly stats: CharacterStats, actions: HudActions) {
    const noop = () => undefined;
    this.model = {
      hp_fraction: 0, mp_fraction: 0, sd_fraction: 0, ag_fraction: 0, exp_fraction: 0,
      hp_text: '', mp_text: '', sd_text: '', ag_text: '', hp_current_text: '', mp_current_text: '', sd_current_text: '', ag_current_text: '',
      hp_tooltip: '', mp_tooltip: '', sd_tooltip: '', ag_tooltip: '', exp_tooltip: '', exp_digit: '0', poisoned: false,
      // Buttons and slots (the skill and potion parts are filled by the skills step)
      chainfo_open: false, myinven_open: false, friend_open: false, window_open: false, cshop_open: false,
      chainfo_alert: false, friend_alert: false, skill_grid_open: false, skill_list_up: false,
      chainfo_tooltip: 'Character (C)', myinven_tooltip: 'Inventory (I, V)', friend_tooltip: 'Friends (F)',
      window_tooltip: 'Menu (U)', cshop_tooltip: 'Cash shop (X)',
      skill_grid_cells: [], pet_skill_cells: [], current_skill_icon: '', current_skill_hotkey: '', current_skill_cooldown: 0,
      item_slot_0_count: '', item_slot_1_count: '', item_slot_2_count: '', item_slot_3_count: '',
      item_slot_0_hovered: false, item_slot_1_hovered: false, item_slot_2_hovered: false, item_slot_3_hovered: false,
      ...Object.fromEntries([0, 1, 2, 3, 4].flatMap((i) => [
        [`skill_slot_${i}_icon`, ''], [`skill_slot_${i}_hotkey`, String((i + 1) % 10)], [`skill_slot_${i}_cooldown`, 0], [`skill_slot_${i}_selected`, false],
      ])),
      mainframe_myinven_click: actions.inventory,
      mainframe_chainfo_click: actions.character ?? noop,
      mainframe_window_click: actions.menu ?? noop,
      mainframe_friend_click: actions.friends ?? noop,
      mainframe_cshop_click: actions.cashShop ?? noop,
      item_hotkey_hover: noop, item_hotkey_unhover: noop, item_hotkey_rightclick: noop,
      skill_hotkey_click: noop, skill_hotkey_hover: noop, skill_unhover: noop, skill_current_click: noop, skill_current_hover: noop,
      skill_grid_click: noop, skill_grid_hover: noop, skill_pet_click: noop, skill_pet_hover: noop,
    };
    stats.onChange(() => this.refresh());
    window.addEventListener('resize', () => this.place());
  }

  async open(parent: HTMLElement = document.body): Promise<void> {
    this.doc = await openDocument('main_frame', this.model, { parent });
    this.doc.host.style.zIndex = '20';
    this.place();
    this.refresh();
  }

  // The root stands for the 640 x 480 reference screen, scaled to the window; the HUD docks to its bottom centre.
  private place(): void {
    if (!this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    Object.assign(this.doc.root.style, {
      width: `${window.innerWidth / scale}px`, height: `${REFERENCE_HEIGHT}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none',
    });
    this.doc.setScale(scale);
    const strip = this.doc.$('#hud_strip');
    if (strip) strip.style.pointerEvents = 'auto';
    for (const part of ['#buttons', '#item_slots', '#skill_slots']) {
      const element = this.doc.$(part);
      if (element) element.style.pointerEvents = 'auto';
    }
  }

  refresh(): void {
    if (!this.doc) return;
    const s = this.stats;
    const m = this.model;
    const life = clamp(s.life, s.maxLife), mana = clamp(s.mana, s.maxMana), shield = clamp(s.shield, s.maxShield), ability = clamp(s.ability, s.maxAbility);
    m.hp_fraction = s.maxLife > 0 ? life / s.maxLife : 0;
    m.mp_fraction = s.maxMana > 0 ? mana / s.maxMana : 0;
    m.sd_fraction = shield / Math.max(1, s.maxShield);
    m.ag_fraction = ability / Math.max(1, s.maxAbility);
    m.hp_text = `${life} / ${s.maxLife}`; m.mp_text = `${mana} / ${s.maxMana}`;
    m.sd_text = `${shield} / ${s.maxShield}`; m.ag_text = `${ability} / ${s.maxAbility}`;
    m.hp_current_text = String(life); m.mp_current_text = String(mana); m.sd_current_text = String(shield); m.ag_current_text = String(ability);
    m.hp_tooltip = `Life: ${life}/${s.maxLife}`; m.mp_tooltip = `Mana: ${mana}/${s.maxMana}`;
    m.sd_tooltip = `SD: ${shield}/${s.maxShield}`; m.ag_tooltip = `AG: ${ability}/${s.maxAbility}`;

    // Experience: the bar shows tenths of the way from this level to the next (CMainFrameWindow).
    const lower = experienceForLevel(s.level);
    const upper = Math.max(lower, s.nextExperience);
    const ratio = upper > lower ? Math.min(1, Math.max(0, (Math.min(Math.max(s.experience, lower), upper) - lower) / (upper - lower))) : 0;
    const scaled = ratio * 10;
    let digit = Math.min(9, Math.max(0, Math.floor(scaled)));
    let progress = scaled - Math.floor(scaled);
    if (ratio >= 1) { digit = 9; progress = 1; }
    m.exp_fraction = progress;
    m.exp_digit = String(digit);
    m.exp_tooltip = `EXP: ${s.experience}/${s.nextExperience}`;
    this.doc.update();
  }
}
