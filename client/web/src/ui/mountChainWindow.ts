// HNMO Mount Chain collection & upgrade window (Shift+U): Golden Boss shard tracking, duplicate mount upgrades/salvage,
// and modest PvE aura selection via packets F5 42 / F5 43.

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';
import { escapeHtml } from './escapeHtml.js';

export interface MountTierState {
  id: string;
  nameEn: string;
  nameVi: string;
  shardsRequired: number;
  previousTierId: string;
  previousMountsRequired: number;
  zenCost: number;
  shardsOnSalvage: number;
  pveDefenseBonus: number;
  bonusExpPercent: number;
  bonusZenPercent: number;
  collectorsCount: number;
  unlocked: boolean;
  unlockedAt: number | null;
  bagDuplicates: number;
  canCraft: boolean;
}

export interface MountChainStatePayload {
  v: number;
  enabled: boolean;
  shards: number;
  activeTierId: string;
  notice?: { status: string; messageEn: string; messageVi: string } | null;
  tiers: MountTierState[];
}

const SUBCODE_STATE = 0x43;

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

export class MountChainWindow {
  private readonly panel: HnmoPanel;
  private state: MountChainStatePayload | null = null;

  constructor(private readonly send: (name: string, fields: PacketValues) => void) {
    this.panel = createHnmoPanel('hnmo-mount-chain-panel', tx('Mount Chain Collection (Shift+U)', 'Bộ Sưu Tập Thú Cưỡi (Shift+U)'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Mount Chain Collection (Shift+U)', 'Bộ Sưu Tập Thú Cưỡi (Shift+U)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.panel.setTitle(tx('Mount Chain Collection (Shift+U)', 'Bộ Sưu Tập Thú Cưỡi (Shift+U)'));
    this.panel.open();
    this.requestState();
    this.render();
  }

  public close(): void {
    this.panel.close();
  }

  public toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  public requestState(): void {
    this.send('HnmoMountChainCommand', { Action: 0, Id: '' });
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) {
      return false;
    }

    const parsed = read('ServerToClient', 'HnmoMountChainState', bytes);
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!json) return true;
    try {
      this.state = JSON.parse(json) as MountChainStatePayload;
      if (this.panel.isOpen()) this.render();
    } catch {
      // Ignore malformed payload.
    }
    return true;
  }

  private render(): void {
    const body = this.panel.body;
    body.innerHTML = '';

    if (!this.state) {
      const p = document.createElement('div');
      p.className = 'hnmo-muted';
      p.textContent = tx('Loading mount collection...', 'Đang tải bộ sưu tập thú cưỡi...');
      body.appendChild(p);
      return;
    }

    if (!this.state.enabled) {
      const p = document.createElement('div');
      p.className = 'hnmo-muted';
      p.textContent = tx('Mount Chain is turned off by the server admin.', 'Chuỗi thú cưỡi hiện đang tắt.');
      body.appendChild(p);
      return;
    }

    const head = document.createElement('div');
    head.className = 'hnmo-card';
    head.innerHTML = `<span>${tx('Mount Shards (from Golden Bosses)', 'Mảnh Thú Cưỡi (từ Boss Vàng)')}</span><strong class="hnmo-badge">${this.state.shards}</strong>`;
    body.appendChild(head);

    if (this.state.notice) {
      const note = document.createElement('div');
      note.className = 'hnmo-card';
      note.style.borderColor = this.state.notice.status === 'ok' ? '#4d7c3f' : '#945238';
      note.textContent = isVi() ? this.state.notice.messageVi : this.state.notice.messageEn;
      body.appendChild(note);
    }

    for (const t of this.state.tiers) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';
      const isActive = t.id === this.state.activeTierId;
      if (t.unlocked) card.style.borderColor = isActive ? '#e3b857' : '#4a6b3d';

      const left = document.createElement('div');
      const status = isActive ? tx('ACTIVE', 'ĐANG DÙNG') : t.unlocked ? tx('UNLOCKED', 'ĐÃ MỞ') : tx('LOCKED', 'CHƯA MỞ');
      left.innerHTML = `<div><strong>${escapeHtml(isVi() ? t.nameVi : t.nameEn)}</strong> <span class="hnmo-badge">${status}</span></div>`
        + `<div class="hnmo-muted">${tx('Cost', 'Cần')}: ${t.shardsRequired} ${tx('shards', 'mảnh')}`
        + (t.previousTierId && t.previousMountsRequired > 0 ? ` + ${Number(t.previousMountsRequired)}x ${escapeHtml(t.previousTierId)}` : '')
        + ` · ${t.zenCost.toLocaleString()} Zen · +${t.pveDefenseBonus} Def, +${t.bonusExpPercent}% EXP`
        + ` · ${tx('In bag', 'Trong túi')}: ${t.bagDuplicates}</div>`;

      const actions = document.createElement('div');
      actions.style.display = 'flex';
      actions.style.gap = '6px';

      const mkBtn = (label: string, enabled: boolean, onClick: () => void) => {
        const b = document.createElement('button');
        b.type = 'button';
        b.className = 'hnmo-panel-close';
        b.style.width = 'auto';
        b.style.padding = '0 9px';
        b.disabled = !enabled;
        b.style.opacity = enabled ? '1' : '0.45';
        b.textContent = label;
        b.onclick = onClick;
        return b;
      };

      actions.append(mkBtn(tx('Forge', 'Chế tạo'), t.canCraft, () => this.send('HnmoMountChainCommand', { Action: 1, Id: t.id })));
      if (t.unlocked && !isActive) {
        actions.append(mkBtn(tx('Activate', 'Kích hoạt'), true, () => this.send('HnmoMountChainCommand', { Action: 2, Id: t.id })));
      }
      if (t.bagDuplicates > 0) {
        actions.append(mkBtn(tx('Salvage', 'Tách mảnh'), true, () => this.send('HnmoMountChainCommand', { Action: 3, Id: t.id })));
      }

      card.append(left, actions);
      body.appendChild(card);
    }
  }
}
