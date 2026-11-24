// The auto play bar (CMuHelperBar, mu_helper_bar.rml): the map and the hero's position, the settings
// button and the start / stop button.

import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { uiScale } from './nativeText.js';
import { onLanguageChange, translate } from '../i18n.js';

interface BarModel extends RmlModel {
  position_text: string; mu_helper_active: boolean; config_hint: string; start_hint: string; stop_hint: string;
  mu_helper_config_click: () => void; mu_helper_toggle_click: () => void;
}

export class AutoPlayBar {
  private doc: RmlDocument | null = null;
  private readonly model: BarModel;

  constructor(actions: { settings: () => void; toggle: () => void }) {
    this.model = {
      position_text: '', mu_helper_active: false,
      config_hint: 'Auto play settings (Z)', start_hint: 'Start auto play (Home)', stop_hint: 'Stop auto play (Home)',
      mu_helper_config_click: actions.settings, mu_helper_toggle_click: actions.toggle,
    };
    window.addEventListener('resize', () => this.place());
    onLanguageChange(() => this.syncHints());
  }

  async open(parent: HTMLElement = document.body): Promise<void> {
    this.doc = await openDocument('mu_helper_bar', this.model, { parent });
    this.doc.host.style.zIndex = '25';
    const panel = this.doc.$('#panel');
    if (panel) panel.style.pointerEvents = 'auto';
    this.place();
    this.syncHints();
  }

  private syncHints(): void {
    if (!this.doc) return;
    for (const el of this.doc.root.querySelectorAll<HTMLElement>('[data-hint]')) {
      const hint = el.getAttribute('data-hint');
      if (hint) el.title = translate(hint) ?? hint;
    }
  }

  private place(): void {
    if (!this.doc) return;
    const scale = uiScale();
    this.doc.setScale(scale);
    Object.assign(this.doc.root.style, { transform: `scale(${scale})`, transformOrigin: 'left top' });
    requestAnimationFrame(() => this.publishBox());
  }

  /**
   * Where the bar is on the screen, as CSS variables of the page: the game's top bar (index.html, .hud) sits on the
   * same line, right of it, and the guide's button below them.
   */
  private publishBox(): void {
    const panel = this.doc?.$('#panel');
    if (!panel) return;
    const box = panel.getBoundingClientRect();
    if (box.width <= 0) return;
    const style = document.documentElement.style;
    style.setProperty('--helper-bar-right', `${Math.round(box.right)}px`);
    style.setProperty('--helper-bar-middle', `${Math.round(box.top + box.height / 2)}px`);
    style.setProperty('--helper-bar-bottom', `${Math.round(box.bottom)}px`);
  }

  /** The map name and the hero's cell, and whether auto play runs. */
  set(mapName: string, cell: { x: number; y: number }, active: boolean): void {
    if (!this.doc) return;
    const text = `${mapName} (${cell.x} , ${cell.y})`;
    if (text === this.model.position_text && active === this.model.mu_helper_active) return;
    this.model.position_text = text;
    this.model.mu_helper_active = active;
    this.doc.update();
  }
}
