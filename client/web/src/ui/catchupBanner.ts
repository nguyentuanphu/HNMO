// HNMO Catch-up Level & Reset Cap in-game indicator + details viewer (F5 3C / F5 3D).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface CatchupStatePayload {
  v: number;
  enabled: boolean;
  now: number;
  nextDayAt: number;
  levelCap: {
    enabled: boolean;
    todayCap: number;
    initialCap: number;
    hardMax: number;
    perDay: number;
    triggered: boolean;
    firstPlayer: string;
  };
  resetCap: {
    enabled: boolean;
    todayDailyLimit: number;
    initialDailyLimit: number;
    perDay: number;
    hardMax: number;
    triggerResets: number;
    triggered: boolean;
    firstPlayer: string;
  };
}

const SUBCODE_STATE = 0x3D;

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

function sydneyText(ms: number): string {
  return new Intl.DateTimeFormat(isVi() ? 'vi-VN' : 'en-AU', {
    month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'Australia/Sydney',
  }).format(ms);
}

export class CatchupBanner {
  private readonly panel: HnmoPanel;
  private state: CatchupStatePayload | null = null;

  constructor(private readonly send: (name: string, fields: PacketValues) => void) {
    this.panel = createHnmoPanel('hnmo-catchup-panel', tx("Today's Catch-up Cap", 'Giới Hạn Đuổi Kịp Hôm Nay'));
    onLanguageChange(() => {
      this.panel.setTitle(tx("Today's Catch-up Cap", 'Giới Hạn Đuổi Kịp Hôm Nay'));
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.panel.setTitle(tx("Today's Catch-up Cap", 'Giới Hạn Đuổi Kịp Hôm Nay'));
    this.panel.open();
    this.request();
    this.render();
  }

  public toggle(): void {
    if (this.panel.isOpen()) this.panel.close();
    else this.open();
  }

  public request(): void {
    this.send('HnmoCatchupCapRequest', {});
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 4 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) {
      return false;
    }

    const parsed = read('ServerToClient', 'HnmoCatchupCapState', bytes);
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!json) return true;
    try {
      this.state = JSON.parse(json) as CatchupStatePayload;
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
    if (!s || !s.enabled) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('Catch-up level cap is disabled on this server.', 'Giới hạn đuổi kịp hiện đang tắt trên máy chủ.');
      body.append(p);
      return;
    }

    if (s.levelCap.enabled) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';
      const left = document.createElement('div');
      const h = document.createElement('strong');
      h.textContent = tx(`Today's level cap: Lv.${s.levelCap.todayCap} / ${s.levelCap.hardMax}`, `Giới hạn cấp hôm nay: Cấp ${s.levelCap.todayCap} / ${s.levelCap.hardMax}`);
      const sub = document.createElement('div');
      sub.className = 'hnmo-muted';
      sub.textContent = s.levelCap.triggered
        ? tx(`Unlocked by ${s.levelCap.firstPlayer} (+${s.levelCap.perDay}/day in Sydney time).`, `Đã mở bởi ${s.levelCap.firstPlayer} (+${s.levelCap.perDay} cấp/ngày giờ Sydney).`)
        : tx(`Rises +${s.levelCap.perDay}/day once first player reaches Lv.${s.levelCap.initialCap}.`, `Tăng +${s.levelCap.perDay} cấp/ngày sau khi người đầu tiên đạt cấp ${s.levelCap.initialCap}.`);
      left.append(h, sub);
      card.append(left);
      body.append(card);
    }

    if (s.resetCap.enabled) {
      const rCard = document.createElement('div');
      rCard.className = 'hnmo-card';
      const left = document.createElement('div');
      const h = document.createElement('strong');
      h.textContent = tx(`Today's daily reset limit: ${s.resetCap.todayDailyLimit}`, `Giới hạn reset hôm nay: ${s.resetCap.todayDailyLimit}`);
      const sub = document.createElement('div');
      sub.className = 'hnmo-muted';
      sub.textContent = s.resetCap.triggered
        ? tx(`Rises +${s.resetCap.perDay}/day up to ${s.resetCap.hardMax}/day.`, `Tăng +${s.resetCap.perDay} lần/ngày (tối đa ${s.resetCap.hardMax}).`)
        : tx(`Starts rising once someone reaches ${s.resetCap.triggerResets} resets.`, `Bắt đầu tăng khi có người đạt ${s.resetCap.triggerResets} reset.`);
      left.append(h, sub);
      rCard.append(left);
      body.append(rCard);
    }

    const foot = document.createElement('div');
    foot.className = 'hnmo-muted';
    foot.textContent = `${tx('Next cap rise (Sydney time)', 'Lần tăng tiếp theo (giờ Sydney)')}: ${sydneyText(s.nextDayAt)}`;
    body.append(foot);
  }
}
