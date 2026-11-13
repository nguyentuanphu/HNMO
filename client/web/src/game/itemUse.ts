// Using items, as the desktop client does it: the potion hotkeys Q, W, E and R (CItemHotKey, their
// bindings from the server's key configuration), right-clicking an item in the bag
// (CInventoryActionController: use it, else wear it, else put it on the ground) and the one-at-a-time
// use request (SendRequestUse / ConsumeItemRequest, EnableUse).

import type { Connection } from '../net/connection.js';
import { BAG, VAULT, Equipment, Inventory, NpcWindow, Storage, type Item } from './inventory.js';
import { allItemDefinitions, itemDefinition, type ItemDefinition } from './items.js';

/** The desktop's item type: group * 512 + number. */
export const itemType = (group: number, number: number): number => group * MAX_ITEM_INDEX + number;
const MAX_ITEM_INDEX = 512;
const POTION = 14 * MAX_ITEM_INDEX;
const HELPER = 13 * MAX_ITEM_INDEX;
const WING = 12 * MAX_ITEM_INDEX;
const ETC = 15 * MAX_ITEM_INDEX;

export const ItemKind = {
  APPLE: POTION + 0, ALE: POTION + 9, SIEGE_POTION: POTION + 7, ANTIDOTE: POTION + 8, TOWN_PORTAL_SCROLL: POTION + 10,
  BOX_OF_LUCK: POTION + 11, REMEDY_OF_LOVE: POTION + 20, SMALL_SHIELD_POTION: POTION + 35, LARGE_COMPLEX_POTION: POTION + 40,
  JACK_OLANTERN_BLESSINGS: POTION + 46, JACK_OLANTERN_DRINK: POTION + 50, ELITE_HEALING_POTION: POTION + 70, ELITE_MANA_POTION: POTION + 71,
  MEDIUM_ELITE_HEALING_POTION: POTION + 94, CHERRY_BLOSSOM_WINE: POTION + 85, CHERRY_BLOSSOM_FLOWER_PETAL: POTION + 87, ELITE_SD_POTION: POTION + 133,
  INDULGENCE: HELPER + 60, TALISMAN_OF_GUARDIAN: HELPER + 81, TALISMAN_OF_ITEM_PROTECTION: HELPER + 82,
  KALIMA_TICKET: HELPER + 48, LIFE_STONE: HELPER + 11, WIZARDS_RING: HELPER + 20,
} as const;

export const Hotkey = { Q: 0, W: 1, E: 2, R: 3 } as const;
export const HOTKEY_LETTERS = ['Q', 'W', 'E', 'R'] as const;

// CMyInventory::CanRegisterItemHotKey: the items a hotkey can be bound to.
const REGISTERABLE = new Set([
  0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 20, 35, 36, 37, 38, 39, 40, 46, 47, 48, 49, 50, 70, 71, 78, 79, 80, 81, 82, 94, 85, 86, 87, 133,
].map((number) => POTION + number));
// IsSingleHotKeyItem: items a hotkey takes only of the type it is bound to (Remedy of Love only at level 0).
const SINGLE = new Set([7, 8, 9, 10, 20, 46, 47, 48, 49, 50, 70, 71, 78, 79, 80, 81, 82, 94, 85, 86, 87, 133].map((number) => POTION + number));

export function canRegisterHotkey(type: number): boolean {
  return REGISTERABLE.has(type);
}

const definitionOfType = (type: number): ItemDefinition | null => itemDefinition(Math.floor(type / MAX_ITEM_INDEX), type % MAX_ITEM_INDEX);
const hasTag = (type: number, tag: string): boolean => definitionOfType(type)?.tags?.includes(tag) ?? false;

// ItemDatabase::GetItemTypesWithTagInUseOrder: the types with the tag from the highest type down,
// then (stable) by their potion priority, highest first.
const inUseOrderCache = new Map<string, number[]>();
export function itemTypesInUseOrder(tag: string): number[] {
  let types = inUseOrderCache.get(tag);
  if (!types) {
    const tagged: { type: number; priority: number }[] = [];
    for (const { group, definition } of allItemDefinitions()) {
      if (definition.tags?.includes(tag)) {
        tagged.push({ type: itemType(group, definition.number), priority: typeof definition.potionPriority === 'number' ? definition.potionPriority : 0 });
      }
    }
    tagged.sort((a, b) => a.type - b.type).reverse();
    types = tagged.map((entry, index) => ({ ...entry, index }))
      .sort((a, b) => b.priority - a.priority || a.index - b.index).map((entry) => entry.type);
    if (types.length) inUseOrderCache.set(tag, types);
  }
  return types;
}

// GetHotKeyItemTypes: the item types a hotkey uses, in the order it tries them.
export function hotkeyItemTypes(hotkey: number, boundType: number, boundLevel: number): number[] {
  if (SINGLE.has(boundType) && (boundType !== ItemKind.REMEDY_OF_LOVE || boundLevel === 0)) return [boundType];
  if (hasTag(boundType, 'shieldPotion')) return itemTypesInUseOrder('shieldPotion');
  if (hasTag(boundType, 'complexPotion')) return itemTypesInUseOrder('complexPotion');
  // Q and W take the healing or mana potions, E and R also something else when bound to neither.
  const healing = hasTag(boundType, 'healingPotion');
  const mana = hasTag(boundType, 'manaPotion');
  switch (hotkey) {
    case Hotkey.Q: return itemTypesInUseOrder(mana ? 'manaPotion' : 'healingPotion');
    case Hotkey.W: return itemTypesInUseOrder(healing ? 'healingPotion' : 'manaPotion');
    case Hotkey.E:
      if (healing || mana) return itemTypesInUseOrder(healing ? 'healingPotion' : 'manaPotion');
      return [ItemKind.ANTIDOTE];
    case Hotkey.R:
      if (healing || mana) return itemTypesInUseOrder(healing ? 'healingPotion' : 'manaPotion');
      return itemTypesInUseOrder('shieldPotion');
    default: return [];
  }
}

const typeOf = (item: Item): number => itemType(item.group, item.number);

// CInventoryCtrl::FindItemReverseIndex: the bag item of the type (and level, unless -1) met first
// going through the columns from the right and each column from the bottom.
export function findItemReverse(items: ReadonlyMap<number, Item>, type: number, level: number): number | null {
  let best: { slot: number; x: number; y: number } | null = null;
  for (const [slot, item] of items) {
    if (slot < BAG.firstSlot || slot >= BAG.firstSlot + BAG.columns * BAG.rows) continue;
    if (typeOf(item) !== type || (level !== -1 && item.level !== level)) continue;
    const index = slot - BAG.firstSlot;
    // The cell met first is the item's bottom right one.
    const x = index % BAG.columns + item.width - 1, y = Math.floor(index / BAG.columns) + item.height - 1;
    if (!best || x > best.x || (x === best.x && y > best.y)) best = { slot, x, y };
  }
  return best?.slot ?? null;
}

/** The Q W E R bindings: an item type and level each, from the server's key configuration. */
export class ItemHotkeys {
  readonly types = [-1, -1, -1, -1];
  readonly levels = [0, 0, 0, 0];
  /** The whole key configuration as the server sent it (skills and options too), kept to save it back. */
  private configuration = new Uint8Array(CONFIGURATION_LENGTH);
  private readonly listeners = new Set<() => void>();
  /** Told about each key configuration the server sends (the skill hotkeys read it too). */
  onConfiguration?: (configuration: Uint8Array) => void;

  constructor(private readonly connection: Connection) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  // ApplyKeyConfiguration (C1 F3 30): skill hotkeys (0-19), options (20), Q W E (21-23), chat box (24),
  // R (25), each an offset into the potions; the hotkeys' item levels (26-29, Q W E R, as the client saves them).
  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xf3 || bytes[3] !== 0x30) return;
    this.configuration = new Uint8Array(CONFIGURATION_LENGTH);
    this.configuration.set(bytes.subarray(4, 4 + CONFIGURATION_LENGTH));
    const c = this.configuration;
    [c[21], c[22], c[23], c[25]].forEach((offset, hotkey) => this.set(hotkey, POTION + offset, c[26 + hotkey]));
    this.onConfiguration?.(c);
    for (const listener of this.listeners) listener();
  }

  // SetHotKey: only items a hotkey can take; anything else unbinds it.
  private set(hotkey: number, type: number, level: number): void {
    const ok = canRegisterHotkey(type);
    this.types[hotkey] = ok ? type : -1;
    this.levels[hotkey] = ok ? level : 0;
  }

  /** Binds a hotkey to an item (Ctrl + Q/W/E/R over it in the bag) and saves the configuration. */
  bind(hotkey: number, item: Item): boolean {
    const type = typeOf(item);
    if (!canRegisterHotkey(type)) return false;
    this.set(hotkey, type, item.level);
    this.save();
    for (const listener of this.listeners) listener();
    return true;
  }

  /** Saves the skill hotkeys 0-9 (skill numbers, 0xFFFF for none) with the rest of the configuration. */
  saveSkillHotkeys(numbers: number[]): void {
    numbers.forEach((number, i) => { this.configuration[i * 2] = (number >> 8) & 0xff; this.configuration[i * 2 + 1] = number & 0xff; });
    this.save();
  }

  /** The key configuration as last received or saved. */
  get keyConfiguration(): Uint8Array { return this.configuration; }

  // SaveOptions: the desktop saves when leaving the game; a browser tab may close without warning, so
  // this saves on each change.
  save(): void {
    const c = this.configuration;
    const offset = (hotkey: number) => (this.types[hotkey] - POTION) & 0xff;
    c[21] = offset(Hotkey.Q); c[22] = offset(Hotkey.W); c[23] = offset(Hotkey.E); c[25] = offset(Hotkey.R);
    for (let hotkey = 0; hotkey < 4; hotkey++) c[26 + hotkey] = this.levels[hotkey];
    this.connection.send('ClientToServer', 'SaveKeyConfiguration', { Configuration: c });
  }

  // GetHotKeyItemIndex: the bag slot the hotkey uses now, or null.
  slotOf(hotkey: number, items: ReadonlyMap<number, Item>): number | null {
    const level = this.levels[hotkey];
    for (const type of hotkeyItemTypes(hotkey, this.types[hotkey], level)) {
      const slot = findItemReverse(items, type, hasTag(type, 'healingPotion') ? -1 : level);
      if (slot === null) continue;
      const item = items.get(slot)!;
      const levelBound = type === ItemKind.SIEGE_POTION || type === ItemKind.TOWN_PORTAL_SCROLL || type === ItemKind.REMEDY_OF_LOVE;
      if (!levelBound || item.level === level) return slot;
    }
    return null;
  }

  // GetHotKeyItemIndex(…, true): how many the hotkey has left (a potion counts its durability).
  count(hotkey: number, items: ReadonlyMap<number, Item>): number {
    const level = this.levels[hotkey];
    let total = 0;
    for (const type of hotkeyItemTypes(hotkey, this.types[hotkey], level)) {
      for (const [slot, item] of items) {
        if (slot < BAG.firstSlot || typeOf(item) !== type) continue;
        if (item.level !== level && !hasTag(type, 'healingPotion')) continue;
        total += type === ItemKind.ALE || type === ItemKind.TOWN_PORTAL_SCROLL || type === ItemKind.REMEDY_OF_LOVE ? 1 : item.durability;
      }
    }
    return total;
  }
}

const CONFIGURATION_LENGTH = 30;

export interface UseContext {
  level: number;
  strength: number;
  energy: number;
  /** Whether the hero stands in a safe zone. */
  safeZone: boolean;
  classIndex: number;
  message: (text: string) => void;
}

/** What right-clicking an item does first: use it, nothing (known but not usable now), or go on to wearing it. */
export type UseDecision = 'use' | 'handled' | 'continue';

const between = (type: number, first: number, last: number) => type >= first && type <= last;

// CInventoryActionController::TryConsumeItem, for the items that are used at once. Fruits, reset
// fruits, event tickets and talismans that open a dialog first come with their windows.
export function consumeDecision(item: Item, context: UseContext): UseDecision {
  const type = typeOf(item);
  if (type === ItemKind.TOWN_PORTAL_SCROLL) return 'use';
  const isPotion = between(type, ItemKind.APPLE, ItemKind.ALE) || between(type, ItemKind.SMALL_SHIELD_POTION, ItemKind.LARGE_COMPLEX_POTION);
  if (isPotion || (type === ItemKind.REMEDY_OF_LOVE && item.level === 0)
    || between(type, ItemKind.JACK_OLANTERN_BLESSINGS, ItemKind.JACK_OLANTERN_DRINK)
    || (type === ItemKind.BOX_OF_LUCK && item.level === 14)
    || hasTag(type, 'elitePotion') || hasTag(type, 'buffScroll') || type === ItemKind.INDULGENCE
    || type === ItemKind.MEDIUM_ELITE_HEALING_POTION
    || between(type, ItemKind.CHERRY_BLOSSOM_WINE, ItemKind.CHERRY_BLOSSOM_FLOWER_PETAL)
    || hasTag(type, 'battleOrStrengthScroll') || type === ItemKind.TALISMAN_OF_GUARDIAN
    || type === ItemKind.TALISMAN_OF_ITEM_PROTECTION || type === ItemKind.ELITE_SD_POTION) return 'use';
  // Elixirs: the server refuses one while another's effect is on (the buffs come with the buff strip).
  if (hasTag(type, 'elixir')) return 'use';
  if (type === ItemKind.KALIMA_TICKET) {
    if (context.safeZone) { context.message("Can't be used in the safe zone."); return 'handled'; }
    return 'use';
  }
  if (type === ItemKind.LIFE_STONE) return item.level === 0 || item.level === 1 ? 'use' : 'handled';
  // Skill scrolls, orbs and crystals: learnt when the level, energy and strength are enough.
  const group = item.group;
  const isSkillBook = group === 15 || between(type, WING + 7, WING + 14) || between(type, WING + 16, WING + 19)
    || type === WING + 20 || between(type, WING + 21, WING + 24) || type === WING + 35 || between(type, WING + 44, WING + 48);
  if (isSkillBook) {
    const requirements = itemDefinition(item.group, item.number)?.requirements ?? {};
    if (context.level >= (requirements.level ?? 0) && context.energy >= (requirements.energy ?? 0) && context.strength >= (requirements.strength ?? 0)) return 'use';
    return 'handled';
  }
  if (isKnownWithDialog(type)) return 'handled';
  return 'continue';
}

// Items the desktop answers with a dialog or an event window first (fruits, reset fruits, tickets,
// talismans of resurrection and mobility, armor of guardsman, …): not dropped or worn by a right click.
function isKnownWithDialog(type: number): boolean {
  return [
    HELPER + 15, // fruits
    HELPER + 18, // invisibility cloak
    HELPER + 29, // armor of guardsman
    HELPER + 46, HELPER + 47, HELPER + 51, HELPER + 61, HELPER + 121, POTION + 19, // tickets and invitations
    HELPER + 66, // invitation to the Santa village
    HELPER + 69, HELPER + 70, // talismans of resurrection and mobility
  ].includes(type) || between(type, HELPER + 54, HELPER + 58); // reset fruits
}

// GameLogic::Items::IsHighValueItem: what the game won't put on the ground.
export function isHighValue(item: Item): boolean {
  const type = typeOf(item);
  return hasTag(type, 'valuable') || item.ancientSet > 0 || item.excellent > 0 || (item.level > 6 && type < WING)
    || (type === ItemKind.WIZARDS_RING && item.level === 0);
}

const PRIMARY_SLOT: Record<string, number> = {
  mainHand: Equipment.RIGHT_HAND, twoHand: Equipment.RIGHT_HAND, bothHands: Equipment.RIGHT_HAND, offHand: Equipment.LEFT_HAND,
  helm: Equipment.HELM, armor: Equipment.ARMOR, pants: Equipment.PANTS, gloves: Equipment.GLOVES, boots: Equipment.BOOTS,
  wings: Equipment.WINGS, pet: Equipment.PET, helper: Equipment.PET, pendant: Equipment.PENDANT, amulet: Equipment.PENDANT,
  ring: Equipment.RING_RIGHT,
};
const DUAL_WIELDERS = new Set([1, 3, 6]); // Dark Knight, Magic Gladiator, Rage Fighter

// TryEquipItem / FindAlternateEquipSlot: where right-clicking wears the item, null when it can't.
export function equipSlotFor(item: Item, equipped: ReadonlyMap<number, Item>, classIndex: number): number | null | 'none' {
  const definition = itemDefinition(item.group, item.number);
  const primary = definition?.slot ? PRIMARY_SLOT[definition.slot] : undefined;
  if (primary === undefined) return 'none';
  if (!equipped.has(primary)) return primary;
  let alternate: number | null = null;
  if (primary === Equipment.RIGHT_HAND && DUAL_WIELDERS.has(classIndex) && !definition?.twoHanded) alternate = Equipment.LEFT_HAND;
  else if (primary === Equipment.RING_RIGHT) alternate = Equipment.RING_LEFT;
  return alternate !== null && !equipped.has(alternate) ? alternate : null;
}

/** Sends use requests one at a time, like the desktop's EnableUse. */
export class ItemUser {
  private busy = false;
  private busyTimer: ReturnType<typeof setTimeout> | undefined;

  constructor(private readonly connection: Connection, private readonly inventory: Inventory, private readonly message: (text: string) => void) {
    // The answers that free the next use: item removed (C1 28), durability (C1 2A), life/mana (C1 26),
    // a server message (C1 0D).
    connection.onPacket((bytes) => {
      if (bytes[0] === 0xc1 && [0x26, 0x28, 0x2a, 0x0d].includes(bytes[2])) this.free();
    });
  }

  private free(): void {
    this.busy = false;
    clearTimeout(this.busyTimer);
  }

  /** ConsumeItemRequest for a bag slot; false when not sent. */
  use(slot: number, target = 0): boolean {
    // IsCanUseItem: not while the vault (or a trade) is open.
    if (this.inventory.openWindow === NpcWindow.VAULT) {
      this.message('You cannot use your items while using the vault or while trading.');
      return false;
    }
    if (this.busy) return false;
    this.busy = true;
    // The desktop waits for the answer only; a lost answer would block items for good, so give up after a while.
    this.busyTimer = setTimeout(() => { this.busy = false; }, 1500);
    this.connection.send('ClientToServer', 'ConsumeItemRequest', { ItemSlot: slot, TargetSlot: target, FruitConsumption: 1 });
    return true;
  }
}

export interface RightClickContext extends UseContext {
  inventory: Inventory;
  user: ItemUser;
  /** The hero's cell, null while dead (nothing goes on the ground then). */
  heroCell: () => { x: number; y: number } | null;
  /** Sells to the open merchant (HandleSellToNPC). */
  sell?: (slot: number, item: Item) => void;
}

// CInventoryActionController::HandleRightClick for a bag item: into the vault while it's open; else
// use it, wear it, or put it on the ground at the hero's feet.
export function rightClickBagItem(slot: number, item: Item, context: RightClickContext): void {
  const { inventory } = context;
  if ((inventory.openWindow === NpcWindow.MERCHANT || inventory.openWindow === NpcWindow.MERCHANT1) && context.sell) {
    if (slot >= BAG.firstSlot) context.sell(slot, item);
    return;
  }
  if (inventory.openWindow === NpcWindow.VAULT) {
    // CStorageInventory::ProcessMyInvenItemAutoMove (not the Wizard's Ring).
    if (slot < BAG.firstSlot || typeOf(item) === ItemKind.WIZARDS_RING) return;
    const target = inventory.firstEmptySlot(inventory.vault, VAULT, item);
    if (target !== null) inventory.move(Storage.INVENTORY, slot, Storage.VAULT, target);
    return;
  }
  if (slot < BAG.firstSlot) return; // worn items are taken off by dragging
  const decision = consumeDecision(item, context);
  if (decision === 'use') { context.user.use(slot); return; }
  if (decision === 'handled') return;
  const equipSlot = equipSlotFor(item, inventory.items, context.classIndex);
  if (typeof equipSlot === 'number') { inventory.move(Storage.INVENTORY, slot, Storage.INVENTORY, equipSlot); return; }
  if (equipSlot === null) return; // wearable, but its slots are taken
  const cell = context.heroCell();
  if (!cell) return;
  if (isHighValue(item)) { context.message('You are not allowed to drop this expensive item.'); return; }
  if (itemDefinition(item.group, item.number)?.droppable === false) { context.message('This item cannot be dropped.'); return; }
  inventory.dropToGround(slot, cell);
}

// GameLogic::Items::IsSellingBan: Rena +1, Wizard's Ring +1/+2, Remedy of Love +1..+5 and what the
// item data marks as not sellable.
export function isSellingBan(item: Item): boolean {
  const type = typeOf(item);
  if ((type === POTION + 21 && item.level === 1) || (type === ItemKind.WIZARDS_RING && (item.level === 1 || item.level === 2))
    || (type === ItemKind.REMEDY_OF_LOVE && item.level >= 1 && item.level <= 5)) return true;
  return itemDefinition(item.group, item.number)?.sellable === false;
}

const JEWEL_OF_BLESS = POTION + 13, JEWEL_OF_SOUL = POTION + 14, JEWEL_OF_LIFE = POTION + 16, JEWEL_OF_HARMONY = POTION + 42;
const HORN_OF_FENRIR = HELPER + 37, WINGS_OF_DARKNESS = WING + 6;

/** Whether an item is a jewel that's applied by dropping it on another item. */
export function isApplicableJewel(item: Item): boolean {
  const type = typeOf(item);
  return type === JEWEL_OF_BLESS || type === JEWEL_OF_SOUL || type === JEWEL_OF_LIFE || type === JEWEL_OF_HARMONY
    || hasTag(type, 'refineStone') || type === POTION + 160 || type === POTION + 161;
}

// CInventoryActionController::ApplyJewels: whether dropping the jewel on the item uses it on it.
// 'confirm' for a Jewel of Bless on a worn Horn of Fenrir (it repairs the horn after asking).
export function jewelApplies(jewel: Item, target: Item): boolean | 'confirm' {
  if (!isApplicableJewel(jewel)) return false;
  const jewelType = typeOf(jewel), type = typeOf(target);
  const definition = itemDefinition(target.group, target.number);
  let ok = true;
  if (type > WINGS_OF_DARKNESS && !definition?.wingTier) ok = false;
  if (hasTag(type, 'ammunition')) ok = false;
  if ((jewelType === JEWEL_OF_BLESS && target.level >= 6) || (jewelType === JEWEL_OF_SOUL && target.level >= 9)) ok = false;
  if (jewelType === JEWEL_OF_BLESS && type === HORN_OF_FENRIR && target.durability !== 255) return 'confirm';
  // Harmony needs an item without sockets or a harmony option yet (the kinds it strengthens come with harmony itself).
  if (jewelType === JEWEL_OF_HARMONY && (target.sockets.length > 0 || target.harmony !== 0)) ok = false;
  if (hasTag(jewelType, 'refineStone') && (target.sockets.length > 0 || target.harmony === 0)) ok = false;
  if (hasTag(type, 'luckyItem')) {
    ok = false;
    if (jewelType === POTION + 161 && target.harmony === 0) ok = true;
    else if (jewelType === POTION + 160 && target.durability > 0) ok = true;
  }
  return ok;
}
