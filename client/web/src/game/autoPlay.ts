// Auto play: the desktop client's helper (MUHelper::CMuHelper) run in the browser. Started and
// stopped through the server (MuHelperStatusChangeRequest → MuHelperStatusUpdate, which may charge
// zen), it works once per frame like CMuHelper::Work: the Dark Raven's command once, health potions,
// the three buffs (on the hero, or on each party member in turn; refreshed on a timer), the healing
// skill (the hero, or each party member by its health step) and Drain Life, regrouping at the start
// spot, picking up drops by the pick-up rules, attacking the nearest monster in the hunting range with
// the skill rotation (basic skill, two activation skills on a timer or a monster count, combo) or a
// plain attack, and self repair. Entering a safe zone stops it.

import { read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import type { Character } from './characters.js';
import type { Item } from './inventory.js';
import { PetAttackMode, SkillActivation, type AutoPlayConfig } from './autoPlayConfig.js';

const PICKUP_RETRY_SECONDS = 1;
const PICKUP_REQUEST_RETRY_SECONDS = 3;
const TARGET_RETRY_SECONDS = 2;
const MAX_PICKUP_REQUESTS = 3;
const DEFAULT_DURABILITY_THRESHOLD = 50;
const MAX_NOT_READY_ATTEMPTS = 4;
const MAX_ACTIONABLE_DISTANCE = 10;
const NO_TARGET = 0xffff;

// The desktop's skill numbers (ActionSkillType, _enum.h) the helper looks for.
export const HelperSkill = {
  ATTACK: 28, ATTACK_STR: 420,
  DEFENSE: 27, DEFENSE_STR: 417, DEFENSE_MASTERY: 423,
  INFINITY_ARROW: 77, INFINITY_ARROW_STR: 441,
  SOUL_BARRIER: 16, SOUL_BARRIER_STR: 403, SOUL_BARRIER_PROFICIENCY: 404,
  SWELL_LIFE: 48, SWELL_LIFE_STR: 356, SWELL_LIFE_PROFICIENCY: 360,
  EXPANSION_OF_WIZARDRY: 233, EXPANSION_OF_WIZARDRY_STR: 380, EXPANSION_OF_WIZARDRY_MASTERY: 383,
  ADD_CRITICAL: 64, ADD_CRITICAL_STR1: 511, ADD_CRITICAL_STR2: 515, ADD_CRITICAL_STR3: 517,
  ALICE_BERSERKER: 218, ALICE_BERSERKER_STR: 469, ALICE_THORNS: 217,
  ATT_UP_OURFORCES: 266, HP_UP_OURFORCES: 267, HP_UP_OURFORCES_STR: 573,
  DEF_UP_OURFORCES: 268, DEF_UP_OURFORCES_STR: 569, DEF_UP_OURFORCES_MASTERY: 572,
  HEALING: 26, HEALING_STR: 413, ALICE_DRAINLIFE: 214, ALICE_DRAINLIFE_STR: 458,
} as const;
const Skill = HelperSkill;

// The buffs (eBuffState, the server's effect numbers) the helper checks for.
export const HelperBuff = {
  ATTACK: 1, DEFENSE: 2, WIZ_DEFENSE: 4, ADD_CRITICAL_DAMAGE: 5, INFINITY_ARROW: 6, LIFE: 8,
  THORNS: 71, BERSERKER: 81, SWELL_OF_MAGIC_POWER: 82, ATT_UP_OURFORCES: 129, HP_UP_OURFORCES: 130, DEF_UP_OURFORCES: 131,
} as const;
const Buff = HelperBuff;

/** How BuffTarget casts a buff skill: the buff it gives, whether the timed refresh recasts it, whether it's aimed at the character. */
interface BuffRule { buff: number; timed: boolean; needsTarget: boolean }

// BuffTarget's table.
const BUFF_RULES = new Map<number, BuffRule>();
const rule = (skills: number[], buff: number, timed: boolean, needsTarget: boolean) => { for (const skill of skills) BUFF_RULES.set(skill, { buff, timed, needsTarget }); };
rule([Skill.ATTACK, Skill.ATTACK_STR], Buff.ATTACK, true, true);
rule([Skill.DEFENSE, Skill.DEFENSE_STR, Skill.DEFENSE_MASTERY], Buff.DEFENSE, true, true);
rule([Skill.INFINITY_ARROW, Skill.INFINITY_ARROW_STR], Buff.INFINITY_ARROW, false, false);
rule([Skill.SOUL_BARRIER, Skill.SOUL_BARRIER_STR, Skill.SOUL_BARRIER_PROFICIENCY], Buff.WIZ_DEFENSE, true, true);
rule([Skill.SWELL_LIFE, Skill.SWELL_LIFE_STR, Skill.SWELL_LIFE_PROFICIENCY], Buff.LIFE, true, false);
rule([Skill.EXPANSION_OF_WIZARDRY, Skill.EXPANSION_OF_WIZARDRY_STR, Skill.EXPANSION_OF_WIZARDRY_MASTERY], Buff.SWELL_OF_MAGIC_POWER, false, false);
rule([Skill.ADD_CRITICAL, Skill.ADD_CRITICAL_STR1, Skill.ADD_CRITICAL_STR2, Skill.ADD_CRITICAL_STR3], Buff.ADD_CRITICAL_DAMAGE, false, false);
rule([Skill.ALICE_BERSERKER, Skill.ALICE_BERSERKER_STR], Buff.BERSERKER, false, false);
rule([Skill.ALICE_THORNS], Buff.THORNS, false, false);
rule([Skill.ATT_UP_OURFORCES], Buff.ATT_UP_OURFORCES, true, false);
rule([Skill.HP_UP_OURFORCES, Skill.HP_UP_OURFORCES_STR], Buff.HP_UP_OURFORCES, true, false);
rule([Skill.DEF_UP_OURFORCES, Skill.DEF_UP_OURFORCES_STR, Skill.DEF_UP_OURFORCES_MASTERY], Buff.DEF_UP_OURFORCES, true, false);
const SWELL_LIFE_SKILLS: readonly number[] = [Skill.SWELL_LIFE, Skill.SWELL_LIFE_STR, Skill.SWELL_LIFE_PROFICIENCY];

/** The buff a helper buff skill gives (BuffTarget's table), null for other skills. */
export function helperBuffOf(skill: number): number | null {
  return BUFF_RULES.get(skill)?.buff ?? null;
}

/** A party member as the helper sees it: its character when in view, and its health in tenths. */
export interface AutoPlayPartyMember { character: Character | null; healthStep: number }

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
  /** Whether the hero knows a skill (the skill list's GetSkillIndex). */
  knowsSkill: (skill: number) => boolean;
  /** Casts a buff or heal at the hero or a party member, or Drain Life at a monster; false when it can't be. */
  castSupport: (skill: number, target: Character) => boolean;
  /** Whether a walk, swing or cast is under way (the automation's Busy: IsSwingInProgress, IsWalkInProgress). */
  actionBusy: () => boolean;
  /** Whether a character has a buff (g_isCharacterBuff). */
  hasBuff: (character: Character, buff: number) => boolean;
  /** The party in its list's order; empty without a party. */
  party: () => AutoPlayPartyMember[];
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
  // Buffs, heals and the pet
  private currentBuffIndex = 0;
  private currentBuffPartyIndex = 0;
  private currentHealPartyIndex = 0;
  private nextBuffCastAt = 0;
  private timerActivatedBuffOngoing = false;
  private petActivated = false;
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
    this.currentBuffIndex = 0;
    this.currentBuffPartyIndex = 0;
    this.currentHealPartyIndex = 0;
    this.nextBuffCastAt = 0;
    this.timerActivatedBuffOngoing = false;
    this.petActivated = false;
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

  // Work: the pet, potions, buffs, heals, then the drop being fetched, regrouping, more loot,
  // attacking, repairing. A buff or heal under way holds up what comes after it.
  private work(now: number): void {
    this.activatePet();
    this.consumePotion();
    if (!this.buff()) return;
    if (!this.recoverHealth(now)) return;
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

  // ActivatePet: the Dark Raven's attack mode, sent once when auto play starts.
  private activatePet(): void {
    const config = this.config!;
    if (!config.useDarkRaven || this.petActivated) return;
    // PetCommandMode: Normal (0), AttackRandom (1), AttackWithOwner (2).
    const mode = config.darkRavenMode === PetAttackMode.CEASE ? 0
      : config.darkRavenMode === PetAttackMode.AUTO ? 1
      : config.darkRavenMode === PetAttackMode.TOGETHER ? 2 : null;
    if (mode !== null) this.connection.send('ClientToServer', 'PetCommandRequest', { PetType: 0 /* DarkRaven */, CommandMode: mode, TargetId: NO_TARGET });
    this.petActivated = true;
  }

  // Buff: the three buff skills in turn, on the hero, or with party support on each member in view
  // (within ten cells) in turn; a timed refresh recasts them every so many seconds.
  private buff(): boolean {
    const config = this.config!;
    if (!config.buffs.some((skill) => skill !== 0)) return true;
    const party = this.world.party();
    if (config.supportParty && party.length > 0) {
      this.currentBuffPartyIndex %= party.length;
      const member = party[this.currentBuffPartyIndex].character;
      if (member && this.distanceTo(member) <= MAX_ACTIONABLE_DISTANCE) {
        this.startTimedBuffRefresh(config.buffDurationParty);
        // A cast still under way keeps this member and buff for the next tick.
        if (!this.buffTarget(member, config.buffs[this.currentBuffIndex])) return false;
      }
      this.currentBuffPartyIndex = (this.currentBuffPartyIndex + 1) % party.length;
    } else {
      this.currentBuffPartyIndex = 0;
      this.startTimedBuffRefresh(config.buffDuration);
      if (!this.buffTarget(this.world.hero(), config.buffs[this.currentBuffIndex])) return false;
    }
    if (this.currentBuffPartyIndex === 0) {
      this.currentBuffIndex = (this.currentBuffIndex + 1) % config.buffs.length;
      // Everyone has had every buff: a scheduled refresh is complete, the next one counts from now.
      if (this.currentBuffIndex === 0 && this.timerActivatedBuffOngoing) {
        this.timerActivatedBuffOngoing = false;
        this.nextBuffCastAt = this.secondsElapsed + config.buffCastInterval;
      }
    }
    return true;
  }

  // StartTimedBuffRefresh: unless only expired buffs are recast, the interval starts a refresh round.
  private startTimedBuffRefresh(refreshOnExpiryOnly: boolean): void {
    const config = this.config!;
    if (!refreshOnExpiryOnly && config.buffCastInterval !== 0 && !this.timerActivatedBuffOngoing && this.secondsElapsed >= this.nextBuffCastAt) {
      this.timerActivatedBuffOngoing = true;
    }
  }

  // BuffTarget: cast when the character lacks the skill's buff, or (the timed ones) during a refresh round.
  private buffTarget(character: Character, skill: number): boolean {
    const rule = BUFF_RULES.get(skill);
    if (!rule) return true;
    if (SWELL_LIFE_SKILLS.includes(skill) && this.comboState === 2) return true;
    if (!this.world.hasBuff(character, rule.buff) || (rule.timed && this.timerActivatedBuffOngoing)) {
      return this.supportSkill(skill, rule.needsTarget ? character : this.world.hero());
    }
    return true;
  }

  // RecoverHealth: the healing skill, then Drain Life.
  private recoverHealth(now: number): boolean {
    return this.heal() && this.drainLife(now);
  }

  // GetHealingSkill / GetDrainLifeSkill: the first of the skills the hero knows (0 for none).
  private knownSkill(skills: number[]): number {
    return skills.find((skill) => this.world.knowsSkill(skill)) ?? 0;
  }

  // The hero's life in whole percent, rounded up (null without a maximum).
  private lifePercent(): number | null {
    const life = this.world.life(), max = this.world.maxLife();
    return max <= 0 ? null : Math.ceil((life * 100) / max);
  }

  // Heal: the hero below the threshold, or with party healing each member in turn whose health
  // step is at or below the party threshold, within ten cells.
  private heal(): boolean {
    const config = this.config!;
    if (!config.autoHeal) return true;
    const skill = this.knownSkill([Skill.HEALING, Skill.HEALING_STR]);
    if (!skill) return true;
    const party = this.world.party();
    if (config.autoHealParty && party.length > 0) {
      this.currentHealPartyIndex %= party.length;
      const member = party[this.currentHealPartyIndex];
      const character = member.character;
      let result = true;
      if (character) {
        if (character === this.world.hero()) result = this.healSelf(skill);
        else if (member.healthStep * 10 <= config.healPartyThreshold && this.distanceTo(character) <= MAX_ACTIONABLE_DISTANCE) result = this.supportSkill(skill, character);
      }
      this.currentHealPartyIndex = (this.currentHealPartyIndex + 1) % party.length;
      return result;
    }
    this.currentHealPartyIndex = 0;
    return this.healSelf(skill);
  }

  // HealSelf: the hero's life (whole percent, rounded up) at or below the threshold.
  private healSelf(skill: number): boolean {
    const percent = this.lifePercent();
    if (percent === null || percent > this.config!.healThreshold) return true;
    return this.supportSkill(skill, this.world.hero());
  }

  // DrainLife: at or below the heal threshold, Drain Life at the nearest monster in the hunting range.
  private drainLife(now: number): boolean {
    const config = this.config!;
    if (!config.useDrainLife) return true;
    const skill = this.knownSkill([Skill.ALICE_DRAINLIFE, Skill.ALICE_DRAINLIFE_STR]);
    if (!skill) return true;
    const percent = this.lifePercent();
    if (percent === null || percent > config.healThreshold) return true;
    this.currentTarget = this.nearestTarget(now);
    if (this.currentTarget) return this.supportSkill(skill, this.currentTarget);
    return true;
  }

  // SimulateSupportSkill: only a cast under way (busy, or walking into range) holds up the work
  // after it; one that can't happen must not.
  private supportSkill(skill: number, target: Character): boolean {
    if (this.world.actionBusy()) return false;
    if (!this.world.skillUsable(skill)) return true;
    this.world.castSupport(skill, target);
    return true;
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
    if (!this.currentTarget) this.currentTarget = this.nearestTarget(now, monsters);
    return this.currentTarget;
  }

  // GetNearestTarget: the nearest living monster in the hunting range that isn't cooling down.
  private nearestTarget(now: number, monsters = [...this.world.monsters()].filter((m) => !m.dead && m.kind === 'monster')): Character | null {
    let near: Character | null = null, nearDistance = this.huntingDistance;
    for (const m of monsters) {
      if (this.coolingDown(m.id, now)) continue;
      const d = this.distanceTo(m);
      if (d <= nearDistance) { nearDistance = d; near = m; }
    }
    return near;
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
