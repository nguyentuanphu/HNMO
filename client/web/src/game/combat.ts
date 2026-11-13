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
  newMonsterAttackState, playerAnimationReaction, playerAttackAction, playerShockAction, ServerAnimation,
  type AttackLookInput, type DamageText, type HandItem, type MonsterAttackState, type Mount,
} from './combatLook.js';

const ID_MASK = 0x7fff;
const HIT_SHOCK_FLAG = 0x8000;
const AT_ATTACK1 = ServerAnimation.ATTACK1;

interface HitPacket {
  Kind: number; IsDoubleDamage: boolean; IsTripleDamage: boolean; ObjectId: number;
  HealthStatus: number; ShieldStatus: number; HealthDamage: number; ShieldDamage: number;
}
interface KilledPacket { KilledId: number; SkillId: number; KillerId: number }
interface AnimationPacket { ObjectId: number; Direction: number; Animation: number; TargetId: number }
interface ExperiencePacket { AddedExperience: number; DamageOfLastHit: number; KilledObjectId: number }
interface RespawnPacket { PositionX: number; PositionY: number; MapNumber: number; Direction: number }

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

  // The player did something else (walked somewhere): the attack stops.
  cancel(): void {
    if (!this.target) return;
    this.target = null;
    this.events.onTargetChanged?.(null);
  }

  private distanceInCells(other: Character): number {
    return this.hero.position.distanceTo(other.position) / TERRAIN_SCALE;
  }

  update(): void {
    const target = this.target;
    if (!target) return;
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
    const degrees = ((this.hero.angle * 180) / Math.PI + 360) % 360;
    const direction = Math.floor((degrees + 22.5) / 360 * 8 + 1) % 8;
    this.connection.send('ClientToServer', 'HitRequest', { TargetId: target.id, AttackAnimation: AT_ATTACK1, LookingDirection: direction });
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
      else if (bytes[0] === 0xc3 && code === 0x16) this.experience(read<ExperiencePacket>('ServerToClient', 'ExperienceGainedExtended', bytes));
      else if (bytes[0] === 0xc1 && code === 0xf3 && bytes[3] === 0x04) this.respawn(read<RespawnPacket>('ServerToClient', 'RespawnAfterDeathExtended', bytes));
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
      reaction = monsterAnimationReaction(packet.Animation, null, state);
    }
    if (reaction.kind === 'attack' && reaction.action !== null) actor.playOnce(reaction.action);
    else if (reaction.kind === 'play') actor.playOnce(reaction.action);
    else if (reaction.kind === 'stop') actor.oneShot = null;
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
