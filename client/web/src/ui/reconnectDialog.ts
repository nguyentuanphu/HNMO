// The game's reconnect box (reconnect_dialog.rml in the legacy theme): a dim over the game, the 230 x 122 message box
// below the middle of the screen with the title "Connection lost", the step it is at, the seconds to the next try,
// the four-step gauge and Cancel. The texts are the game's own (the language files translate them). The world stays
// on screen under the dim while the server is looked for.
//
// The same document's gauge is the loading bar (ui/loadingProgress.ts), drawn without the box.

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { nativeTextPx } from './nativeText.js';

/** The box in reference pixels (reconnect_dialog.rcss). */
export const PANEL_WIDTH = 230;
export const PANEL_HEIGHT = 122;
/** The gauge's fill at its fullest (reference pixels). */
export const GAUGE_WIDTH = 150;
/** The dim over the world while it is shown (DIM_ALPHA), and over a page without it. */
const DIM_ALPHA = 0.45;

/** What the box says it is doing, in the order of a reconnect. */
export type ReconnectStep = 'probing' | 'connecting' | 'loggingIn' | 'characters' | 'joining';

const STEP_TEXT: Record<ReconnectStep, string> = {
  probing: 'Checking server availability',
  connecting: 'Connecting to the server',
  loggingIn: 'Logging in',
  characters: 'Loading character list',
  joining: 'Entering the game',
};

/** The gauge's step (of four): looking for the server and connecting share the first (the bar doesn't jump back). */
const STEP_INDEX: Record<ReconnectStep, number> = { probing: 1, connecting: 1, loggingIn: 2, characters: 3, joining: 4 };
export const STEP_COUNT = 4;

/**
 * The box's scale on a screen: the 640 x 480 layout's, and on a narrow screen (a phone held upright) as wide as the
 * screen allows with a 16 px margin each side, up to 1.3 times the original, so its texts and Cancel stay usable.
 */
export function boxScale(width: number, height: number): number {
  const layout = Math.min(width / 640, height / REFERENCE_HEIGHT);
  return Math.max(layout, Math.min((width - 32) / PANEL_WIDTH, 1.3));
}

/** Where the box goes (screen pixels): centred across, 100 reference pixels below the middle, always on the screen. */
export function boxPlace(width: number, height: number, scale: number): { x: number; y: number } {
  const w = PANEL_WIDTH * scale, h = PANEL_HEIGHT * scale;
  const y = (height - h) / 2 + 100 * (height / REFERENCE_HEIGHT);
  return { x: Math.round((width - w) / 2), y: Math.round(Math.max(8, Math.min(y, height - h - 8))) };
}

/** Opens reconnect_dialog.rml as a full-window layer (above every other window, as the game draws it last). */
export async function openReconnectDocument(model: RmlModel, zIndex: number): Promise<RmlDocument> {
  const doc = await openDocument('reconnect_dialog', model, { parent: document.body });
  Object.assign(doc.host.style, { position: 'fixed', inset: '0', pointerEvents: 'none', zIndex: String(zIndex) });
  Object.assign(doc.root.style, { width: '100vw', height: '100vh' });
  // The lines are laid out inside the scaled box here (not counter-scaled out of it): a box's width each.
  for (const line of doc.root.querySelectorAll<HTMLElement>('.line')) {
    for (const [name, value] of [['width', `${PANEL_WIDTH}px`], ['left', '0'], ['text-align', 'center'], ['transform', 'none']]) {
      line.style.setProperty(name, value, 'important');
    }
  }
  return doc;
}

/** The model's box place, scale and text sizes for the window's size now. */
export function placeModel(model: RmlModel, scale: number, place: { x: number; y: number }): void {
  model.root_x = place.x;
  model.root_y = place.y;
  model.root_scale = scale;
  // The native text size on the screen: in the scaled box, divided by its scale.
  model.text_px = +(nativeTextPx() / scale).toFixed(2);
  model.bold_text_px = model.text_px;
}

export interface ReconnectDialogOptions {
  /** Cancel (the box's button): stop trying. */
  onCancel: () => void;
}

/** The open box. */
export class ReconnectDialog {
  private doc: RmlDocument | null = null;
  private closed = false;
  private step: ReconnectStep = 'probing';
  private countdown = 0;
  private dim = DIM_ALPHA;
  readonly model: RmlModel;
  private readonly onResize = () => this.render();

  constructor(options: ReconnectDialogOptions) {
    this.model = {
      dim_alpha: DIM_ALPHA, root_x: 0, root_y: 0, root_scale: 1, text_px: 12, bold_text_px: 12,
      title_text: 'Connection lost', step_text: STEP_TEXT.probing, countdown_text: '', progress_width: 0,
      reconnect_cancel: () => { if (!this.closed) options.onCancel(); },
    };
    window.addEventListener('resize', this.onResize);
    void openReconnectDocument(this.model, 96).then((doc) => {
      if (this.closed) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('reconnect-dialog');
      doc.host.setAttribute('role', 'alertdialog');
      doc.$('#cancel')?.setAttribute('aria-label', 'Cancel');
      this.render();
    }).catch((error: unknown) => console.warn('Interface', error));
  }

  /** The step shown and the seconds to the next try (0: none). */
  set(step: ReconnectStep, countdown = 0): void {
    this.step = step;
    this.countdown = countdown;
    this.render();
  }

  /** The dim: light over the world, full over a page without it (after a reload, before the world is back). */
  setDim(over: 'world' | 'page'): void {
    this.dim = over === 'world' ? DIM_ALPHA : 1;
    this.render();
  }

  get currentStep(): ReconnectStep { return this.step; }

  close(): void {
    this.closed = true;
    window.removeEventListener('resize', this.onResize);
    this.doc?.close();
    this.doc = null;
  }

  private render(): void {
    const m = this.model;
    const scale = boxScale(window.innerWidth, window.innerHeight);
    placeModel(m, scale, boxPlace(window.innerWidth, window.innerHeight, scale));
    m.dim_alpha = this.dim;
    m.step_text = STEP_TEXT[this.step];
    m.countdown_text = this.countdown > 0 ? `Retrying in ${this.countdown} seconds...` : '';
    m.progress_width = (GAUGE_WIDTH * STEP_INDEX[this.step]) / STEP_COUNT;
    this.doc?.update();
  }
}
