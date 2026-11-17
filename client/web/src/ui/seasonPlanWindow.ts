// HNMO Season Plan in-game timeline window (F8): displays seasonal progression stages, unlock conditions
// (Sydney dates, boss kills, player levels), and active gameplay feature gates (F5 3E / F5 3F).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface SeasonStageState {
  id: string;
  nameEn: string;
  nameVi: string;
  descriptionEn: string;
  descriptionVi: string;
  unlocks: string[];
  unlocked: boolean;
  unlockedAt: number | null;
  reason: string;
  unlockAt: number | null;
  requiredBossId: string;
  requiredBossKills: number;
  currentBossKills: number;
  requiredPlayerLevel: number;
  requiredPlayersCount: number;
  currentPlayers: number;
  maxBoxLevel: number;
}

export interface SeasonPlanStatePayload {
  v: number;
  enabled: boolean;
  now: number;
  maxBoxLevel: number;
  gates: Record<string, boolean>;
  stages: SeasonStageState[];
}

const SUBCODE_STATE = 0x3F;

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

function sydneyDate(ms: number): string {
  return new Intl.DateTimeFormat(isVi() ? 'vi-VN' : 'en-AU', {
    month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'Australia/Sydney',
  }).format(ms);
}

export class SeasonPlanWindow {
  private readonly panel: HnmoPanel;
  private state: SeasonPlanStatePayload | null = null;

  constructor(private readonly send: (name: string, fields: PacketValues) => void) {
    this.panel = createHnmoPanel('hnmo-season-plan-panel', tx('Season Plan (F8)', 'Lộ Trình Mùa Giải (F8)'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Season Plan (F8)', 'Lộ Trình Mùa Giải (F8)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.panel.setTitle(tx('Season Plan (F8)', 'Lộ Trình Mùa Giải (F8)'));
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
    this.send('HnmoSeasonPlanRequest', {});
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 4 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) {
      return false;
    }

    const parsed = read('ServerToClient', 'HnmoSeasonPlanState', bytes);
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!json) return true;
    try {
      this.state = JSON.parse(json) as SeasonPlanStatePayload;
      if (this.panel.isOpen()) this.render();
    } catch {
      // Ignore malformed JSON.
    }
    return true;
  }

  private render(): void {
    const body = this.panel.body;
    body.replaceChildren();
    const s = this.state;
    if (!s) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('Loading season timeline…', 'Đang tải lộ trình mùa giải…');
      body.append(p);
      return;
    }

    if (!s.enabled) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('Season Plan progressive stages are turned off.', 'Lộ trình mùa giải hiện đang tắt.');
      body.append(p);
      return;
    }

    for (const st of s.stages) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';
      card.style.borderColor = st.unlocked ? '#5fa354' : '#5a492e';

      const left = document.createElement('div');
      const title = document.createElement('strong');
      title.textContent = (isVi() ? st.nameVi : st.nameEn) || st.nameEn;

      const badge = document.createElement('span');
      badge.className = 'hnmo-badge';
      badge.style.marginLeft = '8px';
      badge.textContent = st.unlocked
        ? tx('OPEN', 'ĐÃ MỞ')
        : tx('LOCKED', 'KHÓA');

      const desc = document.createElement('div');
      desc.className = 'hnmo-muted';
      desc.textContent = (isVi() ? st.descriptionVi : st.descriptionEn) || st.descriptionEn;

      const condParts: string[] = [];
      if (st.unlockAt) condParts.push(`${tx('Date (Sydney)', 'Ngày (Sydney)')}: ${sydneyDate(st.unlockAt)}`);
      if (st.requiredBossId) condParts.push(`${tx('Boss', 'Boss')}: ${st.requiredBossId} (${st.currentBossKills}/${Math.max(1, st.requiredBossKills)})`);
      if (st.requiredPlayerLevel > 0 && st.requiredPlayersCount > 0) {
        condParts.push(`${tx('Players Lv.', 'Người chơi cấp ')}${st.requiredPlayerLevel}: ${st.currentPlayers}/${st.requiredPlayersCount}`);
      }
      if (condParts.length > 0) {
        const cond = document.createElement('div');
        cond.className = 'hnmo-muted';
        cond.textContent = condParts.join(' • ');
        left.append(title, badge, desc, cond);
      } else {
        left.append(title, badge, desc);
      }

      card.append(left);
      body.append(card);
    }
  }
}
