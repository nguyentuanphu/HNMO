// The character's items as the server keeps them: the equipment (slots 0-11), the bag
// (slots 12-75, 8 x 8 cells), the vault while it's open (120 slots, 8 x 15), and the money.
// Follows the server's packets like the desktop client's InventoryUpdates / StorageUpdates.

import { itemDefinition } from './items.js';
import type { Connection } from '../net/connection.js';

export const EQUIPMENT_SLOTS = 12;
export const Equipment = {
  RIGHT_HAND: 0, LEFT_HAND: 1, HELM: 2, ARMOR: 3, PANTS: 4, GLOVES: 5, BOOTS: 6, WINGS: 7,
  PET: 8, PENDANT: 9, RING_LEFT: 10, RING_RIGHT: 11,
} as const;
export type Equipment = (typeof Equipment)[keyof typeof Equipment];

/** A grid of item cells: its first slot number and its size in cells. */
export interface ItemGrid {
  firstSlot: number;
  columns: number;
  rows: number;
}

export const BAG: ItemGrid = { firstSlot: 12, columns: 8, rows: 8 };
export const VAULT: ItemGrid = { firstSlot: 0, columns: 8, rows: 15 };
export const Storage = { INVENTORY: 0, TRADE: 1, VAULT: 2, CHAOS_MACHINE: 3, PLAYER_SHOP: 4 } as const;
export type Storage = (typeof Storage)[keyof typeof Storage];
export const NpcWindow = { MERCHANT: 0, MERCHANT1: 1, VAULT: 2, CHAOS_MACHINE: 3 } as const;
export type NpcWindow = (typeof NpcWindow)[keyof typeof NpcWindow];

const Flag = { OPTION: 0x01, LUCK: 0x02, SKILL: 0x04, EXCELLENT: 0x08, ANCIENT: 0x10, HARMONY: 0x20, GUARDIAN: 0x40, SOCKETS: 0x80 } as const;

/** An item as the server sends it (decodeItem), with its size in cells from its definition. */
export interface Item {
  group: number;
  number: number;
  level: number;
  durability: number;
  luck: boolean;
  skill: boolean;
  guardian: boolean;
  optionLevel: number;
  optionType: number;
  /** The excellent option bits, 0 for none. */
  excellent: number;
  ancientSet: number;
  ancientBonus: number;
  harmony: number;
  sockets: number[];
  socketBonus: number;
  /** Bytes the item took in the packet. */
  length: number;
  width: number;
  height: number;
}

/** An item and the slot it's stored at. */
export interface StoredItem {
  slot: number;
  item: Item;
}

/** What changed, for Inventory.onChange listeners. */
export type InventoryChange = 'inventory' | 'vault' | 'shop' | 'window' | 'money';

/** A move the server hasn't answered yet. */
export interface PendingMove {
  fromStorage: number;
  fromSlot: number;
  toStorage: number;
  toSlot: number;
}

// An item in the server's extended format (ItemSerializerExtended): 5 to 15 bytes.
export function decodeItem(bytes: Uint8Array, at = 0): Item {
  const flags = bytes[at + 4];
  let next = at + 5;
  // length, width and height are set below
  const item: Item = {
    group: bytes[at] >> 4,
    number: ((bytes[at] & 0x0f) << 8) | bytes[at + 1],
    level: bytes[at + 2],
    durability: bytes[at + 3],
    luck: (flags & Flag.LUCK) !== 0,
    skill: (flags & Flag.SKILL) !== 0,
    guardian: (flags & Flag.GUARDIAN) !== 0,
    optionLevel: 0, optionType: 0, excellent: 0, ancientSet: 0, ancientBonus: 0, harmony: 0, sockets: [], socketBonus: 0,
    length: 0, width: 1, height: 1,
  };
  if (flags & Flag.OPTION) { item.optionLevel = bytes[next] & 0x0f; item.optionType = bytes[next] >> 4; next++; }
  if (flags & Flag.EXCELLENT) item.excellent = bytes[next++];
  if (flags & Flag.ANCIENT) { item.ancientSet = bytes[next] & 0x0f; item.ancientBonus = bytes[next] >> 4; next++; }
  if (flags & Flag.HARMONY) item.harmony = bytes[next++];
  if (flags & Flag.SOCKETS) {
    const count = bytes[next] & 0x0f;
    item.socketBonus = bytes[next] >> 4;
    next++;
    item.sockets = [...bytes.slice(next, next + count)];
    next += count;
  }
  item.length = next - at;
  const definition = itemDefinition(item.group, item.number);
  item.width = definition?.width ?? 1;
  item.height = definition?.height ?? 1;
  return item;
}

// [{ slot, item }] of a stored-items list (inventory, vault, shop): slot byte then the item.
export function decodeStoredItems(bytes: Uint8Array, at: number, count: number): StoredItem[] {
  const result: StoredItem[] = [];
  for (let i = 0; i < count && at < bytes.length; i++) {
    const slot = bytes[at];
    const item = decodeItem(bytes, at + 1);
    result.push({ slot, item });
    at += 1 + item.length;
  }
  return result;
}

// Where the packet's own data starts and its code: C1/C3 have a one byte length, C2/C4 two.
function header(bytes: Uint8Array): { code: number; data: number } {
  const long = bytes[0] === 0xc2 || bytes[0] === 0xc4;
  return { code: bytes[long ? 3 : 2], data: long ? 4 : 3 };
}

const u32le = (b: Uint8Array, at: number) => (b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24)) >>> 0;
const u32be = (b: Uint8Array, at: number) => ((b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3]) >>> 0;

export class Inventory {
  connection: Connection;
  items: Map<number, Item>;
  vault: Map<number, Item>;
  money: number;
  vaultMoney: number;
  /** The NPC window the server opened (a NpcWindow value), null for none. */
  openWindow: number | null;
  pendingMove: PendingMove | null;
  listeners: Set<(what: InventoryChange) => void>;
  stop: () => void;
  /** A merchant's items, from the last StoreItemList that wasn't the vault's. */
  shopItems?: StoredItem[];
  lastMoveTouchedVault?: boolean;

  constructor(connection: Connection, { money = 0 }: { money?: number } = {}) {
    this.connection = connection;
    this.items = new Map(); // inventory slot -> item (equipment and bag)
    this.vault = new Map(); // vault slot -> item
    this.money = money;
    this.vaultMoney = 0;
    this.openWindow = null; // the NPC window the server opened (NpcWindow)
    this.pendingMove = null;
    this.listeners = new Set();
    this.stop = connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: (what: InventoryChange) => void): () => boolean {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  changed(what: InventoryChange): void {
    for (const listener of this.listeners) listener(what);
  }

  storage(kind: number): Map<number, Item> {
    return kind === Storage.VAULT ? this.vault : this.items;
  }

  handle(bytes: Uint8Array): void {
    const { code, data } = header(bytes);
    const c1 = bytes[0] === 0xc1, c3 = bytes[0] === 0xc3, c4 = bytes[0] === 0xc4, c2 = bytes[0] === 0xc2;
    if (c4 && code === 0xf3 && bytes[data] === 0x10) {
      // CharacterInventory: the whole inventory after entering the world
      this.items.clear();
      for (const { slot, item } of decodeStoredItems(bytes, data + 2, bytes[data + 1])) this.items.set(slot, item);
      this.changed('inventory');
    } else if (c2 && code === 0x31) {
      // StoreItemList: the vault's (or a merchant's) items
      const kind = bytes[data];
      const list = decodeStoredItems(bytes, data + 2, bytes[data + 1]);
      if (this.openWindow === NpcWindow.VAULT || kind === 0) {
        this.vault.clear();
        for (const { slot, item } of list) this.vault.set(slot, item);
        this.changed('vault');
      } else {
        this.shopItems = list;
        this.changed('shop');
      }
    } else if (c3 && code === 0x30) {
      this.openWindow = bytes[data];
      this.changed('window');
    } else if (c3 && code === 0x22 && bytes[data] === 0xfe) {
      this.money = u32be(bytes, data + 1);
      this.changed('money');
    } else if (c3 && code === 0x22 && bytes.length > 5) {
      // ItemAddedToInventory (picked up, bought, …)
      this.items.set(bytes[data], decodeItem(bytes, data + 1));
      this.changed('inventory');
    } else if (c3 && code === 0x24 && bytes[data] === 0xff) {
      this.pendingMove = null; // ItemMoveRequestFailed: the item stays where it was
      this.changed('inventory');
    } else if (c3 && code === 0x24) {
      // ItemMoved: the item of our last move request is at its new place
      const target = this.storage(bytes[data]);
      const item = decodeItem(bytes, data + 2);
      if (this.pendingMove) this.storage(this.pendingMove.fromStorage).delete(this.pendingMove.fromSlot);
      target.set(bytes[data + 1], item);
      this.pendingMove = null;
      this.changed(bytes[data] === Storage.VAULT ? 'vault' : 'inventory');
      if (this.lastMoveTouchedVault) this.changed('vault');
    } else if (c1 && code === 0x23) {
      // ItemDropResponse: the item we put on the ground left the inventory
      if (bytes[data] !== 0) { this.items.delete(bytes[data + 1]); this.changed('inventory'); }
    } else if (c1 && code === 0x28) {
      this.items.delete(bytes[data]);
      this.changed('inventory');
    } else if (c1 && code === 0x2a) {
      const item = this.items.get(bytes[data]);
      if (item) { item.durability = bytes[data + 1]; this.changed('inventory'); }
    } else if (c1 && code === 0xf3 && bytes[data] === 0x14) {
      this.items.set(bytes[data + 1], decodeItem(bytes, data + 2));
      this.changed('inventory');
    } else if (c1 && code === 0x81) {
      // VaultMoneyUpdate
      this.vaultMoney = u32le(bytes, data + 1);
      this.money = u32le(bytes, data + 5);
      this.changed('money');
    } else if (c1 && code === 0x82) {
      this.openWindow = null;
      this.vault.clear();
      this.changed('window');
    }
  }

  // Asks the server to move an item; the change shows once it answers (ItemMoved).
  move(fromStorage: number, fromSlot: number, toStorage: number, toSlot: number): void {
    if (this.pendingMove) return;
    this.pendingMove = { fromStorage, fromSlot, toStorage, toSlot };
    this.lastMoveTouchedVault = fromStorage === Storage.VAULT || toStorage === Storage.VAULT;
    this.connection.send('ClientToServer', 'ItemMoveRequestExtended', { FromStorage: fromStorage, FromSlot: fromSlot, ToStorage: toStorage, ToSlot: toSlot });
    setTimeout(() => { if (this.pendingMove?.fromSlot === fromSlot) { this.pendingMove = null; this.changed('inventory'); } }, 3000);
  }

  // Puts an inventory item on the ground at a cell (DropItemRequest); the server answers with ItemDropResponse.
  dropToGround(slot: number, cell: { x: number; y: number }): void {
    this.connection.send('ClientToServer', 'DropItemRequest', { TargetX: cell.x, TargetY: cell.y, ItemSlot: slot });
  }

  moveMoney(toVault: boolean, amount: number): void {
    this.connection.send('ClientToServer', 'VaultMoveMoneyRequest', { Direction: toVault ? 0 : 1, Amount: amount });
  }

  closeVault(): void {
    this.connection.send('ClientToServer', 'VaultClosed', {});
    this.openWindow = null;
    this.vault.clear();
    this.changed('window');
  }

  // The cells of a grid an item at this slot covers, or null when it doesn't fit.
  static cellsOf(grid: ItemGrid, slot: number, item: Pick<Item, 'width' | 'height'>): number[] | null {
    const index = slot - grid.firstSlot;
    const x = index % grid.columns, y = Math.floor(index / grid.columns);
    if (index < 0 || x + item.width > grid.columns || y + item.height > grid.rows) return null;
    const cells: number[] = [];
    for (let dy = 0; dy < item.height; dy++) for (let dx = 0; dx < item.width; dx++) cells.push(grid.firstSlot + (y + dy) * grid.columns + x + dx);
    return cells;
  }

  // CInventoryCtrl::FindEmptySlot: the first slot, row by row, where the item fits; null for none.
  firstEmptySlot(storage: Map<number, Item>, grid: ItemGrid, item: Item): number | null {
    for (let index = 0; index < grid.columns * grid.rows; index++) {
      if (this.fits(storage, grid, grid.firstSlot + index, item, null)) return grid.firstSlot + index;
    }
    return null;
  }

  // Whether an item fits at a grid slot, leaving out the item being moved.
  fits(storage: Map<number, Item>, grid: ItemGrid, slot: number, item: Pick<Item, 'width' | 'height'>, ignoreSlot?: number | null): boolean {
    const cells = Inventory.cellsOf(grid, slot, item);
    if (!cells) return false;
    const occupied = new Set<number>();
    for (const [s, other] of storage) {
      if (s === ignoreSlot || s < grid.firstSlot) continue;
      for (const cell of Inventory.cellsOf(grid, s, other) ?? []) occupied.add(cell);
    }
    return cells.every((cell) => !occupied.has(cell));
  }
}
