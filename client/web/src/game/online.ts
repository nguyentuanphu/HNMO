// Keeps the map in sync with the game server: who is in view, where they walk, and
// sends the player's own walks. Mirrors the client's handlers in WSclient.cpp
// (ReceiveCreateMonsterViewport, ReceiveCreatePlayerViewportExtended,
// ReceiveDeleteCharacterViewport, ReceiveMoveCharacter).

import { read, packetCode } from '../net/packets.js';
import { classOfAppearance } from '../net/session.js';
import { parseAppearance } from './items.js';
import { createMonsterCharacter, createPlayerCharacter } from './characters.js';
import { findPath, directionBetween, isWalkableCell } from './pathfinding.js';
import type * as THREE from 'three';
import type { Connection } from '../net/connection.js';
import type { PacketValues } from '../net/packets.js';
import type { Cell, Character, CharacterWorld } from './characters.js';

const ID_MASK = 0x7fff;          // the high bit marks "just created" (WSclient.cpp: Key &= 0x7FFF)
const APPEARANCE_LENGTH = 27;   // the server's extended appearance (AppearanceSerializerExtended.NeededSpace)
const MAX_STEPS_PER_WALK = 15;   // the step count is a 4-bit field
const TALK_DISTANCE = 3;         // cells; the server refuses farther talks
const PING_INTERVAL_MS = 20000;

const Code = { CHAT: 0x00, SERVER_MESSAGE: 0x0d, ADD_PLAYERS: 0x12, ADD_NPCS: 0x13, OUT_OF_SCOPE: 0x14, MOVED: 0x15, MAP_CHANGED: 0x1c, WALKED: 0xd4, PING_REQUEST: 0x71 };
const MAP_CHANGED_SUB_CODE = 0x0f;

// The fields this file reads of the server's packets (server/src/Network/Packets).
interface MapChangedPacket { IsMapChange: boolean; MapNumber: number; PositionX: number; PositionY: number; Rotation: number }
interface ChatMessagePacket { Sender: string; Message: string }
interface SlideNoticePacket { Message: string }
interface NpcData {
  Id: number; TypeNumber: number; CurrentPositionX: number; CurrentPositionY: number;
  TargetPositionX: number; TargetPositionY: number; Rotation: number;
}
interface AddNpcsPacket { NPCs: NpcData[] }
interface AddCharacterPacket {
  Id: number; Name: string; CurrentPositionX: number; CurrentPositionY: number;
  TargetPositionX: number; TargetPositionY: number; Rotation: number; AppearanceAndEffects: Uint8Array;
}
interface OutOfScopePacket { Objects: { Id: number }[] }
interface ObjectWalkedPacket { ObjectId: number; TargetX: number; TargetY: number }
interface ObjectMovedPacket { ObjectId: number; PositionX: number; PositionY: number }

/** Where a character shows up and where it's walking to. */
interface Placement { x: number; y: number; targetX: number; targetY: number; rotation: number }

/** The player's own character; it may know how to turn to a cell. */
export type Hero = Character & { faceTo?(cell: Cell): void };

/** How a message shows in the chat: said by someone (in public, to the party, the guild, the alliance, the Gens), whispered, or from the server. */
export type MessageKind = 'chat' | 'system' | 'party' | 'guild' | 'union' | 'gens' | 'whisper' | 'error';

/** ReceiveChat: the channel prefix says where a line belongs (~ party, @@ alliance, @ guild, $ Gens) and is cut off. */
export function chatKind(message: string): [MessageKind, string] {
  if (message.startsWith('~')) return ['party', message.slice(1)];
  if (message.startsWith('@@')) return ['union', message.slice(2)];
  if (message.startsWith('@')) return ['guild', message.slice(1)];
  if (message.startsWith('$')) return ['gens', message.slice(2)];
  return ['chat', message];
}

export interface OnlineWorldOptions {
  connection: Connection;
  /** The scene node characters are added to. */
  root: THREE.Object3D;
  world: CharacterWorld;
  hero: Hero;
  playerId: number;
  onChange?: () => void;
  /** Loads another map and puts the hero at x, y. */
  onMapChanged?: (mapNumber: number, x: number, y: number) => Promise<void> | void;
  onMessage?: (text: string, kind: MessageKind) => void;
}

export class OnlineWorld {
  onMapChanged: OnlineWorldOptions['onMapChanged'];
  onMessage: OnlineWorldOptions['onMessage'];
  connection: Connection;
  root: THREE.Object3D;
  world: CharacterWorld;
  hero: Hero;
  playerId: number;
  /** The other characters in view, by object id. */
  characters: Map<number, Character>;
  /** Ids whose characters are still loading. */
  pending: Set<number>;
  onChange: OnlineWorldOptions['onChange'];
  /** A player came into view with these effects (buffs), or a character left it (effects null). */
  onEffects?: (id: number, effects: number[] | null) => void;
  unsubscribe: () => void;
  pingTimer: ReturnType<typeof setInterval>;
  /** Where the hero is walking to, over several walk packets. */
  goal?: Cell | null;
  /** The NPC to talk to once the hero gets there. */
  talkTarget?: Character | null;
  /** Told when the hero talks to an NPC (TalkToNpcRequest sent), e.g. which merchant the shop is. */
  onTalk?: (npc: Character) => void;
  /** Asked before talking to an NPC: true when it handled the talk itself (an event's own NPCs). */
  interceptTalk?: (npc: Character) => boolean;
  /** Also asked before TalkToNpcRequest: true when the click was handled otherwise (a Crywolf altar's contract). */
  beforeTalk?: (npc: Character) => boolean;

  constructor({ connection, root, world, hero, playerId, onChange, onMapChanged, onMessage }: OnlineWorldOptions) {
    this.onMapChanged = onMapChanged;
    this.onMessage = onMessage;
    this.connection = connection;
    this.root = root;
    this.world = world;
    this.hero = hero;
    this.playerId = playerId & ID_MASK;
    this.characters = new Map();
    this.pending = new Set();
    this.onChange = onChange;
    this.unsubscribe = connection.onPacket((bytes) => this.handle(bytes));
    this.pingTimer = setInterval(() => this.ping(), PING_INTERVAL_MS);
  }

  handle(bytes: Uint8Array) {
    const { code, subCode } = packetCode(bytes);
    try {
      if (code === Code.MAP_CHANGED && bytes[0] === 0xc3 && subCode === MAP_CHANGED_SUB_CODE) return this.mapChanged(read<MapChangedPacket>('ServerToClient', 'MapChanged', bytes));
      if (code === Code.CHAT && bytes[0] === 0xc1) { const m = read<ChatMessagePacket>('ServerToClient', 'ChatMessage', bytes); const [kind, text] = chatKind(m.Message); return this.onMessage?.(`${m.Sender}: ${text}`, kind); }
      // The client reads every notice with the layout of PRECEIVE_NOTICE: type, count, delay, color, speed, text (ReceiveNotice).
      if (code === Code.SERVER_MESSAGE && bytes[0] === 0xc1 && bytes.length > 13) return this.onMessage?.(read<SlideNoticePacket>('ServerToClient', 'SlideNotice', bytes).Message, 'system');
      if (code === Code.ADD_NPCS && bytes[0] === 0xc2) this.addNpcs(read<AddNpcsPacket>('ServerToClient', 'AddNpcsToScope', bytes));
      else if (code === Code.ADD_PLAYERS && bytes[0] === 0xc2) this.addPlayer(read<AddCharacterPacket>('ServerToClient', 'AddCharacterToScopeExtended', bytes));
      else if (code === Code.OUT_OF_SCOPE && bytes[0] === 0xc1) this.remove(read<OutOfScopePacket>('ServerToClient', 'MapObjectOutOfScope', bytes));
      else if (code === Code.WALKED && bytes[0] === 0xc1) this.walked(read<ObjectWalkedPacket>('ServerToClient', 'ObjectWalkedExtended', bytes));
      else if (code === Code.MOVED && bytes[0] === 0xc1) this.moved(read<ObjectMovedPacket>('ServerToClient', 'ObjectMoved', bytes));
      else if (code === Code.PING_REQUEST && bytes[0] === 0xc1) this.connection.send('ClientToServer', 'PingResponse');
    } catch (error) {
      console.warn('Packet not handled:', error);
    }
  }

  async spawn(id: number, create: () => Promise<Character>, placement: Placement): Promise<void> {
    if (id === this.playerId || this.characters.has(id) || this.pending.has(id)) return;
    this.pending.add(id);
    try {
      const character = await create();
      if (!this.pending.has(id)) return; // went out of view while loading
      character.placeAt(placement.x, placement.y, placement.rotation);
      if (placement.targetX !== placement.x || placement.targetY !== placement.y) character.walkStraightTo(placement.targetX, placement.targetY);
      this.characters.set(id, character);
      this.root.add(character.group);
      this.onChange?.();
    } catch (error) {
      console.warn(`Couldn't show object ${id}:`, (error as Error).message);
    } finally {
      this.pending.delete(id);
    }
  }

  addNpcs(packet: AddNpcsPacket): void {
    for (const npc of packet.NPCs) {
      const id = npc.Id & ID_MASK;
      this.spawn(id, () => createMonsterCharacter({ id, type: npc.TypeNumber }), {
        x: npc.CurrentPositionX, y: npc.CurrentPositionY, targetX: npc.TargetPositionX, targetY: npc.TargetPositionY, rotation: npc.Rotation,
      });
    }
  }

  addPlayer(player: AddCharacterPacket): void {
    const id = player.Id & ID_MASK;
    const bodySet = classOfAppearance(player.AppearanceAndEffects) + 1;
    const { equipment } = parseAppearance(player.AppearanceAndEffects);
    // The effects follow the appearance (27 bytes): a count, then one byte per effect.
    const effects = player.AppearanceAndEffects;
    const count = effects[APPEARANCE_LENGTH] ?? 0;
    this.onEffects?.(id, [...effects.subarray(APPEARANCE_LENGTH + 1, APPEARANCE_LENGTH + 1 + count)]);
    this.spawn(id, () => createPlayerCharacter({ id, name: player.Name, bodySet, equipment }), {
      x: player.CurrentPositionX, y: player.CurrentPositionY, targetX: player.TargetPositionX, targetY: player.TargetPositionY, rotation: player.Rotation,
    });
  }

  remove(packet: OutOfScopePacket): void {
    for (const { Id } of packet.Objects) {
      const id = Id & ID_MASK;
      this.pending.delete(id);
      this.onEffects?.(id, null);
      const character = this.characters.get(id);
      if (!character) continue;
      this.root.remove(character.group);
      this.characters.delete(id);
    }
    this.onChange?.();
  }

  walked(packet: ObjectWalkedPacket): void {
    const character = this.characters.get(packet.ObjectId & ID_MASK);
    character?.walkStraightTo(packet.TargetX, packet.TargetY);
  }

  moved(packet: ObjectMovedPacket): void {
    const id = packet.ObjectId & ID_MASK;
    if (id === this.playerId) this.goal = null;
    const character = id === this.playerId ? this.hero : this.characters.get(id);
    character?.placeAt(packet.PositionX, packet.PositionY);
  }

  // A warp: to another map (the client loads it), or a teleport on the same map.
  // Mirrors ReceiveTeleport (WSclient.cpp), which ends with ClientReadyAfterMapChange.
  async mapChanged(packet: MapChangedPacket): Promise<void> {
    this.goal = null;
    this.clearCharacters();
    if (packet.IsMapChange) await this.onMapChanged?.(packet.MapNumber, packet.PositionX, packet.PositionY);
    else this.hero.placeAt(packet.PositionX, packet.PositionY);
    this.connection.send('ClientToServer', 'ClientReadyAfterMapChange');
  }

  clearCharacters(): void {
    for (const [id, character] of this.characters) { this.root.remove(character.group); this.onEffects?.(id, null); }
    this.characters.clear();
    this.pending.clear();
    this.onChange?.();
  }

  say(text: string): void {
    this.connection.send('ClientToServer', 'PublicChatMessage', { Character: this.hero.name, Message: text });
  }

  // Talks to an NPC (vault keeper, merchants, …): walks next to it first when it's farther than
  // the server allows, then sends TalkToNpcRequest, as the client does when an NPC is clicked.
  talkTo(npc: Character): void {
    this.talkTarget = npc;
    if (!this.closeEnoughToTalk(npc)) {
      const near = this.cellNextTo(npc.cell);
      if (near) this.walkTo(near, { keepTalk: true });
    }
  }

  closeEnoughToTalk(npc: Character): boolean {
    return Math.max(Math.abs(npc.cell.x - this.hero.cell.x), Math.abs(npc.cell.y - this.hero.cell.y)) <= TALK_DISTANCE;
  }

  cellNextTo(cell: Cell): Cell | null {
    let best: (Cell & { distance: number }) | null = null;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const candidate = { x: cell.x + dx, y: cell.y + dy };
        if ((dx || dy) && isWalkableCell(this.world.flags, candidate.x, candidate.y)) {
          const distance = Math.hypot(candidate.x - this.hero.cell.x, candidate.y - this.hero.cell.y);
          if (!best || distance < best.distance) best = { ...candidate, distance };
        }
      }
    }
    return best && { x: best.x, y: best.y };
  }

  // Walks the player to a cell and tells the server, as the client's SendRequestMove does.
  // A walk packet holds at most 15 steps; longer walks continue when a part ends.
  walkTo(cell: Cell, { keepTalk = false }: { keepTalk?: boolean; keepAttack?: boolean } = {}): boolean {
    if (!keepTalk) this.talkTarget = null;
    this.goal = { x: cell.x, y: cell.y };
    return this.walkNextPart();
  }

  walkNextPart(): boolean {
    const cell = this.goal;
    if (!cell || (cell.x === this.hero.cell.x && cell.y === this.hero.cell.y)) { this.goal = null; return false; }
    const path = findPath(this.world.flags, this.hero.cell, cell).slice(0, MAX_STEPS_PER_WALK);
    if (!path.length) { this.goal = null; return false; }
    const directions = new Uint8Array(Math.ceil(path.length / 2));
    let previous = this.hero.cell;
    path.forEach((step, index) => {
      const direction = directionBetween(previous, step);
      directions[index >> 1] |= index % 2 === 0 ? direction << 4 : direction;
      previous = step;
    });
    const last = directionBetween(path.length > 1 ? path[path.length - 2] : this.hero.cell, path[path.length - 1]);
    this.connection.send('ClientToServer', 'WalkRequest', {
      SourceX: this.hero.cell.x, SourceY: this.hero.cell.y, StepCount: path.length, TargetRotation: last, Directions: directions,
    });
    this.hero.walk(path);
    return true;
  }

  ping(): void {
    this.connection.send('ClientToServer', 'Ping', { TickCount: Math.floor(performance.now()) >>> 0, AttackSpeed: 0, MagicSpeed: 0 });
  }

  update(dt: number): void {
    if (this.goal && !this.hero.isWalking) this.walkNextPart();
    if (this.talkTarget && !this.hero.isWalking && !this.goal) {
      if (this.closeEnoughToTalk(this.talkTarget)) {
        if (this.interceptTalk?.(this.talkTarget) || this.beforeTalk?.(this.talkTarget)) {
          this.hero.faceTo?.(this.talkTarget.cell);
        } else {
          this.connection.send('ClientToServer', 'TalkToNpcRequest', { NpcId: this.talkTarget.id });
          this.onTalk?.(this.talkTarget);
          this.hero.faceTo?.(this.talkTarget.cell);
        }
      }
      this.talkTarget = null;
    }
    for (const character of this.characters.values()) character.update(dt, this.world);
  }

  dispose(): void {
    clearInterval(this.pingTimer);
    this.unsubscribe();
    for (const character of this.characters.values()) this.root.remove(character.group);
    this.characters.clear();
  }
}
