// The party, as the desktop keeps it (Party[], ReceiveParty, ReceivePartyResult, ReceivePartyList,
// ReceivePartyInfo, ReceivePartyLeave): invitations (PartyRequest C1 40) answered with
// PartyInviteResponse, the result codes (C1 41), the member list (C1 42 01) and their health steps
// (C1 44), leaving (C1 43). /party invites the player standing next to the hero, facing it.

import type { Connection } from '../net/connection.js';
import type { Character } from './characters.js';

export interface PartyMember {
  name: string; number: number; map: number; x: number; y: number; life: number; maxLife: number;
  /** Health in tenths (ReceivePartyInfo's step). */
  healthStep: number;
}

// ReceivePartyResult's messages (Localization/Game.en.resx).
const RESULTS = [
  'Creating a party has failed.', 'Your request has been denied.', 'Party is full.', 'The user has left the game.',
  'The user is already in another party.', 'You have just left the party.', 'You cannot form a party with a member of the opposing gens.',
  'You cannot form a party within a Battle Zone.', 'Parties are not activated within a Battle Zone.',
];

/** The player next to the hero (one cell) that faces it, as the desktop's /party and /trade find them. */
export function facingPlayer(hero: Character, players: Iterable<Character>): Character | null {
  const direction = (angle: number) => Math.floor((((angle * 180) / Math.PI + 360) % 360 + 22.5) / 360 * 8 + 1) % 8;
  for (const player of players) {
    if (player === hero || player.kind !== 'player' || player.dead) continue;
    if (Math.abs(player.cell.x - hero.cell.x) > 1 || Math.abs(player.cell.y - hero.cell.y) > 1) continue;
    if (Math.abs(direction(player.angle) - direction(hero.angle)) === 4) return player;
  }
  return null;
}

export class Party {
  members: PartyMember[] = [];
  private readonly listeners = new Set<() => void>();

  constructor(private readonly connection: Connection, private readonly actions: {
    /** Asks the player whether to join (true: accept). */
    confirm: (text: string) => Promise<boolean>;
    message: (text: string) => void;
    nameOf: (id: number) => string;
  }) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(): void { for (const listener of this.listeners) listener(); }

  get count(): number { return this.members.length; }

  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1) return;
    const code = bytes[2];
    if (code === 0x40 && bytes.length >= 5) {
      // ReceiveParty: someone asks the hero to join
      const requester = (bytes[3] << 8) | bytes[4];
      void this.actions.confirm(`${this.actions.nameOf(requester)}\nSomeone requests you to join their a party`).then((accepted) => {
        this.connection.send('ClientToServer', 'PartyInviteResponse', { Accepted: accepted, RequesterId: requester });
      });
    } else if (code === 0x41 && bytes.length === 4) {
      const text = RESULTS[bytes[3]];
      if (text) this.actions.message(text);
    } else if (code === 0x42 && bytes[3] === 0x01) {
      // ReceivePartyList: names, numbers, maps, positions, life
      const count = Math.min(5, bytes[4]);
      const decoder = new TextDecoder();
      const members: PartyMember[] = [];
      for (let i = 0; i < count; i++) {
        const at = 5 + i * 24;
        if (at + 24 > bytes.length) break;
        const raw = bytes.subarray(at, at + 10);
        const end = raw.indexOf(0);
        const u32 = (o: number) => (bytes[o] | (bytes[o + 1] << 8) | (bytes[o + 2] << 16) | (bytes[o + 3] << 24)) >>> 0;
        members.push({
          name: decoder.decode(raw.subarray(0, end < 0 ? 10 : end)), number: bytes[at + 10], map: bytes[at + 11],
          x: bytes[at + 12], y: bytes[at + 13], life: u32(at + 16), maxLife: u32(at + 20),
          healthStep: this.members[i]?.healthStep ?? 10,
        });
      }
      this.members = members;
      this.changed();
    } else if (code === 0x44) {
      // ReceivePartyInfo: each member's health in tenths
      const count = bytes[3];
      for (let i = 0; i < count && i < this.members.length; i++) this.members[i].healthStep = Math.min(10, Math.max(0, bytes[4 + i] & 0x0f));
      this.changed();
    } else if (code === 0x43 && bytes.length <= 4) {
      // ReceivePartyLeave
      this.members = [];
      this.actions.message('You have just left the party.');
      this.changed();
    }
  }

  /** Asks the server to invite a player (PartyInviteRequest). */
  invite(target: Character): void {
    this.connection.send('ClientToServer', 'PartyInviteRequest', { TargetPlayerId: target.id });
    this.actions.message(`You have requested ${target.name} to join your party.`);
  }

  // The /party command: not when in someone else's party; the player next to the hero (one cell)
  // that faces it.
  command(hero: Character, players: Iterable<Character>, heroName: string): void {
    if (this.members.length > 0 && this.members[0].name !== heroName) { this.actions.message('You are already in a party'); return; }
    const player = facingPlayer(hero, players);
    if (player) { this.invite(player); return; }
    this.actions.message('Type /party with the mouse cursor on a player to create a party with them.');
  }

  /** Leaves or (the leader) removes a member (PartyPlayerKickRequest with its number). */
  leave(index: number): void {
    const member = this.members[index];
    if (member) this.connection.send('ClientToServer', 'PartyPlayerKickRequest', { PlayerIndex: member.number });
  }

  /** The leader may remove anyone; everyone else only themselves. */
  canLeave(index: number, heroName: string): boolean {
    return this.members[0]?.name === heroName || this.members[index]?.name === heroName;
  }
}
