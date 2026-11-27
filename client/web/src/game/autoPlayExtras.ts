// HNMO Ultimate Auto Play (built on top of AutoPlay + HNMO extras):
// - Presets (Balanced, AFK Farm, Boss Hunt, Custom)
// - Back to town when bag is full or potions run out, sell junk, restock HP & Mana potions, and return
// - Auto-drink Mana & SD potions by threshold
// - Auto-deposit jewels to Jewel Bank before a full-bag town trip
// - Target priority (Nearest, Aggro First, Lowest HP) & rare-drop pickup priority
// - Multi-spot patrol when a spawn is empty
// - Emergency low-HP retreat & optional auto-return after death
// - Live session telemetry (runtime, kills, EXP/hr, Zen, town trips, jewel deposits, rare pickups)
// Settings are kept in this browser per character.

import { gameName } from '../gameName.js';
import type { TargetPriority, UltimateAutoPlayHooks } from './autoPlay.js';
import type { Character } from './characters.js';
import type { Item } from './inventory.js';
import { currentLanguage } from '../i18n.js';

const tx = (en: string, vi: string): string => (currentLanguage() === 'vi' ? vi : en);

export type UltimatePreset = 'balanced' | 'afkFarm' | 'bossHunt' | 'custom';

export interface PatrolSpot {
  map: number;
  x: number;
  y: number;
}

export type UltimateFeatureKey =
  | 'sparkleGuide'
  | 'extrasTown'
  | 'manaSdPotions'
  | 'jewelDeposit'
  | 'targetPriority'
  | 'patrol'
  | 'survival'
  | 'bossFocus'
  | 'presetsStats';

export interface UltimateFeatureAccess {
  mode: number; // 0 = Off, 1 = Free, 2 = VipOnly (Lv 1+), 3 = HigherVip (Lv 2+)
  allowed: boolean;
  vipRequired: boolean;
  requiredVipLevel?: number;
}

export interface UltimateAutoPlayPolicy {
  v: number;
  enabled: boolean;
  isVip: boolean;
  vipLevel?: number;
  features: Partial<Record<UltimateFeatureKey, UltimateFeatureAccess>>;
}

export function defaultPolicy(): UltimateAutoPlayPolicy {
  return {
    v: 2,
    enabled: true,
    isVip: false,
    vipLevel: 0,
    features: {
      sparkleGuide: { mode: 1, allowed: true, vipRequired: false, requiredVipLevel: 0 },
      extrasTown: { mode: 2, allowed: false, vipRequired: true, requiredVipLevel: 1 },
      manaSdPotions: { mode: 3, allowed: false, vipRequired: true, requiredVipLevel: 2 },
      jewelDeposit: { mode: 3, allowed: false, vipRequired: true, requiredVipLevel: 2 },
      targetPriority: { mode: 3, allowed: false, vipRequired: true, requiredVipLevel: 2 },
      patrol: { mode: 3, allowed: false, vipRequired: true, requiredVipLevel: 2 },
      survival: { mode: 3, allowed: false, vipRequired: true, requiredVipLevel: 2 },
      bossFocus: { mode: 3, allowed: false, vipRequired: true, requiredVipLevel: 2 },
      presetsStats: { mode: 3, allowed: false, vipRequired: true, requiredVipLevel: 2 },
    },
  };
}

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
  // Ultimate Autoplay settings
  preset: UltimatePreset;
  useManaPotion: boolean;
  manaThreshold: number;
  manaPotionsToBuy: number;
  useSdPotion: boolean;
  sdThreshold: number;
  autoDepositJewels: boolean;
  targetPriority: TargetPriority;
  prioritizeRareDrops: boolean;
  patrolEnabled: boolean;
  patrolIdleSeconds: number;
  patrolSpots: PatrolSpot[];
  retreatHpPercent: number;
  autoReturnAfterDeath: boolean;
  showUltimateHud: boolean;
  sparkleGuide: boolean;
  bossFocus: boolean;
}

export interface AutoPlaySessionStats {
  startedAt: number;
  elapsedMs: number;
  kills: number;
  expGained: number;
  zenGained: number;
  townTrips: number;
  jewelDeposits: number;
  potionsDrunk: number;
  rarePickups: number;
}

export function defaultExtrasSettings(): AutoPlayExtrasSettings {
  return {
    returnWhenBagFull: true,
    returnWhenOutOfPotions: true,
    potionsToBuy: 20,
    sellJunk: true,
    stopAfterMinutes: 0,
    alertOnDeath: true,
    alertOnBagFull: true,
    preset: 'balanced',
    useManaPotion: true,
    manaThreshold: 30,
    manaPotionsToBuy: 10,
    useSdPotion: false,
    sdThreshold: 40,
    autoDepositJewels: true,
    targetPriority: 'nearest',
    prioritizeRareDrops: true,
    patrolEnabled: false,
    patrolIdleSeconds: 15,
    patrolSpots: [],
    retreatHpPercent: 15,
    autoReturnAfterDeath: false,
    showUltimateHud: false,
    sparkleGuide: true,
    bossFocus: false,
  };
}

export const ULTIMATE_PRESETS: Record<Exclude<UltimatePreset, 'custom'>, Partial<AutoPlayExtrasSettings>> = {
  balanced: {
    preset: 'balanced',
    returnWhenBagFull: true,
    returnWhenOutOfPotions: true,
    potionsToBuy: 20,
    manaPotionsToBuy: 10,
    sellJunk: true,
    useManaPotion: true,
    manaThreshold: 30,
    useSdPotion: false,
    autoDepositJewels: true,
    targetPriority: 'nearest',
    prioritizeRareDrops: true,
    patrolEnabled: false,
    retreatHpPercent: 15,
    autoReturnAfterDeath: false,
  },
  afkFarm: {
    preset: 'afkFarm',
    returnWhenBagFull: true,
    returnWhenOutOfPotions: true,
    potionsToBuy: 40,
    manaPotionsToBuy: 20,
    sellJunk: true,
    useManaPotion: true,
    manaThreshold: 35,
    useSdPotion: true,
    sdThreshold: 40,
    autoDepositJewels: true,
    targetPriority: 'nearest',
    prioritizeRareDrops: true,
    patrolEnabled: true,
    patrolIdleSeconds: 15,
    retreatHpPercent: 20,
    autoReturnAfterDeath: true,
  },
  bossHunt: {
    preset: 'bossHunt',
    returnWhenBagFull: true,
    returnWhenOutOfPotions: true,
    potionsToBuy: 30,
    manaPotionsToBuy: 20,
    sellJunk: true,
    useManaPotion: true,
    manaThreshold: 40,
    useSdPotion: true,
    sdThreshold: 50,
    autoDepositJewels: true,
    targetPriority: 'aggroFirst',
    prioritizeRareDrops: true,
    patrolEnabled: false,
    retreatHpPercent: 15,
    autoReturnAfterDeath: false,
    sparkleGuide: true,
    bossFocus: true,
  },
};

const STORAGE_KEY = 'hnmo.autoPlayExtras.';

export function loadExtrasSettings(character: string): AutoPlayExtrasSettings {
  try {
    const text = localStorage.getItem(STORAGE_KEY + character);
    if (text) {
      const parsed = JSON.parse(text) as Partial<AutoPlayExtrasSettings>;
      return {
        ...defaultExtrasSettings(),
        ...parsed,
        patrolSpots: Array.isArray(parsed.patrolSpots) ? parsed.patrolSpots.slice(0, 8) : [],
      };
    }
  } catch { /* storage unavailable */ }
  return defaultExtrasSettings();
}

export function saveExtrasSettings(character: string, settings: AutoPlayExtrasSettings): void {
  try { localStorage.setItem(STORAGE_KEY + character, JSON.stringify(settings)); } catch { /* storage unavailable */ }
}

/** The potion seller the trip goes to: Potion Girl Amy in Lorencia. */
const TOWN = { warp: 'lorencia', map: 0, sellerNumber: 253, sellerCell: { x: 127, y: 88 } };
const TOWN_POTION = { group: 14, number: 3 }; // what's bought when the seller has it (else the best healing potion she sells)
const TOWN_MANA_POTION = { group: 14, number: 6 }; // Large Mana Potion fallback when available

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
  // Optional Ultimate Autoplay hooks (existing tests/callers don't have to provide these)
  life?: () => number;
  maxLife?: () => number;
  mana?: () => number;
  maxMana?: () => number;
  drinkManaPotion?: () => boolean;
  isManaPotion?: (item: Item) => boolean;
  manaPotions?: () => number;
  shield?: () => number;
  maxShield?: () => number;
  drinkSdPotion?: () => boolean;
  experience?: () => number;
  money?: () => number;
  hasJewels?: () => boolean;
  depositAllJewels?: () => boolean;
  monsters?: () => Iterable<Character>;
  huntingRange?: () => number;
  setOrigin?: (cell: { x: number; y: number }) => void;
  applyUltimateHooks?: (hooks: UltimateAutoPlayHooks) => void;
}

type TripStep = 'warpToTown' | 'walkToSeller' | 'talk' | 'sell' | 'buy' | 'buyMana' | 'close' | 'warpBack' | 'walkBack' | 'restart';

export class AutoPlayExtras {
  settings: AutoPlayExtrasSettings;
  policy: UltimateAutoPlayPolicy = defaultPolicy();
  readonly stats: AutoPlaySessionStats = {
    startedAt: 0, elapsedMs: 0, kills: 0, expGained: 0, zenGained: 0,
    townTrips: 0, jewelDeposits: 0, potionsDrunk: 0, rarePickups: 0,
  };
  private startedAt = 0;
  private wasActive = false;
  private trip: { step: TripStep; map: number; spot: { x: number; y: number }; since: number; bought: number; boughtMana: number; lastAt: number } | null = null;
  private bagFullAlerted = false;
  private wasDead = false;
  private pendingDeathReturn: { map: number; spot: { x: number; y: number }; respawnedAt: number } | null = null;
  private lastExp: number | null = null;
  private lastMoney: number | null = null;
  private lastManaPotionAt = 0;
  private lastSdPotionAt = 0;
  private lastJewelDepositAt = 0;
  private lastMonsterSeenAt = 0;
  private patrolIndex = 0;

  constructor(private readonly character: string, private readonly world: ExtrasWorld) {
    this.settings = loadExtrasSettings(character);
    this.syncUltimateHooks();
  }

  save(settings: AutoPlayExtrasSettings): void {
    this.settings = settings;
    saveExtrasSettings(this.character, settings);
    this.syncUltimateHooks();
  }

  applyPreset(preset: Exclude<UltimatePreset, 'custom'>): AutoPlayExtrasSettings {
    const next: AutoPlayExtrasSettings = { ...this.settings, ...ULTIMATE_PRESETS[preset], preset };
    this.save(next);
    return next;
  }

  resetStats(now = performance.now()): void {
    this.stats.startedAt = this.world.active() ? now : 0;
    this.stats.elapsedMs = 0;
    this.stats.kills = 0;
    this.stats.expGained = 0;
    this.stats.zenGained = 0;
    this.stats.townTrips = 0;
    this.stats.jewelDeposits = 0;
    this.stats.potionsDrunk = 0;
    this.stats.rarePickups = 0;
    this.lastExp = this.world.experience?.() ?? null;
    this.lastMoney = this.world.money?.() ?? null;
  }

  addPatrolSpot(): PatrolSpot | null {
    const map = this.world.mapNumber();
    if (map === null) return null;
    const cell = this.world.hero().cell;
    const spot: PatrolSpot = { map, x: cell.x, y: cell.y };
    const nextSpots = [...this.settings.patrolSpots.filter((s) => !(s.map === spot.map && Math.hypot(s.x - spot.x, s.y - spot.y) <= 2)), spot].slice(-8);
    this.save({ ...this.settings, patrolSpots: nextSpots });
    return spot;
  }

  clearPatrolSpots(): void {
    this.save({ ...this.settings, patrolSpots: [] });
  }

  setPolicy(json: string): void {
    try {
      const parsed = JSON.parse(json) as Partial<UltimateAutoPlayPolicy>;
      if (typeof parsed.enabled === 'boolean') {
        const isVip = Boolean(parsed.isVip);
        const vipLevel = typeof parsed.vipLevel === 'number' ? parsed.vipLevel : (isVip ? 2 : 0);
        const rawFeatures = parsed.features ?? {};
        const extrasFallback: UltimateFeatureAccess = {
          mode: 2,
          allowed: isVip || vipLevel >= 1,
          vipRequired: parsed.enabled && !(isVip || vipLevel >= 1),
          requiredVipLevel: 1,
        };
        this.policy = {
          v: parsed.v ?? 2,
          enabled: parsed.enabled,
          isVip: isVip || vipLevel > 0,
          vipLevel,
          features: { extrasTown: extrasFallback, ...rawFeatures },
        };
        this.syncUltimateHooks();
      }
    } catch { /* ignore malformed policy */ }
  }

  isFeatureAllowed(key: UltimateFeatureKey): boolean {
    if (!this.policy.enabled) return false;
    const access = this.policy.features[key];
    if (!access) return false;
    return access.allowed;
  }

  isFeatureVipLocked(key: UltimateFeatureKey): boolean {
    if (!this.policy.enabled) return false;
    const access = this.policy.features[key];
    return Boolean(access?.vipRequired);
  }

  featureRequiredVipLevel(key: UltimateFeatureKey): number {
    const access = this.policy.features[key];
    if (!access) return 0;
    if (typeof access.requiredVipLevel === 'number') return access.requiredVipLevel;
    return access.mode >= 3 ? 2 : access.mode === 2 ? 1 : 0;
  }

  get onTrip(): boolean { return this.trip !== null; }
  get tripStepLabel(): string { return this.trip?.step ?? (this.pendingDeathReturn ? 'respawnWait' : 'idle'); }

  private syncUltimateHooks(): void {
    const allowTarget = this.isFeatureAllowed('targetPriority');
    this.world.applyUltimateHooks?.({
      targetPriority: allowTarget ? this.settings.targetPriority : 'nearest',
      prioritizeRareDrops: allowTarget && this.settings.prioritizeRareDrops,
      onKill: () => { this.stats.kills++; },
      onRareDropPicked: () => { this.stats.rarePickups++; },
    });
  }

  /** Starts a trip to town now (from the hunting spot), as when the bag is full. */
  goToTown(now = performance.now()): void {
    if (!this.isFeatureAllowed('extrasTown')) {
      this.world.message(tx('Auto play: VIP required for town trip automation.', 'Tự động: cần VIP để tự động về thành.'));
      return;
    }
    const map = this.world.mapNumber();
    if (this.trip || map === null) return;
    this.world.stop();
    this.stats.townTrips++;
    this.trip = { step: 'warpToTown', map, spot: { ...this.world.hero().cell }, since: now, bought: 0, boughtMana: 0, lastAt: 0 };
  }

  update(now = performance.now()): void {
    const active = this.world.active();
    const hero = this.world.hero();
    if (active && !this.wasActive) {
      this.startedAt = now;
      this.lastMonsterSeenAt = now;
      if (this.stats.startedAt === 0) this.stats.startedAt = now;
      this.lastExp = this.world.experience?.() ?? null;
      this.lastMoney = this.world.money?.() ?? null;
      this.bagFullAlerted = false;
    }
    this.wasActive = active;

    // Alert on death & optionally queue auto-return once respawned in town.
    if (hero.dead && !this.wasDead && (active || this.trip)) {
      if (this.settings.alertOnDeath) this.alert('Your character died.');
      if (this.settings.autoReturnAfterDeath && this.isFeatureAllowed('survival') && !this.trip) {
        const map = this.world.mapNumber();
        if (map !== null) {
          this.pendingDeathReturn = { map, spot: { ...hero.cell }, respawnedAt: 0 };
          this.world.stop();
        }
      }
    }
    this.wasDead = hero.dead;

    if (this.pendingDeathReturn) {
      if (!hero.dead) {
        if (this.pendingDeathReturn.respawnedAt === 0) this.pendingDeathReturn.respawnedAt = now;
        else if (now - this.pendingDeathReturn.respawnedAt >= 5000) {
          const ret = this.pendingDeathReturn;
          this.pendingDeathReturn = null;
          this.world.message(tx('Auto play: respawned, returning to the hunting spot.', 'Tự động: đã hồi sinh, đang quay lại bãi săn.'));
          this.trip = { step: 'warpBack', map: ret.map, spot: ret.spot, since: now, bought: 0, boughtMana: 0, lastAt: 0 };
        }
      }
      return;
    }

    if (this.trip) { this.stepTrip(now); return; }
    if (!active) return;

    // Update live session telemetry while active.
    this.stats.elapsedMs += Math.max(0, now - (this.stats.startedAt || now));
    this.stats.startedAt = now;
    this.trackSessionGains();

    // Stop timer.
    if (this.settings.stopAfterMinutes > 0 && now - this.startedAt >= this.settings.stopAfterMinutes * 60_000) {
      this.world.stop();
      this.world.message(`Auto play stopped after ${this.settings.stopAfterMinutes} minutes.`);
      return;
    }

    // Ultimate potions (Mana & SD) while fighting.
    if (this.isFeatureAllowed('manaSdPotions')) this.stepUltimatePotions(now);

    // Emergency low-HP retreat when out of healing potions.
    if (this.isFeatureAllowed('survival') && this.settings.retreatHpPercent > 0 && this.world.healingPotions() === 0 && this.world.life && this.world.maxLife) {
      const max = this.world.maxLife();
      if (max > 0 && Math.ceil((this.world.life() * 100) / max) <= this.settings.retreatHpPercent) {
        this.alert(`Emergency retreat at <= ${this.settings.retreatHpPercent}% HP (out of healing potions).`);
        this.goToTown(now);
        return;
      }
    }

    // Pre-trip Jewel Bank auto-deposit: free bag slots before deciding bag is full.
    let full = this.world.bagFull();
    if (full && this.isFeatureAllowed('jewelDeposit') && this.settings.autoDepositJewels && this.world.hasJewels?.() && now - this.lastJewelDepositAt >= 10_000) {
      this.lastJewelDepositAt = now;
      if (this.world.depositAllJewels?.()) {
        this.stats.jewelDeposits++;
        this.world.message(tx('Auto play: jewels deposited in the Jewel Bank.', 'Tự động: đã gửi ngọc vào Ngân hàng ngọc.'));
        full = this.world.bagFull();
      }
    }

    if (full && !this.bagFullAlerted && this.settings.alertOnBagFull) { this.bagFullAlerted = true; this.alert('Your bag is full.'); }
    if (!full) this.bagFullAlerted = false;
    const outOfPotions = this.world.healingPotions() === 0;
    if (this.isFeatureAllowed('extrasTown') && ((full && this.settings.returnWhenBagFull) || (outOfPotions && this.settings.returnWhenOutOfPotions))) {
      const map = this.world.mapNumber();
      if (map === null) return;
      this.world.message(full ? 'Bag full: going to town to sell and buy potions.' : 'Out of potions: going to town to buy more.');
      this.world.stop();
      this.stats.townTrips++;
      this.trip = { step: 'warpToTown', map, spot: { ...hero.cell }, since: now, bought: 0, boughtMana: 0, lastAt: 0 };
      return;
    }

    // Multi-spot patrol when current spawn has no monsters in hunting range.
    this.stepPatrol(now, hero);
  }

  private trackSessionGains(): void {
    if (this.world.experience) {
      const exp = this.world.experience();
      if (this.lastExp !== null && exp > this.lastExp) this.stats.expGained += exp - this.lastExp;
      this.lastExp = exp;
    }
    if (this.world.money) {
      const money = this.world.money();
      if (this.lastMoney !== null && money > this.lastMoney) this.stats.zenGained += money - this.lastMoney;
      this.lastMoney = money;
    }
  }

  private stepUltimatePotions(now: number): void {
    if (this.settings.useManaPotion && this.world.mana && this.world.maxMana && this.world.drinkManaPotion && now - this.lastManaPotionAt >= 600) {
      const max = this.world.maxMana();
      if (max > 0 && Math.ceil((this.world.mana() * 100) / max) <= this.settings.manaThreshold) {
        if (this.world.drinkManaPotion()) { this.lastManaPotionAt = now; this.stats.potionsDrunk++; }
      }
    }
    if (this.settings.useSdPotion && this.world.shield && this.world.maxShield && this.world.drinkSdPotion && now - this.lastSdPotionAt >= 600) {
      const max = this.world.maxShield();
      if (max > 0 && Math.ceil((this.world.shield() * 100) / max) <= this.settings.sdThreshold) {
        if (this.world.drinkSdPotion()) { this.lastSdPotionAt = now; this.stats.potionsDrunk++; }
      }
    }
  }

  private stepPatrol(now: number, hero: Character): void {
    if (!this.isFeatureAllowed('patrol') || !this.settings.patrolEnabled || !this.world.monsters) return;
    const map = this.world.mapNumber();
    if (map === null) return;
    const spots = this.settings.patrolSpots.filter((s) => s.map === map);
    if (spots.length < 2) return;
    const range = Math.max(4, (this.world.huntingRange?.() ?? 6) + 2);
    const hasMonster = [...this.world.monsters()].some((m) => !m.dead && m.kind === 'monster' && Math.hypot(m.cell.x - hero.cell.x, m.cell.y - hero.cell.y) <= range);
    if (hasMonster) { this.lastMonsterSeenAt = now; return; }
    if (hero.isWalking || now - this.lastMonsterSeenAt < this.settings.patrolIdleSeconds * 1000) return;
    this.patrolIndex = (this.patrolIndex + 1) % spots.length;
    const next = spots[this.patrolIndex];
    this.lastMonsterSeenAt = now;
    this.world.setOrigin?.({ x: next.x, y: next.y });
    this.world.walkTo({ x: next.x, y: next.y });
    this.world.message(tx(`Auto play: patrolling to spot (${next.x}, ${next.y}).`, `Tự động: chuyển sang bãi (${next.x}, ${next.y}).`));
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
        const junk = this.world.bagItems().find(([, item]) =>
          !this.world.isKeeper(item) && !this.world.isHealingPotion(item) && !(this.world.isManaPotion?.(item) ?? false));
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
          this.next(this.settings.manaPotionsToBuy > 0 && this.world.isManaPotion ? 'buyMana' : 'close', now);
          return;
        }
        if (this.due(now, 400)) { this.world.buy(slot); trip.bought++; }
        return;
      }
      case 'buyMana': {
        const items = this.world.shopItems();
        let slot: number | null = null;
        for (const [s, item] of items) if (item.group === TOWN_MANA_POTION.group && item.number === TOWN_MANA_POTION.number) slot = s;
        if (slot === null && this.world.isManaPotion) {
          for (const [s, item] of items) if (this.world.isManaPotion(item) && (slot === null || item.number > items.get(slot)!.number)) slot = s;
        }
        const manaCount = this.world.manaPotions?.() ?? 0;
        if (slot === null || this.world.bagFull() || manaCount >= this.settings.manaPotionsToBuy || trip.boughtMana >= this.settings.manaPotionsToBuy * 2) {
          this.next('close', now);
          return;
        }
        if (this.due(now, 400)) { this.world.buy(slot); trip.boughtMana++; }
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
        if (Notification.permission === 'granted') new Notification(gameName(), { body: text });
        else if (Notification.permission === 'default') void Notification.requestPermission();
      }
    } catch { /* notifications unavailable */ }
    if (typeof document !== 'undefined' && document.hidden) {
      const title = document.title;
      document.title = `(!) ${text}`;
      const restore = () => { document.title = title; document.removeEventListener('visibilitychange', restore); };
      document.addEventListener('visibilitychange', restore);
    }
  }
}
