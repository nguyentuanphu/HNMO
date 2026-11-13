// The settings window (O, or the ⚙ button): the desktop's option window (option_window.rml in the
// window_shell frame, COptionWindow) with what the web game can switch. HNMO extras:
//  - Display: render scale (the desktop's resolution row), windowed / full screen, the FPS limit
//    (the desktop's choices) and the text size (the UI scale row: small / normal / large).
//  - Graphics: a low-spec mode switch, the view distance and skill effects off (render/quality.ts).
//  - Audio: the desktop's sound and music volume bars (levels 0..10, audioSystem) and a mute
//    switch (the same as the ♪ button).
//  - Interface: the FPS counter on / off.
// Rows the web game has nothing for (shadows, tuning, language, ...) are left out. Everything is
// kept in the browser (localStorage 'hnmo.settings'; the volumes in the audio system's own key)
// and applied when the page loads.

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { audioSystem, MAX_VOLUME_LEVEL } from '../audio/audioSystem.js';
import {
  applyQuality, DEFAULT_QUALITY, FPS_CAPS, isLowSpec, LOW_SPEC, refreshQuality, RENDER_SCALES, VIEW_DISTANCES,
  type QualitySettings, type QualityTarget,
} from '../render/quality.js';

const STORAGE_KEY = 'hnmo.settings';
export const TEXT_SCALES = [0.85, 1, 1.2];
const TEXT_SIZE_LABELS = ['Small', 'Normal', 'Large'];
const VIEW_DISTANCE_LABELS = ['Near', 'Medium', 'Far'];
/** The HNMO view distance dropdown's id among the window's dropdowns (the desktop's go up to 8). */
const VIEW_DISTANCE_DROPDOWN = 100;
/** How often new maps and the effect system get the view distance / effects switch. */
const REFRESH_MS = 1000;

export interface GameSettings {
  quality: QualitySettings;
  /** Index of TEXT_SCALES. */
  textSize: number;
  showFps: boolean;
}

const DEFAULT_SETTINGS: GameSettings = { quality: { ...DEFAULT_QUALITY }, textSize: 1, showFps: true };

function pick<T>(list: readonly T[], value: unknown, fallback: T): T {
  return list.includes(value as T) ? value as T : fallback;
}

export function loadSettings(): GameSettings {
  const settings: GameSettings = { ...DEFAULT_SETTINGS, quality: { ...DEFAULT_QUALITY } };
  try {
    const text = localStorage.getItem(STORAGE_KEY);
    if (text === null) return settings;
    const saved = JSON.parse(text) as { quality?: Partial<Record<keyof QualitySettings, unknown>>; textSize?: unknown; showFps?: unknown };
    const q = saved.quality ?? {};
    settings.quality = {
      renderScale: pick(RENDER_SCALES, q.renderScale, DEFAULT_QUALITY.renderScale),
      fpsCap: pick(FPS_CAPS, q.fpsCap, DEFAULT_QUALITY.fpsCap),
      viewDistance: pick(VIEW_DISTANCES, q.viewDistance, DEFAULT_QUALITY.viewDistance),
      effects: typeof q.effects === 'boolean' ? q.effects : DEFAULT_QUALITY.effects,
    };
    settings.textSize = pick([0, 1, 2], saved.textSize, 1);
    if (typeof saved.showFps === 'boolean') settings.showFps = saved.showFps;
  } catch {
    /* storage unavailable or damaged: defaults */
  }
  return settings;
}

function saveSettings(settings: GameSettings): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
  } catch {
    /* private window */
  }
}

// The page's own text (chat, name labels) follows the text size too; the game's windows read the
// same variable (ui/rml.ts).
const PAGE_TEXT_CSS = `
.chat-log { font-size: calc(13px * var(--hnmo-text-scale, 1)); }
#labels .label { font-size: calc(12px * var(--hnmo-text-scale, 1)); }
`;

// Rows this window adds, drawn with the window's own classes.
const EXTRA_CSS = `
#panel { pointer-events: auto; }
.option-row, .option-tab-btn, .option-close-btn, .option-dropdown-option, .option-dropdown-value, .level-gauge-hit { pointer-events: auto; }
.hnmo-left-out { display: none !important; }
`;

interface OptionModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  active_tab: number; open_dropdown: number;
  [name: string]: unknown;
}

function checkboxRow(label: string, onClick: () => void): { row: HTMLDivElement; box: HTMLDivElement } {
  const row = document.createElement('div');
  row.className = 'option-row option-checkbox-row hnmo-extra';
  const text = document.createElement('span');
  text.className = 'option-row-label';
  text.textContent = label;
  const box = document.createElement('div');
  box.className = 'checkbox-box';
  const tick = document.createElement('div');
  tick.className = 'checkbox-tick';
  box.append(tick);
  row.append(text, box);
  row.addEventListener('click', onClick);
  return { row, box };
}

export class SettingsWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private readonly settings: GameSettings = loadSettings();
  private readonly model: OptionModel;
  private extras: { lowSpec?: HTMLDivElement; mute?: HTMLDivElement; viewValue?: HTMLDivElement; viewList?: HTMLDivElement; viewOptions: HTMLDivElement[] } = { viewOptions: [] };

  constructor(private readonly target: QualityTarget) {
    const close = () => this.close();
    this.model = {
      has_title: false, title: 'Options', positioned: false, root_x: 0, root_y: 0,
      active_tab: 0, open_dropdown: -1,
      tab_display_label: 'Display', tab_graphics_label: 'Graphics', tab_audio_label: 'Audio', tab_gameplay_label: 'Gameplay', tab_interface_label: 'Interface',
      resolution_row_label: 'Render scale', resolution_labels: RENDER_SCALES.map((scale) => `${Math.round(scale * 100)} %`),
      windowed_mode_label: 'Windowed mode', windowed_mode: true,
      fps_cap_row_label: 'FPS limit', fps_cap_labels: FPS_CAPS.map((fps) => (fps > 0 ? `${fps} FPS` : 'Uncapped')),
      ui_scale_row_label: 'Text size', ui_scale_labels: TEXT_SIZE_LABELS, ui_scale_tooltip: '',
      section_quality_label: 'Quality', section_effects_label: 'Effects', section_shadows_label: 'Shadows',
      disable_effects_label: 'Disable skill effects',
      sound_volume_label: 'Sound volume', music_volume_label: 'Music volume',
      show_fps_counter_label: 'Show FPS counter',
      option_select_tab: (tab: number) => { this.model.active_tab = tab; this.model.open_dropdown = -1; },
      option_toggle_dropdown: (id: number) => { this.model.open_dropdown = this.model.open_dropdown === id ? -1 : id; },
      option_dropdown_option_click: (id: number, index: number) => this.choose(id, index),
      option_click_close: close,
      option_toggle_windowed_mode: () => this.toggleFullScreen(),
      option_toggle_disable_effects: () => this.setQuality({ effects: !this.settings.quality.effects }),
      option_toggle_show_fps_counter: () => { this.settings.showFps = !this.settings.showFps; this.applyPage(); saveSettings(this.settings); },
      option_gauge: () => undefined, // the bars take the pointer themselves (see wireGauges)
    };
    this.sync();
    this.applyPage();
    applyQuality(this.target, this.settings.quality);
    setInterval(() => refreshQuality(), REFRESH_MS);
    const style = document.createElement('style');
    style.textContent = PAGE_TEXT_CSS;
    document.head.append(style);

    document.getElementById('settingsButton')?.addEventListener('click', () => { void this.toggle(); });
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.doc) { this.close(); return; }
      if (event.target instanceof Element && event.target.closest('input, textarea, select')) return;
      if (event.key.toLowerCase() === 'o' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void this.toggle(); }
    });
    window.addEventListener('resize', () => this.place());
    document.addEventListener('fullscreenchange', () => this.update());
  }

  get isOpen(): boolean { return this.doc !== null; }
  get current(): GameSettings { return { ...this.settings, quality: { ...this.settings.quality } }; }

  async toggle(): Promise<void> {
    if (this.doc) this.close(); else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.openDocument().finally(() => { this.opening = null; });
    await this.opening;
  }

  close(): void {
    this.doc?.close();
    this.doc = null;
    this.model.open_dropdown = -1;
  }

  private async openDocument(): Promise<void> {
    const doc = await openDocument('option_window', this.model);
    Object.assign(doc.host.style, { position: 'fixed', left: '0', top: '0', width: '100%', height: '100%', pointerEvents: 'none', zIndex: '60' });
    const style = document.createElement('style');
    style.textContent = EXTRA_CSS;
    doc.host.shadowRoot?.append(style);
    this.doc = doc;
    this.leaveOut(doc);
    this.addExtras(doc);
    this.wireGauges(doc);
    const update = this.model.__update;
    this.model.__update = () => { this.sync(); update?.(); this.refreshExtras(); };
    this.place();
    this.update();
  }

  private update(): void {
    this.model.__update?.();
  }

  // The window at the size of the game's other windows (they scale with the screen's height).
  private place(): void {
    const panel = this.doc?.root;
    if (!panel) return;
    const scale = Math.max(0.8, window.innerHeight / REFERENCE_HEIGHT / 1.25);
    panel.style.transform = `translate(-50%, -50%) scale(${scale.toFixed(3)})`;
  }

  // The model's values from the settings and the audio system.
  private sync(): void {
    const q = this.settings.quality;
    const audio = audioSystem().getSettings();
    const m = this.model;
    m.resolution_index = RENDER_SCALES.indexOf(q.renderScale);
    m.resolution_value_label = (m.resolution_labels as string[])[RENDER_SCALES.indexOf(q.renderScale)];
    m.windowed_mode = !document.fullscreenElement;
    m.fps_cap_index = FPS_CAPS.indexOf(q.fpsCap);
    m.fps_cap_value_label = (m.fps_cap_labels as string[])[FPS_CAPS.indexOf(q.fpsCap)];
    m.ui_scale_index = this.settings.textSize;
    m.ui_scale_value_label = TEXT_SIZE_LABELS[this.settings.textSize];
    m.disable_effects = !q.effects;
    m.sound_volume = audio.effectsLevel;
    m.music_volume = audio.musicLevel;
    m.show_fps_counter = this.settings.showFps;
  }

  private choose(id: number, index: number): void {
    this.model.open_dropdown = -1;
    if (id === 0 && index >= 0 && index < RENDER_SCALES.length) this.setQuality({ renderScale: RENDER_SCALES[index] });
    else if (id === 1 && index >= 0 && index < FPS_CAPS.length) this.setQuality({ fpsCap: FPS_CAPS[index] });
    else if (id === 5 && index >= 0 && index < TEXT_SCALES.length) { this.settings.textSize = index; this.applyPage(); saveSettings(this.settings); }
    else if (id === VIEW_DISTANCE_DROPDOWN && index >= 0 && index < VIEW_DISTANCES.length) this.setQuality({ viewDistance: VIEW_DISTANCES[index] });
  }

  private setQuality(change: Partial<QualitySettings>): void {
    this.settings.quality = { ...this.settings.quality, ...change };
    applyQuality(this.target, this.settings.quality);
    saveSettings(this.settings);
  }

  private applyPage(): void {
    document.documentElement.style.setProperty('--hnmo-text-scale', String(TEXT_SCALES[this.settings.textSize]));
    const fps = document.getElementById('fps');
    if (fps) fps.hidden = !this.settings.showFps;
  }

  private toggleFullScreen(): void {
    const request = document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen();
    void request.catch(() => undefined).then(() => this.update());
  }

  private setMuted(muted: boolean): void {
    audioSystem().setMuted(muted);
    document.getElementById('soundButton')?.setAttribute('aria-pressed', String(muted));
  }

  // Rows and tabs of the desktop window the web game has nothing for.
  private leaveOut(doc: RmlDocument): void {
    const panels = [...doc.root.querySelectorAll<HTMLElement>('.option-tab-panel')];
    const rowOf = (selector: string): Element | null => doc.root.querySelector(selector)?.closest('.option-row') ?? null;
    const keep = new Set<Element | null>([
      rowOf('[data-event-click="option_toggle_dropdown(0)"]'), rowOf('[data-event-click="option_toggle_windowed_mode"]'),
      rowOf('[data-event-click="option_toggle_dropdown(1)"]'), rowOf('[data-event-click="option_toggle_dropdown(5)"]'),
      rowOf('[data-event-click="option_toggle_disable_effects"]'),
      rowOf('[data-event-mousedown="option_gauge(0)"]'), rowOf('[data-event-mousedown="option_gauge(1)"]'),
      rowOf('[data-event-click="option_toggle_show_fps_counter"]'),
    ]);
    const graphicsSections = panels[1]?.querySelectorAll(':scope > .option-section');
    keep.add(graphicsSections?.[0] ?? null);
    keep.add(graphicsSections?.[1] ?? null);
    for (const panel of panels) {
      for (const child of panel.children) if (!keep.has(child)) child.classList.add('hnmo-left-out');
    }
    doc.root.querySelectorAll('.option-tab-btn')[3]?.classList.add('hnmo-left-out'); // Gameplay
  }

  private addExtras(doc: RmlDocument): void {
    const panels = [...doc.root.querySelectorAll<HTMLElement>('.option-tab-panel')];
    const graphics = panels[1], audio = panels[2];
    if (graphics) {
      const lowSpec = checkboxRow('Low-spec mode', () => {
        this.setQuality(isLowSpec(this.settings.quality) ? { ...DEFAULT_QUALITY } : { ...LOW_SPEC });
        this.update();
      });
      lowSpec.row.id = 'hnmo_low_spec';
      // View distance: a dropdown like the window's own.
      const row = document.createElement('div');
      row.className = 'option-row option-select-row hnmo-extra';
      row.id = 'hnmo_view_distance';
      const label = document.createElement('span');
      label.className = 'option-row-label';
      label.textContent = 'View distance';
      const dropdown = document.createElement('div');
      dropdown.className = 'option-dropdown';
      const value = document.createElement('div');
      value.className = 'option-dropdown-value';
      value.addEventListener('click', () => { this.model.open_dropdown = this.model.open_dropdown === VIEW_DISTANCE_DROPDOWN ? -1 : VIEW_DISTANCE_DROPDOWN; this.update(); });
      const list = document.createElement('div');
      list.className = 'option-dropdown-list';
      const options = VIEW_DISTANCE_LABELS.map((text, index) => {
        const option = document.createElement('div');
        option.className = 'option-dropdown-option';
        option.textContent = text;
        option.addEventListener('click', () => { this.choose(VIEW_DISTANCE_DROPDOWN, index); this.update(); });
        return option;
      });
      list.append(...options);
      dropdown.append(value, list);
      row.append(label, dropdown);
      const first = graphics.querySelector(':scope > .option-section');
      if (first) first.after(lowSpec.row, row); else graphics.prepend(lowSpec.row, row);
      this.extras = { ...this.extras, lowSpec: lowSpec.box, viewValue: value, viewList: list, viewOptions: options };
    }
    if (audio) {
      const mute = checkboxRow('Mute all sound', () => { this.setMuted(!audioSystem().getSettings().muted); this.update(); });
      mute.row.id = 'hnmo_mute';
      audio.append(mute.row);
      this.extras.mute = mute.box;
    }
  }

  private refreshExtras(): void {
    const { lowSpec, mute, viewValue, viewList, viewOptions } = this.extras;
    const q = this.settings.quality;
    lowSpec?.classList.toggle('checked', isLowSpec(q));
    mute?.classList.toggle('checked', audioSystem().getSettings().muted);
    const index = VIEW_DISTANCES.indexOf(q.viewDistance);
    if (viewValue) viewValue.textContent = VIEW_DISTANCE_LABELS[index] ?? '';
    viewList?.classList.toggle('hidden', this.model.open_dropdown !== VIEW_DISTANCE_DROPDOWN);
    viewOptions.forEach((option, i) => option.classList.toggle('selected', i === index));
  }

  // The volume bars: press or drag along a bar, or turn the wheel over it (COptionWindow::RmlGaugeEvent:
  // the level is the nearest of 0..10, a press left of the bar reads 0).
  private wireGauges(doc: RmlDocument): void {
    for (const hit of doc.root.querySelectorAll<HTMLElement>('[data-event-mousedown^="option_gauge("]')) {
      const gauge = Number(/option_gauge\((\d+)\)/.exec(hit.getAttribute('data-event-mousedown') ?? '')?.[1] ?? -1);
      if (gauge !== 0 && gauge !== 1) continue;
      const set = (level: number) => {
        const audio = audioSystem();
        if (gauge === 0) audio.setEffectsLevel(level); else audio.setMusicLevel(level);
        this.update();
      };
      const level = () => (gauge === 0 ? audioSystem().getSettings().effectsLevel : audioSystem().getSettings().musicLevel);
      const fromPointer = (event: PointerEvent) => {
        const rect = hit.getBoundingClientRect();
        const x = event.clientX - rect.left;
        set(x < 0 ? 0 : Math.min(MAX_VOLUME_LEVEL, Math.floor((MAX_VOLUME_LEVEL * x) / rect.width + 0.5)));
      };
      hit.addEventListener('pointerdown', (event) => {
        event.preventDefault();
        hit.setPointerCapture(event.pointerId);
        fromPointer(event);
      });
      hit.addEventListener('pointermove', (event) => { if (hit.hasPointerCapture(event.pointerId)) fromPointer(event); });
      hit.addEventListener('wheel', (event) => {
        event.preventDefault();
        set(Math.max(0, Math.min(MAX_VOLUME_LEVEL, level() + (event.deltaY < 0 ? 1 : -1))));
      }, { passive: false });
    }
  }
}

/** Makes the settings window and applies the saved settings (main.ts calls this once). */
export function setUpSettings(target: QualityTarget): SettingsWindow {
  return new SettingsWindow(target);
}
