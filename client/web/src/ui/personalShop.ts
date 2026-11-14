// The personal store, as the desktop client's CMyShopInventory (my_shop.rml), CPurchaseShopInventory
// (purchase_shop.rml), CPersonalShopTitleImp (the store titles over the sellers' heads) and their packet
// handlers (WSclient.cpp ReceiveCreateShopTitleViewport … ReceiveRefreshItemList, NotifySoldItem,
// NotifyClosePersonalShop). The own store is the 8 x 4 grid of inventory slots 204-235 next to the
// inventory: a bag item put on it asks its zen price first (PlayerShopSetItemPrice, then the move), a
// right click on a store item changes its price, the store name field and Open / Close send
// PlayerShopOpen / PlayerShopClose. Other players' stores open with /purchase (PlayerShopItemListRequest);
// a click on an item asks and buys it (PlayerShopItemBuyRequest).

import { BAG, Storage, decodeItem } from '../game/inventory.js';
import type { Inventory, Item, ItemGrid } from '../game/inventory.js';
import { itemDefinition, itemName } from '../game/items.js';
import { itemValue, loadItemValueData } from '../game/itemValue.js';
import { loadTooltipData, tooltipLines } from '../game/itemTooltip.js';
import type { TooltipCharacter, TooltipLine } from '../game/itemTooltip.js';
import type { Character } from '../game/characters.js';
import { read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import { ItemWindow, emptyCells, formatGold } from './itemWindows.js';
import type { ItemWindowModel, ItemWindows, PickedItem } from './itemWindows.js';
import { REFERENCE_HEIGHT, prepareDocument } from './rml.js';
import * as THREE from 'three';
import { nativeTextPx } from './nativeText.js';

// The desktop's texts (I18N::Game).
export const SHOP_TEXT = {
  personalStore: 'Personal store', close: 'Close', apply: 'Apply', open: 'Open', closed: 'Closed', warning: 'Warning!',
  stillOpening: 'Still opening', sellingPriceWhenOpening: 'Selling price when opening the store', pleaseVerify: 'Please verify.',
  alreadyInStore: 'Already in the personal store', cancelSold: 'Cancel sold item', cancelPurchased: 'Cancel purchased item',
  cantBeReturned: "Can't be returned.", allItemTrading: 'All item trading', zenOnly: 'can only be done using zen.',
  wantToOpen: 'Do you want to open a store?', noNameOrPrice: "There's no store name or item price.",
  notAllowed: 'This item is not allowed to use the private store.', enterPrice: 'Enter selling price.',
  sellingPriceZen: 'Selling price : %s zen', sellAtThisPrice: 'Do you want to sell item at this price?',
  wantToBuy: 'Do you want to buy an item?', notOpen: 'Store is not open at the moment.', failedToPurchase: 'Failed to purchase. Please try again.',
  shortOfZen: 'You are short of Zen.', inventoryFull: 'Inventory is full.', soldTo: 'Item was sold to %s.',
  otherClosed: 'The other character has closed the store.', sellingPrice: 'Selling Price: %s', rightClickForPrice: 'Right click for price setting',
  onlyAboveLevel: 'Only above level %d can use.', notInChaosCastle: "Can't be in Chaos Castle", cantBeOpened: "Store can't be opened.",
  canView: 'Can view personal store window.', cannotView: 'Cannot view personal store window.', store: '[Store]',
  openStoreTooltip: 'Open personal store(S)', closeStoreTooltip: 'Close personal store(S)',
  purchaseHint: 'Type /purchase <name>, or face a player, to see their store.',
} as const;

const format = (text: string, ...values: (string | number)[]): string => {
  let i = 0;
  return text.replace(/%[sd]/g, () => String(values[i++] ?? ''));
};

/** The first inventory slot of the own store (InventoryConstants.FirstStoreItemSlotIndex) and its 8 x 4 grid. */
export const STORE_GRID: ItemGrid = { firstSlot: 204, columns: 8, rows: 4 };
/** The level from which a store can be opened (CMyInventory: level 6). */
export const STORE_MIN_LEVEL = 6;
/** The browsed store's items: a client-side storage. */
const PURCHASE_STORAGE = 101;
/** The store name's length (iMAX_SHOPTITLE_MULTI - 1). */
const TITLE_MAX = 25;
const CHAOS_CASTLE_MAPS = [18, 19, 20, 21, 22, 23, 53];

const POTION = 14 * 512, HELPER = 13 * 512;
const typeOf = (item: Pick<Item, 'group' | 'number'>): number => item.group * 512 + item.number;

// GameLogic::Items::IsPersonalShopBan: the Sign of Lord may be sold, the Heart of Dark Lord and the Wizard's
// Ring above +0 never; otherwise the item data's "sellable in a personal store".
export function isPersonalShopBan(item: Item): boolean {
  const t = typeOf(item);
  if (t === POTION + 21 && item.level === 3) return false;
  if ((t === POTION + 11 && item.level === 13) || (t === HELPER + 20 && item.level !== 0)) return true;
  return itemDefinition(item.group, item.number)?.personalShopSellable === false;
}

/** A store price (GameLogic::PersonalShop::ItemPrice): zen, account coins or pieces of an item. */
export interface StorePrice { kind: 'zen' | 'coin' | 'item'; amount: number; currency: string }

const COINS: Record<number, string> = { 0xfff0: 'W Coin (C)', 0xfff1: 'W Coin (P)', 0xfff2: 'Goblin Points' };

// DecodeExtendedPrice: pieces of an item when an amount is given, account coins for the reserved types, else zen.
export function decodePrice(money: number, priceItemType: number, amount: number): StorePrice {
  if (amount > 0) {
    return { kind: 'item', amount, currency: itemName(priceItemType >> 9, priceItemType & 0x1ff) };
  }
  if (COINS[priceItemType]) return { kind: 'coin', amount: money, currency: COINS[priceItemType] };
  return { kind: 'zen', amount: money, currency: 'zen' };
}

/** A store's items (PlayerShopItemListExtended): from byte 55 a 9-byte price block, then the item. */
function decodeStoreItems(bytes: Uint8Array): { slot: number; item: Item; price: StorePrice }[] {
  const count = bytes[54] ?? 0;
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const result: { slot: number; item: Item; price: StorePrice }[] = [];
  let at = 55;
  for (let i = 0; i < count && at + 9 < bytes.length; i++) {
    const price = decodePrice(view.getUint32(at, true), view.getUint16(at + 4, true), view.getUint16(at + 6, true));
    const slot = bytes[at + 8];
    const item = decodeItem(bytes, at + 9);
    result.push({ slot, item, price });
    at += 9 + item.length;
  }
  return result;
}

const readName = (bytes: Uint8Array, at: number, length: number): string => {
  const slice = bytes.subarray(at, Math.min(at + length, bytes.length));
  const end = slice.indexOf(0);
  return new TextDecoder().decode(end >= 0 ? slice.subarray(0, end) : slice);
};

/** A price line of the tooltip (the desktop's colours by amount). */
function priceLines(price: StorePrice | undefined, sale: boolean, money: number): TooltipLine[] {
  const line = (text: string, color: string): TooltipLine => ({ text, color, bold: true, background: null, spacer: false });
  const spacer: TooltipLine = { text: '', color: 'white', bold: false, background: null, spacer: true };
  if (!price) return sale ? [line(SHOP_TEXT.rightClickForPrice, 'red'), spacer] : [];
  if (price.kind !== 'zen') return [line(format(SHOP_TEXT.sellingPrice, `${price.amount} ${price.currency}`), 'yellow'), spacer];
  const color = price.amount >= 10_000_000 ? 'red' : price.amount >= 1_000_000 ? 'yellow' : price.amount >= 100_000 ? 'green' : 'white';
  const lines = [line(format(SHOP_TEXT.sellingPrice, formatGold(price.amount)), color), spacer];
  if (!sale && money < price.amount) lines.push(line(SHOP_TEXT.shortOfZen, 'red'), spacer);
  return lines;
}

/** A question with a text field in the game (the desktop's GenericConfirmDialog with an input). Doesn't block. */
export function askInGame(lines: string[], { numeric = false, maxLength = 8, initial = '', keepOpen }: {
  numeric?: boolean; maxLength?: number; initial?: string; keepOpen?: (text: string) => boolean;
} = {}): Promise<string | null> {
  return new Promise((resolve) => {
    const box = document.createElement('div');
    box.className = 'chip confirm-dialog ask-dialog';
    box.setAttribute('role', 'dialog');
    Object.assign(box.style, {
      position: 'fixed', left: '50%', top: '35%', transform: 'translate(-50%, -50%)', zIndex: '95', pointerEvents: 'auto',
      display: 'grid', gap: '10px', padding: '16px', minWidth: 'min(320px, calc(100vw - 32px))', textAlign: 'center',
    });
    const text = document.createElement('div');
    text.style.whiteSpace = 'pre-line';
    text.textContent = lines.join('\n');
    const input = document.createElement('input');
    input.className = 'ask-input';
    input.maxLength = maxLength;
    input.value = initial;
    if (numeric) { input.inputMode = 'numeric'; input.addEventListener('input', () => { input.value = input.value.replace(/\D/g, ''); }); }
    const buttons = document.createElement('div');
    buttons.className = 'buttons';
    const ok = document.createElement('button');
    ok.type = 'button'; ok.className = 'primary confirm-ok'; ok.textContent = 'OK';
    const cancel = document.createElement('button');
    cancel.type = 'button'; cancel.className = 'confirm-cancel'; cancel.textContent = 'Cancel';
    buttons.append(ok, cancel);
    box.append(text, input, buttons);
    const done = (answer: string | null) => { box.remove(); window.removeEventListener('keydown', onKey, true); resolve(answer); };
    const accept = () => { if (!keepOpen?.(input.value)) done(input.value); };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); accept(); }
      else if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); done(null); }
      else event.stopPropagation(); // typing stays in the field (no hotkeys)
    };
    ok.addEventListener('click', accept);
    cancel.addEventListener('click', () => done(null));
    box.addEventListener('pointerdown', (event) => event.stopPropagation());
    window.addEventListener('keydown', onKey, true);
    document.body.append(box);
    input.focus();
  });
}

// my_shop.rml's data model (MyShopRmlModel).
interface MyShopModel extends ItemWindowModel {
  shop_title: string; exit_tooltip: string; open_locked: boolean; open_tooltip: string; close_locked: boolean; close_tooltip: string;
  show_still_opening: boolean; still_opening_text: string; warning_text: string; selling_price_text: string; please_verify_text: string;
  already_in_store_text: string; cancel_sold_text: string; cant_be_returned_text: string; all_item_trading_text: string;
  can_only_be_done_using_zen_text: string;
  my_shop_exit_click: () => void; my_shop_open_click: () => void; my_shop_close_click: () => void;
}

/** The own store (CMyShopInventory): the store slots of the inventory, the name field and Open / Close. */
export class MyShopWindow extends ItemWindow<MyShopModel> {
  /** A bag item put on the grid (MyShopInventoryProcess): true when it was taken (the price dialog follows). */
  onPlace?: (from: { storage: number; slot: number; item: Item }, target: number) => boolean;
  /** A right click on a store item: its price. */
  onPriceClick?: (slot: number) => void;

  constructor({ inventory, picked, onClose, onOpen, onCloseStore }: {
    inventory: Inventory; picked: PickedItem; onClose: () => void; onOpen: () => void; onCloseStore: () => void;
  }) {
    const T = SHOP_TEXT;
    const model: MyShopModel = {
      text_px: 12, title: T.personalStore, grid_cells: emptyCells(STORE_GRID.columns * STORE_GRID.rows), window_close: () => onClose(),
      shop_title: '', exit_tooltip: T.close, open_locked: false, open_tooltip: T.open, close_locked: true, close_tooltip: T.closed,
      show_still_opening: false, still_opening_text: T.stillOpening, warning_text: T.warning, selling_price_text: T.sellingPriceWhenOpening,
      please_verify_text: T.pleaseVerify, already_in_store_text: T.alreadyInStore, cancel_sold_text: T.cancelSold,
      cant_be_returned_text: T.cantBeReturned, all_item_trading_text: T.allItemTrading, can_only_be_done_using_zen_text: T.zenOnly,
      my_shop_exit_click: () => onClose(), my_shop_open_click: () => onOpen(), my_shop_close_click: () => onCloseStore(),
    };
    super({ inventory, picked, documentName: 'my_shop', grid: STORE_GRID, storage: Storage.PLAYER_SHOP, model });
  }

  override async open(parent: HTMLElement): Promise<void> {
    const wasOpen = this.isOpen;
    await super.open(parent);
    if (wasOpen || !this.doc) return;
    // The name field keeps its keys (no hotkeys, no chat) and its length (ApplyShopTitleLimit).
    const field = this.doc.$<HTMLInputElement>('#shop_title');
    if (field) {
      field.maxLength = TITLE_MAX;
      field.style.pointerEvents = 'auto';
      for (const type of ['keydown', 'keyup', 'keypress'] as const) field.addEventListener(type, (event) => event.stopPropagation());
    }
  }

  override boxOf(slot: number, item: Item) {
    if (slot < STORE_GRID.firstSlot || slot >= STORE_GRID.firstSlot + STORE_GRID.columns * STORE_GRID.rows) return null;
    return super.boxOf(slot, item);
  }

  override click(event: MouseEvent): void {
    if (event.target instanceof Element && event.target.closest('#frame_corner_close, .shop-btn, #shop_title')) return;
    const source = this.picked.source;
    // An item from the bag or the equipment: its price first (the dialog), then the move.
    if (source && !(source.storage === Storage.INVENTORY && source.slot >= STORE_GRID.firstSlot)) {
      const target = this.dropSlotFor(this.pointOf(event), source.item);
      if (!target || target.equipment || !this.inventory.fits(this.items, this.grid, target.slot, source.item, null)) return;
      if (this.onPlace?.(source, target.slot)) { this.picked.drop(); this.onPickedChanged?.(); }
      return;
    }
    super.click(event);
  }

  override rightClick(event: MouseEvent): void {
    if (this.picked.source || this.inventory.pendingMove) return;
    const slot = this.itemAtGridSlot(this.gridSlotAt(this.pointOf(event)));
    if (slot !== null) this.onPriceClick?.(slot);
  }
}

// purchase_shop.rml's data model (PurchaseShopRmlModel).
interface PurchaseModel extends ItemWindowModel {
  shop_owner_text: string; warning_label: string; selling_price_line: string; verify_line: string; already_in_store_line: string;
  cancel_purchased_line: string; cant_be_returned_line: string; all_item_trading_line: string; zen_only_line: string;
  purchase_shop_exit_click: () => void;
}

/** Another player's store (CPurchaseShopInventory): a click on an item buys it after asking. */
export class PurchaseShopWindow extends ItemWindow<PurchaseModel> {
  /** The store's items by the seller's inventory slot. */
  readonly stock = new Map<number, Item>();
  onBuy?: (slot: number, item: Item) => void;

  constructor({ inventory, picked, onClose }: { inventory: Inventory; picked: PickedItem; onClose: () => void }) {
    const T = SHOP_TEXT;
    const model: PurchaseModel = {
      text_px: 12, title: T.personalStore, grid_cells: emptyCells(STORE_GRID.columns * STORE_GRID.rows), window_close: () => onClose(),
      shop_owner_text: '', warning_label: T.warning, selling_price_line: T.sellingPriceWhenOpening, verify_line: T.pleaseVerify,
      already_in_store_line: T.alreadyInStore, cancel_purchased_line: T.cancelPurchased, cant_be_returned_line: T.cantBeReturned,
      all_item_trading_line: T.allItemTrading, zen_only_line: T.zenOnly, purchase_shop_exit_click: () => onClose(),
    };
    super({ inventory, picked, documentName: 'purchase_shop', grid: STORE_GRID, storage: PURCHASE_STORAGE, model });
  }

  override get items(): Map<number, Item> { return this.stock; }

  override click(event: MouseEvent): void {
    if (event.target instanceof Element && event.target.closest('#frame_corner_close, .shop-btn-exit')) return;
    if (this.picked.source) return; // the store is locked (LockInventory)
    const slot = this.itemAtGridSlot(this.gridSlotAt(this.pointOf(event)));
    const item = slot === null ? undefined : this.stock.get(slot);
    if (slot !== null && item) this.onBuy?.(slot, item);
  }

  override rightClick(): void {}
}

/** The store title boxes over the sellers' heads (CPersonalShopTitleImp::Draw). */
class ShopTitles {
  private readonly layer: HTMLDivElement;
  private readonly boxes = new Map<number, HTMLDivElement>();
  /** Whether titles are shown (/View store on, /View store off). */
  shown = true;

  constructor(private readonly options: {
    camera: THREE.Camera; canvas: HTMLCanvasElement; root: THREE.Object3D;
    characterOf: (id: number) => Character | undefined; onClick: (id: number) => void;
  }) {
    this.layer = document.createElement('div');
    this.layer.className = 'shop-titles';
    Object.assign(this.layer.style, { position: 'fixed', inset: '0', pointerEvents: 'none', zIndex: '14', overflow: 'hidden' });
    document.body.append(this.layer);
    const frame = () => { this.update(); requestAnimationFrame(frame); };
    requestAnimationFrame(frame);
  }

  set(id: number, title: string): void {
    let box = this.boxes.get(id);
    if (!box) {
      box = document.createElement('div');
      box.className = 'shop-title';
      box.dataset.id = String(id);
      Object.assign(box.style, {
        position: 'absolute', left: '0', top: '0', pointerEvents: 'auto', cursor: 'pointer', whiteSpace: 'nowrap',
        fontFamily: 'Tahoma, Arial, sans-serif', lineHeight: '1.2', display: 'none',
      });
      box.addEventListener('pointerdown', (event) => { event.stopPropagation(); this.options.onClick(id); });
      this.layer.append(box);
      this.boxes.set(id, box);
    }
    box.dataset.title = title;
    box.dataset.name = '';
    box.title = title;
  }

  // The first line: "[Store]" and the seller's name in bold; then the title in one or two lines (SeparateShopTitle).
  private fill(box: HTMLDivElement, name: string): void {
    const title = box.dataset.title ?? '';
    box.dataset.name = name;
    const [top, bottom] = splitTitle(title);
    const span = (text: string, style: Partial<CSSStyleDeclaration>) => {
      const element = document.createElement('span');
      element.textContent = text;
      Object.assign(element.style, style);
      return element;
    };
    const head = document.createElement('div');
    head.append(span(SHOP_TEXT.store, { background: 'rgba(108,57,41,0.5)', color: 'rgb(250,150,0)', fontWeight: 'bold' }),
      span(name, { background: 'rgba(108,57,41,0.5)', color: 'rgb(230,230,0)', fontWeight: 'bold' }));
    const lines = [head];
    for (const text of [top, bottom].filter(Boolean)) {
      const row = document.createElement('div');
      row.append(span(text, { background: 'rgba(108,57,41,0.78)', color: '#fff' }));
      lines.push(row);
    }
    box.replaceChildren(...lines);
  }

  remove(id: number): void {
    this.boxes.get(id)?.remove();
    this.boxes.delete(id);
  }

  has(id: number): boolean { return this.boxes.has(id); }
  ids(): number[] { return [...this.boxes.keys()]; }

  private update(): void {
    const { camera, canvas, root } = this.options;
    const rect = canvas.getBoundingClientRect();
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    const point = new THREE.Vector3();
    for (const [id, box] of this.boxes) {
      const character = this.options.characterOf(id);
      if (!character || !this.shown) { box.style.display = 'none'; continue; }
      if (box.dataset.name !== character.name) this.fill(box, character.name);
      // Over the head (the bounding box's top + 60), then 36 reference pixels up (CalculateBooleanPos).
      point.set(character.group.position.x, character.group.position.y, character.group.position.z + 240);
      root.localToWorld(point);
      point.project(camera);
      if (point.z > 1) { box.style.display = 'none'; continue; }
      box.style.display = 'block';
      box.style.fontSize = `${nativeTextPx()}px`;
      box.style.transform = `translate(${rect.left + (point.x + 1) / 2 * rect.width}px, ${rect.top + (1 - point.y) / 2 * rect.height - 12 * scale}px) translate(-50%, -100%)`;
    }
  }
}

// CutText: a long title in two lines, broken at a space near the middle.
function splitTitle(title: string): [string, string] {
  if (title.length <= 14) return [title, ''];
  const middle = Math.ceil(title.length / 2);
  let cut = title.lastIndexOf(' ', middle);
  if (cut <= 0) cut = middle;
  return [title.slice(0, cut).trim(), title.slice(cut).trim()];
}

export interface PersonalShopOptions {
  connection: Connection;
  inventory: Inventory;
  itemWindows: ItemWindows;
  /** A line for the chat (the desktop's system log). */
  message: (text: string) => void;
  /** A yes / no question in the game. */
  confirm: (text: string) => Promise<boolean>;
  /** The hero's level, name and object id. */
  level: () => number;
  heroName: string;
  heroId: () => number;
  /** The characters in view by object id, and the hero. */
  characters: () => Map<number, Character>;
  hero: () => Character;
  mapNumber: () => number;
  /** For the store titles over the heads. */
  camera: THREE.Camera;
  canvas: HTMLCanvasElement;
  root: THREE.Object3D;
  /** What the tooltip reads of the hero. */
  tooltipCharacter?: () => TooltipCharacter | null;
}

/** The personal store: the own store, the stores of others, the titles and the packets. */
export class PersonalShop {
  readonly myShop: MyShopWindow;
  readonly purchase: PurchaseShopWindow;
  /** The own store's prices by inventory slot (PSHOPWNDTYPE_SALE). */
  readonly prices = new Map<number, StorePrice>();
  /** The browsed store's prices (PSHOPWNDTYPE_PURCHASE). */
  readonly buyPrices = new Map<number, StorePrice>();
  /** Whether the own store is open (m_EnablePersonalShop) and its name (g_szPersonalShopTitle). */
  isOpen = false;
  title = '';
  /** The seller whose store is shown. */
  seller: { id: number; name: string } | null = null;
  /** The store slot of the last buy request (GetSourceIndex). */
  private buyingSlot = -1;
  /** The slot and old price of the latest price change (RememberReplacedOwnShopPrice). */
  private replaced: { slot: number; price: StorePrice | undefined } | null = null;
  /** The target slot of the item being priced (GetTargetIndex). */
  private pricingTarget = -1;
  private readonly titles: ShopTitles;
  private readonly options: PersonalShopOptions;

  constructor(options: PersonalShopOptions) {
    this.options = options;
    const { inventory, itemWindows, connection } = options;
    this.myShop = new MyShopWindow({
      inventory, picked: itemWindows.picked, onClose: () => this.closeMyShop(),
      onOpen: () => { void this.openButton(); }, onCloseStore: () => this.closeStore(),
    });
    this.purchase = new PurchaseShopWindow({ inventory, picked: itemWindows.picked, onClose: () => itemWindows.closeAll() });
    itemWindows.dock(this.myShop, () => { this.closeMyShop(false); return true; });
    itemWindows.dock(this.purchase, () => { this.closePurchase(); return true; });
    this.myShop.onPlace = (from, target) => this.place(from, target);
    this.myShop.onPriceClick = (slot) => { void this.changePrice(slot); };
    this.purchase.onBuy = (slot, item) => { void this.buy(slot, item); };
    // The tooltips show the price (or that it needs one) above the item's lines.
    this.myShop.onHover = (item) => this.tooltip(this.myShop, item, true);
    this.purchase.onHover = (item) => this.tooltip(this.purchase, item, false);
    this.titles = new ShopTitles({
      camera: options.camera, canvas: options.canvas, root: options.root,
      characterOf: (id) => (id === options.heroId() ? options.hero() : options.characters().get(id)),
      onClick: (id) => { if (id !== options.heroId()) this.requestList(id); },
    });
    this.hookInventoryButton();
    // S with the inventory open (CMyInventory::UpdateKeyEvent) opens or closes the own store.
    window.addEventListener('keydown', (event) => {
      if (event.target instanceof Element && event.target.closest('input, textarea')) return;
      if (event.key.toLowerCase() !== 's' || event.ctrlKey || event.altKey || event.metaKey) return;
      if (!itemWindows.bag.isOpen || !this.canOpenMyShop() || options.level() < STORE_MIN_LEVEL) return;
      event.preventDefault();
      if (this.myShop.isOpen) this.closeMyShop(); else void this.showMyShop();
    });
    void prepareDocument('my_shop').then(() => prepareDocument('purchase_shop')).catch((error: unknown) => console.warn('Interface', error));
    void loadItemValueData().catch(() => undefined);
    connection.onPacket((bytes) => this.handle(bytes));
  }

  // The inventory's store button (my_inventory_myshop_click) and when it shows (level 6, no other window open).
  private hookInventoryButton(): void {
    const { itemWindows, level } = this.options;
    const bag = itemWindows.bag;
    const update = bag.updateModel.bind(bag);
    bag.updateModel = () => {
      update();
      bag.model.myshop_visible = level() >= STORE_MIN_LEVEL && this.canOpenMyShop();
      bag.model.myshop_mode_open = !this.myShop.isOpen;
      bag.model.myshop_tooltip = this.myShop.isOpen ? SHOP_TEXT.closeStoreTooltip : SHOP_TEXT.openStoreTooltip;
    };
    bag.model.my_inventory_myshop_click = () => {
      if (this.myShop.isOpen) this.closeMyShop(); else void this.showMyShop();
    };
  }

  /** CanOpenMyShopInterface: not while a merchant, the vault, a mix or a trade is open (nor in Chaos Castle). */
  canOpenMyShop(): boolean {
    const w = this.options.itemWindows;
    return !w.shop.isOpen && !w.vault.isOpen && !w.trade.isOpen && !w.docked.some((d) => d.window.isOpen && d.window !== this.myShop && d.window !== this.purchase)
      && !CHAOS_CASTLE_MAPS.includes(this.options.mapNumber());
  }

  /** Shows the own store next to the inventory (CSystem::Show(INTERFACE_MYSHOP_INVENTORY), HideAllGroupA). */
  async showMyShop(): Promise<void> {
    const { itemWindows } = this.options;
    if (this.purchase.isOpen) this.closePurchase();
    await prepareDocument('my_shop');
    await itemWindows.bag.open(itemWindows.parent);
    await this.myShop.open(itemWindows.parent);
    this.myShop.model.shop_title = this.title;
    this.syncButtons();
    itemWindows.placeAll();
  }

  /** ClosingProcess: an item on the cursor goes back. `closeBag` also closes the inventory (the X button). */
  closeMyShop(closeBag = false): void {
    const { itemWindows } = this.options;
    if (itemWindows.picked.source) itemWindows.picked.drop();
    this.myShop.close();
    itemWindows.tooltip.hide();
    if (closeBag) itemWindows.closeAll();
    else itemWindows.refresh();
  }

  private closePurchase(): void {
    this.purchase.close();
    this.purchase.stock.clear();
    this.buyPrices.clear();
    this.seller = null;
  }

  private syncButtons(): void {
    const m = this.myShop.model;
    m.open_locked = false;
    m.open_tooltip = this.isOpen ? SHOP_TEXT.apply : SHOP_TEXT.open;
    m.close_locked = !this.isOpen;
    m.show_still_opening = this.isOpen;
    this.options.itemWindows.refresh();
  }

  private tooltip(window: ItemWindow<ItemWindowModel>, item: Item | null, sale: boolean): void {
    const { itemWindows, inventory } = this.options;
    const slot = window.hoverSlot;
    const box = item && !itemWindows.picked.source && slot !== null ? window.screenBoxOf(slot) : null;
    if (!item || !box || slot === null) { itemWindows.tooltip.hide(); return; }
    void loadTooltipData().then(() => {
      if (window.hoverSlot !== slot) return;
      const price = (sale ? this.prices : this.buyPrices).get(slot);
      const lines = isPersonalShopBan(item) ? [] : priceLines(price, sale, inventory.money);
      void itemWindows.tooltip.showLines([...lines, ...tooltipLines(item, this.options.tooltipCharacter?.() ?? null)], box, item);
    });
  }

  /** IsExistUndecidedPrice: a store item without a price. */
  hasUndecidedPrice(): boolean {
    for (const slot of this.options.inventory.items.keys()) {
      if (slot >= STORE_GRID.firstSlot && slot < STORE_GRID.firstSlot + 32 && !this.prices.has(slot)) return true;
    }
    return false;
  }

  // my_shop_open_click: with a name and every price set, asks once and sends PlayerShopOpen; an open store's
  // name is changed at once. The windows close.
  private async openButton(): Promise<void> {
    if (this.myShop.model.open_locked) return;
    const title = this.myShop.model.shop_title.trim().slice(0, TITLE_MAX);
    if (!title || this.hasUndecidedPrice()) { this.options.message(SHOP_TEXT.noNameOrPrice); return; }
    if (!this.isOpen && !(await this.options.confirm(SHOP_TEXT.wantToOpen))) return;
    this.title = title;
    this.options.connection.send('ClientToServer', 'PlayerShopOpen', { StoreName: title });
    this.closeMyShop(true);
  }

  /** The Close button (my_shop_close_click): PlayerShopClose. */
  closeStore(): void {
    if (!this.isOpen) return;
    this.options.connection.send('ClientToServer', 'PlayerShopClose', {});
    this.closeMyShop(true);
  }

  // MyShopInventoryProcess: a bag or worn item put on the store grid asks its price first.
  private place(from: { storage: number; slot: number; item: Item }, target: number): boolean {
    if (isPersonalShopBan(from.item)) { this.options.message(SHOP_TEXT.notAllowed); return true; }
    this.pricingTarget = target;
    void this.askPrice(from.item).then((price) => {
      if (price === null) return;
      // ApplyPersonalShopItemPrice: an open store closes first; the price goes with the item's place in the bag.
      if (this.isOpen) this.options.connection.send('ClientToServer', 'PlayerShopClose', {});
      this.sendPrice(from.slot, price);
      this.options.inventory.move(from.storage, from.slot, Storage.PLAYER_SHOP, target);
      this.prices.set(target, { kind: 'zen', amount: price, currency: 'zen' });
      this.options.itemWindows.refresh();
    });
    return true;
  }

  // A right click on a store item: a new price for it.
  private async changePrice(slot: number): Promise<void> {
    const item = this.options.inventory.items.get(slot);
    if (!item) return;
    this.pricingTarget = -1;
    const price = await this.askPrice(item);
    if (price === null) return;
    if (this.isOpen) this.options.connection.send('ClientToServer', 'PlayerShopClose', {});
    this.replaced = { slot, price: this.prices.get(slot) };
    this.sendPrice(slot, price);
    this.prices.set(slot, { kind: 'zen', amount: price, currency: 'zen' });
    this.options.itemWindows.refresh();
  }

  // ShowItemPriceDialog: "Enter selling price." (up to 8 digits, 0 keeps the dialog open); a zen price under the
  // item's value asks again (ShowZenPriceConfirmation).
  private async askPrice(item: Item): Promise<number | null> {
    const text = await askInGame([SHOP_TEXT.enterPrice], { numeric: true, maxLength: 8, keepOpen: (value) => !(Number(value) > 0) });
    if (text === null) return null;
    const price = Number(text);
    let value = 0;
    try { value = itemValue(item, 2); } catch { value = 0; }
    if (price < value && !(await this.options.confirm(`${format(SHOP_TEXT.sellingPriceZen, formatGold(price))}\n${SHOP_TEXT.sellAtThisPrice}`))) return null;
    return price;
  }

  private sendPrice(slot: number, price: number): void {
    this.options.connection.send('ClientToServer', 'PlayerShopSetItemPrice', { ItemSlot: slot, Price: price });
  }

  /** Asks for a player's store (PlayerShopItemListRequest). */
  requestList(id: number): void {
    const character = this.options.characters().get(id);
    if (!character) return;
    if (!this.canOpenMyShop() && !this.purchase.isOpen) { this.options.message(SHOP_TEXT.cantBeOpened); return; }
    this.options.connection.send('ClientToServer', 'PlayerShopItemListRequest', { PlayerId: id, PlayerName: character.name });
  }

  // A click on a store item: the question, then PlayerShopItemBuyRequest.
  private async buy(slot: number, item: Item): Promise<void> {
    const seller = this.seller;
    if (!seller) return;
    this.buyingSlot = slot;
    if (!(await this.options.confirm(`${SHOP_TEXT.wantToBuy}\n${itemNameOf(item)}`))) return;
    if (this.seller?.id !== seller.id || !this.purchase.stock.has(slot)) return;
    this.options.connection.send('ClientToServer', 'PlayerShopItemBuyRequest', { PlayerId: seller.id, PlayerName: seller.name, ItemSlot: slot });
  }

  /**
   * The chat commands: /personalshop opens the own store (level 6), /purchase [name] the store of the named or
   * facing player, /view store on|off shows or hides the titles. True when the text was one of them.
   */
  command(text: string, facing: () => Character | null): boolean {
    const lower = text.trim().toLowerCase();
    if (lower === '/personal store' || lower === '/personalshop') {
      if (CHAOS_CASTLE_MAPS.includes(this.options.mapNumber())) { this.options.message(SHOP_TEXT.notInChaosCastle); return true; }
      if (this.options.level() >= STORE_MIN_LEVEL) void this.showMyShop();
      else this.options.message(format(SHOP_TEXT.onlyAboveLevel, STORE_MIN_LEVEL));
      return true;
    }
    if (lower.startsWith('/purchase') || lower.startsWith('/buy')) {
      if (CHAOS_CASTLE_MAPS.includes(this.options.mapNumber())) { this.options.message(SHOP_TEXT.notInChaosCastle); return true; }
      const w = this.options.itemWindows;
      if (w.shop.isOpen || w.vault.isOpen || w.trade.isOpen || w.docked.some((d) => d.window.isOpen && d.window !== this.myShop && d.window !== this.purchase)) {
        this.options.message(SHOP_TEXT.cantBeOpened);
        return true;
      }
      const name = text.trim().split(/\s+/)[1];
      const players = [...this.options.characters().values()].filter((c) => c.kind === 'player');
      const target = name ? players.find((c) => c.name.toLowerCase() === name.toLowerCase()) : facing();
      if (target) this.requestList(target.id);
      else this.options.message(SHOP_TEXT.purchaseHint);
      return true;
    }
    if (lower === '/view store on') { this.titles.shown = true; this.options.message(SHOP_TEXT.canView); return true; }
    if (lower === '/view store off') { this.titles.shown = false; this.options.message(SHOP_TEXT.cannotView); return true; }
    return false;
  }

  private nameOf(id: number): string {
    return id === this.options.heroId() ? this.options.heroName : this.options.characters().get(id)?.name ?? '';
  }

  // AddShopTitle: the box over the seller's head (drawn once the seller is in view).
  private showTitle(id: number, title: string): void {
    this.titles.set(id, title);
  }

  // ---------- packets ----------

  handle(bytes: Uint8Array): void {
    const c1 = bytes[0] === 0xc1, c2 = bytes[0] === 0xc2, c3 = bytes[0] === 0xc3;
    // Players leaving the view lose their title; the store being browsed closes (ReceiveDeleteCharacterViewport).
    if (c1 && bytes[2] === 0x14) { this.outOfView(bytes); return; }
    if (c3 && bytes[2] === 0x1c && bytes[3] === 0x0f) { this.mapChanged(); return; }
    const code = c2 ? bytes[3] : bytes[2];
    if (code !== 0x3f) return;
    const sub = c2 ? bytes[4] : bytes[3];
    const options = this.options;
    if (c2 && sub === 0x00) {
      // PlayerShops: the stores in view (ReceiveCreateShopTitleViewport).
      const packet = read<{ Shops: { PlayerId: number; StoreName: string }[] }>('ServerToClient', 'PlayerShops', bytes);
      for (const shop of packet.Shops ?? []) {
        const id = shop.PlayerId & 0x7fff;
        if (id === options.heroId()) { this.title = shop.StoreName; this.isOpen = true; this.myShop.model.shop_title = shop.StoreName; this.syncButtons(); }
        this.showTitle(id, shop.StoreName);
      }
    } else if (c2 && (sub === 0x05 || sub === 0x13)) {
      this.itemList(bytes, sub === 0x05);
    } else if (sub === 0x01) {
      // PlayerShopSetItemPriceResponse (ReceiveSetPriceResult): refused prices go back to what they were.
      const result = bytes[4], slot = bytes[5];
      if (result === 1) { if (this.replaced?.slot === slot) this.replaced = null; return; }
      if (this.replaced?.slot === slot) {
        if (this.replaced.price) this.prices.set(slot, this.replaced.price); else this.prices.delete(slot);
        this.replaced = null;
      } else if (this.pricingTarget >= 0) this.prices.delete(this.pricingTarget);
      options.itemWindows.refresh();
    } else if (sub === 0x02) {
      // PlayerShopOpenSuccessful (ReceiveCreatePersonalShop).
      if (bytes[4] === 1) { this.isOpen = true; this.showTitle(options.heroId(), this.title); this.syncButtons(); }
    } else if (sub === 0x03) {
      // PlayerShopClosed (ReceiveDestroyPersonalShop).
      if (bytes[4] !== 1) return;
      const id = ((bytes[5] << 8) | bytes[6]) & 0x7fff;
      if (id === options.heroId()) { this.isOpen = false; this.syncButtons(); }
      this.titles.remove(id);
    } else if (sub === 0x06) {
      this.buyResult(bytes);
    } else if (sub === 0x08) {
      // PlayerShopItemSoldToPlayer (NotifySoldItem).
      options.message(format(SHOP_TEXT.soldTo, readName(bytes, 5, 10)));
      this.prices.delete(bytes[4]);
    } else if (sub === 0x10) {
      // PlayerShopTitleChanged (ReceiveShopTitleChange).
      const packet = read<{ PlayerId: number; ShopTitle: string; PlayerName: string }>('ServerToClient', 'PlayerShopTitleChanged', bytes);
      const id = packet.PlayerId & 0x7fff;
      const name = this.nameOf(id);
      if (!name || name === packet.PlayerName) this.showTitle(id, packet.ShopTitle);
    } else if (sub === 0x12) {
      // ClosePlayerShopDialog (NotifyClosePersonalShop).
      if (this.purchase.isOpen) { options.itemWindows.closeAll(); options.message(SHOP_TEXT.otherClosed); }
    }
  }

  // PlayerShopItemListExtended: the store asked for opens (ReceivePersonalShopItemList), an update refreshes the own
  // prices (after entering the world with an open store) or the store shown (ReceiveRefreshItemList).
  private itemList(bytes: Uint8Array, byRequest: boolean): void {
    const { itemWindows, heroId, message } = this.options;
    const success = bytes[5] === 1;
    const id = ((bytes[6] << 8) | bytes[7]) & 0x7fff;
    if (!success) { if (byRequest) message(SHOP_TEXT.notOpen); return; }
    const name = readName(bytes, 8, 10), title = readName(bytes, 18, 36);
    const items = decodeStoreItems(bytes);
    if (!byRequest && id === heroId()) {
      for (const { slot, price } of items) this.prices.set(slot, price);
      this.isOpen = true;
      this.title = title;
      this.myShop.model.shop_title = title;
      this.showTitle(id, title);
      this.syncButtons();
      return;
    }
    if (!byRequest) {
      if (!this.purchase.isOpen || this.seller?.id !== id) return;
      this.purchase.stock.clear();
      this.buyPrices.clear();
      for (const { slot, item, price } of items) { this.purchase.stock.set(slot, item); this.buyPrices.set(slot, price); }
      itemWindows.refresh();
      return;
    }
    void this.openPurchase(id, name, title, items);
  }

  // OpenBrowsedShop: the vault and the inventory close, the store opens with the inventory.
  private async openPurchase(id: number, name: string, title: string, items: { slot: number; item: Item; price: StorePrice }[]): Promise<void> {
    const { itemWindows } = this.options;
    if (items.some(({ price }) => price.amount <= 0)) { itemWindows.closeAll(); return; }
    if (this.myShop.isOpen) this.closeMyShop();
    if (this.purchase.isOpen) this.closePurchase();
    if (itemWindows.vault.isOpen) itemWindows.closeAll();
    await prepareDocument('purchase_shop');
    this.purchase.stock.clear();
    this.buyPrices.clear();
    for (const entry of items) { this.purchase.stock.set(entry.slot, entry.item); this.buyPrices.set(entry.slot, entry.price); }
    this.seller = { id, name };
    this.purchase.model.shop_owner_text = title;
    await itemWindows.bag.open(itemWindows.parent);
    await this.purchase.open(itemWindows.parent);
    itemWindows.placeAll();
  }

  // PlayerShopBuyResultExtended (ReceivePurchaseItem): the item into the bag, or why not.
  private buyResult(bytes: Uint8Array): void {
    const { inventory, itemWindows, message } = this.options;
    const result = bytes[6], slot = bytes[7];
    if (result === 1) {
      if (this.purchase.isOpen) { this.purchase.stock.delete(this.buyingSlot); this.buyPrices.delete(this.buyingSlot); }
      else this.buyPrices.clear();
      if (bytes.length > 8 && slot >= BAG.firstSlot && slot < STORE_GRID.firstSlot) {
        inventory.items.set(slot, decodeItem(bytes, 8));
        inventory.changed('inventory');
      }
      itemWindows.refresh();
    } else if (result === 6) {
      message(SHOP_TEXT.failedToPurchase);
      if (this.myShop.isOpen) this.closeMyShop();
      if (this.purchase.isOpen) itemWindows.closeAll();
    } else if (result === 7) message(SHOP_TEXT.shortOfZen);
    else if (result === 8) message(SHOP_TEXT.inventoryFull);
  }

  private outOfView(bytes: Uint8Array): void {
    const packet = read<{ Objects: { Id: number }[] }>('ServerToClient', 'MapObjectOutOfScope', bytes);
    for (const { Id } of packet.Objects ?? []) {
      const id = Id & 0x7fff;
      if (this.seller?.id === id && this.purchase.isOpen) this.options.itemWindows.closeAll();
      this.titles.remove(id);
    }
  }

  // RemoveAllShopTitleExceptHero on a map change.
  private mapChanged(): void {
    const hero = this.options.heroId();
    for (const id of this.titles.ids()) if (id !== hero) this.titles.remove(id);
    if (this.purchase.isOpen) this.options.itemWindows.closeAll();
  }

  /** Whether a player has an open store in view (IsShopInViewport). */
  hasStore(id: number): boolean { return this.titles.has(id); }
}

function itemNameOf(item: Item): string {
  const name = itemName(item.group, item.number);
  return item.level > 0 ? `${name} +${item.level}` : name;
}
