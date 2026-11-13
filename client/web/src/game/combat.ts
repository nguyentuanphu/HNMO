// Fighting, as the desktop client does it (ZzzInterface's MOVEMENT_ATTACK, ReceiveAttackDamage,
// ReceiveDie, ReceiveAction): clicking a monster makes it the target; out of range the hero walks
// next to it; in range, once the last swing has played, the hero turns to it, swings with the
// animation its weapons give (SetPlayerAttack) and sends HitRequest. With auto attack (the
// desktop's option, on by default) the hero keeps attacking until the target dies or the player
// does something else. The server's hits show their damage numbers, make the target flinch and
// set its health bar; deaths play the death animation; others' attacks play theirs.

import * as THREE from 'three';
import { read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import { TERRAIN_SCALE, TERRAIN_SIZE, TerrainFlag } from '../formats.js';
import type { Character } from './characters.js';
import type { OnlineWorld } from './online.js';
import type { Inventory, Item } from './inventory.js';
import {
  attackRange, damageTexts, deathAction, hitPlaysShock, killDamageText, monsterAnimationReaction, monsterShockAction,
  monsterAttackAction, newMonsterAttackState, playerAnimationReaction, playerAttackAction, playerShockAction, ServerAnimation,
  type AttackLookInput, type DamageText, type HandItem, type MonsterAttackState, type Mount,
} from './combatLook.js';
import { areaSkillRotation, skillCastPlan, skillAnimationAction, skillRange, type SkillCastContext, type SkillCastPlan } from './skillCast.js';
import { itemDefinition } from './items.js';
import { skillInfo } from './skills.js';

const ID_MASK = 0x7fff;
const HIT_SHOCK_FLAG = 0x8000;
const AT_ATTACK1 = ServerAnimation.ATTACK1;

interface HitPacket {
  Kind: number; IsDoubleDamage: boolean; IsTripleDamage: boolean; ObjectId: number;
  HealthStatus: number; ShieldStatus: number; HealthDamage: number; ShieldDamage: number;
}
interface KilledPacket { KilledId: number; SkillId: number; KillerId: number }
interface AnimationPacket { ObjectId: number; Direction: number; Animation: number; TargetId: number }
interface MonsterSkillPacket { SkillNumber: number; AttackerId: number; TargetId: number }
interface ExperiencePacket { AddedExperience: number; DamageOfLastHit: number; KilledObjectId: number }
interface RespawnPacket { PositionX: number; PositionY: number; MapNumber: number; Direction: number }
interface SkillAnimationPacket { SkillId: number; PlayerId: number; TargetId: number }
interface AreaSkillAnimationPacket { SkillId: number; PlayerId: number; PointX: number; PointY: number; Rotation: number }

/** What casting needs from the rest of the game: the current skill and the hero's mana and AG. */
export interface SkillSource {
  /** The skill number the right mouse button casts (the current skill), null for none. */
  current: () => number | null;
  mana: () => number;
  ability: () => number;
  /** Mana ran short: the desktop drinks a mana potion then (FindManaItemIndex). */
  outOfMana?: () => void;
}

/** A skill the hero is about to cast: at a character, or at a cell (area skills). */
interface PendingSkill { skill: number; plan: SkillCastPlan; target: Character | null; cell: { x: number; y: number } | null }

export interface CombatEvents {
  /** A hit to show: the character hit and the texts the desktop draws for it. */
  onDamage?: (target: Character, texts: DamageText[]) => void;
  /** A system line for the chat (experience, death). */
  onMessage?: (text: string) => void;
  /** The hero died; respawn comes from the server. */
  onHeroDied?: () => void;
  onRespawn?: (map: number, x: number, y: number) => void;
  /** The target or its health changed. */
  onTargetChanged?: (target: Character | null) => void;
  /** Someone started an attack (for arrows and other projectiles); look is the hero's when it's the hero. */
  onSwing?: (actor: Character, target: Character | null, look: AttackLookInput | null) => void;
  /** Someone died (for the death effects). */
  onKilled?: (character: Character) => void;
  /** A monster attacked the hero (for auto play's counter attack and self defense). */
  onHeroAttacked?: (attacker: Character) => void;
  /** Someone cast a skill (SkillAnimation / AreaSkillAnimation, and the hero's own cast): for its effects. */
  onSkill?: (caster: Character, skill: number, target: Character | null, cell: { x: number; y: number } | null) => void;
}

// The pets that change how the hero attacks (inventory slot 8): Horn of Fenrir, Dark Horse, Uniria, Dinorant.
function mountOf(pet: Item | undefined): Mount {
  if (!pet || pet.group !== 13) return 'none';
  if (pet.number === 37) return 'fenrir';
  if (pet.number === 4) return 'darkHorse';
  if (pet.number === 2) return 'uniria';
  if (pet.number === 3) return 'dinorant';
  return 'none';
}

const hand = (item: Item | undefined): HandItem | null => (item ? { group: item.group, number: item.number } : null);

export class Combat {
  target: Character | null = null;
  private swing = 0;
  private readonly monsterStates = new Map<number, MonsterAttackState>();
  private readonly stop: () => void;
  private pendingSkill: PendingSkill | null = null;
  /** The skill auto attack repeats (the desktop's Attacking = 2 after a cast). */
  private repeatSkill: PendingSkill | null = null;
  skills: SkillSource | null = null;

  constructor(
    private readonly connection: Connection,
    private readonly online: OnlineWorld,
    private readonly inventory: Inventory,
    private readonly classIndex: number,
    private readonly events: CombatEvents = {},
  ) {
    this.stop = connection.onPacket((bytes) => this.handle(bytes));
  }

  private get hero(): Character { return this.online.hero; }

  // What decides the hero's swing and reach: class, weapons, mount, wings, safe zone.
  lookInput(): AttackLookInput {
    const items = this.inventory.items;
    return {
      classIndex: this.classIndex, rightHand: hand(items.get(0)), leftHand: hand(items.get(1)),
      mount: mountOf(items.get(8)), wings: items.has(7), safeZone: this.inSafeZone(),
    };
  }

  private inSafeZone(): boolean {
    const { x, y } = this.hero.cell;
    return (this.online.world.flags[y * TERRAIN_SIZE + x] & TerrainFlag.SAFEZONE) !== 0;
  }

  // Attacks a monster (the player clicked it).
  attack(target: Character): void {
    if (target.dead) return;
    this.target = target;
    this.events.onTargetChanged?.(target);
  }

  // What the cast rules look at: the hands' weapon kinds, the mount, wings, safe zone.
  private castContext(): SkillCastContext {
    const items = this.inventory.items;
    const kind = (item: Item | undefined) => (item ? itemDefinition(item.group, item.number)?.weaponKind : undefined);
    const look = this.lookInput();
    return {
      rightHandKind: kind(items.get(0)), leftHandKind: kind(items.get(1)), rightHand: look.rightHand, leftHand: look.leftHand,
      mount: look.mount, mounted: look.mount !== 'none', wings: look.wings, safeZone: look.safeZone,
    };
  }

  // A right click: cast the current skill at a character (or the hero itself for buffs), or at a cell
  // for area skills (the desktop's ExecuteSkill with the selected character and the mouse's tile).
  cast(target: Character | null, cell: { x: number; y: number } | null, skillNumber?: number): boolean {
    const skill = skillNumber ?? this.skills?.current() ?? null;
    if (skill === null || this.hero.dead) return false;
    const plan = skillCastPlan(skill, this.classIndex, this.castContext());
    if (!plan || plan.packet === 'none') return false;
    if (target && target.dead) target = null;
    if (plan.needsTarget && !target) return false;
    this.pendingSkill = { skill, plan, target, cell: cell ?? (target ? { ...target.cell } : null) };
    this.repeatSkill = null;
    if (target && !plan.friendly) { this.target = target; this.events.onTargetChanged?.(target); }
    return true;
  }

  /** Whether the hero is fighting: a skill about to be cast, or a living target being attacked. */
  get busy(): boolean {
    return this.pendingSkill !== null || (this.target !== null && !this.target.dead && this.online.characters.has(this.target.id));
  }

  // The player did something else (walked somewhere): the attack stops.
  cancel(): void {
    this.pendingSkill = null;
    this.repeatSkill = null;
    if (!this.target) return;
    this.target = null;
    this.events.onTargetChanged?.(null);
  }

  private distanceInCells(other: Character): number {
    return this.hero.position.distanceTo(other.position) / TERRAIN_SCALE;
  }

  update(): void {
    if (this.pendingSkill) { this.updateSkill(this.pendingSkill); return; }
    const target = this.target;
    if (!target) return;
    if (this.repeatSkill && !target.dead && this.online.characters.has(target.id)) {
      this.pendingSkill = { ...this.repeatSkill, target, cell: { ...target.cell } };
      return;
    }
    if (target.dead || !this.online.characters.has(target.id) || this.hero.dead) { this.cancel(); return; }
    if (this.inSafeZone()) return; // no attacks in a safe zone, as on the desktop
    const look = this.lookInput();
    const range = attackRange(look);
    if (this.distanceInCells(target) > range) {
      // Walk next to it (the desktop's path to the target), unless already on the way.
      if (!this.hero.isWalking) {
        const near = this.online.cellNextTo(target.cell);
        if (near) this.online.walkTo(near, { keepTalk: false, keepAttack: true });
      }
      return;
    }
    if (this.hero.isWalking || this.hero.isPlayingOnce) return; // the last swing first
    this.hero.faceTo(target.cell);
    this.hero.playOnce(playerAttackAction(look, this.swing++));
    this.events.onSwing?.(this.hero, target, look);
    const degrees = ((this.hero.angle * 180) / Math.PI + 360) % 360;
    const direction = Math.floor((degrees + 22.5) / 360 * 8 + 1) % 8;
    this.connection.send('ClientToServer', 'HitRequest', { TargetId: target.id, AttackAnimation: AT_ATTACK1, LookingDirection: direction });
  }

  // Walk into the skill's range, then turn, play the cast and send the skill (AttackWizard /
  // AttackKnight / AttackElf: SendRequestMagic or SendRequestMagicContinue).
  private updateSkill(pending: PendingSkill): void {
    const { skill, plan } = pending;
    const target = pending.target;
    if (this.hero.dead || (target && (target.dead || (target !== this.hero && !this.online.characters.has(target.id))))) { this.pendingSkill = null; return; }
    if (this.inSafeZone()) { this.pendingSkill = null; return; } // CanExecuteSkill: no skills in a safe zone
    const data = skillInfo(skill);
    const range = skillRange(skill, data?.distance ?? 0);
    const aim = target && target !== this.hero ? target.cell : pending.cell;
    if (aim && range > 0 && target !== this.hero) {
      const distance = Math.hypot(aim.x - this.hero.cell.x, aim.y - this.hero.cell.y);
      if (distance > range) {
        if (!this.hero.isWalking) {
          const near = target ? this.online.cellNextTo(target.cell) : aim;
          if (near) this.online.walkTo(near, { keepTalk: false, keepAttack: true });
        }
        return;
      }
    }
    if (this.hero.isWalking || this.hero.isPlayingOnce) return;
    // CheckMana: mana and AG first (out of mana the desktop drinks a mana potion instead).
    if (this.skills && data && data.mana > this.skills.mana()) { this.skills.outOfMana?.(); this.pendingSkill = null; return; }
    if (this.skills && data && data.ability > this.skills.ability()) { this.pendingSkill = null; return; }
    if (aim) this.hero.faceTo(aim);
    if (plan.heroAction !== null) this.hero.playOnce(plan.heroAction);
    const degrees = ((this.hero.angle * 180) / Math.PI + 360) % 360;
    if (plan.packet === 'targeted') {
      const id = plan.targetSelf || !target || target === this.hero ? this.online.playerId : target.id;
      this.connection.send('ClientToServer', 'TargetedSkill', { SkillId: skill, TargetId: id });
    } else if (plan.packet === 'area') {
      // AreaSkill: at the hero's own tile or the target's, with the selected character when the skill names one.
      const at = plan.areaAt === 'self' ? this.hero.cell : aim;
      if (at) {
        const extra = plan.areaExtraTarget && target && target !== this.hero ? target.id : 0xffff;
        this.connection.send('ClientToServer', 'AreaSkill', { SkillId: skill, TargetX: at.x, TargetY: at.y, Rotation: areaSkillRotation(degrees, plan.rotationScale), ExtraTargetId: extra, AnimationCounter: 0 });
      }
    }
    this.events.onSkill?.(this.hero, skill, target, aim);
    this.pendingSkill = null;
    // Auto attack keeps casting at a monster for attack skills (Attacking = 2).
    this.repeatSkill = plan.keepAutoAttack && target && target !== this.hero && !plan.friendly ? pending : null;
  }

  private characterById(id: number): Character | null {
    if (id === this.online.playerId) return this.hero;
    return this.online.characters.get(id) ?? null;
  }

  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1 && bytes[0] !== 0xc3) return;
    const code = bytes[2];
    try {
      if (bytes[0] === 0xc1 && code === 0x11) this.hit(read<HitPacket>('ServerToClient', 'ObjectHitExtended', bytes));
      else if (bytes[0] === 0xc1 && code === 0x17) this.killed(read<KilledPacket>('ServerToClient', 'ObjectGotKilled', bytes));
      else if (bytes[0] === 0xc1 && code === 0x18) this.animation(read<AnimationPacket>('ServerToClient', 'ObjectAnimation', bytes));
      else if (bytes[0] === 0xc1 && code === 0x69) this.monsterSkill(read<MonsterSkillPacket>('ServerToClient', 'MonsterSkillAnimation', bytes));
      else if (bytes[0] === 0xc3 && code === 0x16) this.experience(read<ExperiencePacket>('ServerToClient', 'ExperienceGainedExtended', bytes));
      else if (bytes[0] === 0xc1 && code === 0xf3 && bytes[3] === 0x04) this.respawn(read<RespawnPacket>('ServerToClient', 'RespawnAfterDeathExtended', bytes));
      else if (bytes[0] === 0xc3 && code === 0x19 && bytes.length >= 9) this.skillAnimation(read<SkillAnimationPacket>('ServerToClient', 'SkillAnimation', bytes));
      else if (bytes[0] === 0xc3 && code === 0x1e && bytes.length >= 10) this.areaSkillAnimation(read<AreaSkillAnimationPacket>('ServerToClient', 'AreaSkillAnimation', bytes));
    } catch (error) {
      console.warn('Combat packet not handled:', error);
    }
  }

  private hit(packet: HitPacket): void {
    const id = packet.ObjectId & ID_MASK;
    const target = this.characterById(id);
    if (!target) return;
    const toHero = target === this.hero;
    // HealthStatus: 0xFF unknown, else 250ths of the life left (ReceiveAttackDamage).
    target.healthStatus = packet.HealthStatus === 0xff ? -1 : packet.HealthStatus / 250;
    const hitShock = (packet.ObjectId & HIT_SHOCK_FLAG) !== 0;
    this.events.onDamage?.(target, damageTexts({
      kind: packet.Kind, healthDamage: packet.HealthDamage, shieldDamage: packet.ShieldDamage,
      isDoubleDamage: packet.IsDoubleDamage, isTripleDamage: packet.IsTripleDamage, toHero, hitShock,
    }));
    if (hitPlaysShock({ hitShock, toHero }) && !target.dead) {
      const damage = packet.HealthDamage + packet.ShieldDamage;
      const action = target.kind === 'player' || toHero
        ? playerShockAction(toHero ? this.lookInput() : { classIndex: 0, rightHand: null, leftHand: null, mount: 'none' }, damage, target.action)
        : monsterShockAction(damage, target.action);
      if (action !== null && !target.isPlayingOnce) target.playOnce(action);
    }
    if (target === this.target) this.events.onTargetChanged?.(target);
  }

  private killed(packet: KilledPacket): void {
    const target = this.characterById(packet.KilledId & ID_MASK);
    if (!target || target.dead) return;
    target.dead = true;
    target.path = [];
    target.healthStatus = 0;
    target.playOnce(deathAction(target.kind === 'player' || target === this.hero), undefined, true);
    this.events.onKilled?.(target);
    if (target === this.hero) {
      this.cancel();
      this.events.onHeroDied?.();
    } else if (target === this.target) {
      this.cancel();
    }
  }

  private animation(packet: AnimationPacket): void {
    const actor = this.characterById(packet.ObjectId & ID_MASK);
    if (!actor || actor === this.hero || actor.dead) return;
    const victim = this.characterById(packet.TargetId & ID_MASK);
    if (victim) actor.faceTo(victim.cell);
    let reaction;
    if (actor.kind === 'player') {
      reaction = playerAnimationReaction(packet.Animation, { classIndex: 0, rightHand: null, leftHand: null, mount: 'none', safeZone: false }, this.swing);
    } else {
      let state = this.monsterStates.get(actor.id);
      if (!state) { state = newMonsterAttackState(); this.monsterStates.set(actor.id, state); }
      reaction = monsterAnimationReaction(packet.Animation, actor.attacks, state);
    }
    if (victim === this.hero && actor.kind === 'monster') this.events.onHeroAttacked?.(actor);
    if (reaction.kind === 'attack' && reaction.action !== null && reaction.action >= 0) {
      actor.playOnce(reaction.action);
      this.events.onSwing?.(actor, victim, null);
    }
    else if (reaction.kind === 'play') actor.playOnce(reaction.action);
    else if (reaction.kind === 'stop') actor.oneShot = null;
  }

  // A monster's own skill (MonsterSkillAnimation): the attack its data names for that skill.
  private monsterSkill(packet: MonsterSkillPacket): void {
    const actor = this.characterById(packet.AttackerId & ID_MASK);
    if (!actor || actor === this.hero || actor.dead) return;
    const victim = this.characterById(packet.TargetId & ID_MASK);
    if (victim) actor.faceTo(victim.cell);
    let state = this.monsterStates.get(actor.id);
    if (!state) { state = newMonsterAttackState(); this.monsterStates.set(actor.id, state); }
    state.monsterSkill = packet.SkillNumber;
    if (victim === this.hero) this.events.onHeroAttacked?.(actor);
    const action = monsterAttackAction(actor.attacks, state);
    if (action !== null && action >= 0) actor.playOnce(action);
  }

  // SkillAnimation: someone cast a skill at a target. Others play the skill's action; the hero
  // already did when casting.
  private skillAnimation(packet: SkillAnimationPacket): void {
    const caster = this.characterById(packet.PlayerId & ID_MASK);
    if (!caster || caster.dead) return;
    const target = this.characterById(packet.TargetId & ID_MASK);
    if (caster !== this.hero) {
      if (target && target !== caster) caster.faceTo(target.cell);
      const action = caster.kind === 'player' ? skillAnimationAction(packet.SkillId, caster.classIndex ?? 0, { own: false, via: 'targeted' }) : null;
      if (action !== null) caster.playOnce(action);
      this.events.onSkill?.(caster, packet.SkillId, target, target ? { ...target.cell } : null);
    }
  }

  private areaSkillAnimation(packet: AreaSkillAnimationPacket): void {
    const caster = this.characterById(packet.PlayerId & ID_MASK);
    if (!caster || caster.dead || caster === this.hero) return;
    const cell = { x: packet.PointX, y: packet.PointY };
    caster.faceTo(cell);
    const action = caster.kind === 'player' ? skillAnimationAction(packet.SkillId, caster.classIndex ?? 0, { own: false, via: 'area' }) : null;
    if (action !== null) caster.playOnce(action);
    this.events.onSkill?.(caster, packet.SkillId, null, cell);
  }

  private experience(packet: ExperiencePacket): void {
    if (packet.AddedExperience > 0) this.events.onMessage?.(`${packet.AddedExperience} experience`);
    const killed = this.characterById(packet.KilledObjectId & ID_MASK);
    const text = killDamageText(packet.DamageOfLastHit);
    if (killed && text) this.events.onDamage?.(killed, [text]);
  }

  private respawn(packet: RespawnPacket): void {
    const hero = this.hero;
    hero.dead = false;
    hero.oneShot = null;
    hero.healthStatus = -1;
    this.events.onRespawn?.(packet.MapNumber, packet.PositionX, packet.PositionY);
  }

  // A point above a character's feet in the scene's root coordinates (for damage numbers).
  static anchorOf(character: Character, height: number): THREE.Vector3 {
    return new THREE.Vector3(character.group.position.x, character.group.position.y, character.group.position.z + height);
  }

  dispose(): void {
    this.stop();
  }
}
