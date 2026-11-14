// The cash shop (HNMO gives its coins for free), as the desktop client's CInGameShop (in_game_shop.rml),
// CMsgBoxIGSBuyPackageItem / CMsgBoxIGSBuySelectItem (igs_buy_package.rml, igs_buy_select.rml), the buy, use and
// storage item dialogs (MsgBoxIGS*.cpp) and their packets (WSclient.cpp ReceiveIGS_*): X or the HUD's shop button
// asks the server (CashShopOpenState); the window shows the sales zones, the categories, the packages with their
// items and prices 9 a page (game/cashShopScript.ts), the coins, the storage and gift lists. Buy asks and sends
// CashShopItemBuyRequest; a bought item waits in the storage until Use (CashShopStorageItemConsumeRequest) moves it
// to the inventory (seals and scrolls work at once).

import { gameName } from '../gameName.js';
import { itemDefinition } from '../game/items.js';
import type { Item } from '../game/inventory.js';
import type { Connection } from '../net/connection.js';
import { read } from '../net/packets.js';
import { CashShopScript, GIFT_ENABLED, PACKAGES_PER_PAGE, convertGold, loadCashShopBanner, loadCashShopScript, scriptFolder } from '../game/cashShopScript.js';
import type { ShopCategory, ShopPackage } from '../game/cashShopScript.js';
import { drawItem } from './itemIcons.js';
import { openDocument, prepareDocument, REFERENCE_HEIGHT } from './rml.js';
import type { RmlDocument, RmlModel } from './rml.js';
import { askInGame } from './personalShop.js';

// The desktop's texts (I18N::Game); the shop's own name is the game's name (gameName.ts).
export const IGS_TEXT = {
  myWCoin: 'My W Coin : ', myWCoinP: 'My W Coin(P) : ', goblinPoints: 'Goblin Points : ', storage: 'Storage', giftInventory: 'Gift Inventory',
  buy: 'Buy', gift: 'Gift', cancel: 'Cancel', use: 'Use', itemName: 'Item Name', duration: 'Duration', sendWCoin: 'Send W Coin',
  recharge: 'Recharge W Coin', update: 'Update Information', close: 'Close', shop: 'Shop', error: 'Error', error2: 'error2',
  restricted: 'Restricted Function',
  get notSupported() { return `This function is not supported in the ${gameName()} Item Shop.`; },
  get safeZoneOnly() { return `You can only use the ${gameName()} Item Shop in a town or safe zone.`; },
  get reconnect() { return `Cannot open ${gameName()} Item Shop.\nPlease reconnect to the game.`; },
  purchaseConfirmation: 'Purchase Confirmation', wishToBuy: 'Do you wish to buy the following item(s)?', item: 'Item: %s', price: 'Price: %s',
  durationS: 'Duration: %s', noReturn: 'Bought items used or taken out of storage cannot be returned.',
  purchaseCompleted: 'Purchase Completed', purchaseMade: 'Your purchase has been made.', purchaseFailed: 'Purchase Failed',
  notEnoughCoins: 'You do not have enough W Coin or points.', storageFull: 'You do not have enough space in storage.', soldOut: 'This item has sold out.',
  notAvailable: 'This item is not currently available.', noLonger: 'This item is no longer available.', cannotBuy: 'This item cannot be bought.',
  eventItems: 'Event items cannot be bought.', eventLimit: "You've exceeded the maximum number of times you can purchase event items.",
  wrongCoin: 'You have selected an incorrect W Coin type. Please select again.', dbFailed: 'Database access failed.', dbError: 'A database error has occurred.',
  unknown: 'Unknown Error', noUsable: 'There is no usable item.', itemInfo: 'Item Info Window', giftInfo: 'Gift Info Window', quantity: 'Quantity: %s',
  giftFrom: "It's a gift from %s.", useConfirmation: 'Use Confirmation',
  wishToUse: 'Do you wish to use %s?\n\n*With the exception of seals and scrolls, all items will be transferred to your Inventory.\n\nItems removed from storage or gift inventory cannot be recovered or returned.',
  itemUsed: 'Item Used', hasBeenUsed: 'The item has been used.', failedToUse: 'Failed to Use', storageMissing: '[Use Storage] does not exist.',
  pcCafe: 'You can receive this item only from a PC cafe.', colorPlan: 'An active Color Plan exists in the selected period.',
  fixedPlan: 'An active Personal Fixed Plan exists in the selected period.', inventoryFull: 'Not enough space in the Inventory.\nPlease try again.',
  cannotUse: 'Cannot use the selected item.', conflicting: "This item cannot be used along with an item that's already in use.",
  anError: 'There has been an error.', dbAccess: 'A database access error has occurred.', wCoins: 'W Coin %s Coins', sWCoin: '%s W Coin',
  quantityDuration: 'Quantity: %d / Duration: %s', sendGift: 'Send Gift Items', recipient: "Recipient's Character Name:",
  message: '<Message to the Recipient>', giftConfirm: 'Gifted items cannot be returned. Deliver the gift(s)?', giftDelivered: 'Gift Delivered',
  giftDone: 'Your gift has been delivered.', giftFailed: 'Gift Delivery Failed', notEnoughCash: 'You do not have enough cash.',
  recipientFull: "The recipient's storage is full.", noRecipient: 'Cannot find the recipient.', notGiftable: 'This item cannot be sent as a gift.',
} as const;

const format = (text: string, ...values: (string | number)[]): string => {
  let i = 0;
  return text.replace(/%[sd]/g, () => String(values[i++] ?? ''));
};

/** An item to draw from its code (group * 512 + number); null for none (65535). */
export function itemOfCode(code: number): Item | null {
  if (code < 0 || code >= 0xffff) return null;
  const group = code >> 9, number = code & 0x1ff;
  const definition = itemDefinition(group, number);
  if (!definition) return null;
  return {
    group, number, level: 0, durability: 1, luck: false, skill: false, guardian: false, optionLevel: 0, optionType: 0, excellent: 0,
    ancientSet: 0, ancientBonus: 0, harmony: 0, sockets: [], socketBonus: 0, length: 5, width: definition.width ?? 1, height: definition.height ?? 1,
  };
}

/** An OK box in the game with a title (CreateOkMessageBoxWithTitle). Doesn't block. */
export function messageInGame(title: string, text: string): Promise<void> {
  return new Promise((resolve) => {
    const box = document.createElement('div');
    box.className = 'chip confirm-dialog message-dialog';
    box.setAttribute('role', 'dialog');
    Object.assign(box.style, {
      position: 'fixed', left: '50%', top: '35%', transform: 'translate(-50%, -50%)', zIndex: '96', pointerEvents: 'auto',
      display: 'grid', gap: '10px', padding: '16px', minWidth: 'min(320px, calc(100vw - 32px))', textAlign: 'center',
    });
    const heading = document.createElement('div');
    heading.className = 'message-title';
    heading.textContent = title;
    Object.assign(heading.style, { fontWeight: 'bold', color: '#ffd040' });
    const lines = document.createElement('div');
    lines.className = 'message-text';
    lines.style.whiteSpace = 'pre-line';
    lines.textContent = text;
    const ok = document.createElement('button');
    ok.type = 'button'; ok.className = 'primary confirm-ok'; ok.textContent = 'OK';
    box.append(heading, lines, ok);
    const done = () => { box.remove(); window.removeEventListener('keydown', onKey, true); resolve(); };
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Enter' || event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); done(); } };
    ok.addEventListener('click', done);
    box.addEventListener('pointerdown', (event) => event.stopPropagation());
    window.addEventListener('keydown', onKey, true);
    document.body.append(box);
    ok.focus();
  });
}

// '#' breaks a description's lines; long lines wrap (DivideStringByPixel at the box's width, about this many letters).
function descriptionLines(text: string, width = 26): string[] {
  const result: string[] = [];
  for (const part of text.split('#')) {
    let line = '';
    for (const word of part.split(' ')) {
      if (line && (line + ' ' + word).length > width) { result.push(line); line = word; } else line = line ? `${line} ${word}` : word;
    }
    result.push(line);
  }
  while (result.length && result[result.length - 1] === '') result.pop();
  return result.filter((line, i) => line !== '' || i > 0);
}

interface RadioEntry { name: string; selected: boolean; last: boolean }
type RadioList = RadioEntry[] & { size: number };
const radio = (names: string[], selected: number): RadioList =>
  Object.assign(names.map((name, i) => ({ name, selected: i === selected, last: i === names.length - 1 })), { size: names.length });

// in_game_shop.rml's data model (InGameShopRmlModel).
interface ShopModel extends RmlModel {
  text_px: number; character_name: string; wallet: { label: string; value: string }[]; zones: RadioList; categories: RadioList;
  packages: { name: string; price: string; shown: boolean }[]; page: string; total_pages: string; storage_tabs: RadioList;
  storage_rows: { name: string; period: string; selected: boolean }[]; storage_page: string; storage_total_pages: string;
  buy_label: string; use_label: string; item_name_label: string; duration_label: string; gift_hint: string; charge_hint: string;
  refresh_hint: string; close_hint: string; banner_src: string; banner_linked: boolean; script_version: string; banner_version: string;
  igs_zone: (i: number) => void; igs_category: (i: number) => void; igs_storage_tab: (i: number) => void; igs_buy: (i: number) => void;
  igs_page: (d: number) => void; igs_storage_page: (d: number) => void; igs_gift: () => void; igs_charge: () => void; igs_refresh: () => void;
  igs_use: () => void; igs_close: () => void; igs_banner: () => void; igs_select_storage: (i: number) => void;
}

/** A storage or gift list entry (GameShop::StorageItem). */
export interface StorageItem {
  storageSeq: number; storageItemSeq: number; groupCode: number; productSeq: number; priceSeq: number; cashPoint: number;
  type: string; name: string; num: string; count: number; period: string; itemCode: number; sender: string; message: string;
}

/** A price option of a package (GameShop::BuyOption). */
interface BuyOption { priceSeq: number; cashType: number; itemCode: number; name: string; price: string; period: string }

export interface CashShopOptions {
  connection: Connection;
  /** The hero's name. */
  heroName: string;
  /** Whether the hero stands in a safe zone (the shop only opens there) and is walking. */
  safeZone: () => boolean;
  walking?: () => boolean;
  /** Closes the other windows (HideAll). */
  closeOthers?: () => void;
  /** Lights the HUD's shop button while the shop is open (MAINFRAME_BTN_PARTCHARGE). */
  onToggle?: (open: boolean) => void;
  click?: () => void;
}

const u = (bytes: Uint8Array, at: number): number => bytes[at] ?? 0;

export class CashShop {
  private doc: RmlDocument | null = null;
  private dialog: RmlDocument | null = null;
  private readonly model: ShopModel;
  private readonly options: CashShopOptions;
  /** The script and banner versions the server named (CashShopScriptVersion, CashShopBannerVersion). */
  scriptVersion: { zone: number; year: number; id: number } | null = null;
  bannerVersion: { zone: number; year: number; id: number } | null = null;
  script: CashShopScript | null = null;
  /** The coins (SetCashCreditCard, SetCashPrepaid, SetTotalMileage). */
  coins = { wCoinC: 0, wCoinP: 0, goblinPoints: 0 };
  zoneIndex = 0;
  categoryIndex = 0;
  page = 1;
  /** IGS_SAFEKEEPING_LISTBOX (0) or IGS_PRESENTBOX_LISTBOX (1). */
  storageBox = 0;
  storageItems: StorageItem[] = [];
  selectedRow = -1;
  storagePage = 0;
  storageTotalPages = 0;
  private storagePageCount = 0;
  private storageReceived = 0;
  private selectedStorageIndex = 0;
  private requestCurrentPage = false;
  private requestingOpen = false;
  private readonly canvases: HTMLCanvasElement[] = [];

  constructor(options: CashShopOptions) {
    this.options = options;
    this.model = {
      text_px: 12, character_name: options.heroName, wallet: [], zones: radio([], 0), categories: radio([], 0), packages: [],
      page: '1', total_pages: '1', storage_tabs: radio([IGS_TEXT.storage, IGS_TEXT.giftInventory], 0), storage_rows: [],
      storage_page: '0', storage_total_pages: '0', buy_label: IGS_TEXT.buy, use_label: IGS_TEXT.use, item_name_label: IGS_TEXT.itemName,
      duration_label: IGS_TEXT.duration, gift_hint: IGS_TEXT.sendWCoin, charge_hint: IGS_TEXT.recharge, refresh_hint: IGS_TEXT.update,
      close_hint: IGS_TEXT.close, banner_src: '', banner_linked: false, script_version: '', banner_version: '',
      igs_zone: (i) => this.selectZone(i), igs_category: (i) => this.selectCategory(i), igs_storage_tab: (i) => this.selectStorageBox(i),
      igs_buy: (i) => this.buyPackage(i), igs_page: (d) => this.turnPage(d > 0), igs_storage_page: (d) => this.turnStoragePage(d > 0),
      igs_gift: () => { void messageInGame(IGS_TEXT.restricted, IGS_TEXT.notSupported); },
      igs_charge: () => { void messageInGame(IGS_TEXT.restricted, IGS_TEXT.notSupported); },
      igs_refresh: () => this.send('CashShopPointInfoRequest'),
      igs_use: () => { void this.useStorageItem(); }, igs_close: () => this.close(), igs_banner: () => undefined,
      igs_select_storage: (i) => { if (i >= 0 && i < this.storageItems.length) { this.selectedRow = i; this.sync(); } },
    };
    options.connection.onPacket((bytes) => this.handle(bytes));
    window.addEventListener('keydown', (event) => {
      if (event.target instanceof Element && event.target.closest('input, textarea')) return;
      if (event.ctrlKey || event.altKey || event.metaKey) return;
      if (event.key === 'Escape' && this.doc && !this.dialog) { this.close(); return; }
      if (event.key.toLowerCase() === 'x') { event.preventDefault(); void this.toggle(); }
    });
    window.addEventListener('resize', () => this.place());
  }

  get isOpen(): boolean { return this.doc !== null; }

  private send(name: string, values: Record<string, number | boolean | string> = {}): void {
    this.options.connection.send('ClientToServer', name, values);
  }

  /** X or the HUD's button (CHotKey / CMainFrameWindow): asks the server to open the shop, or closes it. */
  async toggle(): Promise<void> {
    if (this.doc) { this.close(); return; }
    // IsInGameShopOpen: not while walking, only in a safe zone, only once the server named the script.
    if (this.options.walking?.()) return;
    if (!this.options.safeZone()) { void messageInGame(IGS_TEXT.error, IGS_TEXT.safeZoneOnly); return; }
    if (!this.scriptVersion) { void messageInGame(IGS_TEXT.error, IGS_TEXT.reconnect); return; }
    try {
      const v = this.scriptVersion;
      this.script = await loadCashShopScript(scriptFolder(v.zone, v.year, v.id));
    } catch (error) {
      void messageInGame(IGS_TEXT.error, `${gameName()} Item Shop information download failed!\n\nPlease reconnect to the game.\n${String(error)}`);
      return;
    }
    if (this.bannerVersion && !this.model.banner_src) {
      const b = this.bannerVersion;
      this.model.banner_src = (await loadCashShopBanner(scriptFolder(b.zone, b.year, b.id)).catch(() => null)) ?? '';
    }
    if (this.requestingOpen) return;
    this.requestingOpen = true;
    this.send('CashShopOpenState', { IsClosed: false });
    this.options.onToggle?.(true);
  }

  /** CInGameShop::Close: tells the server and closes. */
  close(): void {
    if (!this.doc) return;
    this.send('CashShopOpenState', { IsClosed: true });
    this.closeDialog();
    this.doc.close();
    this.doc = null;
    this.canvases.length = 0;
    // ClosingProcess: the storage list starts over next time.
    this.storageBox = 0;
    this.clearStorage();
    this.options.click?.();
    this.options.onToggle?.(false);
  }

  // ReceiveIGS_ShopOpenResult then CSystem::Show(INTERFACE_INGAMESHOP): OpeningProcess.
  private async show(): Promise<void> {
    if (this.doc) return;
    this.options.closeOthers?.();
    this.options.click?.();
    this.coins = { wCoinC: 0, wCoinP: 0, goblinPoints: 0 };
    this.zoneIndex = 0; this.categoryIndex = 0; this.page = 1;
    await prepareDocument('in_game_shop');
    if (this.doc) return;
    this.doc = await openDocument('in_game_shop', this.model, { parent: document.body });
    this.doc.host.style.zIndex = '45';
    const panel = this.doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
      panel.addEventListener('contextmenu', (event) => event.preventDefault());
    }
    this.place();
    this.sync();
  }

  private place(): void {
    if (!this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    this.model.text_px = Math.round(12 * scale);
    const panel = this.doc.$('#panel');
    if (panel) Object.assign(panel.style, { transform: `scale(${scale})`, transformOrigin: 'left top', left: `${this.left(scale)}px`, top: `${this.top(scale)}px` });
    this.placeDialog();
    this.sync();
  }

  private left(scale: number): number { return Math.max(0, (window.innerWidth - 640 * scale) / 2); }
  private top(scale: number): number { return Math.max(0, (window.innerHeight - 48 * scale - 429 * scale) / 2); }

  // ---------- zones, categories, pages ----------

  get zones(): ShopCategory[] { return this.script?.zones() ?? []; }
  get categories(): ShopCategory[] { return this.script?.categoriesOf(this.zones[this.zoneIndex] ?? null) ?? []; }
  get categoryPackages(): ShopPackage[] { return this.script?.packagesOf(this.categories[this.categoryIndex] ?? null) ?? []; }
  /** GetTotalPages: the desktop counts one page more when the list fills its pages exactly. */
  get totalPages(): number { return Math.floor(this.categoryPackages.length / PACKAGES_PER_PAGE) + 1; }
  get displayPackages(): ShopPackage[] {
    const start = (this.page - 1) * PACKAGES_PER_PAGE;
    return this.categoryPackages.slice(start, start + PACKAGES_PER_PAGE);
  }

  selectZone(index: number): void {
    if (index < 0 || index >= this.zones.length || index === this.zoneIndex) return;
    this.zoneIndex = index; this.categoryIndex = 0; this.page = 1;
    this.sync();
  }

  selectCategory(index: number): void {
    if (index < 0 || index >= this.categories.length || index === this.categoryIndex) return;
    this.categoryIndex = index; this.page = 1;
    this.sync();
  }

  private turnPage(next: boolean): void {
    if (next && this.totalPages > this.page) this.page++;
    else if (!next && this.page > 1) this.page--;
    this.sync();
  }

  // ---------- the storage ----------

  private storageCode(): number { return (this.storageBox === 0 ? 'S' : this.storageBox === 1 ? 'G' : 'Z').charCodeAt(0); }

  private requestStorage(page: number): void {
    this.send('CashShopStorageListRequest', { PageIndex: page, InventoryType: this.storageCode() });
  }

  private selectStorageBox(index: number): void {
    if (index < 0 || index > 1 || index === this.storageBox) return;
    this.storageBox = index;
    this.selectedStorageIndex = 0;
    this.requestCurrentPage = true;
    this.requestStorage(1);
    this.sync();
  }

  private turnStoragePage(next: boolean): void {
    const page = next ? this.storagePage + 1 : this.storagePage - 1;
    if ((next && this.storagePage < this.storageTotalPages) || (!next && this.storagePage > 1)) {
      this.selectedStorageIndex = 0;
      this.requestCurrentPage = true;
      this.requestStorage(page);
    }
  }

  private clearStorage(): void {
    this.storageItems = [];
    this.selectedRow = -1;
    this.storagePage = 0;
    this.storageTotalPages = 0;
    this.storagePageCount = 0;
    this.storageReceived = 0;
  }

  // InitStorage.
  private initStorage(_total: number, pageCount: number, totalPages: number, page: number): void {
    this.clearStorage();
    this.storagePageCount = pageCount;
    this.storageTotalPages = totalPages;
    this.storagePage = totalPages > 0 ? page : 0;
    if (this.selectedStorageIndex === 0 || !this.requestCurrentPage) this.selectedStorageIndex = pageCount;
    this.requestCurrentPage = false;
    this.sync();
  }

  // CInGameShop::AddStorageItem: the entry's name, count and duration from the script.
  private addStorageItem(entry: Omit<StorageItem, 'name' | 'num' | 'count' | 'period' | 'itemCode'>): void {
    const script = this.script;
    const item: StorageItem = { ...entry, name: '', num: '-', count: 1, period: '-', itemCode: -1 };
    if (entry.type === 'C' || entry.type === 'c') {
      const value = convertGold(entry.cashPoint);
      item.name = format(IGS_TEXT.wCoins, value);
      item.num = format(IGS_TEXT.sWCoin, value);
      item.count = entry.cashPoint;
    } else if ((entry.type === 'P' || entry.type === 'p') && script) {
      const info = (attribute: 'name' | 'count' | 'period' | 'itemCode') => (entry.priceSeq > 0
        ? script.infoByPrice(entry.productSeq, entry.priceSeq, attribute) : script.infoByProduct(entry.productSeq, attribute));
      const name = info('name');
      if (!name) { if (entry.priceSeq > 0) item.name = 'aaa'; else return; } else item.name = name.text;
      const count = info('count');
      if (count && count.value > 0) { item.num = `${count.value} ${count.text}`; item.count = count.value; }
      const period = info('period');
      if (period && period.value > 0) item.period = `${period.value} ${period.text}`;
      item.itemCode = info('itemCode')?.value ?? -1;
    } else return;
    this.storageReceived++;
    this.storageItems.push(item);
    this.selectedRow = this.storageItems.length - 1;
    if (this.storageReceived >= this.storagePageCount) {
      this.selectedRow = this.selectedStorageIndex > this.storagePageCount ? this.storagePageCount - 1 : this.selectedStorageIndex - 1;
    }
    this.sync();
  }

  // UpdateStorageItemList: the page again after an item was used.
  private refreshStorage(): void {
    const line = this.selectedRow + 1;
    this.requestCurrentPage = true;
    if (this.storagePageCount === 1 && this.storageTotalPages > 1) { this.selectedStorageIndex = 1; this.requestStorage(this.storagePage - 1); }
    else if (line === 1) { this.selectedStorageIndex = line; this.requestStorage(this.storagePage); }
    else if (this.storagePageCount < 9) { this.selectedStorageIndex = line - 1; this.requestStorage(this.storagePage); }
    else { this.selectedStorageIndex = line; this.requestStorage(this.storagePage); }
  }

  // UseStorageItem: the item's window (gift or not), then the use question, then the request.
  private async useStorageItem(): Promise<void> {
    if (!this.storageItems.length) { void messageInGame(IGS_TEXT.error, IGS_TEXT.noUsable); return; }
    const item = this.storageItems[this.selectedRow];
    if (!item) return;
    const lines = this.storageBox === 1
      ? [IGS_TEXT.giftInfo, item.name, format(IGS_TEXT.giftFrom, item.sender), format(IGS_TEXT.quantity, item.num), format(IGS_TEXT.durationS, item.period), item.message]
      : [IGS_TEXT.itemInfo, item.name, format(IGS_TEXT.quantity, item.num), format(IGS_TEXT.durationS, item.period)];
    if (!(await this.confirmWithItem(lines, item.itemCode))) return;
    if (!(await this.confirmWithItem([IGS_TEXT.useConfirmation, format(IGS_TEXT.wishToUse, item.name)], -1))) return;
    this.send('CashShopStorageItemConsumeRequest', { BaseItemCode: item.storageSeq, MainItemCode: item.storageItemSeq, ItemIndex: item.itemCode & 0xffff, ProductType: item.type.charCodeAt(0) });
    this.send('CashShopPointInfoRequest');
  }

  /** The desktop's GenericConfirmDialog (title, lines, an item drawn above them). */
  private confirmWithItem(lines: string[], itemCode: number): Promise<boolean> {
    return new Promise((resolve) => {
      const box = document.createElement('div');
      box.className = 'chip confirm-dialog igs-confirm';
      box.setAttribute('role', 'dialog');
      Object.assign(box.style, {
        position: 'fixed', left: '50%', top: '35%', transform: 'translate(-50%, -50%)', zIndex: '96', pointerEvents: 'auto',
        display: 'grid', gap: '8px', padding: '16px', minWidth: 'min(320px, calc(100vw - 32px))', textAlign: 'center',
      });
      const [title, ...rest] = lines;
      const heading = document.createElement('div');
      heading.textContent = title;
      Object.assign(heading.style, { fontWeight: 'bold', color: '#ffd040' });
      box.append(heading);
      const item = itemOfCode(itemCode);
      if (item) {
        const canvas = document.createElement('canvas');
        canvas.width = 96; canvas.height = 60;
        canvas.style.margin = '0 auto';
        box.append(canvas);
        void drawItem(canvas, item);
      }
      for (const text of rest) {
        const row = document.createElement('div');
        row.style.whiteSpace = 'pre-line';
        row.textContent = text;
        box.append(row);
      }
      const buttons = document.createElement('div');
      buttons.className = 'buttons';
      const ok = document.createElement('button');
      ok.type = 'button'; ok.className = 'primary confirm-ok'; ok.textContent = 'OK';
      const cancel = document.createElement('button');
      cancel.type = 'button'; cancel.className = 'confirm-cancel'; cancel.textContent = IGS_TEXT.cancel;
      buttons.append(ok, cancel);
      box.append(buttons);
      const done = (answer: boolean) => { box.remove(); window.removeEventListener('keydown', onKey, true); resolve(answer); };
      const onKey = (event: KeyboardEvent) => {
        if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); done(true); }
        if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); done(false); }
      };
      ok.addEventListener('click', () => done(true));
      cancel.addEventListener('click', () => done(false));
      box.addEventListener('pointerdown', (event) => event.stopPropagation());
      window.addEventListener('keydown', onKey, true);
      document.body.append(box);
    });
  }

  // ---------- buying ----------

  // BuyPackage: one price shows igs_buy_package, several igs_buy_select.
  buyPackage(index: number): void {
    const pack = this.displayPackages[index];
    if (!pack || !this.script) return;
    if (pack.priceCount === 1) void this.openBuyDialog(pack, null);
    else if (pack.priceCount > 1) void this.openBuyDialog(pack, this.buyOptions(pack));
  }

  // CMsgBoxIGSBuySelectItem::AddData: each price option's item, name, price and duration; the last one picked.
  private buyOptions(pack: ShopPackage): BuyOption[] {
    const script = this.script!;
    const productSeq = pack.productSeqs[0] ?? 0;
    return pack.priceSeqs.map((priceSeq) => {
      const period = script.infoByPrice(productSeq, priceSeq, 'period');
      return {
        priceSeq, cashType: pack.cashType, itemCode: script.infoByPrice(productSeq, priceSeq, 'itemCode')?.value ?? -1,
        name: script.infoByPrice(productSeq, priceSeq, 'name')?.text ?? '',
        price: `${script.infoByPrice(productSeq, priceSeq, 'price')?.text ?? ''} ${pack.priceUnit}`,
        period: period && period.value > 0 ? `${period.value} ${period.text}` : '-',
      };
    });
  }

  private async openBuyDialog(pack: ShopPackage, options: BuyOption[] | null): Promise<void> {
    this.closeDialog();
    const script = this.script!;
    const single = options === null;
    const name = single ? 'igs_buy_package' : 'igs_buy_select';
    await prepareDocument(name);
    const giftEnabled = pack.giftFlag === GIFT_ENABLED;
    const firstPeriod = script.infoByProduct(pack.productSeqs[0] ?? 0, 'period');
    const singlePeriod = firstPeriod && firstPeriod.value > 0 ? `${firstPeriod.value} ${firstPeriod.text}` : '-';
    const singlePrice = `${convertGold(pack.price)} ${pack.priceUnit}`;
    let selected = options ? options.length - 1 : -1;
    const lines = descriptionLines(pack.description);
    const model: RmlModel = {
      text_px: this.model.text_px, title: IGS_TEXT.shop, name: pack.name,
      price: single ? singlePrice : options[selected]?.price ?? '',
      description_lines: single ? lines.map((text) => ({ text })) : lines,
      options: (options ?? []).map((option, i) => ({ name: option.name, selected: i === selected })),
      buttons: [{ label: IGS_TEXT.buy, enabled: true }, { label: IGS_TEXT.gift, enabled: giftEnabled }, { label: IGS_TEXT.cancel, enabled: true }],
      debug_lines: [],
      igs_select_option: (i: number) => {
        if (!options || i < 0 || i >= options.length) return;
        selected = i;
        model.price = options[i].price;
        model.options = options.map((option, j) => ({ name: option.name, selected: j === selected }));
        this.drawDialogItem(options[i].itemCode);
        this.dialog?.update();
      },
      igs_dialog_button: (i: number) => {
        const option = options ? options[selected] : null;
        if (options && !option) return;
        if (i === 1 && !giftEnabled) return;
        this.options.click?.();
        this.closeDialog();
        const priceSeq = option?.priceSeq ?? 0, cashType = option?.cashType ?? pack.cashType;
        const itemName = option?.name ?? pack.name, price = option?.price ?? singlePrice, period = option?.period ?? singlePeriod;
        const itemCode = option?.itemCode ?? pack.itemCode; // the picked option's item (BuySelect Update)
        if (i === 0) void this.confirmBuy(pack, priceSeq, itemCode, cashType, itemName, price, period);
        else if (i === 1) void this.sendGift(pack, priceSeq, itemCode, cashType, itemName, price, period);
      },
    };
    this.dialog = await openDocument(name, model, { parent: document.body });
    this.dialog.host.style.zIndex = '46';
    Object.assign(this.dialog.root.style, { position: 'fixed', left: '0', top: '0', width: '100%', height: '100%' });
    this.dialog.root.addEventListener('pointerdown', (event) => event.stopPropagation());
    this.placeDialog();
    this.drawDialogItem(single ? pack.itemCode : options[selected]?.itemCode ?? pack.itemCode);
  }

  private drawDialogItem(code: number): void {
    const box = this.dialog?.$('#igs_item_box');
    if (!box) return;
    box.replaceChildren();
    const item = itemOfCode(code);
    if (!item) return;
    const canvas = document.createElement('canvas');
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    canvas.width = Math.round(96 * scale); canvas.height = Math.round(60 * scale);
    Object.assign(canvas.style, { width: '100%', height: '100%' });
    box.append(canvas);
    void drawItem(canvas, item);
  }

  private placeDialog(): void {
    if (!this.dialog) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.dialog.setScale(scale);
    const stage = this.dialog.$('#stage');
    // .stage centres the cash shop's 640 x 480 at the UI scale (base.rcss), as the shop itself is.
    stage?.style.setProperty('--ui-scale', String(scale));
    this.dialog.update();
  }

  private closeDialog(): void {
    this.dialog?.close();
    this.dialog = null;
  }

  // ShowIGSBuyConfirmDialog: the item, price and duration; OK sends CashShopItemBuyRequest.
  private async confirmBuy(pack: ShopPackage, priceSeq: number, itemCode: number, cashType: number, name: string, price: string, period: string): Promise<void> {
    const ok = await this.confirmWithItem([IGS_TEXT.purchaseConfirmation, IGS_TEXT.wishToBuy, format(IGS_TEXT.item, name),
      format(IGS_TEXT.price, price), format(IGS_TEXT.durationS, period), IGS_TEXT.noReturn], -1);
    if (!ok) return;
    this.send('CashShopItemBuyRequest', { PackageMainIndex: pack.seq, Category: pack.displaySeq, ProductMainIndex: priceSeq, ItemIndex: itemCode & 0xffff, CoinIndex: cashType, MileageFlag: 0 });
  }

  // CMsgBoxIGSSendGift: the recipient and a message, the confirmation, then CashShopItemGiftRequest.
  private async sendGift(pack: ShopPackage, priceSeq: number, itemCode: number, cashType: number, name: string, price: string, period: string): Promise<void> {
    const recipient = await askInGame([IGS_TEXT.sendGift, format(IGS_TEXT.item, name), format(IGS_TEXT.price, price), format(IGS_TEXT.durationS, period), IGS_TEXT.recipient], { maxLength: 10 });
    if (!recipient) return;
    const text = await askInGame([IGS_TEXT.message], { maxLength: 200 });
    if (text === null) return;
    if (!(await this.confirmWithItem([IGS_TEXT.sendGift, IGS_TEXT.giftConfirm], itemCode))) return;
    this.send('CashShopItemGiftRequest', { PackageMainIndex: pack.seq, Category: pack.displaySeq, ProductMainIndex: priceSeq, ItemIndex: itemCode & 0xffff,
      CoinIndex: cashType, MileageFlag: 0, GiftReceiverName: recipient, GiftText: text });
  }

  // ---------- the window's model ----------

  private sync(): void {
    if (!this.doc) return;
    const m = this.model;
    m.character_name = this.options.heroName;
    m.wallet = [
      { label: IGS_TEXT.myWCoin, value: convertGold(this.coins.wCoinC) },
      { label: IGS_TEXT.myWCoinP, value: convertGold(this.coins.wCoinP) },
      { label: IGS_TEXT.goblinPoints, value: convertGold(this.coins.goblinPoints, 1) },
    ];
    m.zones = radio(this.zones.map((z) => z.name), this.zoneIndex);
    m.categories = radio(this.categories.map((c) => c.name), this.categoryIndex);
    const shown = this.displayPackages;
    m.packages = Array.from({ length: PACKAGES_PER_PAGE }, (_, i) => {
      const pack = shown[i];
      return pack ? { name: pack.name, price: `${convertGold(pack.price)} ${pack.priceUnit}`, shown: true } : { name: '', price: '', shown: false };
    });
    m.page = String(this.page);
    m.total_pages = String(this.totalPages);
    m.storage_tabs = radio([IGS_TEXT.storage, IGS_TEXT.giftInventory], this.storageBox);
    m.storage_rows = this.storageItems.map((item, i) => ({ name: item.count > 1 ? `${item.name}(${item.count})` : item.name, period: item.period, selected: i === this.selectedRow }));
    m.storage_page = String(this.storagePage);
    m.storage_total_pages = String(this.storageTotalPages);
    this.doc.update();
    this.drawPackages(shown);
  }

  // RenderItems: each shown package's item in its card.
  private drawPackages(shown: ShopPackage[]): void {
    const boxes = [...(this.doc?.root.querySelectorAll<HTMLElement>('.igs-package-item') ?? [])];
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    boxes.forEach((box, i) => {
      const code = shown[i]?.itemCode ?? -1;
      const signature = `${code}:${scale}`;
      if (box.dataset.drawn === signature) return;
      box.dataset.drawn = signature;
      box.replaceChildren();
      const item = itemOfCode(code);
      if (!item) return;
      const canvas = document.createElement('canvas');
      canvas.width = Math.round(108 * scale); canvas.height = Math.round(58 * scale);
      Object.assign(canvas.style, { width: '100%', height: '100%' });
      box.append(canvas);
      this.canvases.push(canvas);
      void drawItem(canvas, item);
    });
  }

  // ---------- packets (D2) ----------

  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xd2) return;
    const sub = bytes[3];
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    if (sub === 0x0c && bytes.length >= 10) {
      // CashShopScriptVersion: the shop can open (ShopOpenUnLock).
      this.scriptVersion = { zone: view.getUint16(4, true), year: view.getUint16(6, true), id: view.getUint16(8, true) };
    } else if (sub === 0x15 && bytes.length >= 10) {
      this.bannerVersion = { zone: view.getUint16(4, true), year: view.getUint16(6, true), id: view.getUint16(8, true) };
    } else if (sub === 0x02) {
      // CashShopOpenStateResponse: the points, the storage's first page, and the window.
      this.requestingOpen = false;
      if (u(bytes, 4) === 0) { this.options.onToggle?.(false); return; }
      this.send('CashShopPointInfoRequest');
      this.requestStorage(1);
      void this.show();
    } else if (sub === 0x01 && bytes.length >= 45) {
      const p = read<{ WCoinC: number; WCoinP: number; GoblinPoints: number }>('ServerToClient', 'CashShopPointInfo', bytes);
      this.coins = { wCoinC: p.WCoinC, wCoinP: p.WCoinP, goblinPoints: p.GoblinPoints };
      this.sync();
    } else if (sub === 0x03) {
      this.buyResult(u(bytes, 4));
    } else if (sub === 0x04) {
      this.giftResult(u(bytes, 4));
    } else if (sub === 0x06 && bytes.length >= 12) {
      this.initStorage(view.getUint16(4, true), view.getUint16(6, true), view.getUint16(10, true), view.getUint16(8, true));
    } else if ((sub === 0x0d || sub === 0x0e) && bytes.length >= 33) {
      if (!this.scriptVersion) return;
      const gift = sub === 0x0e;
      const p = gift ? read<{ SenderName: string; Message: string }>('ServerToClient', 'CashShopGiftStorageItem', bytes) : null;
      this.addStorageItem({
        storageSeq: view.getUint32(4, true), storageItemSeq: view.getUint32(8, true), groupCode: view.getUint32(12, true),
        productSeq: view.getUint32(16, true), priceSeq: view.getUint32(20, true), cashPoint: view.getFloat64(24, true),
        type: String.fromCharCode(u(bytes, 32)), sender: p?.SenderName ?? '', message: p?.Message ?? '',
      });
    } else if (sub === 0x0b) {
      this.useResult(u(bytes, 4));
    }
  }

  // ReceiveIGS_BuyItem.
  private buyResult(result: number): void {
    const failed = (text: string) => { void messageInGame(IGS_TEXT.purchaseFailed, text); };
    switch (result) {
      case 0xfe: failed(IGS_TEXT.dbFailed); break;
      case 0xff: failed(IGS_TEXT.dbError); break;
      case 0:
        void messageInGame(IGS_TEXT.purchaseCompleted, IGS_TEXT.purchaseMade);
        this.send('CashShopPointInfoRequest');
        this.requestStorage(1);
        break;
      case 1: failed(IGS_TEXT.notEnoughCoins); break;
      case 2: failed(IGS_TEXT.storageFull); break;
      case 3: failed(IGS_TEXT.soldOut); break;
      case 4: failed(IGS_TEXT.notAvailable); break;
      case 5: failed(IGS_TEXT.noLonger); break;
      case 6: failed(IGS_TEXT.cannotBuy); break;
      case 7: failed(IGS_TEXT.eventItems); break;
      case 8: failed(IGS_TEXT.eventLimit); break;
      case 9: failed(IGS_TEXT.wrongCoin); break;
      default: void messageInGame(IGS_TEXT.error2, IGS_TEXT.unknown);
    }
  }

  // ReceiveIGS_SendItemGift.
  private giftResult(result: number): void {
    const failed = (text: string) => { void messageInGame(IGS_TEXT.giftFailed, text); };
    switch (result) {
      case 0xfe: failed(IGS_TEXT.dbFailed); break;
      case 0xff: failed(IGS_TEXT.dbError); break;
      case 0: void messageInGame(IGS_TEXT.giftDelivered, IGS_TEXT.giftDone); this.send('CashShopPointInfoRequest'); break;
      case 1: failed(IGS_TEXT.notEnoughCash); break;
      case 2: failed(IGS_TEXT.recipientFull); break;
      case 3: failed(IGS_TEXT.noRecipient); break;
      case 4: failed(IGS_TEXT.soldOut); break;
      case 5: failed(IGS_TEXT.noLonger); break;
      case 6: void messageInGame(IGS_TEXT.error2, IGS_TEXT.noLonger); break;
      case 7: failed(IGS_TEXT.notGiftable); break;
      case 10: failed(IGS_TEXT.wrongCoin); break;
      default: void messageInGame(IGS_TEXT.error2, IGS_TEXT.unknown);
    }
  }

  // ReceiveIGS_UseStorageItem.
  private useResult(result: number): void {
    const failed = (text: string) => { void messageInGame(IGS_TEXT.failedToUse, text); };
    switch (result) {
      case 0xfe: failed(IGS_TEXT.dbAccess); break;
      case 0xff: failed(IGS_TEXT.anError); break;
      case 0: void messageInGame(IGS_TEXT.itemUsed, IGS_TEXT.hasBeenUsed); this.refreshStorage(); break;
      case 1: failed(IGS_TEXT.storageMissing); break;
      case 2: failed(IGS_TEXT.pcCafe); break;
      case 3: failed(IGS_TEXT.colorPlan); break;
      case 4: failed(IGS_TEXT.fixedPlan); break;
      case 21: failed(IGS_TEXT.inventoryFull); break;
      case 22: failed(IGS_TEXT.cannotUse); break;
      case 24: failed(IGS_TEXT.conflicting); break;
      default: void messageInGame(IGS_TEXT.error2, IGS_TEXT.unknown);
    }
  }
}
