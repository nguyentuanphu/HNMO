// What the field event windows (Kalima, Crywolf, Castle Siege) share: the desktop's RML documents
// docked at the right or laid on the 640x480 HUD board, their <img> art, their text sizes, the
// generic_confirm_dialog OK boxes, questions and the amount entry, and message_box_view's
// progress boxes (CProgressMsgBox). Nothing here knows an event; ui/fieldEventWindows.ts wires them.

import { loadImage, openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { nativeLineHeightPx, nativeTextPx, nativeTextPxInBox } from './nativeText.js';

export const PANEL_WIDTH = 190;   // the docked windows' width
const REFRESH_MS = 200;

export const screenScale = (): number => window.innerHeight / REFERENCE_HEIGHT;

/** A line of a docked window: what it says and its native size, shrunk to its box like NativeTextPxInBox. */
export interface SizedLine { text: string; text_px: number }

/** The line in the window's font at the native size, shrunk until it fits a box of `box` reference px (0: no box). */
export function sizedLine(text: string, bold = false, box = 0): SizedLine {
  if (!text) return { text: '', text_px: nativeTextPx() };
  return { text, text_px: nativeTextPxInBox(text, box * screenScale(), bold) };
}

/** Lays a document's root out as the 640x480 reference screen stretched to the window (a .stage / .hud-board). */
export function placeBoard(doc: RmlDocument): number {
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

/** A HUD board document (.screen.hud-board): the theme centres it at the bottom; the root is the 640x480 screen scaled to the window. */
export function centreScreen(doc: RmlDocument): void {
  placeBoard(doc);
}

// The themed documents load their art from Data/Interface/RmlUi/themes/<theme>/.
const IMAGE_BASE = '/data/Interface/RmlUi/themes/legacy/';
const imageUrls = new Map<string, Promise<string | null>>();

function croppedUrl(src: string, rect: string | null): Promise<string | null> {
  const url = new URL(src, `http://x${IMAGE_BASE}`).pathname;
  const key = `${url}#${rect ?? ''}`;
  if (!imageUrls.has(key)) {
    imageUrls.set(key, (async () => {
      const image = await loadImage(url);
      if (!image) return null;
      const [x, y, w, h] = rect ? rect.split(/\s+/).map(Number) : [0, 0, image.width, image.height];
      if (!(w > 0) || !(h > 0)) return null;
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(w));
      canvas.height = Math.max(1, Math.round(h));
      canvas.getContext('2d')?.drawImage(image, x, y, w, h, 0, 0, w, h);
      return canvas.toDataURL('image/png');
    })().catch(() => null));
  }
  return imageUrls.get(key)!;
}

/**
 * The documents' <img src rect> art (the game's .tga / .jpg, which browsers cannot show): each image
 * is cut to its rect and shown as the element's content. Call after each update.
 */
export function syncImages(doc: RmlDocument): void {
  for (const image of doc.root.querySelectorAll<HTMLImageElement>('img')) {
    const src = image.getAttribute('src');
    const rect = image.getAttribute('rect');
    const key = `${src ?? ''}#${rect ?? ''}`;
    if (image.dataset.art === key) continue;
    image.dataset.art = key;
    if (!src) { image.style.content = ''; continue; }
    image.style.content = 'none';
    void croppedUrl(src, rect).then((url) => {
      if (image.dataset.art === key) image.style.content = url ? `url("${url}")` : 'none';
    });
  }
}

/** Docks a document's #panel at the right edge, scaled like the other right-docked windows. */
export function dockRight(doc: RmlDocument): number {
  const panel = doc.$('#panel');
  const scale = screenScale();
  doc.setScale(scale);
  if (panel) {
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'auto',
    });
  }
  return scale;
}

/**
 * A docked NPC window of the desktop (layer depth 5): one RML document, its model filled by `sync`
 * before each update, Escape and the exit buttons close it; closing tells `onClose`.
 */
export class DockedWindow<M extends RmlModel> {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;

  constructor(private readonly name: string, readonly model: M, private readonly options: {
    sync: () => void;
    onClose?: () => void;
    click: () => void;
  }) {
    prepareDocument(name).catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.refresh());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { this.close(); this.options.click(); }
    });
    // The desktop syncs its windows every frame: the server's answers show without a click.
    setInterval(() => { if (this.doc) this.refresh(); }, REFRESH_MS);
  }

  get isOpen(): boolean { return this.wanted; }
  get document(): RmlDocument | null { return this.doc; }

  open(): Promise<void> {
    this.wanted = true;
    if (this.doc) { this.refresh(); return Promise.resolve(); }
    this.opening ??= openDocument(this.name, this.model, { parent: document.body, scale: screenScale() }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '30';
      doc.$('#panel')?.addEventListener('pointerdown', (event) => event.stopPropagation());
      const update = doc.update;
      this.model.__update = () => { this.options.sync(); update(); syncImages(doc); };
      this.refresh();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
    return this.opening;
  }

  /** Fills the model and redraws (the desktop's per-frame SyncRmlModel). */
  refresh(): void {
    if (!this.doc) return;
    dockRight(this.doc);
    this.model.__update?.();
  }

  close(notify = true): void {
    const was = this.wanted;
    this.wanted = false;
    this.doc?.close();
    this.doc = null;
    if (was && notify) this.options.onClose?.();
  }
}

// ---------- the OK boxes, questions and the amount entry (CGenericConfirmDialog) ----------

interface DialogLine { text: string; bold: boolean; color: string }
interface DialogEntry { lines: DialogLine[]; cancellable: boolean; input: boolean; done: (accepted: boolean, input: string) => boolean | void }
interface DialogModel extends RmlModel {
  lines: DialogLine[]; show_cancel: boolean; primary_label: string; cancel_label: string; has_input: boolean; input_value: string;
  gcd_primary_click: () => void; gcd_cancel_click: () => void;
}

/** One box at a time, the others wait in turn; never blocks a packet handler. */
export class FieldDialogs {
  private doc: RmlDocument | null = null;
  private readonly queue: DialogEntry[] = [];
  private current: DialogEntry | null = null;
  private readonly model: DialogModel;

  constructor(private readonly click: () => void) {
    this.model = {
      has_tall_panel: false, canvas_top: 0, has_portrait2d_overlay: false, has_title: false, title: '', severity_warning: false, severity_error: false,
      has_item3d: false, has_portrait2d_beside: false, portrait2d_text: '', has_input: false, input_is_keypad: false, input_text: '', input_value: '',
      has_progress: false, progress_fraction: 0, has_secondary: false, secondary_label: '', secondary_is_stock_max: false,
      primary_is_stock_ok: true, cancel_is_stock_cancel: true,
      lines: [], show_cancel: false, primary_label: 'OK', cancel_label: 'Cancel',
      gcd_primary_click: () => this.finish(true),
      gcd_cancel_click: () => this.finish(false),
    };
    prepareDocument('generic_confirm_dialog').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => { if (this.doc) { placeBoard(this.doc); this.doc.update(); } });
    window.addEventListener('keydown', (event) => {
      if (!this.current) return;
      if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); this.finish(true); }
      else if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); this.finish(false); }
    }, true);
  }

  get isOpen(): boolean { return this.current !== null; }
  get lines(): string[] { return this.model.lines.map((line) => line.text); }

  /** An OK box; `bold` lines are drawn in the bold font (a heading). */
  ok(lines: string[], bold: number[] = []): Promise<boolean> { return this.show(lines, false, false, bold).then(([accepted]) => accepted); }

  /** OK and Cancel. */
  ask(lines: string[], bold: number[] = []): Promise<boolean> { return this.show(lines, true, false, bold).then(([accepted]) => accepted); }

  /** OK and Cancel with a text field; `accept` returns false to keep the box open (KeepOpen). */
  askInput(lines: string[], accept: (text: string) => boolean): void {
    this.queue.push({ lines: lines.map((text) => ({ text, bold: false, color: '' })), cancellable: true, input: true, done: (ok, text) => (ok ? accept(text) : true) });
    if (!this.current) void this.next();
  }

  private show(lines: string[], cancellable: boolean, input: boolean, bold: number[]): Promise<[boolean, string]> {
    return new Promise((resolve) => {
      this.queue.push({ lines: lines.map((text, i) => ({ text, bold: bold.includes(i), color: '' })), cancellable, input, done: (ok, text) => resolve([ok, text]) });
      if (!this.current) void this.next();
    });
  }

  private async next(): Promise<void> {
    const entry = this.queue.shift();
    if (!entry) return;
    this.current = entry;
    this.model.lines = entry.lines;
    this.model.show_cancel = entry.cancellable;
    this.model.has_input = entry.input;
    this.model.input_value = '';
    if (!this.doc) {
      const doc = await openDocument('generic_confirm_dialog', this.model, { parent: document.body });
      this.doc = doc;
      doc.host.style.zIndex = '95';
      doc.$('#panel')?.addEventListener('pointerdown', (event) => event.stopPropagation());
      placeBoard(doc);
    }
    this.doc.update();
    if (entry.input) this.doc.$<HTMLInputElement>('#gcd_input')?.focus();
  }

  private finish(accepted: boolean): void {
    const entry = this.current;
    if (!entry) return;
    this.click();
    const text = this.doc?.$<HTMLInputElement>('#gcd_input')?.value ?? this.model.input_value;
    if (entry.done(accepted, text) === false) return; // KeepOpen
    this.current = null;
    if (this.queue.length === 0) { this.doc?.close(); this.doc = null; }
    void this.next();
  }
}

// ---------- the progress boxes (CProgressMsgBox) ----------

interface BoxModel extends RmlModel {
  kind: string; text_px: number; bold_text_px: number; line_height: number; bold_line_height: number;
  strips: { kind: string }[];
  lines: { role: string; bold: boolean; cells: { text: string; bold: boolean; color: string; text_px: number }[] }[];
  buttons: { label: string; index: number; enabled: boolean; ok_art: boolean }[];
  progress_shown: boolean; progress_width: number; list_shown: boolean; list_rows: { text: string; index: number; selected: boolean }[];
  message_box_button: (index: number) => void;
}

/** A message with a bar that runs out and closes it (the crown and seal notices); a new one replaces it. */
export class ProgressBox {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private start = 0;
  private duration = 0;
  private timer: ReturnType<typeof setInterval> | null = null;
  private readonly model: BoxModel = {
    kind: 'progress', text_px: nativeTextPx(), bold_text_px: nativeTextPx(), line_height: 14, bold_line_height: 14, strips: [], lines: [], buttons: [],
    progress_shown: true, progress_width: 0, list_shown: false, list_rows: [],
    message_box_button: () => this.close(),
  };

  constructor() {
    prepareDocument('message_box_view').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => { if (this.doc) { placeBoard(this.doc); this.doc.update(); } });
  }

  get isOpen(): boolean { return this.duration > 0; }
  get lines(): string[] { return this.model.lines.map((line) => line.cells.map((cell) => cell.text).join('')); }

  /** Shows the lines for `durationMs` (the desktop's Create(3000) / SetElapseTime). */
  show(lines: string[], durationMs: number): void {
    this.model.lines = lines.map((text) => ({ role: 'message', bold: false, cells: [{ text, bold: false, color: '', text_px: 0 }] }));
    // CProgressMsgBox::AddMsg: the box grows by a middle strip for each line past the second.
    this.model.strips = Array.from({ length: Math.max(0, lines.length - 2) }, () => ({ kind: 'middle' }));
    this.start = performance.now();
    this.duration = Math.max(1, durationMs);
    this.model.progress_width = 0;
    if (!this.timer) this.timer = setInterval(() => this.tick(), 100);
    if (this.doc) { this.doc.update(); return; }
    this.opening ??= openDocument('message_box_view', this.model, { parent: document.body }).then((doc) => {
      if (!this.isOpen) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '90';
      placeBoard(doc);
      doc.update();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  private tick(): void {
    const fraction = (performance.now() - this.start) / this.duration;
    if (fraction >= 1) { this.close(); return; }
    this.model.progress_width = 150 * fraction;
    this.doc?.update();
  }

  /** PopAllMessageBoxes. */
  close(): void {
    this.duration = 0;
    if (this.timer) { clearInterval(this.timer); this.timer = null; }
    this.doc?.close();
    this.doc = null;
  }
}

export { format, gold } from '../game/textFormat.js';
