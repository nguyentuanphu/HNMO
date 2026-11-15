// The social side of the game in one place, set up once when the hero enters the world:
//  - the guild (game/guild.ts): the guild window (G), the guild master NPC's window, /guild, /union,
//    /rival, /rivaloff, the guild's name and mark over its members' heads (the desktop's name box);
//  - the messenger (game/messenger.ts): the friend window (F, from level 6), friend requests,
//    letters;
//  - whispers: the desktop's whisper box next to the chat line (a name there whispers the line to
//    that player), incoming whispers with their sound, "No users" for a name nobody has;
//    **(extra)** "/w name text" whispers without the box.
// Guild chat ("@text"), alliance chat ("@@text") and party chat ("~text") are typed as on the
// desktop; game/online.ts colours each line by its prefix.

import { Guild, GuildStatus, markCellColor, type GuildMark } from './guild.js';
import { Messenger } from './messenger.js';
import { GuildInfoWindow, GuildMakeWindow } from '../ui/guildWindows.js';
import { FriendWindow } from '../ui/friendWindows.js';
import { audioSystem } from '../audio/audioSystem.js';
import { confirmInGame } from '../ui/confirmDialog.js';
import type { Connection } from '../net/connection.js';
import type { Character } from './characters.js';
import type { Chat } from './chat.js';
import type { MessageKind } from './online.js';

const ID_MASK = 0x7fff;
const FRIEND_LEVEL = 6;
const MAX_NAME = 10;

export interface SocialOptions {
  connection: Connection;
  chat: Chat;
  heroName: string;
  /** The hero's level, leadership and base class (4 is the Dark Lord). */
  hero: () => { level: number; leadership: number; classIndex: number };
  heroCharacter: () => Character;
  players: () => Iterable<Character>;
  /** Packets that came before this was set up (the guild and the messenger come on entering). */
  earlyPackets: readonly Uint8Array[];
  click: () => void;
  /** Opening a docked window closes the others there (the inventory, character and party windows). */
  closeDocked: () => void;
  /** A docked window opened or closed: the inventory moves next to it. */
  dockedChanged: (open: boolean) => void;
  /** The guild window opened or closed (HNMO's guild levels panel goes with it). */
  guildWindowToggled?: (open: boolean) => void;
}

export interface Social { guild: Guild; messenger: Messenger; guildWindow: GuildInfoWindow; guildMakeWindow: GuildMakeWindow; friendWindow: FriendWindow; whisperTo(name: string): void }

const CSS = `
.label .guild-tag { display: flex; align-items: center; justify-content: center; gap: 3px; color: #ffe08a; font-size: 11px; }
.label .guild-tag canvas { width: 12px; height: 12px; image-rendering: pixelated; }
`;

let current: Social | null = null;

/** Sets the social side up; call once after the chat is enabled. */
export function setupSocial(options: SocialOptions): Social {
  const { connection, chat, heroName } = options;
  const style = document.createElement('style');
  style.textContent = CSS;
  document.head.append(style);
  const say = (text: string, kind: MessageKind = 'system') => chat.add(text, kind);
  const sound = (id: string) => audioSystem().play(id);
  const heroId = () => connection.playerId ?? 0;
  const nameOf = (id: number) => [...options.players()].find((c) => c.id === (id & ID_MASK))?.name ?? 'Someone';

  const guild = new Guild(connection, { confirm: confirmInGame, message: say, nameOf, heroId });
  const messenger = new Messenger(connection, { message: say, sound });
  for (const bytes of options.earlyPackets) { guild.handle(bytes); messenger.handle(bytes); }

  const docked = {
    click: options.click,
    onOpen: () => { options.closeDocked(); },
    onToggle: (open: boolean) => options.dockedChanged(open),
  };
  const guildWindow: GuildInfoWindow = new GuildInfoWindow({
    ...docked, guild, heroName, heroId, onMessage: say,
    onOpen: () => { options.closeDocked(); guildMakeWindow.close(); },
    onToggle: (open: boolean) => { options.dockedChanged(open); options.guildWindowToggled?.(open); },
    hero: () => { const h = options.hero(); return { level: h.level, leadership: h.leadership, darkLord: h.classIndex === 4 }; },
  });
  const guildMakeWindow: GuildMakeWindow = new GuildMakeWindow({ ...docked, guild, onOpen: () => { options.closeDocked(); guildWindow.close(); } });

  // Whispers: the input box's whisper box (game/chat.ts) sends a line to the name in it.
  let lastWhisperTarget = '';
  const whisper = (name: string, text: string) => {
    lastWhisperTarget = name;
    connection.send('ClientToServer', 'WhisperMessage', { ReceiverName: name, Message: text });
    chat.add(text.replace(/^(~|@@|@|\$)/, ''), 'whisper', heroName);
  };
  chat.whisper = whisper;
  const whisperTo = (name: string) => {
    chat.setWhisperTarget(name);
    chat.openInput();
  };

  const friendWindow = new FriendWindow({ messenger, heroName, click: options.click, chatWith: whisperTo });

  const whispers = (bytes: Uint8Array) => {
    if (bytes[0] !== 0xc1) return;
    if (bytes[2] === 0x02 && bytes.length > 13) {
      // ReceiveChatWhisper (nothing while whispers are blocked)
      if (chat.blockWhisper) return;
      const decoder = new TextDecoder();
      const cut = (from: number, to: number) => { const raw = bytes.subarray(from, to); const end = raw.indexOf(0); return decoder.decode(end < 0 ? raw : raw.subarray(0, end)); };
      sound('SOUND_WHISPER');
      chat.add(cut(13, bytes.length), 'whisper', cut(3, 13));
    } else if (bytes[2] === 0x0c && bytes.length >= 4 && bytes[3] === 0) {
      // ReceiveChatWhisperResult
      say(`${lastWhisperTarget}: No users`, 'error');
    }
  };
  // Whispers that came while the map loaded (offline whispers arrive right after entering the game).
  for (const bytes of options.earlyPackets) whispers(bytes);
  connection.onPacket(whispers);

  // Commands of the chat line, before the others (CheckCommand); a name in the whisper box whispers.
  const send = chat.send;
  chat.send = (text: string) => {
    const command = text.trim().toLowerCase();
    const hero = options.heroCharacter();
    if (command === '/guild') { guild.command(hero, options.players()); return; }
    if (command === '/union' || command === '/alliance') { guild.relationshipCommand(1, 1, hero, options.players()); return; }
    if (command === '/rival') { guild.relationshipCommand(2, 1, hero, options.players()); return; }
    if (command === '/rivaloff') { guild.relationshipCommand(2, 2, hero, options.players()); return; }
    const w = /^\/w\s+(\S+)\s+(.+)$/i.exec(text.trim());
    if (w) { whisper(w[1].slice(0, MAX_NAME), w[2]); return; }
    send?.(text);
  };

  // G: the guild window; F: the friend window from level 6 (CHotKey).
  window.addEventListener('keydown', (event) => {
    if (event.ctrlKey || event.altKey || event.metaKey || event.repeat) return;
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    const key = event.key.toLowerCase();
    if (key === 'g') { event.preventDefault(); void guildWindow.toggle(); }
    else if (key === 'f') {
      event.preventDefault();
      if (options.hero().level < FRIEND_LEVEL) { say("You must be at least level 6 to use the 'My Friend' function."); options.click(); return; }
      void friendWindow.toggle();
    }
  });

  current = { guild, messenger, guildWindow, guildMakeWindow, friendWindow, whisperTo };
  return current;
}

// ---------- the guild over the head (the name box's guild line, Chat.cpp's AddGuildName) ----------

const ROLE_NAMES: Record<number, string> = { [GuildStatus.PERSON]: 'Members', [GuildStatus.MASTER]: 'Master', [GuildStatus.SUB_MASTER]: 'Assist. M.', [GuildStatus.BATTLE_MASTER]: 'Battle M.' };

function markCanvas(mark: GuildMark): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 8;
  const context = canvas.getContext('2d');
  if (context) {
    mark.cells.forEach((cell, index) => {
      if (cell === 0) return;
      context.fillStyle = markCellColor(cell);
      context.fillRect(index % 8, Math.floor(index / 8), 1, 1);
    });
  }
  return canvas;
}

/** Puts a player's guild ("[Name] Members" and the mark) above its name label; called each frame. */
export function decorateLabel(character: Character, element: HTMLElement): void {
  if (!current || character.kind !== 'player') { if (element.textContent !== character.name) element.textContent = character.name; return; }
  const found = current.guild.guildOf(character.id);
  const text = found ? `[${found.mark.name}] ${ROLE_NAMES[found.player.status] ?? ''}`.trim() : '';
  const alliance = found?.mark.allianceName ? `<${found.mark.allianceName}>` : '';
  // The element may show another player next (the name box over the pointed player): the name is in the key.
  const key = found ? `${character.name}|${text}|${alliance}|${found.mark.cells.join('')}` : character.name;
  if (element.dataset.guild === key) return;
  element.dataset.guild = key;
  if (!found) { element.textContent = character.name; return; }
  const tag = document.createElement('span');
  tag.className = 'guild-tag';
  const words = document.createElement('span');
  words.textContent = alliance ? `${alliance} ${text}` : text;
  tag.append(markCanvas(found.mark), words);
  const name = document.createElement('span');
  name.textContent = character.name;
  element.replaceChildren(tag, name);
}
