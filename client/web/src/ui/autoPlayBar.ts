// The auto play bar (CMuHelperBar, mu_helper_bar.rml): the map and the hero's position, the settings
// button and the start / stop button.

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';

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
  }

  async open(parent: HTMLElement = document.body): Promise<void> {
    this.doc = await openDocument('mu_helper_bar', this.model, { parent });
    this.doc.host.style.zIndex = '25';
    const panel = this.doc.$('#panel');
    if (panel) panel.style.pointerEvents = 'auto';
    this.place();
  }

  private place(): void {
    if (!this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    Object.assign(this.doc.root.style, { transform: `scale(${scale})`, transformOrigin: 'left top' });
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
