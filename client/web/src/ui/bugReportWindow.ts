// The HNMO bug report window (an HNMO extra; Alt/Option+B, the 🐞 button on PCs and Macs, 🐞 in the phone menu): a player describes a bug, the game adds the
// technical details (browser, device, map, recent errors; never the login ticket), and the server keeps it for the
// server's owner (F5 54 → F5 55). The owner answers on the admin panel; the answers and the status show here, and in the
// chat when the player enters the game. No screenshots (owner's choice, 2026-11-20).

import { read, type PacketValues } from '../net/packets.js';
import { currentLanguage } from '../i18n.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

/** A report of the player, as the server lists it. */
export interface BugReportRow {
  id: string;
  category: string;
  text: string;
  status: string;
  duplicateOf?: string | null;
  reply?: string | null;
  sent: string;
  reward: number;
}

/** The window's state (F5 55). */
export interface BugReportState {
  off?: boolean;
  message?: string | null;
  sent?: boolean;
  state?: {
    left: number;
    maxLength: number;
    categories: string[];
    reward?: string | null;
    mine: BugReportRow[];
  } | null;
}

type Send = (name: string, fields: PacketValues) => void;

const MAX_ERRORS = 10;
const recentErrors: string[] = [];
let errorLogInstalled = false;

function tx(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

function remember(text: string): void {
  const line = text.replace(/\s+/g, ' ').slice(0, 200);
  recentErrors.push(`${new Date().toISOString().slice(11, 19)} ${line}`);
  if (recentErrors.length > MAX_ERRORS) recentErrors.shift();
}

/** Keeps the page's last errors for bug reports (called once at start). */
export function installErrorLog(): void {
  if (errorLogInstalled) return;
  errorLogInstalled = true;
  window.addEventListener('error', (event) => { remember(`${event.message} @ ${event.filename.split('/').pop() ?? ''}:${event.lineno}`); });
  window.addEventListener('unhandledrejection', (event) => { remember(`unhandled: ${String(event.reason)}`); });
  const original = console.error.bind(console);
  console.error = (...args: unknown[]) => {
    remember(args.map((a) => (a instanceof Error ? a.message : String(a))).join(' '));
    original(...args);
  };
}

function webGlRenderer(): string {
  try {
    const gl = document.createElement('canvas').getContext('webgl2');
    if (!gl) return 'no WebGL2';
    const info = gl.getExtension('WEBGL_debug_renderer_info');
    return String(info ? gl.getParameter(info.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER));
  } catch {
    return 'unknown';
  }
}

/** The technical details a report carries. Nothing from the address's hash (it may hold a login ticket). */
export function technicalDetails(extra: Record<string, string | number | null | undefined> = {}): string {
  const lines = [
    `page ${location.pathname}`,
    `browser ${navigator.userAgent}`,
    `screen ${screen.width}x${screen.height} window ${innerWidth}x${innerHeight} dpr ${devicePixelRatio}`,
    `touch ${navigator.maxTouchPoints > 0 ? 'yes' : 'no'} language ${currentLanguage()}`,
    `gpu ${webGlRenderer()}`,
    ...Object.entries(extra).filter(([, v]) => v !== null && v !== undefined && v !== '').map(([k, v]) => `${k} ${String(v)}`),
  ];
  if (recentErrors.length) lines.push('recent errors:', ...recentErrors);
  return lines.join('\n');
}

/** JSON with only ASCII characters (the server reads \u escapes). */
function asciiJson(value: unknown): string {
  return JSON.stringify(value).replace(/[\u007f-￿]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
}

function categoryName(category: string): string {
  switch (category) {
    case 'display': return tx('Display / graphics', 'Hiển thị / đồ họa');
    case 'skill': return tx('Skill', 'Kỹ năng');
    case 'item': return tx('Item', 'Vật phẩm');
    case 'quest': return tx('Quest / event', 'Nhiệm vụ / sự kiện');
    case 'connection': return tx('Connection / lag', 'Kết nối / giật lag');
    default: return tx('Other', 'Khác');
  }
}

function statusName(row: BugReportRow): string {
  switch (row.status) {
    case 'Confirmed': return tx('confirmed', 'đã xác nhận');
    case 'Fixed': return tx('fixed', 'đã sửa');
    case 'NotABug': return tx('not a bug', 'không phải lỗi');
    case 'Duplicate': return tx(`same as ${row.duplicateOf ?? ''}`, `trùng với ${row.duplicateOf ?? ''}`);
    default: return tx('waiting', 'đang chờ xem');
  }
}

/** Whether a key press is the window's shortcut, Alt+B (Option+B on a Mac, whose key gives "∫", so by the key's place). */
export function isBugReportShortcut(event: Pick<KeyboardEvent, 'code' | 'altKey' | 'ctrlKey' | 'metaKey' | 'shiftKey'>): boolean {
  return event.code === 'KeyB' && event.altKey && !event.ctrlKey && !event.metaKey && !event.shiftKey;
}

/**
 * The 🐞 button for PCs and Macs, in the game's top bar next to the guide's "i" (index.html; phones have it in their
 * menus).
 */
function installDesktopButton(toggle: () => void): void {
  const button = document.getElementById('hnmoBugButton');
  if (!button || button.dataset.bound) return;
  button.dataset.bound = '1';
  // The world below shouldn't take the click as a walk.
  button.addEventListener('pointerdown', (event) => { event.stopPropagation(); });
  button.addEventListener('click', (event) => { event.stopPropagation(); button.blur(); toggle(); });
}

export class BugReportWindow {
  private readonly panel: HnmoPanel;
  private state: BugReportState | null = null;
  private draft = '';
  private category = 'other';
  private sending = false;

  constructor(private readonly options: {
    send: Send;
    message: (text: string) => void;
    /** Details of the game the server doesn't know (e.g. the frame rate). */
    details: () => Record<string, string | number | null | undefined>;
  }) {
    this.panel = createHnmoPanel('hnmo-bug-panel', tx('Report a bug', 'Báo lỗi'));
    installDesktopButton(() => this.toggle());
  }

  isOpen(): boolean {
    return this.panel.isOpen();
  }

  open(): void {
    this.panel.setTitle(tx('Report a bug', 'Báo lỗi'));
    this.panel.open();
    this.render();
    this.options.send('HnmoBugReportCommand', { Action: 0, Json: '' });
  }

  close(): void {
    this.panel.close();
  }

  toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  /** F5 55. Returns whether the packet was this one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x55) return false;
    const parsed = read('ServerToClient', 'HnmoBugReportState', bytes);
    try {
      this.state = JSON.parse(typeof parsed.Json === 'string' ? parsed.Json : '') as BugReportState;
    } catch {
      return true;
    }
    this.sending = false;
    if (this.state.message) this.options.message(this.state.message);
    if (this.state.sent) this.draft = '';
    if (this.panel.isOpen()) this.render();
    return true;
  }

  private render(): void {
    const body = this.panel.body;
    body.replaceChildren();
    const intro = document.createElement('p');
    intro.className = 'hnmo-muted';
    intro.textContent = tx(
      'Tell the server\'s owner what went wrong: what you did, what you expected and what happened. The game adds your map, browser and device; no password or login.',
      'Báo cho chủ server điều bị lỗi: bạn đã làm gì, mong đợi gì và chuyện gì xảy ra. Game tự thêm bản đồ, trình duyệt và thiết bị; không có mật khẩu hay thông tin đăng nhập.',
    );
    body.append(intro);
    const state = this.state;
    if (!state) {
      const wait = document.createElement('p');
      wait.className = 'hnmo-muted';
      wait.textContent = tx('Loading…', 'Đang tải…');
      body.append(wait);
      return;
    }
    if (state.off || !state.state) {
      const off = document.createElement('p');
      off.id = 'bug-off';
      off.textContent = state.message ?? tx('Bug reports are off on this server.', 'Báo lỗi đang tắt trên server này.');
      body.append(off);
      return;
    }
    const s = state.state;
    const form = document.createElement('form');
    form.id = 'bug-form';
    const select = document.createElement('select');
    select.id = 'bug-category';
    select.setAttribute('aria-label', tx('Kind of bug', 'Loại lỗi'));
    for (const category of s.categories) {
      const option = document.createElement('option');
      option.value = category;
      option.textContent = categoryName(category);
      option.selected = category === this.category;
      select.append(option);
    }
    select.addEventListener('change', () => { this.category = select.value; });
    const text = document.createElement('textarea');
    text.id = 'bug-text';
    text.rows = 5;
    text.maxLength = s.maxLength;
    text.value = this.draft;
    text.placeholder = tx('What happened? (at least 10 characters)', 'Chuyện gì đã xảy ra? (ít nhất 10 ký tự)');
    text.style.width = '100%';
    text.style.boxSizing = 'border-box';
    const counter = document.createElement('span');
    counter.className = 'hnmo-muted';
    const count = () => { counter.textContent = `${text.value.length} / ${s.maxLength}`; };
    count();
    text.addEventListener('input', () => { this.draft = text.value; count(); });
    const send = document.createElement('button');
    send.type = 'submit';
    send.className = 'hnmo-btn';
    send.id = 'bug-send';
    send.textContent = tx('Send', 'Gửi');
    send.disabled = this.sending || s.left <= 0;
    const left = document.createElement('p');
    left.className = 'hnmo-muted';
    left.id = 'bug-left';
    left.textContent = s.left > 0
      ? tx(`You can send ${s.left} more today (Sydney time).`, `Hôm nay bạn còn gửi được ${s.left} báo lỗi (giờ Sydney).`)
      : tx('You sent today\'s reports; more tomorrow (Sydney time).', 'Bạn đã gửi hết lượt hôm nay; ngày mai (giờ Sydney) gửi tiếp.');
    if (s.reward) left.textContent += ' ' + tx(`A confirmed bug earns ${s.reward}.`, `Lỗi được xác nhận được thưởng ${s.reward}.`);
    form.addEventListener('submit', (event) => {
      event.preventDefault();
      const value = text.value.trim();
      if (this.sending) return;
      if (value.length < 10) {
        this.options.message(tx('Please describe the bug in at least 10 characters.', 'Hãy mô tả lỗi ít nhất 10 ký tự.'));
        return;
      }
      this.sending = true;
      send.disabled = true;
      const payload = asciiJson({ category: select.value, text: value, info: technicalDetails(this.options.details()) });
      this.options.send('HnmoBugReportCommand', { Action: 1, Json: payload });
    });
    const row = document.createElement('div');
    row.style.display = 'flex';
    row.style.gap = '8px';
    row.style.alignItems = 'center';
    row.style.marginTop = '6px';
    row.append(select, counter, send);
    form.append(text, row);
    body.append(form, left);

    const head = document.createElement('h3');
    head.className = 'hnmo-arena-head';
    head.textContent = tx('Your reports', 'Báo lỗi của bạn');
    body.append(head);
    if (!s.mine.length) {
      const none = document.createElement('p');
      none.className = 'hnmo-muted';
      none.textContent = tx('None yet.', 'Chưa có.');
      body.append(none);
      return;
    }
    const list = document.createElement('ul');
    list.id = 'bug-mine';
    list.className = 'hnmo-arena-shop';
    for (const r of s.mine) {
      const li = document.createElement('li');
      li.dataset.id = r.id;
      li.textContent = `${r.id} · ${r.sent} · ${categoryName(r.category)} · ${statusName(r)}: ${r.text}`;
      if (r.reward > 0) li.textContent += tx(` (reward ${r.reward})`, ` (thưởng ${r.reward})`);
      if (r.reply) {
        const answer = document.createElement('div');
        answer.className = 'hnmo-muted';
        answer.textContent = tx(`Answer: ${r.reply}`, `Trả lời: ${r.reply}`);
        li.append(answer);
      }
      list.append(li);
    }
    body.append(list);
  }
}
