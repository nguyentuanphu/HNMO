// The chaos machine (mix_inventory.rml) as the desktop client's CMixInventory (UI/Inventory/MixInventory.cpp) and
// its packet handlers (WSclient.cpp ReceiveTalk, ReceiveMixExtended, ReceiveTradeInventoryExtended): talking to the
// Chaos Goblin (and the other mix NPCs) opens the 8 x 4 mix grid next to the inventory; items move in and out like
// in the vault (ItemMoveRequestExtended to the mix storage); the window shows which recipe the items make
// (game/mixRecipes.ts, the desktop's MixMgr), its success rate and zen, the ingredients; the mix button asks and
// sends ChaosMachineMixRequest with the recipe's mix id; the result fills the grid again.

import { BAG, MIX, NpcWindow, Storage, isMixStorage } from '../game/inventory.js';
import type { Inventory, Item } from '../game/inventory.js';
import { loadTooltipData } from '../game/itemTooltip.js';
import { loadItemValueData } from '../game/itemValue.js';
import { MIX_STORAGE, MixRecipes, MixType, SourceStatus, format, loadMixRecipes, mixItemsOf, recipeName, recipeText } from '../game/mixRecipes.js';
import type { MixItem } from '../game/mixRecipes.js';
import type { Connection } from '../net/connection.js';
import { ItemWindow, emptyCells, formatGold } from './itemWindows.js';
import { prepareDocument } from './rml.js';
import type { ItemWindowModel, ItemWindows, PickedItem } from './itemWindows.js';

/** CMixInventory::MIX_STATE. */
export const MixState = { READY: 0, REQUESTED: 1, FINISHED: 2 } as const;
export type MixState = (typeof MixState)[keyof typeof MixState];

// The desktop's texts (I18N::Game).
const TEXT = {
  chaos: 'Chaos', regular: 'Regular Combination', chaosWeapon: 'Chaos Weapon Combination', itemOption: 'Item option combination',
  store: 'Store', resurrectSpirit: 'Resurrect spirit', refine: 'Refine', restore: 'Restore', chaosCard: 'Chaos card combination',
  cherry: 'Spirit of Cherry Blossoms', extraction: 'Extraction', assembly: 'Assembly', application: 'Application', destruction: 'Destruction',
  combining: 'Combining', resurrection: 'Resurrection', resurrecting: 'Item resurrection', refining: 'Refining', restoring: 'Restoring',
  taxRate: 'Tax rate: %d%% (changed in real-time)', successRate: '%s Success rate: %d%%', requiredZen: 'Required zen: %s(%s)',
  requiredZenPotion: 'Required zen for potion: %s(%s)', prediction: 'Assembly prediction: %s', upload: 'Please upload the assembly items.',
  improper: 'Improper items for combination', inappropriate: 'Item inappropriate for %s', add380: 'Add 380 item option',
  notEnoughZen: 'Not enough Zen to combine items', lackOf: 'You are lack of %s items.', fromLevel: 'from above the level %d, %s enabled and on.',
  organize: 'Combine items after organizing your inventory.', talismans: 'You cannot use the Talisman of Chaos Assembly and Talisman of Luck together.',
  wantToCombine: 'Do you want to combine your items?', closeFirst: 'Close inventory after moving your items in the inventory.',
  succeeded: 'Chaos combination has succeeded', failed: 'Chaos combination has failed', wasSuccessful: '%s was successful.',
  hasFailed: '%s has failed..', hasFailed2112: '%s has failed.', chaosCardOperation: 'Chaos card combination',
  cherryOperation: 'Cherry blossom branch assembly', level10: 'Must be over level 10 to combine the invitation to Devil Square.',
  level15: 'Must be over level 15 to combine a Cloak of Invisibility.', resurrectionFailed: 'Resurrection failed.',
  selectMethod: 'Select method of combination', regularLines: ["Wings(7 types), Fruit, Devil's Invitation", 'Dinorant, +10, +15 items, Cloak of Invisibility', "Fenrir's Horn, Scroll of Blood, Condor's Feather"],
  chaosWeaponLines: ['Chaos Dragon Axe, Chaos Lightning Staff', 'Chaos Nature Bow'], close: 'Close',
  warning: '!! Warning !!', refinery: 'Refinery has started. Refinery is a part of process to change the item to Refining Stone to be reinforced. Refined item will be disapper, make sure to check the item.',
  refineItem: 'Refine the item to create', refiningStone: 'the Refining Stone.', forOnly: '%s for only %s', weaponsOrShields: 'Weapons or shields',
  allowed: 'Allowed', disappear: 'Item will disappear when failed.',
  restoration: ['Restoration is deleting the', 'reinforcement option', 'of the weapons.', 'For restoring reinforced item,', 'reinforcement option has to be', 'deleted through restoration.'],
  elpis: ['Getting through refining process', 'of Jewel of Harmony, orignal', 'gemstone will give more power.'],
  onceAtATime: 'Combinations can be used once at a time', space2x4: 'More than 2 X 4 space in inventory is needed.',
  specialItems: 'You can achieve special items with combinations.', golden255: '255 Golden Cherry Blossom Branches',
  sameCherry: 'Only the same type of Cherry Blossoms branches can be uploaded.', selectApplicable: 'Select applicable socket',
  selectDestructible: 'Select destructible socket', castleSenior: ['Brave one,', 'now you have become', 'a lord of the castle.', 'May the blessings', 'of the guardian', 'be upon you.'],
} as const;

// The window's title (the former RenderFrame() title switch) and the mix button's tooltip.
const TITLES: readonly string[] = [TEXT.regular, TEXT.chaosWeapon, TEXT.itemOption, TEXT.store, TEXT.resurrectSpirit, TEXT.refine,
  TEXT.restore, TEXT.refine, TEXT.chaosCard, TEXT.cherry, TEXT.extraction, TEXT.assembly, TEXT.application, TEXT.destruction];
const TOOLTIPS: Partial<Record<MixType, string>> = {
  [MixType.TRAINER]: TEXT.resurrection, [MixType.OSBOURNE]: TEXT.refine, [MixType.JERRIDON]: TEXT.restore, [MixType.ELPIS]: TEXT.refine,
  [MixType.EXTRACT_SEED]: TEXT.extraction, [MixType.SEED_SPHERE]: TEXT.assembly, [MixType.ATTACH_SOCKET]: TEXT.application,
  [MixType.DETACH_SOCKET]: TEXT.destruction,
};
const GOBLIN: MixType[] = [MixType.GOBLIN_NORMAL, MixType.GOBLIN_CHAOSITEM, MixType.GOBLIN_ADD380];
const SHOWS_TAX: MixType[] = [...GOBLIN, MixType.TRAINER];
const SHOWS_SUCCESS: MixType[] = [...GOBLIN, MixType.OSBOURNE, MixType.ELPIS, MixType.TRAINER, MixType.EXTRACT_SEED, MixType.SEED_SPHERE];
const SHOWS_ZEN: MixType[] = [...GOBLIN, MixType.CASTLE_SENIOR, MixType.TRAINER, MixType.JERRIDON, MixType.EXTRACT_SEED, MixType.SEED_SPHERE,
  MixType.ATTACH_SOCKET, MixType.DETACH_SOCKET];

/** The NPC windows (NpcWindowResponse) that open the mix window the same way (ReceiveTalk) and their mix types. */
const MIX_NPC_WINDOWS: Partial<Record<number, MixType>> = {
  [NpcWindow.CHAOS_MACHINE]: MixType.GOBLIN_NORMAL, 0x12: MixType.OSBOURNE, 0x13: MixType.JERRIDON, 0x15: MixType.CHAOS_CARD, 0x16: MixType.CHERRYBLOSSOM,
};

// A line of the window's lists (MixInventoryRmlModel::MixLine).
interface MixLine { text: string; kind: string; row: number; align_left: boolean; fit: number }

// mix_inventory.rml's data model (MixInventoryRmlModel).
interface MixModel extends ItemWindowModel {
  mix_visible: boolean; mix_locked: boolean; mix_tooltip: string;
  show_tax_rate: boolean; tax_rate_text: string; tax_rate_fit: number;
  show_recipe: boolean; recipe_line1: string; recipe_line2: string; show_recipe_line2: boolean; recipe_ready: boolean;
  show_success_rate: boolean; success_rate_text: string; success_boosted: boolean;
  show_required_zen: boolean; required_zen_text: string;
  show_prediction: boolean; prediction_text: string;
  source_lines: MixLine[]; status_lines: MixLine[]; advice_lines: MixLine[]; description_lines: MixLine[]; descriptions_lowered: boolean;
  show_socket_prompt: boolean; socket_prompt_text: string; show_socket_list: boolean; socket_lines: { text: string; index: number; selected: boolean }[];
  mix_inventory_mix_click: () => void; mix_inventory_select_socket: () => void;
}

// ConvertGold / ConvertChaosTaxGold.
const withTax = (zen: number, tax: number): number => zen + Math.floor((zen * tax) / 100);

// RenderText() shrinks a line to its box: the factor for a bold line of the window's text in a box of reference px.
let measure: CanvasRenderingContext2D | null = null;
function fitScale(text: string, box: number): number {
  measure ??= document.createElement('canvas').getContext('2d');
  if (!measure || !text) return 1;
  measure.font = 'bold 12px sans-serif';
  const width = measure.measureText(text).width;
  return width <= box ? 1 : Math.max(box / width, 0.75);
}

const line = (text: string, kind: string, row = 0, alignLeft = false, box = 0): MixLine =>
  ({ text, kind, row, align_left: alignLeft, fit: box ? fitScale(text, box) : 1 });

/** The mix window: the grid, the recipe text, the mix button. */
export class MixWindow extends ItemWindow<MixModel> {
  mixType: MixType = MixType.GOBLIN_NORMAL;
  state: MixState = MixState.READY;
  /** The chaos machine's tax rate (CastleSiegeTaxRateNotification, g_nChaosTaxRate). */
  taxRate = 0;
  /** Crywolf's bonus to the success rate (CrywolfBenefitPlusChaosRate). */
  plusChaosRate = 0;
  /** The recipes of each mix type (loaded with the window). */
  recipesByType: MixRecipes[] = [];
  /** The grid's items as the recipes see them (CheckMixInventory), and whether the grid is empty (IsMixInit). */
  mixItems: MixItem[] = [];
  mixInit = true;
  /** A bag item may go into the grid (AcceptsHeldItem); set by the machine. */
  accepts: (item: Item, fromStorage: number) => boolean = () => false;

  constructor({ inventory, picked, onClose, onMix }: { inventory: Inventory; picked: PickedItem; onClose: () => void; onMix: () => void }) {
    let self: MixWindow | null = null;
    const model: MixModel = {
      text_px: 12, title: TEXT.chaos, grid_cells: emptyCells(MIX.columns * MIX.rows), window_close: () => onClose(),
      mix_visible: true, mix_locked: false, mix_tooltip: TEXT.combining,
      show_tax_rate: false, tax_rate_text: '', tax_rate_fit: 1,
      show_recipe: true, recipe_line1: '', recipe_line2: '', show_recipe_line2: false, recipe_ready: false,
      show_success_rate: false, success_rate_text: '', success_boosted: false, show_required_zen: false, required_zen_text: '',
      show_prediction: false, prediction_text: '', source_lines: [], status_lines: [], advice_lines: [], description_lines: [],
      descriptions_lowered: false, show_socket_prompt: false, socket_prompt_text: '', show_socket_list: false, socket_lines: [],
      // The button isn't there once the mix finished (RenderFrame()'s early return); defense in depth as on the desktop.
      mix_inventory_mix_click: () => { if (self?.state !== MixState.FINISHED) onMix(); },
      mix_inventory_select_socket: () => undefined,
    };
    super({ inventory, picked, documentName: 'mix_inventory', grid: MIX, storage: Storage.CHAOS_MACHINE, model });
    self = this;
  }

  get recipes(): MixRecipes | null { return this.recipesByType[this.mixType] ?? null; }

  /** CheckMixInventory: what the grid's items make. */
  check(): void {
    const recipes = this.recipes;
    if (!recipes) return;
    this.mixItems = mixItemsOf([...this.items.entries()].sort(([a], [b]) => a - b).map(([, item]) => item));
    this.mixInit = this.mixItems.length === 0;
    recipes.checkRecipe(this.mixItems);
    recipes.checkSimilarity(this.mixItems);
  }

  isMixSource(item: Item): boolean {
    return this.recipes?.isMixSource(item, this.mixType, this.mixItems) ?? false;
  }

  // Items from the grid always move; a bag item only while the machine is ready and it's a source of a recipe.
  override click(event: MouseEvent): void {
    if (event.target instanceof Element && event.target.closest('#frame_corner_close, #btn_mix, #socket_list')) return;
    if (this.state === MixState.REQUESTED) return;
    const source = this.picked.source;
    if (source && !isMixStorage(source.storage) && !this.accepts(source.item, source.storage)) return;
    super.click(event);
  }

  // A right click sends a grid item back to the bag (ProcessMixItemAutoMoveToInventory), only while ready.
  override rightClick(event: MouseEvent): void {
    if (this.picked.source || this.inventory.pendingMove || this.state !== MixState.READY) return;
    const slot = this.itemAtGridSlot(this.gridSlotAt(this.pointOf(event)));
    const item = slot === null ? undefined : this.items.get(slot);
    if (slot === null || !item) return;
    const target = this.inventory.firstEmptySlot(this.inventory.items, BAG, item);
    if (target !== null) this.inventory.move(this.storageKind, slot, Storage.INVENTORY, target);
  }

  override updateModel(): void {
    const m = this.model;
    this.check();
    const recipes = this.recipes;
    const type = this.mixType;
    m.title = TITLES[type] ?? TEXT.chaos;
    m.mix_tooltip = TOOLTIPS[type] ?? TEXT.combining;
    m.mix_visible = this.state !== MixState.FINISHED;
    m.mix_locked = this.state === MixState.REQUESTED;
    m.show_tax_rate = SHOWS_TAX.includes(type);
    m.tax_rate_text = format(TEXT.taxRate, this.taxRate);
    m.tax_rate_fit = fitScale(m.tax_rate_text, 160);
    m.show_recipe = this.state !== MixState.FINISHED;
    const ready = recipes?.readyToMix ?? false;
    m.recipe_ready = ready;
    // GetCurRecipeName: the recipe, or why the items make none.
    const current = recipes?.current ?? null;
    if (current && current.mixOption === 'C') m.recipe_line1 = TEXT.add380;
    else if (current) m.recipe_line1 = recipeName(current);
    else if (type === MixType.TRAINER) m.recipe_line1 = format(TEXT.inappropriate, TEXT.resurrecting);
    else if (type === MixType.OSBOURNE || type === MixType.ELPIS) m.recipe_line1 = format(TEXT.inappropriate, TEXT.refining);
    else if (type === MixType.JERRIDON) m.recipe_line1 = format(TEXT.inappropriate, TEXT.restoring);
    else m.recipe_line1 = TEXT.improper;
    m.show_recipe_line2 = false;
    m.recipe_line2 = '';
    const rate = recipes?.successRate ?? 0;
    m.show_success_rate = SHOWS_SUCCESS.includes(type);
    m.success_boosted = ready && this.plusChaosRate > 0 && current?.mixOption === 'F';
    if (m.success_boosted) m.success_rate_text = `${format(TEXT.successRate, TEXT.combining, rate)} + ${this.plusChaosRate}%`;
    else if (type === MixType.TRAINER) m.success_rate_text = format(TEXT.successRate, TEXT.resurrecting, rate);
    else if (type === MixType.OSBOURNE || type === MixType.ELPIS) m.success_rate_text = format(TEXT.successRate, TEXT.refining, rate);
    else m.success_rate_text = format(TEXT.successRate, TEXT.combining, rate);
    const zen = recipes?.requiredZen ?? 0;
    m.show_required_zen = SHOWS_ZEN.includes(type);
    m.required_zen_text = format(ready && current?.zenType === 'C' ? TEXT.requiredZenPotion : TEXT.requiredZen,
      formatGold(withTax(zen, this.taxRate)), formatGold(zen));

    // The prediction, the most similar recipe's ingredients, or what's wrong.
    const sources: MixLine[] = [], status: MixLine[] = [], advice: MixLine[] = [];
    const similar = recipes?.mostSimilar ?? null;
    m.show_prediction = false;
    if (recipes && similar) {
      if (!ready) { m.show_prediction = true; m.prediction_text = format(TEXT.prediction, similar.mixOption === 'C' ? TEXT.add380 : recipeName(similar)); }
      for (let i = 0; i < 8; i++) {
        const source = recipes.sourceName(i, this.mixItems, this.mixInit);
        if (source.status === SourceStatus.ERROR) break;
        sources.push(line(source.text, source.status === SourceStatus.NO ? 'missing' : source.status === SourceStatus.PARTIALLY ? 'partial' : 'ready'));
      }
    } else if (this.mixInit) status.push(line(TEXT.upload, 'missing'));
    else { status.push(line(format(TEXT.prediction, ' '), 'missing')); status.push(line(TEXT.improper, 'missing')); }
    // The recipe's description when ready, else the advice of the most similar one.
    const texts = ready ? current?.descriptions : similar?.advice;
    for (const id of texts ?? []) if (id > 0) advice.push(line(recipeText(id), 'missing'));
    m.source_lines = sources;
    m.status_lines = status;
    m.advice_lines = advice;

    // RenderMixDescriptions(): the mix type's own instructions.
    const descriptions: MixLine[] = [];
    const describe = (text: string, kind: string, row: number, left = false) => descriptions.push(line(text, kind, row, left, left ? 180 : 160));
    m.descriptions_lowered = type === MixType.CASTLE_SENIOR;
    if (type === MixType.CASTLE_SENIOR) TEXT.castleSenior.forEach((text, i) => describe(text, 'dim', i));
    else if (type === MixType.OSBOURNE) {
      describe(TEXT.refineItem, 'normal', 0); describe(TEXT.refiningStone, 'normal', 1);
      describe(format(TEXT.forOnly, TEXT.refining, TEXT.weaponsOrShields), 'normal', 2); describe(TEXT.allowed, 'normal', 3); describe(TEXT.disappear, 'loss', 4);
    } else if (type === MixType.JERRIDON) TEXT.restoration.forEach((text, i) => describe(text, 'normal', i));
    else if (type === MixType.ELPIS) TEXT.elpis.forEach((text, i) => describe(text, 'normal', i));
    else if (type === MixType.CHAOS_CARD) {
      describe(TEXT.warning, 'warning', 4); describe(TEXT.onceAtATime, 'normal', 6, true); describe(TEXT.space2x4, 'normal', 7, true); describe(TEXT.specialItems, 'normal', 8, true);
    } else if (type === MixType.CHERRYBLOSSOM) {
      describe(TEXT.warning, 'warning', 0); describe(TEXT.golden255, 'normal', 2, true); describe(TEXT.sameCherry, 'normal', 3, true); describe(TEXT.space2x4, 'normal', 4, true);
    }
    m.description_lines = descriptions;
    m.show_socket_prompt = type === MixType.ATTACH_SOCKET || type === MixType.DETACH_SOCKET;
    m.socket_prompt_text = type === MixType.ATTACH_SOCKET ? TEXT.selectApplicable : TEXT.selectDestructible;
  }
}

/** A choice in the game (the desktop's menu message boxes): a title, buttons with their lines, Close. Doesn't block. */
export function chooseInGame(title: string, options: { label: string; lines: readonly string[] }[], closeLabel: string): Promise<number | null> {
  return new Promise((resolve) => {
    const box = document.createElement('div');
    box.className = 'chip choose-dialog mix-method-dialog';
    box.setAttribute('role', 'dialog');
    Object.assign(box.style, {
      position: 'fixed', left: '50%', top: '35%', transform: 'translate(-50%, -50%)', zIndex: '95', pointerEvents: 'auto',
      display: 'grid', gap: '8px', padding: '16px', minWidth: 'min(320px, calc(100vw - 32px))', textAlign: 'center',
    });
    const heading = document.createElement('div');
    heading.textContent = title;
    heading.style.fontWeight = 'bold';
    heading.style.color = '#ffd040';
    box.append(heading);
    const done = (answer: number | null) => { box.remove(); window.removeEventListener('keydown', onKey, true); resolve(answer); };
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); done(null); } };
    options.forEach((option, index) => {
      for (const text of option.lines) {
        const row = document.createElement('div');
        row.textContent = text;
        row.style.fontSize = '0.85em';
        box.append(row);
      }
      const button = document.createElement('button');
      button.type = 'button';
      button.className = `primary choose-option choose-option-${index}`;
      button.textContent = option.label;
      button.addEventListener('click', () => done(index));
      box.append(button);
    });
    const close = document.createElement('button');
    close.type = 'button';
    close.className = 'choose-close';
    close.textContent = closeLabel;
    close.addEventListener('click', () => done(null));
    box.append(close);
    box.addEventListener('pointerdown', (event) => event.stopPropagation());
    window.addEventListener('keydown', onKey, true);
    document.body.append(box);
  });
}

export interface ChaosMachineOptions {
  connection: Connection;
  inventory: Inventory;
  itemWindows: ItemWindows;
  /** A line for the chat (the desktop's system log). */
  message: (text: string) => void;
  /** A yes / no question (the mix confirmation, the refinery warning). */
  confirm: (text: string) => Promise<boolean>;
  /** The hero's level (the recipes' required level). */
  level: () => number;
  /** Plays one of the game's sounds by its id (SOUND_MIX01, …). */
  play?: (id: string) => void;
}

const u8 = (bytes: Uint8Array, at: number): number => bytes[at] ?? 0;

/** The mix NPCs' windows and their packets. */
export class ChaosMachine {
  readonly window: MixWindow;
  private readonly options: ChaosMachineOptions;

  constructor(options: ChaosMachineOptions) {
    this.options = options;
    const { inventory, itemWindows, connection } = options;
    this.window = new MixWindow({
      inventory, picked: itemWindows.picked, onClose: () => itemWindows.closeAll(), onMix: () => { void this.mix(); },
    });
    this.window.accepts = (item, fromStorage) => fromStorage === Storage.INVENTORY && this.window.state === MixState.READY && this.window.isMixSource(item);
    itemWindows.dock(this.window, () => this.close());
    // While the machine is open, a right click on a bag item puts it into the grid (ProcessMyInvenItemAutoMove).
    const previous = itemWindows.bag.interceptRightClick;
    itemWindows.bag.interceptRightClick = (slot, item) => {
      if (!this.window.isOpen) return previous?.(slot, item) ?? false;
      if (slot >= BAG.firstSlot && this.window.state === MixState.READY && this.window.isMixSource(item)) {
        const target = inventory.firstEmptySlot(inventory.mix, MIX, item);
        if (target !== null) inventory.move(Storage.INVENTORY, slot, this.window.storageKind, target);
      }
      return true;
    };
    inventory.onChange((what) => {
      if (what !== 'window') return;
      const type = inventory.openWindow === null ? undefined : MIX_NPC_WINDOWS[inventory.openWindow];
      if (type !== undefined && !this.window.isOpen) void this.talk(inventory.openWindow!, type);
    });
    connection.onPacket((bytes) => this.handle(bytes));
  }

  get isOpen(): boolean { return this.window.isOpen; }

  // ReceiveTalk: the refinery asks first; the Chaos Goblin's window opens with the method menu.
  private async talk(npcWindow: number, type: MixType): Promise<void> {
    if (npcWindow === 0x12 && !(await this.options.confirm(`${TEXT.warning}\n\n${TEXT.refinery}`))) return;
    await this.open(type);
  }

  /** Shows the mix window of a mix type next to the inventory (OpeningProcess). */
  async open(type: MixType): Promise<void> {
    const { itemWindows, connection } = this.options;
    const [recipes] = await Promise.all([loadMixRecipes(), loadTooltipData(), loadItemValueData(), prepareDocument('mix_inventory')]);
    this.window.recipesByType = recipes.map((list) => new MixRecipes(list));
    this.window.mixType = type;
    this.window.storageKind = MIX_STORAGE[type];
    this.window.plusChaosRate = 0;
    connection.send('ClientToServer', 'CrywolfChaosRateBenefitRequest', {});
    this.window.state = MixState.READY;
    await itemWindows.bag.open(itemWindows.parent);
    await this.window.open(itemWindows.parent);
    itemWindows.placeAll();
    if (type !== MixType.GOBLIN_NORMAL) return;
    // ShowChaosMixMenuDialog: regular, chaos weapon or item option combination; Close closes the machine.
    const choice = await chooseInGame(TEXT.selectMethod, [
      { label: TEXT.regular, lines: TEXT.regularLines }, { label: TEXT.chaosWeapon, lines: TEXT.chaosWeaponLines },
      { label: TEXT.itemOption, lines: [TEXT.add380] },
    ], TEXT.close);
    if (!this.window.isOpen) return;
    if (choice === null) { itemWindows.closeAll(); return; }
    this.window.mixType = GOBLIN[choice];
    itemWindows.refresh();
  }

  /** ClosingProcess: not while items are in the grid or on the cursor; tells the server the window closed. */
  close(): boolean {
    const { inventory, itemWindows, connection, message } = this.options;
    if (!this.window.isOpen) return true;
    if (inventory.mix.size > 0 || itemWindows.picked.source) { message(TEXT.closeFirst); return false; }
    if (this.window.mixType === MixType.TRAINER) connection.send('ClientToServer', 'CloseNpcRequest', {});
    else connection.send('ClientToServer', 'CraftingDialogCloseRequest', {});
    this.window.close();
    this.window.recipes?.clear();
    this.window.mixType = MixType.GOBLIN_NORMAL;
    inventory.openWindow = null;
    inventory.mix.clear();
    return true;
  }

  /** The mix button (CMixInventory::Mix): the checks, then the question, then ChaosMachineMixRequest. */
  async mix(): Promise<void> {
    const { inventory, itemWindows, message, level, play } = this.options;
    const window = this.window;
    window.check();
    play?.('SOUND_CLICK01');
    const recipes = window.recipes;
    if (!recipes) return;
    if (withTax(recipes.requiredZen, window.taxRate) > inventory.money) { message(TEXT.notEnoughZen); return; }
    const recipe = recipes.current;
    if (!recipe) { message(format(TEXT.lackOf, TEXT.combining)); return; }
    if (level() < recipe.requiredLevel) { message(format(TEXT.fromLevel, recipe.requiredLevel, recipeName(recipe))); return; }
    if (recipe.width !== -1) {
      const size = { width: recipe.width, height: recipe.height } as Item;
      if (inventory.firstEmptySlot(inventory.items, BAG, size) === null) { message(TEXT.organize); return; }
    }
    recipes.calcCharmBonus(window.mixItems);
    recipes.calcChaosCharmCount(window.mixItems);
    if (recipes.totalChaosCharmCount > 0 && recipes.totalCharmBonus > 0) { message(TEXT.talismans); return; }
    if (itemWindows.picked.source) return;
    const type = window.mixType, id = recipe.id;
    if (!(await this.options.confirm(`${recipeName(recipe)}\n${TEXT.wantToCombine}`))) return;
    // ConfirmMix: only when nothing changed while asking.
    if (!window.isOpen || window.state !== MixState.READY) return;
    window.check();
    if (window.mixType !== type || window.recipes?.currentMixId !== id) return;
    window.state = MixState.REQUESTED;
    this.options.connection.send('ClientToServer', 'ChaosMachineMixRequest', { MixType: id, SocketSlot: 0 });
    itemWindows.refresh();
  }

  // The finished combination's message (AddMixResultMessage).
  private resultMessage(succeeded: boolean): void {
    const type = this.window.mixType;
    let text: string | null = null;
    if (GOBLIN.includes(type) || type === MixType.EXTRACT_SEED || type === MixType.SEED_SPHERE) text = succeeded ? TEXT.succeeded : TEXT.failed;
    else if (type === MixType.OSBOURNE) text = format(succeeded ? TEXT.wasSuccessful : TEXT.hasFailed, TEXT.refining);
    else if (type === MixType.JERRIDON) text = format(succeeded ? TEXT.wasSuccessful : TEXT.hasFailed, TEXT.restoring);
    else if (type === MixType.ELPIS) text = format(succeeded ? TEXT.wasSuccessful : TEXT.hasFailed2112, TEXT.refining);
    else if (type === MixType.CHAOS_CARD) text = format(succeeded ? TEXT.wasSuccessful : TEXT.hasFailed2112, TEXT.chaosCardOperation);
    else if (type === MixType.CHERRYBLOSSOM) text = format(succeeded ? TEXT.wasSuccessful : TEXT.hasFailed2112, TEXT.cherryOperation);
    if (text) this.options.message(text);
  }

  private setState(state: MixState): void {
    this.window.state = state;
    this.options.itemWindows.refresh();
  }

  private handle(bytes: Uint8Array): void {
    const play = this.options.play;
    if (bytes[0] === 0xc1 && bytes[2] === 0x86) {
      // ItemCraftingResult (ReceiveMixExtended); the inventory put a created item into the grid.
      if (!this.window.isOpen) return;
      const result = u8(bytes, 3);
      if (result === 0) { this.setState(MixState.FINISHED); this.resultMessage(false); }
      else if (result === 1) { this.setState(MixState.FINISHED); this.resultMessage(true); play?.('SOUND_MIX01'); play?.('SOUND_JEWEL01'); }
      else if (result === 2 || result === 0x0b) { this.setState(MixState.READY); this.options.message(TEXT.notEnoughZen); }
      else if (result === 4) { this.options.message(TEXT.level10); this.setState(MixState.FINISHED); }
      else if (result === 9) { this.options.message(TEXT.level15); this.setState(MixState.FINISHED); }
      else this.setState(MixState.FINISHED);
    } else if (bytes[0] === 0xc2 && bytes[3] === 0x31 && (u8(bytes, 4) === 3 || u8(bytes, 4) === 5)) {
      // StoreItemList of a mix (ReceiveTradeInventoryExtended): what the mix left fills the grid again.
      if (u8(bytes, 4) === 5) this.options.message(TEXT.resurrectionFailed);
      this.setState(MixState.FINISHED);
      play?.('SOUND_MIX01');
      play?.('SOUND_BREAK01');
    } else if (bytes[0] === 0xc1 && bytes[2] === 0xbd && bytes[3] === 0x09) {
      this.window.plusChaosRate = u8(bytes, 4); // CrywolfBenefitPlusChaosRate
      this.options.itemWindows.refresh();
    } else if (bytes[0] === 0xc1 && bytes[2] === 0xb2 && bytes[3] === 0x1a && u8(bytes, 4) === 1) {
      this.window.taxRate = u8(bytes, 5); // CastleSiegeTaxRateNotification of the chaos machine (ReceiveTaxInfo)
      this.options.itemWindows.refresh();
    }
  }
}
