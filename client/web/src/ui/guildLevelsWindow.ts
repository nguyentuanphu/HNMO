// HNMO's guild levels in the web game: a panel shown next to the guild window (G) with the guild's level, its
// experience bar, this week's ladder points and rank, the player's own part, the buff of the level (also on the buff
// strip, with its name in English or Vietnamese) and the top guilds of the week. It has no desktop window, so it is
// HNMO's own interface document (public/data/Interface/RmlUi/hnmo_guild_levels.rml) in the game's message box frame
// (window_shell). The game asks for the state once when it enters the world (HnmoGuildsRequest, F5 11); the server
// then sends it (HnmoGuildsState, C2 F5 12) when something changes: a level-up and the ladder rewards are told in the
// chat. The panel's button sends HnmoGuildsAction (F5 13).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale, uiScale } from './nativeText.js';
import { tr, rewardText } from './missionsWindow.js';
import { countdown } from './worldBossWindow.js';
import { setHnmoBuff } from '../game/buffs.js';

type CoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';
interface RewardItem { group: number; number: number; level: number; count: number; name?: string }
interface Bonus { level: number; exp: number; zen: number; defense: number }
interface LadderReward { from: number; to: number; coins: number; masterBonus: number; coinType: CoinType; zen: number; items: RewardItem[] }
export interface GuildLevelsState {
  v: number; now: number; inGuild: boolean; guildName?: string; level: number; maxLevel: number;
  xp: number; levelXp: number; nextXp?: number; weekXp: number; rank: number; myWeekXp: number;
  today: number; dailyCap: number; weekEnds: number; buffNameEn: string; buffNameVi: string;
  buff?: Bonus; nextBuff?: Bonus; ladderOn: boolean;
  ladder: { rank: number; name: string; xp: number; level: number }[];
  rewards: LadderReward[];
  owed: { week: string; rank: number; reward: { zen: number; items: RewardItem[] } }[];
  event?: { type: string; result?: string; level?: number; rank?: number; coins?: number; week?: string; inGame?: boolean };
}

export interface GuildLevelsOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The name of an item. */
  itemName: (group: number, number: number) => string;
  /** The hero's buffs changed (the guild buff came or went). */
  buffsChanged?: () => void;
  /** The width the windows docked at the right take, in screen px (the panel stands left of them). */
  dockedWidth?: () => number;
}

interface GuildLevelsModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  loading: boolean; in_guild: boolean; guild_name: string; level_text: string; bar_width: string; xp_text: string;
  rank_text: string; mine_text: string; buff_head: string; buff_text: string; next_text: string;
  ladder_head: string; ladder: { line: string; mine: boolean }[]; rewards_text: string;
  owed_head: string; owed: { line: string }[]; has_owed: boolean;
  loading_label: string; none_label: string; claim_label: string; close_label: string; hint_text: string;
  gl_claim: () => void; gl_close: () => void;
}

/** The buff strip's number of the guild buff (an HNMO buff; it wears the Seal of Wealth's icon). */
export const GUILD_BUFF_NUMBER = 190;
const ACTION = { ClaimRewards: 1 } as const;

function number(value: number): string { return value.toLocaleString(currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU'); }

function coinName(type: CoinType): string {
  return type === 'GoblinPoints' ? tr('Goblin Points') : type === 'WCoinP' ? tr('W Coin (P)') : tr('W Coin (C)');
}

/** "+3% EXP · +5% zen · +10 defense". */
export function bonusText(bonus: Bonus): string {
  const parts: string[] = [];
  if (bonus.exp > 0) parts.push(tr('+%s% EXP', bonus.exp));
  if (bonus.zen > 0) parts.push(tr('+%s% zen from monsters', bonus.zen));
  if (bonus.defense > 0) parts.push(tr('+%d defense', bonus.defense));
  return parts.join(' · ');
}

export class GuildLevelsWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private current: GuildLevelsState | null = null;
  private skew = 0;
  private buffShown = false;
  private readonly model: GuildLevelsModel;

  constructor(private readonly options: GuildLevelsOptions) {
    this.model = {
      has_title: true, title: '', positioned: false, root_x: 0, root_y: 0,
      loading: true, in_guild: false, guild_name: '', level_text: '', bar_width: '0%', xp_text: '', rank_text: '', mine_text: '',
      buff_head: '', buff_text: '', next_text: '', ladder_head: '', ladder: [], rewards_text: '', owed_head: '', owed: [], has_owed: false,
      loading_label: '', none_label: '', claim_label: '', close_label: '', hint_text: '',
      gl_claim: () => this.options.send('HnmoGuildsAction', { Action: ACTION.ClaimRewards, Argument: '' }),
      gl_close: () => this.close(),
    };
    window.addEventListener('resize', () => this.place());
    onLanguageChange(() => { this.updateBuff(); this.fill(); });
  }

  get isOpen(): boolean { return this.wanted; }

  /** The last state from the server. */
  get state(): GuildLevelsState | null { return this.current; }

  /** The document (for tests). */
  get document(): RmlDocument | null { return this.doc; }

  /** The HNMO buffs of the hero for the buff strip. */
  activeBuffs(): number[] { return this.buffShown ? [GUILD_BUFF_NUMBER] : []; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoGuildsRequest', {});
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
    this.options.send('HnmoGuildsRequest', {});
    this.fill();
    if (this.doc) { this.place(); this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_guild_levels', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-guild-levels');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', tr('Guild level'));
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      this.place();
      this.fill();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** A packet from the server: the guild levels state (C2 F5 12). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x12) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoGuildsState', bytes);
    let state: GuildLevelsState;
    try {
      state = JSON.parse(Json) as GuildLevelsState;
    } catch {
      console.warn('Guild levels: unreadable state');
      return true;
    }
    this.current = state;
    this.skew = state.now - Date.now();
    this.model.loading = false;
    if (state.event) this.notice(state, state.event);
    this.updateBuff();
    this.fill();
    return true;
  }

  private buffName(state: GuildLevelsState): string {
    return (currentLanguage() === 'vi' ? state.buffNameVi : state.buffNameEn) || state.buffNameEn || tr('Guild blessing');
  }

  /** The guild buff on the buff strip: its name and bonuses in both languages. */
  private updateBuff(): void {
    const state = this.current;
    const buff = state?.inGuild ? state.buff : undefined;
    const shown = !!buff;
    if (buff && state) {
      const text = bonusText(buff);
      setHnmoBuff(GUILD_BUFF_NUMBER, {
        number: GUILD_BUFF_NUMBER, name: state.buffNameEn, kind: 'buff', iconFrom: 41,
        displayName: { en: `${state.buffNameEn} (${state.guildName ?? ''}, level ${buff.level})`, vi: `${state.buffNameVi || state.buffNameEn} (${state.guildName ?? ''}, cấp ${buff.level})` },
        description: { en: this.inLanguage('en', text), vi: this.inLanguage('vi', text) },
      });
    } else {
      setHnmoBuff(GUILD_BUFF_NUMBER, null);
    }
    if (shown !== this.buffShown) {
      this.buffShown = shown;
      this.options.buffsChanged?.();
    } else if (shown) {
      this.options.buffsChanged?.();
    }
  }

  /** The buff's description in a language (the current one is already translated). */
  private inLanguage(language: string, current: string): string {
    const buff = this.current?.buff;
    if (!buff || language === currentLanguage()) return current;
    const parts: string[] = [];
    const vi = language === 'vi';
    if (buff.exp > 0) parts.push(vi ? `+${buff.exp}% KN` : `+${buff.exp}% EXP`);
    if (buff.zen > 0) parts.push(vi ? `+${buff.zen}% zen từ quái vật` : `+${buff.zen}% zen from monsters`);
    if (buff.defense > 0) parts.push(vi ? `+${buff.defense} phòng thủ` : `+${buff.defense} defense`);
    return parts.join(' · ');
  }

  /** The chat line of an event. */
  private notice(state: GuildLevelsState, event: NonNullable<GuildLevelsState['event']>): void {
    const say = this.options.message;
    switch (event.type) {
      case 'levelup':
        // The server's guild notice (in the player's language) already put it in the chat.
        if (!event.inGame) say(tr('Your guild %s reached level %d!', state.guildName ?? '', event.level ?? state.level));
        if (state.buff) say(tr('Guild buff: %s', bonusText(state.buff)));
        break;
      case 'ladder':
        say(tr('Guild ladder: your guild was rank %d last week. Reward: %s.', event.rank ?? 0,
          (event.coins ?? 0) > 0 ? `${number(event.coins ?? 0)} ${coinName(state.rewards.find((r) => (event.rank ?? 0) >= r.from && (event.rank ?? 0) <= r.to)?.coinType ?? 'GoblinPoints')}` : tr('zen and items')));
        break;
      case 'goods':
        if (event.result === 'BagFull') say(tr('Your bag is full: the guild ladder rewards wait. Make room, then take them in the guild window (G).'));
        else if ((event.coins ?? 0) > 0) say(tr('Guild ladder rewards received.'));
        break;
      case 'siege':
        say(tr('Your guild won the castle siege: guild experience and ladder points for the week!'));
        break;
      case 'claim':
        if (event.result === 'Ok') say(tr('Guild ladder rewards received.'));
        else if (event.result === 'BagFull') say(tr('Your bag is full: the guild ladder rewards wait. Make room, then take them in the guild window (G).'));
        else say(tr('No guild ladder rewards are waiting.'));
        break;
    }
  }

  private fill(): void {
    const m = this.model;
    m.title = tr('Guild level');
    m.loading_label = tr('Loading...');
    m.none_label = tr('Join a guild: the members\' play raises its level, and its level gives the online members a small buff.');
    m.claim_label = tr('Take');
    m.close_label = tr('Close');
    m.buff_head = tr('Guild buff');
    m.ladder_head = tr('Guild ladder this week');
    m.owed_head = tr('Rewards waiting for room in your bag');
    const state = this.current;
    if (!state) { this.doc?.update(); return; }
    const now = Date.now() + this.skew;
    m.in_guild = state.inGuild;
    m.guild_name = state.guildName ?? '';
    m.level_text = tr('Level %d', state.level);
    const span = state.nextXp !== undefined ? Math.max(1, state.nextXp - state.levelXp) : 1;
    const into = state.nextXp !== undefined ? Math.max(0, state.xp - state.levelXp) : 1;
    m.bar_width = `${Math.min(100, Math.round((into / span) * 100))}%`;
    m.xp_text = state.nextXp !== undefined
      ? tr('Guild XP: %s / %s (level %d)', number(state.xp), number(state.nextXp), state.level + 1)
      : tr('Guild XP: %s (top level)', number(state.xp));
    m.rank_text = state.rank > 0
      ? tr('This week: %s points, rank %d. The week ends in %s.', number(state.weekXp), state.rank, countdown(now, state.weekEnds))
      : tr('This week: no points yet. The week ends in %s.', countdown(now, state.weekEnds));
    m.mine_text = state.dailyCap > 0
      ? tr('Your part this week: %s · today %s / %s', number(state.myWeekXp), number(state.today), number(state.dailyCap))
      : tr('Your part this week: %s', number(state.myWeekXp));
    m.buff_text = state.buff ? `${this.buffName(state)}: ${bonusText(state.buff)}` : tr('No buff at this level.');
    m.next_text = state.nextBuff ? tr('Level %d: %s', state.nextBuff.level, bonusText(state.nextBuff)) : '';
    m.ladder = state.ladder.map((l) => ({ line: `${l.rank}. ${l.name} · ${tr('level %d', l.level)} · ${number(l.xp)}`, mine: state.inGuild && l.name === state.guildName }));
    m.rewards_text = state.ladderOn && state.rewards.length > 0
      ? `${tr('Monday rewards for every member')}: ${state.rewards.map((r) => {
        const ranks = r.from === r.to ? `#${r.from}` : `#${r.from}-${r.to}`;
        const parts = [r.coins > 0 ? `${number(r.coins)} ${coinName(r.coinType)}` : '', r.zen > 0 ? tr('%s zen', number(r.zen)) : '', ...r.items.map((i) => `${i.name ?? this.options.itemName(i.group, i.number)} x${i.count}`)].filter((p) => p);
        return `${ranks} ${parts.join(' + ')}`;
      }).join(' · ')}`
      : '';
    m.owed = state.owed.map((o) => ({ line: `${tr('Rank %d', o.rank)} · ${rewardText({ zen: o.reward.zen, coins: 0, coinType: 'GoblinPoints', items: o.reward.items }, this.options.itemName)}` }));
    m.has_owed = m.owed.length > 0;
    m.hint_text = tr('Guild XP comes from monsters, events, world bosses, missions, time in the game and the castle siege.');
    this.doc?.update();
  }

  // A dp window: the window in dp, scaled by the dp ratio (TypographyScale), as the desktop's documents; it stands
  // left of the guild window docked at the right.
  private place(): void {
    const doc = this.doc;
    if (!doc) return;
    const scale = typographyScale();
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0px', top: '0px', width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none', zIndex: '80',
    });
    const panel = doc.$('#panel');
    const docked = this.options.dockedWidth?.() ?? 190 * uiScale();
    if (panel) {
      const left = Math.max(4, (window.innerWidth - docked) / scale - 260 - 8);
      Object.assign(panel.style, { left: `${left}px`, marginLeft: '0px' });
    }
    doc.setScale(scale);
    doc.update();
  }
}
