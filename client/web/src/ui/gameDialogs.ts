import { panelKey } from '../game/keyboardShortcuts.js';
// The game's small dialogs, drawn with the desktop's own interface documents:
//   generic_confirm_dialog  CGenericConfirmDialog: OK boxes, questions, a text field, a title, an item
//   generic_menu_dialog     CGenericMenuDialog: a title and a list of buttons with their own lines
//                           (the chaos machine's "Select method of combination", GenericMenuConfig kind)
//   message_box_view        MessageBoxView: the native boxes the desktop still lays out by kind
//                           (the guild's appointment box)
// Each is one instance with a queue, as on the desktop: a dialog asked for while another is open waits
// its turn. Nothing here blocks the page, so packet handlers keep running while a box is open.
// The dp documents are laid out at the dp ratio (TypographyScale), their text in rem (the native size).

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { nativeLineHeightPx, nativeTextPx, typographyScale } from './nativeText.js';
import { drawItem } from './itemIcons.js';
import type { Item } from '../game/inventory.js';

// ---------- placement ----------

/** RmlDialogCanvas: the 640x480 canvas's top edge below the window top, in dp. */
function canvasTop(): number {
  const scale = typographyScale();
  return Math.max(0, (window.innerHeight - REFERENCE_HEIGHT * scale) / 2 / scale);
}

/** Lays a dp document over the window: its host is the window in dp, scaled by the dp ratio. */
function placeDp(doc: RmlDocument, zIndex: number): void {
  const scale = typographyScale();
  Object.assign(doc.host.style, {
    position: 'fixed', left: '0px', top: '0px', width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
    transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none', zIndex: String(zIndex),
  });
  doc.setScale(scale);
}

/**
 * Lays a .stage document (message_box_view) over the window: base.rcss centres the 640x480 stage and
 * scales it by --ui-scale, which the desktop sets to the dp ratio (RmlScaleInputs: TypographyScale).
 */
function placeStage(doc: RmlDocument): void {
  const scale = typographyScale();
  doc.setScale(scale);
  Object.assign(doc.root.style, { width: `${window.innerWidth}px`, height: `${window.innerHeight}px`, pointerEvents: 'none' });
  doc.root.style.setProperty('--ui-scale', String(scale));
  doc.root.style.setProperty('--hud-scale', String(scale));
  doc.root.style.setProperty('--board-scale', '1');
}

const warn = (error: unknown) => console.warn('Interface', error);

// ---------- CGenericConfirmDialog ----------

export interface DialogLine { text: string; bold?: boolean; color?: string }

export interface ConfirmConfig {
  /** The lines; a line's "\n" starts a new line. */
  lines: (string | DialogLine)[];
  /** A bold title on the box's title banner. */
  title?: string;
  severity?: 'normal' | 'warning' | 'error';
  primaryLabel?: string;
  /** A real second action (not Cancel); never answered by Escape. */
  secondaryLabel?: string;
  showCancel?: boolean;
  cancelLabel?: string;
  /** A text field (Mode::Text). */
  input?: { masked?: boolean; maxLength?: number; numericOnly?: boolean; initialText?: string };
  /** An item drawn left of the lines (item3D). */
  item?: Item | null;
  /** Names for the box (classes on its host), so tests and callers can find it. */
  tag?: string;
  /** OK: return false to keep the box open (KeepOpen), e.g. an invalid entry. */
  accept?: (text: string) => boolean | void;
}

export type ConfirmButton = 'primary' | 'secondary' | 'cancel';
export interface ConfirmAnswer { button: ConfirmButton; text: string }

interface ConfirmEntry { config: ConfirmConfig; resolve: (answer: ConfirmAnswer) => void }

interface ConfirmModel extends RmlModel {
  lines: { text: string; bold: boolean; color: string }[];
  has_title: boolean; title: string; severity_warning: boolean; severity_error: boolean;
  show_cancel: boolean; primary_label: string; cancel_label: string; has_secondary: boolean; secondary_label: string;
  has_input: boolean; input_value: string; has_item3d: boolean; canvas_top: number;
  gcd_primary_click: () => void; gcd_secondary_click: () => void; gcd_cancel_click: () => void;
}

const lineOf = (line: string | DialogLine): { text: string; bold: boolean; color: string }[] => {
  const value = typeof line === 'string' ? { text: line } : line;
  return value.text.split('\n').map((text) => ({ text: text || ' ', bold: value.bold ?? false, color: value.color ?? '' }));
};

class ConfirmDialog {
  private doc: RmlDocument | null = null;
  private opening: Promise<RmlDocument> | null = null;
  private readonly queue: ConfirmEntry[] = [];
  private current: ConfirmEntry | null = null;
  private itemCanvas: HTMLCanvasElement | null = null;
  readonly model: ConfirmModel = {
    has_tall_panel: false, has_portrait2d_overlay: false, has_portrait2d_beside: false, portrait2d_text: '',
    input_is_keypad: false, input_text: '', has_progress: false, progress_fraction: 0,
    primary_is_stock_ok: true, cancel_is_stock_cancel: true, secondary_is_stock_max: false,
    lines: [], has_title: false, title: '', severity_warning: false, severity_error: false,
    show_cancel: false, primary_label: 'OK', cancel_label: 'Cancel', has_secondary: false, secondary_label: '',
    has_input: false, input_value: '', has_item3d: false, canvas_top: 0,
    gcd_primary_click: () => this.finish('primary'),
    gcd_secondary_click: () => this.finish('secondary'),
    gcd_cancel_click: () => this.finish('cancel'),
  };

  constructor() {
    window.addEventListener('resize', () => this.place());
    // A true modal: Enter and Escape are the box's before any window's (GetKeyEventOrder 100).
    window.addEventListener('keydown', (event) => {
      if (!this.current) return;
      if (panelKey(event, 'Enter')) { event.preventDefault(); event.stopImmediatePropagation(); this.finish('primary'); }
      else if (panelKey(event, 'Escape')) { event.preventDefault(); event.stopImmediatePropagation(); this.finish('cancel', true); }
      else if (this.model.has_input) event.stopImmediatePropagation(); // typing stays in the field
    }, true);
  }

  get isOpen(): boolean { return this.current !== null; }
  get lines(): string[] { return this.model.lines.map((line) => line.text); }

  /** Closes the boxes with this tag, open or waiting, as if cancelled (their window or session ended). */
  dismiss(tag: string): void {
    for (let i = this.queue.length - 1; i >= 0; i--) {
      const entry = this.queue[i];
      if (entry?.config.tag !== tag) continue;
      this.queue.splice(i, 1);
      entry.resolve({ button: 'cancel', text: '' });
    }
    if (this.current?.config.tag === tag) this.finish('cancel', true);
  }

  show(config: ConfirmConfig): Promise<ConfirmAnswer> {
    return new Promise((resolve) => {
      this.queue.push({ config, resolve });
      if (!this.current) void this.next();
    });
  }

  private async next(): Promise<void> {
    const entry = this.queue.shift() ?? null;
    this.current = entry;
    if (!entry) { this.doc?.close(); this.doc = null; this.itemCanvas = null; return; }
    const c = entry.config;
    const m = this.model;
    m.lines = c.lines.flatMap(lineOf);
    m.has_title = Boolean(c.title);
    m.title = c.title ?? '';
    m.severity_warning = c.severity === 'warning';
    m.severity_error = c.severity === 'error';
    m.primary_label = c.primaryLabel ?? 'OK';
    m.primary_is_stock_ok = m.primary_label === 'OK'; // native's lettered OK art
    m.has_secondary = Boolean(c.secondaryLabel);
    m.secondary_label = c.secondaryLabel ?? '';
    m.show_cancel = c.showCancel ?? false;
    m.cancel_label = c.cancelLabel ?? 'Cancel';
    m.cancel_is_stock_cancel = m.cancel_label === 'Cancel';
    m.has_input = Boolean(c.input);
    m.input_value = c.input?.initialText ?? '';
    m.has_item3d = Boolean(c.item);
    m.canvas_top = canvasTop();
    // The next box of the queue reuses the open document; opening another one left it in the page for good, on top
    // of the box in use, where it caught the clicks meant for that box's buttons.
    if (!this.doc) this.opening ??= openDocument('generic_confirm_dialog', m, { parent: document.body }).then((doc) => {
      doc.root.addEventListener('pointerdown', (event) => { if (event.target !== doc.root) event.stopPropagation(); });
      // Test hooks and the callers' names for the buttons and the field.
      doc.$('#gcd_btn_primary')?.classList.add('confirm-ok');
      doc.$('#gcd_btn_cancel')?.classList.add('confirm-cancel');
      doc.$('#gcd_btn_secondary')?.classList.add('confirm-secondary');
      doc.$('.gcd-header-title')?.classList.add('message-title');
      const field = doc.$<HTMLInputElement>('#gcd_input');
      field?.classList.add('ask-input', 'social-dialog-input');
      field?.setAttribute('autocomplete', 'off');
      field?.addEventListener('input', () => {
        if (this.current?.config.input?.numericOnly) field.value = field.value.replace(/\D/g, '');
        this.model.input_value = field.value;
      });
      return doc;
    });
    const doc = this.doc ?? await (this.opening ?? Promise.resolve(null)).catch((error: unknown) => { warn(error); return null; });
    this.opening = null;
    if (!doc) { entry.resolve({ button: 'cancel', text: '' }); this.current = null; void this.next(); return; }
    if (this.current !== entry) return;
    this.doc = doc;
    doc.host.className = `rml-document rml-generic_confirm_dialog confirm-dialog game-dialog ${c.tag ?? ''}`.trim();
    doc.host.setAttribute('role', 'dialog');
    this.place();
    const field = doc.$<HTMLInputElement>('#gcd_input');
    if (field) {
      field.type = c.input?.masked ? 'password' : 'text';
      field.maxLength = c.input?.maxLength ?? 20;
      field.inputMode = c.input?.numericOnly ? 'numeric' : 'text';
      field.value = m.input_value;
    }
    this.drawItem(c.item ?? null);
    doc.update();
    if (c.input) field?.focus();
  }

  private drawItem(item: Item | null): void {
    const slot = this.doc?.$('#gcd_item3d_slot');
    if (!slot) return;
    this.itemCanvas?.remove();
    this.itemCanvas = null;
    if (!item) return;
    const canvas = document.createElement('canvas');
    const scale = typographyScale() * (window.devicePixelRatio || 1);
    canvas.width = Math.round(40 * scale); canvas.height = Math.round(40 * scale);
    Object.assign(canvas.style, { position: 'absolute', left: '0', top: '0', width: '100%', height: '100%', pointerEvents: 'none' });
    slot.append(canvas);
    this.itemCanvas = canvas;
    void drawItem(canvas, item);
  }

  private place(): void {
    if (!this.doc) return;
    placeDp(this.doc, 95);
    this.model.canvas_top = canvasTop();
    this.doc.update();
  }

  /** escape: Escape closes even a box without Cancel (its answer is then 'cancel'). */
  private finish(button: ConfirmButton, escape = false): void {
    const entry = this.current;
    if (!entry) return;
    if (button === 'secondary' && !this.model.has_secondary) return;
    if (button === 'cancel' && !this.model.show_cancel && !escape) return;
    const text = (this.doc?.$<HTMLInputElement>('#gcd_input')?.value ?? this.model.input_value).trim();
    if (button === 'primary' && entry.config.accept?.(text) === false) return; // KeepOpen
    this.current = null;
    entry.resolve({ button, text });
    void this.next();
  }
}

let confirmDialog: ConfirmDialog | null = null;
const confirms = (): ConfirmDialog => (confirmDialog ??= new ConfirmDialog());

/** CGenericConfirmDialog::Show: the box (now or after the open ones), and the button that closed it. */
export function showConfirm(config: ConfirmConfig): Promise<ConfirmAnswer> {
  return confirms().show(config);
}

/** Closes the confirm boxes with this tag, open or waiting, as cancelled. */
export function dismissConfirm(tag: string): void { confirmDialog?.dismiss(tag); }

/** Whether a confirm box is open (the game's hotkeys wait while it is). */
export function confirmOpen(): boolean { return confirmDialog?.isOpen ?? false; }

// ---------- CGenericMenuDialog ----------

export interface MenuButton { label: string; lines?: readonly (string | DialogLine)[]; compact?: boolean; enabled?: boolean; tag?: string }
export interface MenuConfig {
  /** GenericMenuConfig::kind: the native box it is laid out like ('chaos-mix', 'npc-menu', ...). */
  kind?: string;
  title?: string;
  highlightTitle?: boolean;
  lines?: readonly (string | DialogLine)[];
  buttons: MenuButton[];
  /** Names for the box (classes on its host). */
  tag?: string;
}

interface MenuModel extends RmlModel {
  kind: string; menu_id: string; title: string; has_title: boolean; highlight_title: boolean; is_system_menu: boolean; canvas_top: number;
  lines: { text: string; bold: boolean; color: string }[];
  buttons: { label: string; tooltip: string; lines: { text: string; bold: boolean; color: string }[]; has_lines: boolean; enabled: boolean; compact: boolean; cols2: boolean; narrow: boolean; lines_below: boolean }[];
  positioned: boolean; root_x: number; root_y: number;
  gmd_button_click: (index: number) => void;
  gmd_cancel: () => void;
}

interface MenuEntry { config: MenuConfig; resolve: (index: number | null) => void }

class MenuDialog {
  private doc: RmlDocument | null = null;
  private opening: Promise<RmlDocument> | null = null;
  private readonly queue: MenuEntry[] = [];
  private current: MenuEntry | null = null;
  readonly model: MenuModel = {
    kind: '', menu_id: '', title: '', has_title: false, highlight_title: false, is_system_menu: false, canvas_top: 0,
    lines: [], buttons: [], positioned: false, root_x: 0, root_y: 0,
    gmd_button_click: (index: number) => this.finish(index),
    gmd_cancel: () => this.finish(null),
  };

  constructor() {
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (!this.current || !panelKey(event, 'Escape')) return;
      event.preventDefault(); event.stopImmediatePropagation();
      this.finish(null);
    }, true);
  }

  get isOpen(): boolean { return this.current !== null; }

  show(config: MenuConfig): Promise<number | null> {
    return new Promise((resolve) => {
      this.queue.push({ config, resolve });
      if (!this.current) void this.next();
    });
  }

  private async next(): Promise<void> {
    const entry = this.queue.shift() ?? null;
    this.current = entry;
    if (!entry) { this.doc?.close(); this.doc = null; return; }
    const c = entry.config;
    const m = this.model;
    m.kind = c.kind ?? '';
    m.menu_id = c.kind ?? '';
    m.title = c.title ?? '';
    m.has_title = Boolean(c.title);
    m.highlight_title = c.highlightTitle ?? false;
    m.lines = (c.lines ?? []).flatMap(lineOf);
    m.buttons = c.buttons.map((button) => {
      const lines = (button.lines ?? []).flatMap(lineOf);
      return { label: button.label, tooltip: '', lines, has_lines: lines.length > 0, enabled: button.enabled ?? true, compact: button.compact ?? false, cols2: false, narrow: false, lines_below: false };
    });
    m.canvas_top = canvasTop();
    // The next box of the queue reuses the open document; opening another one left it in the page for good, on top
    // of the box in use, where it caught the clicks meant for that box's buttons.
    if (!this.doc) this.opening ??= openDocument('generic_menu_dialog', m, { parent: document.body }).then((doc) => {
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      return doc;
    });
    const doc = this.doc ?? await (this.opening ?? Promise.resolve(null)).catch((error: unknown) => { warn(error); return null; });
    this.opening = null;
    if (!doc) { entry.resolve(null); this.current = null; void this.next(); return; }
    if (this.current !== entry) return;
    this.doc = doc;
    doc.host.className = `rml-document rml-generic_menu_dialog choose-dialog game-dialog ${c.tag ?? ''}`.trim();
    doc.host.setAttribute('role', 'dialog');
    this.place();
    // Test hooks: each button by its index, the compact one is Close.
    doc.root.querySelectorAll<HTMLElement>('.gmd-btn').forEach((element, index) => {
      element.classList.remove(...[...element.classList].filter((name) => name.startsWith('choose-')));
      const button = c.buttons[index];
      element.classList.add(button?.compact ? 'choose-close' : 'choose-option', `choose-option-${index}`);
      if (button?.tag) element.classList.add(button.tag);
    });
  }

  private place(): void {
    if (!this.doc) return;
    placeDp(this.doc, 95);
    this.model.canvas_top = canvasTop();
    this.doc.update();
  }

  private finish(index: number | null): void {
    const entry = this.current;
    if (!entry) return;
    if (index !== null && !(entry.config.buttons[index]?.enabled ?? true)) return;
    this.current = null;
    // A compact button is the box's Close: the same as Escape.
    entry.resolve(index !== null && entry.config.buttons[index]?.compact ? null : index);
    void this.next();
  }
}

let menuDialog: MenuDialog | null = null;

/** CGenericMenuDialog::Show: the index of the button pressed; null for Close / Escape. */
export function showMenu(config: MenuConfig): Promise<number | null> {
  return (menuDialog ??= new MenuDialog()).show(config);
}

// ---------- MessageBoxView (the native boxes laid out by kind) ----------

interface BoxModel extends RmlModel {
  kind: string; text_px: number; bold_text_px: number; line_height: number; bold_line_height: number;
  strips: { kind: string }[];
  lines: { role: string; bold: boolean; cells: { text: string; bold: boolean; color: string; text_px: number }[] }[];
  buttons: { label: string; index: number; enabled: boolean; ok_art: boolean }[];
  progress_shown: boolean; progress_width: number; list_shown: boolean; list_rows: { text: string; index: number; selected: boolean }[];
  message_box_button: (index: number) => void;
}

export interface BoxConfig {
  kind: string;
  /** Middle strips of the frame (the box's height). */
  middles: number;
  lines: { text: string; bold?: boolean; color?: string; role?: string }[];
  buttons: string[];
  tag?: string;
}

/**
 * A message_box_view box: its lines and buttons; `onButton` answers a click and returns true to close
 * the box (the boxes that change their own lines, like the appointment box, return false and call update).
 */
export class MessageBox {
  private doc: RmlDocument | null = null;
  private readonly model: BoxModel;
  private readonly onResize = () => { if (this.doc) { placeStage(this.doc); this.doc.update(); } };
  private readonly onKey = (event: KeyboardEvent) => {
    if (!panelKey(event, 'Escape')) return;
    event.preventDefault(); event.stopImmediatePropagation();
    this.close();
    this.onEscape?.();
  };
  onEscape?: () => void;

  constructor(config: BoxConfig, private readonly onButton: (index: number) => boolean) {
    this.model = {
      kind: config.kind, text_px: nativeTextPx(), bold_text_px: nativeTextPx(), line_height: 0, bold_line_height: 0,
      strips: Array.from({ length: config.middles }, () => ({ kind: 'middle' })), lines: [], buttons: [],
      progress_shown: false, progress_width: 0, list_shown: false, list_rows: [],
      message_box_button: (index: number) => { if (this.onButton(index)) this.close(); },
    };
    this.set(config);
    void this.open(config.tag ?? '');
  }

  /** New lines and buttons (MessageBoxView::Sync). */
  set(config: Pick<BoxConfig, 'lines' | 'buttons'>): void {
    const m = this.model;
    const scale = typographyScale();
    m.text_px = nativeTextPx();
    m.bold_text_px = nativeTextPx();
    // The line advance in the box's reference px (the lines are counter-scaled into it).
    m.line_height = nativeLineHeightPx() / scale;
    m.bold_line_height = nativeLineHeightPx() / scale;
    m.lines = config.lines.map((line) => ({ role: line.role ?? 'message', bold: line.bold ?? false, cells: [{ text: line.text, bold: line.bold ?? false, color: line.color ?? '', text_px: 0 }] }));
    m.buttons = config.buttons.map((label, index) => ({ label, index, enabled: true, ok_art: false }));
    this.doc?.update();
  }

  private closed = false;
  get isOpen(): boolean { return !this.closed; }

  private async open(tag: string): Promise<void> {
    const doc = await openDocument('message_box_view', this.model, { parent: document.body }).catch((error: unknown) => { warn(error); return null; });
    if (!doc) return;
    if (this.closed) { doc.close(); return; }
    this.doc = doc;
    doc.host.className = `rml-document rml-message_box_view game-dialog ${tag}`.trim();
    doc.host.style.zIndex = '95';
    doc.$('#panel')?.addEventListener('pointerdown', (event) => event.stopPropagation());
    placeStage(doc);
    window.addEventListener('resize', this.onResize);
    window.addEventListener('keydown', this.onKey, true);
    doc.update();
  }

  close(): void {
    this.closed = true;
    window.removeEventListener('resize', this.onResize);
    window.removeEventListener('keydown', this.onKey, true);
    this.doc?.close();
    this.doc = null;
  }
}

/** Loads the dialogs' documents ahead of their first use. */
export function prepareGameDialogs(): void {
  for (const name of ['generic_confirm_dialog', 'generic_menu_dialog']) prepareDocument(name).catch(warn);
}

