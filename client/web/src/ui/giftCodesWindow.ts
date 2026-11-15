// HNMO's gift code window in the web game (the community window's "Gift code" button, the touch "more" menu, and the
// chat command /code without a code): a field for a code, the result of the last code, the rewards which wait for room
// in the bag (with Take) and the account's last codes. It has no desktop window, so it is HNMO's own interface document
// (public/data/Interface/RmlUi/hnmo_gift_codes.rml) in the game's message box frame, like the community window.
// The game asks for the state once when it enters the world (HnmoGiftCodesRequest, F5 1A); the server then sends it
// (HnmoGiftCodesState, C2 F5 1B) when something changes: a code entered here or with /code CODE in the chat (the server
// answers that one in the chat itself), waiting rewards given. The buttons send HnmoGiftCodesAction (F5 1C).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale } from './nativeText.js';
import { tr } from './missionsWindow.js';

type CoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';
interface RewardItem { group: number; number: number; level: number; count: number; name?: string }
interface Reward { coins: number; coinType: CoinType; zen: number; items: RewardItem[] }
interface Rules { limit: 'OncePerAccount' | 'OncePerCharacter'; start?: number; end?: number; minLevel: number; maxLevel: number; minResets: number; maxResets: number; classes: number[] }
export type GiftCodeResult = 'Ok' | 'Invalid' | 'TooManyTries' | 'Disabled' | 'NotStarted' | 'Ended' | 'UsedUp' | 'AlreadyUsedAccount'
  | 'AlreadyUsedCharacter' | 'BatchCodeUsed' | 'LevelTooLow' | 'LevelTooHigh' | 'ResetsTooLow' | 'ResetsTooHigh' | 'WrongClass';
export interface GiftCodesState {
  v: number; on: boolean; website: boolean;
  owed: { code: string; zen: number; items: RewardItem[] }[];
  recent: { code: string; at: number; character: string; source: string }[];
  event?: { type: 'redeem' | 'command' | 'goods' | 'claim'; result?: string; code?: string; reward?: Reward; waiting?: boolean; triesLeft?: number; retryAt?: number; details?: Rules };
}

export interface GiftCodesWindowOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The name of an item. */
  itemName: (group: number, number: number) => string;
}

interface GiftCodesModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  intro_text: string; code_input: string; code_placeholder: string; enter_label: string;
  result_text: string; result_ok: boolean;
  owed_head: string; owed: { line: string }[]; has_owed: boolean; take_label: string;
  recent_head: string; recent: { line: string }[]; has_recent: boolean;
  hint_text: string; close_label: string;
  gc_send: () => void; gc_claim: () => void; gc_close: () => void;
}

const ACTION = { Redeem: 1, ClaimRewards: 2 } as const;

function locale(): string { return currentLanguage() === 'vi' ? 'vi-VN' : 'en-AU'; }

function number(value: number): string { return value.toLocaleString(locale()); }

function coinName(type: CoinType): string {
  return type === 'GoblinPoints' ? tr('Goblin Points') : type === 'WCoinP' ? tr('W Coin (P)') : tr('W Coin (C)');
}

/** A time in Sydney time, e.g. "Fri 20 Nov 18:00". */
function sydney(ms: number): string {
  return new Intl.DateTimeFormat(locale(), { timeZone: 'Australia/Sydney', weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hour12: false }).format(ms);
}

/** A time in the player's own time zone. */
function local(ms: number): string {
  return new Date(ms).toLocaleString(locale(), { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hour12: false });
}

export class GiftCodesWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private current: GiftCodesState | null = null;
  private lastResult: { text: string; ok: boolean } | null = null;
  private readonly model: GiftCodesModel;

  constructor(private readonly options: GiftCodesWindowOptions) {
    this.model = {
      has_title: true, title: '', positioned: false, root_x: 0, root_y: 0,
      intro_text: '', code_input: '', code_placeholder: '', enter_label: '', result_text: '', result_ok: false,
      owed_head: '', owed: [], has_owed: false, take_label: '', recent_head: '', recent: [], has_recent: false,
      hint_text: '', close_label: '',
      gc_send: () => this.redeem(this.typed()),
      gc_claim: () => this.options.send('HnmoGiftCodesAction', { Action: ACTION.ClaimRewards, Argument: '' }),
      gc_close: () => this.close(),
    };
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { event.preventDefault(); this.close(); }
    });
    onLanguageChange(() => this.fill());
  }

  get isOpen(): boolean { return this.wanted; }

  /** The last state from the server. */
  get state(): GiftCodesState | null { return this.current; }

  /** The document (for tests). */
  get document(): RmlDocument | null { return this.doc; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoGiftCodesRequest', {});
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
    this.options.send('HnmoGiftCodesRequest', {});
    this.fill();
    if (this.doc) { this.place(); this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_gift_codes', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-gift-codes');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', tr('Gift code'));
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      // Typing goes into the field, not to the game's hotkeys; Enter sends the code.
      doc.root.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') event.stopPropagation();
        if (event.key === 'Enter' && event.target instanceof HTMLInputElement) this.model.gc_send();
      });
      this.place();
      this.fill();
      doc.root.querySelector<HTMLInputElement>('.gc-code-input')?.focus();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** Sends a code (from the field, or the chat's /code without the server's chat command). */
  redeem(code: string): void {
    const text = code.trim();
    if (!text) return;
    this.options.send('HnmoGiftCodesAction', { Action: ACTION.Redeem, Argument: text.slice(0, 32) });
  }

  /** A packet from the server: the gift code state (C2 F5 1B). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x1b) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoGiftCodesState', bytes);
    let state: GiftCodesState;
    try {
      state = JSON.parse(Json) as GiftCodesState;
    } catch {
      console.warn('Gift codes: unreadable state');
      return true;
    }
    this.current = state;
    if (state.event) this.notice(state.event);
    this.fill();
    return true;
  }

  private typed(): string {
    return (this.doc?.root.querySelector<HTMLInputElement>('.gc-code-input')?.value ?? this.model.code_input).trim();
  }

  /** "100 Goblin Points + 50,000 zen + Jewel of Bless x2". */
  private reward(reward: { coins?: number; coinType?: CoinType; zen: number; items: RewardItem[] }): string {
    const parts: string[] = [];
    if (reward.coins && reward.coins > 0) parts.push(`${number(reward.coins)} ${coinName(reward.coinType ?? 'GoblinPoints')}`);
    if (reward.zen > 0) parts.push(tr('%s zen', number(reward.zen)));
    for (const item of reward.items) parts.push(`${item.name ?? this.options.itemName(item.group, item.number)}${item.level > 0 ? ` +${item.level}` : ''} x${item.count}`);
    return parts.join(' + ');
  }

  /** The text of a code's result. */
  resultText(event: NonNullable<GiftCodesState['event']>): string {
    const d = event.details;
    switch (event.result as GiftCodeResult | undefined) {
      case 'Ok': {
        const text = tr('Gift code %s accepted: %s.', event.code ?? '', event.reward ? this.reward(event.reward) : '');
        return event.waiting ? `${text} ${tr('Your bag is full: the items wait. Make room, then take them here.')}` : text;
      }
      case 'Invalid': return tr('That gift code doesn\'t exist (%d tries left).', event.triesLeft ?? 0);
      case 'TooManyTries': return tr('Too many wrong codes. Try again after %s (Sydney time; %s your time).', event.retryAt ? sydney(event.retryAt) : '-', event.retryAt ? local(event.retryAt) : '-');
      case 'Disabled': return tr('This gift code is switched off.');
      case 'NotStarted': return tr('This gift code works from %s (Sydney time).', d?.start ? sydney(d.start) : '-');
      case 'Ended': return tr('This gift code has expired.');
      case 'UsedUp': return tr('This gift code has been used up.');
      case 'AlreadyUsedAccount': return tr('Your account already used this gift code.');
      case 'AlreadyUsedCharacter': return tr('This character already used this gift code.');
      case 'BatchCodeUsed': return tr('This gift code was already used.');
      case 'LevelTooLow': return tr('This gift code needs level %d.', d?.minLevel ?? 0);
      case 'LevelTooHigh': return tr('This gift code is for level %d or lower.', d?.maxLevel ?? 0);
      case 'ResetsTooLow': return tr('This gift code needs %d resets.', d?.minResets ?? 0);
      case 'ResetsTooHigh': return tr('This gift code is for characters with %d resets or fewer.', d?.maxResets ?? 0);
      case 'WrongClass': return tr('This gift code is not for your class.');
      default: return '';
    }
  }

  /** What happened: the window's line and, unless the server said it in the chat already, a chat line. */
  private notice(event: NonNullable<GiftCodesState['event']>): void {
    const say = this.options.message;
    switch (event.type) {
      case 'redeem':
      case 'command': {
        const text = this.resultText(event);
        this.lastResult = { text, ok: event.result === 'Ok' };
        if (event.result === 'Ok') this.model.code_input = '';
        // /code CODE is answered by the server in the chat.
        if (event.type === 'redeem' && text) say(`[${tr('Gift code')}] ${text}`);
        break;
      }
      case 'goods':
        // The server tells a given reward in the chat; a full bag only here.
        if (event.result === 'BagFull') say(tr('Your bag is full: the gift code rewards wait. Make room, then take them in the gift code window.'));
        break;
      case 'claim':
        if (event.result === 'Ok') say(tr('The waiting gift code rewards are in your bag.'));
        else if (event.result === 'BagFull') say(tr('Your bag is full: the gift code rewards wait. Make room, then take them in the gift code window.'));
        else say(tr('No gift code rewards are waiting.'));
        break;
    }
  }

  private fill(): void {
    const m = this.model;
    m.title = tr('Gift code');
    m.intro_text = tr('Enter a gift code: the coins go to your account, zen and items to this character.');
    m.code_placeholder = tr('Gift code');
    m.enter_label = tr('Use code');
    m.owed_head = tr('Rewards waiting for room in your bag');
    m.take_label = tr('Take');
    m.recent_head = tr('Your last codes');
    m.close_label = tr('Close');
    const state = this.current;
    m.hint_text = state?.website === false
      ? tr('You can also type /code CODE in the chat.')
      : tr('You can also type /code CODE in the chat, or enter codes on the website\'s account page.');
    m.result_text = this.lastResult?.text ?? '';
    m.result_ok = this.lastResult?.ok ?? false;
    m.owed = (state?.owed ?? []).map((o) => ({ line: `${o.code} · ${this.reward(o)}` }));
    m.has_owed = m.owed.length > 0;
    m.recent = (state?.recent ?? []).map((r) => ({
      line: `${r.code} · ${local(r.at)} · ${r.source === 'website' ? tr('website: %s', r.character) : r.character}`,
    }));
    m.has_recent = m.recent.length > 0;
    this.doc?.update();
  }

  private place(): void {
    const doc = this.doc;
    if (!doc) return;
    const scale = typographyScale();
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0px', top: '0px', width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none', zIndex: '81',
    });
    doc.setScale(scale);
    doc.update();
  }
}
