// The HUD at the bottom of the screen (main_frame.rml), as the desktop client's
// CMainFrameWindow fills it: life, mana, shield (SD) and ability (AG) gauges with their numbers,
// the experience bar (its digit and the progress within the current tenth), the menu buttons and
// the potion slots Q W E R (CItemHotKey: the item each uses next, drawn as its 3D model, and how
// many are left). Skill slots come with the skills.

import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { experienceForLevel, type CharacterStats } from '../game/stats.js';
import type { Item } from '../game/inventory.js';
import { drawItem } from './itemIcons.js';
import { skillCooldowns, skillIconSprite, type SkillList } from '../game/skills.js';
import { hudScale } from './nativeText.js';
import { onLanguageChange, translate } from '../i18n.js';

/** What the skill bar shows: the skill list and whether a skill can be used now (lit or grey icon). */
export interface HudSkills {
  list: SkillList;
  usable: (skill: number) => boolean;
  hasPet: () => boolean;
  /**
   * Shows the tooltip of a list index's skill (null hides it): centred on `anchor.x`, its bottom at `anchor.y`
   * (screen pixels; the box ends `anchor.belowPerLine` per line below it, as the native box did).
   */
  tooltip?: (index: number | null, anchor: SkillTooltipAnchor | null) => void;
}

export interface SkillTooltipAnchor { x: number; y: number }

// CSkillList::QueueTooltip's anchors, in the HUD's reference units: the tooltip is centred 10 right of a
// hotkey slot's left edge (15 in the expanded list), 10 above the slot; the current skill's 32x38 hit box
// sits 7 left of and 6 above its 20x28 icon (the element the web hovers).
const SLOT_TOOLTIP_OFFSET_X = 10;
const GRID_TOOLTIP_OFFSET_X = 15;
const TOOLTIP_GAP_ABOVE = 10;
const CURRENT_SKILL_BOX_INSET_X = 7;
const CURRENT_SKILL_BOX_INSET_Y = 6;

/** The master level's experience: the total, where the level starts and where the next one does. */
export interface MasterExperience { experience: number; start: number; next: number }

interface SkillCell { skill_index: number; is_current: boolean; icon: string; hotkey: string; cooldown_fraction: number }

/** What the potion slots show and do. */
export interface HudItemSlots {
  /** The item a slot uses next (0-3: Q W E R), or null. */
  item: (slot: number) => Item | null;
  count: (slot: number) => number;
  /** Right-click on a slot: use its item. */
  use: (slot: number) => void;
}


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
  party?: () => void;
  guild?: () => void;
  command?: () => void;
}

const clamp = (value: number, max: number) => Math.min(Math.max(0, value), Math.max(1, max));

export class Hud {
  private doc: RmlDocument | null = null;
  private readonly model: HudModel;
  private readonly slotCanvases: HTMLCanvasElement[] = [];
  private readonly slotSignatures: string[] = ['', '', '', ''];
  private hoveredSlot = -1;
  private spinStart = 0;
  private skills: HudSkills | null = null;
  private masterExperience: (() => MasterExperience | null) | null = null;
  /** The popup cell under the mouse: Ctrl + a number key puts its skill on that key. */
  hoveredSkill = -1;
  /** The slot or popup cell the mouse is over (found before the document's own handlers run). */
  private hoveredElement: Element | null = null;

  constructor(private readonly stats: CharacterStats, actions: HudActions, private readonly itemSlots: HudItemSlots | null = null) {
    const noop = () => undefined;
    this.model = {
      hp_fraction: 0, mp_fraction: 0, sd_fraction: 0, ag_fraction: 0, exp_fraction: 0,
      hp_text: '', mp_text: '', sd_text: '', ag_text: '', hp_current_text: '', mp_current_text: '', sd_current_text: '', ag_current_text: '',
      hp_tooltip: '', mp_tooltip: '', sd_tooltip: '', ag_tooltip: '', exp_tooltip: '', exp_digit: '0', poisoned: false,
      // Buttons and slots (the skill and potion parts are filled by the skills step)
      chainfo_open: false, myinven_open: false, friend_open: false, window_open: false, cshop_open: false,
      party_open: false, guild_open: false, command_open: false,
      chainfo_alert: false, friend_alert: false, skill_grid_open: false, skill_list_up: false,
      chainfo_tooltip: 'Character (C)', myinven_tooltip: 'Inventory (I, V)', friend_tooltip: 'Friends (F)',
      window_tooltip: 'Menu (U)', cshop_tooltip: 'Cash shop (X)',
      party_tooltip: 'Party (P)', guild_tooltip: 'Guild (G)', command_tooltip: 'Command (D)',
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
      mainframe_party_click: actions.party ?? noop,
      mainframe_guild_click: actions.guild ?? noop,
      mainframe_command_click: actions.command ?? noop,
      // The hovered slot's item turns (RenderItem3DWithHover); a right click uses it (OnHotkeySlotRightClick).
      item_hotkey_hover: (slot: number) => this.hoverSlot(slot),
      item_hotkey_unhover: () => this.hoverSlot(-1),
      item_hotkey_rightclick: noop, // the right button is told apart in open()
      // The skill bar (CSkillList's OnHotkeySlotClick, OnCurrentSkillClick, OnGridCellClick, …).
      skill_hotkey_click: (slot: number) => this.skillSlotClick(slot),
      skill_hotkey_hover: (slot: number) => this.skillTooltip(this.slotIndex(slot), this.doc?.$(`#skill_slot_${slot}`) ?? null, 'slot'),
      skill_unhover: () => { this.hoveredSkill = -1; this.skills?.tooltip?.(null, null); },
      skill_current_click: () => { this.model.skill_grid_open = !this.model.skill_grid_open; this.refreshSkills(); },
      skill_current_hover: () => { if (this.skills && this.skills.list.count > 0) this.skillTooltip(this.skills.list.current, this.doc?.$('#current_skill_slot') ?? null, 'current'); },
      skill_grid_click: (index: number) => this.pickSkill(index),
      skill_grid_hover: (index: number) => { this.hoveredSkill = index; this.skillTooltip(index, this.hoveredElement, 'grid'); },
      skill_pet_click: (index: number) => this.pickSkill(index),
      skill_pet_hover: (index: number) => { this.hoveredSkill = index; this.skillTooltip(index, this.hoveredElement, 'grid'); },
    };
    stats.onChange(() => this.refresh());
    onLanguageChange(() => this.syncHints());
    window.addEventListener('resize', () => this.place());
  }

  async open(parent: HTMLElement = document.body): Promise<void> {
    this.doc = await openDocument('main_frame', this.model, { parent });
    this.doc.host.style.zIndex = '20';
    this.doc.root.addEventListener('mouseover', (event) => {
      this.hoveredElement = event.target instanceof Element ? event.target.closest('.slot-frame, .skill-cell') : null;
    }, true);
    for (let slot = 0; slot < 4; slot++) {
      const frame = this.doc.$(`#item_slot_${slot}`);
      const icon = this.doc.$(`#item_icon_${slot}`);
      if (!frame || !icon) continue;
      const canvas = document.createElement('canvas');
      canvas.className = 'item-icon';
      canvas.dataset.slot = String(slot);
      canvas.style.display = 'none';
      icon.replaceWith(canvas);
      this.slotCanvases[slot] = canvas;
      frame.addEventListener('contextmenu', (event) => event.preventDefault());
      frame.addEventListener('mouseup', (event) => { if (event.button === 2) this.itemSlots?.use(slot); });
    }
    this.place();
    this.refresh();
  }

  // The root stands for the 640 x 480 reference screen, scaled to the window; the HUD docks to its bottom centre.
  private place(): void {
    if (!this.doc) return;
    const scale = hudScale();
    Object.assign(this.doc.root.style, {
      width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
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

  /** Lights the cash shop button while the shop is open (MAINFRAME_BTN_PARTCHARGE). */
  setCashShopOpen(open: boolean): void { this.setOpenStates({ cashShop: open }); }

  /** Swaps the HP orb art between red and poisoned green (eDeBuff_Poison). */
  setPoisoned(poisoned: boolean): void {
    if (this.model.poisoned === poisoned) return;
    this.model.poisoned = poisoned;
    this.doc?.update();
  }

  /** Lights the Character (quest/stat alert) and Friends (unread mail/chat alert) dots. */
  setAlertStates(alerts: Partial<{ character: boolean; friends: boolean }>): void {
    let changed = false;
    if (alerts.character !== undefined && this.model.chainfo_alert !== alerts.character) {
      this.model.chainfo_alert = alerts.character;
      changed = true;
    }
    if (alerts.friends !== undefined && this.model.friend_alert !== alerts.friends) {
      this.model.friend_alert = alerts.friends;
      changed = true;
    }
    if (changed) this.doc?.update();
  }

  private syncHints(): void {
    if (!this.doc) return;
    for (const el of this.doc.root.querySelectorAll<HTMLElement>('[data-hint]')) {
      const hint = el.getAttribute('data-hint');
      if (hint) el.title = translate(hint) ?? hint;
    }
  }

  /** Updates bottom HUD frame button highlight states (`open` class). */
  setOpenStates(states: Partial<{
    character: boolean;
    inventory: boolean;
    friends: boolean;
    menu: boolean;
    cashShop: boolean;
    party: boolean;
    guild: boolean;
    command: boolean;
  }>): void {
    const m = this.model;
    if (states.character !== undefined) m.chainfo_open = states.character;
    if (states.inventory !== undefined) m.myinven_open = states.inventory;
    if (states.friends !== undefined) m.friend_open = states.friends;
    if (states.menu !== undefined) m.window_open = states.menu;
    if (states.cashShop !== undefined) m.cshop_open = states.cashShop;
    if (states.party !== undefined) m.party_open = states.party;
    if (states.guild !== undefined) m.guild_open = states.guild;
    if (states.command !== undefined) m.command_open = states.command;
    this.doc?.update();
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

    // Experience: the bar shows tenths of the way from this level to the next (CMainFrameWindow); a master
    // past level 400 sees the master level's experience instead.
    const master = this.masterExperience?.() ?? null;
    const experience = master ? master.experience : s.experience, next = master ? master.next : s.nextExperience;
    const lower = master ? master.start : experienceForLevel(s.level);
    const upper = Math.max(lower, next);
    const ratio = upper > lower ? Math.min(1, Math.max(0, (Math.min(Math.max(experience, lower), upper) - lower) / (upper - lower))) : 0;
    const scaled = ratio * 10;
    let digit = Math.min(9, Math.max(0, Math.floor(scaled)));
    let progress = scaled - Math.floor(scaled);
    if (ratio >= 1) { digit = 9; progress = 1; }
    m.exp_fraction = progress;
    m.exp_digit = String(digit);
    m.exp_tooltip = `EXP: ${experience}/${next}`;
    this.refreshItemSlots();
    this.refreshSkills();
    this.doc.update();
    this.syncHints();
  }

  /** The master experience the bar shows instead (IsMasterExperienceActive), or null for the normal one. */
  setMasterExperience(source: () => MasterExperience | null): void {
    this.masterExperience = source;
    this.refresh();
  }

  /** Shows the skill list on the bar (call once, with the character's skills). */
  setSkills(skills: HudSkills): void {
    this.skills = skills;
    skills.list.onChange(() => this.refreshSkills());
    this.refreshSkills();
  }

  private iconOf(index: number): string {
    if (!this.skills) return 'none';
    const skill = index >= 120 && index < 124 ? index : this.skills.list.skillAt(index);
    const sprite = skillIconSprite(skill, this.skills.usable(skill));
    return sprite ? `image(${sprite})` : 'none';
  }

  // SyncRmlModel's skill part: the row's icons and hotkeys, the current skill, the popup's cells.
  refreshSkills(): void {
    if (!this.doc || !this.skills) return;
    const list = this.skills.list, m = this.model, pet = this.skills.hasPet();
    m.skill_list_up = list.listUp;
    for (let slot = 0; slot < 5; slot++) {
      const hotkey = list.slotHotkey(slot, pet);
      m[`skill_slot_${slot}_icon`] = hotkey >= 0 ? this.iconOf(list.hotkeys[hotkey]) : 'none';
      m[`skill_slot_${slot}_hotkey`] = hotkey >= 0 ? String(hotkey) : '';
      m[`skill_slot_${slot}_selected`] = hotkey >= 0 && list.hotkeys[hotkey] === list.current;
      m[`skill_slot_${slot}_cooldown`] = hotkey >= 0 ? skillCooldowns.fraction(list.skillAt(list.hotkeys[hotkey])) : 0;
    }
    const any = list.count > 0;
    m.current_skill_icon = any ? this.iconOf(list.current) : 'none';
    const currentHotkey = list.hotkeyOf(list.current);
    m.current_skill_hotkey = any && currentHotkey >= 0 ? String(currentHotkey) : '';
    m.current_skill_cooldown = any ? skillCooldowns.fraction(list.skillAt(list.current)) : 0;
    const cell = (index: number): SkillCell => {
      const hotkey = list.hotkeyOf(index);
      return { skill_index: index, is_current: index === list.current, icon: this.iconOf(index), hotkey: hotkey >= 0 ? String(hotkey) : '', cooldown_fraction: index < 120 ? skillCooldowns.fraction(list.skillAt(index)) : 0 };
    };
    m.skill_grid_cells = m.skill_grid_open ? list.gridIndexes().map(cell) : [];
    m.pet_skill_cells = m.skill_grid_open && pet ? [120, 121, 122, 123].map(cell) : [];
    this.doc.update();
  }

  /** Every frame while a skill cools down: the wipes shrink (and vanish once it's ready). */
  private cooling = false;
  updateCooldowns(): void {
    if (!skillCooldowns.active && !this.cooling) return;
    this.cooling = skillCooldowns.active;
    this.refreshSkills();
  }

  /** The list index of a hotkey row slot's skill, -1 for none. */
  private slotIndex(slot: number): number {
    if (!this.skills) return -1;
    const hotkey = this.skills.list.slotHotkey(slot, this.skills.hasPet());
    return hotkey < 0 ? -1 : this.skills.list.hotkeys[hotkey];
  }

  // QueueTooltip: the hovered skill's tooltip above its slot, anchored as the desktop anchors it.
  private skillTooltip(index: number, element: Element | null, kind: 'slot' | 'current' | 'grid'): void {
    if (!this.skills?.tooltip || index < 0 || !element) return;
    const box = element.getBoundingClientRect();
    const unit = hudScale();
    const anchor: SkillTooltipAnchor = kind === 'current'
      ? { x: box.left + (SLOT_TOOLTIP_OFFSET_X - CURRENT_SKILL_BOX_INSET_X) * unit, y: box.top - (CURRENT_SKILL_BOX_INSET_Y + TOOLTIP_GAP_ABOVE) * unit }
      : { x: box.left + (kind === 'grid' ? GRID_TOOLTIP_OFFSET_X : SLOT_TOOLTIP_OFFSET_X) * unit, y: box.top - TOOLTIP_GAP_ABOVE * unit };
    this.skills.tooltip(index, anchor);
  }

  // OnHotkeySlotClick: the slot's skill becomes the current one.
  private skillSlotClick(slot: number): void {
    if (!this.skills) return;
    const list = this.skills.list;
    const hotkey = list.slotHotkey(slot, this.skills.hasPet());
    if (hotkey < 0) return;
    const index = list.hotkeys[hotkey];
    const skill = list.skillAt(index);
    if (skill === 0 || (skill >= 67 && skill <= 72)) return;
    list.select(index);
  }

  // OnGridCellClick / OnPetCellClick: pick the skill and close the popup.
  private pickSkill(index: number): void {
    if (!this.skills) return;
    this.model.skill_grid_open = false;
    this.skills.list.select(index);
  }

  /** The potion slots: their items, counts and icons (call when the bag or the bindings change). */
  refreshItemSlots(): void {
    if (!this.doc || !this.itemSlots) return;
    for (let slot = 0; slot < 4; slot++) {
      const count = this.itemSlots.count(slot);
      this.model[`item_slot_${slot}_count`] = count > 0 ? String(count) : '';
      this.model[`item_slot_${slot}_hovered`] = this.hoveredSlot === slot;
      const item = this.itemSlots.item(slot);
      const canvas = this.slotCanvases[slot];
      if (!canvas) continue;
      const signature = item ? `${item.group}:${item.number}:${item.level}` : '';
      canvas.style.display = item ? 'block' : 'none';
      if (signature !== this.slotSignatures[slot]) {
        this.slotSignatures[slot] = signature;
        if (item) this.drawSlot(slot, item, null);
      }
    }
    this.doc.update();
  }

  private drawSlot(slot: number, item: Item, spin: number | null): void {
    const canvas = this.slotCanvases[slot];
    const size = Math.max(20, Math.round(canvas.getBoundingClientRect().width * window.devicePixelRatio) || 40);
    if (canvas.width !== size) { canvas.width = size; canvas.height = size; }
    void drawItem(canvas, item, { spin, time: performance.now() / 1000 });
  }

  private hoverSlot(slot: number): void {
    const previous = this.hoveredSlot;
    this.hoveredSlot = slot;
    this.spinStart = performance.now();
    const item = previous >= 0 ? this.itemSlots?.item(previous) : null;
    if (item) this.drawSlot(previous, item, null);
    this.refreshItemSlots();
    if (slot >= 0) requestAnimationFrame(() => this.spin(slot));
  }

  private spin(slot: number): void {
    if (this.hoveredSlot !== slot) return;
    const item = this.itemSlots?.item(slot);
    if (item) this.drawSlot(slot, item, (performance.now() - this.spinStart) / 1000);
    requestAnimationFrame(() => this.spin(slot));
  }
}
