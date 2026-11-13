// The inventory (my_inventory.rml) and the vault (storage.rml), as the desktop client's
// CMyInventory and CStorageInventory: the equipment slots and the 8 x 8 bag, the vault's 8 x 15
// grid, the money, and moving items by clicking an item to pick it up and clicking where it goes.

import { openDocument, prepareDocument, REFERENCE_HEIGHT } from './rml.js';
import type { RmlDocument, RmlModel } from './rml.js';
import { drawItem } from './itemIcons.js';
import { BAG, VAULT, SHOP, SHOP_STORAGE, Equipment, Inventory, NpcWindow, Storage } from '../game/inventory.js';
import type { Item, ItemGrid } from '../game/inventory.js';
import { itemName } from '../game/items.js';
import { ItemTooltipView, type ScreenBox } from './itemTooltipView.js';
import type { TooltipCharacter } from '../game/itemTooltip.js';
import { isHighValue, isSellingBan } from '../game/itemUse.js';

const CELL = 20; // the grid's cell pitch in reference pixels (item_grid.rcss --item-cell)
const PANEL = { width: 190, height: 429 };

// A point or a box in panel reference px.
interface Point { x: number; y: number }
interface Box extends Point { w: number; h: number }
// Where a picked item came from.
interface PickedSource { storage: number; slot: number; item: Item }

// A cell of the grid as the RML draws it: the tint under an item, the drop preview, the stack count.
export interface GridCell { tint: string; drop: string; count: string }

// The data model every item window's RML binds to.
export interface ItemWindowModel extends RmlModel {
  text_px: number;
  title: string;
  grid_cells: GridCell[];
  window_close: () => void;
}

// my_inventory.rml's data model.
export interface InventoryModel extends ItemWindowModel {
  gold_text: string;
  gold_tier: string;
  slot_states: string[];
  repair_visible: boolean;
  myshop_visible: boolean;
  myshop_mode_open: boolean;
  myshop_locked: boolean;
  exit_tooltip: string;
  repair_tooltip: string;
  myshop_tooltip: string;
  expand_tooltip: string;
  set_option_label: string;
  socket_option_label: string;
  set_option_active: boolean;
  socket_option_active: boolean;
  my_inventory_exit_click: () => void;
  my_inventory_repair_click: () => void;
  my_inventory_myshop_click: () => void;
  my_inventory_expand_click: () => void;
  my_inventory_set_option_hover: () => void;
  my_inventory_set_option_unhover: () => void;
  my_inventory_socket_option_hover: () => void;
  my_inventory_socket_option_unhover: () => void;
}

// storage.rml's data model.
export interface VaultModel extends ItemWindowModel {
  title_locked: boolean;
  zen_text: string;
  zen_tier: string;
  fee_label: string;
  fee_value: string;
  storage_locked: boolean;
  expand_visible: boolean;
  insert_tooltip: string;
  take_tooltip: string;
  lock_tooltip: string;
  expand_tooltip: string;
  storage_insert_click: () => void;
  storage_take_click: () => void;
  storage_lock_click: () => void;
  storage_expand_click: () => void;
}

// The equipment slots' elements in my_inventory.rml, by inventory slot.
const SLOT_ELEMENTS: Record<number, string> = {
  [Equipment.RIGHT_HAND]: 'slot_weapon_right', [Equipment.LEFT_HAND]: 'slot_weapon_left', [Equipment.HELM]: 'slot_helm',
  [Equipment.ARMOR]: 'slot_armor', [Equipment.PANTS]: 'slot_pants', [Equipment.GLOVES]: 'slot_gloves', [Equipment.BOOTS]: 'slot_boots',
  [Equipment.WINGS]: 'slot_wing', [Equipment.PET]: 'slot_helper', [Equipment.PENDANT]: 'slot_amulet',
  [Equipment.RING_LEFT]: 'slot_ring_left', [Equipment.RING_RIGHT]: 'slot_ring_right',
};

// Which item groups each equipment slot takes (IsEquipable, by the item's slot in Data/Items).
const SLOT_OF_DEFINITION: Record<string, number[]> = {
  mainHand: [Equipment.RIGHT_HAND, Equipment.LEFT_HAND], offHand: [Equipment.LEFT_HAND], twoHand: [Equipment.RIGHT_HAND],
  bothHands: [Equipment.RIGHT_HAND, Equipment.LEFT_HAND], helm: [Equipment.HELM], armor: [Equipment.ARMOR], pants: [Equipment.PANTS],
  gloves: [Equipment.GLOVES], boots: [Equipment.BOOTS], wings: [Equipment.WINGS], pet: [Equipment.PET], helper: [Equipment.PET],
  pendant: [Equipment.PENDANT], amulet: [Equipment.PENDANT], ring: [Equipment.RING_LEFT, Equipment.RING_RIGHT],
};

export function formatGold(amount: number): string {
  return Math.floor(amount).toLocaleString('en-US');
}

export function goldTier(amount: number): string {
  if (amount >= 10_000_000) return 'ten-million';
  if (amount >= 1_000_000) return 'million';
  if (amount >= 100_000) return 'hundred-thousand';
  return 'base';
}

function uiScale(): number {
  return window.innerHeight / REFERENCE_HEIGHT;
}

function emptyCells(count: number): GridCell[] {
  return Array.from({ length: count }, () => ({ tint: '', drop: '', count: '' }));
}

// The item following the mouse after it was picked up (CInventoryCtrl's picked item).
class PickedItem {
  canvas: HTMLCanvasElement;
  source: PickedSource | null;
  lastEvent?: MouseEvent;

  constructor(layer: HTMLElement) {
    this.canvas = document.createElement('canvas');
    Object.assign(this.canvas.style, { position: 'fixed', pointerEvents: 'none', zIndex: 60, display: 'none' });
    layer.append(this.canvas);
    this.source = null;
    window.addEventListener('pointermove', (event) => this.follow(event));
  }

  async pick(source: { storage: number; slot: number }, item: Item): Promise<void> {
    this.source = { ...source, item };
    const scale = uiScale();
    const w = item.width * CELL * scale, h = item.height * CELL * scale;
    this.canvas.width = Math.round(w);
    this.canvas.height = Math.round(h);
    this.canvas.style.width = `${w}px`;
    this.canvas.style.height = `${h}px`;
    await drawItem(this.canvas, item);
    this.canvas.style.display = 'block';
  }

  follow(event: MouseEvent): void {
    if (!this.source) return;
    this.canvas.style.left = `${event.clientX - this.canvas.width / 2}px`;
    this.canvas.style.top = `${event.clientY - this.canvas.height / 2}px`;
    this.lastEvent = event;
  }

  drop() {
    this.source = null;
    this.canvas.style.display = 'none';
  }
}

// One RML window with an item grid (and, for the inventory, equipment slots).
class ItemWindow<M extends ItemWindowModel = ItemWindowModel> {
  inventory: Inventory;
  picked: PickedItem;
  documentName: string;
  grid: ItemGrid;
  storageKind: number;
  model: M;
  doc: RmlDocument | null;
  canvases: Map<string, { canvas: HTMLCanvasElement; signature?: string }>;
  hoverSlot: number | null;
  // Set while the window is open (by open() and layout()).
  panel!: HTMLElement;
  layer!: HTMLDivElement;
  gridOrigin!: Point;
  onPickedChanged?: () => void;
  onHover?: (item: Item | null, event: PointerEvent | null) => void;
  /** An item on the cursor dropped on another item of this window: true when handled (jewels). */
  onDropOnItem?: (from: { storage: number; slot: number; item: Item }, slot: number, target: Item) => boolean;
  /** A left click on an item, before it's picked up: true when handled (the shop's repair mode). */
  onItemClick?: (slot: number, item: Item) => boolean;
  /** A right click on an item of the window (CInventoryActionController::HandleRightClick). */
  onRightClick?: (slot: number, item: Item) => void;

  constructor({ inventory, picked, documentName, grid, storage, model }: {
    inventory: Inventory; picked: PickedItem; documentName: string; grid: ItemGrid; storage: number; model: M;
  }) {
    this.inventory = inventory;
    this.picked = picked;
    this.documentName = documentName;
    this.grid = grid;
    this.storageKind = storage;
    this.model = model;
    this.doc = null;
    this.canvases = new Map();
    this.hoverSlot = null;
  }

  // The item at a slot as a box on the screen, for its tooltip.
  screenBoxOf(slot: number): ScreenBox | null {
    const item = this.items.get(slot);
    if (!item || !this.doc) return null;
    const box = this.boxOf(slot, item);
    if (!box) return null;
    const rect = this.panel.getBoundingClientRect();
    const scale = rect.width / PANEL.width;
    return { left: rect.left + box.x * scale, top: rect.top + box.y * scale, width: box.w * scale, height: box.h * scale };
  }

  get items(): Map<number, Item> {
    return this.inventory.storage(this.storageKind);
  }

  private opening: Promise<void> | null = null;

  // Opens the window; calls while it's opening wait for the same window (no second document).
  open(parent: HTMLElement): Promise<void> {
    if (this.doc) return Promise.resolve();
    this.opening ??= this.openDocument(parent).finally(() => { this.opening = null; });
    return this.opening;
  }

  private async openDocument(parent: HTMLElement): Promise<void> {
    this.doc = await openDocument(this.documentName, this.model, { parent, scale: uiScale() });
    this.panel = this.doc.$('#panel')!;
    // The theme leaves pointer events to the game's own hit tests (as the desktop client does them in C++).
    this.panel.style.pointerEvents = 'auto';
    this.layer = document.createElement('div');
    Object.assign(this.layer.style, { position: 'absolute', left: 0, top: 0, width: '100%', height: '100%', pointerEvents: 'none' });
    this.panel.append(this.layer);
    this.panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    this.panel.addEventListener('click', (event) => this.click(event));
    this.panel.addEventListener('contextmenu', (event) => { event.preventDefault(); this.rightClick(event); });
    this.panel.addEventListener('pointermove', (event) => this.hover(event));
    this.panel.addEventListener('pointerleave', () => this.hover(null));
    this.layout();
    this.refresh();
  }

  close() {
    this.doc?.close();
    this.doc = null;
    this.canvases.clear();
  }

  get isOpen(): boolean { return Boolean(this.doc); }

  // Panel placement: docked from the right edge, like the original's dock-right column.
  place(right: number): void {
    if (!this.doc) return;
    const scale = uiScale();
    Object.assign(this.panel.style, { left: `${window.innerWidth - (right + PANEL.width) * scale}px`, top: '0px', transform: `scale(${scale})` });
    this.doc.setScale(scale);
    this.model.text_px = Math.round(12 * scale);
    this.doc.update();
  }

  layout(): void {
    const gridElement = this.doc!.$('#item_grid')!;
    this.gridOrigin = { x: gridElement.offsetLeft, y: gridElement.offsetTop };
  }

  // The panel point (reference px) of a mouse event.
  pointOf(event: MouseEvent): Point {
    const rect = this.panel.getBoundingClientRect();
    const scale = rect.width / PANEL.width;
    return { x: (event.clientX - rect.left) / scale, y: (event.clientY - rect.top) / scale };
  }

  gridSlotAt(point: Point): number | null {
    const col = Math.floor((point.x - this.gridOrigin.x) / CELL);
    const row = Math.floor((point.y - this.gridOrigin.y) / CELL);
    if (col < 0 || row < 0 || col >= this.grid.columns || row >= this.grid.rows) return null;
    return this.grid.firstSlot + row * this.grid.columns + col;
  }

  // The item under a grid slot: the item whose cells cover it.
  itemAtGridSlot(slot: number | null): number | null {
    if (slot === null) return null;
    for (const [s, item] of this.items) {
      if (s < this.grid.firstSlot) continue;
      if (Inventory.cellsOf(this.grid, s, item)?.includes(slot)) return s;
    }
    return null;
  }

  equipmentSlotAt(_point: Point): number | null { return null; }

  // Where a picked item would go for this pointer: the slot of its top-left cell.
  dropSlotFor(point: Point, item: Item): { slot: number; equipment: boolean } | null {
    const equipment = this.equipmentSlotAt(point);
    if (equipment !== null) return { slot: equipment, equipment: true };
    const col = Math.round((point.x - this.gridOrigin.x) / CELL - item.width / 2);
    const row = Math.round((point.y - this.gridOrigin.y) / CELL - item.height / 2);
    if (col < 0 || row < 0 || col + item.width > this.grid.columns || row + item.height > this.grid.rows) return null;
    return { slot: this.grid.firstSlot + row * this.grid.columns + col, equipment: false };
  }

  click(event: MouseEvent): void {
    if (event.target instanceof Element && event.target.closest('#btn_exit, #frame_corner_close, .inv-btn, .stor-btn')) return;
    const point = this.pointOf(event);
    if (this.picked.source) {
      const item = this.picked.source.item;
      // Dropped on another item: a jewel is used on it (ApplyJewels) before any move.
      const equipmentUnder = this.equipmentSlotAt(point);
      const under = equipmentUnder !== null && this.items.has(equipmentUnder) ? equipmentUnder : this.itemAtGridSlot(this.gridSlotAt(point));
      const source = this.picked.source;
      if (under !== null && !(source.storage === this.storageKind && source.slot === under) && this.items.has(under)
        && this.onDropOnItem?.(source, under, this.items.get(under)!)) {
        this.picked.drop();
        this.onPickedChanged?.();
        return;
      }
      const target = this.dropSlotFor(point, item);
      if (!target) return;
      const from = this.picked.source;
      const sameStorage = from.storage === this.storageKind;
      const fits = target.equipment ? !this.items.has(target.slot) || (sameStorage && from.slot === target.slot)
        : this.inventory.fits(this.items, this.grid, target.slot, item, sameStorage ? from.slot : null);
      if (!fits) return;
      this.picked.drop();
      if (!(sameStorage && from.slot === target.slot)) this.inventory.move(from.storage, from.slot, this.storageKind, target.slot);
      this.onPickedChanged?.();
      return;
    }
    const equipment = this.equipmentSlotAt(point);
    const slot = equipment !== null && this.items.has(equipment) ? equipment : this.itemAtGridSlot(this.gridSlotAt(point));
    if (slot === null || !this.items.has(slot) || this.inventory.pendingMove) return;
    if (this.onItemClick?.(slot, this.items.get(slot)!)) return;
    this.picked.pick({ storage: this.storageKind, slot }, this.items.get(slot)!);
    this.picked.follow(event);
    this.onPickedChanged?.();
  }

  rightClick(event: MouseEvent): void {
    if (this.picked.source || this.inventory.pendingMove) return;
    const point = this.pointOf(event);
    const equipment = this.equipmentSlotAt(point);
    const slot = equipment !== null && this.items.has(equipment) ? equipment : this.itemAtGridSlot(this.gridSlotAt(point));
    const item = slot === null ? undefined : this.items.get(slot);
    if (slot !== null && item) this.onRightClick?.(slot, item);
  }

  hover(event: PointerEvent | null): void {
    const point = event ? this.pointOf(event) : null;
    let slot: number | null = null;
    if (point) {
      const equipment = this.equipmentSlotAt(point);
      slot = equipment !== null ? equipment : this.itemAtGridSlot(this.gridSlotAt(point));
    }
    if (slot !== this.hoverSlot) {
      this.hoverSlot = slot;
      this.onHover?.(slot === null ? null : this.items.get(slot) ?? null, event);
    }
    this.updateDropPreview(point);
  }

  // The cells under the picked item: blue where it fits, red where it doesn't (drop-free / drop-blocked).
  updateDropPreview(point: Point | null): void {
    const cells = this.model.grid_cells;
    for (const cell of cells) cell.drop = '';
    const picked = this.picked.source;
    if (picked && point) {
      const target = this.dropSlotFor(point, picked.item);
      if (target && !target.equipment) {
        const fits = this.inventory.fits(this.items, this.grid, target.slot, picked.item, picked.storage === this.storageKind ? picked.slot : null);
        for (const slot of Inventory.cellsOf(this.grid, target.slot, picked.item) ?? []) cells[slot - this.grid.firstSlot].drop = fits ? 'drop-free' : 'drop-blocked';
      }
    }
    this.doc?.update();
  }

  // The item's box in panel reference px.
  boxOf(slot: number, item: Item): Box | null {
    const index = slot - this.grid.firstSlot;
    return {
      x: this.gridOrigin.x + (index % this.grid.columns) * CELL,
      y: this.gridOrigin.y + Math.floor(index / this.grid.columns) * CELL,
      w: item.width * CELL,
      h: item.height * CELL,
    };
  }

  refresh(): void {
    if (!this.doc) return;
    // Cells: tinted under every item, as CInventoryCtrl::Render() does.
    const cells = emptyCells(this.grid.columns * this.grid.rows);
    for (const [slot, item] of this.items) {
      if (slot < this.grid.firstSlot) continue;
      const hidden = this.picked.source?.storage === this.storageKind && this.picked.source.slot === slot;
      for (const cell of Inventory.cellsOf(this.grid, slot, item) ?? []) cells[cell - this.grid.firstSlot].tint = hidden ? '' : 'tint-normal';
      if (item.group === 14 && item.durability > 1 && !hidden) cells[slot - this.grid.firstSlot].count = String(item.durability);
    }
    this.model.grid_cells = cells;
    this.updateModel();
    this.doc.update();
    this.drawItems();
  }

  updateModel(): void {}

  drawItems(): void {
    const scale = uiScale();
    const seen = new Set();
    for (const [slot, item] of this.items) {
      const box = this.boxOf(slot, item);
      if (!box) continue;
      const key = `${slot}`;
      seen.add(key);
      let entry = this.canvases.get(key);
      const signature = `${item.group}:${item.number}:${item.level}:${item.excellent}:${item.ancientSet}:${box.w}x${box.h}:${scale}`;
      if (!entry) {
        const canvas = document.createElement('canvas');
        canvas.style.position = 'absolute';
        this.layer.append(canvas);
        entry = { canvas };
        this.canvases.set(key, entry);
      }
      const { canvas } = entry;
      Object.assign(canvas.style, { left: `${box.x}px`, top: `${box.y}px`, width: `${box.w}px`, height: `${box.h}px` });
      canvas.style.visibility = this.picked.source?.storage === this.storageKind && this.picked.source.slot === slot ? 'hidden' : 'visible';
      canvas.title = itemName(item.group, item.number);
      if (entry.signature !== signature) {
        entry.signature = signature;
        canvas.width = Math.round(box.w * scale);
        canvas.height = Math.round(box.h * scale);
        drawItem(canvas, item);
      }
    }
    for (const [key, entry] of this.canvases) {
      if (!seen.has(key)) { entry.canvas.remove(); this.canvases.delete(key); }
    }
  }
}

export class InventoryWindow extends ItemWindow<InventoryModel> {
  slotBoxes!: Map<number, Box>; // set by layout()

  constructor({ inventory, picked, onClose }: { inventory: Inventory; picked: PickedItem; onClose: () => void }) {
    const model: InventoryModel = {
      text_px: 12, title: 'Inventory', gold_text: '0', gold_tier: 'base',
      slot_states: Array(12).fill(''), grid_cells: emptyCells(64),
      repair_visible: false, myshop_visible: false, myshop_mode_open: true, myshop_locked: false,
      exit_tooltip: 'Close (I, V)', repair_tooltip: 'Repair', myshop_tooltip: 'Personal store', expand_tooltip: 'Inventory expansion',
      set_option_label: '', socket_option_label: '', set_option_active: false, socket_option_active: false,
      window_close: () => onClose(), my_inventory_exit_click: () => onClose(),
      my_inventory_repair_click() {}, my_inventory_myshop_click() {}, my_inventory_expand_click() {},
      my_inventory_set_option_hover() {}, my_inventory_set_option_unhover() {},
      my_inventory_socket_option_hover() {}, my_inventory_socket_option_unhover() {},
    };
    super({ inventory, picked, documentName: 'my_inventory', grid: BAG, storage: Storage.INVENTORY, model });
  }

  override layout(): void {
    super.layout();
    this.slotBoxes = new Map();
    for (const [slot, id] of Object.entries(SLOT_ELEMENTS)) {
      const element = this.doc!.$(`#${id}`);
      if (element) this.slotBoxes.set(Number(slot), { x: element.offsetLeft, y: element.offsetTop, w: element.offsetWidth, h: element.offsetHeight });
    }
  }

  override equipmentSlotAt(point: Point): number | null {
    for (const [slot, box] of this.slotBoxes) {
      if (point.x >= box.x && point.x < box.x + box.w && point.y >= box.y && point.y < box.y + box.h) return slot;
    }
    return null;
  }

  override boxOf(slot: number, item: Item): Box | null {
    if (slot < BAG.firstSlot) return this.slotBoxes.get(slot) ?? null;
    if (slot >= BAG.firstSlot + BAG.columns * BAG.rows) return null; // the extensions aren't shown yet
    return super.boxOf(slot, item);
  }

  override updateModel(): void {
    this.model.gold_text = formatGold(this.inventory.money);
    this.model.gold_tier = goldTier(this.inventory.money);
    this.model.slot_states = Array.from({ length: 12 }, (_, slot) => (this.items.has(slot) ? '' : ''));
  }
}

export class VaultWindow extends ItemWindow<VaultModel> {
  constructor({ inventory, picked, onClose, onMoney }: {
    inventory: Inventory; picked: PickedItem; onClose: () => void; onMoney: (toVault: boolean) => void;
  }) {
    const model: VaultModel = {
      text_px: 12, title: 'Vault', title_locked: false, zen_text: '0', zen_tier: 'base',
      fee_label: 'Fee', fee_value: '0', grid_cells: emptyCells(120),
      storage_locked: false, expand_visible: false,
      insert_tooltip: 'Deposit zen', take_tooltip: 'Withdraw zen', lock_tooltip: 'Lock', expand_tooltip: 'Expand',
      window_close: () => onClose(),
      storage_insert_click: () => onMoney(true), storage_take_click: () => onMoney(false),
      storage_lock_click() {}, storage_expand_click() {},
    };
    super({ inventory, picked, documentName: 'storage', grid: VAULT, storage: Storage.VAULT, model });
  }

  override updateModel(): void {
    this.model.zen_text = formatGold(this.inventory.vaultMoney);
    this.model.zen_tier = goldTier(this.inventory.vaultMoney);
  }
}


interface ShopModel extends ItemWindowModel {
  title: string; tax_rate_text: string; repair_visible: boolean; repair_tooltip: string; repair_all_tooltip: string;
  repair_all_label: string; repair_gold_text: string; repair_gold_tier: string;
  window_close: () => void; npc_shop_repair_click: () => void; npc_shop_repair_all_click: () => void;
}

// A merchant's window (CNPCShop, npc_shop.rml): its items in an 8 x 15 grid; a click buys one, a bag
// item put on it is sold; repair shops show the repair buttons and what repairing everything costs.
export class ShopWindow extends ItemWindow<ShopModel> {
  /** Whether this merchant repairs (SetRepairShop: the smiths and weapon merchants). */
  repairShop = false;
  /** What repairing everything costs (RepairAllGold), set by the owner. */
  repairAllCost: () => number = () => 0;

  constructor({ inventory, picked, onClose, onRepairMode, onSell }: {
    inventory: Inventory; picked: PickedItem; onClose: () => void; onRepairMode: () => void; onSell: (slot: number, item: Item) => void;
  }) {
    const model: ShopModel = {
      text_px: 12, grid_cells: emptyCells(SHOP.columns * SHOP.rows), title: 'Merchant', tax_rate_text: 'Tax rate 0% (changed in real time)',
      repair_visible: false, repair_tooltip: 'Repair (L)', repair_all_tooltip: 'Repair all (A)', repair_all_label: 'Repair all',
      repair_gold_text: '0', repair_gold_tier: 'base',
      window_close: () => onClose(),
      npc_shop_repair_click: () => onRepairMode(),
      npc_shop_repair_all_click: () => inventory.repair(0xff, false),
    };
    super({ inventory, picked, documentName: 'npc_shop', grid: SHOP, storage: SHOP_STORAGE, model });
    this.onSell = onSell;
  }

  private readonly onSell: (slot: number, item: Item) => void;

  // A click buys the item under the mouse; with a bag item on the cursor it sells that item (InventoryProcess).
  override click(event: MouseEvent): void {
    if (event.target instanceof Element && event.target.closest('#frame_corner_close, .shop-btn')) return;
    const source = this.picked.source;
    if (source) {
      if (source.storage === Storage.INVENTORY && source.slot >= BAG.firstSlot) {
        const item = source.item;
        this.picked.drop();
        this.onSell(source.slot, item);
        this.onPickedChanged?.();
      }
      return;
    }
    const slot = this.itemAtGridSlot(this.gridSlotAt(this.pointOf(event)));
    if (slot !== null) this.inventory.buy(slot);
  }

  override updateModel(): void {
    this.model.repair_visible = this.repairShop;
    const cost = this.repairAllCost();
    this.model.repair_gold_text = formatGold(cost);
    this.model.repair_gold_tier = goldTier(cost);
  }
}

// The inventory and vault windows with the item on the cursor, and the keys that open them.
export class ItemWindows {
  inventory: Inventory;
  parent: HTMLElement;
  picked: PickedItem;
  bag: InventoryWindow;
  vault: VaultWindow;
  shop: ShopWindow;
  tooltip: ItemTooltipView;
  /** The repair mode of a repair shop (L): a click on an item repairs it (CNPCShop::ToggleState). */
  repairMode = false;
  /** A line for the chat (the shop's refusals). */
  message: (text: string) => void = () => undefined;

  constructor({ inventory, parent = document.body as HTMLElement, character = () => null, groundCell = () => null }: {
    inventory: Inventory; parent?: HTMLElement; character?: () => TooltipCharacter | null;
    /** The ground cell under the mouse, where a picked inventory item is dropped (null: not on the ground). */
    groundCell?: (event: MouseEvent) => { x: number; y: number } | null;
  }) {
    this.inventory = inventory;
    this.parent = parent;
    this.picked = new PickedItem(parent);
    this.tooltip = new ItemTooltipView(parent, character);
    // Get the windows ready in the background, so they open at once.
    prepareDocument('my_inventory').then(() => prepareDocument('storage')).catch((error) => console.warn('Interface', error));
    this.bag = new InventoryWindow({ inventory, picked: this.picked, onClose: () => this.closeAll() });
    this.vault = new VaultWindow({ inventory, picked: this.picked, onClose: () => this.closeAll(), onMoney: (toVault) => this.askMoney(toVault) });
    this.shop = new ShopWindow({
      inventory, picked: this.picked, onClose: () => this.closeAll(),
      onRepairMode: () => { this.repairMode = !this.repairMode; },
      onSell: (slot, item) => this.sell(slot, item),
    });
    // In a repair shop's repair mode a click on an item repairs it (RepairItemAtMousePoint).
    this.bag.onItemClick = (slot) => {
      if (!this.repairMode || !this.shop.isOpen) return false;
      this.inventory.repair(slot, false);
      return true;
    };
    for (const itemWindow of [this.bag, this.vault, this.shop]) {
      itemWindow.onPickedChanged = () => { this.tooltip.hide(); this.refresh(); };
      // The hovered item's tooltip, unless an item is on the cursor (CInventoryCtrl only shows it then).
      itemWindow.onHover = (item) => {
        const box = item && !this.picked.source && itemWindow.hoverSlot !== null ? itemWindow.screenBoxOf(itemWindow.hoverSlot) : null;
        if (item && box) void this.tooltip.show(item, box);
        else this.tooltip.hide();
      };
    }
    inventory.onChange((what) => {
      if (what === 'window') this.syncVault();
      this.refresh();
    });
    window.addEventListener('keydown', (event) => {
      if (event.target instanceof Element && event.target.closest('input, textarea')) return;
      const key = event.key.toLowerCase();
      if (key === 'i' || key === 'v') { event.preventDefault(); this.toggle(); }
      if (key === 'escape' && (this.bag.isOpen || this.vault.isOpen || this.shop.isOpen)) this.closeAll();
      // L: the repair mode of a repair shop; Shift + L repairs everything (CNPCShop::UpdateKeyEvent).
      if (key === 'l' && this.shop.isOpen && this.shop.repairShop) {
        if (event.shiftKey) this.inventory.repair(0xff, false);
        else this.repairMode = !this.repairMode;
      }
    });
    window.addEventListener('resize', () => this.placeAll());
    // A click outside the windows with an inventory item on the cursor drops it on the ground there, as
    // on the desktop; a vault item goes back.
    window.addEventListener('pointerdown', (event) => {
      const source = this.picked.source;
      if (!source) return;
      // Clicks on a window are the window's (placing the item there).
      if (event.composedPath().some((node) => node instanceof HTMLElement && node.classList.contains('rml-document'))) return;
      const cell = source.storage === Storage.INVENTORY ? groundCell(event) : null;
      if (cell) {
        event.stopImmediatePropagation();
        inventory.dropToGround(source.slot, cell);
      }
      this.picked.drop();
      this.refresh();
    }, true);
  }

  async toggle(): Promise<void> {
    if (this.bag.isOpen) this.closeAll();
    else { await this.bag.open(this.parent); this.placeAll(); }
  }

  async syncVault(): Promise<void> {
    const open = this.inventory.openWindow;
    if (open === NpcWindow.MERCHANT || open === NpcWindow.MERCHANT1) {
      await this.bag.open(this.parent);
      await this.shop.open(this.parent);
      this.placeAll();
      return;
    }
    if (this.shop.isOpen) { this.shop.close(); this.repairMode = false; this.placeAll(); }
    if (this.inventory.openWindow === NpcWindow.VAULT) {
      await this.bag.open(this.parent);
      await this.vault.open(this.parent);
      this.placeAll();
    } else if (this.vault.isOpen) {
      this.vault.close();
      this.placeAll();
    }
  }

  closeAll(): void {
    this.tooltip.hide();
    if (this.picked.source) this.picked.drop();
    if (this.vault.isOpen) { this.vault.close(); this.inventory.closeVault(); }
    if (this.shop.isOpen) { this.shop.close(); this.repairMode = false; this.inventory.closeShop(); }
    this.bag.close();
  }

  /** Panels docked at the right edge before these windows (the character window): the bag moves left of them. */
  dockedRight = 0;

  placeAll(): void {
    const right = this.dockedRight * PANEL.width;
    this.bag.place(right);
    this.vault.place(right + PANEL.width);
    this.shop.place(right + PANEL.width);
    this.bag.refresh();
    this.vault.refresh();
    this.shop.refresh();
  }

  refresh(): void {
    this.bag.refresh();
    this.vault.refresh();
    this.shop.refresh();
  }

  // Selling a bag item to the open merchant (HandleSellToNPC / InventoryProcess): not what can't be
  // sold, and an expensive item only after asking.
  sell(slot: number, item: Item): void {
    if (this.inventory.sellingSlot !== null) return;
    if (isSellingBan(item)) { this.message('These items cannot be traded.'); return; }
    if (isHighValue(item) && !window.confirm('An expensive item. Check the item, please. Are you sure you want to sell it?')) return;
    this.inventory.sell(slot);
  }

  askMoney(toVault: boolean): void {
    const text = window.prompt(toVault ? 'How much zen to deposit?' : 'How much zen to withdraw?', '0');
    const amount = Math.floor(Number(text));
    if (amount > 0) this.inventory.moveMoney(toVault, amount);
  }
}
