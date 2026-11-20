// The HNMO Arena window (an HNMO extra; Alt+A, 🏟 in the phone menu): battles across servers and seasons. From a home
// server it takes the player to the Arena, from the Arena back home. Travel is a one-time login ticket (F5 52 → F5 53):
// the page reloads with it and logs in on the other side, where the Arena's own characters play; nothing of the home
// character goes along. Which home server to come back to is kept in this browser tab (sessionStorage).

import { read, type PacketValues } from '../net/packets.js';
import { currentLanguage } from '../i18n.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

/** What the server answers (F5 53). */
export interface ArenaTravelState {
  inArena: boolean;
  account: string;
  ticket: string;
  message?: string | null;
  /** In the Arena: the ranked 1v1 record, the queue and the leaderboards. */
  ranked?: ArenaRanked | null;
  /** At home: the account's Arena points and the points shop. */
  shop?: ArenaShop | null;
}

/** An item of the Arena points shop. */
export interface ArenaShopEntry {
  index: number;
  Category: string;
  NameEn: string;
  NameVi: string;
  Price: number;
}

/** The account's Arena points and the shop's items. */
export interface ArenaShop {
  points: number;
  items: ArenaShopEntry[];
}

/** A row of the Arena's leaderboards. */
export interface ArenaRankRow {
  name: string;
  className: string;
  rating: number;
  wins: number;
  losses: number;
  weekWins: number;
  weekLosses: number;
}

/** The player's ranked 1v1 record and the Arena's leaderboards. */
export interface ArenaRanked {
  rating: number;
  wins: number;
  losses: number;
  weekWins: number;
  weekLosses: number;
  queued: boolean;
  inDuel: boolean;
  waiting: number;
  top: ArenaRankRow[];
  week: ArenaRankRow[];
}

/** The home server to come back to: its start screen column and server id. */
export interface ArenaHome {
  column: string;
  serverId: number;
}

const HOME_KEY = 'hnmo-arena-home';

type Send = (name: string, fields: PacketValues) => void;

function tx(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

/** The home server saved before going to the Arena, if any. */
export function savedArenaHome(): ArenaHome | null {
  try {
    const raw = sessionStorage.getItem(HOME_KEY);
    const value: unknown = raw ? JSON.parse(raw) : null;
    if (typeof value === 'object' && value !== null && typeof (value as ArenaHome).column === 'string' && typeof (value as ArenaHome).serverId === 'number') {
      return value as ArenaHome;
    }
  } catch {
    // no storage (private window): the server list is shown instead
  }
  return null;
}

function categoryName(category: string): string {
  switch (category.toLowerCase()) {
    case 'jewels': return tx('Jewels', 'Ngọc');
    case 'charms': return tx('Charms', 'Bùa');
    case 'tickets': return tx('Event tickets', 'Vé sự kiện');
    case 'cosmetic': case 'cosmetics': return tx('Cosmetics', 'Trang trí');
    default: return category;
  }
}

export class ArenaWindow {
  private readonly panel: HnmoPanel;
  private state: ArenaTravelState | null = null;
  private travelling = false;
  private refreshTimer: number | undefined;

  constructor(private readonly options: {
    send: Send;
    message: (text: string) => void;
    /** Whether the Arena runs (the web server's seasons.json). */
    arenaRunning: () => Promise<boolean>;
    /** The server being played, to come back to. */
    home: () => ArenaHome | null;
    /** Reloads into the other side with the ticket. */
    travel: (account: string, ticket: string, to: 'arena' | 'home') => void;
  }) {
    this.panel = createHnmoPanel('hnmo-arena-panel', tx('Arena', 'Đấu trường'));
  }

  isOpen(): boolean {
    return this.panel.isOpen();
  }

  open(): void {
    this.panel.setTitle(tx('Arena', 'Đấu trường'));
    this.panel.open();
    this.render();
    this.options.send('HnmoArenaTravelRequest', { Action: 0, Index: 0 });
    // While open, the queue and the leaderboards stay current.
    window.clearInterval(this.refreshTimer);
    this.refreshTimer = window.setInterval(() => {
      if (!this.panel.isOpen()) { window.clearInterval(this.refreshTimer); return; }
      if (this.state?.inArena) this.options.send('HnmoArenaTravelRequest', { Action: 0, Index: 0 });
    }, 5000);
  }

  close(): void {
    window.clearInterval(this.refreshTimer);
    this.panel.close();
  }

  toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  /** F5 53. Returns whether the packet was this one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x53) return false;
    const parsed = read('ServerToClient', 'HnmoArenaTravelState', bytes);
    try {
      this.state = JSON.parse(typeof parsed.Json === 'string' ? parsed.Json : '') as ArenaTravelState;
    } catch {
      return true;
    }
    const state = this.state;
    if (state.message) this.options.message(state.message);
    if (state.ticket && this.travelling) {
      if (!state.inArena) {
        const home = this.options.home();
        try {
          if (home) sessionStorage.setItem(HOME_KEY, JSON.stringify(home));
        } catch {
          // no storage: coming back shows the server list
        }
      }
      this.options.travel(state.account, state.ticket, state.inArena ? 'home' : 'arena');
      return true;
    }
    this.travelling = false;
    if (this.panel.isOpen()) this.render();
    return true;
  }

  private render(): void {
    const body = this.panel.body;
    body.replaceChildren();
    const intro = document.createElement('p');
    intro.className = 'hnmo-muted';
    intro.textContent = tx(
      'Battles across servers and seasons. Everyone plays an arena character of their class with the same level, stats and gear; your own character and items stay at home.',
      'Đấu giữa các server và season. Mọi người dùng nhân vật đấu trường cùng class với cùng cấp, chỉ số và trang bị; nhân vật và đồ của bạn ở lại máy chủ nhà.',
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
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'hnmo-btn';
    button.id = state.inArena ? 'arena-leave' : 'arena-go';
    button.textContent = state.inArena ? tx('Leave the Arena', 'Rời Đấu trường') : tx('Go to the Arena', 'Tới Đấu trường');
    button.disabled = this.travelling;
    button.addEventListener('click', () => { void this.travel(); });
    if (!state.inArena) {
      const note = document.createElement('p');
      note.className = 'hnmo-muted';
      note.id = 'arena-closed';
      void this.options.arenaRunning().then((running) => {
        button.disabled = !running || this.travelling;
        note.textContent = running ? '' : tx('The Arena isn\'t open right now.', 'Đấu trường hiện chưa mở.');
      });
      body.append(button, note);
      if (state.shop) this.renderShop(body, state.shop);
    } else {
      body.append(button);
      if (state.ranked) this.renderRanked(body, state.ranked);
    }
  }

  private renderRanked(body: HTMLElement, ranked: ArenaRanked): void {
    const head = document.createElement('h3');
    head.className = 'hnmo-arena-head';
    head.textContent = tx('Ranked 1v1 (best of 3)', 'Đấu xếp hạng 1v1 (thắng 2 trong 3)');
    const me = document.createElement('p');
    me.id = 'arena-rating';
    me.textContent = tx(
      `Rating ${ranked.rating} · ${ranked.wins} wins, ${ranked.losses} losses · this week ${ranked.weekWins} / ${ranked.weekLosses}`,
      `Điểm ${ranked.rating} · thắng ${ranked.wins}, thua ${ranked.losses} · tuần này ${ranked.weekWins} / ${ranked.weekLosses}`,
    );
    const queue = document.createElement('button');
    queue.type = 'button';
    queue.className = 'hnmo-btn';
    queue.id = ranked.queued ? 'arena-leave-queue' : 'arena-join-queue';
    queue.disabled = ranked.inDuel;
    queue.textContent = ranked.inDuel
      ? tx('In a duel…', 'Đang đấu…')
      : ranked.queued
        ? tx(`Leave the queue (${ranked.waiting} waiting)`, `Rời hàng chờ (${ranked.waiting} người chờ)`)
        : tx('Find a 1v1 opponent', 'Tìm đối thủ 1v1');
    queue.addEventListener('click', () => {
      this.options.send('HnmoArenaTravelRequest', { Action: ranked.queued ? 3 : 2, Index: 0 });
    });
    body.append(head, me, queue);
    const list = (title: string, rows: ArenaRankRow[], week: boolean): void => {
      const caption = document.createElement('p');
      caption.className = 'hnmo-muted';
      caption.textContent = title;
      body.append(caption);
      if (!rows.length) return;
      const ol = document.createElement('ol');
      ol.className = 'hnmo-arena-top';
      for (const row of rows.slice(0, 10)) {
        const li = document.createElement('li');
        li.textContent = `${row.name} (${row.className}) · ${row.rating} · ${week ? `${row.weekWins}/${row.weekLosses}` : `${row.wins}/${row.losses}`}`;
        ol.append(li);
      }
      body.append(ol);
    };
    list(tx('This week (Sydney, from Monday)', 'Tuần này (giờ Sydney, từ thứ Hai)'), ranked.week, true);
    list(tx('All time', 'Mọi thời điểm'), ranked.top, false);
  }

  private renderShop(body: HTMLElement, shop: ArenaShop): void {
    const head = document.createElement('h3');
    head.className = 'hnmo-arena-head';
    head.textContent = tx('Arena points shop', 'Cửa hàng điểm Đấu trường');
    const points = document.createElement('p');
    points.id = 'arena-points';
    points.textContent = tx(
      `You have ${shop.points} Arena points (win 5, loss 2 in ranked duels).`,
      `Bạn có ${shop.points} điểm Đấu trường (thắng 5, thua 2 trong đấu xếp hạng).`,
    );
    body.append(head, points);
    const categories = [...new Set(shop.items.map((i) => i.Category))];
    for (const category of categories) {
      const caption = document.createElement('p');
      caption.className = 'hnmo-muted';
      caption.textContent = categoryName(category);
      const ul = document.createElement('ul');
      ul.className = 'hnmo-arena-shop';
      for (const item of shop.items.filter((i) => i.Category === category)) {
        const li = document.createElement('li');
        const name = document.createElement('span');
        name.textContent = `${tx(item.NameEn, item.NameVi || item.NameEn)} · ${item.Price} ${tx('points', 'điểm')}`;
        const buy = document.createElement('button');
        buy.type = 'button';
        buy.className = 'hnmo-btn';
        buy.dataset.index = String(item.index);
        buy.textContent = tx('Buy', 'Mua');
        buy.disabled = shop.points < item.Price;
        buy.addEventListener('click', () => {
          buy.disabled = true;
          this.options.send('HnmoArenaTravelRequest', { Action: 4, Index: item.index });
        });
        li.append(name, ' ', buy);
        ul.append(li);
      }
      body.append(caption, ul);
    }
  }

  private async travel(): Promise<void> {
    if (this.travelling || !this.state) return;
    if (!this.state.inArena && !(await this.options.arenaRunning())) return;
    this.travelling = true;
    this.render();
    this.options.send('HnmoArenaTravelRequest', { Action: 1, Index: 0 });
  }
}
