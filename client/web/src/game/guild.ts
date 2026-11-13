// The guild, as the desktop keeps it (WSclient.cpp's ReceiveGuild*, GuildCache, GuildInfoWindow's
// data): join requests (C1 50) answered with GuildJoinResponse, join results (C1 51), the member list
// (C2 52), leaving / kicking results (C1 53), the guild master NPC's dialog (C1 54), the creation
// result (C1 56), a player leaving a guild (C1 5D), who belongs to which guild (C2 65) and each
// guild's name and mark (C1 66, asked for once per guild with GuildInfoRequest), the role results
// (C1 E1) and the guild notices (C1 0D type 2). /guild asks the guild master the hero faces to let
// it join, as the desktop's CheckCommand does.

import type { Connection } from '../net/connection.js';
import type { Character } from './characters.js';
import { facingPlayer } from './party.js';

/** The hero's and other players' place in a guild (GUILD_STATUS). */
export const GuildStatus = { NONE: 0xff, PERSON: 0, BATTLE_MASTER: 32, SUB_MASTER: 64, MASTER: 128 } as const;

export const MARK_CELLS = 64;

/** A guild's name, alliance and 8 x 8 mark (palette indexes 0..15), as GuildCache keeps them. */
export interface GuildMark { id: number; name: string; allianceName: string; cells: number[] }

export interface GuildMember {
  name: string;
  number: number;
  /** The server the member plays on (0-based), -1 when offline. */
  server: number;
  status: number;
}

/** What a player in view belongs to (ReceiveGuildIDViewport). */
export interface PlayerGuild { guildId: number; status: number; type: number; relationship: number }

// ReceiveGuildResult's messages (Localization/Game.en.resx).
const JOIN_RESULTS: Record<number, string> = {
  0: 'Guild master has refused your request to join the guild.',
  1: 'You have just joined the guild.',
  2: 'The guild is full.',
  3: 'The user has left the game.',
  4: 'The user is not a guild master.',
  5: 'You cannot join more than one guild.',
  6: 'The guild master is too busy to approve your request to join the guild',
  7: 'Chracters over level 6 can join a guild.',
  0xa1: 'The guild master has not joined the gens.',
  0xa2: 'The guild master is with a different gens.',
  0xa3: 'You must belong to the same gens as the guild master in order to join the guild.',
};

// ReceiveGuildLeave's messages.
const LEAVE_RESULTS: Record<number, string> = {
  0: 'The password you have entered is incorrect.',
  1: 'You have left the guild.',
  2: 'Only a guild master can disband a guild.',
  3: 'You have failed from the guild',
  4: 'The guild has been dissolved',
  5: 'guild member has been withdrawn.',
};

// ReceiveCreateGuildResult's messages.
const CREATE_RESULTS: Record<number, string> = {
  0: 'The guild name already exists',
  2: 'Guild name must be at least 4 characters',
  3: 'You are already in a guild.',
  4: 'No space allowed in guild names',
  5: 'No symbols allowed in guild names',
  6: 'Reserved name',
};

// ReceiveGuildAssign's messages.
const ASSIGN_DONE: Record<number, string> = { 1: 'Appointed', 2: 'Changed', 3: 'Cancelled' };
const ASSIGN_FAILED: Record<number, string> = { 0x10: 'That guild does not exist.', 0x11: 'No authorization', 0x12: 'Can no longer be appointed', 0x13: 'Wrong appointment' };

// GuildRelationshipResultText's failures.
const RELATIONSHIP_FAILED: Record<number, string> = {
  0x00: 'Failed', 0x10: 'Alliance function will be restricted due to the Castle Siege.', 0x11: 'No authorization',
  0x15: 'Alliance guild.', 0x16: 'Hostile guild.', 0x17: 'Guild alliance exists.', 0x18: 'Hostile guild exists.',
  0x19: 'Guild alliance does not exist.', 0x1a: 'Hostile guild does not exist.', 0x1b: 'Not a master of guild alliance',
  0x1c: 'Income', 0x1d: 'Incomplete requirements for creating a guild alliance', 0x1e: 'Maximum no. of guild alliance is 7.',
  0x20: 'Request has been cancelled.', 0xa1: 'The alliance master has not joined the gens.',
  0xa2: 'The guild master has not joined the gens.', 0xa3: 'You are with a different gens than the alliance master.',
};

const ID_MASK = 0x7fff;
const decoder = new TextDecoder();

function text(bytes: Uint8Array, at: number, length: number): string {
  const raw = bytes.subarray(at, Math.min(bytes.length, at + length));
  const end = raw.indexOf(0);
  return decoder.decode(end < 0 ? raw : raw.subarray(0, end));
}

/** The 32 bytes of a mark (two cells a byte, high nibble first) as 64 palette indexes. */
export function unpackMark(bytes: Uint8Array): number[] {
  const cells: number[] = [];
  for (let i = 0; i < MARK_CELLS; i++) cells.push(i % 2 === 0 ? (bytes[i >> 1] >> 4) & 0x0f : bytes[i >> 1] & 0x0f);
  return cells;
}

/** 64 palette indexes as the 32 bytes GuildCreateRequest carries (UpdateGMResultInfo). */
export function packMark(cells: readonly number[]): Uint8Array {
  const bytes = new Uint8Array(MARK_CELLS / 2);
  for (let i = 0; i < MARK_CELLS; i++) bytes[i >> 1] |= i % 2 === 0 ? (cells[i] & 0x0f) << 4 : cells[i] & 0x0f;
  return bytes;
}

// GuildMarkPalette.cpp: CreateGuildMark()'s 16 colours (R, G, B); 0 is see-through.
export const MARK_PALETTE: readonly (readonly [number, number, number])[] = [
  [0, 0, 0], [0, 0, 0], [128, 128, 128], [255, 255, 255], [255, 0, 0], [255, 128, 0], [255, 255, 0], [128, 255, 0],
  [0, 255, 0], [0, 255, 128], [0, 255, 255], [0, 128, 255], [0, 0, 255], [128, 0, 255], [255, 0, 255], [255, 0, 128],
];

const hex = (value: number) => value.toString(16).padStart(2, '0');

/** A finished mark's cell colour (Guild::MarkPalette::CellColor): index 0 is transparent. */
export function markCellColor(index: number): string {
  if (index <= 0 || index >= MARK_PALETTE.length) return '#00000000';
  const [r, g, b] = MARK_PALETTE[index];
  return `#${hex(r)}${hex(g)}${hex(b)}ff`;
}

/** The editor's cell colour (GuildMakeWindow's EditorCell): it reads the texture's colours as ARGB,
 * so red and blue show swapped, as on the desktop; index 0 is the black cell with a grey cross. */
export function editorCellColor(index: number): { color: string; empty: boolean } {
  if (index <= 0 || index >= MARK_PALETTE.length) return { color: '#000000ff', empty: true };
  const [r, g, b] = MARK_PALETTE[index];
  return { color: `#${hex(b)}${hex(g)}${hex(r)}ff`, empty: false };
}

export class Guild {
  /** The hero's position in its guild (GuildStatus.NONE without one). */
  status: number = GuildStatus.NONE;
  guildId: number | null = null;
  members: GuildMember[] = [];
  memberCount = -1;
  totalScore = 0;
  rivalName = '';
  notices: string[] = [];
  /** The guilds of the hero's alliance (the Alliance tab). */
  alliances: { name: string; memberCount: number; cells: number[] }[] = [];
  readonly marks = new Map<number, GuildMark>();
  /** Guild data of the players in view, by object id (the hero's own too). */
  readonly players = new Map<number, PlayerGuild>();
  private readonly requested = new Set<number>();
  private readonly listeners = new Set<(what: GuildEvent) => void>();

  constructor(private readonly connection: Connection, private readonly actions: {
    confirm: (text: string) => Promise<boolean>;
    message: (text: string) => void;
    nameOf: (id: number) => string;
    heroId: () => number;
  }) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: (what: GuildEvent) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(what: GuildEvent): void { for (const listener of this.listeners) listener(what); }

  /** The hero's guild (its name and mark), once the server has told it. */
  get heroMark(): GuildMark | null { return this.guildId === null ? null : this.marks.get(this.guildId) ?? null; }

  /** A player's guild and its mark, when known. */
  guildOf(id: number): { player: PlayerGuild; mark: GuildMark } | null {
    const player = this.players.get(id & ID_MASK);
    const mark = player ? this.marks.get(player.guildId) : undefined;
    return player && mark ? { player, mark } : null;
  }

  handle(bytes: Uint8Array): void {
    const type = bytes[0];
    if (type === 0xc2) {
      const code = bytes[3];
      if (code === 0x52) this.memberList(bytes);
      else if (code === 0x65) this.assign(bytes);
      else if (code === 0x67) this.unionViewport(bytes);
      else if (code === 0xe9) this.allianceList(bytes);
      return;
    }
    if (type !== 0xc1) return;
    const code = bytes[2];
    switch (code) {
      case 0x50: { // ReceiveGuild: someone asks the guild master (the hero) to join
        if (bytes.length < 5) return;
        const requester = (bytes[3] << 8) | bytes[4];
        void this.actions.confirm(`${this.actions.nameOf(requester & ID_MASK)}\nYou have received an offer to join a guild.`).then((accepted) => {
          this.connection.send('ClientToServer', 'GuildJoinResponse', { Accepted: accepted, RequesterId: requester });
        });
        return;
      }
      case 0x51: { const message = JOIN_RESULTS[bytes[3]]; if (message) this.actions.message(message); return; }
      case 0x53: this.leaveResult(bytes[3]); return;
      case 0x54: this.changed('masterDialog'); return; // ReceiveCreateGuildInterface
      case 0x56: { // ReceiveCreateGuildResult
        const result = bytes[3];
        const message = CREATE_RESULTS[result];
        if (message) this.actions.message(message);
        if (result === 1) this.changed('created');
        return;
      }
      case 0x5d: { // ReceiveDeleteGuildViewport
        if (bytes.length < 5) return;
        const id = ((bytes[3] & 0x7f) << 8) | bytes[4];
        this.players.delete(id);
        if (id === (this.actions.heroId() & ID_MASK)) { this.status = GuildStatus.NONE; this.guildId = null; }
        this.memberCount = -1;
        this.changed('left');
        return;
      }
      case 0x66: this.information(bytes); return;
      case 0xe1: { // ReceiveGuildAssign
        const message = bytes[4] === 1 ? ASSIGN_DONE[bytes[3]] ?? 'Invalid GuildAssign' : ASSIGN_FAILED[bytes[4]] ?? 'Invalid GuildAssign';
        this.actions.message(message);
        return;
      }
      case 0xe5: this.relationshipRequest(bytes); return;
      case 0xe6: this.relationshipResult(bytes); return;
      case 0xeb: // ReceiveBanUnionGuildResult
        if (bytes[3] !== 0x01) return;
        if (bytes[4] === 1) {
          if (this.alliances.length > 2) this.requestAlliances();
          this.alliances = [];
          this.changed('alliances');
        } else this.actions.message('Failed');
        return;
      case 0x0d: // ReceiveNotice type 2: a notice for the guild's members
        if (bytes[3] === 2 && bytes.length > 13) { this.notices.push(text(bytes, 13, bytes.length - 13)); this.changed('notice'); }
        return;
      default:
    }
  }

  // ReceiveGuildList: the hero's guild's members, its score and rival.
  private memberList(bytes: Uint8Array): void {
    const count = bytes[5];
    this.memberCount = count;
    this.totalScore = Math.max(0, ((bytes[8] | (bytes[9] << 8) | (bytes[10] << 16) | (bytes[11] << 24)) >> 0));
    this.rivalName = text(bytes, 13, 8);
    const members: GuildMember[] = [];
    for (let i = 0; i < count; i++) {
      const at = 24 + i * 13;
      if (at + 13 > bytes.length) break;
      const current = bytes[at + 11];
      members.push({ name: text(bytes, at, 10), number: bytes[at + 10], server: current & 0x80 ? current & 0x7f : -1, status: bytes[at + 12] });
    }
    this.members = members;
    this.changed('members');
  }

  // ReceiveGuildIDViewport: which guild each player in view belongs to; unknown guilds are asked for.
  private assign(bytes: Uint8Array): void {
    const count = bytes[4];
    const heroId = this.actions.heroId() & ID_MASK;
    for (let i = 0; i < count; i++) {
      const at = 5 + i * 12;
      if (at + 12 > bytes.length) break;
      const guildId = (bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24)) >>> 0;
      const id = ((bytes[at + 7] & 0x7f) << 8) | bytes[at + 8];
      const player: PlayerGuild = { guildId, status: bytes[at + 4], type: bytes[at + 5], relationship: bytes[at + 6] };
      this.players.set(id, player);
      if (id === heroId) { this.status = player.status; this.guildId = guildId; }
      if (!this.marks.has(guildId) && !this.requested.has(guildId)) {
        this.requested.add(guildId);
        this.connection.send('ClientToServer', 'GuildInfoRequest', { GuildId: guildId });
      }
    }
    this.changed('players');
  }

  // ReceiveGuildInfo: a guild's names and mark (GuildCache::SetGuildMark).
  private information(bytes: Uint8Array): void {
    if (bytes.length < 57) return;
    const id = (bytes[4] | (bytes[5] << 8) | (bytes[6] << 16) | (bytes[7] << 24)) >>> 0;
    this.marks.set(id, { id, allianceName: text(bytes, 9, 8), name: text(bytes, 17, 8), cells: unpackMark(bytes.subarray(25, 57)) });
    this.requested.delete(id);
    this.changed('players');
  }

  // ReceiveUnionViewportNotify: the alliance a guild belongs to and each player's relationship.
  private unionViewport(bytes: Uint8Array): void {
    const count = bytes[4];
    for (let i = 0; i < count; i++) {
      const at = 5 + i * 20;
      if (at + 20 > bytes.length) break;
      const id = ((bytes[at] & 0x7f) << 8) | bytes[at + 1];
      const guildId = (bytes[at + 4] | (bytes[at + 5] << 8) | (bytes[at + 6] << 16) | (bytes[at + 7] << 24)) >>> 0;
      const mark = this.marks.get(guildId);
      if (mark) mark.allianceName = text(bytes, at + 9, 8);
      const player = this.players.get(id);
      if (player) player.relationship = bytes[at + 8];
    }
    this.changed('players');
  }

  // ReceiveUnionList: the guilds of the hero's alliance.
  private allianceList(bytes: Uint8Array): void {
    this.alliances = [];
    if (bytes[5] === 1) {
      for (let i = 0; i < bytes[4]; i++) {
        const at = 8 + i * 41;
        if (at + 41 > bytes.length) break;
        this.alliances.push({ memberCount: bytes[at], cells: unpackMark(bytes.subarray(at + 1, at + 33)), name: text(bytes, at + 33, 8) });
      }
    }
    this.changed('alliances');
  }

  // CGuildInfoWindow::ReceiveGuildRelationShip: another guild master asks for an alliance or a rivalry.
  private relationshipRequest(bytes: Uint8Array): void {
    if (bytes.length < 7) return;
    const relationship = bytes[3], request = bytes[4];
    const requester = (bytes[5] << 8) | bytes[6];
    const reply = (approved: boolean) => this.connection.send('ClientToServer', 'GuildRelationshipChangeResponse', {
      RelationshipType: relationship, RequestType: request, Response: approved, TargetPlayerId: requester,
    });
    if ((relationship !== 1 && relationship !== 2) || (request !== 1 && request !== 2)) { reply(false); return; }
    const name = this.actions.nameOf(requester & ID_MASK);
    const join = request === 1;
    const lines = relationship === 1
      ? [`From ${name}, for a guild alliance`, join ? 'Received a registration request' : 'Received a withdrawal request']
      : [`From ${name}, for a hostile guild`, join ? 'Received approval request.' : 'Received cancellation request.'];
    void this.actions.confirm([...lines, 'Approve?'].join('\n')).then(reply);
  }

  // ReceiveGuildRelationShipResult (GuildRelationshipResultText.cpp).
  private relationshipResult(bytes: Uint8Array): void {
    if (bytes.length < 8) return;
    const relationship = bytes[3], request = bytes[4], result = bytes[5];
    const id = ((bytes[6] & 0x7f) << 8) | bytes[7];
    let message: string | undefined;
    if (result === 1) {
      if (relationship !== 1) message = request === 1 ? 'Hostile guild is connected.' : 'Hostile guild is disconnected.';
      else message = { 1: 'Guild alliance registration is successful.', 2: 'Guild alliance withdrawal is successful.', 0x10: 'Disband of alliance (guild) or request for alliance is not allowed during the siege period' }[request];
    } else message = RELATIONSHIP_FAILED[result];
    if (!message) return;
    this.actions.message(message);
    if (id === (this.actions.heroId() & ID_MASK) && result === 1 && relationship === 1 && request === 2) {
      const mark = this.heroMark;
      if (mark) mark.allianceName = '';
      this.changed('alliances');
    }
  }

  requestAlliances(): void { this.connection.send('ClientToServer', 'RequestAllianceList', {}); }
  removeAlliance(guildName: string): void { this.connection.send('ClientToServer', 'RemoveAllianceGuildRequest', { GuildName: guildName }); }

  /** /union, /rival, /rivaloff and leaving the alliance (SendGuildRelationshipChangeRequest). */
  changeRelationship(relationship: 1 | 2, request: 1 | 2, targetId: number): void {
    this.connection.send('ClientToServer', 'GuildRelationshipChangeRequest', { RelationshipType: relationship, RequestType: request, TargetPlayerId: targetId });
  }

  /** /union, /rival, /rivaloff: with the guild master the hero faces (CheckCommand). */
  relationshipCommand(relationship: 1 | 2, request: 1 | 2, hero: Character, players: Iterable<Character>): void {
    if (this.status === GuildStatus.NONE) { this.actions.message('Do not belong to the guild.'); return; }
    const target = facingPlayer(hero, players);
    if (target) this.changeRelationship(relationship, request, target.id);
  }

  // ReceiveGuildLeave.
  private leaveResult(result: number): void {
    const message = LEAVE_RESULTS[result];
    if (message) this.actions.message(message);
    if (result === 1 || result === 4) {
      const heroId = this.actions.heroId() & ID_MASK;
      if (result === 4 && this.guildId !== null) this.marks.delete(this.guildId);
      this.players.delete(heroId);
      this.status = GuildStatus.NONE;
      this.guildId = null;
      this.memberCount = -1;
      this.members = [];
      this.changed('left');
    } else if (result === 5) {
      this.requestList();
    }
  }

  requestList(): void { this.connection.send('ClientToServer', 'GuildListRequest', {}); }

  /** /guild: ask the guild master the hero faces to let it join (CheckCommand). */
  command(hero: Character, players: Iterable<Character>): void {
    if (this.status !== GuildStatus.NONE) { this.actions.message('You are already in a guild'); return; }
    const master = facingPlayer(hero, players);
    if (!master) return;
    this.requestJoin(master);
  }

  requestJoin(master: Character): void {
    this.connection.send('ClientToServer', 'GuildJoinRequest', { GuildMasterPlayerId: master.id });
    this.actions.message(`You have requested ${master.name} to join your guild.`);
  }

  /** Leaves the guild, disbands it (the master on itself) or releases a member, with the password. */
  kick(name: string, password: string): void {
    this.connection.send('ClientToServer', 'GuildKickPlayerRequest', { PlayerName: name, SecurityCode: password });
  }

  /** Appoints (type 1/2) or dismisses (type 3) a member (SendGuildRoleAssignRequest). */
  assignRole(name: string, role: number, type: number): void {
    this.connection.send('ClientToServer', 'GuildRoleAssignRequest', { Type: type, Role: role, PlayerName: name });
  }

  /** The guild master NPC's question: true opens the creation (SendGuildMasterAnswer). */
  answerMaster(create: boolean): void {
    this.connection.send('ClientToServer', 'GuildMasterAnswer', { ShowCreationDialog: create });
  }

  create(name: string, cells: readonly number[]): void {
    this.connection.send('ClientToServer', 'GuildCreateRequest', { GuildName: name, GuildEmblem: packMark(cells) });
  }

  /** The member list's position of the hero (GetGuildMemberIndex). */
  memberNamed(name: string): GuildMember | undefined { return this.members.find((member) => member.name === name); }
}

export type GuildEvent = 'masterDialog' | 'created' | 'left' | 'members' | 'players' | 'notice' | 'alliances';
