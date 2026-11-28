import { gameKey, panelKey } from '../game/keyboardShortcuts.js';
// The inventory (my_inventory.rml) and the vault (storage.rml), as the desktop client's
// CMyInventory and CStorageInventory: the equipment slots and the 8 x 8 bag, the vault's 8 x 15
// grid, the money, and moving items by clicking an item to pick it up and clicking where it goes.

import { translate } from '../i18n.js';
import { openDocument, prepareDocument } from './rml.js';
import type { RmlDocument, RmlModel } from './rml.js';
import { dismissConfirm, showConfirm } from './gameDialogs.js';
import { drawItem } from './itemIcons.js';
import { BAG, VAULT, SHOP, SHOP_STORAGE, TRADE, Equipment, Inventory, NpcWindow, Storage } from '../game/inventory.js';
import type { Item, ItemGrid } from '../game/inventory.js';
import { itemName } from '../game/items.js';
import { ItemTooltipView, type ScreenBox } from './itemTooltipView.js';
import type { TooltipCharacter, TooltipLine } from '../game/itemTooltip.js';
import { isHighValue, isSellingBan } from '../game/itemUse.js';
import { nativeTextPx, uiScale, dockTop } from './nativeText.js';

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

export function emptyCells(count: number): GridCell[] {
  return Array.from({ length: count }, () => ({ tint: '', drop: '', count: '' }));
}

// The item following the mouse after it was picked up (CInventoryCtrl's picked item).
/** Which items show their durability as a count: the potions group, and HNMO's stack kinds (ui/itemStacks.ts). */
let stackCountRule: (item: Item) => boolean = (item) => item.group === 14;

/** Sets which items show their durability as a count (HNMO's bigger stacks). */
export function setStackCountRule(rule: (item: Item) => boolean): void {
  stackCountRule = rule;
}

export class PickedItem {
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
    await drawItem(this.canvas, item, { pixelsPerReference: scale });
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
export class ItemWindow<M extends ItemWindowModel = ItemWindowModel> {
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
  /** A Shift + click on an item (HNMO: split a stack); returns whether it was taken. */
  onShiftClick?: (slot: number, item: Item) => boolean;
  /** A right click on an item of the window (CInventoryActionController::HandleRightClick). */
  onRightClick?: (slot: number, item: Item) => void;
  /** A right click on an item, before onRightClick: true when handled (the open trade takes bag items). */
  interceptRightClick?: (slot: number, item: Item) => boolean;
  /** Alt + right click on an item (HNMO: the item lock): true when handled. */
  onAltRightClick?: (slot: number, item: Item) => boolean;

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
    this.doc.host.style.zIndex = '28';
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
    Object.assign(this.panel.style, { left: `${window.innerWidth - (right + PANEL.width) * scale}px`, top: `${dockTop()}px`, transform: `scale(${scale})` });
    this.doc.setScale(scale);
    this.model.text_px = nativeTextPx();
    this.doc.update();
  }

  protected activeGrids(): { grid: ItemGrid; origin: Point; cells: GridCell[] }[] {
    return [{ grid: this.grid, origin: this.gridOrigin, cells: this.model.grid_cells }];
  }

  protected regionOfSlot(slot: number): { grid: ItemGrid; origin: Point; cells: GridCell[] } | null {
    return this.activeGrids().find((r) => slot >= r.grid.firstSlot && slot < r.grid.firstSlot + r.grid.columns * r.grid.rows) ?? null;
  }

  protected prepareGrids(): void {}

  protected setRegionCells(_grid: ItemGrid, cells: GridCell[]): void {
    this.model.grid_cells = cells;
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
    for (const region of this.activeGrids()) {
      const col = Math.floor((point.x - region.origin.x) / CELL);
      const row = Math.floor((point.y - region.origin.y) / CELL);
      if (col >= 0 && row >= 0 && col < region.grid.columns && row < region.grid.rows) {
        return region.grid.firstSlot + row * region.grid.columns + col;
      }
    }
    return null;
  }

  // The item under a grid slot: the item whose cells cover it.
  itemAtGridSlot(slot: number | null): number | null {
    if (slot === null) return null;
    const region = this.regionOfSlot(slot);
    if (!region) return null;
    const end = region.grid.firstSlot + region.grid.columns * region.grid.rows;
    for (const [s, item] of this.items) {
      if (s < region.grid.firstSlot || s >= end) continue;
      if (Inventory.cellsOf(region.grid, s, item)?.includes(slot)) return s;
    }
    return null;
  }

  equipmentSlotAt(_point: Point): number | null { return null; }

  // Where a picked item would go for this pointer: the slot of its top-left cell.
  dropSlotFor(point: Point, item: Item): { slot: number; equipment: boolean } | null {
    const equipment = this.equipmentSlotAt(point);
    if (equipment !== null) return { slot: equipment, equipment: true };
    const regions = this.activeGrids();
    for (const region of regions) {
      if (regions.length > 1) {
        const px = point.x - region.origin.x, py = point.y - region.origin.y;
        if (px < 0 || py < 0 || px >= region.grid.columns * CELL || py >= region.grid.rows * CELL) continue;
      }
      const col = Math.round((point.x - region.origin.x) / CELL - item.width / 2);
      const row = Math.round((point.y - region.origin.y) / CELL - item.height / 2);
      if (col < 0 || row < 0 || col + item.width > region.grid.columns || row + item.height > region.grid.rows) continue;
      return { slot: region.grid.firstSlot + row * region.grid.columns + col, equipment: false };
    }
    return null;
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
      const targetGrid = target.equipment ? this.grid : (this.regionOfSlot(target.slot)?.grid ?? this.grid);
      const fits = target.equipment ? !this.items.has(target.slot) || (sameStorage && from.slot === target.slot)
        : this.inventory.fits(this.items, targetGrid, target.slot, item, sameStorage ? from.slot : null);
      if (!fits) return;
      this.picked.drop();
      if (!(sameStorage && from.slot === target.slot)) this.inventory.move(from.storage, from.slot, this.storageKind, target.slot);
      this.onPickedChanged?.();
      return;
    }
    const equipment = this.equipmentSlotAt(point);
    const slot = equipment !== null && this.items.has(equipment) ? equipment : this.itemAtGridSlot(this.gridSlotAt(point));
    if (slot === null || !this.items.has(slot) || this.inventory.pendingMove) return;
    if (event.shiftKey && this.onShiftClick?.(slot, this.items.get(slot)!)) return;
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
    if (slot !== null && item && event.altKey && this.onAltRightClick?.(slot, item)) return;
    if (slot !== null && item && !this.interceptRightClick?.(slot, item)) this.onRightClick?.(slot, item);
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
    for (const region of this.activeGrids()) {
      for (const cell of region.cells) cell.drop = '';
    }
    const picked = this.picked.source;
    if (picked && point) {
      const target = this.dropSlotFor(point, picked.item);
      const region = target && !target.equipment ? this.regionOfSlot(target.slot) : null;
      if (target && region) {
        const fits = this.inventory.fits(this.items, region.grid, target.slot, picked.item, picked.storage === this.storageKind ? picked.slot : null);
        for (const slot of Inventory.cellsOf(region.grid, target.slot, picked.item) ?? []) region.cells[slot - region.grid.firstSlot].drop = fits ? 'drop-free' : 'drop-blocked';
      }
    }
    this.doc?.update();
  }

  // The item's box in panel reference px.
  boxOf(slot: number, item: Item): Box | null {
    const region = this.regionOfSlot(slot);
    if (!region) return null;
    const index = slot - region.grid.firstSlot;
    return {
      x: region.origin.x + (index % region.grid.columns) * CELL,
      y: region.origin.y + Math.floor(index / region.grid.columns) * CELL,
      w: item.width * CELL,
      h: item.height * CELL,
    };
  }

  refresh(): void {
    if (!this.doc) return;
    this.prepareGrids();
    // Cells: tinted under every item, as CInventoryCtrl::Render() does.
    for (const region of this.activeGrids()) {
      const cells = emptyCells(region.grid.columns * region.grid.rows);
      const end = region.grid.firstSlot + region.grid.columns * region.grid.rows;
      for (const [slot, item] of this.items) {
        if (slot < region.grid.firstSlot || slot >= end) continue;
        const hidden = this.picked.source?.storage === this.storageKind && this.picked.source.slot === slot;
        for (const cell of Inventory.cellsOf(region.grid, slot, item) ?? []) cells[cell - region.grid.firstSlot].tint = hidden ? '' : 'tint-normal';
        if (stackCountRule(item) && item.durability > 1 && !hidden) cells[slot - region.grid.firstSlot].count = String(item.durability);
      }
      this.setRegionCells(region.grid, cells);
    }
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
        drawItem(canvas, item, { pixelsPerReference: scale });
      }
    }
    for (const [key, entry] of this.canvases) {
      if (!seen.has(key)) { entry.canvas.remove(); this.canvases.delete(key); }
    }
  }
}

export class InventoryWindow extends ItemWindow<InventoryModel> {
  slotBoxes!: Map<number, Box>; // set by layout()

  constructor({ inventory, picked, onClose, onExpand }: {
    inventory: Inventory; picked: PickedItem; onClose: () => void; onExpand?: () => void;
  }) {
    const model: InventoryModel = {
      text_px: 12, title: 'Inventory', gold_text: '0', gold_tier: 'base',
      slot_states: Array(12).fill(''), grid_cells: emptyCells(64),
      repair_visible: false, myshop_visible: false, myshop_mode_open: true, myshop_locked: false,
      exit_tooltip: 'Close (I, V)', repair_tooltip: 'Repair', myshop_tooltip: 'Personal store',
      expand_tooltip: translate('Open Expanded Inventory (K)') ?? 'Open Expanded Inventory (K)',
      set_option_label: '', socket_option_label: '', set_option_active: false, socket_option_active: false,
      window_close: () => onClose(), my_inventory_exit_click: () => onClose(),
      my_inventory_repair_click() {}, my_inventory_myshop_click() {}, my_inventory_expand_click: () => onExpand?.(),
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
    if (slot >= BAG.firstSlot + BAG.columns * BAG.rows) return null; // extension slots are drawn by InventoryExtensionWindow
    return super.boxOf(slot, item);
  }

  override updateModel(): void {
    this.model.gold_text = formatGold(this.inventory.money);
    this.model.gold_tier = goldTier(this.inventory.money);
    this.model.slot_states = Array.from({ length: 12 }, (_, slot) => (this.items.has(slot) ? '' : ''));
  }
}

const BAG_EXTENSION_COUNT = 4;
export const BAG_EXTENSIONS: readonly ItemGrid[] = Array.from(
  { length: BAG_EXTENSION_COUNT },
  (_, page) => ({ firstSlot: BAG.firstSlot + BAG.columns * BAG.rows + page * 32, columns: 8, rows: 4 }),
);
const EXTENSION_DEFAULT_ORIGINS: readonly Point[] = [0, 1, 2, 3].map((page) => ({ x: 15, y: 45 + page * 87 }));

type RmlCells = GridCell[] & { size: number };
const withRmlSize = (cells: GridCell[]): RmlCells => Object.assign(cells, { size: cells.length });

export interface InventoryExtensionModel extends ItemWindowModel {
  title: string;
  exit_tooltip: string;
  grid_cells_1: RmlCells;
  grid_cells_2: RmlCells;
  grid_cells_3: RmlCells;
  grid_cells_4: RmlCells;
  locked_pages: { number: number }[];
  inventory_extension_exit_click: () => void;
}

const EXTENSION_CELL_KEYS = ['grid_cells_1', 'grid_cells_2', 'grid_cells_3', 'grid_cells_4'] as const;

export class InventoryExtensionWindow extends ItemWindow<InventoryExtensionModel> {
  private readonly extensions: () => number;
  private pageOrigins: Point[] = EXTENSION_DEFAULT_ORIGINS.map((p) => ({ ...p }));

  constructor({ inventory, picked, extensions, onClose }: {
    inventory: Inventory; picked: PickedItem; extensions: () => number; onClose: () => void;
  }) {
    const model: InventoryExtensionModel = {
      text_px: 12,
      grid_cells: [],
      title: translate('Expanded Inventory') ?? 'Expanded Inventory',
      exit_tooltip: translate('Close') ?? 'Close',
      grid_cells_1: withRmlSize([]),
      grid_cells_2: withRmlSize([]),
      grid_cells_3: withRmlSize([]),
      grid_cells_4: withRmlSize([]),
      locked_pages: [],
      window_close: () => onClose(),
      inventory_extension_exit_click: () => onClose(),
    };
    super({ inventory, picked, documentName: 'inventory_extension', grid: BAG_EXTENSIONS[0], storage: Storage.INVENTORY, model });
    this.extensions = extensions;
    this.prepareGrids();
  }

  private unlockedCount(): number {
    return Math.max(0, Math.min(BAG_EXTENSION_COUNT, this.extensions()));
  }

  protected override prepareGrids(): void {
    const unlocked = this.unlockedCount();
    const locked: { number: number }[] = [];
    for (let i = 0; i < BAG_EXTENSION_COUNT; i++) {
      const key = EXTENSION_CELL_KEYS[i];
      if (i < unlocked) {
        if (this.model[key].length !== 32) this.model[key] = withRmlSize(emptyCells(32));
      } else {
        if (this.model[key].length !== 0) this.model[key] = withRmlSize([]);
        locked.push({ number: i + 1 });
      }
    }
    this.model.locked_pages = locked;
    this.model.title = translate('Expanded Inventory') ?? 'Expanded Inventory';
    this.model.exit_tooltip = translate('Close') ?? 'Close';
    if (this.doc) {
      this.doc.update();
      this.measurePageOrigins();
    }
  }

  private measurePageOrigins(): void {
    if (!this.doc) return;
    for (let i = 0; i < BAG_EXTENSION_COUNT; i++) {
      const el = this.doc.$(`#item_grid_${i + 1}`);
      if (el && el.offsetWidth > 0) this.pageOrigins[i] = { x: el.offsetLeft, y: el.offsetTop };
    }
  }

  override layout(): void {
    this.prepareGrids();
  }

  protected override activeGrids(): { grid: ItemGrid; origin: Point; cells: GridCell[] }[] {
    const unlocked = this.unlockedCount();
    const regions: { grid: ItemGrid; origin: Point; cells: GridCell[] }[] = [];
    for (let i = 0; i < unlocked; i++) {
      regions.push({ grid: BAG_EXTENSIONS[i], origin: this.pageOrigins[i], cells: this.model[EXTENSION_CELL_KEYS[i]] });
    }
    return regions;
  }

  protected override setRegionCells(grid: ItemGrid, cells: GridCell[]): void {
    const page = BAG_EXTENSIONS.indexOf(grid);
    if (page >= 0) this.model[EXTENSION_CELL_KEYS[page]] = withRmlSize(cells);
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

  /** Whether the vault has a PIN (CStorageInventory::m_bLock); set by the account safety. */
  isProtected: () => boolean = () => false;
  /** The texts of the title (the desktop's Storage, Open and Close), translated by the owner. */
  titleText: (locked: boolean) => string = (locked) => `Storage (${locked ? 'Close' : 'Open'})`;

  override updateModel(): void {
    this.model.zen_text = formatGold(this.inventory.vaultMoney);
    this.model.zen_tier = goldTier(this.inventory.vaultMoney);
    // SyncRmlModel: "Storage (Open/Close)", red while locked, and the lock icon closed.
    const locked = this.isProtected();
    this.model.title = this.titleText(locked);
    this.model.title_locked = locked;
    this.model.storage_locked = locked;
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

// trade.rml's data model (CTrade::TradeRmlModel).
interface TradeModel extends ItemWindowModel {
  big_text_px: number;
  partner_cells: GridCell[];
  your_id_text: string; your_guild_visible: boolean; your_guild_name: string; your_level_text: string; your_level_bucket: number;
  your_gold_text: string; your_gold_tier: string; your_confirm_checked: boolean;
  my_id_text: string; my_gold_text: string; my_gold_tier: string; my_confirm_checked: boolean; my_confirm_waiting: boolean;
  warning_label: string; notice_line1: string; notice_line2: string; notice_line3: string; warning_opacity: number;
  close_tooltip: string; zen_tooltip: string;
  item_warning_text: string; item_warning_badges: { x: number; y: number; width: number }[];
  trade_exit_click: () => void; trade_zen_click: () => void; trade_my_confirm_click: () => void;
}

/** What the trade window shows, kept by the trade (src/ui/tradeWindow.ts). */
export interface TradeView {
  partnerName: string;
  partnerLevel: number;
  partnerGold: number;
  partnerConfirmed: boolean;
  /** The partner's items by trade slot (TradeItemAdded / TradeItemRemoved). */
  partner: Map<number, Item>;
  /** The partner's slots whose items changed for the worse since they were last offered (ITEM_COLOR_TRADE_WARNING). */
  warnings: Set<number>;
  myName: string;
  myGold: number;
  myConfirmed: boolean;
  /** Until when (performance.now()) the own OK button waits after the offer changed. */
  waitUntil: number;
}

/** Hover slots at or above this are the partner's grid (PARTNER_SLOT + trade slot). */
const PARTNER_SLOT = 1000;

// CTrade::ConvertYourLevel: the partner's level rounded down to the bucket the window shows ("About N").
export function tradeLevelBucket(level: number): number {
  for (const bucket of [400, 300, 200, 100, 50]) if (level >= bucket) return bucket;
  return 10;
}

// The trade window (CTrade, trade.rml): the partner's 8 x 4 grid, zen and OK mark at the top, the own
// grid, zen, the zen and close buttons and the own OK button at the bottom. Own items move in and out
// like in the vault (ItemMoveRequestExtended to the trade storage); the partner's are only shown.
export class TradeWindow extends ItemWindow<TradeModel> {
  view: TradeView = {
    partnerName: '', partnerLevel: 0, partnerGold: 0, partnerConfirmed: false, partner: new Map(), warnings: new Set(),
    myName: '', myGold: 0, myConfirmed: false, waitUntil: 0,
  };
  /** A bag item is about to be put into the trade: false keeps it out (IsTradeBan). */
  onOffer?: (item: Item) => boolean;
  /** A bag item was sent into the trade, or an own trade item picked up (the own OK is cleared). */
  onOfferChanged?: (kind: 'added' | 'picked') => void;
  /** A right click on an own trade item (it goes back to the bag). */
  onTradeItemRightClick?: (slot: number, item: Item) => void;
  partnerOrigin: Point = { x: 16, y: 68 };
  private partnerCanvases = new Map<number, { canvas: HTMLCanvasElement; signature?: string }>();
  private pulse: ReturnType<typeof setInterval> | null = null;

  constructor({ inventory, picked, onClose, onZen, onConfirm }: {
    inventory: Inventory; picked: PickedItem; onClose: () => void; onZen: () => void; onConfirm: () => void;
  }) {
    const model: TradeModel = {
      text_px: 12, big_text_px: 14, title: 'Trade', grid_cells: emptyCells(TRADE.columns * TRADE.rows), partner_cells: emptyCells(TRADE.columns * TRADE.rows),
      your_id_text: '', your_guild_visible: false, your_guild_name: '', your_level_text: '', your_level_bucket: 10,
      your_gold_text: '0', your_gold_tier: 'base', your_confirm_checked: false,
      my_id_text: '', my_gold_text: '0', my_gold_tier: 'base', my_confirm_checked: false, my_confirm_waiting: false,
      warning_label: 'Warning!', notice_line1: 'Notice! Please check out', notice_line2: 'the level of the player', notice_line3: 'and the items before trading.',
      warning_opacity: 1, close_tooltip: 'Close', zen_tooltip: 'Zen Trade',
      item_warning_text: 'Warning!', item_warning_badges: [],
      window_close: () => onClose(), trade_exit_click: () => onClose(), trade_zen_click: () => onZen(), trade_my_confirm_click: () => onConfirm(),
    };
    super({ inventory, picked, documentName: 'trade', grid: TRADE, storage: Storage.TRADE, model });
    this.onRightClick = (slot, item) => this.onTradeItemRightClick?.(slot, item);
  }

  override layout(): void {
    super.layout();
    const partner = this.doc!.$('#partner_grid');
    if (partner) this.partnerOrigin = { x: partner.offsetLeft, y: partner.offsetTop };
    // The "Warning!" word pulses (sin(WorldTime / 200) * 200 + 275) and the OK button waits a moment
    // after an offer changed: both follow the clock while the window is open.
    this.pulse ??= setInterval(() => {
      if (!this.doc) return;
      const now = performance.now();
      this.model.warning_opacity = Math.min(255, Math.sin(now / 200) * 200 + 275) / 255;
      this.model.my_confirm_waiting = now < this.view.waitUntil;
      this.model.item_warning_badges = this.warningBadges(now);
      this.doc.update();
    }, 100);
  }

  override close(): void {
    if (this.pulse !== null) { clearInterval(this.pulse); this.pulse = null; }
    for (const entry of this.partnerCanvases.values()) entry.canvas.remove();
    this.partnerCanvases.clear();
    super.close();
  }

  /** The partner's item under a panel point: its trade slot. */
  partnerItemAt(point: Point): number | null {
    const col = Math.floor((point.x - this.partnerOrigin.x) / CELL), row = Math.floor((point.y - this.partnerOrigin.y) / CELL);
    if (col < 0 || row < 0 || col >= TRADE.columns || row >= TRADE.rows) return null;
    const cell = row * TRADE.columns + col;
    for (const [slot, item] of this.view.partner) if (Inventory.cellsOf(TRADE, slot, item)?.includes(cell)) return slot;
    return null;
  }

  partnerBoxOf(slot: number, item: Item): Box {
    return {
      x: this.partnerOrigin.x + (slot % TRADE.columns) * CELL, y: this.partnerOrigin.y + Math.floor(slot / TRADE.columns) * CELL,
      w: item.width * CELL, h: item.height * CELL,
    };
  }

  override screenBoxOf(slot: number): ScreenBox | null {
    if (slot < PARTNER_SLOT) return super.screenBoxOf(slot);
    const item = this.view.partner.get(slot - PARTNER_SLOT);
    if (!item || !this.doc) return null;
    const box = this.partnerBoxOf(slot - PARTNER_SLOT, item);
    const rect = this.panel.getBoundingClientRect();
    const scale = rect.width / PANEL.width;
    return { left: rect.left + box.x * scale, top: rect.top + box.y * scale, width: box.w * scale, height: box.h * scale };
  }

  // The partner's items show their tooltips too.
  override hover(event: PointerEvent | null): void {
    const point = event ? this.pointOf(event) : null;
    const partnerSlot = point ? this.partnerItemAt(point) : null;
    if (partnerSlot !== null) {
      if (this.hoverSlot !== PARTNER_SLOT + partnerSlot) {
        this.hoverSlot = PARTNER_SLOT + partnerSlot;
        this.onHover?.(this.view.partner.get(partnerSlot) ?? null, event);
      }
      this.updateDropPreview(point);
      return;
    }
    if (this.hoverSlot !== null && this.hoverSlot >= PARTNER_SLOT) this.hoverSlot = -1; // left the partner's item
    super.hover(event);
  }

  override click(event: MouseEvent): void {
    if (event.target instanceof Element && event.target.closest('#frame_corner_close, .trade-btn, #my_confirm')) return;
    const source = this.picked.source;
    // Only bag items go into the trade (CTrade::SendRequestItemToTrade), and not those that can't be traded.
    if (source && source.storage !== Storage.TRADE && (source.storage !== Storage.INVENTORY || source.slot < BAG.firstSlot || this.onOffer?.(source.item) === false)) return;
    const pendingBefore = this.inventory.pendingMove;
    super.click(event);
    const pending = this.inventory.pendingMove;
    if (source && pending && pending !== pendingBefore && pending.fromStorage !== Storage.TRADE) this.onOfferChanged?.('added');
    else if (!source && this.picked.source?.storage === Storage.TRADE) this.onOfferChanged?.('picked');
  }

  override updateModel(): void {
    const view = this.view;
    const cells = emptyCells(TRADE.columns * TRADE.rows);
    for (const [slot, item] of view.partner) {
      for (const cell of Inventory.cellsOf(TRADE, slot, item) ?? []) cells[cell].tint = 'tint-normal';
      if (stackCountRule(item) && item.durability > 1) cells[slot].count = String(item.durability);
    }
    this.model.partner_cells = cells;
    this.model.big_text_px = nativeTextPx('big');
    this.model.your_id_text = view.partnerName;
    const bucket = tradeLevelBucket(view.partnerLevel);
    this.model.your_level_bucket = bucket;
    this.model.your_level_text = bucket === 400 ? 'Lv.400' : `Lv.About ${bucket}`;
    this.model.your_gold_text = formatGold(view.partnerGold);
    this.model.your_gold_tier = goldTier(view.partnerGold);
    this.model.your_confirm_checked = view.partnerConfirmed;
    this.model.my_id_text = view.myName;
    this.model.my_gold_text = formatGold(view.myGold);
    this.model.my_gold_tier = goldTier(view.myGold);
    this.model.my_confirm_checked = view.myConfirmed;
    this.model.my_confirm_waiting = performance.now() < view.waitUntil;
    this.model.item_warning_badges = this.warningBadges(performance.now());
  }

  // The "Warning!" badges over the partner's changed items, with the original's little wobble.
  warningBadges(now: number): { x: number; y: number; width: number }[] {
    const badges: { x: number; y: number; width: number }[] = [];
    for (const slot of this.view.warnings) {
      const item = this.view.partner.get(slot);
      if (!item) continue;
      const box = this.partnerBoxOf(slot, item);
      badges.push({ x: box.x, y: box.y + Math.sin(now * 0.015), width: box.w });
    }
    return badges;
  }

  override drawItems(): void {
    super.drawItems();
    const scale = uiScale();
    const seen = new Set<number>();
    for (const [slot, item] of this.view.partner) {
      const box = this.partnerBoxOf(slot, item);
      seen.add(slot);
      let entry = this.partnerCanvases.get(slot);
      if (!entry) {
        const canvas = document.createElement('canvas');
        canvas.style.position = 'absolute';
        this.layer.append(canvas);
        entry = { canvas };
        this.partnerCanvases.set(slot, entry);
      }
      const { canvas } = entry;
      Object.assign(canvas.style, { left: `${box.x}px`, top: `${box.y}px`, width: `${box.w}px`, height: `${box.h}px` });
      canvas.title = itemName(item.group, item.number);
      const signature = `${item.group}:${item.number}:${item.level}:${item.excellent}:${item.ancientSet}:${box.w}x${box.h}:${scale}`;
      if (entry.signature !== signature) {
        entry.signature = signature;
        canvas.width = Math.round(box.w * scale);
        canvas.height = Math.round(box.h * scale);
        void drawItem(canvas, item, { pixelsPerReference: scale });
      }
    }
    for (const [slot, entry] of this.partnerCanvases) {
      if (!seen.has(slot)) { entry.canvas.remove(); this.partnerCanvases.delete(slot); }
    }
  }
}

// The inventory and vault windows with the item on the cursor, and the keys that open them.
export class ItemWindows {
  inventory: Inventory;
  parent: HTMLElement;
  picked: PickedItem;
  bag: InventoryWindow;
  extension: InventoryExtensionWindow;
  vault: VaultWindow;
  shop: ShopWindow;
  /** The trade window; the trade itself (src/ui/tradeWindow.ts) fills it and sets its handlers. */
  trade: TradeWindow;
  /** The player closed the open trade (Escape, closing the inventory): the trade sets this to cancel it. */
  onTradeClosed: () => void = () => undefined;
  tooltip: ItemTooltipView;
  /** The repair mode of a repair shop (L): a click on an item repairs it (CNPCShop::ToggleState). */
  repairMode = false;
  /** A line for the chat (the shop's refusals). */
  message: (text: string) => void = () => undefined;
  /** HNMO's extra tooltip lines of an item at a storage's slot (the account safety's lock). */
  extraTooltipLines: (storage: number, slot: number) => TooltipLine[] = () => [];
  /** The vault window was shown or hidden (HNMO's vault pages follow it). */
  onVaultShown: (shown: boolean) => void = () => undefined;
  /** The bag opened or closed. */
  onBagToggle: (open: boolean) => void = () => undefined;
  private lastBagOpen = false;
  /** The windows were placed again (a resize, a docked panel). */
  onPlaced: () => void = () => undefined;

  constructor({ inventory, parent = document.body as HTMLElement, character = () => null, groundCell = () => null, extensions = () => 0 }: {
    inventory: Inventory; parent?: HTMLElement; character?: () => TooltipCharacter | null;
    /** The ground cell under the mouse, where a picked inventory item is dropped (null: not on the ground). */
    groundCell?: (event: MouseEvent) => { x: number; y: number } | null;
    extensions?: () => number;
  }) {
    this.inventory = inventory;
    this.parent = parent;
    this.picked = new PickedItem(parent);
    this.tooltip = new ItemTooltipView(parent, character);
    // Get the windows ready in the background, so they open at once.
    prepareDocument('my_inventory').then(() => prepareDocument('inventory_extension')).then(() => prepareDocument('storage')).then(() => prepareDocument('trade')).catch((error) => console.warn('Interface', error));
    this.bag = new InventoryWindow({ inventory, picked: this.picked, onClose: () => this.closeAll(), onExpand: () => { void this.toggleExtension(); } });
    this.extension = new InventoryExtensionWindow({ inventory, picked: this.picked, extensions, onClose: () => { this.extension.close(); this.placeAll(); } });
    this.vault = new VaultWindow({ inventory, picked: this.picked, onClose: () => this.closeAll(), onMoney: (toVault) => this.askMoney(toVault) });
    this.shop = new ShopWindow({
      inventory, picked: this.picked, onClose: () => this.closeAll(),
      onRepairMode: () => { this.repairMode = !this.repairMode; },
      onSell: (slot, item) => this.sell(slot, item),
    });
    // Its buttons call the handlers the trade sets (tradeHandlers).
    this.trade = new TradeWindow({
      inventory, picked: this.picked,
      onClose: () => this.tradeHandlers.close(), onZen: () => this.tradeHandlers.zen(), onConfirm: () => this.tradeHandlers.confirm(),
    });
    for (const bagWindow of [this.bag, this.extension]) {
      // While trading, a right click on a bag item puts it into the trade (CTrade::ProcessMyInvenItemAutoMove).
      bagWindow.interceptRightClick = (slot, item) => {
        if (!this.trade.isOpen) return false;
        this.tradeHandlers.bagRightClick(slot, item);
        return true;
      };
      // In a repair shop's repair mode a click on an item repairs it (RepairItemAtMousePoint).
      bagWindow.onItemClick = (slot) => {
        if (!this.repairMode || !this.shop.isOpen) return false;
        this.inventory.repair(slot, false);
        return true;
      };
    }
    for (const itemWindow of [this.bag, this.extension, this.vault, this.shop, this.trade]) this.hook(itemWindow);
    inventory.onChange((what) => {
      if (what === 'window') this.syncVault();
      this.refresh();
    });
    window.addEventListener('keydown', (event) => {
      if (gameKey(event, 'i') || gameKey(event, 'v')) { event.preventDefault(); this.toggle(); }
      if (gameKey(event, 'k') && this.bag.isOpen) { event.preventDefault(); void this.toggleExtension(); }
      if (panelKey(event, 'Escape') && (this.bag.isOpen || this.extension.isOpen || this.vault.isOpen || this.shop.isOpen || this.trade.isOpen || this.docked.some((d) => d.window.isOpen))) this.closeAll();
      // L: the repair mode of a repair shop; Shift + L repairs everything (CNPCShop::UpdateKeyEvent).
      if ((gameKey(event, 'l') || gameKey(event, 'l', 'shift')) && this.shop.isOpen && this.shop.repairShop) {
        event.preventDefault();
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

  private hook(itemWindow: ItemWindow<ItemWindowModel>): void {
    itemWindow.onPickedChanged = () => { this.tooltip.hide(); this.refresh(); };
    // The hovered item's tooltip, unless an item is on the cursor (CInventoryCtrl only shows it then).
    itemWindow.onHover = (item) => {
      const box = item && !this.picked.source && itemWindow.hoverSlot !== null ? itemWindow.screenBoxOf(itemWindow.hoverSlot) : null;
      if (item && box) void this.tooltip.show(item, box, this.extraTooltipLines(itemWindow.storageKind, itemWindow.hoverSlot ?? -1));
      else this.tooltip.hide();
    };
  }

  /** Windows opened next to the inventory by other parts (the mix window): placed, refreshed and closed with these;
   * `close` returns false to keep everything open (the desktop's ClosingProcess). */
  docked: { window: ItemWindow<ItemWindowModel>; close: () => boolean }[] = [];
  dock(window: ItemWindow<ItemWindowModel>, close: () => boolean): void { this.hook(window); this.docked.push({ window, close }); }

  async toggle(): Promise<void> {
    if (this.bag.isOpen) this.closeAll();
    else { await this.bag.open(this.parent); this.placeAll(); this.syncBagToggle(); }
  }

  async toggleExtension(): Promise<void> {
    if (!this.bag.isOpen) return;
    if (this.extension.isOpen) { this.extension.close(); this.placeAll(); }
    else { await this.extension.open(this.parent); this.placeAll(); }
  }

  async syncVault(): Promise<void> {
    const open = this.inventory.openWindow;
    if (open === NpcWindow.MERCHANT || open === NpcWindow.MERCHANT1) {
      await this.bag.open(this.parent);
      await this.shop.open(this.parent);
      this.placeAll();
      this.syncBagToggle();
      return;
    }
    if (this.shop.isOpen) { this.shop.close(); this.repairMode = false; this.placeAll(); }
    if (this.inventory.openWindow === NpcWindow.VAULT) {
      const wasOpen = this.vault.isOpen;
      await this.bag.open(this.parent);
      await this.vault.open(this.parent);
      this.placeAll();
      this.syncBagToggle();
      if (!wasOpen) { this.vaultSession++; this.onVaultShown(true); }
    } else if (this.vault.isOpen) {
      this.vault.close();
      this.vaultEnded();
      this.onVaultShown(false);
      this.placeAll();
    }
  }

  closeAll(): void {
    if (this.docked.some((d) => d.window.isOpen && !d.close())) return;
    this.tooltip.hide();
    if (this.picked.source) this.picked.drop();
    if (this.vault.isOpen) { this.vault.close(); this.inventory.closeVault(); this.vaultEnded(); this.onVaultShown(false); }
    if (this.shop.isOpen) { this.shop.close(); this.repairMode = false; this.inventory.closeShop(); }
    // Closing the inventory (or Escape) while trading cancels the trade (CSystem's HideAllGroupA, CTrade::UpdateKeyEvent).
    if (this.trade.isOpen) { this.trade.close(); this.onTradeClosed(); }
    if (this.extension.isOpen) this.extension.close();
    this.bag.close();
    this.syncBagToggle();
  }

  private syncBagToggle(): void {
    const open = this.bag.isOpen;
    if (open !== this.lastBagOpen) {
      this.lastBagOpen = open;
      this.onBagToggle(open);
    }
  }

  /** What the trade window's buttons and a right click on a bag item do while trading: set by the trade. */
  tradeHandlers: { close: () => void; zen: () => void; confirm: () => void; bagRightClick: (slot: number, item: Item) => void } = {
    close: () => undefined, zen: () => undefined, confirm: () => undefined, bagRightClick: () => undefined,
  };

  /** Opens the trade window next to the inventory; the vault and a merchant close (HideAllGroupA). */
  async openTrade(): Promise<void> {
    this.tooltip.hide();
    if (this.picked.source) this.picked.drop();
    if (this.vault.isOpen) { this.vault.close(); this.inventory.closeVault(); this.vaultEnded(); this.onVaultShown(false); }
    if (this.shop.isOpen) { this.shop.close(); this.repairMode = false; this.inventory.closeShop(); }
    await this.bag.open(this.parent);
    await this.trade.open(this.parent);
    this.placeAll();
    this.syncBagToggle();
  }

  /** Closes the trade window and the inventory with it, as the server ended the trade. */
  closeTrade(): void {
    this.tooltip.hide();
    if (this.picked.source) this.picked.drop();
    if (this.extension.isOpen) this.extension.close();
    if (this.trade.isOpen) { this.trade.close(); this.bag.close(); }
    this.refresh();
    this.syncBagToggle();
  }

  /** Panels docked at the right edge before these windows (the character window): the bag moves left of them. */
  dockedRight = 0;

  placeAll(): void {
    const right = this.dockedRight * PANEL.width;
    const extWidth = this.extension.isOpen ? PANEL.width : 0;
    this.bag.place(right);
    this.extension.place(right + PANEL.width);
    this.vault.place(right + PANEL.width + extWidth);
    this.shop.place(right + PANEL.width + extWidth);
    this.trade.place(right + PANEL.width + extWidth);
    for (const d of this.docked) { d.window.place(right + PANEL.width + extWidth); d.window.refresh(); }
    this.bag.refresh();
    this.extension.refresh();
    this.vault.refresh();
    this.shop.refresh();
    this.trade.refresh();
    this.onPlaced();
  }

  refresh(): void {
    this.bag.refresh();
    this.extension.refresh();
    this.vault.refresh();
    this.shop.refresh();
    this.trade.refresh();
    for (const d of this.docked) d.window.refresh();
  }

  // Selling a bag item to the open merchant (HandleSellToNPC / InventoryProcess): not what can't be
  // sold, and an expensive item only after asking.
  sell(slot: number, item: Item): void {
    if (this.inventory.sellingSlot !== null) return;
    if (isSellingBan(item)) { this.message('These items cannot be traded.'); return; }
    if (isHighValue(item) && !window.confirm(translate('An expensive item. Check the item, please. Are you sure you want to sell it?') ?? 'An expensive item. Check the item, please. Are you sure you want to sell it?')) return;
    this.inventory.sell(slot);
  }

  /** Counts the vault's openings and closings: a zen box asked while it was open does nothing after it closed. */
  private vaultSession = 0;

  private vaultEnded(): void {
    this.vaultSession++;
    dismissConfirm('vault-zen-dialog');
  }

  /** The vault switched to another page (each page has its own zen): a zen box asked on the old page does nothing. */
  vaultPageChanged(): void {
    this.vaultEnded();
  }

  async askMoney(toVault: boolean): Promise<void> {
    const question = toVault ? 'How much zen to deposit?' : 'How much zen to withdraw?';
    const session = this.vaultSession;
    const answer = await showConfirm({
      title: translate('Vault') ?? 'Vault',
      lines: [translate(question) ?? question],
      input: { numericOnly: true, maxLength: 10, initialText: '0' },
      showCancel: true,
      tag: 'vault-zen-dialog',
    });
    // The vault it was asked for closed (and maybe opened again) while the box was open.
    if (answer.button !== 'primary' || session !== this.vaultSession || !this.vault.isOpen) return;
    const amount = Math.floor(Number(answer.text));
    if (amount > 0) this.inventory.moveMoney(toVault, amount);
  }
}
