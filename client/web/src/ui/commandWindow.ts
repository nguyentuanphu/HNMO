// The command window (CCommandWindow, command_window.rml), docked at the right: D opens and closes
// it (not in Chaos Castle). A button arms its command; the next right click on a player within two
// cells runs it (RunCommand): trade, buy from the store, party, whisper, guild, alliance, hostility,
// end hostility, add friend, follow, duel. While a command is armed the target box follows the
// pointer over a player, with the name in red when the player is too far. Escape and the X close it.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { nativeTextPx } from './nativeText.js';
import type { Character } from '../game/characters.js';

export const Command = {
  NONE: -1, TRADE: 0, PURCHASE: 1, PARTY: 2, WHISPER: 3, GUILD: 4, GUILDUNION: 5, RIVAL: 6, RIVALOFF: 7,
  ADD_FRIEND: 8, FOLLOW: 9, BATTLE: 10, SPECIAL: 11,
} as const;

// kCommandLabels (Game.en.resx).
const LABELS = ['Trade', 'Buy', 'Party', 'Whisper', 'Guild', 'Alliance', 'Hostility guild', 'Suspend hostilities', 'Add Friend', 'Follow', 'Duel', 'Special'];
const MAX_DISTANCE_TILE = 2;
const PANEL_WIDTH = 190; // COMMAND_WINDOW_WIDTH
const TARGET_OFFSET = 5; // kTargetBoxOffset

interface ButtonEntry { label: string; index: number; selected: boolean; label_text_px: number; label_line_px: number }
interface CommandModel extends RmlModel {
  big_text_px: number; title_text_px: number; title_line_px: number; title_text: string; exit_tooltip: string;
  buttons: ButtonEntry[];
  target_visible: boolean; target_left: number; target_top: number; target_name: string; target_in_range: boolean;
  command_press: (index: number) => void; command_exit: () => void;
}

/** What each command does with the chosen player (the game's own requests and checks). */
export interface CommandActions {
  trade(player: Character): void;
  purchase(player: Character): void;
  party(player: Character): void;
  whisper(player: Character): void;
  guild(player: Character): void;
  alliance(player: Character): void;
  rival(player: Character): void;
  rivalOff(player: Character): void;
  addFriend(player: Character): void;
  follow(player: Character): void;
  duel(player: Character): void;
}

export interface CommandWindowOptions {
  actions: CommandActions;
  hero: () => Character;
  /** The player under the pointer, if any. */
  playerAt: (event: MouseEvent) => Character | null;
  /** Whether the window may open here (not in Chaos Castle). */
  allowed: () => boolean;
  /** Opening it closes the other docked windows (HideAllGroupA). */
  onOpen?: () => void;
  onToggle?: (open: boolean) => void;
  click?: () => void;
  parent?: HTMLElement;
}

export class CommandWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private selected: number = Command.NONE;
  private readonly model: CommandModel;

  constructor(private readonly options: CommandWindowOptions) {
    this.model = {
      big_text_px: 12, title_text_px: 12, title_line_px: 14, title_text: 'Command Window', exit_tooltip: 'Close (D)',
      buttons: LABELS.map((label, index) => ({ label, index, selected: false, label_text_px: 12, label_line_px: 14 })),
      target_visible: false, target_left: 0, target_top: 0, target_name: '', target_in_range: false,
      command_press: (index) => this.press(index),
      command_exit: () => { this.close(); this.options.click?.(); },
    };
    prepareDocument('command_window').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen && !document.querySelector('.confirm-dialog')) { this.close(); this.options.click?.(); }
    });
    window.addEventListener('resize', () => this.place());
    window.addEventListener('pointermove', (event) => this.track(event), { passive: true });
  }

  get isOpen(): boolean { return this.doc !== null; }
  /** The armed command (Command.NONE when none). */
  get armed(): number { return this.isOpen ? this.selected : Command.NONE; }

  /** D (ToggleCommandWindow). */
  async toggle(): Promise<void> {
    if (this.isOpen) { this.close(); this.options.click?.(); return; }
    if (!this.options.allowed()) return;
    this.options.click?.();
    await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  private async load(): Promise<void> {
    this.options.onOpen?.();
    this.selected = Command.NONE; // OpenningProcess
    const doc = await openDocument('command_window', this.model, { parent: this.options.parent ?? document.body, scale: window.innerHeight / REFERENCE_HEIGHT });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    doc.host.style.zIndex = '26';
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.style.overflow = 'visible';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    }
    this.place();
    this.refresh();
    this.options.onToggle?.(true);
  }

  close(): void {
    if (!this.doc) return;
    this.selected = Command.NONE; // ClosingProcess
    this.doc.close();
    this.doc = null;
    this.options.onToggle?.(false);
  }

  // PressCommandButton: Special Commands would open the command list (not on the web yet); the others arm.
  private press(index: number): void {
    this.options.click?.();
    if (index === Command.SPECIAL) return;
    this.selected = index;
    this.refresh();
  }

  /**
   * A right click while a command is armed (RunCommand): a player within two cells gets the command; the
   * command is spent either way. True when the click was the command window's (no skill is cast then).
   */
  rightClick(event: MouseEvent): boolean {
    if (this.armed === Command.NONE) return false;
    const command = this.selected;
    this.selected = Command.NONE;
    this.model.target_visible = false;
    this.refresh();
    const player = this.options.playerAt(event);
    if (!player || !this.inRange(player)) return true;
    const a = this.options.actions;
    const run: Record<number, (p: Character) => void> = {
      [Command.TRADE]: a.trade, [Command.PURCHASE]: a.purchase, [Command.PARTY]: a.party, [Command.WHISPER]: a.whisper,
      [Command.GUILD]: a.guild, [Command.GUILDUNION]: a.alliance, [Command.RIVAL]: a.rival, [Command.RIVALOFF]: a.rivalOff,
      [Command.ADD_FRIEND]: a.addFriend, [Command.FOLLOW]: a.follow, [Command.BATTLE]: a.duel,
    };
    run[command]?.call(a, player);
    return true;
  }

  private inRange(player: Character): boolean {
    const hero = this.options.hero();
    return player !== hero && Math.abs(player.cell.x - hero.cell.x) <= MAX_DISTANCE_TILE && Math.abs(player.cell.y - hero.cell.y) <= MAX_DISTANCE_TILE;
  }

  // SyncTarget: the box at the pointer + 5 over a player while a command is armed.
  private track(event: PointerEvent): void {
    if (!this.doc) return;
    const player = this.armed !== Command.NONE ? this.options.playerAt(event) : null;
    const visible = !!player && player !== this.options.hero();
    if (!visible && !this.model.target_visible) return;
    this.model.target_visible = visible;
    if (player && visible) {
      const panel = this.doc.$('#panel')?.getBoundingClientRect();
      const scale = window.innerHeight / REFERENCE_HEIGHT;
      this.model.target_left = (event.clientX - (panel?.left ?? 0)) / scale + TARGET_OFFSET;
      this.model.target_top = (event.clientY - (panel?.top ?? 0)) / scale + TARGET_OFFSET;
      this.model.target_name = player.name;
      this.model.target_in_range = this.inRange(player);
    }
    this.doc.update();
  }

  private refresh(): void {
    const text = nativeTextPx();
    for (const button of this.model.buttons) {
      button.selected = button.index === this.selected;
      button.label_text_px = text;
      button.label_line_px = Math.round(text * 1.25);
    }
    this.model.title_text_px = text;
    this.model.title_line_px = Math.round(text * 1.25);
    this.model.big_text_px = nativeTextPx('big');
    this.doc?.update();
  }

  /** Docks it at the right edge, like the other docked windows. */
  place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top',
    });
    this.refresh();
  }
}
