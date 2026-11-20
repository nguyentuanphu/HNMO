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
  /** In the Arena: the 5v5 team battle queue and the battle being fought. */
  team?: ArenaTeam | null;
  /** In the Arena: the battle royale's schedule, sign-up and the match being fought. */
  royale?: ArenaRoyale | null;
  /** In the Arena: the guild war's schedule, sign-up and the match being fought. */
  guildWar?: ArenaGuildWar | null;
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

/** A fighter of a team battle and their kills. */
export interface ArenaFighter {
  name: string;
  kills: number;
}

/** The 5v5 team battles: the queue, and the battle being fought (side 1 red, 2 blue, 0 not fighting). */
export interface ArenaTeam {
  queued: boolean;
  waiting: number;
  size: number;
  minSize: number;
  killsToWin: number;
  minutes: number;
  running: boolean;
  side: number;
  scoreA: number;
  scoreB: number;
  secondsLeft: number;
  ended: boolean;
  /** 1 red, -1 blue, 0 a draw (once ended). */
  winner: number;
  teamA?: ArenaFighter[] | null;
  teamB?: ArenaFighter[] | null;
}

/** The battle royale: the next start (Sydney), the sign-up, and the match (zone in tiles). */
export interface ArenaRoyale {
  times: string;
  next?: string | null;
  nextSydney?: string | null;
  secondsToStart: number;
  signupOpen: boolean;
  signedUp: boolean;
  signups: number;
  minPlayers: number;
  running: boolean;
  fighter: boolean;
  alive: boolean;
  kills: number;
  left: number;
  total: number;
  secondsLeft: number;
  zoneX: number;
  zoneY: number;
  zoneRadius: number;
  /** Where the zone closes in (known from the start). */
  finalX: number;
  finalY: number;
  ended: boolean;
  /** 1 wins; 0 while fighting or not a fighter. */
  place: number;
  winner?: string | null;
}

/** A capture point of the guild war: its tile, the side holding it (0 none) and the side taking it. */
export interface ArenaFlag {
  x: number;
  y: number;
  owner: number;
  taking: number;
  seconds: number;
}

/** The guild war: the schedule (Sydney), the sign-up, the match (side 1 red, 2 blue) and the week's champion. */
export interface ArenaGuildWar {
  days: string;
  times: string;
  nextSydney?: string | null;
  secondsToStart: number;
  signupOpen: boolean;
  signedUp: boolean;
  myGuild?: string | null;
  minMembers: number;
  guilds: { name: string; count: number }[];
  running: boolean;
  waiting: number;
  side: number;
  guildA?: string | null;
  guildB?: string | null;
  scoreA: number;
  scoreB: number;
  secondsLeft: number;
  flags?: ArenaFlag[] | null;
  captureSeconds: number;
  ended: boolean;
  /** 1 side 1, -1 side 2, 0 a draw (once ended). */
  winner: number;
  champion?: string | null;
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
  private battleTimer: number | undefined;
  private scoreboard: HTMLElement | null = null;
  /** The last answer drawn, so a refresh that changed nothing doesn't rebuild the buttons under the pointer. */
  private drawn = '';

  constructor(private readonly options: {
    send: Send;
    message: (text: string) => void;
    /** Whether the Arena runs (the web server's seasons.json). */
    arenaRunning: () => Promise<boolean>;
    /** The server being played, to come back to. */
    home: () => ArenaHome | null;
    /** The hero's tile, to tell whether it stands in the battle royale's safe zone. */
    heroCell?: () => { x: number; y: number } | null;
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
    this.drawn = '';
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
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    try {
      const answer = JSON.parse(json) as ArenaTravelState | { open: true };
      // The Arena Herald (an NPC in the towns) opens the window, which then asks for the full state.
      if ('open' in answer) {
        if (!this.panel.isOpen()) this.open();
        return true;
      }
      this.state = answer;
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
    this.updateScoreboard(state.team ?? null, state.royale ?? null, state.guildWar ?? null);
    if (this.panel.isOpen() && json !== this.drawn) {
      this.drawn = json;
      this.render();
    }
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
      if (state.guildWar) this.renderGuildWar(body, state.guildWar);
      if (state.royale) this.renderRoyale(body, state.royale);
      if (state.team) this.renderTeam(body, state.team);
      if (state.ranked) this.renderRanked(body, state.ranked);
    }
  }

  private renderGuildWar(body: HTMLElement, war: ArenaGuildWar): void {
    const head = document.createElement('h3');
    head.className = 'hnmo-arena-head';
    head.textContent = tx('Guild war', 'Bang chiến');
    const rules = document.createElement('p');
    rules.className = 'hnmo-muted';
    rules.textContent = tx(
      `Valley of Loren, ${war.days} at ${war.times} (Sydney). Members of a guild sign up; a guild needs ${war.minMembers} in the Arena at the start. A kill is a point; hold a flag alone for ${war.captureSeconds} s to take it, and every held flag keeps giving points. Winners +15 Arena points, others +4.`,
      `Thung lũng Loren, ${war.days} lúc ${war.times} (giờ Sydney). Thành viên bang đăng ký; bang cần ${war.minMembers} người có mặt trong Đấu trường lúc bắt đầu. Hạ một người được một điểm; đứng một mình ở cờ ${war.captureSeconds} giây để chiếm, cờ đang giữ liên tục cho điểm. Bang thắng +15 điểm Đấu trường, bang thua +4.`,
    );
    body.append(head, rules);
    if (war.champion) {
      const champion = document.createElement('p');
      champion.id = 'arena-guild-champion';
      champion.textContent = tx(`Guild war champion of the week: ${war.champion}`, `Vô địch bang chiến tuần này: ${war.champion}`);
      body.append(champion);
    }
    const line = document.createElement('p');
    line.id = 'arena-guild-status';
    if (war.running) {
      line.textContent = this.guildWarText(war);
      body.append(line);
      return;
    }
    const minutes = Math.floor(war.secondsToStart / 60), seconds = war.secondsToStart % 60;
    const signed = war.guilds.map((g) => `${g.name} (${g.count})`).join(', ');
    line.textContent = war.waiting > 0
      ? tx(`${war.waiting} more match(es) to come.`, `Còn ${war.waiting} trận nữa.`)
      : war.nextSydney
        ? tx(`Next: ${war.nextSydney} Sydney (in ${minutes}:${String(seconds).padStart(2, '0')}). Signed up: ${signed || 'none yet'}.`,
          `Lần tới: ${war.nextSydney} giờ Sydney (còn ${minutes}:${String(seconds).padStart(2, '0')}). Đã đăng ký: ${signed || 'chưa có'}.`)
        : tx('No guild war planned.', 'Chưa có lịch bang chiến.');
    body.append(line);
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'hnmo-btn';
    button.id = war.signedUp ? 'arena-guild-leave' : 'arena-guild-join';
    button.disabled = !war.signupOpen && !war.signedUp;
    button.textContent = war.signedUp
      ? tx(`Cancel (fighting for ${war.myGuild ?? ''})`, `Huỷ (đang đăng ký cho ${war.myGuild ?? ''})`)
      : war.signupOpen ? tx('Fight for my guild', 'Chiến đấu cho bang của tôi') : tx('The sign-up is closed', 'Đăng ký đã đóng');
    button.addEventListener('click', () => {
      this.options.send('HnmoArenaTravelRequest', { Action: war.signedUp ? 10 : 9, Index: 0 });
    });
    body.append(button);
  }

  /** "Guild A 12 : 9 Guild B · flags A - B · 11:20 · you: red", then the result. */
  private guildWarText(war: ArenaGuildWar): string {
    const score = `${war.guildA ?? '?'} ${war.scoreA} : ${war.scoreB} ${war.guildB ?? '?'}`;
    if (war.ended) {
      const result = war.winner === 0 ? tx('Draw', 'Hoà')
        : !war.side ? tx(`${war.winner > 0 ? war.guildA : war.guildB} won`, `${war.winner > 0 ? war.guildA : war.guildB} thắng`)
          : (war.winner > 0) === (war.side === 1) ? tx('Victory', 'Thắng') : tx('Defeat', 'Thua');
      return `${result} · ${score}`;
    }
    const time = `${Math.floor(war.secondsLeft / 60)}:${String(war.secondsLeft % 60).padStart(2, '0')}`;
    const mark = (f: ArenaFlag) => (f.owner === 1 ? tx('red', 'đỏ') : f.owner === 2 ? tx('blue', 'xanh') : '-') + (f.taking ? `>${f.taking === 1 ? tx('red', 'đỏ') : tx('blue', 'xanh')} ${f.seconds}s` : '');
    const flags = (war.flags ?? []).map((f) => `(${f.x},${f.y}) ${mark(f)}`).join(' ');
    const me = war.side === 1 ? tx(' · you: red', ' · bạn: đỏ') : war.side === 2 ? tx(' · you: blue', ' · bạn: xanh') : '';
    return `${score} · ${tx('flags', 'cờ')} ${flags} · ${time}${me}`;
  }

  private renderRoyale(body: HTMLElement, royale: ArenaRoyale): void {
    const head = document.createElement('h3');
    head.className = 'hnmo-arena-head';
    head.textContent = tx('Battle royale', 'Đấu sinh tồn');
    const rules = document.createElement('p');
    rules.className = 'hnmo-muted';
    rules.textContent = tx(
      `Lorencia, every day at ${royale.times} (Sydney). One life, everyone against everyone; the safe zone shrinks and outside it (and in town) you lose health. The last one standing wins 20 Arena points, everyone else 2.`,
      `Lorencia, mỗi ngày lúc ${royale.times} (giờ Sydney). Một mạng, ai cũng là đối thủ; vùng an toàn thu nhỏ dần, ra ngoài vùng (và trong thị trấn) sẽ mất máu. Người cuối cùng còn đứng được 20 điểm Đấu trường, những người khác 2.`,
    );
    body.append(head, rules);
    const line = document.createElement('p');
    line.id = 'arena-royale-status';
    if (royale.running) {
      line.textContent = this.royaleText(royale);
      body.append(line);
      return;
    }
    const minutes = Math.floor(royale.secondsToStart / 60), seconds = royale.secondsToStart % 60;
    const wait = `${minutes}:${String(seconds).padStart(2, '0')}`;
    line.textContent = royale.nextSydney
      ? tx(`Next match: ${royale.nextSydney} Sydney (in ${wait}). Signed up: ${royale.signups} (at least ${royale.minPlayers}).`,
        `Trận tiếp theo: ${royale.nextSydney} giờ Sydney (còn ${wait}). Đã đăng ký: ${royale.signups} (tối thiểu ${royale.minPlayers}).`)
      : tx('No match planned.', 'Chưa có lịch đấu.');
    body.append(line);
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'hnmo-btn';
    button.id = royale.signedUp ? 'arena-royale-leave' : 'arena-royale-join';
    button.disabled = !royale.signupOpen && !royale.signedUp;
    button.textContent = royale.signedUp
      ? tx('Cancel my sign-up', 'Huỷ đăng ký')
      : royale.signupOpen ? tx('Sign up for the battle royale', 'Đăng ký đấu sinh tồn') : tx('The sign-up opens shortly before the start', 'Đăng ký mở ngay trước giờ đấu');
    button.addEventListener('click', () => {
      this.options.send('HnmoArenaTravelRequest', { Action: royale.signedUp ? 8 : 7, Index: 0 });
    });
    body.append(button);
  }

  /** "5 left · zone (60, 200) r 40 → (52, 210) · 7:12 · you are in the zone", the end's place and winner. */
  private royaleText(royale: ArenaRoyale): string {
    if (royale.ended) {
      const result = royale.place === 1 ? tx('You won!', 'Bạn thắng!')
        : royale.place ? tx(`Place ${royale.place} of ${royale.total}`, `Hạng ${royale.place}/${royale.total}`) : '';
      return `${result} ${tx('Winner', 'Người thắng')}: ${royale.winner ?? '-'}`.trim();
    }
    const time = `${Math.floor(royale.secondsLeft / 60)}:${String(royale.secondsLeft % 60).padStart(2, '0')}`;
    const cell = this.options.heroCell?.() ?? null;
    const inside = cell ? Math.hypot(cell.x - royale.zoneX, cell.y - royale.zoneY) <= royale.zoneRadius : true;
    const me = !royale.fighter ? '' : !royale.alive ? tx(' · you are out', ' · bạn đã bị loại')
      : inside ? tx(' · you are in the zone', ' · bạn trong vùng an toàn') : tx(' · OUTSIDE THE ZONE', ' · NGOÀI VÙNG AN TOÀN');
    return `${tx(`${royale.left} left`, `Còn ${royale.left}`)} · ${tx('zone', 'vùng')} (${royale.zoneX}, ${royale.zoneY}) r ${royale.zoneRadius} → (${royale.finalX}, ${royale.finalY}) · ${time}${me}`;
  }

  private renderTeam(body: HTMLElement, team: ArenaTeam): void {
    const head = document.createElement('h3');
    head.className = 'hnmo-arena-head';
    // While a battle runs, its own size (a 3v3 when fewer came); otherwise the full size the queue looks for.
    const n = team.running && team.teamA?.length ? team.teamA.length : team.size;
    head.textContent = tx(`Team battle ${n}v${n}`, `Đấu đội ${n}v${n}`);
    const rules = document.createElement('p');
    rules.className = 'hnmo-muted';
    rules.textContent = tx(
      `Valley of Loren: the most kills in ${team.minutes} minutes, or ${team.killsToWin} kills first, wins. Queue alone or as a party (the leader queues it). After a minute of waiting a ${team.minSize}v${team.minSize} may start.`,
      `Thung lũng Loren: đội hạ nhiều hơn trong ${team.minutes} phút, hoặc đủ ${team.killsToWin} mạng trước, sẽ thắng. Xếp hàng một mình hoặc cả nhóm (trưởng nhóm xếp). Chờ quá một phút có thể đấu ${team.minSize}v${team.minSize}.`,
    );
    body.append(head, rules);
    if (team.side) {
      const score = document.createElement('p');
      score.id = 'arena-team-score';
      score.textContent = this.scoreText(team);
      body.append(score);
      const sides: [string, ArenaFighter[] | null | undefined][] = [[tx('Red', 'Đỏ'), team.teamA], [tx('Blue', 'Xanh'), team.teamB]];
      for (const [name, fighters] of sides) {
        const p = document.createElement('p');
        p.className = 'hnmo-muted';
        p.textContent = `${name}: ${(fighters ?? []).map((f) => `${f.name} (${f.kills})`).join(', ')}`;
        body.append(p);
      }
      return;
    }
    const queue = document.createElement('button');
    queue.type = 'button';
    queue.className = 'hnmo-btn';
    queue.id = team.queued ? 'arena-team-leave' : 'arena-team-join';
    queue.textContent = team.queued
      ? tx(`Leave the team queue (${team.waiting} waiting)`, `Rời hàng chờ đấu đội (${team.waiting} người chờ)`)
      : team.running
        ? tx('Queue for the next team battle', 'Xếp hàng trận đấu đội tiếp theo')
        : tx('Find a team battle', 'Tìm trận đấu đội');
    queue.addEventListener('click', () => {
      this.options.send('HnmoArenaTravelRequest', { Action: team.queued ? 6 : 5, Index: 0 });
    });
    body.append(queue);
    if (team.running) {
      const busy = document.createElement('p');
      busy.className = 'hnmo-muted';
      busy.textContent = tx(`A battle is being fought (${team.scoreA} : ${team.scoreB}).`, `Đang có trận đấu (${team.scoreA} : ${team.scoreB}).`);
      body.append(busy);
    }
  }

  private scoreText(team: ArenaTeam): string {
    const time = `${Math.floor(team.secondsLeft / 60)}:${String(team.secondsLeft % 60).padStart(2, '0')}`;
    const mine = team.side === 1 ? tx('you: Red', 'bạn: Đỏ') : tx('you: Blue', 'bạn: Xanh');
    if (team.ended) {
      const result = team.winner === 0 ? tx('Draw', 'Hoà')
        : (team.winner === 1) === (team.side === 1) ? tx('Victory', 'Thắng') : tx('Defeat', 'Thua');
      return `${result} · ${tx('Red', 'Đỏ')} ${team.scoreA} : ${team.scoreB} ${tx('Blue', 'Xanh')}`;
    }
    return `${tx('Red', 'Đỏ')} ${team.scoreA} : ${team.scoreB} ${tx('Blue', 'Xanh')} · ${time} · ${mine}`;
  }

  /**
   * While the player waits for or fights a team battle, the state is asked for every 2 s (also with the window closed),
   * and while they fight a small score bar stays on screen.
   */
  private updateScoreboard(team: ArenaTeam | null, royale: ArenaRoyale | null = null, war: ArenaGuildWar | null = null): void {
    const inRoyale = !!royale && royale.running && royale.fighter;
    const inWar = !!war && war.running && war.side !== 0;
    const fighting = (!!team && team.running && team.side !== 0) || inRoyale || inWar;
    const watching = fighting || !!team?.queued || !!royale?.signedUp || !!war?.signedUp || (!!war && war.waiting > 0);
    if (!watching) {
      window.clearInterval(this.battleTimer);
      this.battleTimer = undefined;
    }
    if (!fighting) {
      this.scoreboard?.remove();
      this.scoreboard = null;
      if (!watching) return;
    }
    if (fighting) {
      if (!this.scoreboard) this.scoreboard = this.createScoreboard();
      const text = inWar && war ? this.guildWarText(war) : inRoyale && royale ? this.royaleText(royale) : team ? this.scoreText(team) : '';
      this.scoreboard.textContent = text;
      this.scoreboard.classList.toggle('arena-outside', inRoyale && text.includes(tx('OUTSIDE', 'NGOÀI')));
      this.scoreboard.style.color = this.scoreboard.classList.contains('arena-outside') ? '#ff7060' : '#f0e0b0';
    }
    if (this.battleTimer === undefined) {
      this.battleTimer = window.setInterval(() => {
        this.options.send('HnmoArenaTravelRequest', { Action: 0, Index: 0 });
      }, 2000);
    }
  }

  private createScoreboard(): HTMLElement {
    const bar = document.createElement('div');
    bar.id = 'arena-scoreboard';
    bar.setAttribute('role', 'status');
    bar.style.cssText = 'position:fixed;top:8px;left:50%;transform:translateX(-50%);z-index:30;padding:4px 12px;border-radius:6px;background:rgba(0,0,0,.7);color:#f0e0b0;font:600 14px sans-serif;pointer-events:none;white-space:nowrap';
    document.body.append(bar);
    return bar;
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
      `You have ${shop.points} Arena points (ranked 1v1: win 5, loss 2; team battle: win 8, loss 3; battle royale: winner 20, others 2; up to 100 a day).`,
      `Bạn có ${shop.points} điểm Đấu trường (xếp hạng 1v1: thắng 5, thua 2; đấu đội: thắng 8, thua 3; sinh tồn: người thắng 20, còn lại 2; tối đa 100 mỗi ngày).`,
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
