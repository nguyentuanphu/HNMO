// HNMO's community window in the web game (K, when the inventory isn't open; also in the touch "more" menu): the
// one-time coins for linking the account to Discord (a link code from the server, typed as /link CODE in the Discord
// server, where the bot only answers members) and for joining the Zalo group (a claim code posted in the group, which
// the admin approves in the admin panel: Zalo has no open bot API for groups), the community events with a field for
// their code word, the rewards which wait for room in the bag, and the screenshot button. It has no desktop window, so
// it is HNMO's own interface document (public/data/Interface/RmlUi/hnmo_community.rml) in the game's message box frame.
// The game asks for the state once when it enters the world (HnmoCommunityRequest, F5 14); the server then sends it
// (HnmoCommunityState, C2 F5 15) when something changes. The buttons send HnmoCommunityAction (F5 16); the Discord link
// code comes from the Discord integration's own messages (DiscordLinkCodeRequest F5 04, DiscordLinkCode F5 05).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { coinRewardsShown } from './coinRewards.js';
import { typographyScale } from './nativeText.js';
import { tr } from './missionsWindow.js';
import { communityLinks } from '../gameName.js';

type CoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';
interface RewardItem { group: number; number: number; level: number; count: number; name?: string }
interface Reward { coins: number; coinType: CoinType; zen: number; items: RewardItem[] }
type Phase = 'Upcoming' | 'Open' | 'Ended';
export interface CommunityEvent {
  id: string; titleEn: string; titleVi: string; descriptionEn: string; descriptionVi: string;
  start?: number; end?: number; phase: Phase; hasCode: boolean; entered: boolean; winner: boolean; reward: Reward;
}
export interface CommunityState {
  v: number; now: number;
  discord: { on: boolean; coins: number; coinType: CoinType; linkAvailable: boolean; linked: boolean; paid: boolean };
  zalo: { on: boolean; coins: number; coinType: CoinType; status: 'None' | 'Pending' | 'Approved' | 'Rejected'; code?: string; name?: string };
  events: CommunityEvent[];
  owed: { eventId: string; titleEn: string; titleVi: string; reward: { zen: number; items: RewardItem[] } }[];
  event?: { type: string; result?: string; coins?: number; eventId?: string };
}

export interface CommunityWindowOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The name of an item. */
  itemName: (group: number, number: number) => string;
  /** Opens the screenshot / share panel. */
  share?: () => void;
}

interface CommunityModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  loading: boolean; off: boolean; off_label: string;
  discord_on: boolean; discord_head: string; discord_text: string; discord_done: boolean; discord_code: string;
  can_link: boolean; can_claim_discord: boolean; has_discord_url: boolean;
  zalo_on: boolean; zalo_head: string; zalo_text: string; zalo_done: boolean; zalo_code: string; can_zalo: boolean;
  zalo_input: string; zalo_placeholder: string; has_zalo_url: boolean;
  events_head: string; no_events_label: string; events: { title: string; description: string; when: string; status: string; done: boolean }[];
  has_code_events: boolean; code_input: string; code_placeholder: string;
  owed_head: string; owed: { line: string }[]; has_owed: boolean;
  loading_label: string; link_label: string; take_label: string; claim_label: string; enter_label: string;
  open_discord_label: string; open_zalo_label: string; share_label: string; gift_label: string; close_label: string; hint_text: string;
  cm_link: () => void; cm_discord: () => void; cm_zalo: () => void; cm_code: () => void; cm_claim: () => void;
  cm_open_discord: () => void; cm_open_zalo: () => void; cm_share: () => void; cm_gift: () => void; cm_close: () => void;
}

const ACTION = { ClaimDiscord: 1, ClaimZalo: 2, EnterCode: 3, ClaimRewards: 4 } as const;

function number(value: number): string { return value.toLocaleString(currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU'); }

function coinName(type: CoinType): string {
  return type === 'GoblinPoints' ? tr('Goblin Points') : type === 'WCoinP' ? tr('W Coin (P)') : tr('W Coin (C)');
}

/** A date and time in the player's own time zone, e.g. "Fri 20 Nov 18:00". */
function when(ms: number): string {
  return new Date(ms).toLocaleString(currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
}

function openLink(url: string | undefined): void {
  if (url) window.open(url, '_blank', 'noopener,noreferrer');
}

export class CommunityWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private current: CommunityState | null = null;
  private linkCode = '';
  private discordClaimSent = false;
  private readonly model: CommunityModel;

  constructor(private readonly options: CommunityWindowOptions) {
    this.model = {
      has_title: true, title: '', positioned: false, root_x: 0, root_y: 0,
      loading: true, off: false, off_label: '',
      discord_on: false, discord_head: '', discord_text: '', discord_done: false, discord_code: '', can_link: false, can_claim_discord: false, has_discord_url: false,
      zalo_on: false, zalo_head: '', zalo_text: '', zalo_done: false, zalo_code: '', can_zalo: false, zalo_input: '', zalo_placeholder: '', has_zalo_url: false,
      events_head: '', no_events_label: '', events: [], has_code_events: false, code_input: '', code_placeholder: '',
      owed_head: '', owed: [], has_owed: false,
      loading_label: '', link_label: '', take_label: '', claim_label: '', enter_label: '', open_discord_label: '', open_zalo_label: '',
      share_label: '', gift_label: '', close_label: '', hint_text: '',
      cm_link: () => this.options.send('DiscordLinkCodeRequest', {}),
      cm_discord: () => this.action(ACTION.ClaimDiscord, ''),
      cm_zalo: () => this.action(ACTION.ClaimZalo, this.typed('.cm-zalo-input', this.model.zalo_input)),
      cm_code: () => {
        const code = this.typed('.cm-code-input', this.model.code_input);
        if (code) this.action(ACTION.EnterCode, code);
      },
      cm_claim: () => this.action(ACTION.ClaimRewards, ''),
      cm_open_discord: () => openLink(communityLinks().discord),
      cm_open_zalo: () => openLink(communityLinks().zalo),
      cm_share: () => this.options.share?.(),
      // HNMO's gift code window (ui/giftCodesWindow.ts).
      cm_gift: () => window.dispatchEvent(new Event('hnmo-gift-codes-open')),
      cm_close: () => this.close(),
    };
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { event.preventDefault(); this.close(); }
    });
    onLanguageChange(() => this.fill());
  }

  get isOpen(): boolean { return this.wanted; }

  /** The last state from the server. */
  get state(): CommunityState | null { return this.current; }

  /** The document (for tests). */
  get document(): RmlDocument | null { return this.doc; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoCommunityRequest', {});
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
    this.options.send('HnmoCommunityRequest', {});
    this.fill();
    if (this.doc) { this.place(); this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_community', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-community');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', tr('Community'));
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      // Typing goes into the fields, not to the game's hotkeys.
      doc.root.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') event.stopPropagation();
        if (event.key === 'Enter' && event.target instanceof HTMLInputElement) {
          if (event.target.classList.contains('cm-code-input')) this.model.cm_code(); else this.model.cm_zalo();
        }
      });
      this.place();
      this.fill();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** A packet from the server: the community state (C2 F5 15) or a Discord link code (C1 F5 05). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] === 0xc1 && bytes[2] === 0xf5 && bytes[3] === 0x05) return this.handleLinkCode(bytes);
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x15) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoCommunityState', bytes);
    let state: CommunityState;
    try {
      state = JSON.parse(Json) as CommunityState;
    } catch {
      console.warn('Community: unreadable state');
      return true;
    }
    this.current = state;
    this.model.loading = false;
    if (state.event) this.notice(state, state.event);
    // Linked with the bot while the window was open: the coins come without another click.
    if (state.discord.on && state.discord.linked && !state.discord.paid && !this.discordClaimSent) {
      this.discordClaimSent = true;
      this.action(ACTION.ClaimDiscord, '');
    }
    this.fill();
    return true;
  }

  private handleLinkCode(bytes: Uint8Array): boolean {
    const { Result, LinkCode, ValidMinutes } = read<{ Result: number | string; LinkCode: string; ValidMinutes: number }>('ServerToClient', 'DiscordLinkCode', bytes);
    const result = typeof Result === 'number' ? Result : Result === 'Success' ? 0 : Result === 'TooSoon' ? 2 : 1;
    if (result === 0 && LinkCode) {
      this.linkCode = LinkCode;
      this.options.message(tr('In our Discord server, type /link %s (valid for %d minutes).', LinkCode, ValidMinutes));
    } else if (result === 2) {
      this.options.message(tr('A link code was made a moment ago; wait a little before asking again.'));
    } else {
      this.options.message(tr('Linking to Discord isn\'t available on this server.'));
    }
    this.fill();
    return true;
  }

  private typed(selector: string, fallback: string): string {
    return (this.doc?.root.querySelector<HTMLInputElement>(selector)?.value ?? fallback).trim();
  }

  private action(action: number, argument: string): void {
    this.options.send('HnmoCommunityAction', { Action: action, Argument: argument });
  }

  private title(item: { titleEn: string; titleVi: string }): string {
    return (currentLanguage() === 'vi' ? item.titleVi : item.titleEn) || item.titleEn || item.titleVi;
  }

  private reward(reward: Reward): string {
    const parts: string[] = [];
    if (reward.coins > 0 && coinRewardsShown()) parts.push(`${number(reward.coins)} ${coinName(reward.coinType)}`);
    parts.push(...this.goods(reward));
    return parts.join(' + ');
  }

  /** "1,000 zen", "Jewel of Bless x2". */
  private goods(reward: { zen: number; items: RewardItem[] }): string[] {
    const parts: string[] = [];
    if (reward.zen > 0) parts.push(tr('%s zen', number(reward.zen)));
    for (const item of reward.items) parts.push(`${item.name ?? this.options.itemName(item.group, item.number)}${item.level > 0 ? ` +${item.level}` : ''} x${item.count}`);
    return parts;
  }

  /** The chat line of an event. */
  private notice(state: CommunityState, event: NonNullable<CommunityState['event']>): void {
    const say = this.options.message;
    const coins = (n: number, type: CoinType) => `${number(n)} ${coinName(type)}`;
    const named = state.events.find((e) => e.id === event.eventId);
    switch (event.type) {
      case 'discord':
        if (event.result === 'Ok') say(tr('Thanks for joining our Discord: %s received.', coins(event.coins ?? state.discord.coins, state.discord.coinType)));
        else if (event.result === 'NotLinked') say(tr('Your account isn\'t linked to Discord yet: get a link code and type /link CODE in our Discord server.'));
        else if (event.result === 'DiscordUserUsed') say(tr('This Discord user already got the coins for another account.'));
        else if (event.result === 'AlreadyPaid') say(tr('You already got the Discord coins.'));
        break;
      case 'zalo':
        if (event.result === 'Ok' || event.result === 'Pending') say(tr('Post the code %s in our Zalo group (or send it to the admin). The coins come when the admin approves it.', state.zalo.code ?? ''));
        else if (event.result === 'Paid') say(tr('Your Zalo claim was approved: %s received.', coins(event.coins ?? state.zalo.coins, state.zalo.coinType)));
        else if (event.result === 'AlreadyPaid') say(tr('You already got the Zalo coins.'));
        break;
      case 'code':
        if (event.result === 'Ok') say(tr('Code accepted: %s. Reward: %s.', named ? this.title(named) : '', named ? this.reward(named.reward) : ''));
        else if (event.result === 'AlreadyEntered') say(tr('Your account already entered this code.'));
        else if (event.result === 'NotStarted') say(tr('This event hasn\'t started yet.'));
        else if (event.result === 'Ended') say(tr('This event is over.'));
        else say(tr('Unknown code.'));
        break;
      case 'winner':
        say(tr('You are a winner of the community event %s: %s received.', named ? this.title(named) : (event.eventId ?? ''), coins(event.coins ?? 0, named?.reward.coinType ?? 'GoblinPoints')));
        break;
      case 'goods':
      case 'claim':
        if (event.result === 'Ok') say(tr('Community event rewards received.'));
        else if (event.result === 'BagFull') say(tr('Your bag is full: the community event rewards wait. Make room, then take them in the community window (K).'));
        else if (event.type === 'claim') say(tr('No community event rewards are waiting.'));
        break;
    }
  }

  private fill(): void {
    const m = this.model;
    const links = communityLinks();
    m.title = tr('Community');
    m.loading_label = tr('Loading...');
    m.off_label = tr('There are no community rewards on this server right now.');
    m.discord_head = 'Discord';
    m.zalo_head = 'Zalo';
    m.events_head = tr('Community events');
    m.no_events_label = tr('No community events right now.');
    m.owed_head = tr('Rewards waiting for room in your bag');
    m.link_label = tr('Link code');
    m.take_label = tr('Take');
    m.claim_label = tr('Claim');
    m.enter_label = tr('Enter');
    m.open_discord_label = tr('Open Discord');
    m.open_zalo_label = tr('Open Zalo');
    m.share_label = tr('Screenshot');
    m.gift_label = tr('Gift code');
    m.close_label = tr('Close');
    m.zalo_placeholder = tr('Your name in Zalo');
    m.code_placeholder = tr('Event code');
    m.hint_text = tr('Print Screen saves a screenshot; the Screenshot button can share it.');
    m.has_discord_url = Boolean(links.discord);
    m.has_zalo_url = Boolean(links.zalo);
    const state = this.current;
    if (!state) { this.doc?.update(); return; }
    const d = state.discord;
    // Discord and Zalo only give coins: none on a season without a cash shop (classic play).
    m.discord_on = d.on && coinRewardsShown();
    m.discord_done = d.paid;
    m.discord_code = !d.paid && !d.linked ? this.linkCode : '';
    m.can_link = d.on && !d.paid && !d.linked && d.linkAvailable;
    m.can_claim_discord = d.on && !d.paid && d.linked;
    m.discord_text = d.paid
      ? tr('Discord linked: you got %s.', `${number(d.coins)} ${coinName(d.coinType)}`)
      : d.linked
        ? tr('Discord linked: take %s.', `${number(d.coins)} ${coinName(d.coinType)}`)
        : d.linkAvailable
          ? tr('Join our Discord and link your account: %s once. Get a link code, then type /link CODE in the Discord server.', `${number(d.coins)} ${coinName(d.coinType)}`)
          : tr('Join our Discord and link your account: %s once (the Discord bot isn\'t set up yet).', `${number(d.coins)} ${coinName(d.coinType)}`);
    const z = state.zalo;
    m.zalo_on = z.on && coinRewardsShown();
    m.zalo_done = z.status === 'Approved';
    m.zalo_code = z.status === 'Pending' ? z.code ?? '' : '';
    m.can_zalo = z.on && (z.status === 'None' || z.status === 'Rejected');
    const zaloCoins = `${number(z.coins)} ${coinName(z.coinType)}`;
    m.zalo_text = z.status === 'Approved'
      ? tr('Zalo group joined: you got %s.', zaloCoins)
      : z.status === 'Pending'
        ? tr('Post this code in our Zalo group (or send it to the admin); the admin approves it, then %s come.', zaloCoins)
        : z.status === 'Rejected'
          ? tr('Your Zalo claim wasn\'t approved. Join the group, then claim again: %s once.', zaloCoins)
          : tr('Join our Zalo group, then claim %s once: you get a code to post in the group.', zaloCoins);
    m.off = !d.on && !z.on && state.events.length === 0;
    m.events = state.events.map((e) => {
      const description = (currentLanguage() === 'vi' ? e.descriptionVi : e.descriptionEn) || e.descriptionEn || '';
      const span = e.start !== undefined && e.end !== undefined ? `${when(e.start)} – ${when(e.end)}`
        : e.end !== undefined ? tr('Until %s', when(e.end)) : e.start !== undefined ? tr('From %s', when(e.start)) : '';
      const reward = this.reward(e.reward);
      const done = e.entered || e.winner;
      const status = e.winner ? tr('You are a winner!') : e.entered ? tr('Code entered.')
        : e.phase === 'Upcoming' ? tr('Starts later.') : e.phase === 'Ended' ? tr('Over.') : e.hasCode ? tr('Enter the event code below.') : tr('Winners are chosen by the admin.');
      return { title: this.title(e), description, when: [span, reward ? tr('Reward: %s', reward) : ''].filter((p) => p).join(' · '), status, done };
    });
    m.has_code_events = state.events.some((e) => e.hasCode && e.phase === 'Open' && !e.entered);
    m.owed = state.owed.map((o) => ({ line: `${this.title(o)} · ${this.goods(o.reward).join(' + ')}` }));
    m.has_owed = m.owed.length > 0;
    this.doc?.update();
  }

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
