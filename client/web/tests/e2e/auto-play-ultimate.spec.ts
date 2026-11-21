// Ultimate Autoplay unit-style e2e tests (offline, no server required):
// - Admin Free vs VIP policy gating
// - Auto Mana / SD potions (VIP)
// - Auto Jewel Bank deposit before town trip (VIP)
// - Target priority (aggroFirst / lowestHp) & rare-drop priority
// - Anti-stuck watchdog
import { test, expect } from '@playwright/test';
import { AutoPlay, type AutoPlayDrop, type AutoPlayWorld } from '../../src/game/autoPlay.js';
import { defaultAutoPlayConfig } from '../../src/game/autoPlayConfig.js';
import { AutoPlayExtras, type ExtrasWorld } from '../../src/game/autoPlayExtras.js';
import type { Character } from '../../src/game/characters.js';
import type { Item } from '../../src/game/inventory.js';
import type { Connection } from '../../src/net/connection.js';

const makeChar = (id: number, kind: string, x: number, y: number, healthStatus = -1): Character =>
  ({ id, name: `c${id}`, kind, dead: false, cell: { x, y }, path: [], isWalking: false, healthStatus }) as unknown as Character;

const makeItem = (group: number, number: number, excellent = 0, ancientSet = 0): Item =>
  ({ group, number, excellent, ancientSet, level: 0, durability: 1 }) as unknown as Item;

function makeAutoPlayHarness() {
  const hero = makeChar(1, 'player', 100, 100);
  const monsters: Character[] = [];
  const drops: AutoPlayDrop[] = [];
  const attacks: Character[] = [];
  const pickups: number[] = [];
  const messages: string[] = [];
  const connection = { onPacket: () => () => undefined, send: () => undefined } as unknown as Connection;

  const world: AutoPlayWorld = {
    hero: () => hero,
    inSafeZone: () => false,
    monsters: () => monsters,
    cast: () => false,
    attack: (t) => { attacks.push(t); },
    busy: () => false,
    skillUsable: () => false,
    knowsSkill: () => false,
    castSupport: () => false,
    actionBusy: () => false,
    hasBuff: () => false,
    party: () => [],
    life: () => 100,
    maxLife: () => 100,
    money: () => 1000,
    drinkHealingPotion: () => false,
    drops: () => drops,
    pickUp: (id) => { pickups.push(id); },
    pickingUp: () => false,
    walkTo: () => true,
    worn: () => [],
    repair: () => undefined,
    itemName: () => 'Item',
    isJewel: (item) => item.group === 14 && item.number === 13,
    message: (t) => { messages.push(t); },
  };

  const autoPlay = new AutoPlay(connection, world);
  autoPlay.setConfig({
    ...defaultAutoPlayConfig(),
    huntingRange: 6,
    obtainingRange: 8,
    pickSelectItems: true,
    pickZen: true,
    pickJewel: true,
    fallbackBasicAttack: true,
  });
  return { hero, monsters, drops, attacks, pickups, messages, autoPlay };
}

function makeExtrasHarness(policyJson?: string) {
  const hero = makeChar(1, 'player', 100, 100);
  let active = true;
  let full = false;
  let hasJewels = false;
  let mana = 100;
  let shield = 100;
  let manaDrinks = 0;
  let sdDrinks = 0;
  let jewelDeposits = 0;
  const messages: string[] = [];

  const world: ExtrasWorld = {
    active: () => active,
    start: () => { active = true; },
    stop: () => { active = false; },
    hero: () => hero,
    mapNumber: () => 0,
    mapWarpName: () => 'lorencia',
    say: () => undefined,
    walkTo: () => true,
    npcs: () => [],
    talkTo: () => undefined,
    shopOpen: () => false,
    shopItems: () => new Map(),
    buy: () => undefined,
    sell: () => false,
    selling: () => false,
    closeShop: () => undefined,
    bagItems: () => [],
    bagFull: () => full,
    healingPotions: () => 5,
    isHealingPotion: () => false,
    isKeeper: () => false,
    message: (t) => { messages.push(t); },
    life: () => 100,
    maxLife: () => 100,
    mana: () => mana,
    maxMana: () => 100,
    drinkManaPotion: () => { manaDrinks++; mana = 100; return true; },
    shield: () => shield,
    maxShield: () => 100,
    drinkSdPotion: () => { sdDrinks++; shield = 100; return true; },
    hasJewels: () => hasJewels,
    depositAllJewels: () => { jewelDeposits++; hasJewels = false; full = false; return true; },
  };

  const extras = new AutoPlayExtras('test_char', world);
  if (policyJson) extras.setPolicy(policyJson);
  return {
    extras,
    setState: (s: { full?: boolean; hasJewels?: boolean; mana?: number; shield?: number }) => {
      if (s.full !== undefined) full = s.full;
      if (s.hasJewels !== undefined) hasJewels = s.hasJewels;
      if (s.mana !== undefined) mana = s.mana;
      if (s.shield !== undefined) shield = s.shield;
    },
    counts: () => ({ manaDrinks, sdDrinks, jewelDeposits, active }),
    messages,
  };
}

test('Free vs VIP policy gates Mana/SD potions and Jewel Bank deposit cleanly', () => {
  const freePolicy = JSON.stringify({
    v: 1, enabled: true, isVip: false,
    features: {
      sparkleGuide: { mode: 1, allowed: true, vipRequired: false },
      presetsStats: { mode: 1, allowed: true, vipRequired: false },
      manaSdPotions: { mode: 2, allowed: false, vipRequired: true },
      jewelDeposit: { mode: 2, allowed: false, vipRequired: true },
    },
  });
  const free = makeExtrasHarness(freePolicy);
  expect(free.extras.isFeatureAllowed('sparkleGuide')).toBe(true);
  expect(free.extras.isFeatureVipLocked('manaSdPotions')).toBe(true);
  free.setState({ mana: 10, shield: 10, full: true, hasJewels: true });
  free.extras.update(10_000);
  expect(free.counts().manaDrinks).toBe(0);
  expect(free.counts().jewelDeposits).toBe(0);
  expect(free.extras.onTrip).toBe(true);

  const vipPolicy = JSON.stringify({
    v: 1, enabled: true, isVip: true,
    features: {
      sparkleGuide: { mode: 1, allowed: true, vipRequired: false },
      presetsStats: { mode: 1, allowed: true, vipRequired: false },
      manaSdPotions: { mode: 2, allowed: true, vipRequired: false },
      jewelDeposit: { mode: 2, allowed: true, vipRequired: false },
    },
  });
  const vip = makeExtrasHarness(vipPolicy);
  vip.extras.applyPreset('afkFarm');
  vip.setState({ mana: 10, shield: 10, full: true, hasJewels: true });
  vip.extras.update(15_000);
  expect(vip.counts().manaDrinks).toBe(1);
  expect(vip.counts().sdDrinks).toBe(1);
  expect(vip.counts().jewelDeposits).toBe(1);
  // Bag was freed by jewel deposit, so no town trip needed!
  expect(vip.extras.onTrip).toBe(false);
});

test('Target priority (aggroFirst / lowestHp) and rare-drop priority work', () => {
  const h = makeAutoPlayHarness();
  const nearMob = makeChar(10, 'monster', 101, 100, 0.9);
  const farAggroMob = makeChar(11, 'monster', 104, 100, 0.8);
  const weakMob = makeChar(12, 'monster', 103, 100, 0.2);
  h.monsters.push(nearMob, farAggroMob, weakMob);

  h.autoPlay.start();
  h.autoPlay.setUltimateHooks({ targetPriority: 'aggroFirst', prioritizeRareDrops: true });
  h.autoPlay.attackedBy(farAggroMob);
  h.autoPlay.update(100);
  expect(h.attacks[0]?.id).toBe(11);

  // Switch to lowestHp with fresh start
  h.attacks.length = 0;
  h.autoPlay.stop();
  h.autoPlay.start();
  h.autoPlay.setUltimateHooks({ targetPriority: 'lowestHp', prioritizeRareDrops: true });
  h.autoPlay.update(200);
  expect(h.attacks[0]?.id).toBe(12);

  // Rare drop (Jewel of Bless at dist 4) picked before Zen at dist 1
  h.drops.push(
    { id: 50, cell: { x: 101, y: 100 }, item: null, zen: 500 },
    { id: 51, cell: { x: 104, y: 100 }, item: makeItem(14, 13), zen: 0 },
  );
  h.autoPlay.update(300);
  expect(h.pickups[0]).toBe(51);
});

test('Anti-stuck watchdog forgets an unreachable target after 8s without progress', () => {
  const h = makeAutoPlayHarness();
  const stuckMob = makeChar(20, 'monster', 101, 100);
  const otherMob = makeChar(21, 'monster', 103, 100);
  h.monsters.push(stuckMob, otherMob);

  h.autoPlay.start();
  h.autoPlay.update(1000);
  expect(h.attacks[0]?.id).toBe(20);

  // 9 seconds later with hero still at (100, 100) and stuckMob still alive -> watchdog forgets 20, next tick attacks 21
  h.autoPlay.update(10_000);
  h.attacks.length = 0;
  h.autoPlay.update(10_100);
  expect(h.attacks[0]?.id).toBe(21);
});
