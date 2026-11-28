// HNMO Season 9 pentagram window (Ctrl+P): the worn pentagram with its element and errtel slots, the pentagrams and
// errtels of the bag, wearing and taking off, putting errtels in and taking them out, and the errtel mixes.
// Uses F5 5B (HnmoPentagramCommand) and F5 5C (HnmoPentagramState); elemental hits come as F5 5D (HnmoElementalHit).
// The desktop client has no pentagram window, so this follows the web game's HNMO panels (see jewelBankWindow.ts).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { elementName, optionText, setPentagramState, type PentagramBagItem, type PentagramErrtel, type PentagramState } from '../game/pentagram.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface PentagramWindowOptions {
  send: (name: string, fields: PacketValues) => void;
  message: (text: string) => void;
  /** Shows the elemental damage of a hit above its target. */
  elementalHit: (targetId: number, damage: number, element: number) => void;
}

const SUBCODE_STATE = 0x5c;
const SUBCODE_HIT = 0x5d;
const CMD = { Request: 1, Wear: 2, TakeOff: 3, Attach: 4, Remove: 5, LevelUp: 6, RankUp: 7 } as const;

function tx(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

/** The player's text for a server result ("" for none). */
export function pentagramResultText(result: string | null | undefined): string {
  switch (result) {
    case 'Ok': return tx('Done.', 'Đã xong.');
    case 'Disabled': return tx('The pentagram system is off.', 'Hệ thống ngũ hành đang tắt.');
    case 'InvalidItem': return tx('That item can\'t be used here.', 'Không dùng được vật phẩm này.');
    case 'Occupied': return tx('That slot is already taken.', 'Ô này đã có đồ.');
    case 'Empty': return tx('Nothing is there.', 'Ô này đang trống.');
    case 'ElementMismatch': return tx('The element (or rank) doesn\'t match.', 'Nguyên tố (hoặc bậc) không khớp.');
    case 'BagFull': return tx('Make room in your bag first.', 'Hãy dọn chỗ trong túi đồ trước.');
    case 'SaveFailed': return tx('It couldn\'t be saved; nothing changed. Please try again.', 'Không lưu được, chưa có gì thay đổi. Vui lòng thử lại.');
    case 'WrongPlace': return tx('Do this at the pentagram NPC (or in a safe zone).', 'Hãy làm ở NPC ngũ hành (hoặc trong vùng an toàn).');
    case 'NotEnoughZen': return tx('Not enough Zen.', 'Không đủ Zen.');
    case 'MaximumReached': return tx('It is already at its highest level or rank.', 'Đã đạt cấp hoặc bậc cao nhất.');
    case 'Failed': return tx('The mix failed.', 'Ghép thất bại.');
    case 'Broken': return tx('The errtel broke while being taken out.', 'Errtel đã vỡ khi tháo ra.');
    default: return '';
  }
}

export class PentagramWindow {
  private readonly panel: HnmoPanel;
  private state: PentagramState | null = null;

  constructor(private readonly options: PentagramWindowOptions) {
    this.panel = createHnmoPanel('hnmo-pentagram-panel', tx('Pentagram (Ctrl+P)', 'Ngũ Hành (Ctrl+P)'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Pentagram (Ctrl+P)', 'Ngũ Hành (Ctrl+P)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.panel.open();
    this.send(CMD.Request);
    this.render();
  }

  public close(): void {
    this.panel.close();
  }

  public toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  /** Handles F5 5C and F5 5D; returns whether the packet was one of them. */
  public handle(bytes: Uint8Array): boolean {
    if (bytes.length >= 5 && bytes[0] === 0xc2 && bytes[3] === 0xf5 && bytes[4] === SUBCODE_STATE) {
      const parsed = read('ServerToClient', 'HnmoPentagramState', bytes);
      const json = typeof parsed.Json === 'string' ? parsed.Json : '';
      try {
        const next = JSON.parse(json) as PentagramState;
        this.state = next;
        setPentagramState(next);
        const text = pentagramResultText(next.result);
        if (text) this.options.message(text);
        if (this.panel.isOpen()) this.render();
      } catch {
        // Ignore malformed JSON.
      }

      return true;
    }

    if (bytes.length >= 11 && bytes[0] === 0xc1 && bytes[2] === 0xf5 && bytes[3] === SUBCODE_HIT) {
      const parsed = read('ServerToClient', 'HnmoElementalHit', bytes);
      this.options.elementalHit(Number(parsed.TargetId ?? 0), Number(parsed.Damage ?? 0), Number(parsed.Element ?? 0));
      return true;
    }

    return false;
  }

  private send(action: number, slot = 0, index = 0): void {
    this.options.send('HnmoPentagramCommand', { Action: action, Slot: slot, Index: index });
  }

  private button(text: string, onClick: () => void): HTMLButtonElement {
    const b = document.createElement('button');
    b.type = 'button';
    b.className = 'hnmo-btn';
    b.textContent = text;
    b.addEventListener('click', onClick);
    return b;
  }

  private row(text: string, ...buttons: HTMLButtonElement[]): HTMLDivElement {
    const div = document.createElement('div');
    div.className = 'hnmo-row';
    const span = document.createElement('span');
    span.textContent = text;
    div.append(span, ...buttons);
    return div;
  }

  private errtelText(errtel: PentagramErrtel): string {
    return `${elementName(errtel.element)} — ${errtel.options.map(optionText).join(', ')}`;
  }

  private render(): void {
    const body = this.panel.body;
    body.replaceChildren();
    const s = this.state;
    if (!s || !s.enabled) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = s ? tx('The pentagram system is off in this realm.', 'Hệ thống ngũ hành đang tắt ở máy chủ này.') : tx('Loading…', 'Đang tải…');
      body.append(p);
      return;
    }

    const worn = s.worn;
    const title = document.createElement('h4');
    title.textContent = worn
      ? `${tx('Worn pentagram', 'Ngũ hành đang đeo')}: ${elementName(worn.element)}`
      : tx('No pentagram worn', 'Chưa đeo ngũ hành');
    body.append(title);
    if (worn) {
      body.append(this.row('', this.button(tx('Take off', 'Tháo ra'), () => this.send(CMD.TakeOff))));
      for (let i = 0; i < worn.usable; i++) {
        const errtel = worn.errtels[i] ?? null;
        const label = `${tx('Slot', 'Ô')} ${i + 1}: ${errtel ? this.errtelText(errtel) : tx('empty', 'trống')}`;
        body.append(errtel
          ? this.row(label, this.button(`${tx('Take out', 'Tháo')} (${s.costs?.extractZen ?? 0} Zen)`, () => this.send(CMD.Remove, 0, i)))
          : this.row(label));
      }
    }

    const bagTitle = document.createElement('h4');
    bagTitle.textContent = tx('In the bag', 'Trong túi đồ');
    body.append(bagTitle);
    const errtels = s.items.filter((item) => item.errtel);
    for (const item of s.items) {
      if (item.errtels) {
        body.append(this.row(`${tx('Pentagram', 'Ngũ hành')} ${elementName(item.element)}`, this.button(tx('Wear', 'Đeo'), () => this.send(CMD.Wear, item.slot))));
        continue;
      }

      if (!item.errtel) continue;
      const buttons: HTMLButtonElement[] = [];
      if (worn && worn.element === item.element) {
        const free = worn.errtels.slice(0, worn.usable).findIndex((errtel) => !errtel);
        if (free >= 0) buttons.push(this.button(tx('Put in', 'Gắn vào'), () => this.send(CMD.Attach, item.slot, free)));
      }

      buttons.push(this.button(tx('Level up', 'Nâng cấp'), () => this.send(CMD.LevelUp, item.slot)));
      const partner = this.rankPartner(errtels, item);
      if (partner) buttons.push(this.button(`${tx('Rank up', 'Nâng bậc')} (${s.costs?.rankUpZen ?? 0} Zen)`, () => this.send(CMD.RankUp, item.slot, partner.slot)));
      body.append(this.row(`Errtel ${this.errtelText(item.errtel)}`, ...buttons));
    }
  }

  /** A second errtel of the same kind, element and rank, for a rank-up. */
  private rankPartner(errtels: PentagramBagItem[], item: PentagramBagItem): PentagramBagItem | undefined {
    return errtels.find((other) => other !== item && other.number === item.number && other.element === item.element
      && (other.errtel?.options.length ?? 0) === (item.errtel?.options.length ?? -1));
  }
}
