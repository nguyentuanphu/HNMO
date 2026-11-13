// Trading with another player, as the desktop client's CTrade (Trade.cpp) and its packet handlers
// (WSclient.cpp ReceiveTrade*): asking a player to trade, answering a request, both sides' items, the
// zen each puts in, the OK buttons and how the trade ends. Own items go in and out through the
// inventory (ItemMoveRequestExtended to the trade storage); the partner's come as their own packets.
// The items a trade brings arrive with the inventory list the server sends after it.

import { BAG, NpcWindow, Storage, TRADE, decodeItem } from '../game/inventory.js';
import type { Inventory, Item } from '../game/inventory.js';
import { itemDefinition } from '../game/items.js';
import type { Connection } from '../net/connection.js';
import type { ItemWindows, TradeView } from './itemWindows.js';

/** The character level from which the trade command works (TRADELIMITLEVEL). */
export const TRADE_MIN_LEVEL = 6;
/** How long the own OK button waits after the offer changed (MyTradeWaitAfterChange: 150 frames at 25 per second). */
const TRADE_WAIT_MS = 6000;

const ButtonState = { UNCHECKED: 0, CHECKED: 1, RED: 2 } as const;
type ButtonState = (typeof ButtonState)[keyof typeof ButtonState];

// The desktop's item type (group * 512 + number) and the types the trade rules name.
const type = (item: Pick<Item, 'group' | 'number'>): number => item.group * 512 + item.number;
const WING = 12 * 512, HELPER = 13 * 512, POTION = 14 * 512;
const ITEM_RENA = POTION + 21, ITEM_BOX_OF_LUCK = POTION + 11, ITEM_WIZARDS_RING = HELPER + 20;

// GameLogic::Items::IsTradeBan: the Sign of Lord can always be traded, the Heart of Dark Lord and the
// Wizard's Ring above +0 never; otherwise the item data's "tradable" flag.
export function isTradeBan(item: Item): boolean {
  const t = type(item);
  if (t === ITEM_RENA && item.level === 3) return false;
  if ((t === ITEM_BOX_OF_LUCK && item.level === 13) || (t === ITEM_WIZARDS_RING && item.level !== 0)) return true;
  return itemDefinition(item.group, item.number)?.tradable === false;
}

// CTrade::BackUpYourInven: the partner's items worth remembering when they leave the trade, so a
// changed item offered again is flagged (helpers, jewels, wings, capes, +5 or better, excellent).
function worthRemembering(item: Item): boolean {
  const t = type(item);
  return (t >= HELPER && t <= HELPER + 4) || t === POTION + 13 || t === POTION + 14 || t === POTION + 16
    || t >= POTION + 31 || t === WING + 30 || t === WING + 31 || (t >= WING + 136 && t <= WING + 143)
    || (t >= WING && t <= WING + 6) || t >= HELPER + 30 || (t >= WING + 36 && t <= WING + 43) || t === WING + 15
    || (t >= WING + 49 && t <= WING + 50) || (item.level > 4 && t < WING) || item.excellent > 0;
}

// The number of an item's options (the desktop's SpecialNum): skill, luck and the extra option.
const optionCount = (item: Item): number => Number(item.skill) + Number(item.luck) + Number(item.optionLevel > 0);

// CompareItem: 2 for another type; else -1 when the first is worse, 1 when it's better, 0 when the same
// (level, skill, excellent options, options, the extra option's level, then durability).
export function compareTradeItems(a: Item, b: Item): number {
  if (type(a) !== type(b)) return 2;
  const order: [number, number][] = [
    [a.level, b.level], [Number(a.skill), Number(b.skill)], [a.excellent, b.excellent], [optionCount(a), optionCount(b)],
    [a.optionLevel, b.optionLevel], [a.durability, b.durability],
  ];
  for (const [x, y] of order) {
    if (x < y) return -1;
    if (x > y) return 1;
  }
  return 0;
}

const u32le = (b: Uint8Array, at: number) => (b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24)) >>> 0;

function readName(bytes: Uint8Array, at: number, length: number): string {
  const slice = bytes.subarray(at, Math.min(at + length, bytes.length));
  const end = slice.indexOf(0);
  return new TextDecoder().decode(end >= 0 ? slice.subarray(0, end) : slice);
}

export interface TradeOptions {
  connection: Connection;
  inventory: Inventory;
  itemWindows: ItemWindows;
  /** A line for the chat (the system messages of the desktop's log box). */
  message: (text: string) => void;
  /** A yes / no question (the trade request, the warning before OK). */
  confirm: (text: string) => Promise<boolean>;
  /** Asks for an amount (the zen to trade); default: the browser's prompt. */
  prompt?: (text: string) => Promise<string | null>;
  /** The hero's level: the trade command needs level 6. Default: no check. */
  level?: () => number;
  /** The hero's name, shown on the own side. */
  heroName?: () => string;
  /** A player's name by object id, for "You have requested %s to trade." */
  playerName?: (playerId: number) => string | undefined;
}

export class Trade {
  readonly connection: Connection;
  readonly inventory: Inventory;
  readonly itemWindows: ItemWindows;
  private readonly options: TradeOptions;
  /** The partner's last name, kept so the remembered items (backup) stay for a trade with the same player. */
  private lastPartner = '';
  /** The partner's items remembered after they left a trade (m_aYourInvenBackUp). */
  private backup: Item[] = [];
  /** m_bTradeAlert: the partner's items changed; OK asks first. */
  alert = false;
  /** The zen asked for with SetTradeMoney, until the server confirms it (m_nTempMyTradeGold). */
  private requestedGold = 0;
  /** A trade request is waiting for this player's answer. */
  requestPending = false;
  /** Called when a trade opens or ends (for tests and the HUD). */
  onChange: (what: 'opened' | 'closed' | 'request') => void = () => undefined;
  readonly stop: () => void;

  constructor(options: TradeOptions) {
    this.options = options;
    this.connection = options.connection;
    this.inventory = options.inventory;
    this.itemWindows = options.itemWindows;
    const window = this.itemWindows.trade;
    this.itemWindows.tradeHandlers = {
      close: () => this.closeButton(),
      zen: () => { void this.askGold(); },
      confirm: () => { void this.confirmButton(); },
      bagRightClick: (slot, item) => this.offerFromBag(slot, item),
    };
    this.itemWindows.onTradeClosed = () => this.cancel();
    window.onOffer = (item) => {
      if (!isTradeBan(item)) return true;
      this.options.message('These items cannot be traded.');
      return false;
    };
    window.onOfferChanged = (kind) => {
      // Putting an item in clears the own OK (UncheckMyConfirm); taking one out after OK too.
      if (kind === 'added') this.uncheck(true);
      else if (this.view.myConfirmed) this.uncheck(true);
    };
    window.onTradeItemRightClick = (slot, item) => this.takeBack(slot, item);
    this.stop = this.connection.onPacket((bytes) => this.handle(bytes));
  }

  get view(): TradeView { return this.itemWindows.trade.view; }

  /** Whether a trade is open (from the accepted answer until TradeFinished; the window may still be loading). */
  get isOpen(): boolean { return this.active; }
  private active = false;

  /** Asks another player (by object id) to trade (TradeRequest); the server forwards it if they are in view. */
  requestTrade(playerId: number): void {
    const level = this.options.level?.();
    if (level !== undefined && level < TRADE_MIN_LEVEL) {
      this.options.message('You can use the trade command at character level 6');
      return;
    }
    if (this.isOpen) return;
    this.connection.send('ClientToServer', 'TradeRequest', { PlayerId: playerId });
    const name = this.options.playerName?.(playerId);
    if (name) this.options.message(`You have requested ${name} to trade.`);
  }

  /** Cancels the open trade (TradeCancel); the server answers with TradeFinished for both players. */
  cancel(): void {
    this.alert = false;
    this.connection.send('ClientToServer', 'TradeCancel', {});
  }

  // The close button (CTrade::ProcessCloseBtn): not while an item is on the cursor; the window closes
  // when the server says the trade ended.
  private closeButton(): void {
    if (this.itemWindows.picked.source) return;
    this.cancel();
  }

  handle(bytes: Uint8Array): void {
    const c1 = bytes[0] === 0xc1, c3 = bytes[0] === 0xc3;
    if (!c1 && !c3) return;
    const code = bytes[2];
    if (c3 && code === 0x36 && bytes.length >= 4) void this.received(readName(bytes, 3, 10));
    else if (!c1) return;
    else if (code === 0x37 && bytes.length >= 4) {
      // TradeRequestAnswer: 0 declined, 1 accepted, 2 not possible now
      const partner = { name: readName(bytes, 4, 10), level: bytes.length >= 16 ? (bytes[14] << 8) | bytes[15] : 0 };
      void this.answered(bytes[3], partner);
    } else if (code === 0x38 && bytes.length >= 4) this.partnerRemoved(bytes[3]);
    else if (code === 0x39 && bytes.length >= 9) this.partnerAdded(bytes[3], decodeItem(bytes, 4));
    else if (code === 0x3a && bytes[3] === 0x01) {
      // TradeMoneySetResponse: the zen asked for is in the trade
      this.view.myGold = this.requestedGold;
      this.itemWindows.trade.refresh();
    } else if (code === 0x3b && bytes.length >= 8) {
      this.view.partnerGold = u32le(bytes, 4);
      this.itemWindows.trade.refresh();
    } else if (code === 0x3c && bytes.length >= 4) this.partnerConfirmChanged(bytes[3]);
    else if (code === 0x3d && bytes.length >= 4) this.finished(bytes[3]);
  }

  // A player asks to trade (CTrade::ProcessToReceiveTradeRequest): declined at once while the vault is
  // open, otherwise asked.
  private async received(requester: string): Promise<void> {
    if (this.inventory.openWindow === NpcWindow.VAULT || this.itemWindows.vault.isOpen || this.isOpen) {
      this.connection.send('ClientToServer', 'TradeRequestResponse', { TradeAccepted: false });
      return;
    }
    this.requestPending = true;
    this.onChange('request');
    const accepted = await this.options.confirm(`${requester} would like to trade with you.`);
    this.requestPending = false;
    this.connection.send('ClientToServer', 'TradeRequestResponse', { TradeAccepted: accepted });
  }

  // The answer to a request (CTrade::ProcessToReceiveTradeResult); both players get it when the trade opens.
  private async answered(reply: number, partner: { name: string; level: number }): Promise<void> {
    if (reply === 0) { this.options.message('Your trade has been canceled.'); return; }
    if (reply === 2) { this.options.message('You cannot trade right now.'); return; }
    if (reply !== 1) return;
    if (!this.alert && this.lastPartner !== partner.name) this.backup = [];
    this.alert = false;
    this.lastPartner = partner.name;
    this.inventory.trade.clear();
    Object.assign(this.view, {
      partnerName: partner.name, partnerLevel: partner.level, partnerGold: 0, partnerConfirmed: false,
      myName: this.options.heroName?.() ?? '', myGold: 0, myConfirmed: false, waitUntil: 0,
    });
    this.view.partner.clear();
    this.view.warnings.clear();
    this.requestedGold = 0;
    this.active = true;
    await this.itemWindows.openTrade();
    // The trade may have ended while its window was loading.
    if (!this.active) { this.itemWindows.closeTrade(); return; }
    this.onChange('opened');
  }

  private partnerAdded(slot: number, item: Item): void {
    this.view.partner.set(slot, item);
    this.checkPartnerItems();
    this.itemWindows.trade.refresh();
  }

  private partnerRemoved(slot: number): void {
    const item = this.view.partner.get(slot);
    if (item) this.remember(item);
    this.view.partner.delete(slot);
    this.checkPartnerItems();
    this.itemWindows.trade.refresh();
  }

  // CTrade::BackUpYourInven: remembers a valuable item the partner took out (or offered in a cancelled trade).
  private remember(item: Item): void {
    if (!worthRemembering(item)) return;
    let same = false;
    for (let i = 0; i < this.backup.length; i++) {
      const compared = compareTradeItems(this.backup[i], item);
      if (compared === 0) { same = true; break; }
      if (compared === -1) { same = true; this.backup[i] = item; break; }
      if (compared !== 2) same = true;
    }
    if (!same && this.backup.length < TRADE.columns * TRADE.rows) this.backup.push(item);
  }

  // CTrade::AlertYourTradeInven: flags the partner's items that are worse than the same item remembered
  // from before; an unchanged remembered item clears the flags of its type.
  private checkPartnerItems(): void {
    this.alert = false;
    this.view.warnings.clear();
    const unchangedTypes: number[] = [];
    for (const [slot, item] of this.view.partner) {
      for (const remembered of this.backup) {
        if (type(remembered) !== type(item)) continue;
        const compared = compareTradeItems(remembered, item);
        if (compared === 1) { this.alert = true; this.view.warnings.add(slot); }
        else {
          if (compared === 0) unchangedTypes.push(type(remembered));
          this.view.warnings.delete(slot);
          break;
        }
      }
    }
    if (unchangedTypes.length > 0) {
      this.alert = false;
      for (const [slot, item] of this.view.partner) if (unchangedTypes.includes(type(item))) this.view.warnings.delete(slot);
    }
  }

  // TradeButtonStateChanged (CTrade::ProcessToReceiveYourConfirm): the partner's OK; "red" clears both
  // and makes the own button wait (the zen changed).
  private partnerConfirmChanged(state: number): void {
    if (state === ButtonState.UNCHECKED) this.view.partnerConfirmed = false;
    else if (state === ButtonState.CHECKED) this.view.partnerConfirmed = true;
    else if (state === ButtonState.RED) {
      this.view.myConfirmed = false;
      this.view.partnerConfirmed = false;
      this.view.waitUntil = performance.now() + TRADE_WAIT_MS;
    }
    this.itemWindows.trade.refresh();
  }

  // TradeFinished (CTrade::ProcessToReceiveTradeExit): the message, then the window closes. The server
  // sends the inventory list and the money after it.
  private finished(result: number): void {
    const messages: Record<number, string> = {
      0: 'Your trade has been canceled.',
      2: 'Your trade has been canceled because your inventory is full.',
      3: 'Trade request is canceled',
      4: "Reinforced item can't be traded.",
    };
    if (result === 0) {
      this.alert = false;
      for (const item of this.view.partner.values()) this.remember(item);
    }
    if (messages[result]) this.options.message(messages[result]);
    this.inventory.trade.clear();
    this.view.partner.clear();
    this.view.warnings.clear();
    this.view.myConfirmed = false;
    this.view.partnerConfirmed = false;
    this.active = false;
    this.itemWindows.closeTrade();
    this.onChange('closed');
  }

  private send(state: ButtonState): void {
    this.connection.send('ClientToServer', 'TradeButtonStateChange', { NewState: state });
  }

  // CTrade::UncheckMyConfirm.
  private uncheck(send: boolean): void {
    this.view.myConfirmed = false;
    if (send) this.send(ButtonState.UNCHECKED);
    this.itemWindows.trade.refresh();
  }

  // CTrade::AlertTrade: the own OK switches.
  private toggleConfirm(): void {
    this.view.myConfirmed = !this.view.myConfirmed;
    this.alert = true;
    this.send(this.view.myConfirmed ? ButtonState.CHECKED : ButtonState.UNCHECKED);
    this.itemWindows.trade.refresh();
  }

  // The OK button: not while it waits or an item is on the cursor; with changed partner items it warns first.
  async confirmButton(): Promise<void> {
    if (performance.now() < this.view.waitUntil || this.itemWindows.picked.source) return;
    if (this.alert && !this.view.myConfirmed) {
      const sure = await this.options.confirm('The levels and options of some items have been changed in trade. '
        + 'Please check whether the item is the same item that you want to trade.');
      if (!sure) return;
    }
    this.toggleConfirm();
  }

  // The zen button (trade_zen_click, SendRequestMyGoldInput): an amount up to the money plus what's in already.
  async askGold(): Promise<void> {
    const ask = this.options.prompt ?? ((text: string) => Promise.resolve(window.prompt(text, '')));
    const text = await ask('Enter the amount of Zen you would like to trade.');
    if (text === null) return;
    const amount = Math.floor(Number(text.replace(/[^0-9]/g, '').slice(0, 8)) || 0);
    if (amount === 0) return;
    this.setGold(amount);
  }

  /** Puts an amount of zen into the trade (SetTradeMoney). */
  setGold(amount: number): void {
    if (!this.isOpen) return;
    if (amount > this.inventory.money + this.view.myGold) { this.options.message('You are short of Zen.'); return; }
    if (this.view.myConfirmed) this.uncheck(true);
    if (this.view.myGold > 0) this.view.waitUntil = performance.now() + TRADE_WAIT_MS;
    this.requestedGold = amount;
    this.connection.send('ClientToServer', 'SetTradeMoney', { Amount: amount });
    this.itemWindows.trade.refresh();
  }

  /** Presses the own OK button (as a click on it). */
  pressOk(): Promise<void> { return this.confirmButton(); }

  // A right click on a bag item while trading (CTrade::ProcessMyInvenItemAutoMove): into the first free trade cell.
  offerFromBag(slot: number, item: Item): void {
    if (slot < BAG.firstSlot || this.inventory.pendingMove) return;
    if (isTradeBan(item)) { this.options.message('These items cannot be traded.'); return; }
    const target = this.inventory.firstEmptySlot(this.inventory.trade, TRADE, item);
    if (target === null) return;
    this.inventory.move(Storage.INVENTORY, slot, Storage.TRADE, target);
    this.uncheck(true);
  }

  // A right click on an own trade item (ProcessMyTradeItemAutoMoveToInventory): back into the bag; after
  // OK it clears the OK, and the button waits so the partner sees the change.
  takeBack(slot: number, item: Item): void {
    if (this.inventory.pendingMove) return;
    const target = this.inventory.firstEmptySlot(this.inventory.items, BAG, item);
    if (target === null) return;
    this.inventory.move(Storage.TRADE, slot, Storage.INVENTORY, target);
    if (this.view.myConfirmed) this.toggleConfirm();
    this.view.waitUntil = performance.now() + TRADE_WAIT_MS;
    this.itemWindows.trade.refresh();
  }
}
