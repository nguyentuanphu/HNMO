// The HNMO extras of auto play (not in the desktop helper): a small settings panel, kept in this
// browser per character. Opened with Shift + Z (and from the auto play settings).

import type { AutoPlayExtras, AutoPlayExtrasSettings } from '../game/autoPlayExtras.js';

export class AutoPlayExtrasPanel {
  private element: HTMLDivElement | null = null;

  constructor(private readonly extras: AutoPlayExtras) {}

  get isOpen(): boolean { return this.element !== null; }

  toggle(): void {
    if (this.element) this.close(); else this.open();
  }

  close(): void {
    this.element?.remove();
    this.element = null;
  }

  open(): void {
    const s = this.extras.settings;
    const panel = document.createElement('div');
    panel.className = 'chip auto-play-extras';
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-label', 'Auto play: extras');
    Object.assign(panel.style, { position: 'fixed', left: '50%', top: '18%', transform: 'translateX(-50%)', zIndex: '80', width: 'min(340px, calc(100vw - 32px))', display: 'grid', gap: '8px', padding: '14px', pointerEvents: 'auto' });
    const check = (key: keyof AutoPlayExtrasSettings, label: string) =>
      `<label style="display:flex;gap:8px;align-items:center"><input type="checkbox" data-key="${key}" ${s[key] ? 'checked' : ''}> ${label}</label>`;
    const number = (key: keyof AutoPlayExtrasSettings, label: string, max: number) =>
      `<label style="display:flex;gap:8px;align-items:center;justify-content:space-between">${label}<input type="number" min="0" max="${max}" data-key="${key}" value="${Number(s[key])}" style="width:72px"></label>`;
    panel.innerHTML = `<strong style="color:var(--gold)">Auto play: extras</strong>
      ${check('returnWhenBagFull', 'Back to town when the bag is full')}
      ${check('returnWhenOutOfPotions', 'Back to town when the healing potions run out')}
      ${number('potionsToBuy', 'Healing potions to buy', 255)}
      ${check('sellJunk', 'Sell junk (plain weapons and armour) in town')}
      ${number('stopAfterMinutes', 'Stop after (minutes, 0 = never)', 1440)}
      ${check('alertOnDeath', 'Alert when the character dies')}
      ${check('alertOnBagFull', 'Alert when the bag is full')}
      <p class="hint" style="margin:0">Trips go to the potion seller in Lorencia and back to the spot where auto play was hunting.</p>
      <div class="buttons"><button type="button" class="primary" data-action="save">Save</button><button type="button" data-action="close">Close</button></div>`;
    panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    panel.addEventListener('keydown', (event) => event.stopPropagation());
    panel.addEventListener('click', (event) => {
      const action = event.target instanceof HTMLElement ? event.target.dataset.action : undefined;
      if (action === 'close') this.close();
      if (action === 'save') {
        const next: AutoPlayExtrasSettings = { ...this.extras.settings };
        for (const input of panel.querySelectorAll<HTMLInputElement>('input[data-key]')) {
          const key = input.dataset.key as keyof AutoPlayExtrasSettings;
          if (input.type === 'checkbox') (next as unknown as Record<string, boolean | number>)[key] = input.checked;
          else (next as unknown as Record<string, boolean | number>)[key] = Math.max(0, Math.floor(Number(input.value) || 0));
        }
        this.extras.save(next);
        this.close();
      }
    });
    document.body.append(panel);
    this.element = panel;
  }
}
