// HNMO's world bosses in the web game: a line on the screen while a boss is alive (the boss, its map, the time
// left and your damage rank) and a window (N) with every boss, its next spawn in Sydney time and the player's own
// time, the damage table of a boss on a map, the waiting rewards and the boss-hunt points of the week. It has no
// desktop window, so it is HNMO's own interface document (public/data/Interface/RmlUi/hnmo_world_bosses.rml) in the
// game's message box frame (window_shell). The game asks for the state once when it enters the world
// (HnmoWorldBossesRequest, F5 0B); the server then sends it (HnmoWorldBossesState, C2 F5 0C) whenever something
// changes: announcements (told in the chat), damage ranks while the player fights a boss, rewards. The window's
// button sends HnmoWorldBossesAction (F5 0D).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale } from './nativeText.js';
import { tr, rewardText } from './missionsWindow.js';

type CoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';
interface RewardItem { group: number; number: number; level: number; count: number }
interface Reward { zen: number; coins: number; coinType: CoinType; coinsGiven?: boolean; items: RewardItem[] }
interface TopLine { rank: number; name: string; damage: number; share: number }
export interface BossFight {
  x: number; y: number; spawnedAt: number; despawnAt: number; maximumHealth: number; totalDamage: number;
  fighters: number; myDamage: number; myRank: number; top: TopLine[];
}
export interface BossState { id: string; nameEn: string; nameVi: string; map: number; mapName?: string; nextSpawn?: number; alive?: BossFight }
export interface WorldBossBoard {
  v: number; now: number; bosses: BossState[];
  owed: { bossEn: string; bossVi: string; tier: string; reward: Reward }[];
  huntPoints: number; huntRank: number; weekEnds: number;
  event?: { type: string; result?: string; bossId?: string; textEn?: string; textVi?: string; count?: number; inGame?: boolean };
}

export interface WorldBossWindowOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The name of an item. */
  itemName: (group: number, number: number) => string;
  /** The name of a map (the game's own); the server's name when it gives none. */
  mapName?: (map: number) => string;
}

interface Row {
  id: string; name: string; map: string; alive: boolean; status: string; when: string; mine: string;
  top: { line: string }[];
}
interface Owed { line: string }

interface BossesModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  loading: boolean; rows: Row[]; owed: Owed[]; has_owed: boolean;
  loading_label: string; empty_label: string; owed_head: string; claim_label: string; close_label: string;
  points_text: string; hint_text: string;
  wb_claim: () => void; wb_close: () => void;
}

const ACTION = { ClaimRewards: 1 } as const;
const SYDNEY = 'Australia/Sydney';

function locale(): string { return currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU'; }

function number(value: number): string { return value.toLocaleString(locale()); }

/** "Sat 20:00" in a time zone (the player's own when none is given). */
export function clockText(at: number, timeZone?: string): string {
  return new Intl.DateTimeFormat(locale(), { weekday: 'short', hour: '2-digit', minute: '2-digit', hour12: false, ...(timeZone ? { timeZone } : {}) }).format(at);
}

/** "1 h 05 min" / "4:59" until a time. */
export function countdown(now: number, at: number): string {
  const seconds = Math.max(0, Math.round((at - now) / 1000));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0) return tr('%d h %d min', hours, minutes);
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`;
}

function tierName(tier: string): string {
  switch (tier) {
    case 'top1': return tr('Rank 1');
    case 'top2': return tr('Rank 2');
    case 'top3': return tr('Rank 3');
    case 'top10': return tr('Ranks 4 to 10');
    case 'participation': return tr('Participation');
    default: return tr('Last hit');
  }
}

export class WorldBossWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private board: WorldBossBoard | null = null;
  /** The difference between the server's clock and this one. */
  private skew = 0;
  private readonly model: BossesModel;
  private readonly line: HTMLDivElement;
  private timer: number | null = null;

  constructor(private readonly options: WorldBossWindowOptions) {
    this.model = {
      has_title: true, title: 'World bosses', positioned: false, root_x: 0, root_y: 0,
      loading: true, rows: [], owed: [], has_owed: false,
      loading_label: '', empty_label: '', owed_head: '', claim_label: '', close_label: '', points_text: '', hint_text: '',
      wb_claim: () => this.options.send('HnmoWorldBossesAction', { Action: ACTION.ClaimRewards, Argument: '' }),
      wb_close: () => this.close(),
    };
    this.line = document.createElement('div');
    this.line.className = 'hnmo-boss-line';
    this.line.setAttribute('role', 'status');
    this.line.hidden = true;
    Object.assign(this.line.style, {
      position: 'fixed', left: '50%', top: '104px', transform: 'translateX(-50%)', zIndex: '70', pointerEvents: 'none',
      padding: '3px 10px', background: 'rgba(0, 0, 0, 0.55)', border: '1px solid #8a6a30', color: '#ffe9a8',
      font: '12px Tahoma, Verdana, sans-serif', whiteSpace: 'nowrap', textShadow: '1px 1px 0 #000',
    });
    document.body.append(this.line);
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { event.preventDefault(); this.close(); }
    });
    onLanguageChange(() => this.fill());
  }

  get isOpen(): boolean { return this.wanted; }

  /** The last state from the server. */
  get state(): WorldBossBoard | null { return this.board; }

  /** The line on the screen (for tests). */
  get hudLine(): HTMLDivElement { return this.line; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoWorldBossesRequest', {});
  }

  toggle(): void {
    if (this.wanted) this.close(); else this.open();
  }

  close(): void {
    this.wanted = false;
    this.doc?.close();
    this.doc = null;
  }

  open(): void {
    this.wanted = true;
    this.options.send('HnmoWorldBossesRequest', {});
    this.fill();
    if (this.doc) { this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_world_bosses', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-world-bosses');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', tr('World bosses'));
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      this.place();
      this.fill();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** A packet from the server: the world bosses state (C2 F5 0C). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x0c) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoWorldBossesState', bytes);
    let board: WorldBossBoard;
    try {
      board = JSON.parse(Json) as WorldBossBoard;
    } catch {
      console.warn('World bosses: unreadable state');
      return true;
    }
    this.board = board;
    this.skew = board.now - Date.now();
    this.model.loading = false;
    if (board.event) this.notice(board, board.event);
    this.fill();
    this.tick();
    return true;
  }

  private now(): number { return Date.now() + this.skew; }

  private name(boss: { nameEn: string; nameVi: string }): string {
    return (currentLanguage() === 'vi' ? boss.nameVi : boss.nameEn) || boss.nameEn;
  }

  private mapOf(boss: BossState): string {
    return this.options.mapName?.(boss.map) || boss.mapName || tr('Map %d', boss.map);
  }

  /** The chat line of an event. */
  private notice(board: WorldBossBoard, event: NonNullable<WorldBossBoard['event']>): void {
    const say = this.options.message;
    switch (event.type) {
      case 'soon': case 'spawn': case 'kill': case 'escape': {
        // The server's golden notice (in the player's language) already put it in the chat.
        if (event.inGame) break;
        const text = (currentLanguage() === 'vi' ? event.textVi : event.textEn) || event.textEn;
        if (text) say(`[${tr('World boss')}] ${text}`);
        break;
      }
      case 'reward':
        if ((event.count ?? 0) > 0) say(tr('World boss rewards received (%d).', event.count ?? 0));
        if (event.result === 'BagFull') say(tr('Your bag is full: the world boss rewards wait. Make room, then take them in the world boss window (N).'));
        break;
      case 'claim':
        if (event.result === 'Ok') say(tr('World boss rewards received.'));
        else if (event.result === 'BagFull') say(tr('Your bag is full: the world boss rewards wait. Make room, then take them in the world boss window (N).'));
        else say(tr('No world boss rewards are waiting.'));
        break;
    }
    void board;
  }

  private statusOf(boss: BossState, now: number): { status: string; when: string; mine: string } {
    const fight = boss.alive;
    if (fight) {
      const mine = fight.myRank > 0
        ? tr('Your damage: %s (rank %d of %d)', number(fight.myDamage), fight.myRank, fight.fighters)
        : tr('You haven\'t hit it yet.');
      return { status: tr('On the map now at %d, %d: leaves in %s', fight.x, fight.y, countdown(now, fight.despawnAt)), when: '', mine };
    }
    if (!boss.nextSpawn) return { status: tr('No spawn planned.'), when: '', mine: '' };
    return {
      status: tr('Next: in %s', countdown(now, boss.nextSpawn)),
      when: tr('%s Sydney time (%s your time)', clockText(boss.nextSpawn, SYDNEY), clockText(boss.nextSpawn)),
      mine: '',
    };
  }

  private fill(): void {
    const m = this.model;
    m.title = tr('World bosses');
    m.loading_label = tr('Loading...');
    m.empty_label = tr('No world bosses right now.');
    m.owed_head = tr('Rewards waiting for room in your bag');
    m.claim_label = tr('Take');
    m.close_label = tr('Close');
    m.hint_text = tr('Rewards go by the damage you deal; no damage, no reward.');
    const board = this.board;
    if (!board) { this.doc?.update(); return; }
    const now = this.now();
    m.rows = board.bosses.map((boss) => {
      const { status, when, mine } = this.statusOf(boss, now);
      return {
        id: boss.id, name: this.name(boss), map: this.mapOf(boss), alive: !!boss.alive, status, when, mine,
        top: (boss.alive?.top ?? []).map((t) => ({ line: `${t.rank}. ${t.name} · ${number(t.damage)} (${t.share}%)` })),
      };
    });
    m.owed = board.owed.map((o) => ({
      line: `${(currentLanguage() === 'vi' ? o.bossVi : o.bossEn) || o.bossEn} · ${tierName(o.tier)} · ${rewardText({ ...o.reward, coins: o.reward.coinsGiven ? 0 : o.reward.coins }, this.options.itemName)}`,
    }));
    m.has_owed = m.owed.length > 0;
    m.points_text = board.huntRank > 0
      ? tr('Boss-hunt points this week: %d (rank %d). The week ends in %s.', board.huntPoints, board.huntRank, countdown(now, board.weekEnds))
      : tr('Boss-hunt points this week: %d. The week ends in %s.', board.huntPoints, countdown(now, board.weekEnds));
    this.doc?.update();
  }

  /** The line on the screen and the countdowns, every second while a boss is alive or the window is open. */
  private tick(): void {
    const board = this.board;
    const alive = board?.bosses.filter((b) => b.alive && (b.alive.despawnAt > this.now())) ?? [];
    if (alive.length > 0) {
      const now = this.now();
      this.line.textContent = alive.map((boss) => {
        const fight = boss.alive!;
        const rank = fight.myRank > 0 ? ` · ${tr('your rank %d', fight.myRank)}` : '';
        return `⚔ ${this.name(boss)} · ${this.mapOf(boss)} · ${countdown(now, fight.despawnAt)}${rank}`;
      }).join('   ');
      this.line.hidden = false;
    } else {
      this.line.hidden = true;
    }
    if (this.wanted) this.fill();
    const needed = alive.length > 0 || this.wanted;
    if (needed && this.timer === null) this.timer = window.setInterval(() => this.tick(), 1000);
    if (!needed && this.timer !== null) { window.clearInterval(this.timer); this.timer = null; }
  }

  // A dp window: the window in dp, scaled by the dp ratio (TypographyScale), as the desktop's documents.
  private place(): void {
    const doc = this.doc;
    if (!doc) return;
    const scale = typographyScale();
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0px', top: '0px', width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none', zIndex: '80',
    });
    doc.setScale(scale);
    doc.update();
  }
}
