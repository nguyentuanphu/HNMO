// Duels, as the desktop keeps them (CDuelMgr and WSclient.cpp's ReceiveDuel* handlers, C1 AA xx):
// the hero and the enemy (their ids, names, scores, life and shield shares), whether a duel is on,
// the colosseums Doorkeeper Titus lists and the spectators of a watched duel. The windows (the score
// window, the questions, Titus's list, the spectator HUD) are in ui/duelWindows.ts.

import { packetCode, read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import type { Character } from './characters.js';

export const DUEL_HERO = 0, DUEL_ENEMY = 1;
const DUEL_MIN_LEVEL = 30; // CommandDual / the /DuelChallenge command
/** eBuff_DuelWatch: the server's invisible effect on a spectator, which shows the spectator HUD. */
export const DUEL_WATCH_BUFF = 98;
const ROOM_COUNT = 4;

// Game.en.resx, the lines the desktop shows.
export const DuelText = {
  Challenged: 'You are challenged to a duel.',
  AcceptQuestion: 'Would you like to accept the challenge?',
  Accepted: '%s has accepted your challenge.',
  Declined: '%s has declined your challenge.',
  ColosseumOccupied: 'Colosseum is occupied.',
  TryAgainLater: 'Try it again later',
  LevelTooLow: 'Open only for level %d or higher.',
  ZenInsufficient: 'Zen is insufficient.',
  Canceled: 'The duel has been canceled.',
  FinishedWarp: 'Duel Finished. You will be warped back to the viallage in %d seconds.',
  Finished: 'Duel Finished',
  HasJustWon: '%s has just won',
  TheDuelWith: 'the duel with %s.',
  NotAvailable: 'Not available',
  TooManyPeople: 'Too many people in the colossum.',
  AlreadyInDuel: 'You cannot challenge, player is already in a duel.',
  NotHere: 'Dueling is not possible in this area.',
  NotInLorenMarket: 'You cannot engage in duels while in Loren Market.',
  NotInChaosCastle: "Can't be in Chaos Castle",
  DoorkeeperTitus: 'Doorkeeper Titus',
  SelectColosseum: "Select an Colosseum you'd like to watch.",
  Colosseum: 'Colosseum # %d',
  NoDuelOn: 'No duel on.',
  Watch: 'Watch',
} as const;

/** mu_swprintf with %s / %d. */
export function fill(text: string, ...values: (string | number)[]): string {
  let index = 0;
  return text.replace(/%[sd]/g, () => String(values[index++] ?? ''));
}

export interface DuelPlayer { id: number; name: string; score: number; hp: number; sd: number }
export interface DuelRoom { running: boolean; joinable: boolean; player1: string; player2: string }

/** What the duel logic shows: messages in the chat and the windows (ui/duelWindows.ts). */
export interface DuelView {
  message: (text: string, kind?: 'system' | 'error') => void;
  /** A question (ShowConfirm; caption: the duel art's caption, '' for none; bold: the lines shown bold); resolves with the answer. */
  ask: (caption: string, lines: string[], cancellable: boolean, bold?: number[]) => Promise<boolean>;
  /** IsDuelRequestBlocked (CSystem::IsImpossibleDuelInterface): the mix or the vault window is open. */
  requestBlocked: () => boolean;
  sound: (name: 'open' | 'start') => void;
  showScore: (show: boolean) => void;
  hideWatchList: () => void;
  changed: () => void;
}

interface StartResultPacket { Result: number; OpponentId: number; OpponentName: string }
interface StartRequestPacket { RequesterId: number; RequesterName: string }
interface EndPacket { Result: number; OpponentId: number; OpponentName: string }
interface ScorePacket { Player1Id: number; Player2Id: number; Player1Score: number; Player2Score: number }
interface HealthPacket { Player1Id: number; Player2Id: number; Player1HealthPercentage: number; Player2HealthPercentage: number; Player1ShieldPercentage: number; Player2ShieldPercentage: number }
interface StatusPacket { Rooms: { Player1Name: string; Player2Name: string; DuelRunning: boolean; DuelOpen: boolean }[] }
interface InitPacket { Result: number; RoomIndex: number; Player1Name: string; Player2Name: string; Player1Id: number; Player2Id: number }
interface NamePacket { Name: string }
interface SpectatorListPacket { Count: number; Spectators: { Name: string }[] }
interface FinishedPacket { Winner: string; Loser: string }
interface HealthBarInitPacket { Flag: number }

const emptyPlayer = (): DuelPlayer => ({ id: 0, name: '', score: 0, hp: 0, sd: 0 });

export class Duel {
  enabled = false;
  readonly players: [DuelPlayer, DuelPlayer] = [emptyPlayer(), emptyPlayer()];
  readonly rooms: DuelRoom[] = Array.from({ length: ROOM_COUNT }, () => ({ running: false, joinable: false, player1: '', player2: '' }));
  currentChannel = -1;
  /** SetFighterRegenerated: the spectator gauges start over (DuelHealthBarInit 0). */
  regenerated = false;
  readonly spectators: string[] = [];
  view: DuelView | null = null;

  constructor(private readonly connection: Connection, private readonly hero: { id: () => number; name: string; level: () => number; map: () => number | null }) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  /** Reset(): nothing on, no players, no rooms, no spectators. */
  reset(): void {
    this.enabled = false;
    this.players[0] = emptyPlayer();
    this.players[1] = emptyPlayer();
    for (const room of this.rooms) Object.assign(room, { running: false, joinable: false, player1: '', player2: '' });
    this.currentChannel = -1;
    this.regenerated = false;
    this.spectators.length = 0;
  }

  /** EnableDuel(): switching it off resets everything. */
  private enable(on: boolean): void {
    this.enabled = on;
    if (!on) this.reset();
  }

  private setPlayer(which: number, id: number, name: string): void {
    this.players[which].id = id;
    this.players[which].name = name;
  }

  /** IsDuelPlayer(character, ENEMY): the character fighting the hero. */
  isEnemy(character: Pick<Character, 'id' | 'name'>): boolean {
    return this.enabled && character.id === this.players[DUEL_ENEMY].id && character.name === this.players[DUEL_ENEMY].name;
  }

  // ---- the hero's requests ----

  /**
   * CommandDual (the command window's and the quick menu's Duel): level 30 or more, not on a Doppelganger map
   * or in Loren Market; a duel request when no duel is on, a stop request to the enemy, else "already in a duel".
   */
  challenge(target: Pick<Character, 'id' | 'name'>): void {
    const map = this.hero.map() ?? -1;
    if (this.hero.level() < DUEL_MIN_LEVEL) { this.view?.message(fill(DuelText.LevelTooLow, DUEL_MIN_LEVEL), 'error'); return; }
    if (map >= 65 && map <= 68) { this.view?.message(DuelText.NotHere, 'error'); return; }
    if (map === 79) { this.view?.message(DuelText.NotInLorenMarket, 'error'); return; }
    if (!this.enabled) this.connection.send('ClientToServer', 'DuelStartRequest', { PlayerId: target.id, PlayerName: target.name });
    else if (this.isEnemy(target)) this.stop();
    else this.view?.message(DuelText.AlreadyInDuel, 'error');
  }

  /**
   * CheckCommand's /DuelChallenge or /duelstart: not in Chaos Castle, no duel on, level 30 or more, then a
   * request to the player found next to the hero (target; none sends nothing).
   */
  command(target: Pick<Character, 'id' | 'name'> | null): void {
    const map = this.hero.map() ?? -1;
    if (map >= 18 && map <= 23 || map === 53) { this.view?.message(DuelText.NotInChaosCastle); return; }
    if (this.enabled) { this.view?.message(DuelText.AlreadyInDuel); return; }
    if (this.hero.level() < DUEL_MIN_LEVEL) { this.view?.message(fill(DuelText.LevelTooLow, DUEL_MIN_LEVEL), 'error'); return; }
    if (target) this.connection.send('ClientToServer', 'DuelStartRequest', { PlayerId: target.id, PlayerName: target.name });
  }

  /** /DuelCancel or /duelend: not in Chaos Castle; a stop request while a duel is on. */
  cancelCommand(): void {
    const map = this.hero.map() ?? -1;
    if (map >= 18 && map <= 23 || map === 53) { this.view?.message(DuelText.NotInChaosCastle); return; }
    this.stop();
  }

  /** SendDuelStopRequest while a duel is on. */
  stop(): void {
    if (this.enabled) this.connection.send('ClientToServer', 'DuelStopRequest', {});
  }

  /** Doorkeeper Titus's Watch: joins a running colosseum open to spectators. */
  watch(room: number): void {
    const entry = this.rooms[room];
    if (entry?.running && entry.joinable) this.connection.send('ClientToServer', 'DuelChannelJoinRequest', { ChannelId: room });
  }

  /** The spectator HUD's exit: leaves the watched colosseum. */
  leaveWatch(): void {
    if (this.currentChannel >= 0) this.connection.send('ClientToServer', 'DuelChannelQuitRequest', { ChannelId: this.currentChannel });
  }

  /** SendDuelRequestAnswer: to the challenger as it was when asked (a later request changes DUEL_ENEMY). */
  private answer(accepted: boolean, enemy: { id: number; name: string }): void {
    this.connection.send('ClientToServer', 'DuelStartResponse', { Response: accepted, PlayerId: enemy.id, PlayerName: enemy.name });
  }

  // ---- the server's packets ----

  handle(bytes: Uint8Array): void {
    if (bytes[0] !== 0xc1 && bytes[0] !== 0xc3) return;
    const { code, subCode } = packetCode(bytes);
    if (code !== 0xaa) return;
    switch (subCode) {
      case 0x01: this.started(read<StartResultPacket>('ServerToClient', 'DuelStartResult', bytes)); break;
      case 0x02: this.requested(read<StartRequestPacket>('ServerToClient', 'DuelStartRequest', bytes)); break;
      case 0x03: this.ended(read<EndPacket>('ServerToClient', 'DuelEnd', bytes)); break;
      case 0x04: this.score(read<ScorePacket>('ServerToClient', 'DuelScore', bytes)); break;
      case 0x05: this.health(read<HealthPacket>('ServerToClient', 'DuelHealthUpdate', bytes)); break;
      case 0x06: this.status(read<StatusPacket>('ServerToClient', 'DuelStatus', bytes)); break;
      case 0x07: this.init(read<InitPacket>('ServerToClient', 'DuelInit', bytes)); break;
      case 0x08: this.spectators.push(read<NamePacket>('ServerToClient', 'DuelSpectatorAdded', bytes).Name); break;
      case 0x09: if (bytes[4] === 0) this.spectators.length = 0; break; // DuelChannelQuitResult
      case 0x0a: {
        const name = read<NamePacket>('ServerToClient', 'DuelSpectatorRemoved', bytes).Name;
        const at = this.spectators.indexOf(name);
        if (at >= 0) this.spectators.splice(at, 1);
        break;
      }
      case 0x0b: {
        const list = read<SpectatorListPacket>('ServerToClient', 'DuelSpectatorList', bytes);
        this.spectators.length = 0;
        for (const spectator of list.Spectators.slice(0, list.Count)) this.spectators.push(spectator.Name);
        break;
      }
      case 0x0c: this.finished(read<FinishedPacket>('ServerToClient', 'DuelFinished', bytes)); break;
      case 0x0d: this.round(read<HealthBarInitPacket>('ServerToClient', 'DuelHealthBarInit', bytes)); break;
      default: return;
    }
    this.view?.changed();
  }

  /**
   * ReceiveDuelRequest: the challenger becomes DUEL_ENEMY; with the mix or the vault open the answer is no at
   * once, else the duel art question with "[name]" over it (queued behind an open question, like ShowConfirm).
   */
  private requested(packet: StartRequestPacket): void {
    if (!this.view) return;
    const enemy = { id: packet.RequesterId & 0x7fff, name: packet.RequesterName };
    this.setPlayer(DUEL_ENEMY, enemy.id, enemy.name);
    if (this.view.requestBlocked()) { this.answer(false, enemy); return; }
    void this.view.ask(`[${packet.RequesterName}]`, [DuelText.Challenged, DuelText.AcceptQuestion], true).then((accepted) => this.answer(accepted, enemy));
    this.view.sound('open');
  }

  /** ReceiveDuelStart: the result of the hero's request, or the start of the duel for both. */
  private started(packet: StartResultPacket): void {
    const id = packet.OpponentId & 0x7fff;
    switch (packet.Result) {
      case 0:
        this.enable(true);
        this.setPlayer(DUEL_HERO, this.hero.id(), this.hero.name);
        this.setPlayer(DUEL_ENEMY, id, packet.OpponentName);
        this.view?.message(fill(DuelText.Accepted, packet.OpponentName), 'error');
        this.view?.showScore(true);
        this.view?.sound('start');
        break;
      case 15:
        this.setPlayer(DUEL_ENEMY, id, packet.OpponentName);
        this.view?.message(fill(DuelText.Declined, packet.OpponentName), 'error');
        break;
      case 16:
        void this.view?.ask('', [DuelText.ColosseumOccupied, DuelText.TryAgainLater], false);
        break;
      case 28:
        this.setPlayer(DUEL_ENEMY, id, packet.OpponentName);
        this.view?.message(fill(DuelText.LevelTooLow, DUEL_MIN_LEVEL), 'error');
        break;
      case 30:
        this.setPlayer(DUEL_ENEMY, id, packet.OpponentName);
        this.view?.message(DuelText.ZenInsufficient, 'error');
        break;
      default:
    }
  }

  /** ReceiveDuelEnd: the score window closes, the duel is off. */
  private ended(packet: EndPacket): void {
    if (packet.Result !== 0) return;
    this.view?.showScore(false);
    this.enable(false);
    this.setPlayer(DUEL_ENEMY, packet.OpponentId & 0x7fff, packet.OpponentName);
    this.view?.message(DuelText.Canceled, 'error');
  }

  /** ReceiveDuelScore: each side's points, the hero's first. */
  private score(packet: ScorePacket): void {
    const hero = this.players[DUEL_HERO];
    if ((packet.Player1Id & 0x7fff) === hero.id) { hero.score = packet.Player1Score; this.players[DUEL_ENEMY].score = packet.Player2Score; }
    else if ((packet.Player2Id & 0x7fff) === hero.id) { hero.score = packet.Player2Score; this.players[DUEL_ENEMY].score = packet.Player1Score; }
  }

  /** ReceiveDuelHP: the fighters' life and shield shares (for spectators). */
  private health(packet: HealthPacket): void {
    const [hero, enemy] = this.players;
    if ((packet.Player1Id & 0x7fff) === hero.id) {
      hero.hp = packet.Player1HealthPercentage / 100; enemy.hp = packet.Player2HealthPercentage / 100;
      hero.sd = packet.Player1ShieldPercentage / 100; enemy.sd = packet.Player2ShieldPercentage / 100;
    } else if ((packet.Player2Id & 0x7fff) === hero.id) {
      hero.hp = packet.Player2HealthPercentage / 100; enemy.hp = packet.Player1HealthPercentage / 100;
      hero.sd = packet.Player2ShieldPercentage / 100; enemy.sd = packet.Player1ShieldPercentage / 100;
    }
  }

  /** ReceiveDuelChannelList: the colosseums (Doorkeeper Titus's list). */
  private status(packet: StatusPacket): void {
    packet.Rooms.slice(0, ROOM_COUNT).forEach((room, index) => {
      Object.assign(this.rooms[index], { running: room.DuelRunning, joinable: room.DuelOpen, player1: room.Player1Name, player2: room.Player2Name });
    });
  }

  /** ReceiveDuelWatchRequestReply: the watched channel and its fighters, or why not. */
  private init(packet: InitPacket): void {
    if (packet.Result === 0) {
      this.view?.hideWatchList();
      this.currentChannel = packet.RoomIndex;
      this.setPlayer(DUEL_HERO, packet.Player1Id & 0x7fff, packet.Player1Name);
      this.setPlayer(DUEL_ENEMY, packet.Player2Id & 0x7fff, packet.Player2Name);
    } else if (packet.Result === 16) {
      void this.view?.ask('', [DuelText.ColosseumOccupied, DuelText.TryAgainLater], false);
    } else if (packet.Result === 27) {
      void this.view?.ask('', [DuelText.NotAvailable, DuelText.TooManyPeople], false, [0]);
    }
  }

  /** ReceiveDuelResult: the warp-back line and the result over the duel art. */
  private finished(packet: FinishedPacket): void {
    this.view?.message(fill(DuelText.FinishedWarp, 10));
    void this.view?.ask(DuelText.Finished, [fill(DuelText.HasJustWon, packet.Winner), fill(DuelText.TheDuelWith, packet.Loser), ''], false);
    this.view?.sound('open');
  }

  /** ReceiveDuelRound: a new round fills both fighters' bars. */
  private round(packet: HealthBarInitPacket): void {
    if (packet.Flag !== 0) return;
    for (const player of this.players) { player.hp = 1; player.sd = 1; }
    this.regenerated = true;
  }
}
