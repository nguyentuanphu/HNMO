// HNMO's bigger stacks (an HNMO extra): which items stack and how many (F5 51 state), splitting a stack with
// Shift + click (F5 50 action 1), and unlocking a stack kind with coins (F5 50 action 2) from the vault's panel.
// Potions, scrolls, event tickets and keys stack for everybody; jewels, boxes and quest items and crafting
// materials after the account unlocked them (the server decides; this only shows it and asks).

import { read, type PacketValues } from '../net/packets.js';
import { currentLanguage } from '../i18n.js';
import { confirmInGame } from './confirmDialog.js';
import { showConfirm } from './gameDialogs.js';
import type { Item } from '../game/inventory.js';

/** A stack kind as the server describes it. */
export interface StackKind {
  key: string;
  name: string;
  free: boolean;
  unlocked: boolean;
  limit: number;
  price: number;
  /** `group:number` or `group:from-to`. */
  items: string[];
}

export interface ItemStackState {
  enabled: boolean;
  coinType: string;
  balance: number;
  kinds: StackKind[];
  success?: boolean | null;
  message?: string | null;
}

type Send = (name: string, fields: PacketValues) => void;

/** Reads `group:number` or `group:from-to` into the item keys (group * 512 + number). */
export function parseStackItems(ranges: readonly string[]): Set<number> {
  const keys = new Set<number>();
  for (const range of ranges) {
    const match = /^\s*(\d+)\s*:\s*(\d+)\s*(?:-\s*(\d+)\s*)?$/.exec(range);
    if (!match) continue;
    const group = Number(match[1]);
    const from = Number(match[2]);
    const to = match[3] === undefined ? from : Number(match[3]);
    for (let number = from; number <= to && number - from < 512; number++) keys.add(group * 512 + number);
  }
  return keys;
}

export class ItemStacks {
  state: ItemStackState | null = null;
  /** The items of every kind (stacked items show their count), and of the kinds this account stacks. */
  private kindItems = new Set<number>();
  private stackingItems = new Set<number>();
  readonly panel: HTMLDivElement;
  readonly button: HTMLButtonElement;

  constructor(private readonly options: { send: Send; message: (text: string) => void; parent?: HTMLElement }) {
    this.panel = document.createElement('div');
    this.panel.className = 'hnmo-stacks-panel';
    this.panel.hidden = true;
    this.panel.setAttribute('role', 'dialog');
    (options.parent ?? document.body).append(this.panel);
    this.button = document.createElement('button');
    this.button.type = 'button';
    this.button.className = 'hnmo-stacks-button';
    this.button.addEventListener('click', () => this.togglePanel());
    this.renderButton();
  }

  private vi(): boolean {
    return currentLanguage() === 'vi';
  }

  /** Asks the server for the kinds (after entering the world). */
  request(): void {
    this.options.send('HnmoItemStackCommand', { Action: 0, Storage: 0, Slot: 0, Count: 0, Kind: '' });
  }

  /** Whether the item is of a stack kind (its durability is a count of pieces). */
  isStackKind(item: Item): boolean {
    return this.kindItems.has(item.group * 512 + item.number);
  }

  /** Whether the item stacks for this account (a free kind or an unlocked one). */
  stacksForMe(item: Item): boolean {
    return this.stackingItems.has(item.group * 512 + item.number);
  }

  /** Shift + click on a stack in the bag (0) or the open vault (1): asks how many pieces to split off. */
  async split(storage: 0 | 1, slot: number, item: Item): Promise<boolean> {
    if (item.durability < 2 || !(item.group === 14 || this.isStackKind(item))) return false;
    const vi = this.vi();
    const half = Math.floor(item.durability / 2);
    const answer = await showConfirm({
      lines: [vi ? `Tách bao nhiêu món? (1 - ${item.durability - 1})` : `How many to split off? (1 - ${item.durability - 1})`],
      showCancel: true,
      input: { numericOnly: true, maxLength: 3, initialText: String(half) },
    });
    if (answer.button !== 'primary') return true;
    const count = Number(answer.text);
    if (!Number.isInteger(count) || count < 1 || count >= item.durability) {
      this.options.message(vi ? 'Số lượng tách không hợp lệ.' : 'That can\'t be split off this stack.');
      return true;
    }
    this.options.send('HnmoItemStackCommand', { Action: 1, Storage: storage, Slot: slot, Count: count, Kind: '' });
    return true;
  }

  async unlock(kind: StackKind): Promise<void> {
    const state = this.state;
    if (!state || kind.unlocked || kind.price <= 0) return;
    const vi = this.vi();
    const question = vi
      ? `Mở khoá chồng "${kind.name}" (tối đa ${kind.limit} món một ô) với ${kind.price.toLocaleString()} ${state.coinType}? (Bạn có ${state.balance.toLocaleString()}.)`
      : `Unlock stacking "${kind.name}" (up to ${kind.limit} in one slot) for ${kind.price.toLocaleString()} ${state.coinType}? (You have ${state.balance.toLocaleString()}.)`;
    if (!(await confirmInGame(question))) return;
    this.options.send('HnmoItemStackCommand', { Action: 2, Storage: 0, Slot: 0, Count: 0, Kind: kind.key });
  }

  /** F5 51. Returns whether the packet was this one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x51) return false;
    const parsed = read('ServerToClient', 'HnmoItemStackState', bytes);
    try {
      this.state = JSON.parse(typeof parsed.Json === 'string' ? parsed.Json : '') as ItemStackState;
    } catch {
      return true;
    }
    const kinds = this.state.enabled ? this.state.kinds ?? [] : [];
    this.kindItems = parseStackItems(kinds.flatMap((k) => k.items));
    this.stackingItems = parseStackItems(kinds.filter((k) => k.unlocked).flatMap((k) => k.items));
    if (this.state.message) this.options.message(this.state.message);
    this.renderButton();
    if (!this.panel.hidden) this.renderPanel();
    this.onChange?.();
    return true;
  }

  /** The kinds changed (the windows redraw their counts). */
  onChange?: () => void;

  private renderButton(): void {
    const vi = this.vi();
    this.button.textContent = vi ? 'Chồng' : 'Stacks';
    this.button.title = vi ? 'Loại đồ chồng được và mở khoá' : 'Which items stack, and unlocks';
    this.button.hidden = !this.state?.enabled;
  }

  togglePanel(): void {
    if (this.panel.hidden) { this.renderPanel(); this.panel.hidden = false; this.place(); } else this.panel.hidden = true;
  }

  hidePanel(): void {
    this.panel.hidden = true;
  }

  private renderPanel(): void {
    const state = this.state;
    const vi = this.vi();
    this.panel.replaceChildren();
    const title = document.createElement('div');
    title.className = 'hnmo-stacks-title';
    title.textContent = vi ? 'Chồng đồ (Shift + click để tách)' : 'Item stacks (Shift + click splits)';
    this.panel.append(title);
    for (const kind of state?.kinds ?? []) {
      const row = document.createElement('div');
      row.className = 'hnmo-stacks-row';
      const name = document.createElement('span');
      name.textContent = `${kind.name} (${kind.limit})`;
      row.append(name);
      if (kind.unlocked) {
        const tag = document.createElement('span');
        tag.className = 'hnmo-stacks-on';
        tag.textContent = kind.free ? (vi ? 'Miễn phí' : 'Free') : (vi ? 'Đã mở' : 'Unlocked');
        row.append(tag);
      } else {
        const buy = document.createElement('button');
        buy.type = 'button';
        buy.dataset.kind = kind.key;
        buy.textContent = `${vi ? 'Mở khoá' : 'Unlock'} ${kind.price.toLocaleString()}`;
        buy.disabled = kind.price <= 0;
        buy.addEventListener('click', () => { void this.unlock(kind); });
        row.append(buy);
      }
      this.panel.append(row);
    }
    const close = document.createElement('button');
    close.type = 'button';
    close.className = 'hnmo-stacks-close';
    close.textContent = vi ? 'Đóng' : 'Close';
    close.addEventListener('click', () => this.hidePanel());
    this.panel.append(close);
  }

  /** Next to the button. */
  place(): void {
    if (this.panel.hidden) return;
    const box = this.button.getBoundingClientRect();
    const width = this.panel.offsetWidth || 240;
    Object.assign(this.panel.style, {
      left: `${Math.max(4, Math.round(box.left - width - 6))}px`,
      top: `${Math.max(4, Math.round(box.top))}px`,
    });
  }
}
