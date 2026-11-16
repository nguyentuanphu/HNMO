// HNMO Event & Boss Timetable window (H key, touch "more" menu, and (i) guide button).
// Displays events, invasions (Golden, Red Dragon, White Wizard, Rabbits, Snowmen, Find the GM),
// and world bosses with Sydney + player local times, countdown, map, and drops.

import { currentLanguage } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { HnmoModalPanel } from './hnmoPanel.js';

export interface TimetableEntryState {
  id: string;
  category: 'event' | 'invasion' | 'boss' | 'custom';
  nameEn: string;
  nameVi: string;
  mapsEn: string;
  mapsVi: string;
  dropsEn: string;
  dropsVi: string;
  schedule: string;
  durationMin: number;
  active: boolean;
  nextStart: number | null;
  endsAt: number | null;
}

export interface FindGmWinnerState {
  name: string;
  map: string;
  coins: number;
  coinType: string;
  zen: number;
  at: number;
}

export interface TimetableStatePayload {
  v: number;
  off?: boolean;
  now: number;
  timeZone: string;
  entries: TimetableEntryState[];
  findGmWinners?: FindGmWinnerState[];
}

export interface TimetableWindowOptions {
  send: (name: string, fields: PacketValues) => void;
}

const ACTION_REQUEST = 1;
const SYDNEY_TZ = 'Australia/Sydney';

function L(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

function locale(): string {
  return currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU';
}

function formatClock(atMs: number, timeZone?: string): string {
  return new Intl.DateTimeFormat(locale(), {
    weekday: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    ...(timeZone ? { timeZone } : {}),
  }).format(atMs);
}

function formatCountdown(nowMs: number, targetMs: number): string {
  const sec = Math.max(0, Math.round((targetMs - nowMs) / 1000));
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  if (h > 0) return L(`${h}h ${m}m`, `${h}g ${m}p`);
  return `${m}:${String(sec % 60).padStart(2, '0')}`;
}

function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  text?: string,
  className?: string,
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

export class TimetableWindow {
  private readonly panel: HnmoModalPanel;
  private state: TimetableStatePayload | null = null;
  private filter: 'all' | 'event' | 'invasion' | 'boss' = 'all';
  private skew = 0;
  private timer: number | null = null;

  constructor(private readonly options: TimetableWindowOptions) {
    this.panel = new HnmoModalPanel('hnmoTimetable', '', () => this.render());
    this.panel.onClose(() => {
      if (this.timer !== null) {
        window.clearInterval(this.timer);
        this.timer = null;
      }
    });
  }

  get isOpen(): boolean {
    return this.panel.isOpen;
  }

  start(): void {
    this.request();
  }

  request(): void {
    this.options.send('HnmoTimetableCommand', { Action: ACTION_REQUEST, Argument: '' });
  }

  open(): void {
    this.panel.open();
    this.request();
    this.render();
    if (this.timer === null) {
      this.timer = window.setInterval(() => {
        if (this.panel.isOpen) this.render();
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
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x33) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoTimetableState', bytes);
    if (!Json) return true;
    try {
      const parsed = JSON.parse(Json) as TimetableStatePayload;
      if (typeof parsed !== 'object' || parsed === null) return true;
      this.state = parsed;
      if (typeof parsed.now === 'number') {
        this.skew = parsed.now - Date.now();
      }
      if (this.panel.isOpen) this.render();
    } catch {
      // Ignore malformed JSON.
    }
    return true;
  }

  render(): void {
    this.panel.setTitle(L('Event & Boss Timetable (H)', 'Lịch sự kiện & Boss (H)'));
    const body = this.panel.body;
    body.replaceChildren();

    const s = this.state;
    if (!s) {
      body.append(el('p', L('Loading timetable from server...', 'Đang tải bảng lịch từ máy chủ...'), 'hnmo-muted'));
      return;
    }

    if (s.off) {
      body.append(
        el(
          'p',
          L(
            'The Event & Boss Timetable feature is currently turned off by the server administrator.',
            'Tính năng Bảng lịch sự kiện & Boss hiện đang tắt trên máy chủ.',
          ),
          'hnmo-muted',
        ),
      );
      return;
    }

    const nowMs = Date.now() + this.skew;
    const localTz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    body.append(
      el(
        'div',
        L(
          `Times show Sydney time (${SYDNEY_TZ}) and your local time (${localTz}).`,
          `Hiển thị giờ Sydney (${SYDNEY_TZ}) và giờ thiết bị của bạn (${localTz}).`,
        ),
        'hnmo-muted',
      ),
    );

    const tabs = el('div');
    tabs.style.display = 'flex';
    tabs.style.flexWrap = 'wrap';
    tabs.style.gap = '6px';
    const categories: { key: 'all' | 'event' | 'invasion' | 'boss'; label: string }[] = [
      { key: 'all', label: L('All', 'Tất cả') },
      { key: 'event', label: L('Events', 'Sự kiện') },
      { key: 'invasion', label: L('Invasions', 'Xâm lăng') },
      { key: 'boss', label: L('Bosses', 'Boss') },
    ];
    for (const cat of categories) {
      const btn = el('button', cat.label, cat.key === this.filter ? 'hnmo-btn hnmo-btn-primary' : 'hnmo-btn');
      btn.type = 'button';
      btn.addEventListener('click', () => {
        this.filter = cat.key;
        this.render();
      });
      tabs.append(btn);
    }
    const refreshBtn = el('button', L('Refresh', 'Làm mới'), 'hnmo-btn');
    refreshBtn.type = 'button';
    refreshBtn.style.marginLeft = 'auto';
    refreshBtn.addEventListener('click', () => this.request());
    tabs.append(refreshBtn);
    body.append(tabs);

    const filtered = (s.entries ?? []).filter((e) => this.filter === 'all' || e.category === this.filter);
    if (!filtered.length) {
      body.append(el('p', L('No scheduled events in this category.', 'Không có sự kiện nào trong mục này.'), 'hnmo-muted'));
    }

    for (const entry of filtered) {
      const card = el('div', undefined, 'hnmo-card');
      const header = el('div');
      header.style.display = 'flex';
      header.style.justifyContent = 'space-between';
      header.style.gap = '8px';

      const title = el('strong', L(entry.nameEn, entry.nameVi));
      const badge = entry.active
        ? el(
            'span',
            entry.endsAt
              ? L(`ACTIVE · ${formatCountdown(nowMs, entry.endsAt)} left`, `ĐANG CHẠY · còn ${formatCountdown(nowMs, entry.endsAt)}`)
              : L('ACTIVE NOW', 'ĐANG DIỄN RA'),
            'hnmo-ok',
          )
        : entry.nextStart
          ? el('span', `${L('In', 'Còn')} ${formatCountdown(nowMs, entry.nextStart)}`, 'hnmo-warn')
          : el('span', '—', 'hnmo-muted');

      header.append(title, badge);
      card.append(header);

      if (entry.nextStart) {
        const sydneyStr = formatClock(entry.nextStart, SYDNEY_TZ);
        const localStr = formatClock(entry.nextStart);
        card.append(
          el(
            'div',
            `${L('Next', 'Lần tới')}: ${sydneyStr} (${L('Sydney', 'Sydney')}) · ${localStr} (${L('Yours', 'Máy bạn')})`,
          ),
        );
      }

      card.append(el('div', `${L('Map', 'Bản đồ')}: ${L(entry.mapsEn, entry.mapsVi)}`, 'hnmo-muted'));
      if (entry.dropsEn || entry.dropsVi) {
        card.append(el('div', `${L('Drops', 'Rơi đồ')}: ${L(entry.dropsEn, entry.dropsVi)}`));
      }
      if (entry.schedule) {
        card.append(el('div', `${L('Daily schedule (Sydney)', 'Lịch hàng ngày (Sydney)')}: ${entry.schedule}`, 'hnmo-muted'));
      }

      body.append(card);
    }

    if (s.findGmWinners && s.findGmWinners.length > 0) {
      const winnersCard = el('div', undefined, 'hnmo-card');
      winnersCard.append(el('strong', L('Recent Find the GM winners', 'Người thắng Truy tìm GM gần đây')));
      for (const w of s.findGmWinners.slice(0, 5)) {
        winnersCard.append(
          el(
            'div',
            `${w.name} — ${w.map} (${formatClock(w.at, SYDNEY_TZ)})`,
            'hnmo-muted',
          ),
        );
      }
      body.append(winnersCard);
    }
  }
}
