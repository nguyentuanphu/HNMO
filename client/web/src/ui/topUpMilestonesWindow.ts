// In-game Top-up milestones window ("thưởng mốc nạp"): opened with U key or touch "More" menu.

import { currentLanguage } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { HnmoModalPanel } from './hnmoPanel.js';

export interface TopUpMilestoneStepView {
  id: string;
  thresholdVnd: number;
  titleEn: string;
  titleVi: string;
  coinType: string;
  coins: number;
  zen: number;
  items: { group: number; number: number; level: number; quantity: number; name?: string }[];
  reached: boolean;
  claimed: boolean;
  claimable: boolean;
}

export interface TopUpMilestonesState {
  v: number;
  enabled: boolean;
  allowWebsite?: boolean;
  totalVnd: number;
  nextThresholdVnd?: number | null;
  steps: TopUpMilestoneStepView[];
  owed?: { reference: string; stepId: string; zen: number; items: { group: number; number: number; level: number; count: number; name?: string }[] }[];
  history?: { atUtc: string; atSydney: string; kind: string; amountVnd: number; detail: string }[];
  result?: string | null;
  messageEn?: string | null;
  messageVi?: string | null;
}

export interface TopUpMilestonesWindowOptions {
  send: (name: string, fields: PacketValues) => void;
  message: (text: string) => void;
}

const ACTION = { Request: 0, Claim: 1, TakeOwed: 2 } as const;

export class TopUpMilestonesWindow {
  private readonly panel: HnmoModalPanel;
  private state: TopUpMilestonesState | null = null;
  private requestedOnce = false;

  constructor(private readonly options: TopUpMilestonesWindowOptions) {
    this.panel = new HnmoModalPanel('hnmo_topup_milestones', '', () => this.render());
    window.addEventListener('hnmo-topup-milestones-open', () => this.open());
    this.render();
  }

  get isOpen(): boolean {
    return this.panel.isOpen;
  }

  get currentState(): TopUpMilestonesState | null {
    return this.state;
  }

  open(): void {
    this.panel.open();
    this.requestState();
    this.render();
  }

  close(): void {
    this.panel.close();
  }

  toggle(): void {
    if (this.isOpen) this.close();
    else this.open();
  }

  requestState(): void {
    this.requestedOnce = true;
    this.options.send('HnmoTopUpMilestonesCommand', { Action: ACTION.Request, Argument: '' });
  }

  handlePacket(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x31) return false;
    const packet = read('ServerToClient', 'HnmoTopUpMilestonesState', bytes);
    const json = typeof packet?.Json === 'string' ? packet.Json : '';
    try {
      this.state = JSON.parse(json) as TopUpMilestonesState;
      const vi = currentLanguage() === 'vi';
      const notice = vi ? this.state.messageVi : this.state.messageEn;
      if (notice) this.options.message(notice);
      this.render();
    } catch {
      /* malformed JSON ignored */
    }
    return true;
  }

  private render(): void {
    const vi = currentLanguage() === 'vi';
    const L = (en: string, v: string): string => (vi ? v : en);
    const fmt = (n: number): string => n.toLocaleString(vi ? 'vi-VN' : 'en-AU');

    this.panel.setTitle(L('Top-up milestones (Shift+T)', 'Thưởng mốc nạp (Shift+T)'));
    const body = this.panel.body;
    body.replaceChildren();

    const s = this.state;
    if (!s) {
      const p = document.createElement('div');
      p.className = 'hnmo-muted';
      p.textContent = this.requestedOnce
        ? L('Top-up milestones are currently off or waiting for server response.', 'Thưởng mốc nạp đang tắt hoặc đang chờ máy chủ.')
        : L('Loading milestone progress…', 'Đang tải tiến độ mốc nạp…');
      body.append(p);
      return;
    }

    if (!s.enabled) {
      const off = document.createElement('div');
      off.className = 'hnmo-card hnmo-muted';
      off.textContent = L('Top-up milestones are turned off by the host.', 'Chủ server hiện đang tắt thưởng mốc nạp.');
      body.append(off);
      return;
    }

    // Summary & progress bar card
    const summary = document.createElement('div');
    summary.className = 'hnmo-card';
    const totalLine = document.createElement('div');
    totalLine.innerHTML = `<strong>${L('Confirmed total', 'Tổng nạp xác nhận')}:</strong> ${fmt(s.totalVnd)} VND`;
    const nextTarget = s.nextThresholdVnd ?? (s.steps.length > 0 ? s.steps[s.steps.length - 1].thresholdVnd : 0);
    const pct = nextTarget > 0 ? Math.min(100, Math.round((s.totalVnd / nextTarget) * 100)) : 100;
    const nextLine = document.createElement('div');
    nextLine.className = 'hnmo-muted';
    nextLine.textContent = s.nextThresholdVnd
      ? L(`Next milestone: ${fmt(s.nextThresholdVnd)} VND (${pct}%)`, `Mốc tiếp theo: ${fmt(s.nextThresholdVnd)} đ (${pct}%)`)
      : L('All configured milestones reached!', 'Bạn đã đạt tất cả các mốc nạp!');
    const bar = document.createElement('div');
    bar.className = 'hnmo-bar';
    const fill = document.createElement('div');
    fill.style.width = `${pct}%`;
    bar.append(fill);

    const linksRow = document.createElement('div');
    linksRow.className = 'hnmo-muted';
    linksRow.textContent = L('Top up on the website top-up page to raise your total.', 'Nạp qua trang Nạp coin trên website để tăng tổng mốc.');
    summary.append(totalLine, nextLine, bar, linksRow);
    body.append(summary);

    // Waiting bag rewards
    if (s.owed && s.owed.length > 0) {
      const owedCard = document.createElement('div');
      owedCard.className = 'hnmo-card';
      const title = document.createElement('strong');
      title.className = 'hnmo-warn';
      title.textContent = L(`Rewards waiting for bag room (${s.owed.length})`, `Phần thưởng chờ túi trống (${s.owed.length})`);
      const takeBtn = document.createElement('button');
      takeBtn.type = 'button';
      takeBtn.className = 'hnmo-btn hnmo-btn-primary';
      takeBtn.textContent = L('Take items', 'Nhận vật phẩm');
      takeBtn.addEventListener('click', () => this.options.send('HnmoTopUpMilestonesCommand', { Action: ACTION.TakeOwed, Argument: '' }));
      owedCard.append(title, takeBtn);
      body.append(owedCard);
    }

    // Steps list
    for (const step of s.steps) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';
      card.dataset.stepId = step.id;
      const top = document.createElement('div');
      top.style.cssText = 'display:flex;justify-content:space-between;align-items:center;gap:8px;flex-wrap:wrap;';
      const left = document.createElement('div');
      const title = document.createElement('strong');
      title.textContent = `${fmt(step.thresholdVnd)} đ — ${vi && step.titleVi ? step.titleVi : step.titleEn}`;
      const rewardParts: string[] = [];
      if (step.coins > 0) rewardParts.push(`${fmt(step.coins)} ${step.coinType}`);
      if (step.zen > 0) rewardParts.push(`${fmt(step.zen)} Zen`);
      for (const item of step.items) {
        rewardParts.push(`${item.name ?? `Item ${item.group}:${item.number}`}${item.level > 0 ? ` +${item.level}` : ''} ×${item.quantity}`);
      }
      const rewardLine = document.createElement('div');
      rewardLine.className = 'hnmo-muted';
      rewardLine.textContent = rewardParts.join(' · ');
      left.append(title, rewardLine);

      const actionBtn = document.createElement('button');
      actionBtn.type = 'button';
      if (step.claimed) {
        actionBtn.className = 'hnmo-btn';
        actionBtn.disabled = true;
        actionBtn.textContent = L('Claimed', 'Đã nhận');
      } else if (step.claimable) {
        actionBtn.className = 'hnmo-btn hnmo-btn-primary';
        actionBtn.textContent = L('Claim', 'Nhận thưởng');
        actionBtn.addEventListener('click', () => {
          this.options.send('HnmoTopUpMilestonesCommand', { Action: ACTION.Claim, Argument: step.id });
        });
      } else {
        actionBtn.className = 'hnmo-btn';
        actionBtn.disabled = true;
        actionBtn.textContent = L('Locked', 'Chưa đạt');
      }

      top.append(left, actionBtn);
      card.append(top);
      body.append(card);
    }
  }
}
