// HNMO's vault pages: a row of page buttons above the open vault window (an HNMO extra; the desktop has one vault).
// Uses F5 4E (HnmoVaultPageCommand: 0 state, 1 switch page, 2 buy a page) and F5 4F (HnmoVaultPageState, JSON).
// A switch makes the server send the new page's items to the open vault window.

import { read, type PacketValues } from '../net/packets.js';
import { currentLanguage } from '../i18n.js';
import { confirmInGame } from './confirmDialog.js';

/** What the server says about the account's vault pages. */
export interface VaultPageState {
  enabled: boolean;
  /** The page in use, 0-based. */
  active: number;
  /** How many pages the account has (free, VIP and bought). */
  unlocked: number;
  /** The most pages an account can have. */
  max: number;
  /** The price of one more page (0: not for sale). */
  price: number;
  coinType: string;
  balance: number;
  success?: boolean | null;
  message?: string | null;
}

type Send = (name: string, fields: PacketValues) => void;

const COIN_NAMES: Record<string, [string, string]> = {
  WCoinC: ['WCoin (C)', 'WCoin (C)'],
  WCoinP: ['WCoin (P)', 'WCoin (P)'],
  GoblinPoints: ['Goblin Points', 'Điểm Goblin'],
};

export class VaultPages {
  readonly bar: HTMLDivElement;
  state: VaultPageState | null = null;
  visible = false;
  /** A switch or purchase was sent and not answered yet: the buttons wait. */
  busy = false;

  constructor(private readonly options: {
    send: Send;
    message: (text: string) => void;
    /** Where the vault window is on the screen (null while it isn't shown). */
    anchor: () => DOMRect | null;
    parent?: HTMLElement;
  }) {
    this.bar = document.createElement('div');
    this.bar.className = 'hnmo-vault-pages';
    this.bar.hidden = true;
    this.bar.setAttribute('role', 'toolbar');
    (options.parent ?? document.body).append(this.bar);
    window.addEventListener('resize', () => this.place());
  }

  private vi(): boolean {
    return currentLanguage() === 'vi';
  }

  /** The vault window opened: ask for the pages. */
  show(): void {
    this.visible = true;
    this.busy = false;
    this.options.send('HnmoVaultPageCommand', { Action: 0, Page: 0 });
    this.render();
  }

  /** The vault window closed. */
  hide(): void {
    this.visible = false;
    this.bar.hidden = true;
  }

  switchTo(page: number): void {
    if (this.busy || !this.state || page === this.state.active || page >= this.state.unlocked) return;
    this.busy = true;
    this.options.send('HnmoVaultPageCommand', { Action: 1, Page: page });
    this.render();
  }

  async buy(): Promise<void> {
    const state = this.state;
    if (this.busy || !state || state.price <= 0 || state.unlocked >= state.max) return;
    const [en, vi] = COIN_NAMES[state.coinType] ?? [state.coinType, state.coinType];
    const question = this.vi()
      ? `Mở thêm 1 trang rương với ${state.price.toLocaleString()} ${vi}? (Bạn có ${state.balance.toLocaleString()}.)`
      : `Unlock one more vault page for ${state.price.toLocaleString()} ${en}? (You have ${state.balance.toLocaleString()}.)`;
    if (!(await confirmInGame(question))) return;
    this.busy = true;
    this.options.send('HnmoVaultPageCommand', { Action: 2, Page: 0 });
    this.render();
  }

  /** F5 4F: the pages' state. Returns whether the packet was this one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x4f) return false;
    const parsed = read('ServerToClient', 'HnmoVaultPageState', bytes);
    const raw = typeof parsed.Json === 'string' ? parsed.Json : '';
    try {
      this.state = JSON.parse(raw) as VaultPageState;
    } catch {
      return true;
    }
    this.busy = false;
    if (this.state.message) this.options.message(this.state.message);
    this.render();
    return true;
  }

  render(): void {
    const state = this.state;
    if (!this.visible || !state || !state.enabled) {
      this.bar.hidden = true;
      return;
    }
    const vi = this.vi();
    this.bar.replaceChildren();
    const label = document.createElement('span');
    label.className = 'hnmo-vault-pages-label';
    label.textContent = vi ? 'Trang rương' : 'Vault page';
    this.bar.append(label);
    for (let page = 0; page < state.unlocked; page++) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'hnmo-vault-page';
      button.textContent = String(page + 1);
      button.dataset.page = String(page);
      const active = page === state.active;
      button.setAttribute('aria-pressed', String(active));
      button.title = active ? (vi ? `Trang ${page + 1} (đang mở)` : `Page ${page + 1} (open)`) : (vi ? `Mở trang ${page + 1}` : `Open page ${page + 1}`);
      button.disabled = this.busy || active;
      button.addEventListener('click', () => this.switchTo(page));
      this.bar.append(button);
    }
    if (state.price > 0 && state.unlocked < state.max) {
      const more = document.createElement('button');
      more.type = 'button';
      more.className = 'hnmo-vault-page-buy';
      more.textContent = '+';
      const [en, viName] = COIN_NAMES[state.coinType] ?? [state.coinType, state.coinType];
      more.title = vi ? `Mở thêm 1 trang: ${state.price.toLocaleString()} ${viName}` : `One more page: ${state.price.toLocaleString()} ${en}`;
      more.setAttribute('aria-label', more.title);
      more.disabled = this.busy;
      more.addEventListener('click', () => { void this.buy(); });
      this.bar.append(more);
    }
    this.bar.hidden = false;
    this.place();
  }

  /** Next to the vault window's left edge, below its title: the window reaches the top of the screen, and the
   * skill bar is under it. */
  place(): void {
    if (this.bar.hidden) return;
    const box = this.options.anchor();
    if (!box) { this.bar.hidden = true; return; }
    const width = this.bar.offsetWidth || 64;
    Object.assign(this.bar.style, {
      left: `${Math.max(0, Math.round(box.left - width - 4))}px`,
      top: `${Math.round(box.top + Math.min(40, box.height * 0.08))}px`,
    });
  }

}
