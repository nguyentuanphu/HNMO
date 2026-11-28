import { panelKey } from '../game/keyboardShortcuts.js';
// HNMO's top races in the web game: a window (F9, and the touch "more" menu) with the running races (what they count,
// who takes part, the time left in Sydney time and the player's own time), the player's rank and value, the top 10 of
// the player's table, the prizes, the races to come, the past winners and the prizes waiting for room in the bag. It
// has no desktop window, so it is HNMO's own interface document (public/data/Interface/RmlUi/hnmo_top_races.rml) in the
// game's message box frame (window_shell), like the missions window. The game asks for the state once when it enters
// the world (HnmoTopRacesRequest, F5 17); the server then sends it (HnmoTopRacesState, C2 F5 18) whenever something
// changes: a race started or ended (told in the chat), a prize was given. The window's button sends HnmoTopRacesAction
// (F5 19).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale } from './nativeText.js';
import { tr, rewardText } from './missionsWindow.js';
import { clockText, countdown as shortCountdown } from './worldBossWindow.js';

type CoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';
export type TopRaceKind = 'ResetsGained' | 'LevelsGained' | 'LevelReached' | 'BossHuntPoints' | 'PkKills' | 'EventWins' | 'GuildPoints' | 'MonsterKills' | 'OnlineMinutes';
export type TopRacePeriod = 'Daily' | 'Weekly' | 'Monthly' | 'Custom';
export type TopRaceScope = 'AllClasses' | 'OneClass' | 'PerClass';
export type TopRaceEvent = 'Any' | 'BloodCastle' | 'DevilSquare' | 'ChaosCastle';
interface RewardItem { group: number; number: number; level: number; count: number; name?: string }
export interface TopRacePrize { from: number; to: number; coins: number; coinType: CoinType; zen: number; items: RewardItem[] }
export interface TopRaceInfo {
  id: string; titleEn: string; titleVi: string; kind: TopRaceKind; event: TopRaceEvent; scope: TopRaceScope; cls: number;
  period: TopRacePeriod; minimum: number; prizes: TopRacePrize[];
}
export interface TopRaceLineState { rank: number; name: string; value: number; cls: number }
export interface TopRaceResultState {
  raceId: string; titleEn: string; titleVi: string; kind: TopRaceKind; period: TopRacePeriod; periodKey: string; start: number; end: number;
  paid: boolean; tables: { cls: number; lines: (TopRaceLineState & { prized: boolean; coins: number; coinType: CoinType; zen: number; items: RewardItem[] })[] }[];
}
export interface TopRacesState {
  v: number; now: number; myClass: number;
  races: { race: TopRaceInfo; start: number; end: number; eligible: boolean; table: number; myRank: number; myValue: number; top: TopRaceLineState[] }[];
  upcoming: { race: TopRaceInfo; start: number; end: number }[];
  history: TopRaceResultState[];
  owed: { raceId: string; titleEn: string; titleVi: string; period: string; rank: number; reward: { zen: number; items: RewardItem[] } }[];
  event?: { type: string; result?: string; raceId?: string; textEn?: string; textVi?: string; rank?: number; coins?: number; inGame?: boolean };
}

export interface TopRacesWindowOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The name of an item. */
  itemName: (group: number, number: number) => string;
}

interface Row {
  id: string; title: string; about: string; time: string; when: string; mine: string; top: { line: string }[]; prizes: { line: string }[];
  has_top: boolean;
}
interface Line { line: string }

interface TopRacesModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  loading: boolean; rows: Row[]; upcoming: Line[]; has_upcoming: boolean; history: Line[]; has_history: boolean;
  owed: Line[]; has_owed: boolean;
  loading_label: string; empty_label: string; upcoming_head: string; history_head: string; owed_head: string;
  claim_label: string; close_label: string; hint_text: string;
  tr_claim: () => void; tr_close: () => void;
}

const ACTION = { ClaimRewards: 1 } as const;
const SYDNEY = 'Australia/Sydney';
const STATE_SUBCODE = 0x18;

function locale(): string { return currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU'; }

function number(value: number): string { return value.toLocaleString(locale()); }

/** "2 d 5 h" for a day or more, else "5 h 03 min" / "4:59". */
function countdown(now: number, at: number): string {
  const minutes = Math.max(0, Math.floor((at - now) / 60000));
  if (minutes >= 1440) return tr('%d d %d h', Math.floor(minutes / 1440), Math.floor((minutes % 1440) / 60));
  return shortCountdown(now, at);
}

function title(race: { titleEn: string; titleVi: string }): string {
  return (currentLanguage() === 'vi' ? race.titleVi : race.titleEn) || race.titleEn;
}

/** The class names (each with its evolutions). */
export function className(cls: number): string {
  switch (cls) {
    case 0: return tr('Dark Wizard');
    case 1: return tr('Dark Knight');
    case 2: return tr('Fairy Elf');
    case 3: return tr('Magic Gladiator');
    case 4: return tr('Dark Lord');
    case 5: return tr('Summoner');
    case 6: return tr('Rage Fighter');
    default: return '';
  }
}

/** What a race counts. */
export function kindText(kind: TopRaceKind, event: TopRaceEvent = 'Any'): string {
  switch (kind) {
    case 'ResetsGained': return tr('Resets done');
    case 'LevelsGained': return tr('Levels gained');
    case 'LevelReached': return tr('Highest level reached');
    case 'BossHuntPoints': return tr('Boss-hunt points');
    case 'PkKills': return tr('Players defeated (PK)');
    case 'EventWins':
      return event === 'BloodCastle' ? tr('Blood Castle wins') : event === 'DevilSquare' ? tr('Devil Square wins') : event === 'ChaosCastle' ? tr('Chaos Castle wins') : tr('Event wins');
    case 'GuildPoints': return tr('Guild points');
    case 'MonsterKills': return tr('Monsters defeated');
    default: return tr('Minutes in the game');
  }
}

/** A value with its unit: "12 resets", "level 380". */
export function valueText(kind: TopRaceKind, value: number): string {
  const n = number(value);
  switch (kind) {
    case 'ResetsGained': return tr('%s resets', n);
    case 'LevelsGained': return tr('%s levels', n);
    case 'LevelReached': return tr('level %s', n);
    case 'BossHuntPoints': case 'GuildPoints': return tr('%s points', n);
    case 'PkKills': return tr('%s players', n);
    case 'EventWins': return tr('%s wins', n);
    case 'MonsterKills': return tr('%s monsters', n);
    default: return tr('%s min', n);
  }
}

export function periodText(period: TopRacePeriod): string {
  switch (period) {
    case 'Daily': return tr('daily');
    case 'Weekly': return tr('weekly');
    case 'Monthly': return tr('monthly');
    default: return tr('special dates');
  }
}

/** Who takes part. */
export function scopeText(race: TopRaceInfo): string {
  if (race.kind === 'GuildPoints') return tr('guilds (every member gets the prize)');
  if (race.scope === 'OneClass') return tr('only %s', className(race.cls));
  if (race.scope === 'PerClass') return tr('a table per class');
  return tr('all classes');
}

/** A prize: "Rank 1: 300 Goblin Points" / "Ranks 4-10: ...". */
export function prizeText(prize: TopRacePrize, itemName: (group: number, number: number) => string): string {
  const ranks = prize.to > prize.from ? tr('Ranks %d-%d', prize.from, prize.to) : tr('Rank %d', prize.from);
  const reward = rewardText({ zen: prize.zen, coins: prize.coins, coinType: prize.coinType, items: prize.items }, (g, n) => prize.items.find((i) => i.group === g && i.number === n)?.name || itemName(g, n));
  return `${ranks}: ${reward.replace(`${tr('Reward')}: `, '')}`;
}

export class TopRacesWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private data: TopRacesState | null = null;
  /** The difference between the server's clock and this one. */
  private skew = 0;
  private readonly model: TopRacesModel;
  private timer: number | null = null;

  constructor(private readonly options: TopRacesWindowOptions) {
    this.model = {
      has_title: true, title: 'Top races', positioned: false, root_x: 0, root_y: 0,
      loading: true, rows: [], upcoming: [], has_upcoming: false, history: [], has_history: false, owed: [], has_owed: false,
      loading_label: '', empty_label: '', upcoming_head: '', history_head: '', owed_head: '', claim_label: '', close_label: '', hint_text: '',
      tr_claim: () => this.options.send('HnmoTopRacesAction', { Action: ACTION.ClaimRewards, Argument: '' }),
      tr_close: () => this.close(),
    };
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (panelKey(event, 'Escape') && this.isOpen) { event.preventDefault(); this.close(); }
    });
    onLanguageChange(() => this.fill());
  }

  get isOpen(): boolean { return this.wanted; }

  /** The last state from the server. */
  get state(): TopRacesState | null { return this.data; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoTopRacesRequest', {});
  }

  toggle(): void {
    if (this.wanted) this.close(); else this.open();
  }

  close(): void {
    this.wanted = false;
    this.doc?.close();
    this.doc = null;
    this.tick();
  }

  open(): void {
    this.wanted = true;
    this.options.send('HnmoTopRacesRequest', {});
    this.fill();
    this.tick();
    if (this.doc) { this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_top_races', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-top-races');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', tr('Top races'));
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      this.place();
      this.fill();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** A packet from the server: the top races state (C2 F5 18). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== STATE_SUBCODE) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoTopRacesState', bytes);
    let data: TopRacesState;
    try {
      data = JSON.parse(Json) as TopRacesState;
    } catch {
      console.warn('Top races: unreadable state');
      return true;
    }
    this.data = data;
    this.skew = data.now - Date.now();
    this.model.loading = false;
    if (data.event) this.notice(data.event);
    this.fill();
    return true;
  }

  private now(): number { return Date.now() + this.skew; }

  /** The chat line of an event. */
  private notice(event: NonNullable<TopRacesState['event']>): void {
    const say = this.options.message;
    const full = tr('Your bag is full: the top race prizes wait. Make room, then take them in the top races window (F9).');
    switch (event.type) {
      case 'start': case 'end': {
        // The server's golden notice (in the player's language) already put it in the chat.
        if (event.inGame) break;
        const text = (currentLanguage() === 'vi' ? event.textVi : event.textEn) || event.textEn;
        if (text) say(`[${tr('Top race')}] ${text}`);
        break;
      }
      case 'prize': {
        const name = (currentLanguage() === 'vi' ? event.textVi : event.textEn) || event.textEn || '';
        say((event.coins ?? 0) > 0
          ? tr('Top race "%s": you are rank %d and got %d coins.', name, event.rank ?? 0, event.coins ?? 0)
          : tr('Top race "%s": you are rank %d.', name, event.rank ?? 0));
        break;
      }
      case 'goods':
        if (event.result === 'BagFull') say(full);
        else say(tr('Top race prizes received.'));
        break;
      case 'claim':
        if (event.result === 'Ok') say(tr('Top race prizes received.'));
        else if (event.result === 'BagFull') say(full);
        else say(tr('No top race prizes are waiting.'));
        break;
    }
  }

  private fill(): void {
    const m = this.model;
    m.title = tr('Top races');
    m.loading_label = tr('Loading...');
    m.empty_label = tr('No top race is running right now.');
    m.upcoming_head = tr('Coming races');
    m.history_head = tr('Past winners');
    m.owed_head = tr('Prizes waiting for room in your bag');
    m.claim_label = tr('Take');
    m.close_label = tr('Close');
    m.hint_text = tr('Prizes are paid once when a race ends: coins to your account, zen and items to the character.');
    const data = this.data;
    if (!data) { this.doc?.update(); return; }
    const now = this.now();
    const itemName = this.options.itemName;
    m.rows = data.races.map((entry) => {
      const race = entry.race;
      let mine: string;
      if (!entry.eligible) mine = race.kind === 'GuildPoints' ? tr('Join a guild to take part.') : tr('Only for %s.', className(race.cls));
      else if (entry.myRank > 0) mine = tr('Your rank: %d · %s', entry.myRank, valueText(race.kind, entry.myValue));
      else mine = tr('You are not ranked yet.');
      const table = entry.table >= 0 && race.scope === 'PerClass' ? ` (${className(entry.table)})` : '';
      return {
        id: race.id,
        title: title(race),
        about: `${kindText(race.kind, race.event)} · ${periodText(race.period)} · ${scopeText(race)}${table}`,
        time: tr('Ends in %s', countdown(now, entry.end)),
        when: tr('%s Sydney time (%s your time)', clockText(entry.end, SYDNEY), clockText(entry.end)),
        mine,
        top: entry.top.map((line) => ({ line: `${line.rank}. ${line.name} · ${valueText(race.kind, line.value)}` })),
        has_top: entry.top.length > 0,
        prizes: [
          ...race.prizes.map((p) => ({ line: prizeText(p, itemName) })),
          ...(race.minimum > 1 ? [{ line: tr('At least %s for a prize.', valueText(race.kind, race.minimum)) }] : []),
        ],
      };
    });
    m.upcoming = data.upcoming.map((u) => ({
      line: `${title(u.race)} · ${kindText(u.race.kind, u.race.event)} · ${tr('starts in %s', countdown(now, u.start))} (${tr('%s Sydney time (%s your time)', clockText(u.start, SYDNEY), clockText(u.start))})`,
    }));
    m.has_upcoming = m.upcoming.length > 0;
    m.history = data.history.flatMap((h) => {
      const winners = h.tables.map((t) => {
        const first = t.lines[0];
        if (!first) return '';
        const cls = t.cls >= 0 ? `${className(t.cls)}: ` : '';
        return `${cls}${first.name} (${valueText(h.kind, first.value)})`;
      }).filter((w) => w.length > 0);
      return [{ line: `${title(h)} · ${clockText(h.end, SYDNEY)} · ${winners.length > 0 ? winners.join(', ') : tr('nobody took part')}` }];
    });
    m.has_history = m.history.length > 0;
    m.owed = data.owed.map((o) => ({
      line: `${title(o)} · ${tr('Rank %d', o.rank)} · ${rewardText({ zen: o.reward.zen, coins: 0, coinType: 'GoblinPoints', items: o.reward.items }, itemName)}`,
    }));
    m.has_owed = m.owed.length > 0;
    this.doc?.update();
  }

  /** The countdowns, every second while the window is open. */
  private tick(): void {
    if (this.wanted && this.timer === null) this.timer = window.setInterval(() => this.fill(), 1000);
    if (!this.wanted && this.timer !== null) { window.clearInterval(this.timer); this.timer = null; }
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
