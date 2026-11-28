// HNMO's missions window (J): the daily and weekly missions with their progress and claim buttons, the daily
// streak, the monthly login calendar, the earned coins of the day and the invite code. It has no desktop window,
// so it is HNMO's own interface document (public/data/Interface/RmlUi/hnmo_missions.rml) in the game's message
// box frame (window_shell). The server sends the state as JSON (HnmoMissionsState, C2 F5 09) when the window asks
// for it (HnmoMissionsRequest, F5 08) and whenever something changes afterwards (a mission is finished, coins are
// given); the buttons send HnmoMissionsAction (F5 0A). A finished mission and the result of a claim are told in
// the chat, also while the window is closed.

import { currentLanguage, onLanguageChange, translate } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale } from './nativeText.js';
import { coinRewardsShown } from './coinRewards.js';

type CoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';
interface RewardItem { group: number; number: number; level: number; count: number }
interface Reward { zen: number; coins: number; coinType: CoinType; items: RewardItem[] }
export interface MissionState {
  id: string; period: 'daily' | 'weekly'; kind: string; titleEn: string; titleVi: string;
  value: number; target: number; claimed: boolean; reward: Reward;
}
export interface MissionBoard {
  v: number; now: number; day: string; week: string; nextDaily: number; nextWeekly: number;
  missions: MissionState[];
  streak: { days: number; todayCounted: boolean; required: number; bonusDay: number; bonusClaimable: boolean; bonus: Reward };
  calendar: { enabled: boolean; month: string; claimedDays: number; claimedToday: boolean; daysInMonth: number; coinType: CoinType; rewards: number[] };
  online: { enabled: boolean; coinsToday: number; cap: number; minutesToday: number; coinsPerHour: number; coinType: CoinType; autoPlayHalf: boolean };
  earned: { today: number; cap: number; owed: number };
  milestones: { enabled: boolean; coinType: CoinType; levels: { level: number; coins: number }[]; coinsPerReset: number };
  invite: { enabled: boolean; code?: string; invitedBy?: string; rewarded: boolean; requiredLevel: number; inviterCoins: number; invitedCoins: number; coinType: CoinType };
  event?: { type: string; result?: string; ids?: string[]; coins?: number; text?: string };
}

export interface MissionsWindowOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The name of an item. */
  itemName: (group: number, number: number) => string;
}

type Tab = 'daily' | 'weekly' | 'coins';
interface Row { id: string; title: string; reward: string; width: string; count: string; done: boolean; claimed: boolean; claimable: boolean; state: string }
interface Day { label: string; coins: string; claimed: boolean; next: boolean; big: boolean }

interface MissionsModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  tab: Tab; loading: boolean; rows: Row[]; days: Day[];
  daily_label: string; weekly_label: string; coins_label: string; loading_label: string; empty_label: string;
  claim_label: string; close_label: string; reset_text: string;
  streak_head: string; streak_text: string; streak_claimable: boolean;
  calendar_on: boolean; calendar_head: string; calendar_text: string; calendar_claimable: boolean;
  earned_head: string; online_on: boolean; online_text: string; earned_text: string; milestones_on: boolean; milestones_text: string;
  invite_on: boolean; invite_head: string; invite_text: string; invite_code: string; can_invite: boolean;
  invite_input: string; invite_placeholder: string; invite_button: string;
  mi_tab: (tab: Tab) => void; mi_claim: (id: string) => void; mi_streak: () => void; mi_calendar: () => void;
  mi_invite: () => void; mi_close: () => void;
}

/** Action numbers of HnmoMissionsAction. */
const ACTION = { ClaimMission: 1, ClaimStreak: 2, ClaimCalendar: 3, EnterInvite: 4 } as const;

/** The English text in the game's language, with its %d / %s filled in. */
export function tr(english: string, ...values: (string | number)[]): string {
  const template = translate(english) ?? english;
  let index = 0;
  return template.replace(/%[ds]/g, () => String(values[index++] ?? ''));
}

function coinName(type: CoinType): string {
  return type === 'GoblinPoints' ? tr('Goblin Points') : type === 'WCoinP' ? tr('W Coin (P)') : tr('W Coin (C)');
}

function number(value: number): string {
  return value.toLocaleString(currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU');
}

/** The rewards as one line: "500,000 zen · 10 Goblin Points · Jewel of Bless x1". */
export function rewardText(reward: Reward, itemName: (group: number, number: number) => string): string {
  const parts: string[] = [];
  if (reward.zen > 0) parts.push(tr('%s zen', number(reward.zen)));
  if (reward.coins > 0 && coinRewardsShown()) parts.push(`${number(reward.coins)} ${coinName(reward.coinType)}`);
  for (const item of reward.items) parts.push(`${itemName(item.group, item.number)}${item.level > 0 ? ` +${item.level}` : ''} x${item.count}`);
  return parts.length > 0 ? `${tr('Reward')}: ${parts.join(' · ')}` : '';
}

function untilText(now: number, at: number): string {
  const minutes = Math.max(0, Math.ceil((at - now) / 60000));
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  return days > 0 ? tr('%d d %d h', days, hours) : tr('%d h %d min', hours, minutes % 60);
}

export class MissionsWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private board: MissionBoard | null = null;
  private readonly model: MissionsModel;

  constructor(private readonly options: MissionsWindowOptions) {
    this.model = {
      has_title: true, title: 'Missions', positioned: false, root_x: 0, root_y: 0,
      tab: 'daily', loading: true, rows: [], days: [],
      daily_label: '', weekly_label: '', coins_label: '', loading_label: '', empty_label: '', claim_label: '', close_label: '', reset_text: '',
      streak_head: '', streak_text: '', streak_claimable: false,
      calendar_on: false, calendar_head: '', calendar_text: '', calendar_claimable: false,
      earned_head: '', online_on: false, online_text: '', earned_text: '', milestones_on: false, milestones_text: '',
      invite_on: false, invite_head: '', invite_text: '', invite_code: '', can_invite: false,
      invite_input: '', invite_placeholder: '', invite_button: '',
      mi_tab: (tab) => { this.model.tab = tab; this.fill(); },
      mi_claim: (id) => this.action(ACTION.ClaimMission, id),
      mi_streak: () => this.action(ACTION.ClaimStreak, ''),
      mi_calendar: () => this.action(ACTION.ClaimCalendar, ''),
      mi_invite: () => {
        const typed = (this.doc?.root.querySelector<HTMLInputElement>('.mi-invite-input')?.value ?? this.model.invite_input).trim();
        if (typed) this.action(ACTION.EnterInvite, typed);
      },
      mi_close: () => this.close(),
    };
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { event.preventDefault(); this.close(); }
    });
    onLanguageChange(() => this.fill());
  }

  get isOpen(): boolean { return this.wanted; }

  /** The last state from the server. */
  get state(): MissionBoard | null { return this.board; }

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
    this.options.send('HnmoMissionsRequest', {});
    this.fill();
    if (this.doc) { this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_missions', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-missions');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', tr('Missions'));
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      // Typing goes into the invite field, not to the game's hotkeys.
      doc.root.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') event.stopPropagation();
        if (event.key === 'Enter' && event.target instanceof HTMLInputElement) this.model.mi_invite();
      });
      this.place();
      this.fill();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** A packet from the server: the missions state (C2 F5 09). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x09) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoMissionsState', bytes);
    let board: MissionBoard;
    try {
      board = JSON.parse(Json) as MissionBoard;
    } catch {
      console.warn('Missions: unreadable state');
      return true;
    }
    this.board = board;
    this.model.loading = false;
    if (board.event) this.notice(board, board.event);
    this.fill();
    return true;
  }

  private action(action: number, argument: string): void {
    this.options.send('HnmoMissionsAction', { Action: action, Argument: argument });
  }

  private title(mission: MissionState): string {
    return (currentLanguage() === 'vi' ? mission.titleVi : mission.titleEn) || mission.titleEn || mission.id;
  }

  /** The chat line of an event. */
  private notice(board: MissionBoard, event: NonNullable<MissionBoard['event']>): void {
    const say = this.options.message;
    const mission = (id: string | undefined) => board.missions.find((m) => m.id === id);
    switch (event.type) {
      case 'finished':
        for (const id of event.ids ?? []) {
          const m = mission(id);
          if (m) say(tr('Mission complete: %s. Claim the reward in the missions window (J).', this.title(m)));
        }
        break;
      case 'claim': {
        const m = mission(event.ids?.[0]);
        const name = m ? this.title(m) : '';
        if (event.result === 'Ok') say(tr('Reward of "%s" received.', name));
        else if (event.result === 'BagFull') say(tr('Your bag is full: make room, then claim "%s" again. It stays claimable.', name));
        else if (event.result === 'ZenFull') say(tr('You can\'t carry more zen: claim "%s" again later. It stays claimable.', name));
        else if (event.result === 'AlreadyClaimed') say(tr('You already claimed this reward.'));
        else if (event.result === 'NotFinished') say(tr('This mission isn\'t finished yet.'));
        break;
      }
      case 'streak':
        if (event.result === 'Ok') say(tr('Streak bonus received.'));
        else if (event.result === 'BagFull') say(tr('Your bag is full: make room, then claim the streak bonus again.'));
        break;
      case 'calendar':
        if (event.result === 'Ok' && event.text === 'capped') say(tr('Login calendar: today is claimed; its coins come tomorrow (today\'s cap is reached).'));
        else if (event.result === 'Ok') say(tr('Login calendar: %d %s received.', event.coins ?? 0, coinName(board.calendar.coinType)));
        else if (event.result === 'AlreadyClaimed') say(tr('You already claimed today\'s login reward.'));
        break;
      case 'invite': {
        const texts: Record<string, string> = {
          Ok: 'Invite saved. Your friend gets the coins when one of your characters reaches the level.',
          NotFound: 'No account has that invite code or character name.',
          Self: 'You can\'t invite yourself.',
          AlreadyInvited: 'You already entered who invited you.',
          SameAddress: 'Invites from the same network don\'t count.',
          TooLate: 'Your account is already past the invite level.',
          Disabled: 'Invites are off.',
        };
        say(tr(texts[event.result ?? ''] ?? texts.NotFound));
        break;
      }
      case 'coins':
        if ((event.coins ?? 0) > 0) say(currentLanguage() === 'en' && event.text ? tr('You earned %d coins: %s.', event.coins ?? 0, event.text) : tr('You earned %d coins.', event.coins ?? 0));
        break;
    }
  }

  private fill(): void {
    const m = this.model;
    m.title = tr('Missions');
    m.daily_label = tr('Daily');
    m.weekly_label = tr('Weekly');
    m.coins_label = tr('Coins');
    m.loading_label = tr('Loading...');
    m.empty_label = tr('No missions right now.');
    m.claim_label = tr('Claim');
    m.close_label = tr('Close');
    m.streak_head = tr('Daily streak');
    m.calendar_head = tr('Login calendar');
    m.earned_head = tr('Earned coins today');
    m.invite_head = tr('Invite a friend');
    m.invite_placeholder = tr('Friend\'s code or character name');
    m.invite_button = tr('Send code');
    const board = this.board;
    if (!board) { this.doc?.update(); return; }
    const itemName = this.options.itemName;
    if (m.tab !== 'coins') {
      m.rows = board.missions.filter((mission) => mission.period === m.tab).map((mission) => {
        const done = mission.value >= mission.target;
        return {
          id: mission.id,
          title: this.title(mission),
          reward: rewardText(mission.reward, itemName),
          width: `${Math.min(100, Math.round((mission.value / Math.max(1, mission.target)) * 100))}%`,
          count: `${number(mission.value)} / ${number(mission.target)}`,
          done, claimed: mission.claimed, claimable: done && !mission.claimed,
          state: mission.claimed ? tr('Claimed') : tr('In progress'),
        };
      });
      m.reset_text = tr('Starts again in %s', untilText(Date.now(), m.tab === 'daily' ? board.nextDaily : board.nextWeekly));
    }
    const s = board.streak;
    m.streak_text = s.required > 0
      ? `${tr('%d days in a row (finish %d daily missions a day).', s.days, s.required)} ${tr('Every %d days: %s', s.bonusDay, rewardText(s.bonus, itemName))}`
      : tr('There are no daily missions.');
    m.streak_claimable = s.bonusClaimable;
    const c = board.calendar;
    m.calendar_on = c.enabled;
    m.calendar_text = c.claimedToday ? tr('Today is claimed. Days this month: %d.', c.claimedDays) : tr('Claim today\'s reward. Days this month: %d.', c.claimedDays);
    m.calendar_claimable = !c.claimedToday;
    m.days = c.rewards.map((coins, index) => ({
      label: tr('Day %d', index + 1), coins: String(coins), claimed: index < c.claimedDays,
      next: !c.claimedToday && index === c.claimedDays, big: coins > (c.rewards[0] ?? 0),
    }));
    const o = board.online;
    m.online_on = o.enabled;
    // Coins for online time and invites: none on a season without a cash shop (classic play).
    m.online_text = coinRewardsShown() ? tr('Online time: %d / %d %s today (%d per hour%s).', o.coinsToday, o.cap, coinName(o.coinType), o.coinsPerHour, o.autoPlayHalf ? tr(', auto play counts half') : '') : '';
    m.earned_text = tr('All earned coins today: %d / %d.', board.earned.today, board.earned.cap) + (board.earned.owed > 0 ? ` ${tr('%d more come on the next days.', board.earned.owed)}` : '');
    m.milestones_on = board.milestones.enabled;
    m.milestones_text = tr('Levels %s and each reset (%d) pay once per character.', board.milestones.levels.map((l) => `${l.level} (${l.coins})`).join(', '), board.milestones.coinsPerReset);
    const i = board.invite;
    m.invite_on = i.enabled;
    m.invite_text = coinRewardsShown() ? tr('When a friend you invited reaches level %d, you get %d %s (and the friend %d).', i.requiredLevel, i.inviterCoins, coinName(i.coinType), i.invitedCoins) : '';
    m.invite_code = `${tr('Your code')}: ${i.code ?? '-'}${i.invitedBy ? ` · ${tr('Invited by')}: ${i.invitedBy}` : ''}`;
    m.can_invite = !i.invitedBy;
    this.doc?.update();
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
