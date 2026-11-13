// Auto play: the desktop client's helper (MUHelper::CMuHelper) run in the browser. Started and
// stopped through the server (MuHelperStatusChangeRequest → MuHelperStatusUpdate, which may charge
// zen), it works once per frame like CMuHelper::Work: health potions, regrouping at the start spot,
// picking up drops by the pick-up rules, attacking the nearest monster in the hunting range with the
// skill rotation (basic skill, two activation skills on a timer or a monster count, combo) or a plain
// attack, and self repair. Entering a safe zone stops it. Buffs, healing skills, party support and
// the Dark Raven come with the buff system.

import { read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import type { Character } from './characters.js';
import type { Item } from './inventory.js';
import { SkillActivation, type AutoPlayConfig } from './autoPlayConfig.js';

const PICKUP_RETRY_SECONDS = 1;
const PICKUP_REQUEST_RETRY_SECONDS = 3;
const TARGET_RETRY_SECONDS = 2;
const MAX_PICKUP_REQUESTS = 3;
const DEFAULT_DURABILITY_THRESHOLD = 50;
const MAX_NOT_READY_ATTEMPTS = 4;

/** A drop the helper may pick up. */
export interface AutoPlayDrop { id: number; cell: { x: number; y: number }; item: Item | null; zen: number }

/** What auto play drives and reads: the rest of the game, through small functions. */
export interface AutoPlayWorld {
  hero: () => Character;
  inSafeZone: () => boolean;
  monsters: () => Iterable<Character>;
  /** Casts a skill at a monster (true when the cast started or is under way). */
  cast: (skill: number, target: Character) => boolean;
  /** A plain attack at a monster. */
  attack: (target: Character) => void;
  /** Whether the hero is busy with a cast, attack or walk it was given. */
  busy: () => boolean;
  /** Whether a skill can be used now (learned, requirements, equipment). */
  skillUsable: (skill: number) => boolean;
  life: () => number;
  maxLife: () => number;
  money: () => number;
  /** Drinks the best healing potion; false when there is none. */
  drinkHealingPotion: () => boolean;
  drops: () => Iterable<AutoPlayDrop>;
  pickUp: (id: number) => void;
  /** Whether the hero is walking to a drop to pick it up. */
  pickingUp: () => boolean;
  walkTo: (cell: { x: number; y: number }) => boolean;
  /** Equipment that wants repair: slots whose durability is at or below the share, and their self-repair cost. */
  worn: () => { slot: number; percent: number; cost: number }[];
  repair: (slot: number) => void;
  itemName: (item: Item) => string;
  isJewel: (item: Item) => boolean;
  message: (text: string) => void;
}

// ComputeDistanceBetween: the distance in whole cells, rounded up.
const distanceBetween = (a: { x: number; y: number }, b: { x: number; y: number }) => Math.ceil(Math.hypot(a.x - b.x, a.y - b.y));
// ComputeDistanceByRange: a range setting as a distance (the diagonal of a range x range square).
const distanceByRange = (range: number) => distanceBetween({ x: 0, y: 0 }, { x: range, y: range });

export class AutoPlay {
  active = false;
  /** Zen the server charged since the helper started (AddCost). */
  totalCost = 0;
  private config: AutoPlayConfig | null = null;
  private huntingDistance = 0;
  private obtainingDistance = 0;
  private origin = { x: 0, y: 0 };
  private secondsElapsed = 0;
  private secondsAway = 0;
  private nextSecondAt = 0;
  private currentTarget: Character | null = null;
  private readonly attackers = new Set<number>();
  private readonly targetRetryAt = new Map<number, number>();
  private currentItem: number | null = null;
  private readonly itemRetryAt = new Map<number, number>();
  private readonly itemRequests = new Map<number, number>();
  private comboState = 0;
  // AttackSkillRotation
  private nextSlot = 0;
  private notReadyAttempts = 0;
  private readonly timerDueAt = [0, 0, 0];
  private readonly conditionHeldUntil = [0, 0, 0];
  private readonly listeners = new Set<() => void>();

  constructor(private readonly connection: Connection, private readonly world: AutoPlayWorld) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(): void {
    for (const listener of this.listeners) listener();
  }

  /** The settings (Load): hunting and pick-up distances come from them. */
  setConfig(config: AutoPlayConfig): void {
    this.config = config;
    this.huntingDistance = distanceByRange(config.huntingRange);
    this.obtainingDistance = distanceByRange(config.obtainingRange);
  }

  // Toggle (Home key, the helper bar): ask the server; it answers with the status.
  toggle(): void {
    if (this.active) { this.requestStop(); this.stop(); } else this.requestStart();
  }

  requestStart(): void {
    if (this.world.inSafeZone()) { this.world.message('Auto play can’t start in a safe zone.'); return; }
    this.connection.send('ClientToServer', 'MuHelperStatusChangeRequest', { PauseStatus: false });
  }

  requestStop(): void {
    this.connection.send('ClientToServer', 'MuHelperStatusChangeRequest', { PauseStatus: true });
  }

  // ReceiveMuHelperStatusUpdate (C1 BF 51).
  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xbf || bytes[3] !== 0x51 || bytes.length < 16) return;
    const status = read<{ ConsumeMoney: boolean; Money: number; PauseStatus: boolean }>('ServerToClient', 'MuHelperStatusUpdate', bytes);
    if (status.PauseStatus) { this.stop(); return; }
    this.start();
    if (status.Money > 0 && status.ConsumeMoney) {
      this.totalCost += status.Money;
      this.world.message(`${this.totalCost} zen(s) have been spent in implementing auto play.`);
    }
  }

  // Start: a fresh run from where the hero stands.
  start(): void {
    if (this.active) return;
    this.totalCost = 0;
    this.comboState = 0;
    this.currentTarget = null;
    this.currentItem = null;
    this.itemRetryAt.clear();
    this.itemRequests.clear();
    this.targetRetryAt.clear();
    this.attackers.clear();
    this.nextSlot = 0;
    this.notReadyAttempts = 0;
    this.timerDueAt.fill(0);
    this.conditionHeldUntil.fill(0);
    this.origin = { ...this.world.hero().cell };
    this.secondsElapsed = 0;
    this.secondsAway = 0;
    this.nextSecondAt = performance.now() + 1000;
    this.active = true;
    this.changed();
  }

  stop(): void {
    if (!this.active) return;
    this.active = false;
    this.currentTarget = null;
    this.changed();
  }

  /** A monster attacked the hero (for counter attacks and self defense). */
  attackedBy(monster: Character): void {
    if (!this.active || !this.config) return;
    this.attackers.add(monster.id);
    if (this.config.useSelfDefense && !this.coolingDown(monster.id)) this.currentTarget = monster;
  }

  // Tick: once per frame.
  update(now = performance.now()): void {
    if (!this.active || !this.config) return;
    const hero = this.world.hero();
    if (hero.dead) return;
    if (this.world.inSafeZone()) { this.world.message('Auto play stopped: you entered a safe zone.'); this.requestStop(); this.stop(); return; }
    const away = distanceBetween(hero.cell, this.origin) > 1;
    while (now >= this.nextSecondAt) {
      this.secondsElapsed++;
      this.secondsAway = away ? this.secondsAway + 1 : 0;
      this.nextSecondAt += 1000;
    }
    this.work(now);
  }

  // Work: potions first, then the drop being fetched, regrouping, more loot, attacking, repairing.
  private work(now: number): void {
    this.consumePotion();
    if (this.currentItem !== null && !this.obtainItem(now)) return;
    if (!this.regroup()) return;
    if (!this.obtainItem(now)) return;
    this.attackStep(now);
    this.repairEquipment();
  }

  // ConsumePotion: below the threshold (life in whole percent, rounded up) drink a healing potion.
  private consumePotion(): void {
    const config = this.config!;
    const life = this.world.life(), max = this.world.maxLife();
    if (!config.useHealPotion || max <= 0 || life <= 0) return;
    if (Math.ceil((life * 100) / max) <= config.potionThreshold) this.world.drinkHealingPotion();
  }

  // Regroup: back to the start spot after being away longer than allowed.
  private regroup(): boolean {
    const config = this.config!;
    if (!config.returnToOriginalPosition || this.secondsAway <= config.maxSecondsAway) return true;
    const hero = this.world.hero();
    if (distanceBetween(hero.cell, this.origin) <= 1) { this.secondsAway = 0; this.comboState = 0; this.currentTarget = null; return true; }
    if (!hero.isWalking && !this.world.walkTo(this.origin)) return true; // no path: carry on
    return false;
  }

  // HelperPickupFilter::ShouldPickUpHelperItem
  private wanted(drop: AutoPlayDrop): boolean {
    const config = this.config!;
    if (config.pickAllItems) return true;
    if (!config.pickSelectItems) return false;
    const item = drop.item;
    if (config.pickZen && !item && drop.zen > 0) return true;
    if (!item) return false;
    if ((config.pickJewel && this.world.isJewel(item)) || (config.pickAncient && item.ancientSet > 0) || (config.pickExcellent && item.excellent > 0)) return true;
    if (config.pickExtraItems) {
      const name = this.world.itemName(item);
      if (config.extraItems.some((keyword) => keyword && name.includes(keyword))) return true;
    }
    return false;
  }

  // ObtainItem / SelectItemToObtain: the nearest wanted drop in the pick-up range.
  private obtainItem(now: number): boolean {
    const hero = this.world.hero();
    const drops = new Map([...this.world.drops()].map((drop) => [drop.id, drop]));
    if (this.currentItem !== null && !drops.has(this.currentItem)) this.forgetItem(this.currentItem);
    if (this.currentItem === null) {
      let best: AutoPlayDrop | null = null;
      let bestDistance = this.config!.obtainingRange;
      for (const drop of drops.values()) {
        const retry = this.itemRetryAt.get(drop.id);
        if ((retry !== undefined && now < retry) || !this.wanted(drop)) continue;
        const distance = distanceBetween(hero.cell, drop.cell);
        if (distance <= bestDistance) { bestDistance = distance; best = drop; }
      }
      if (!best) return true;
      this.currentItem = best.id;
    }
    const drop = drops.get(this.currentItem)!;
    if (this.world.pickingUp()) return false; // walking to it
    if (distanceBetween(hero.cell, drop.cell) > this.obtainingDistance) { this.currentItem = null; return true; } // TooFar
    // Requested: the server's removal forgets the drop; a refused one waits longer each time, then is given up.
    const requests = (this.itemRequests.get(drop.id) ?? 0) + 1;
    if (requests > MAX_PICKUP_REQUESTS) { this.forgetItem(drop.id); return true; }
    this.itemRequests.set(drop.id, requests);
    this.itemRetryAt.set(drop.id, now + PICKUP_REQUEST_RETRY_SECONDS * 1000 * (1 << (requests - 1)));
    this.world.pickUp(drop.id);
    if (!this.world.pickingUp()) this.currentItem = null; // asked at once (it was within reach)
    void PICKUP_RETRY_SECONDS;
    return false;
  }

  private forgetItem(id: number): void {
    this.itemRetryAt.delete(id);
    this.itemRequests.delete(id);
    if (this.currentItem === id) this.currentItem = null;
  }

  private coolingDown(id: number, now = performance.now()): boolean {
    const retry = this.targetRetryAt.get(id);
    if (retry === undefined) return false;
    if (now < retry) return true;
    this.targetRetryAt.delete(id);
    return false;
  }

  private distanceTo(target: Character): number {
    const hero = this.world.hero();
    const next = target.path.length ? target.path[target.path.length - 1] : target.cell;
    return Math.min(distanceBetween(hero.cell, target.cell), distanceBetween(hero.cell, next));
  }

  private inHuntingRange(target: Character): boolean {
    return (this.config!.longRangeCounterAttack && this.attackers.has(target.id)) || this.distanceTo(target) <= this.huntingDistance;
  }

  // SelectAttackTarget: keep the current target while it lives and is in range; else the farthest
  // attacker (long range counter attack) or the nearest monster in the hunting range.
  private selectTarget(now: number): Character | null {
    const monsters = [...this.world.monsters()].filter((m) => !m.dead && m.kind === 'monster');
    const alive = new Set(monsters.map((m) => m.id));
    for (const id of this.attackers) if (!alive.has(id)) this.attackers.delete(id);
    const current = this.currentTarget;
    if (current && alive.has(current.id) && this.inHuntingRange(current)) return current;
    this.currentTarget = null;
    if (this.config!.longRangeCounterAttack) {
      let far: Character | null = null, farDistance = -1;
      for (const m of monsters) {
        if (!this.attackers.has(m.id) || this.coolingDown(m.id, now)) continue;
        const d = this.distanceTo(m);
        if (d > farDistance) { farDistance = d; far = m; }
      }
      this.currentTarget = far;
    }
    if (!this.currentTarget) {
      let near: Character | null = null, nearDistance = this.huntingDistance;
      for (const m of monsters) {
        if (this.coolingDown(m.id, now)) continue;
        const d = this.distanceTo(m);
        if (d <= nearDistance) { nearDistance = d; near = m; }
      }
      this.currentTarget = near;
    }
    return this.currentTarget;
  }

  // Attack: the rotation's skill, else (fallback) a plain attack.
  private attackStep(now: number): void {
    const target = this.selectTarget(now);
    if (!target) { this.comboState = 0; return; }
    if (this.world.busy()) return;
    const config = this.config!;
    if (config.useCombo) {
      if (config.skills.some((skill) => skill <= 0)) return;
      if (this.world.cast(config.skills[this.comboState], target)) this.comboState = (this.comboState + 1) % 3;
      return;
    }
    const nearby = [...this.world.monsters()].filter((m) => !m.dead && m.kind === 'monster' && this.inHuntingRange(m)).length;
    const attacking = [...this.attackers].length;
    const { skill, slot, triggered } = this.selectSkill(nearby, attacking);
    if (skill > 0 && this.world.skillUsable(skill)) {
      const cast = this.world.cast(skill, target);
      this.completeSkill(slot, triggered, cast);
      if (cast) return;
    }
    if (config.fallbackBasicAttack) this.world.attack(target);
  }

  // AttackSkillRotation::Select: a triggered activation skill (timer or monster count), else the
  // automatic slots in turn, else the basic skill.
  private selectSkill(nearby: number, attacking: number): { skill: number; slot: number; triggered: boolean } {
    const config = this.config!;
    const seconds = this.secondsElapsed;
    for (let slot = 1; slot < 3; slot++) {
      if ((config.skillConditions[slot] & SkillActivation.ON_TIMER) && config.skillIntervals[slot] > 0 && this.timerDueAt[slot] === 0) {
        this.timerDueAt[slot] = seconds + config.skillIntervals[slot];
      }
    }
    for (let slot = 1; slot < 3; slot++) {
      const skill = config.skills[slot];
      if (skill <= 0) continue;
      const condition = config.skillConditions[slot];
      const interval = config.skillIntervals[slot];
      const timerDue = (condition & SkillActivation.ON_TIMER) !== 0 && interval > 0 && seconds >= this.timerDueAt[slot];
      const conditionMet = (condition & SkillActivation.ON_CONDITION) !== 0 && seconds >= this.conditionHeldUntil[slot] && conditionHolds(condition, nearby, attacking);
      if ((timerDue || conditionMet) && this.world.skillUsable(skill)) {
        if (timerDue) this.timerDueAt[slot] = seconds + interval;
        return { skill, slot, triggered: true };
      }
    }
    const automatic = (slot: number) => config.skills[slot] > 0 && (slot === 0 || !(config.skillConditions[slot] & (SkillActivation.ON_TIMER | SkillActivation.ON_CONDITION)));
    if ([1, 2].some(automatic)) {
      for (let offset = 0; offset < 3; offset++) {
        const slot = (this.nextSlot + offset) % 3;
        if (!automatic(slot) || !this.world.skillUsable(config.skills[slot])) continue;
        return { skill: config.skills[slot], slot, triggered: false };
      }
    }
    return { skill: config.skills[0], slot: -1, triggered: false };
  }

  // AttackSkillRotation::Complete
  private completeSkill(slot: number, triggered: boolean, cast: boolean): void {
    if (triggered) {
      if (!cast) this.conditionHeldUntil[slot] = this.secondsElapsed + 1;
      return;
    }
    if (slot < 0) return;
    this.nextSlot = slot;
    if (cast || ++this.notReadyAttempts >= MAX_NOT_READY_ATTEMPTS) { this.nextSlot = (slot + 1) % 3; this.notReadyAttempts = 0; }
  }

  // RepairEquipments: worn items at or below half durability, when the zen covers the self repair.
  private repairEquipment(): void {
    if (!this.config!.repairItem) return;
    for (const { slot, percent, cost } of this.world.worn()) {
      if (percent <= DEFAULT_DURABILITY_THRESHOLD && cost <= this.world.money()) this.world.repair(slot);
    }
  }

  /** An unreachable monster isn't chosen again for a while (ForgetTarget). */
  forgetTarget(target: Character): void {
    this.targetRetryAt.set(target.id, performance.now() + TARGET_RETRY_SECONDS * 1000);
    if (this.currentTarget === target) this.currentTarget = null;
  }
}

// IsConditionMet: more than N monsters nearby / attacking.
function conditionHolds(condition: number, nearby: number, attacking: number): boolean {
  let count: number;
  if (condition & SkillActivation.ON_MOBS_NEARBY) count = nearby;
  else if (condition & SkillActivation.ON_MOBS_ATTACKING) count = attacking;
  else return false;
  return ((condition & SkillActivation.ON_MORE_THAN_TWO_MOBS) !== 0 && count >= 2)
    || ((condition & SkillActivation.ON_MORE_THAN_THREE_MOBS) !== 0 && count >= 3)
    || ((condition & SkillActivation.ON_MORE_THAN_FOUR_MOBS) !== 0 && count >= 4)
    || ((condition & SkillActivation.ON_MORE_THAN_FIVE_MOBS) !== 0 && count >= 5);
}
