// HNMO in-game Player Rankings panel (F5 46 / F5 47):
// shows Top Characters (Resets/Master/Level), Online Leaders, Top Guilds, and Mini-Game Events.

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface RankingCharacterEntry {
  rank: number;
  name: string;
  className: string;
  level: number;
  masterLevel: number;
  resets: number;
  online: boolean;
}

export interface RankingOnlineEntry {
  rank: number;
  name: string;
  className: string;
  level: number;
  masterLevel: number;
  resets: number;
  mapName: string;
}

export interface RankingGuildEntry {
  rank: number;
  name: string;
  score: number;
  memberCount: number;
  alliance: string;
}

export interface RankingEventPlayer {
  rank: number;
  name: string;
  className: string;
  totalScore: number;
  gameCount: number;
}

export interface RankingEventBoard {
  eventType: string;
  entries: RankingEventPlayer[];
}

export interface RankingStatePayload {
  enableRankings: boolean;
  enableBulkAddStats: boolean;
  showOnline: boolean;
  showGuilds: boolean;
  showEvents: boolean;
  topCount: number;
  updatedAtUtc: string;
  myRank?: {
    name: string;
    rank: number;
    level: number;
    masterLevel: number;
    resets: number;
    levelUpPoints: number;
  };
  characters: RankingCharacterEntry[];
  online: RankingOnlineEntry[];
  guilds: RankingGuildEntry[];
  events: RankingEventBoard[];
}

type RankingTab = 'characters' | 'online' | 'guilds' | 'events';

const SUBCODE_STATE = 0x47;

function tx(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

function formatUpdatedSydney(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return new Intl.DateTimeFormat(currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU', {
    timeZone: 'Australia/Sydney',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(date);
}

/** In-game Player Rankings panel (Shift+R or /rank). */
export class RankingWindow {
  private readonly panel: HnmoPanel;
  private state: RankingStatePayload | null = null;
  private activeTab: RankingTab = 'characters';
  private loading = false;

  constructor(
    private readonly send: (name: string, fields: PacketValues) => void,
    private readonly openAddStats?: () => void,
  ) {
    this.panel = createHnmoPanel('hnmo-ranking-panel', tx('Player Rankings (Shift+R)', 'Bảng Xếp Hạng (Shift+R)'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Player Rankings (Shift+R)', 'Bảng Xếp Hạng (Shift+R)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  /** Handles incoming S2C packets and extracts HnmoRankingState (F5 47). */
  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc2 || bytes[2] !== 0xf5 || bytes[3] !== SUBCODE_STATE) return;
    try {
      const pkt = read<{ Json: string }>('ServerToClient', 'HnmoRankingState', bytes);
      this.state = JSON.parse(pkt.Json) as RankingStatePayload;
      this.loading = false;
      if (this.panel.isOpen()) this.render();
    } catch {
      // Ignore malformed snapshot frames.
    }
  }

  /** Requests the latest rankings from the server. */
  requestRefresh(): void {
    this.loading = true;
    this.send('HnmoRankingCommand', { Action: 0, Payload: this.activeTab });
    if (this.panel.isOpen()) this.render();
  }

  /** Opens the rankings panel and requests fresh data. */
  open(): void {
    this.panel.open();
    this.requestRefresh();
  }

  /** Closes the rankings panel. */
  close(): void {
    this.panel.close();
  }

  /** Toggles the rankings panel open or closed. */
  toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  /** Returns whether the rankings panel is currently open. */
  isOpen(): boolean {
    return this.panel.isOpen();
  }

  private render(): void {
    const body = this.panel.body;
    body.innerHTML = '';

    const s = this.state;
    const my = s?.myRank;
    const summary = document.createElement('div');
    summary.className = 'hnmo-card';

    const leftSummary = document.createElement('div');
    if (my) {
      const rankBadge = my.rank > 0 ? `#${my.rank}` : tx('Outside Top', 'Ngoài Top');
      leftSummary.innerHTML = `<strong>${escapeHtml(my.name)}</strong> · <span class="hnmo-badge">${escapeHtml(rankBadge)}</span> · RS ${my.resets} · Lv ${my.level} (+ML ${my.masterLevel})`;
    } else {
      leftSummary.textContent = this.loading
        ? tx('Loading server rankings…', 'Đang tải bảng xếp hạng…')
        : tx('Server leaderboards', 'Bảng xếp hạng máy chủ');
    }
    summary.appendChild(leftSummary);

    const actions = document.createElement('div');
    actions.className = 'hnmo-row';
    if (this.openAddStats && (s?.enableBulkAddStats ?? true)) {
      const statsBtn = document.createElement('button');
      statsBtn.type = 'button';
      statsBtn.className = 'hnmo-btn';
      statsBtn.textContent = tx('Add Stats (Shift+A)', 'Cộng điểm (Shift+A)');
      statsBtn.title = tx('Open bulk stat-point allocator', 'Mở cửa sổ cộng điểm tiềm năng nhanh');
      statsBtn.addEventListener('click', () => {
        this.close();
        this.openAddStats?.();
      });
      actions.appendChild(statsBtn);
    }

    const refreshBtn = document.createElement('button');
    refreshBtn.type = 'button';
    refreshBtn.className = 'hnmo-btn';
    refreshBtn.textContent = tx('Refresh', 'Làm mới');
    refreshBtn.title = tx('Reload leaderboard data', 'Tải lại bảng xếp hạng');
    refreshBtn.addEventListener('click', () => this.requestRefresh());
    actions.appendChild(refreshBtn);
    summary.appendChild(actions);
    body.appendChild(summary);

    if (s && !s.enableRankings) {
      const disabled = document.createElement('p');
      disabled.className = 'hnmo-muted';
      disabled.textContent = tx('In-game rankings are disabled by the administrator.', 'Bảng xếp hạng trong game đang tắt bởi quản trị viên.');
      body.appendChild(disabled);
      return;
    }

    const tabsBar = document.createElement('div');
    tabsBar.className = 'hnmo-tabs';
    const availableTabs: { id: RankingTab; label: string; enabled: boolean }[] = [
      { id: 'characters', label: tx('Top Characters', 'Top Nhân Vật'), enabled: true },
      { id: 'online', label: tx('Online', 'Đang Online'), enabled: s?.showOnline ?? true },
      { id: 'guilds', label: tx('Top Guilds', 'Top Hội (Guild)'), enabled: s?.showGuilds ?? true },
      { id: 'events', label: tx('Events', 'Sự Kiện (BC/DS/CC)'), enabled: s?.showEvents ?? true },
    ];
    if (!availableTabs.some((t) => t.id === this.activeTab && t.enabled)) {
      this.activeTab = 'characters';
    }
    for (const tab of availableTabs) {
      if (!tab.enabled) continue;
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = `hnmo-tab${this.activeTab === tab.id ? ' active' : ''}`;
      btn.textContent = tab.label;
      btn.addEventListener('click', () => {
        this.activeTab = tab.id;
        this.render();
      });
      tabsBar.appendChild(btn);
    }
    body.appendChild(tabsBar);

    if (!s) {
      const waiting = document.createElement('p');
      waiting.className = 'hnmo-muted';
      waiting.textContent = tx('Waiting for server rankings…', 'Đang chờ dữ liệu xếp hạng từ máy chủ…');
      body.appendChild(waiting);
      return;
    }

    if (this.activeTab === 'characters') this.renderCharacters(body, s.characters);
    else if (this.activeTab === 'online') this.renderOnline(body, s.online);
    else if (this.activeTab === 'guilds') this.renderGuilds(body, s.guilds);
    else this.renderEvents(body, s.events);

    const footer = document.createElement('div');
    footer.className = 'hnmo-muted';
    footer.textContent = tx(
      `Updated (Sydney): ${formatUpdatedSydney(s.updatedAtUtc)}`,
      `Cập nhật (Sydney): ${formatUpdatedSydney(s.updatedAtUtc)}`,
    );
    body.appendChild(footer);
  }

  private renderCharacters(parent: HTMLElement, rows: RankingCharacterEntry[]): void {
    if (!rows.length) {
      parent.appendChild(this.emptyNote(tx('No ranked characters yet.', 'Chưa có nhân vật nào trong bảng xếp hạng.')));
      return;
    }
    const table = this.createTable([
      '#',
      tx('Character', 'Nhân vật'),
      tx('Class', 'Chủng tộc'),
      tx('Resets', 'Reset'),
      tx('Level', 'Cấp'),
      tx('Master', 'Master'),
      tx('Status', 'Trạng thái'),
    ]);
    const tbody = table.tBodies[0];
    for (const r of rows) {
      const tr = tbody.insertRow();
      tr.insertCell().textContent = String(r.rank);
      const nameCell = tr.insertCell();
      nameCell.style.fontWeight = '600';
      nameCell.textContent = r.name;
      tr.insertCell().textContent = r.className || '-';
      tr.insertCell().textContent = String(r.resets);
      tr.insertCell().textContent = String(r.level);
      tr.insertCell().textContent = String(r.masterLevel);
      const statusCell = tr.insertCell();
      statusCell.textContent = r.online ? tx('Online', 'Online') : '-';
      if (r.online) statusCell.style.color = '#7cc98a';
    }
    parent.appendChild(table);
  }

  private renderOnline(parent: HTMLElement, rows: RankingOnlineEntry[]): void {
    if (!rows.length) {
      parent.appendChild(this.emptyNote(tx('No players online.', 'Hiện chưa có người chơi nào trực tuyến.')));
      return;
    }
    const table = this.createTable([
      '#',
      tx('Character', 'Nhân vật'),
      tx('Class', 'Chủng tộc'),
      tx('Resets', 'Reset'),
      tx('Level', 'Cấp'),
      tx('Map', 'Bản đồ'),
    ]);
    const tbody = table.tBodies[0];
    for (const r of rows) {
      const tr = tbody.insertRow();
      tr.insertCell().textContent = String(r.rank);
      const nameCell = tr.insertCell();
      nameCell.style.fontWeight = '600';
      nameCell.textContent = r.name;
      tr.insertCell().textContent = r.className || '-';
      tr.insertCell().textContent = String(r.resets);
      tr.insertCell().textContent = `${r.level}${r.masterLevel > 0 ? ` (+${r.masterLevel})` : ''}`;
      tr.insertCell().textContent = r.mapName || '-';
    }
    parent.appendChild(table);
  }

  private renderGuilds(parent: HTMLElement, rows: RankingGuildEntry[]): void {
    if (!rows.length) {
      parent.appendChild(this.emptyNote(tx('No guilds ranked yet.', 'Chưa có Hội (Guild) nào.')));
      return;
    }
    const table = this.createTable([
      '#',
      tx('Guild', 'Tên Hội'),
      tx('Score', 'Điểm'),
      tx('Members', 'Thành viên'),
      tx('Hostility', 'Đối địch'),
    ]);
    const tbody = table.tBodies[0];
    for (const g of rows) {
      const tr = tbody.insertRow();
      tr.insertCell().textContent = String(g.rank);
      const nameCell = tr.insertCell();
      nameCell.style.fontWeight = '600';
      nameCell.textContent = g.name;
      tr.insertCell().textContent = String(g.score);
      tr.insertCell().textContent = String(g.memberCount);
      tr.insertCell().textContent = g.alliance || '-';
    }
    parent.appendChild(table);
  }

  private renderEvents(parent: HTMLElement, boards: RankingEventBoard[]): void {
    if (!boards.length) {
      parent.appendChild(this.emptyNote(tx('No mini-game rankings recorded yet.', 'Chưa có dữ liệu xếp hạng sự kiện.')));
      return;
    }
    for (const board of boards) {
      const header = document.createElement('div');
      header.className = 'hnmo-badge';
      header.textContent = board.eventType;
      parent.appendChild(header);

      if (!board.entries.length) {
        parent.appendChild(this.emptyNote(tx('No entries yet.', 'Chưa có lượt chơi.')));
        continue;
      }
      const table = this.createTable([
        '#',
        tx('Character', 'Nhân vật'),
        tx('Class', 'Chủng tộc'),
        tx('Total Score', 'Tổng điểm'),
        tx('Runs', 'Số lượt'),
      ]);
      const tbody = table.tBodies[0];
      for (const e of board.entries) {
        const tr = tbody.insertRow();
        tr.insertCell().textContent = String(e.rank);
        tr.insertCell().textContent = e.name;
        tr.insertCell().textContent = e.className || '-';
        tr.insertCell().textContent = e.totalScore.toLocaleString();
        tr.insertCell().textContent = String(e.gameCount);
      }
      parent.appendChild(table);
    }
  }

  private createTable(headers: string[]): HTMLTableElement {
    const table = document.createElement('table');
    table.style.cssText = 'width:100%;border-collapse:collapse;font-size:12.5px;';
    const thead = table.createTHead();
    const headRow = thead.insertRow();
    for (const label of headers) {
      const th = document.createElement('th');
      th.textContent = label;
      th.style.cssText = 'text-align:left;padding:5px 6px;border-bottom:1px solid #5e4b28;color:#f2cf78;font-weight:600;';
      headRow.appendChild(th);
    }
    const tbody = table.createTBody();
    tbody.style.borderBottom = '1px solid #352b18';
    return table;
  }

  private emptyNote(text: string): HTMLElement {
    const p = document.createElement('p');
    p.className = 'hnmo-muted';
    p.textContent = text;
    return p;
  }
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
