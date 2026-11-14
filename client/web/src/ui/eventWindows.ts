// The event windows, the desktop's own RML documents (Data/Interface/RmlUi):
//   blood_castle_enter / devil_square_enter   CEnterBloodCastle / CEnterDevilSquare (EventEntryView), docked at the right
//   blood_castle_time / chaos_castle_time     CBloodCastle / CChaosCastleTime (EventTimerView), above the HUD's right end
//   hud_status                                the entry countdown line (CSBaseMatch::CountdownText)
//   message_box_view                          the results (CBloodCastleResultMsgBox, CChaosCastleResultMsgBox, CDevilSquareRankMsgBox)
//   generic_confirm_dialog                    the OK boxes and questions (CreateOkMessageBox, ShowConfirm)
// setUpEvents() wires them to the event state (game/events.ts) with one call from main.ts.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import {
  bloodCastleEntry, devilSquareEntry, EventMatches, T,
  type EntryContent, type EventHero, type MatchResult, type TimerView,
} from '../game/events.js';
import type { Connection } from '../net/connection.js';
import type { Item } from '../game/inventory.js';
import { nativeLineHeightPx, nativeTextPx } from './nativeText.js';

const PANEL_WIDTH = 190;   // the docked windows' width

const screenScale = () => window.innerHeight / REFERENCE_HEIGHT;

/** Lays a document's root out as the 640x480 reference screen stretched to the window (a .stage / .hud-board). */
function placeBoard(doc: RmlDocument): number {
  const scale = screenScale();
  // Counter-scaled text cancels the whole board's scale (rml.ts --board-scale).
  doc.setScale(scale);
  Object.assign(doc.root.style, {
    width: `${window.innerWidth / scale}px`, height: `${REFERENCE_HEIGHT}px`, transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none',
  });
  doc.root.style.setProperty('--ui-scale', '1');
  doc.root.style.setProperty('--hud-scale', '1');
  doc.root.style.setProperty('--board-scale', String(scale));
  return window.innerWidth / scale;
}

// ---------- the entry windows ----------

interface EntryModel extends RmlModel {
  text_px: number; bold_text_px: number; title_text: string; exit_tooltip: string;
  lines: { text: string }[]; buttons: { label: string; enabled: boolean }[];
  entry_press: (index: number) => void; entry_exit: () => void;
}

/** CEnterBloodCastle / CEnterDevilSquare: the level bands, only the hero's enabled; its click enters. */
export class EventEntryWindow {
  private doc: RmlDocument | null = null;
  kind: 'bloodCastle' | 'devilSquare' | null = null;
  content: EntryContent | null = null;
  private readonly model: EntryModel;

  constructor(private readonly options: {
    hero: () => EventHero;
    enter: (kind: 'bloodCastle' | 'devilSquare', active: number) => void;
    /** Told when it closes (ClosingProcess: CloseNpcRequest). */
    onClose: () => void;
    click: () => void;
  }) {
    this.model = {
      text_px: nativeTextPx(), bold_text_px: nativeTextPx(), title_text: '', exit_tooltip: T.Close, lines: [], buttons: [],
      entry_press: (index) => this.press(index),
      entry_exit: () => this.close(),
    };
    prepareDocument('blood_castle_enter').catch((error: unknown) => console.warn('Interface', error));
    prepareDocument('devil_square_enter').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { this.close(); this.options.click(); }
    });
  }

  get isOpen(): boolean { return this.doc !== null; }

  async open(kind: 'bloodCastle' | 'devilSquare'): Promise<void> {
    this.close(false);
    const content = kind === 'bloodCastle' ? bloodCastleEntry(this.options.hero()) : devilSquareEntry(this.options.hero());
    this.kind = kind;
    this.content = content;
    this.model.title_text = content.title;
    this.model.lines = content.lines.map((text) => ({ text }));
    this.model.buttons = content.buttons.map((button) => ({ ...button }));
    const doc = await openDocument(kind === 'bloodCastle' ? 'blood_castle_enter' : 'devil_square_enter', this.model, { parent: document.body, scale: screenScale() });
    if (this.kind !== kind || this.doc) { doc.close(); return; }
    this.doc = doc;
    doc.host.style.zIndex = '30';
    const panel = doc.$('#panel');
    panel?.addEventListener('pointerdown', (event) => event.stopPropagation());
    this.place();
  }

  /** The enabled button asks the server to enter (BtnProcess) and closes the window. */
  private press(index: number): void {
    const { kind, content } = this;
    if (!kind || !content || index !== content.active || !content.buttons[index]?.enabled) return;
    this.options.click();
    this.options.enter(kind, content.active);
    this.close();
  }

  close(notify = true): void {
    if (!this.doc) { this.kind = null; return; }
    this.doc.close();
    this.doc = null;
    this.kind = null;
    if (notify) this.options.onClose();
  }

  /** Docked at the right edge, like the other right-docked windows. */
  private place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = screenScale();
    this.doc.setScale(scale);
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'auto',
    });
    this.model.text_px = nativeTextPx();
    this.model.bold_text_px = nativeTextPx();
    this.doc.update();
  }
}

// ---------- the time window ----------

interface TimerModel extends RmlModel {
  text_px: number; big_text_px: number;
  kills_text: string; kills_state: string; time_left_text: string; time_left_state: string; time_text: string; time_state: string;
}

/** CBloodCastle / CChaosCastleTime: the kill count, "Time Left" and the time, red under five minutes. */
export class EventTimerHud {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private kind: TimerView['kind'] | null = null;
  private readonly model: TimerModel = {
    text_px: nativeTextPx(), big_text_px: nativeTextPx('big'), kills_text: '', kills_state: 'normal', time_left_text: T.TimeLeft, time_left_state: 'normal',
    time_text: '', time_state: 'normal',
  };

  constructor() {
    window.addEventListener('resize', () => this.place());
  }

  get shown(): boolean { return this.doc !== null; }

  show(view: TimerView | null): void {
    if (!view) { this.hide(); return; }
    if (this.kind !== view.kind) this.hide();
    this.model.kills_text = view.kills;
    this.model.time_text = view.time;
    this.model.time_state = view.imminent ? 'imminent' : 'normal';
    if (this.doc) { this.doc.update(); return; }
    if (this.opening) return;
    this.kind = view.kind;
    const name = view.kind === 'bloodCastle' ? 'blood_castle_time' : 'chaos_castle_time';
    this.opening = openDocument(name, this.model, { parent: document.body }).then((doc) => {
      if (this.kind !== view.kind) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '12';
      doc.host.style.pointerEvents = 'none';
      this.place();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  hide(): void {
    this.kind = null;
    this.doc?.close();
    this.doc = null;
  }

  // The workspace slot: (513, 348) of the 640x480 frame standing on the HUD.
  private place(): void {
    if (!this.doc) return;
    const width = placeBoard(this.doc);
    const screen = this.doc.$('.screen');
    if (screen) Object.assign(screen.style, { left: `${(width - 640) / 2}px`, top: '0px' });
    const panel = this.doc.$('#panel');
    if (panel) Object.assign(panel.style, { left: '513px', top: '348px' });
    this.doc.update();
  }
}

// ---------- the countdown line ----------

interface StatusModel extends RmlModel {
  text_px: number; switch_first: string; switch_second: string; macro_visible: boolean; macro_fraction: number; world_centre: number; countdown: string;
}

/** hud_status.rml's countdown line, centred above the HUD in (128, 128, 255) on a half-black box. */
export class CountdownLine {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private readonly model: StatusModel = { text_px: nativeTextPx(), switch_first: '', switch_second: '', macro_visible: false, macro_fraction: 0, world_centre: 320, countdown: '' };

  constructor() {
    window.addEventListener('resize', () => this.place());
  }

  get text(): string { return this.model.countdown; }

  set(text: string): void {
    if (text === this.model.countdown && (this.doc || !text)) return;
    this.model.countdown = text;
    if (this.doc) { this.doc.update(); return; }
    if (!text || this.opening) return;
    this.opening = openDocument('hud_status', this.model, { parent: document.body }).then((doc) => {
      this.doc = doc;
      doc.host.style.zIndex = '12';
      doc.host.style.pointerEvents = 'none';
      this.place();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  private place(): void {
    if (!this.doc) return;
    placeBoard(this.doc);
    this.doc.update();
  }
}

// ---------- the result boxes ----------

interface BoxModel extends RmlModel {
  kind: string; text_px: number; bold_text_px: number; line_height: number; bold_line_height: number;
  strips: { kind: string }[];
  lines: { role: string; bold: boolean; cells: { text: string; bold: boolean; color: string; text_px: number }[] }[];
  buttons: { label: string; index: number; enabled: boolean; ok_art: boolean }[];
  progress_shown: boolean; progress_width: number; list_shown: boolean; list_rows: { text: string; index: number; selected: boolean }[];
  message_box_button: (index: number) => void;
}

// The frames: six middle strips (Blood Castle, Chaos Castle); eleven, the divider, three (Devil Square).
function strips(kind: MatchResult['kind']): { kind: string }[] {
  if (kind !== 'devil-square-rank') return Array.from({ length: 6 }, () => ({ kind: 'middle' }));
  return [...Array.from({ length: 11 }, () => ({ kind: 'middle' })), { kind: 'divider' }, ...Array.from({ length: 3 }, () => ({ kind: 'middle' }))];
}

/** An event's result in message_box_view.rml; OK closes it. */
export class ResultBox {
  private doc: RmlDocument | null = null;
  private readonly model: BoxModel = {
    kind: '', text_px: nativeTextPx(), bold_text_px: nativeTextPx(), line_height: 14, bold_line_height: 14, strips: [], lines: [], buttons: [],
    progress_shown: false, progress_width: 0, list_shown: false, list_rows: [],
    message_box_button: () => this.close(),
  };

  constructor(private readonly click: () => void) {
    window.addEventListener('resize', () => { if (this.doc) { placeBoard(this.doc); this.doc.update(); } });
  }

  get isOpen(): boolean { return this.doc !== null; }

  async show(result: MatchResult): Promise<void> {
    this.model.kind = result.kind;
    this.model.strips = strips(result.kind);
    this.model.lines = result.lines.map((line) => ({ role: line.role, bold: false, cells: line.cells.map((c) => ({ ...c, text_px: 0 })) }));
    this.model.buttons = [{ label: 'OK', index: 0, enabled: true, ok_art: true }];
    if (this.doc) { this.doc.update(); return; }
    const doc = await openDocument('message_box_view', this.model, { parent: document.body });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    doc.host.style.zIndex = '90';
    doc.$('#panel')?.addEventListener('pointerdown', (event) => event.stopPropagation());
    placeBoard(doc);
    doc.update();
  }

  close(): void {
    if (!this.doc) return;
    this.click();
    this.doc.close();
    this.doc = null;
  }
}

// ---------- the OK boxes and questions ----------

interface DialogModel extends RmlModel {
  lines: { text: string; bold: boolean; color: string }[];
  show_cancel: boolean; primary_label: string; cancel_label: string;
  gcd_primary_click: () => void; gcd_cancel_click: () => void;
}

/** CGenericConfirmDialog: one box at a time, the others wait in turn; never blocks a packet handler. */
export class EventDialogs {
  private doc: RmlDocument | null = null;
  private readonly queue: { lines: string[]; cancellable: boolean; done: (accepted: boolean) => void }[] = [];
  private current: { done: (accepted: boolean) => void } | null = null;
  private readonly model: DialogModel;

  constructor(private readonly click: () => void) {
    this.model = {
      // Everything else the document binds stays off (no title, item, portrait, input or progress).
      has_tall_panel: false, canvas_top: 0, has_portrait2d_overlay: false, has_title: false, title: '', severity_warning: false, severity_error: false,
      has_item3d: false, has_portrait2d_beside: false, portrait2d_text: '', has_input: false, input_is_keypad: false, input_text: '', input_value: '',
      has_progress: false, progress_fraction: 0, has_secondary: false, secondary_label: '', secondary_is_stock_max: false,
      primary_is_stock_ok: true, cancel_is_stock_cancel: true,
      lines: [], show_cancel: false, primary_label: 'OK', cancel_label: 'Cancel',
      gcd_primary_click: () => this.finish(true),
      gcd_cancel_click: () => this.finish(false),
    };
    window.addEventListener('resize', () => { if (this.doc) { placeBoard(this.doc); this.doc.update(); } });
    window.addEventListener('keydown', (event) => {
      if (!this.current) return;
      if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); this.finish(true); }
      else if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); this.finish(false); }
    }, true);
  }

  get isOpen(): boolean { return this.current !== null; }
  get lines(): string[] { return this.model.lines.map((line) => line.text); }

  ok(text: string): void { void this.ask([text], false); }

  ask(lines: string[], cancellable = true): Promise<boolean> {
    return new Promise((done) => {
      this.queue.push({ lines, cancellable, done });
      if (!this.current) void this.next();
    });
  }

  private async next(): Promise<void> {
    const entry = this.queue.shift();
    if (!entry) return;
    this.current = entry;
    this.model.lines = entry.lines.map((text) => ({ text, bold: false, color: '' }));
    this.model.show_cancel = entry.cancellable;
    if (!this.doc) {
      const doc = await openDocument('generic_confirm_dialog', this.model, { parent: document.body });
      this.doc = doc;
      doc.host.style.zIndex = '95';
      doc.$('#panel')?.addEventListener('pointerdown', (event) => event.stopPropagation());
      placeBoard(doc);
    }
    this.doc.update();
  }

  private finish(accepted: boolean): void {
    const entry = this.current;
    if (!entry) return;
    this.click();
    this.current = null;
    if (this.queue.length === 0) { this.doc?.close(); this.doc = null; }
    entry.done(accepted);
    void this.next();
  }
}

// ---------- wiring ----------

/** What setUpEvents needs of the game (main.ts). */
export interface EventSetupOptions {
  connection: Connection;
  heroName: string;
  hero: () => EventHero;
  mapNumber: () => number | null;
  flags: () => Uint16Array | null;
  safeZone: () => boolean;
  message: (text: string) => void;
  audio: { play: (id: string, options?: { loop?: boolean }) => void; stop: (id: string) => void };
  /** HideAllGroupA: an entry window closes the inventory, the character, party and NPC windows. */
  closeOthers: () => void;
  /** The bag window: right-clicking the Armor of Guardsman, the Devil's Invitation or the Invisibility Cloak. */
  bag: { interceptRightClick?: (slot: number, item: Item) => boolean };
  /** The NPC window the server opened (inventory.openWindow): cleared when the entry window closes. */
  inventory: { openWindow: number | null };
}

export interface EventSystem {
  matches: EventMatches;
  entry: EventEntryWindow;
  timer: EventTimerHud;
  countdown: CountdownLine;
  result: ResultBox;
  dialogs: EventDialogs;
}

const MERCHANT_OR_VAULT = [0, 1, 2];
const FIRST_BAG_SLOT = 12;

export function setUpEvents(options: EventSetupOptions): EventSystem {
  const click = () => options.audio.play('SOUND_CLICK01');
  const dialogs = new EventDialogs(click);
  const result = new ResultBox(click);
  const timer = new EventTimerHud();
  const countdown = new CountdownLine();
  let matches: EventMatches | null = null;
  const entry = new EventEntryWindow({
    hero: options.hero,
    enter: (kind, active) => matches?.enter(kind, active),
    onClose: () => {
      options.connection.send('ClientToServer', 'CloseNpcRequest', {});
      if (options.inventory.openWindow === 4 || options.inventory.openWindow === 6) options.inventory.openWindow = null;
    },
    click,
  });
  matches = new EventMatches(options.connection, {
    mapNumber: options.mapNumber, flags: options.flags, heroName: options.heroName, hero: options.hero, safeZone: options.safeZone,
    ok: (text) => dialogs.ok(text),
    confirm: (lines) => dialogs.ask(lines),
    message: options.message,
    openEntry: (kind) => {
      // OpenningProcess: the other windows close and the NPC talk ends; the window stays.
      options.closeOthers();
      options.connection.send('ClientToServer', 'CloseNpcRequest', {});
      void entry.open(kind);
    },
    closeWindows: () => entry.close(false),
    showResult: (match) => { void result.show(match); },
    play: (id, loop = false) => options.audio.play(id, { loop }),
    stop: (id) => options.audio.stop(id),
  });
  const events = matches;
  // The event items in the bag, before wearing or dropping them (not while a merchant or the vault is open).
  const previous = options.bag.interceptRightClick;
  options.bag.interceptRightClick = (slot, item) => {
    if (previous?.(slot, item)) return true;
    if (slot < FIRST_BAG_SLOT || (options.inventory.openWindow !== null && MERCHANT_OR_VAULT.includes(options.inventory.openWindow))) return false;
    return events.useTicket(slot, item);
  };
  // Every frame, as the desktop renders them: the time window and the countdown line.
  const frame = () => {
    timer.show(events.timer());
    countdown.set(events.countdown());
    requestAnimationFrame(frame);
  };
  requestAnimationFrame(frame);
  return { matches: events, entry, timer, countdown, result, dialogs };
}
