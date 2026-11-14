// The duel windows, the desktop's own RML documents (Data/Interface/RmlUi):
//   generic_confirm_dialog   the duel questions over the duel art (ShowConfirm with a duelCaption)
//   duel_window              CDuelWindow: both duellists' names and scores above the HUD's right end
//   duel_watch               CDuelWatchWindow: Doorkeeper Titus's colosseums (NPC window 0x21), Watch
//   duel_watch_frame         CDuelWatchMainFrameWindow: the spectator's HUD (names, score marks, the
//                            life and shield gauges with their catch-up, the exit button)
//   duel_watch_spectators    CDuelWatchUserListWindow: the spectators of the watched duel
// The duel state is game/duel.ts.

import { loadImage, openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { DUEL_ENEMY, DUEL_HERO, DuelText, fill, type Duel } from '../game/duel.js';
import { nativeLineHeightPx, nativeTextPx } from './nativeText.js';

const PANEL_WIDTH = 190; // the docked windows' width

const screenScale = () => window.innerHeight / REFERENCE_HEIGHT;

/** Lays a document's root out as the 640x480 reference screen stretched to the window. */
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

/** A HUD document's .screen centred on the window and its #panel at a slot of the 640x480 frame. */
function placeHud(doc: RmlDocument, left: number, top: number): void {
  const width = placeBoard(doc);
  const screen = doc.$('.screen');
  if (screen) Object.assign(screen.style, { left: `${(width - 640) / 2}px`, top: '0px' });
  const panel = doc.$('#panel');
  if (panel) Object.assign(panel.style, { left: `${left}px`, top: `${top}px` });
  doc.update();
}

// ---------- the questions ----------

interface DialogModel extends RmlModel {
  lines: { text: string; bold: boolean; color: string }[]; show_cancel: boolean; primary_label: string; cancel_label: string;
  has_portrait2d_overlay: boolean; portrait2d_text: string;
  gcd_primary_click: () => void; gcd_cancel_click: () => void;
}

interface Question { caption: string; lines: string[]; cancellable: boolean; bold: number[]; resolve: (accepted: boolean) => void }

/**
 * The duel questions and results (CGenericConfirmDialog with the duel art); never blocks a packet handler.
 * A question asked while one is open waits for it, as CGenericConfirmDialog::Show queues them.
 */
export class DuelDialog {
  private doc: RmlDocument | null = null;
  private current: Question | null = null;
  private readonly queue: Question[] = [];
  private readonly model: DialogModel;

  constructor(private readonly click: () => void) {
    this.model = {
      has_tall_panel: false, canvas_top: 0, has_title: false, title: '', severity_warning: false, severity_error: false,
      has_item3d: false, has_portrait2d_beside: false, has_input: false, input_is_keypad: false, input_text: '', input_value: '',
      has_progress: false, progress_fraction: 0, has_secondary: false, secondary_label: '', secondary_is_stock_max: false,
      primary_is_stock_ok: true, cancel_is_stock_cancel: true,
      lines: [], show_cancel: false, primary_label: 'OK', cancel_label: 'Cancel', has_portrait2d_overlay: false, portrait2d_text: '',
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

  /** Shows a question (caption: the duel art's caption, '' for none; bold: the lines' indices shown bold). */
  ask(caption: string, lines: string[], cancellable: boolean, bold: number[] = []): Promise<boolean> {
    return new Promise((resolve) => {
      this.queue.push({ caption, lines, cancellable, bold, resolve });
      if (!this.current) this.next();
    });
  }

  /** Activate / ShowNext: the next question in line, or the box closes. */
  private next(): void {
    const question = this.queue.shift() ?? null;
    this.current = question;
    if (!question) { this.doc?.close(); this.doc = null; return; }
    this.model.lines = question.lines.map((text, index) => ({ text, bold: question.bold.includes(index), color: '' }));
    this.model.show_cancel = question.cancellable;
    // ShowConfirm: the duel art and its lines need the tall panel.
    this.model.has_portrait2d_overlay = question.caption !== '';
    this.model.has_tall_panel = question.caption !== '';
    this.model.portrait2d_text = question.caption;
    void this.show();
  }

  private async show(): Promise<void> {
    if (!this.doc) {
      const doc = await openDocument('generic_confirm_dialog', this.model, { parent: document.body });
      if (this.doc || !this.current) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('duel-dialog');
      doc.host.style.zIndex = '95';
      doc.$('#panel')?.addEventListener('pointerdown', (event) => event.stopPropagation());
      placeBoard(doc);
    }
    this.doc.update();
  }

  private finish(accepted: boolean): void {
    const done = this.current;
    if (!done) return;
    this.click();
    this.next();
    done.resolve(accepted);
  }
}

// ---------- the score window ----------

interface ScoreModel extends RmlModel { bold_text_px: number; hero_name: string; hero_score: string; enemy_name: string; enemy_score: string }

/** CDuelWindow: the hero's name and score in blue, the enemy's in red, at the workspace's (509, 359). */
export class DuelScoreHud {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private shown = false;
  private readonly model: ScoreModel = { bold_text_px: nativeTextPx(), hero_name: '', hero_score: '0', enemy_name: '', enemy_score: '0' };

  constructor(private readonly duel: Duel) {
    prepareDocument('duel_window').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => { if (this.doc) placeHud(this.doc, 509, 359); });
  }

  get isOpen(): boolean { return this.doc !== null; }

  show(on: boolean): void {
    this.shown = on;
    if (!on) { this.doc?.close(); this.doc = null; return; }
    this.sync();
    if (this.doc || this.opening) return;
    this.opening = openDocument('duel_window', this.model, { parent: document.body }).then((doc) => {
      if (!this.shown) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '12';
      doc.host.style.pointerEvents = 'none';
      placeHud(doc, 509, 359);
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** SyncRmlModel(): the names and scores of g_DuelMgr. */
  sync(): void {
    const hero = this.duel.players[DUEL_HERO], enemy = this.duel.players[DUEL_ENEMY];
    Object.assign(this.model, { hero_name: hero.name, hero_score: String(hero.score), enemy_name: enemy.name, enemy_score: String(enemy.score) });
    this.doc?.update();
  }
}

// ---------- Doorkeeper Titus's list ----------

interface RoomEntry { index: number; heading: string; running: boolean; player1: string; player2: string; player1_px: number; player2_px: number; joinable: boolean }
interface WatchModel extends RmlModel {
  text_px: number; bold_text_px: number; title: string; subtitle: string; subtitle_px: number; vs_text: string; no_duel_text: string;
  watch_text: string; label_line_px: number; rooms: RoomEntry[];
  duelwatch_join: (index: number) => void; window_close: () => void;
}

/** CDuelWatchWindow, docked right: four colosseums, Watch for a running duel open to spectators. */
export class DuelWatchWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private readonly model: WatchModel;

  constructor(private readonly options: { duel: Duel; closeNpc: () => void; click: () => void; closeOthers: () => void }) {
    this.model = {
      text_px: nativeTextPx(), bold_text_px: nativeTextPx(), title: DuelText.DoorkeeperTitus, subtitle: DuelText.SelectColosseum, subtitle_px: nativeTextPx(),
      vs_text: 'VS', no_duel_text: DuelText.NoDuelOn, watch_text: DuelText.Watch, label_line_px: nativeTextPx() + 2, rooms: [],
      duelwatch_join: (index) => { this.options.click(); this.options.duel.watch(index); },
      window_close: () => this.close(),
    };
    prepareDocument('duel_watch').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { this.close(); this.options.click(); }
    });
  }

  get isOpen(): boolean { return this.doc !== null; }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  private async load(): Promise<void> {
    this.options.closeOthers();
    this.sync();
    const doc = await openDocument('duel_watch', this.model, { parent: document.body, scale: screenScale() });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    const panel = doc.$('#panel');
    panel?.addEventListener('pointerdown', (event) => event.stopPropagation());
    this.place();
  }

  /** Hide(INTERFACE_DUELWATCH): ClosingProcess tells the server the NPC is left. */
  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    this.options.closeNpc();
  }

  /** Closed by the server's answer to a Watch (the NPC conversation ends with the warp). */
  hide(): void {
    this.doc?.close();
    this.doc = null;
  }

  sync(): void {
    const { rooms } = this.options.duel;
    this.model.rooms = rooms.map((room, index) => ({
      index, heading: fill(DuelText.Colosseum, index + 1), running: room.running, joinable: room.running && room.joinable,
      player1: room.running ? room.player1 : '', player2: room.running ? room.player2 : '',
      player1_px: this.model.text_px, player2_px: this.model.text_px,
    }));
    this.doc?.update();
  }

  private place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = screenScale();
    this.doc.setScale(scale);
    Object.assign(panel.style, { left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top' });
    const px = nativeTextPx();
    Object.assign(this.model, { text_px: px, bold_text_px: px, subtitle_px: px, label_line_px: nativeLineHeightPx() });
    this.sync();
  }
}

// ---------- the spectator HUD ----------

const GAUGE = {
  hp: '/data/Interface/menu_pk_hp03(bar2).jpg', sd: '/data/Interface/menu_pk_sd03(bar2).jpg',
  hpFx: '/data/Interface/menu_pk_hp06(bar).jpg', sdFx: '/data/Interface/menu_pk_sd05(bar).jpg',
} as const;
type GaugeImage = (typeof GAUGE)[keyof typeof GAUGE];
interface GaugePiece { src: GaugeImage; sourceWidth: number; sourceHeight: number; left: number; top: number; width: number; height: number; mirrored: boolean }
type PieceOf = (src: GaugeImage, rate: number) => GaugePiece;

// The bar pieces as CDuelWatchMainFrameWindow places them (reference px of the 640 x 51 frame).
const heroHp: PieceOf = (src, rate) => ({ src, sourceWidth: 235 * rate, sourceHeight: 6, left: 60 + 236 * (1 - rate), top: 11, width: 236 * rate, height: 7, mirrored: true });
const enemyHp: PieceOf = (src, rate) => ({ src, sourceWidth: 236 * rate, sourceHeight: 7, left: 580 - 236, top: 11, width: 236 * rate, height: 7, mirrored: false });
const heroSd: PieceOf = (src, rate) => ({ src, sourceWidth: 154 * rate, sourceHeight: 4, left: 142 + 154 * (1 - rate), top: 21, width: 154 * rate, height: 4, mirrored: false });
const enemySd: PieceOf = (src, rate) => ({ src, sourceWidth: 154 * rate, sourceHeight: 4, left: 344, top: 21, width: 154 * rate, height: 4, mirrored: false });

interface FrameModel extends RmlModel {
  watching: boolean; exit_hint: string; hero_name: { text: string; text_px: number }; enemy_name: { text: string; text_px: number };
  hero_marks: number[]; enemy_marks: number[]; gauges: never[];
  duel_watch_exit: () => void;
}

/** One bar's catch-up state: the rate shown, the last received one and where the catch-up started. */
interface Bar { shown: number; last: number; received: number }

/** CDuelWatchMainFrameWindow: in the main HUD's place while the duel-watch buff is on. */
export class DuelWatchFrame {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private shown = false;
  private hasHp = false;
  private readonly bars: Bar[] = Array.from({ length: 4 }, () => ({ shown: 0, last: 0, received: 0 }));
  private readonly images = new Map<GaugeImage, { url: string; width: number; height: number }>();
  private frame = 0;
  private readonly model: FrameModel;

  constructor(private readonly duel: Duel, private readonly click: () => void) {
    this.model = {
      watching: false, exit_hint: DuelText.Finished, hero_name: { text: '', text_px: nativeTextPx() }, enemy_name: { text: '', text_px: nativeTextPx() },
      hero_marks: [], enemy_marks: [], gauges: [],
      duel_watch_exit: () => { this.click(); this.duel.leaveWatch(); },
    };
    prepareDocument('duel_watch_frame').catch((error: unknown) => console.warn('Interface', error));
    for (const src of Object.values(GAUGE)) {
      void loadImage(src).then((canvas) => { if (canvas) this.images.set(src, { url: canvas.toDataURL(), width: canvas.width, height: canvas.height }); });
    }
    window.addEventListener('resize', () => { if (this.doc) placeHud(this.doc, 0, 429); });
  }

  get isOpen(): boolean { return this.doc !== null; }

  show(on: boolean): void {
    if (on === this.shown) return;
    this.shown = on;
    if (!on) {
      cancelAnimationFrame(this.frame);
      this.doc?.close();
      this.doc = null;
      this.hasHp = false; // ClosingProcess
      return;
    }
    this.hasHp = false; // OpeningProcess
    if (this.opening) return;
    this.opening = openDocument('duel_watch_frame', this.model, { parent: document.body }).then((doc) => {
      if (!this.shown) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('duel-watch-frame');
      doc.host.style.zIndex = '13';
      doc.host.style.pointerEvents = 'none';
      const exit = doc.$('#exit');
      exit?.addEventListener('pointerdown', (event) => event.stopPropagation());
      placeHud(doc, 0, 429);
      const step = () => { this.sync(); this.frame = requestAnimationFrame(step); };
      step();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** SyncView(): the names, a mark per point and the gauges, each frame. */
  private sync(): void {
    const doc = this.doc;
    if (!doc) return;
    const watching = this.duel.currentChannel !== -1;
    const hero = this.duel.players[DUEL_HERO], enemy = this.duel.players[DUEL_ENEMY];
    this.model.watching = watching;
    this.model.hero_name = { text: hero.name, text_px: nativeTextPx() };
    this.model.enemy_name = { text: enemy.name, text_px: nativeTextPx() };
    doc.update();
    if (!watching) return;
    // A mark per point (the document's data-for has no list expression; the rows are filled here).
    const marks = (selector: string, count: number) => {
      const row = doc.$(selector);
      if (!row) return;
      while (row.childElementCount > count) row.lastElementChild!.remove();
      while (row.childElementCount < count) { const mark = document.createElement('div'); mark.className = 'score'; row.append(mark); }
    };
    marks('#score_hero', Math.max(0, hero.score));
    marks('#score_enemy', Math.max(0, enemy.score));
    this.drawGauges(doc, this.stepGauges());
  }

  /** StepGauges(): the shown rates catch up with the received ones, faster the larger the change. */
  private stepGauges(): GaugePiece[] {
    const rates = () => [this.duel.players[DUEL_HERO].hp, this.duel.players[DUEL_ENEMY].hp, this.duel.players[DUEL_HERO].sd, this.duel.players[DUEL_ENEMY].sd];
    const now = rates();
    if (!this.hasHp || this.duel.regenerated) {
      this.hasHp = true;
      this.duel.regenerated = false;
      this.bars.forEach((bar, i) => { bar.shown = bar.last = bar.received = now[i]; });
    }
    if (this.bars.some((bar, i) => bar.last !== now[i])) {
      this.bars.forEach((bar, i) => { bar.last = now[i]; bar.received = bar.shown; });
    }
    const pieces: GaugePiece[] = [];
    const lengths = [236, 236, 154, 154];
    const kinds: [PieceOf, GaugeImage, GaugeImage][] = [[heroHp, GAUGE.hp, GAUGE.hpFx], [enemyHp, GAUGE.hp, GAUGE.hpFx], [heroSd, GAUGE.sd, GAUGE.sdFx], [enemySd, GAUGE.sd, GAUGE.sdFx]];
    this.bars.forEach((bar, i) => {
      const rate = now[i];
      const step = (1 / lengths[i]) * (Math.trunc(Math.abs(bar.received - rate) * 5) + 2);
      const [piece, image, fx] = kinds[i];
      if (bar.shown > rate + step) { bar.shown -= step; pieces.push(piece(fx, bar.shown), piece(image, rate)); }
      else if (bar.shown < rate - step) { bar.shown += step; pieces.push(piece(fx, rate), piece(image, bar.shown)); }
      else { bar.shown = rate; pieces.push(piece(image, rate)); }
    });
    return pieces.filter((piece) => piece.width > 0); // an empty bar draws nothing
  }

  private drawGauges(doc: RmlDocument, pieces: GaugePiece[]): void {
    const fight = doc.$('#fight');
    if (!fight) return;
    let layer = fight.querySelector<HTMLDivElement>('.gauges');
    if (!layer) {
      layer = document.createElement('div');
      layer.className = 'gauges';
      fight.prepend(layer);
    }
    while (layer.childElementCount > pieces.length) layer.lastElementChild!.remove();
    while (layer.childElementCount < pieces.length) { const gauge = document.createElement('div'); gauge.className = 'gauge'; layer.append(gauge); }
    pieces.forEach((piece, i) => {
      const element = layer.children[i] as HTMLDivElement;
      const image = this.images.get(piece.src);
      // The texture's first sourceWidth x sourceHeight texels stretched over the piece.
      const scaleX = piece.sourceWidth > 0 ? piece.width / piece.sourceWidth : 1, scaleY = piece.height / piece.sourceHeight;
      Object.assign(element.style, {
        position: 'absolute', left: `${piece.left}px`, top: `${piece.top}px`, width: `${piece.width}px`, height: `${piece.height}px`,
        backgroundImage: image ? `url("${image.url}")` : 'none', backgroundRepeat: 'no-repeat', backgroundPosition: '0 0',
        backgroundSize: image ? `${image.width * scaleX}px ${image.height * scaleY}px` : 'auto',
        transform: piece.mirrored ? 'scale(-1, 1)' : 'none',
      });
    });
  }
}

interface SpectatorModel extends RmlModel { text_px: number; spectators: string[] }

/** CDuelWatchUserListWindow: a box per spectator, stacked upwards from (583, 429). */
export class SpectatorList {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private shown = false;
  private readonly model: SpectatorModel = { text_px: nativeTextPx(), spectators: [] };

  constructor(private readonly duel: Duel) {
    prepareDocument('duel_watch_spectators').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => { if (this.doc) placeHud(this.doc, 583, 429); });
  }

  show(on: boolean): void {
    if (on === this.shown) return;
    this.shown = on;
    if (!on) { this.doc?.close(); this.doc = null; return; }
    this.sync();
    if (this.opening) return;
    this.opening = openDocument('duel_watch_spectators', this.model, { parent: document.body }).then((doc) => {
      if (!this.shown) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '13';
      doc.host.style.pointerEvents = 'none';
      placeHud(doc, 583, 429);
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  sync(): void {
    this.model.spectators = [...this.duel.spectators];
    this.doc?.update();
  }
}
