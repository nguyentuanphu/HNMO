// The loading bar while World 74 (the server and character choice's scene) and then the game world load: the game's
// gauge (reconnect_dialog.rml's newui_Bar_switch frame and fill) near the bottom of the screen, with what is loading
// and how far it is in percent. It follows the real loads (loadProgress.ts LoadMeter: the data files asked for and the
// ones that came, by stage). The host carries the numbers for tests (data-fraction, data-done, data-total).

import type { RmlDocument, RmlModel } from './rml.js';
import { currentLanguage } from '../i18n.js';
import type { LoadReading } from '../loadProgress.js';
import { boxScale, GAUGE_WIDTH, openReconnectDocument, placeModel } from './reconnectDialog.js';

/** The parts of reconnect_dialog.rml the bar doesn't draw: the dim, the message box and Cancel. */
const HIDDEN_PARTS = ['#dim', '#back', '#box_top', '#box_middle', '#box_bottom', '#cancel', '#rc_title'];
/** The lowest reference line the bar draws (the gauge's bottom edge, reconnect_dialog.rcss). */
const BAR_BOTTOM = 84;
/** The highest one (the step line's top). */
const BAR_TOP = 30;

/** "Loading" in the player's language (the bar's own word; the map names are the game's). */
export function loadingWord(): string {
  return currentLanguage() === 'vi' ? 'Đang tải' : 'Loading';
}

/** The bars open now (the page's own status bar hides while there is one). */
let openBars = 0;

export class LoadingProgress {
  private doc: RmlDocument | null = null;
  private closed = false;
  private label: string;
  private reading: LoadReading = { fraction: 0, done: 0, total: 0 };
  private readonly model: RmlModel;
  private readonly onResize = () => this.render();

  /** label: what is loading ("Loading Lorencia"); zIndex: over the loading picture (90) or the login screen. */
  constructor(label: string, zIndex = 91) {
    this.label = label;
    this.model = {
      dim_alpha: 0, root_x: 0, root_y: 0, root_scale: 1, text_px: 12, bold_text_px: 12,
      title_text: '', step_text: label, countdown_text: '0%', progress_width: 0,
      reconnect_cancel: () => undefined,
    };
    window.addEventListener('resize', this.onResize);
    // The page's own status line and bar (#loading) give way while the game's bar shows (index.html).
    openBars++;
    document.body.classList.add('loading-bar');
    void openReconnectDocument(this.model, zIndex).then((doc) => {
      if (this.closed) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('loading-progress');
      doc.host.setAttribute('role', 'progressbar');
      doc.host.setAttribute('aria-valuemin', '0');
      doc.host.setAttribute('aria-valuemax', '100');
      for (const selector of HIDDEN_PARTS) {
        const part = doc.$(selector);
        if (part) part.style.display = 'none';
      }
      this.render();
    }).catch((error: unknown) => console.warn('Interface', error));
  }

  /** What is loading now. */
  setLabel(label: string): void {
    this.label = label;
    this.render();
  }

  /** How far the load is (a LoadMeter's reading). */
  set(reading: LoadReading): void {
    this.reading = reading;
    this.render();
  }

  get fraction(): number { return this.reading.fraction; }

  close(): void {
    if (!this.closed && --openBars === 0) document.body.classList.remove('loading-bar');
    this.closed = true;
    window.removeEventListener('resize', this.onResize);
    this.doc?.close();
    this.doc = null;
  }

  private render(): void {
    const width = window.innerWidth, height = window.innerHeight;
    const scale = boxScale(width, height);
    // The bar's lines and gauge sit over the bottom of the screen (the loading picture's lower band).
    const y = height - 16 - BAR_BOTTOM * scale;
    placeModel(this.model, scale, { x: Math.round((width - 230 * scale) / 2), y: Math.round(Math.max(8 - BAR_TOP * scale, y)) });
    const percent = Math.round(this.reading.fraction * 100);
    this.model.step_text = this.label;
    this.model.countdown_text = this.reading.total > 0 ? `${percent}% (${this.reading.done}/${this.reading.total})` : `${percent}%`;
    this.model.progress_width = GAUGE_WIDTH * this.reading.fraction;
    const host = this.doc?.host;
    if (host) {
      host.dataset.fraction = this.reading.fraction.toFixed(3);
      host.dataset.done = String(this.reading.done);
      host.dataset.total = String(this.reading.total);
      host.setAttribute('aria-valuenow', String(percent));
    }
    this.doc?.update();
  }
}
