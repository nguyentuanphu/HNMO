// Duels and Gens in the game, wired with one call from main.ts (setUpDuelAndGens):
//   - duels (game/duel.ts, ui/duelWindows.ts): /DuelChallenge or /duelstart on the player next to the hero
//     (the one under the pointer, else the one facing it), the question for the other player, the score
//     window, /DuelCancel or /duelend, the result; Doorkeeper Titus's colosseums and the spectator HUD;
//   - Gens (game/gens.ts, ui/gensWindow.ts): the stewards' answers in the NPC dialogue, the Gens Info
//     window on B, the family mark beside a player's name.

import { Duel, DUEL_WATCH_BUFF, type DuelView } from '../game/duel.js';
import { Gens, GENS_JOIN_STATE, GENS_LEAVE_STATE, GENS_REWARD_STATE } from '../game/gens.js';
import { facingPlayer } from '../game/party.js';
import { DuelDialog, DuelScoreHud, DuelWatchFrame, DuelWatchWindow, SpectatorList } from './duelWindows.js';
import { GensMarks, GensRankingWindow } from './gensWindow.js';
import type * as THREE from 'three';
import type { Connection } from '../net/connection.js';
import type { Character } from '../game/characters.js';
import type { Chat } from '../game/chat.js';
import type { BuffTracker } from '../game/buffs.js';
import type { QuestWindows } from './questWindows.js';

const NPC_WINDOW_DUEL_WATCH = 0x21; // NpcWindowResponse: Doorkeeper Titus (ReceiveNpcWindow case 0x21)

export interface DuelAndGensOptions {
  connection: Connection;
  heroName: string;
  hero: () => Character;
  players: () => Iterable<Character>;
  level: () => number;
  mapNumber: () => number | null;
  chat: Chat;
  buffs: BuffTracker;
  quests: QuestWindows;
  camera: THREE.Camera;
  canvas: HTMLCanvasElement;
  click: () => void;
  sound: (id: string) => void;
  /** IsImpossibleDuelInterface: the mix or the vault window is open (a duel request is refused at once). */
  duelBlocked: () => boolean;
  /** HideAllGroupA: the inventory, character, party and NPC windows close. */
  closeOthers: () => void;
  /** Packets that came before this setup (the hero's gens standing comes on entering). */
  earlyPackets: readonly Uint8Array[];
}

export interface DuelAndGens {
  duel: Duel;
  gens: Gens;
  dialog: DuelDialog;
  score: DuelScoreHud;
  watchList: DuelWatchWindow;
  watchFrame: DuelWatchFrame;
  gensWindow: GensRankingWindow;
  marks: GensMarks;
  /** The player the duel commands use: the one under the pointer next to the hero, else the one facing it. */
  duelTarget: () => Character | null;
}

export function setUpDuelAndGens(options: DuelAndGensOptions): DuelAndGens {
  const { connection, chat } = options;
  const message = (text: string) => chat.add(text, 'system');

  // ---- duels ----
  const duel = new Duel(connection, { id: () => connection.playerId ?? 0, name: options.heroName, level: options.level, map: options.mapNumber });
  const dialog = new DuelDialog(options.click);
  const score = new DuelScoreHud(duel);
  const watchList = new DuelWatchWindow({ duel, click: options.click, closeOthers: options.closeOthers, closeNpc: () => connection.send('ClientToServer', 'CloseNpcRequest', {}) });
  const watchFrame = new DuelWatchFrame(duel, options.click);
  const spectators = new SpectatorList(duel);
  const view: DuelView = {
    message: (text) => chat.add(text, 'system'),
    ask: (caption, lines, cancellable, bold) => dialog.ask(caption, lines, cancellable, bold),
    requestBlocked: options.duelBlocked,
    sound: (which) => options.sound(which === 'open' ? 'SOUND_OPEN_DUELWINDOW' : 'SOUND_START_DUEL'),
    showScore: (show) => score.show(show),
    hideWatchList: () => watchList.hide(),
    changed: () => { score.sync(); watchList.sync(); spectators.sync(); },
  };
  duel.view = view;
  // Doorkeeper Titus opens his list (the server then sends the colosseums every 5 seconds).
  connection.onPacket((bytes) => {
    if (bytes[0] === 0xc3 && bytes[2] === 0x30 && bytes[3] === NPC_WINDOW_DUEL_WATCH) void watchList.open();
  });
  // The duel-watch buff shows the spectator HUD and the spectators (and hides them when it goes).
  const watching = () => options.buffs.has(connection.playerId ?? 0, DUEL_WATCH_BUFF);
  options.buffs.onChange((id) => {
    if (id !== connection.playerId) return;
    watchFrame.show(watching());
    spectators.show(watching());
  });

  // CheckCommand's /DuelChallenge: the player under the pointer (SelectedCharacter) if it is next to the hero;
  // with nobody under the pointer, the player next to the hero that faces it.
  let pointed: Character | null = null;
  const gens = new Gens(connection);
  const marks = new GensMarks({ gens, camera: options.camera, canvas: options.canvas, players: options.players });
  const duelTarget = (): Character | null => {
    const hero = options.hero();
    if (pointed && pointed.group.parent) {
      return pointed !== hero && Math.abs(pointed.cell.x - hero.cell.x) <= 1 && Math.abs(pointed.cell.y - hero.cell.y) <= 1 ? pointed : null;
    }
    return facingPlayer(hero, options.players());
  };
  options.canvas.addEventListener('pointermove', () => { pointed = marks.hoveredPlayer; });

  // The chat commands (CheckCommand): the others still go to the server.
  const send = chat.send;
  chat.send = (text) => {
    const command = text.trim();
    const lower = command.toLowerCase();
    if (command === '/DuelChallenge' || lower === '/duelstart') {
      duel.command(duelTarget());
      return;
    }
    if (command === '/DuelCancel' || lower === '/duelend') { duel.cancelCommand(); return; }
    send?.(text);
  };

  // ---- Gens ----
  options.quests.gensInfluence = () => gens.influence;
  gens.answers = {
    joined: (result) => options.quests.gensAnswered(GENS_JOIN_STATE[result]),
    left: (result) => options.quests.gensAnswered(GENS_LEAVE_STATE[result]),
    reward: (result) => options.quests.gensAnswered(GENS_REWARD_STATE[result]),
  };
  const gensWindow = new GensRankingWindow({
    gens, message, click: options.click, closeOthers: options.closeOthers,
    send: () => connection.send('ClientToServer', 'GensRankingRequest', {}),
  });
  // Players leaving the view take their family with them.
  connection.onPacket((bytes) => {
    if (bytes[0] !== 0xc1 || bytes[2] !== 0x14) return;
    for (let i = 0; i < bytes[3]; i++) gens.forget(((bytes[4 + i * 2] << 8) | bytes[5 + i * 2]) & 0x7fff);
  });

  for (const bytes of options.earlyPackets) { gens.handle(bytes); duel.handle(bytes); }
  return { duel, gens, dialog, score, watchList, watchFrame, gensWindow, marks, duelTarget };
}
