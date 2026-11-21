// HNMO Party Options companion dock & modal inside the P Party window (F5 44 / F5 45):
// supports /partypass password join without an invite and Auto-Party hunting spot finder.

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';
import { escapeHtml } from './escapeHtml.js';

export interface PartySpotAdState {
  leaderName: string;
  mapName: string;
  spotName: string;
  minLevel: number;
  maxLevel: number;
  hasPassword: boolean;
  memberCount: number;
  maxSize: number;
  canJoin: boolean;
}

export interface PartyOptionsStatePayload {
  v: number;
  enabled: boolean;
  allowPasswordJoin: boolean;
  allowAutoSpotParty: boolean;
  huntingSpots: string[];
  hasParty: boolean;
  isLeader: boolean;
  ownAd?: { spotName: string; hasPassword: boolean; minLevel: number; maxLevel: number } | null;
  notice?: { status: string; messageEn: string; messageVi: string } | null;
  ads: PartySpotAdState[];
}

const SUBCODE_STATE = 0x45;

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

export class PartyOptionsWindow {
  private readonly panel: HnmoPanel;
  private readonly dockBtn: HTMLButtonElement;
  private state: PartyOptionsStatePayload | null = null;
  private partyOpen = false;

  constructor(private readonly send: (name: string, fields: PacketValues) => void) {
    this.panel = createHnmoPanel('hnmo-party-options-panel', tx('Auto-Party & Password (P)', 'Tổ Đội Nhanh & Mật Khẩu (P)'));
    this.dockBtn = document.createElement('button');
    this.dockBtn.type = 'button';
    this.dockBtn.id = 'hnmo-party-options-dock-btn';
    this.dockBtn.textContent = tx('Auto-Party & Password', 'Tìm nhóm & Mật khẩu');
    Object.assign(this.dockBtn.style, {
      position: 'fixed',
      right: '200px',
      top: '76px',
      zIndex: '62',
      padding: '6px 10px',
      background: '#2c2214',
      border: '1px solid #9b7a3c',
      color: '#f2cf78',
      borderRadius: '4px',
      cursor: 'pointer',
      fontSize: '12px',
      display: 'none',
      pointerEvents: 'auto',
    });
    this.dockBtn.onclick = () => this.toggle();
    document.body.appendChild(this.dockBtn);

    onLanguageChange(() => {
      this.panel.setTitle(tx('Auto-Party & Password (P)', 'Tổ Đội Nhanh & Mật Khẩu (P)'));
      this.dockBtn.textContent = tx('Auto-Party & Password', 'Tìm nhóm & Mật khẩu');
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public setPartyWindowOpen(open: boolean): void {
    this.partyOpen = open;
    this.dockBtn.style.display = open ? 'block' : 'none';
    if (open) {
      this.requestState();
    } else {
      this.close();
    }
  }

  public open(): void {
    this.panel.setTitle(tx('Auto-Party & Password (P)', 'Tổ Đội Nhanh & Mật Khẩu (P)'));
    this.panel.open();
    this.requestState();
    this.render();
  }

  public close(): void {
    this.panel.close();
    if (!this.partyOpen) {
      this.dockBtn.style.display = 'none';
    }
  }

  public toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  public requestState(): void {
    this.send('HnmoPartyOptionsCommand', { Action: 0, Payload: '{}' });
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) {
      return false;
    }

    const parsed = read('ServerToClient', 'HnmoPartyOptionsState', bytes);
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!json) return true;
    try {
      this.state = JSON.parse(json) as PartyOptionsStatePayload;
      if (this.partyOpen) {
        this.dockBtn.style.display = this.state.enabled ? 'block' : 'none';
      }
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
      p.textContent = tx('Loading party options...', 'Đang tải danh sách tổ đội...');
      body.appendChild(p);
      return;
    }

    if (!this.state.enabled) {
      const p = document.createElement('div');
      p.className = 'hnmo-muted';
      p.textContent = tx('Party Options (/partypass & Auto-Party) is turned off.', 'Tính năng Tổ đội nhanh hiện đang tắt.');
      body.appendChild(p);
      return;
    }

    if (this.state.notice) {
      const note = document.createElement('div');
      note.className = 'hnmo-card';
      note.style.borderColor = this.state.notice.status === 'ok' ? '#4d7c3f' : '#945238';
      note.textContent = isVi() ? this.state.notice.messageVi : this.state.notice.messageEn;
      body.appendChild(note);
    }

    // Quick join by password form
    if (!this.state.hasParty && this.state.allowPasswordJoin) {
      const joinBox = document.createElement('div');
      joinBox.className = 'hnmo-card';
      const inp = document.createElement('input');
      inp.placeholder = tx('Enter party password (/partypass)', 'Nhập mật khẩu nhóm (/partypass)');
      Object.assign(inp.style, { flex: '1', background: '#14110c', color: '#efe6d2', border: '1px solid #5e4b28', padding: '5px 8px', borderRadius: '4px' });
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'hnmo-panel-close';
      btn.style.width = 'auto';
      btn.style.padding = '0 12px';
      btn.textContent = tx('Join by password', 'Vào bằng mật khẩu');
      btn.onclick = () => this.send('HnmoPartyOptionsCommand', { Action: 2, Payload: JSON.stringify({ password: inp.value }) });
      joinBox.append(inp, btn);
      body.appendChild(joinBox);
    }

    // Advertise spot / set password form (leader or solo)
    if (this.state.isLeader) {
      const adv = document.createElement('div');
      adv.className = 'hnmo-card';
      adv.style.flexDirection = 'column';
      adv.style.alignItems = 'stretch';

      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.gap = '6px';
      row.style.flexWrap = 'wrap';

      const spotInp = document.createElement('input');
      spotInp.placeholder = tx('Hunting spot (e.g. LostTower 7)', 'Bãi săn (vd LostTower 7)');
      spotInp.value = this.state.ownAd?.spotName ?? (this.state.huntingSpots[0] ?? 'Lorencia');
      Object.assign(spotInp.style, { flex: '1', background: '#14110c', color: '#efe6d2', border: '1px solid #5e4b28', padding: '5px 8px', borderRadius: '4px' });

      const passInp = document.createElement('input');
      passInp.placeholder = tx('Optional password', 'Mật khẩu (bỏ trống = tự vào)');
      Object.assign(passInp.style, { width: '160px', background: '#14110c', color: '#efe6d2', border: '1px solid #5e4b28', padding: '5px 8px', borderRadius: '4px' });

      const advBtn = document.createElement('button');
      advBtn.type = 'button';
      advBtn.className = 'hnmo-panel-close';
      advBtn.style.width = 'auto';
      advBtn.style.padding = '0 10px';
      advBtn.textContent = tx('Advertise spot', 'Đăng tìm nhóm');
      advBtn.onclick = () => this.send('HnmoPartyOptionsCommand', {
        Action: 1,
        Payload: JSON.stringify({ spot: spotInp.value, password: passInp.value, minLevel: 1, maxLevel: 0 }),
      });
      row.append(spotInp, passInp, advBtn);

      if (this.state.ownAd) {
        const stopBtn = document.createElement('button');
        stopBtn.type = 'button';
        stopBtn.className = 'hnmo-panel-close';
        stopBtn.style.width = 'auto';
        stopBtn.style.padding = '0 10px';
        stopBtn.textContent = tx('Stop', 'Tắt tin');
        stopBtn.onclick = () => this.send('HnmoPartyOptionsCommand', { Action: 4, Payload: '{}' });
        row.append(stopBtn);
      }

      adv.appendChild(row);
      body.appendChild(adv);
    }

    // Active hunting spot party list
    const listTitle = document.createElement('div');
    listTitle.className = 'hnmo-muted';
    listTitle.textContent = tx('Open hunting spot parties', 'Danh sách nhóm đang tìm người tại bãi săn');
    body.appendChild(listTitle);

    if (this.state.ads.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'hnmo-muted';
      empty.textContent = tx('No open spot parties listed right now.', 'Hiện chưa có nhóm nào đăng tìm người.');
      body.appendChild(empty);
      return;
    }

    for (const ad of this.state.ads) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';
      const left = document.createElement('div');
      const lockBadge = ad.hasPassword ? tx('Password', 'Mật khẩu') : tx('Auto-join', 'Tự vào');
      left.innerHTML = `<div><strong>${escapeHtml(ad.spotName)}</strong> <span class="hnmo-badge">${Number(ad.memberCount)}/${Number(ad.maxSize)}</span> <span class="hnmo-badge">${lockBadge}</span></div>`
        + `<div class="hnmo-muted">${tx('Leader', 'Trưởng nhóm')}: ${escapeHtml(ad.leaderName)} · ${escapeHtml(ad.mapName)}</div>`;

      const joinBtn = document.createElement('button');
      joinBtn.type = 'button';
      joinBtn.className = 'hnmo-panel-close';
      joinBtn.style.width = 'auto';
      joinBtn.style.padding = '0 12px';
      joinBtn.disabled = !ad.canJoin;
      joinBtn.style.opacity = ad.canJoin ? '1' : '0.45';
      joinBtn.textContent = tx('Join', 'Vào nhóm');
      joinBtn.onclick = () => this.send('HnmoPartyOptionsCommand', { Action: 3, Payload: JSON.stringify({ leader: ad.leaderName }) });

      card.append(left, joinBtn);
      body.appendChild(card);
    }
  }
}
