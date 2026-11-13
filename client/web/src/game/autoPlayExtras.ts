// HNMO extras for auto play (owner-approved, not in the desktop helper):
// - back to town when the bag is full or the healing potions run out: warp to Lorencia, walk to the
//   potion seller, sell the junk the helper picked up, buy healing potions, warp back to the hunting
//   map, walk to the hunting spot and start auto play again;
// - sell junk at the shop (anything that isn't a jewel, excellent, ancient, valuable, a potion or a
//   high-level item);
// - a stop timer;
// - alerts (a browser notification and a chat line) when the hero dies or the bag is full.
// The settings are kept in this browser per character.

import type { Character } from './characters.js';
import type { Item } from './inventory.js';

export interface AutoPlayExtrasSettings {
  returnWhenBagFull: boolean;
  returnWhenOutOfPotions: boolean;
  /** How many healing potions to buy on a trip to town. */
  potionsToBuy: number;
  sellJunk: boolean;
  /** Stop auto play after this many minutes; 0 for never. */
  stopAfterMinutes: number;
  alertOnDeath: boolean;
  alertOnBagFull: boolean;
}

export function defaultExtrasSettings(): AutoPlayExtrasSettings {
  return { returnWhenBagFull: true, returnWhenOutOfPotions: true, potionsToBuy: 20, sellJunk: true, stopAfterMinutes: 0, alertOnDeath: true, alertOnBagFull: true };
}

const STORAGE_KEY = 'hnmo.autoPlayExtras.';

export function loadExtrasSettings(character: string): AutoPlayExtrasSettings {
  try {
    const text = localStorage.getItem(STORAGE_KEY + character);
    if (text) return { ...defaultExtrasSettings(), ...(JSON.parse(text) as Partial<AutoPlayExtrasSettings>) };
  } catch { /* storage unavailable */ }
  return defaultExtrasSettings();
}

export function saveExtrasSettings(character: string, settings: AutoPlayExtrasSettings): void {
  try { localStorage.setItem(STORAGE_KEY + character, JSON.stringify(settings)); } catch { /* storage unavailable */ }
}

/** The potion seller the trip goes to: Potion Girl Amy in Lorencia. */
const TOWN = { warp: 'lorencia', map: 0, sellerNumber: 253, sellerCell: { x: 127, y: 88 } };
const TOWN_POTION = { group: 14, number: 3 }; // what's bought when the seller has it (else the best healing potion she sells)

export interface ExtrasWorld {
  active: () => boolean;
  start: () => void;
  stop: () => void;
  hero: () => Character;
  mapNumber: () => number | null;
  mapWarpName: (map: number) => string;
  say: (text: string) => void;
  walkTo: (cell: { x: number; y: number }) => boolean;
  npcs: () => Iterable<Character>;
  talkTo: (npc: Character) => void;
  shopOpen: () => boolean;
  shopItems: () => Map<number, Item>;
  buy: (shopSlot: number) => void;
  sell: (slot: number) => boolean;
  selling: () => boolean;
  closeShop: () => void;
  bagItems: () => [number, Item][];
  bagFull: () => boolean;
  healingPotions: () => number;
  isHealingPotion: (item: Item) => boolean;
  isKeeper: (item: Item) => boolean;
  message: (text: string) => void;
}

type TripStep = 'warpToTown' | 'walkToSeller' | 'talk' | 'sell' | 'buy' | 'close' | 'warpBack' | 'walkBack' | 'restart';

export class AutoPlayExtras {
  settings: AutoPlayExtrasSettings;
  private startedAt = 0;
  private wasActive = false;
  private trip: { step: TripStep; map: number; spot: { x: number; y: number }; since: number; bought: number; lastAt: number } | null = null;
  private bagFullAlerted = false;
  private wasDead = false;

  constructor(private readonly character: string, private readonly world: ExtrasWorld) {
    this.settings = loadExtrasSettings(character);
  }

  save(settings: AutoPlayExtrasSettings): void {
    this.settings = settings;
    saveExtrasSettings(this.character, settings);
  }

  get onTrip(): boolean { return this.trip !== null; }

  /** Starts a trip to town now (from the hunting spot), as when the bag is full. */
  goToTown(now = performance.now()): void {
    const map = this.world.mapNumber();
    if (this.trip || map === null) return;
    this.world.stop();
    this.trip = { step: 'warpToTown', map, spot: { ...this.world.hero().cell }, since: now, bought: 0, lastAt: 0 };
  }

  update(now = performance.now()): void {
    const active = this.world.active();
    const hero = this.world.hero();
    if (active && !this.wasActive) { this.startedAt = now; this.bagFullAlerted = false; }
    this.wasActive = active;
    // Alert on death (while auto play runs or a trip is under way).
    if (hero.dead && !this.wasDead && (active || this.trip) && this.settings.alertOnDeath) this.alert('Your character died.');
    this.wasDead = hero.dead;
    if (this.trip) { this.stepTrip(now); return; }
    if (!active) return;
    // Stop timer.
    if (this.settings.stopAfterMinutes > 0 && now - this.startedAt >= this.settings.stopAfterMinutes * 60_000) {
      this.world.stop();
      this.world.message(`Auto play stopped after ${this.settings.stopAfterMinutes} minutes.`);
      return;
    }
    const full = this.world.bagFull();
    if (full && !this.bagFullAlerted && this.settings.alertOnBagFull) { this.bagFullAlerted = true; this.alert('Your bag is full.'); }
    if (!full) this.bagFullAlerted = false;
    const outOfPotions = this.world.healingPotions() === 0;
    if ((full && this.settings.returnWhenBagFull) || (outOfPotions && this.settings.returnWhenOutOfPotions)) {
      const map = this.world.mapNumber();
      if (map === null) return;
      this.world.message(full ? 'Bag full: going to town to sell and buy potions.' : 'Out of potions: going to town to buy more.');
      this.world.stop();
      this.trip = { step: 'warpToTown', map, spot: { ...hero.cell }, since: now, bought: 0, lastAt: 0 };
    }
  }

  private next(step: TripStep, now: number): void {
    if (!this.trip) return;
    this.trip.step = step;
    this.trip.since = now;
    this.trip.lastAt = 0;
  }

  /** Whether the step's action is due again (at most every `ms`), marking it done. */
  private due(now: number, ms: number): boolean {
    const trip = this.trip!;
    if (now - trip.lastAt < ms) return false;
    trip.lastAt = now;
    return true;
  }

  // The trip, a step at a time; a step that takes too long gives up (and says so).
  private stepTrip(now: number): void {
    const trip = this.trip!;
    const hero = this.world.hero();
    if (hero.dead) return;
    if (now - trip.since > 90_000) { this.world.message('Auto play: the trip to town took too long; stopped.'); this.trip = null; return; }
    switch (trip.step) {
      case 'warpToTown':
        if (this.world.mapNumber() !== TOWN.map) { if (this.due(now, 15_000)) this.world.say(`/move ${TOWN.warp}`); return; }
        this.next('walkToSeller', now);
        return;
      case 'walkToSeller':
        if (!hero.isWalking) {
          const d = Math.hypot(hero.cell.x - TOWN.sellerCell.x, hero.cell.y - TOWN.sellerCell.y);
          if (d <= 3) { this.next('talk', now); return; }
          if (this.due(now, 2000)) this.world.walkTo(TOWN.sellerCell);
        }
        return;
      case 'talk': {
        if (this.world.shopOpen()) { this.next(this.settings.sellJunk ? 'sell' : 'buy', now); return; }
        const seller = [...this.world.npcs()].find((npc) => npc.monsterNumber === TOWN.sellerNumber);
        if (seller && this.due(now, 3000)) this.world.talkTo(seller);
        return;
      }
      case 'sell': {
        if (this.world.selling()) return;
        const junk = this.world.bagItems().find(([, item]) => !this.world.isKeeper(item) && !this.world.isHealingPotion(item));
        if (junk) { if (this.due(now, 500)) this.world.sell(junk[0]); return; }
        this.next('buy', now);
        return;
      }
      case 'buy': {
        const items = this.world.shopItems();
        let slot: number | null = null;
        for (const [s, item] of items) if (item.group === TOWN_POTION.group && item.number === TOWN_POTION.number) slot = s;
        if (slot === null) for (const [s, item] of items) if (this.world.isHealingPotion(item) && (slot === null || item.number > items.get(slot)!.number)) slot = s;
        if (slot === null || this.world.bagFull() || this.world.healingPotions() >= this.settings.potionsToBuy || trip.bought >= this.settings.potionsToBuy * 2) {
          this.next('close', now);
          return;
        }
        if (this.due(now, 400)) { this.world.buy(slot); trip.bought++; }
        return;
      }
      case 'close':
        this.world.closeShop();
        this.next('warpBack', now);
        return;
      case 'warpBack':
        if (this.world.mapNumber() !== trip.map) { if (this.due(now, 15_000)) this.world.say(`/move ${this.world.mapWarpName(trip.map)}`); return; }
        this.next('walkBack', now);
        return;
      case 'walkBack':
        if (!hero.isWalking) {
          if (Math.hypot(hero.cell.x - trip.spot.x, hero.cell.y - trip.spot.y) <= 1) { this.next('restart', now); return; }
          if (this.due(now, 2000) && !this.world.walkTo(trip.spot)) this.next('restart', now);
        }
        return;
      case 'restart':
        this.trip = null;
        this.world.message('Back at the hunting spot: auto play continues.');
        this.world.start();
        return;
    }
  }

  // A chat line, a browser notification when allowed, and the page title while it isn't seen.
  private alert(text: string): void {
    this.world.message(`Auto play: ${text}`);
    try {
      if ('Notification' in window) {
        if (Notification.permission === 'granted') new Notification('HNMO', { body: text });
        else if (Notification.permission === 'default') void Notification.requestPermission();
      }
    } catch { /* notifications unavailable */ }
    if (document.hidden) {
      const title = document.title;
      document.title = `(!) ${text}`;
      const restore = () => { document.title = title; document.removeEventListener('visibilitychange', restore); };
      document.addEventListener('visibilitychange', restore);
    }
  }
}
