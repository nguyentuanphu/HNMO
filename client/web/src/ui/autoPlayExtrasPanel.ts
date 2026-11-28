import { panelKey } from '../game/keyboardShortcuts.js';
// The HNMO extras of auto play (not in the desktop helper): a small settings window, kept in this
// browser per character. Opened with Shift + Z (and from the auto play settings). It has no desktop
// window, so it is HNMO's own interface document (public/data/Interface/RmlUi/hnmo_auto_play_extras.rml)
// in the game's message box frame (window_shell), with the options window's checkboxes.

import type { AutoPlayExtras, AutoPlayExtrasSettings } from '../game/autoPlayExtras.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale } from './nativeText.js';

type Key = keyof AutoPlayExtrasSettings;
interface Row { key: Key; label: string; number: boolean; on: boolean; value: number; max: number; max_length: number }

const ROWS: { key: Key; label: string; max?: number }[] = [
  { key: 'returnWhenBagFull', label: 'Back to town when the bag is full' },
  { key: 'returnWhenOutOfPotions', label: 'Back to town when the healing potions run out' },
  { key: 'potionsToBuy', label: 'Healing potions to buy', max: 255 },
  { key: 'sellJunk', label: 'Sell junk (plain weapons and armour) in town' },
  { key: 'stopAfterMinutes', label: 'Stop after (minutes, 0 = never)', max: 1440 },
  { key: 'alertOnDeath', label: 'Alert when the character dies' },
  { key: 'alertOnBagFull', label: 'Alert when the bag is full' },
];

interface ExtrasModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  rows: Row[]; hint: string; save_label: string; close_label: string;
  ape_toggle: (index: number) => void; ape_save: () => void; ape_close: () => void;
}

export class AutoPlayExtrasPanel {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private readonly model: ExtrasModel;

  constructor(private readonly extras: AutoPlayExtras) {
    this.model = {
      has_title: true, title: 'Auto play: extras', positioned: false, root_x: 0, root_y: 0,
      rows: [], hint: 'Trips go to the potion seller in Lorencia and back to the spot where auto play was hunting.',
      save_label: 'Save', close_label: 'Close',
      ape_toggle: (index: number) => {
        const row = this.model.rows[index];
        if (row && !row.number) row.on = !row.on;
      },
      ape_save: () => this.save(),
      ape_close: () => this.close(),
    };
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (panelKey(event, 'Escape') && this.isOpen) { event.preventDefault(); this.close(); }
    });
  }

  get isOpen(): boolean { return this.wanted; }

  toggle(): void {
    if (this.wanted) this.close(); else this.open();
  }

  close(): void {
    this.wanted = false;
    this.doc?.close();
    this.doc = null;
  }

  open(): void {
    this.wanted = true;
    const s = this.extras.settings;
    this.model.rows = ROWS.map(({ key, label, max }) => ({
      key, label, number: max !== undefined, on: Boolean(s[key]), value: Number(s[key]), max: max ?? 0, max_length: String(max ?? 0).length,
    }));
    if (this.doc) { this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_auto_play_extras', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('auto-play-extras');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', 'Auto play: extras');
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      // Typing goes into the number fields, not to the game's hotkeys; digits only.
      doc.root.addEventListener('keydown', (event) => { if (event.key !== 'Escape') event.stopPropagation(); });
      doc.root.addEventListener('input', (event) => {
        if (event.target instanceof HTMLInputElement) event.target.value = event.target.value.replace(/\D/g, '');
      });
      this.place();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  private save(): void {
    const next: AutoPlayExtrasSettings = { ...this.extras.settings };
    const values = next as unknown as Record<string, boolean | number>;
    const fields = new Map<string, HTMLInputElement>();
    for (const input of this.doc?.root.querySelectorAll<HTMLInputElement>('input[data-key]') ?? []) fields.set(input.dataset.key ?? '', input);
    for (const row of this.model.rows) {
      if (!row.number) { values[row.key] = row.on; continue; }
      const typed = fields.get(row.key)?.value ?? String(row.value);
      values[row.key] = Math.min(row.max, Math.max(0, Math.floor(Number(typed) || 0)));
    }
    this.extras.save(next);
    this.close();
  }

  // A dp window: the window in dp, scaled by the dp ratio (TypographyScale), as the desktop's documents.
  private place(): void {
    const doc = this.doc;
    if (!doc) return;
    const scale = typographyScale();
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0px', top: '0px', width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none', zIndex: '80',
    });
    doc.setScale(scale);
    doc.update();
  }
}
