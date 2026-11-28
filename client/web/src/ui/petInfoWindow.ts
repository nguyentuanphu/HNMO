import { panelKey } from '../game/keyboardShortcuts.js';
// The pet info window (CPetInfoWindow, pet_info.rml): opened from CharacterWindow's Pet button.
// Displays Dark Horse (slot 8, group 13 number 4) and Dark Raven / Dark Spirit (slot 1, group 13
// number 5) level, life, EXP, calculated damage/speed, charisma requirement, and Dark Raven commands.
import { openDocument, prepareDocument, type RmlDocument, type RmlModel } from './rml.js';
import { nativeTextPx, dockTop, uiScale } from './nativeText.js';
import { Equipment, type Inventory, type Item } from '../game/inventory.js';
import type { CharacterStats } from '../game/stats.js';
import type { Connection } from '../net/connection.js';

const PANEL_WIDTH = 190;
const PET_TYPE_DARK_RAVEN = 0;
const PET_TYPE_DARK_HORSE = 1;
const STORAGE_INVENTORY = 0;
const ITEM_GROUP_HELPER = 13;
const ITEM_DARK_HORSE = 4;
const ITEM_DARK_RAVEN = 5;

interface PetState {
  level: number;
  experience: number;
  health: number;
}

interface PetInfoModel extends RmlModel {
  text_px: number;
  active_tab: number;
  window_title: string;
  tab_darkhorse_label: string;
  tab_darkspirit_label: string;
  exit_tooltip: string;
  dh_has_pet: boolean;
  dh_no_pet_text: string;
  dh_level_text: string;
  dh_life_text: string;
  dh_hp_percent: number;
  dh_exp_text: string;
  dh_dmg_text: string;
  dh_atkspeed_text: string;
  ds_has_pet: boolean;
  ds_no_pet_text: string;
  ds_level_text: string;
  ds_life_text: string;
  ds_hp_percent: number;
  ds_exp_text: string;
  ds_dmg_text: string;
  ds_atkspeed_text: string;
  ds_charisma_text: string;
  commands_label: string;
  skill_basicaction_label: string;
  skill_randomattack_label: string;
  skill_attackwithowner_label: string;
  skill_attacktarget_label: string;
  petinfo_select_tab: (tab: number) => void;
  window_close: () => void;
}

export interface PetInfoWindowOptions {
  connection: Connection;
  earlyPackets: readonly Uint8Array[];
  inventory: Inventory;
  stats: CharacterStats;
  click?: () => void;
  onOpen?: () => void;
  onToggle?: (open: boolean) => void;
}

function isPetItem(item: Item | undefined, number: number): boolean {
  return Boolean(item && item.group === ITEM_GROUP_HELPER && item.number === number);
}

function expForNextLevel(level: number): number {
  const next = level + 1;
  return (10 + next) * next * next * next * 100;
}

/** CPetInfoWindow (`pet_info.rml`), docked left of CharacterWindow when CharacterWindow's Pet button is clicked. */
export class PetInfoWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private activeTab = 0;
  private readonly states: Record<number, PetState> = {
    [PET_TYPE_DARK_HORSE]: { level: 1, experience: 0, health: 255 },
    [PET_TYPE_DARK_RAVEN]: { level: 1, experience: 0, health: 255 },
  };
  private readonly model: PetInfoModel;
  private readonly onResize = () => this.place();
  private readonly onKeyDown = (event: KeyboardEvent) => {
    if (panelKey(event, 'Escape') && this.isOpen) {
      this.close();
      this.options.click?.();
    }
  };

  constructor(private readonly options: PetInfoWindowOptions) {
    this.model = {
      text_px: nativeTextPx(),
      active_tab: 0,
      window_title: 'Pet',
      tab_darkhorse_label: 'Dark Horse',
      tab_darkspirit_label: 'Dark Spirit',
      exit_tooltip: 'Close',
      dh_has_pet: false,
      dh_no_pet_text: 'No Dark Horse.',
      dh_level_text: '',
      dh_life_text: '',
      dh_hp_percent: 0,
      dh_exp_text: '',
      dh_dmg_text: '',
      dh_atkspeed_text: '',
      ds_has_pet: false,
      ds_no_pet_text: 'No Pet',
      ds_level_text: '',
      ds_life_text: '',
      ds_hp_percent: 0,
      ds_exp_text: '',
      ds_dmg_text: '',
      ds_atkspeed_text: '',
      ds_charisma_text: '',
      commands_label: 'Commands',
      skill_basicaction_label: 'Basic Action',
      skill_randomattack_label: 'Random Automatic Attack',
      skill_attackwithowner_label: 'Attack with Owner',
      skill_attacktarget_label: 'Attack Target',
      petinfo_select_tab: (tab: number) => this.selectTab(tab),
      window_close: () => { this.options.click?.(); this.close(); },
    };
    prepareDocument('pet_info').catch((error: unknown) => console.warn('Interface', error));
    for (const pkt of options.earlyPackets) this.handlePacket(pkt);
    options.connection.onPacket((pkt) => this.handlePacket(pkt));
    options.inventory.onChange(() => {
      if (this.isOpen) {
        this.requestPetInfo();
        this.refresh();
      }
    });
    window.addEventListener('resize', this.onResize);
    window.addEventListener('keydown', this.onKeyDown);
  }

  get isOpen(): boolean { return this.doc !== null; }

  async toggle(): Promise<void> {
    this.options.click?.();
    if (this.isOpen) this.close();
    else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    this.options.onToggle?.(false);
  }

  private selectTab(tab: number): void {
    if (tab !== 0 && tab !== 1) return;
    this.options.click?.();
    this.activeTab = tab;
    this.model.active_tab = tab;
    this.doc?.update();
  }

  private handlePacket(bytes: Uint8Array): void {
    // PetInfoResponse: C1, opcode 0xA9 (169), length >= 13
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xa9 || bytes.length < 13) return;
    const petType = bytes[3];
    const storage = bytes[4];
    if (storage !== STORAGE_INVENTORY) return;
    const level = Math.max(1, bytes[6]);
    const exp = new DataView(bytes.buffer, bytes.byteOffset + 8, 4).getUint32(0, true);
    const health = bytes[12];
    if (petType === PET_TYPE_DARK_HORSE || petType === PET_TYPE_DARK_RAVEN) {
      this.states[petType] = { level, experience: exp, health };
      if (this.isOpen) this.refresh();
    }
  }

  private requestPetInfo(): void {
    const items = this.options.inventory.items;
    if (isPetItem(items.get(Equipment.PET), ITEM_DARK_HORSE)) {
      this.options.connection.send('ClientToServer', 'PetInfoRequest', {
        Pet: PET_TYPE_DARK_HORSE, Storage: STORAGE_INVENTORY, ItemSlot: Equipment.PET,
      });
    }
    if (isPetItem(items.get(Equipment.LEFT_HAND), ITEM_DARK_RAVEN)) {
      this.options.connection.send('ClientToServer', 'PetInfoRequest', {
        Pet: PET_TYPE_DARK_RAVEN, Storage: STORAGE_INVENTORY, ItemSlot: Equipment.LEFT_HAND,
      });
    }
  }

  private async load(): Promise<void> {
    this.options.onOpen?.();
    this.requestPetInfo();
    this.refresh();
    const doc = await openDocument('pet_info', this.model, { parent: document.body, scale: uiScale() });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
      panel.addEventListener('contextmenu', (event) => event.preventDefault());
    }
    for (const tab of doc.root.querySelectorAll('.pet-tab')) {
      (tab as HTMLElement).style.pointerEvents = 'none';
    }
    const tabRow = doc.$('.tab-row');
    if (tabRow) tabRow.style.zIndex = '2';
    for (const id of ['#btn_exit', '#frame_corner_close']) {
      const btn = doc.$(id);
      if (btn) btn.style.pointerEvents = 'auto';
    }
    this.place();
    this.options.onToggle?.(true);
  }

  private refresh(): void {
    const { inventory, stats } = this.options;
    const items = inventory.items;
    const m = this.model;
    m.text_px = nativeTextPx();
    m.active_tab = this.activeTab;

    const horseItem = items.get(Equipment.PET);
    m.dh_has_pet = isPetItem(horseItem, ITEM_DARK_HORSE);
    if (m.dh_has_pet) {
      const st = this.states[PET_TYPE_DARK_HORSE];
      const dmgMin = Math.floor(stats.strength / 10) + Math.floor(stats.leadership / 10) + st.level * 5;
      const dmgMax = dmgMin + Math.floor(dmgMin / 2);
      const speed = 20 + Math.floor((st.level * 4) / 5) + Math.floor(stats.leadership / 50);
      const rate = 1000 + st.level + st.level * 15;
      const life = horseItem?.durability ?? st.health;
      m.dh_level_text = `Level: ${st.level}`;
      m.dh_life_text = `Life: ${life} / 255`;
      m.dh_hp_percent = (Math.min(life, 255) * 100) / 255;
      m.dh_exp_text = `EXP: ${st.experience} / ${expForNextLevel(st.level)}`;
      m.dh_dmg_text = `Dmg(rate): ${dmgMin}~${dmgMax} (${rate})`;
      m.dh_atkspeed_text = `Attack speed: ${speed}`;
    }

    const ravenItem = items.get(Equipment.LEFT_HAND);
    m.ds_has_pet = isPetItem(ravenItem, ITEM_DARK_RAVEN);
    if (m.ds_has_pet) {
      const st = this.states[PET_TYPE_DARK_RAVEN];
      const dmgMin = 180 + st.level * 15 + Math.floor(stats.leadership / 8);
      const dmgMax = 200 + st.level * 15 + Math.floor(stats.leadership / 4);
      const speed = 20 + Math.floor((st.level * 4) / 5) + Math.floor(stats.leadership / 50);
      const rate = 1000 + st.level + st.level * 15;
      const life = ravenItem?.durability ?? st.health;
      m.ds_level_text = `Level: ${st.level}`;
      m.ds_life_text = `Life: ${life} / 255`;
      m.ds_hp_percent = (Math.min(life, 255) * 100) / 255;
      m.ds_exp_text = `EXP: ${st.experience} / ${expForNextLevel(st.level)}`;
      m.ds_dmg_text = `Dmg(rate): ${dmgMin}~${dmgMax} (${rate})`;
      m.ds_atkspeed_text = `Attack speed: ${speed}`;
      m.ds_charisma_text = `Charisma Requirement: ${185 + st.level * 15}`;
    }
    this.doc?.update();
  }

  place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = uiScale();
    this.doc.setScale(scale);
    // Docks in slot 2 immediately left of CharacterWindow (slot 1 at right edge).
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * 2 * scale}px`,
      top: `${dockTop()}px`,
      transform: `scale(${scale})`,
      transformOrigin: 'left top',
    });
    this.refresh();
  }
}
