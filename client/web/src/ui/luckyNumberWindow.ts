// HNMO Daily Lucky Number window (L key, touch "more" menu).
// Displays active draw countdown (Sydney + local time), odds & prizes, number picker, owed jewels Take, and recent draw winners.

import { currentLanguage } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { HnmoModalPanel } from './hnmoPanel.js';
import { coinRewardsShown } from './coinRewards.js';

/** A prize's coins and jewels; no coins on a season without a cash shop (classic play). */
function prizeText(prize: { coins: number; coinType: string; jewelCount: number; jewelName: string }): string {
  const parts: string[] = [];
  if (prize.coins > 0 && coinRewardsShown()) parts.push(`${prize.coins} ${prize.coinType}`);
  if (prize.jewelCount > 0) parts.push(`${prize.jewelCount}x ${prize.jewelName}`);
  return parts.join(' + ') || '-';
}

export interface LuckyNumberStatePayload {
  v: number;
  enabled: boolean;
  allowWebsite: boolean;
  now: number;
  timeZone: string;
  drawTime: string;
  roundKey: string;
  nextDrawAt: number;
  minNumber: number;
  maxNumber: number;
  maxPicksPerDay: number;
  entryCostZen: number;
  odds: { exactPercent: number; lastDigitPercent: number };
  prizes: {
    exact: { coins: number; coinType: string; jewelCount: number; jewelName: string };
    lastDigit: { enabled: boolean; coins: number; coinType: string; jewelCount: number; jewelName: string };
  };
  totalPicksToday: number;
  myPicks: { number: number; source: string; at: number }[];
  owed: { id: string; dateKey: string; tier: string; count: number; name: string }[];
  recentDraws: {
    dateKey: string;
    winningNumber: number;
    drawnAt: number;
    pickCount: number;
    winners: { name: string; number: number; tier: string; coins: number; coinType: string; jewelCount: number }[];
  }[];
  event?: { result: string; textEn?: string; textVi?: string } | null;
}

export interface LuckyNumberWindowOptions {
  send: (name: string, fields: PacketValues) => void;
  message: (text: string) => void;
}

const ACTION = { Request: 1, Pick: 2, ClaimOwed: 3 } as const;
const SYDNEY_TZ = 'Australia/Sydney';

function L(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

function el<K extends keyof HTMLElementTagNameMap>(tag: K, text?: string, className?: string): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function formatClock(ms: number, timeZone?: string): string {
  return new Intl.DateTimeFormat(currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU', {
    weekday: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    ...(timeZone ? { timeZone } : {}),
  }).format(ms);
}

export class LuckyNumberWindow {
  private readonly panel: HnmoModalPanel;
  private state: LuckyNumberStatePayload | null = null;
  private skew = 0;
  private timer: number | null = null;
  private countdownEl: HTMLElement | null = null;

  constructor(private readonly options: LuckyNumberWindowOptions) {
    this.panel = new HnmoModalPanel('hnmoLuckyNumber', '', () => this.render());
    this.panel.onClose(() => {
      if (this.timer !== null) {
        window.clearInterval(this.timer);
        this.timer = null;
      }
      this.countdownEl = null;
    });
  }

  get isOpen(): boolean {
    return this.panel.isOpen;
  }

  start(): void {
    this.request();
  }

  request(): void {
    this.options.send('HnmoLuckyNumberCommand', { Action: ACTION.Request, Number: 0 });
  }

  open(): void {
    this.panel.open();
    this.request();
    this.render();
    if (this.timer === null) {
      this.timer = window.setInterval(() => {
        if (this.panel.isOpen) this.updateCountdown();
      }, 1000);
    }
  }

  close(): void {
    this.panel.close();
  }

  toggle(): void {
    if (this.panel.isOpen) this.close();
    else this.open();
  }

  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x35) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoLuckyNumberState', bytes);
    if (!Json) return true;
    try {
      const parsed = JSON.parse(Json) as LuckyNumberStatePayload;
      if (typeof parsed !== 'object' || parsed === null) return true;
      this.state = parsed;
      if (typeof parsed.now === 'number') this.skew = parsed.now - Date.now();
      if (parsed.event?.textEn || parsed.event?.textVi) {
        this.options.message(L(parsed.event.textEn ?? '', parsed.event.textVi ?? parsed.event.textEn ?? ''));
      }
      if (this.panel.isOpen) this.render();
    } catch {
      // Ignore malformed JSON.
    }
    return true;
  }

  render(): void {
    this.panel.setTitle(L('Daily Lucky Number (L)', 'Con số may mắn (L)'));
    const body = this.panel.body;
    body.replaceChildren();

    const s = this.state;
    if (!s) {
      body.append(el('p', L('Loading Daily Lucky Number...', 'Đang tải Con số may mắn...'), 'hnmo-muted'));
      return;
    }

    if (!s.enabled) {
      body.append(el('p', L('Daily Lucky Number is currently turned off on this server.', 'Tính năng Con số may mắn hiện đang tắt trên máy chủ.'), 'hnmo-muted'));
      return;
    }

    const summary = el('div', undefined, 'hnmo-card');
    this.countdownEl = el('div');
    this.updateCountdown();
    summary.append(
      this.countdownEl,
      el('div', L(`Range: ${s.minNumber}–${s.maxNumber} · Odds: ${s.odds.exactPercent}% exact`, `Khoảng số: ${s.minNumber}–${s.maxNumber} · Tỷ lệ trúng đúng: ${s.odds.exactPercent}%`) + (s.prizes.lastDigit.enabled ? L(` · ${s.odds.lastDigitPercent}% last digit`, ` · ${s.odds.lastDigitPercent}% số cuối`) : ''), 'hnmo-muted'),
      el('div', L(`Exact prize: ${prizeText(s.prizes.exact)}`, `Giải chính: ${prizeText(s.prizes.exact)}`)),
    );
    if (s.prizes.lastDigit.enabled) {
      summary.append(el('div', L(`Last-digit prize: ${prizeText(s.prizes.lastDigit)}`, `Giải số cuối: ${prizeText(s.prizes.lastDigit)}`), 'hnmo-muted'));
    }
    body.append(summary);

    const pickCard = el('div', undefined, 'hnmo-card');
    pickCard.append(el('strong', L(`Your picks (${s.myPicks.length}/${s.maxPicksPerDay})`, `Số bạn đã chọn (${s.myPicks.length}/${s.maxPicksPerDay})`)));
    if (s.myPicks.length > 0) {
      pickCard.append(el('div', s.myPicks.map((p) => `#${String(p.number).padStart(2, '0')} (${p.source})`).join(' · '), 'hnmo-ok'));
    }

    if (s.myPicks.length < s.maxPicksPerDay) {
      const form = el('form');
      form.style.display = 'flex';
      form.style.gap = '8px';
      form.style.alignItems = 'center';
      const input = document.createElement('input');
      input.type = 'number';
      input.min = String(s.minNumber);
      input.max = String(s.maxNumber);
      input.placeholder = `${s.minNumber}-${s.maxNumber}`;
      input.style.cssText = 'width:110px;padding:4px 8px;border-radius:5px;border:1px solid #8a6a30;background:#111520;color:#fff;';
      const submitBtn = el('button', s.entryCostZen > 0 ? L(`Pick (${s.entryCostZen.toLocaleString()} Zen)`, `Chọn (${s.entryCostZen.toLocaleString()} Zen)`) : L('Pick number', 'Chọn số'), 'hnmo-btn hnmo-btn-primary');
      submitBtn.type = 'submit';
      form.addEventListener('submit', (e) => {
        e.preventDefault();
        const num = Number.parseInt(input.value, 10);
        if (Number.isNaN(num)) return;
        this.options.send('HnmoLuckyNumberCommand', { Action: ACTION.Pick, Number: num });
      });
      form.append(input, submitBtn);
      pickCard.append(form);
    } else {
      pickCard.append(el('div', L('Wait for the next draw for your next pick!', 'Hãy chờ kỳ quay tiếp theo!'), 'hnmo-muted'));
    }
    body.append(pickCard);

    if (s.owed.length > 0) {
      const owedCard = el('div', undefined, 'hnmo-card');
      owedCard.append(el('strong', L('Jewels waiting for bag space', 'Ngọc chờ nhận (dọn chỗ túi đồ)'), 'hnmo-warn'));
      for (const o of s.owed) {
        owedCard.append(el('div', `${o.count}x ${o.name} (${o.dateKey})`));
      }
      const takeBtn = el('button', L('Take', 'Nhận vào túi'), 'hnmo-btn hnmo-btn-primary');
      takeBtn.type = 'button';
      takeBtn.addEventListener('click', () => {
        this.options.send('HnmoLuckyNumberCommand', { Action: ACTION.ClaimOwed, Number: 0 });
      });
      owedCard.append(takeBtn);
      body.append(owedCard);
    }

    if (s.recentDraws.length > 0) {
      const histCard = el('div', undefined, 'hnmo-card');
      histCard.append(el('strong', L('Recent Draw Results', 'Kết quả quay gần đây')));
      for (const d of s.recentDraws.slice(0, 5)) {
        const winText = d.winners.length
          ? d.winners.slice(0, 3).map((w) => `${w.name} (#${String(w.number).padStart(2, '0')})`).join(', ')
          : L('No winners', 'Chưa có người trúng');
        histCard.append(el('div', `${d.dateKey} — #${String(d.winningNumber).padStart(2, '0')} · ${winText}`, 'hnmo-muted'));
      }
      body.append(histCard);
    }
  }

  private updateCountdown(): void {
    const s = this.state;
    const target = this.countdownEl;
    if (!s || !target) return;
    const nowMs = Date.now() + this.skew;
    const secLeft = Math.max(0, Math.round((s.nextDrawAt - nowMs) / 1000));
    const h = Math.floor(secLeft / 3600);
    const m = Math.floor((secLeft % 3600) / 60);
    const sec = secLeft % 60;
    target.textContent = L(
      `Round ${s.roundKey} · Draw at ${formatClock(s.nextDrawAt, SYDNEY_TZ)} (Sydney) / ${formatClock(s.nextDrawAt)} (Yours) · in ${h}h ${m}m ${sec}s`,
      `Kỳ ${s.roundKey} · Quay lúc ${formatClock(s.nextDrawAt, SYDNEY_TZ)} (Sydney) / ${formatClock(s.nextDrawAt)} (Bạn) · còn ${h}g ${m}p ${sec}s`,
    );
  }
}
