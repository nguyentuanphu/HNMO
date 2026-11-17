// HNMO Combining Leftovers & Surplus Material Exchange window (F10 or Exchange NPC):
// lets players upgrade leftover Kundun boxes using jewels + Zen and trade surplus materials via F5 40 / F5 41.

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface LeftoverIngredientState {
  group: number;
  number: number;
  exactLevel: number;
  count: number;
  nameEn: string;
  nameVi: string;
  have: number;
}

export interface LeftoverRecipeState {
  id: string;
  nameEn: string;
  nameVi: string;
  enabled: boolean;
  successRatePercent: number;
  zenCost: number;
  outputGroup: number;
  outputNumber: number;
  outputLevel: number;
  requiredStageBoxLevel: number;
  blockedBySeasonStage: boolean;
  canCombine: boolean;
  inputs: LeftoverIngredientState[];
}

export interface LeftoverOfferState {
  id: string;
  nameEn: string;
  nameVi: string;
  enabled: boolean;
  inputCount: number;
  have: number;
  rewardCoins: number;
  rewardCoinType: string;
  rewardZen: number;
  rewardItemCount: number;
  canExchange: boolean;
}

export interface LeftoversStatePayload {
  v: number;
  enabled: boolean;
  exchangeNpcNumber: number;
  effectiveMaxBoxLevel: number;
  zen?: number | null;
  notice?: { status: string; messageEn: string; messageVi: string } | null;
  recipes: LeftoverRecipeState[];
  offers: LeftoverOfferState[];
}

const SUBCODE_STATE = 0x41;

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

export class LeftoversWindow {
  private readonly panel: HnmoPanel;
  private state: LeftoversStatePayload | null = null;
  private activeTab: 'combine' | 'exchange' = 'combine';

  constructor(private readonly send: (name: string, fields: PacketValues) => void) {
    this.panel = createHnmoPanel('hnmo-leftovers-panel', tx('Combining Leftovers & Exchange (F10)', 'Đổi Đồ Dư & Nâng Hộp (F10)'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Combining Leftovers & Exchange (F10)', 'Đổi Đồ Dư & Nâng Hộp (F10)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.panel.setTitle(tx('Combining Leftovers & Exchange (F10)', 'Đổi Đồ Dư & Nâng Hộp (F10)'));
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
    this.send('HnmoLeftoversCommand', { Action: 0, Id: '' });
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) {
      return false;
    }

    const parsed = read('ServerToClient', 'HnmoLeftoversState', bytes);
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!json) return true;
    try {
      this.state = JSON.parse(json) as LeftoversStatePayload;
      if (!this.panel.isOpen()) {
        this.panel.open();
      }
      this.render();
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
      p.textContent = tx('Loading recipes & exchange offers...', 'Đang tải công thức ghép & gói đổi...');
      body.appendChild(p);
      return;
    }

    if (!this.state.enabled) {
      const p = document.createElement('div');
      p.className = 'hnmo-muted';
      p.textContent = tx('Combining Leftovers is turned off by the server admin.', 'Tính năng Đổi đồ dư đang tắt.');
      body.appendChild(p);
      return;
    }

    const top = document.createElement('div');
    top.style.display = 'flex';
    top.style.justifyContent = 'space-between';
    top.style.alignItems = 'center';
    top.style.flexWrap = 'wrap';
    top.style.gap = '8px';

    const tabs = document.createElement('div');
    tabs.style.display = 'flex';
    tabs.style.gap = '6px';
    const mkTab = (key: 'combine' | 'exchange', label: string) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.textContent = label;
      b.className = 'hnmo-panel-close';
      b.style.width = 'auto';
      b.style.padding = '0 10px';
      b.style.background = this.activeTab === key ? '#6e5225' : '#2d2316';
      b.onclick = () => { this.activeTab = key; this.render(); };
      return b;
    };
    tabs.append(
      mkTab('combine', tx('Box Upgrade', 'Nâng cấp Hộp')),
      mkTab('exchange', tx('Surplus Exchange', 'Đổi nguyên liệu dư')),
    );

    const meta = document.createElement('span');
    meta.className = 'hnmo-badge';
    meta.textContent = tx(`Season box cap: +${this.state.effectiveMaxBoxLevel}`, `Giới hạn hộp mùa: +${this.state.effectiveMaxBoxLevel}`);
    top.append(tabs, meta);
    body.appendChild(top);

    if (this.state.notice) {
      const note = document.createElement('div');
      note.className = 'hnmo-card';
      note.style.borderColor = this.state.notice.status === 'ok' ? '#4d7c3f' : '#945238';
      note.textContent = isVi() ? this.state.notice.messageVi : this.state.notice.messageEn;
      body.appendChild(note);
    }

    if (this.activeTab === 'combine') {
      for (const r of this.state.recipes.filter((x) => x.enabled)) {
        const card = document.createElement('div');
        card.className = 'hnmo-card';
        const left = document.createElement('div');
        const title = document.createElement('div');
        title.innerHTML = `<strong>${isVi() ? r.nameVi : r.nameEn}</strong> <span class="hnmo-badge">${r.successRatePercent}%</span>`;
        const sub = document.createElement('div');
        sub.className = 'hnmo-muted';
        const ingText = r.inputs.map((i) => `${isVi() ? i.nameVi : i.nameEn} (${i.have}/${i.count})`).join(' · ');
        sub.textContent = `${ingText} · ${r.zenCost.toLocaleString()} Zen`;
        left.append(title, sub);
        if (r.blockedBySeasonStage) {
          const b = document.createElement('div');
          b.className = 'hnmo-muted';
          b.style.color = '#e79e72';
          b.textContent = tx(`Locked until Season Box +${r.requiredStageBoxLevel} opens`, `Khóa tới mốc Hộp +${r.requiredStageBoxLevel} của mùa`);
          left.appendChild(b);
        }

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'hnmo-panel-close';
        btn.style.width = 'auto';
        btn.style.padding = '0 12px';
        btn.disabled = !r.canCombine;
        btn.style.opacity = r.canCombine ? '1' : '0.45';
        btn.textContent = tx('Combine', 'Ghép');
        btn.onclick = () => this.send('HnmoLeftoversCommand', { Action: 1, Id: r.id });

        card.append(left, btn);
        body.appendChild(card);
      }
    } else {
      for (const o of this.state.offers.filter((x) => x.enabled)) {
        const card = document.createElement('div');
        card.className = 'hnmo-card';
        const left = document.createElement('div');
        const title = document.createElement('div');
        title.innerHTML = `<strong>${isVi() ? o.nameVi : o.nameEn}</strong>`;
        const sub = document.createElement('div');
        sub.className = 'hnmo-muted';
        sub.textContent = tx(`In bag: ${o.have}/${o.inputCount}`, `Trong túi: ${o.have}/${o.inputCount}`);
        left.append(title, sub);

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'hnmo-panel-close';
        btn.style.width = 'auto';
        btn.style.padding = '0 12px';
        btn.disabled = !o.canExchange;
        btn.style.opacity = o.canExchange ? '1' : '0.45';
        btn.textContent = tx('Exchange', 'Đổi');
        btn.onclick = () => this.send('HnmoLeftoversCommand', { Action: 2, Id: o.id });

        card.append(left, btn);
        body.appendChild(card);
      }
    }
  }
}
