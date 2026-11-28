// HNMO's Season 9 Muun window (Alt+N): the active Muun and its bonus (or the condition it waits for), the Muuns in
// the bag, converting a Muun into energy, levelling up and evolving. Uses F5 2B (HnmoMuunCommand) and F5 2C
// (HnmoMuunState); the server decides everything, this window shows its state.

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface MuunActiveState {
  slot: number;
  group: number;
  number: number;
  name: string;
  level: number;
  maxLevel: number;
  evolved: boolean;
  bonusEn: string;
  bonusVi: string;
  bonus: number;
  applies: boolean;
  conditionEn: string;
  conditionVi: string;
  canEvolve: boolean;
  levelUpCost: number;
}

export interface MuunBagState {
  slot: number;
  group: number;
  number: number;
  name: string;
  level: number;
  evolved: boolean;
  active: boolean;
  energy: number;
}

export interface MuunStatePayload {
  v: number;
  enabled: boolean;
  active: MuunActiveState | null;
  bag: MuunBagState[];
  energy: number;
  stones?: number;
  converters?: number;
  strictConditions?: boolean;
  event?: { result: string; textEn?: string; textVi?: string } | null;
}

export interface MuunWindowOptions {
  send: (name: string, fields: PacketValues) => void;
  message: (text: string) => void;
}

export const MUUN_SUBCODE_STATE = 0x2c;
export const MUUN_COMMAND = { Request: 1, Equip: 2, Unequip: 3, Evolve: 4, Convert: 5, LevelUp: 6, Feed: 7 } as const;

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

/** Reads an F5 2C packet; `null` when it isn't one or its JSON can't be read. */
export function parseMuunState(bytes: Uint8Array): MuunStatePayload | null {
  if (bytes.length < 5 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== MUUN_SUBCODE_STATE) return null;
  const parsed = read('ServerToClient', 'HnmoMuunState', bytes);
  const json = typeof parsed.Json === 'string' ? parsed.Json : '';
  if (!json) return null;
  try {
    return JSON.parse(json) as MuunStatePayload;
  } catch {
    return null;
  }
}

/** The bonus line of the active Muun in the current language, with whether it applies now. */
export function describeActive(active: MuunActiveState): string {
  const bonus = isVi() ? active.bonusVi : active.bonusEn;
  const condition = isVi() ? active.conditionVi : active.conditionEn;
  const status = active.applies ? tx('active now', 'đang có hiệu lực') : tx('waiting', 'đang chờ');
  return `${bonus} • ${condition} (${status})`;
}

export class MuunWindow {
  private readonly panel: HnmoPanel;
  private state: MuunStatePayload | null = null;

  constructor(private readonly options: MuunWindowOptions) {
    this.panel = createHnmoPanel('hnmo-muun-panel', tx('Muun (Alt+N)', 'Muun (Alt+N)'));
    onLanguageChange(() => {
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.panel.open();
    this.send(MUUN_COMMAND.Request);
    this.render();
  }

  public close(): void {
    this.panel.close();
  }

  public toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  public handle(bytes: Uint8Array): boolean {
    const next = parseMuunState(bytes);
    if (!next) return bytes.length >= 5 && bytes[3] === 0xf5 && bytes[4] === MUUN_SUBCODE_STATE;
    this.state = next;
    if (next.event) {
      const text = isVi() ? (next.event.textVi || next.event.textEn) : (next.event.textEn || next.event.textVi);
      if (text) this.options.message(text);
    }

    if (this.panel.isOpen()) this.render();
    return true;
  }

  private send(action: number, slot = 0): void {
    this.options.send('HnmoMuunCommand', { Action: action, Slot: slot });
  }

  private button(label: string, onClick: () => void, disabled = false, id?: string): HTMLButtonElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'hnmo-btn';
    button.textContent = label;
    button.disabled = disabled;
    if (id) button.id = id;
    button.addEventListener('click', onClick);
    return button;
  }

  private render(): void {
    const body = this.panel.body;
    body.replaceChildren();
    const s = this.state;
    const note = (text: string): void => {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = text;
      body.append(p);
    };

    if (!s) {
      note(tx('Loading Muuns…', 'Đang tải Muun…'));
      return;
    }

    if (!s.enabled) {
      note(tx('Muuns are off on this server (Season 9 realms only).', 'Muun đang tắt trên server này (chỉ có ở Season 9).'));
      return;
    }

    const active = document.createElement('div');
    active.className = 'hnmo-card';
    active.id = 'muun-active';
    if (s.active) {
      const a = s.active;
      const left = document.createElement('div');
      const title = document.createElement('strong');
      const level = a.evolved ? tx('evolved', 'đã tiến hóa') : `${tx('level', 'cấp')} ${a.level}/${a.maxLevel}`;
      title.textContent = `${a.name} (${level})`;
      const line = document.createElement('div');
      line.className = 'hnmo-muted';
      line.textContent = describeActive(a);
      left.append(title, line);
      const right = document.createElement('div');
      right.className = 'hnmo-row';
      if (a.levelUpCost > 0) {
        right.append(this.button(tx(`Level up (${a.levelUpCost} energy)`, `Lên cấp (${a.levelUpCost} năng lượng)`), () => this.send(MUUN_COMMAND.LevelUp), s.energy < a.levelUpCost, 'muun-level-up'));
      }

      if (a.canEvolve) {
        right.append(this.button(tx(`Evolve (stones: ${s.stones ?? 0})`, `Tiến hóa (đá: ${s.stones ?? 0})`), () => this.send(MUUN_COMMAND.Evolve), (s.stones ?? 0) <= 0, 'muun-evolve'));
      }

      right.append(this.button(tx('Put away', 'Cất đi'), () => this.send(MUUN_COMMAND.Unequip), false, 'muun-unequip'));
      active.append(left, right);
    } else {
      const p = document.createElement('div');
      p.className = 'hnmo-muted';
      p.textContent = tx('No active Muun: choose one from your bag below.', 'Chưa có Muun đồng hành: chọn một Muun trong túi bên dưới.');
      active.append(p);
    }

    body.append(active);

    const energy = document.createElement('p');
    energy.className = 'hnmo-muted';
    energy.textContent = tx(`Energy: ${s.energy} • Energy converters in bag: ${s.converters ?? 0}`, `Năng lượng: ${s.energy} • Máy chuyển năng lượng trong túi: ${s.converters ?? 0}`);
    body.append(energy);

    if (s.bag.length === 0) {
      note(tx('No Muun in your bag.', 'Không có Muun trong túi.'));
      return;
    }

    for (const m of s.bag) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';
      card.setAttribute('data-muun-slot', String(m.slot));
      const left = document.createElement('div');
      const title = document.createElement('strong');
      title.textContent = `${m.name} (${m.evolved ? tx('evolved', 'đã tiến hóa') : `${tx('level', 'cấp')} ${m.level}`})`;
      left.append(title);
      const right = document.createElement('div');
      right.className = 'hnmo-row';
      if (m.active) {
        const tag = document.createElement('span');
        tag.className = 'hnmo-muted';
        tag.textContent = tx('Active', 'Đang đồng hành');
        right.append(tag);
      } else {
        const canFeed = Boolean(s.active && !s.active.evolved && s.active.level < s.active.maxLevel && s.active.group === m.group && s.active.number === m.number);
        if (canFeed) {
          right.append(this.button(tx('Feed (+1 lv)', 'Cho ăn (+1 cấp)'), () => this.send(MUUN_COMMAND.Feed, m.slot), false));
        }
        right.append(
          this.button(tx('Make active', 'Chọn đồng hành'), () => this.send(MUUN_COMMAND.Equip, m.slot)),
          this.button(tx(`Convert (+${m.energy})`, `Chuyển đổi (+${m.energy})`), () => this.send(MUUN_COMMAND.Convert, m.slot), (s.converters ?? 0) <= 0),
        );
      }

      card.append(left, right);
      body.append(card);
    }
  }
}
