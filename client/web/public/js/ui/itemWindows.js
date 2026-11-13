// The inventory (my_inventory.rml) and the vault (storage.rml), as the desktop client's
// CMyInventory and CStorageInventory: the equipment slots and the 8 x 8 bag, the vault's 8 x 15
// grid, the money, and moving items by clicking an item to pick it up and clicking where it goes.

import { openDocument, prepareDocument, REFERENCE_HEIGHT } from './rml.js';
import { drawItem } from './itemIcons.js';
import { BAG, VAULT, Equipment, Inventory, NpcWindow, Storage } from '../game/inventory.js';
import { itemName } from '../game/items.js';

const CELL = 20; // the grid's cell pitch in reference pixels (item_grid.rcss --item-cell)
const PANEL = { width: 190, height: 429 };

// The equipment slots' elements in my_inventory.rml, by inventory slot.
const SLOT_ELEMENTS = {
  [Equipment.RIGHT_HAND]: 'slot_weapon_right', [Equipment.LEFT_HAND]: 'slot_weapon_left', [Equipment.HELM]: 'slot_helm',
  [Equipment.ARMOR]: 'slot_armor', [Equipment.PANTS]: 'slot_pants', [Equipment.GLOVES]: 'slot_gloves', [Equipment.BOOTS]: 'slot_boots',
  [Equipment.WINGS]: 'slot_wing', [Equipment.PET]: 'slot_helper', [Equipment.PENDANT]: 'slot_amulet',
  [Equipment.RING_LEFT]: 'slot_ring_left', [Equipment.RING_RIGHT]: 'slot_ring_right',
};

// Which item groups each equipment slot takes (IsEquipable, by the item's slot in Data/Items).
const SLOT_OF_DEFINITION = {
  mainHand: [Equipment.RIGHT_HAND, Equipment.LEFT_HAND], offHand: [Equipment.LEFT_HAND], twoHand: [Equipment.RIGHT_HAND],
  bothHands: [Equipment.RIGHT_HAND, Equipment.LEFT_HAND], helm: [Equipment.HELM], armor: [Equipment.ARMOR], pants: [Equipment.PANTS],
  gloves: [Equipment.GLOVES], boots: [Equipment.BOOTS], wings: [Equipment.WINGS], pet: [Equipment.PET], helper: [Equipment.PET],
  pendant: [Equipment.PENDANT], amulet: [Equipment.PENDANT], ring: [Equipment.RING_LEFT, Equipment.RING_RIGHT],
};

export function formatGold(amount) {
  return Math.floor(amount).toLocaleString('en-US');
}

export function goldTier(amount) {
  if (amount >= 10_000_000) return 'ten-million';
  if (amount >= 1_000_000) return 'million';
  if (amount >= 100_000) return 'hundred-thousand';
  return 'base';
}

function uiScale() {
  return window.innerHeight / REFERENCE_HEIGHT;
}

function emptyCells(count) {
  return Array.from({ length: count }, () => ({ tint: '', drop: '', count: '' }));
}

// The item following the mouse after it was picked up (CInventoryCtrl's picked item).
class PickedItem {
  constructor(layer) {
    this.canvas = document.createElement('canvas');
    Object.assign(this.canvas.style, { position: 'fixed', pointerEvents: 'none', zIndex: 60, display: 'none' });
    layer.append(this.canvas);
    this.source = null;
    window.addEventListener('pointermove', (event) => this.follow(event));
  }

  async pick(source, item) {
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

  follow(event) {
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
class ItemWindow {
  constructor({ inventory, picked, documentName, grid, storage, model }) {
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

  get items() {
    return this.inventory.storage(this.storageKind);
  }

  async open(parent) {
    if (this.doc) return;
    this.doc = await openDocument(this.documentName, this.model, { parent, scale: uiScale() });
    this.panel = this.doc.$('#panel');
    // The theme leaves pointer events to the game's own hit tests (as the desktop client does them in C++).
    this.panel.style.pointerEvents = 'auto';
    this.layer = document.createElement('div');
    Object.assign(this.layer.style, { position: 'absolute', left: 0, top: 0, width: '100%', height: '100%', pointerEvents: 'none' });
    this.panel.append(this.layer);
    this.panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    this.panel.addEventListener('click', (event) => this.click(event));
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

  get isOpen() { return Boolean(this.doc); }

  // Panel placement: docked from the right edge, like the original's dock-right column.
  place(right) {
    if (!this.doc) return;
    const scale = uiScale();
    Object.assign(this.panel.style, { left: `${window.innerWidth - (right + PANEL.width) * scale}px`, top: '0px', transform: `scale(${scale})` });
    this.doc.setScale(scale);
    this.model.text_px = Math.round(12 * scale);
    this.doc.update();
  }

  layout() {
    const gridElement = this.doc.$('#item_grid');
    this.gridOrigin = { x: gridElement.offsetLeft, y: gridElement.offsetTop };
  }

  // The panel point (reference px) of a mouse event.
  pointOf(event) {
    const rect = this.panel.getBoundingClientRect();
    const scale = rect.width / PANEL.width;
    return { x: (event.clientX - rect.left) / scale, y: (event.clientY - rect.top) / scale };
  }

  gridSlotAt(point) {
    const col = Math.floor((point.x - this.gridOrigin.x) / CELL);
    const row = Math.floor((point.y - this.gridOrigin.y) / CELL);
    if (col < 0 || row < 0 || col >= this.grid.columns || row >= this.grid.rows) return null;
    return this.grid.firstSlot + row * this.grid.columns + col;
  }

  // The item under a grid slot: the item whose cells cover it.
  itemAtGridSlot(slot) {
    for (const [s, item] of this.items) {
      if (s < this.grid.firstSlot) continue;
      if (Inventory.cellsOf(this.grid, s, item)?.includes(slot)) return s;
    }
    return null;
  }

  equipmentSlotAt() { return null; }

  // Where a picked item would go for this pointer: the slot of its top-left cell.
  dropSlotFor(point, item) {
    const equipment = this.equipmentSlotAt(point);
    if (equipment !== null) return { slot: equipment, equipment: true };
    const col = Math.round((point.x - this.gridOrigin.x) / CELL - item.width / 2);
    const row = Math.round((point.y - this.gridOrigin.y) / CELL - item.height / 2);
    if (col < 0 || row < 0 || col + item.width > this.grid.columns || row + item.height > this.grid.rows) return null;
    return { slot: this.grid.firstSlot + row * this.grid.columns + col, equipment: false };
  }

  click(event) {
    if (event.target.closest?.('#btn_exit, #frame_corner_close, .inv-btn, .stor-btn')) return;
    const point = this.pointOf(event);
    if (this.picked.source) {
      const item = this.picked.source.item;
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
    this.picked.pick({ storage: this.storageKind, slot }, this.items.get(slot));
    this.picked.follow(event);
    this.onPickedChanged?.();
  }

  hover(event) {
    const point = event ? this.pointOf(event) : null;
    let slot = null;
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
  updateDropPreview(point) {
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
  boxOf(slot, item) {
    const index = slot - this.grid.firstSlot;
    return {
      x: this.gridOrigin.x + (index % this.grid.columns) * CELL,
      y: this.gridOrigin.y + Math.floor(index / this.grid.columns) * CELL,
      w: item.width * CELL,
      h: item.height * CELL,
    };
  }

  refresh() {
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

  updateModel() {}

  drawItems() {
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

export class InventoryWindow extends ItemWindow {
  constructor({ inventory, picked, onClose }) {
    const model = {
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

  layout() {
    super.layout();
    this.slotBoxes = new Map();
    for (const [slot, id] of Object.entries(SLOT_ELEMENTS)) {
      const element = this.doc.$(`#${id}`);
      if (element) this.slotBoxes.set(Number(slot), { x: element.offsetLeft, y: element.offsetTop, w: element.offsetWidth, h: element.offsetHeight });
    }
  }

  equipmentSlotAt(point) {
    for (const [slot, box] of this.slotBoxes) {
      if (point.x >= box.x && point.x < box.x + box.w && point.y >= box.y && point.y < box.y + box.h) return slot;
    }
    return null;
  }

  boxOf(slot, item) {
    if (slot < BAG.firstSlot) return this.slotBoxes.get(slot) ?? null;
    if (slot >= BAG.firstSlot + BAG.columns * BAG.rows) return null; // the extensions aren't shown yet
    return super.boxOf(slot, item);
  }

  updateModel() {
    this.model.gold_text = formatGold(this.inventory.money);
    this.model.gold_tier = goldTier(this.inventory.money);
    this.model.slot_states = Array.from({ length: 12 }, (_, slot) => (this.items.has(slot) ? '' : ''));
  }
}

export class VaultWindow extends ItemWindow {
  constructor({ inventory, picked, onClose, onMoney }) {
    const model = {
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

  updateModel() {
    this.model.zen_text = formatGold(this.inventory.vaultMoney);
    this.model.zen_tier = goldTier(this.inventory.vaultMoney);
  }
}

// The inventory and vault windows with the item on the cursor, and the keys that open them.
export class ItemWindows {
  constructor({ inventory, parent = document.body }) {
    this.inventory = inventory;
    this.parent = parent;
    this.picked = new PickedItem(parent);
    // Get the windows ready in the background, so they open at once.
    prepareDocument('my_inventory').then(() => prepareDocument('storage')).catch((error) => console.warn('Interface', error));
    this.bag = new InventoryWindow({ inventory, picked: this.picked, onClose: () => this.closeAll() });
    this.vault = new VaultWindow({ inventory, picked: this.picked, onClose: () => this.closeAll(), onMoney: (toVault) => this.askMoney(toVault) });
    for (const window of [this.bag, this.vault]) window.onPickedChanged = () => this.refresh();
    inventory.onChange((what) => {
      if (what === 'window') this.syncVault();
      this.refresh();
    });
    window.addEventListener('keydown', (event) => {
      if (event.target.closest?.('input, textarea')) return;
      const key = event.key.toLowerCase();
      if (key === 'i' || key === 'v') { event.preventDefault(); this.toggle(); }
      if (key === 'escape' && (this.bag.isOpen || this.vault.isOpen)) this.closeAll();
    });
    window.addEventListener('resize', () => this.placeAll());
    // A click outside the windows with an item on the cursor puts it back (the original drops it on the ground).
    window.addEventListener('pointerdown', () => {
      if (this.picked.source) { this.picked.drop(); this.refresh(); }
    });
  }

  async toggle() {
    if (this.bag.isOpen) this.closeAll();
    else { await this.bag.open(this.parent); this.placeAll(); }
  }

  async syncVault() {
    if (this.inventory.openWindow === NpcWindow.VAULT) {
      await this.bag.open(this.parent);
      await this.vault.open(this.parent);
      this.placeAll();
    } else if (this.vault.isOpen) {
      this.vault.close();
      this.placeAll();
    }
  }

  closeAll() {
    if (this.picked.source) this.picked.drop();
    if (this.vault.isOpen) { this.vault.close(); this.inventory.closeVault(); }
    this.bag.close();
  }

  placeAll() {
    this.bag.place(0);
    this.vault.place(PANEL.width);
    this.bag.refresh();
    this.vault.refresh();
  }

  refresh() {
    this.bag.refresh();
    this.vault.refresh();
  }

  askMoney(toVault) {
    const text = window.prompt(toVault ? 'How much zen to deposit?' : 'How much zen to withdraw?', '0');
    const amount = Math.floor(Number(text));
    if (amount > 0) this.inventory.moveMoney(toVault, amount);
  }
}
