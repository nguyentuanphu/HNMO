// The messenger, as the desktop keeps it (WSclient.cpp's ReceiveFriendList, ReceiveAddFriendResult,
// ReceiveRequestAcceptAddFriend, ReceiveDeleteFriendResult, ReceiveFriendStateChange,
// ReceiveLetterSendResult, ReceiveLetter, ReceiveLetterText, ReceiveLetterDeleteResult; CFriendList
// and CLetterList in SocialWindowManager.cpp): the friend list with each friend's server, friend
// requests, the letter box, reading, sending and deleting letters.

import type { Connection } from '../net/connection.js';

/** A friend's server: 0.. a game server, 0xFC the friend server is gone, 0xFD cannot use, 0xFE/0xFF offline. */
export const LAST_ONLINE_SERVER = 0xfc;
export const UNAVAILABLE_SERVER = 0xfd;

export interface Friend { name: string; server: number }

export interface Letter {
  id: number;
  sender: string;
  subject: string;
  /** "yyyy-MM-dd" and "HH:mm:ss" (the desktop splits the server's timestamp in two). */
  date: string;
  time: string;
  read: boolean;
}

export interface LetterBody { id: number; text: string; rotation: number; animation: number; appearance: Uint8Array }

/** What the letter costs to send (UI::Social::LetterCost, the server's LetterSendPrice). */
export const LETTER_COST = 1000;
const MAX_LETTER_TEXT = 1000;
const MAX_SUBJECT_LENGTH = 32;

export type MessengerEvent =
  | { kind: 'friends' }
  | { kind: 'letters' }
  | { kind: 'letterBody'; body: LetterBody }
  | { kind: 'letterSent'; windowId: number }
  | { kind: 'letterFailed'; windowId: number; notice: string }
  | { kind: 'notice'; text: string }
  | { kind: 'friendRequest'; name: string }
  | { kind: 'newLetter' };

const decoder = new TextDecoder();
function text(bytes: Uint8Array, at: number, length: number): string {
  const raw = bytes.subarray(at, Math.min(bytes.length, at + length));
  const end = raw.indexOf(0);
  return decoder.decode(end < 0 ? raw : raw.subarray(0, end));
}

// TestAlphabeticOrder: a > b by the first differing character (equal prefixes: not before).
function after(a: string, b: string): boolean {
  const length = Math.min(a.length, b.length);
  for (let i = 0; i < length; i++) {
    if (a[i] === b[i]) continue;
    return a[i] > b[i];
  }
  return false;
}
const order = <T>(first: (a: T, b: T) => boolean) => (a: T, b: T) => (first(a, b) ? -1 : first(b, a) ? 1 : 0);

export class Messenger {
  friends: Friend[] = [];
  letters: Letter[] = [];
  maxLetters = 0;
  /** CFriendList's sort: 0 by name, 1 by server. */
  friendSort = 0;
  /** CLetterList's sort: 0 read first, 1 sender, 2 newest first, 3 subject. */
  letterSort = 2;
  /** Whether the friend server answers (the friend list came). */
  serverEnabled = false;
  /** Set when a letter arrives; the friend window then opens on the letter box. */
  newMailAlert = false;
  readonly bodies = new Map<number, LetterBody>();
  private readonly listeners = new Set<(event: MessengerEvent) => void>();

  constructor(private readonly connection: Connection, private readonly actions: {
    message: (text: string) => void;
    sound: (id: string) => void;
  }) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: (event: MessengerEvent) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private emit(event: MessengerEvent): void { for (const listener of this.listeners) listener(event); }

  handle(bytes: Uint8Array): void {
    const type = bytes[0];
    if (type === 0xc2 && bytes[3] === 0xc0) { this.friendList(bytes); return; }
    if (type === 0xc3 && bytes[2] === 0xc6) { this.letterListed(bytes); return; }
    if (type === 0xc4 && bytes[3] === 0xc7) { this.letterText(bytes); return; }
    if (type !== 0xc1) return;
    switch (bytes[2]) {
      case 0xc1: this.addFriendResult(bytes); break;
      case 0xc2: { // ReceiveRequestAcceptAddFriend
        const name = text(bytes, 3, 10);
        this.actions.sound('SOUND_FRIEND_LOGIN_ALERT');
        this.emit({ kind: 'friendRequest', name });
        break;
      }
      case 0xc3: { // ReceiveDeleteFriendResult
        const name = text(bytes, 4, 10);
        if (bytes[3] === 1) { this.friends = this.friends.filter((f) => f.name !== name); this.emit({ kind: 'friends' }); } else this.emit({ kind: 'notice', text: "Couldn't delete." });
        break;
      }
      case 0xc4: { // ReceiveFriendStateChange
        const name = text(bytes, 3, 10);
        const server = bytes[13];
        if (server === LAST_ONLINE_SERVER) {
          for (const friend of this.friends) friend.server = server;
          this.serverEnabled = false;
        } else {
          const friend = this.friends.find((f) => f.name === name);
          if (friend) friend.server = server;
        }
        this.sortFriends();
        this.emit({ kind: 'friends' });
        break;
      }
      case 0xc5: this.letterSendResult(bytes); break;
      case 0xc8: { // ReceiveLetterDeleteResult
        const id = bytes[4] | (bytes[5] << 8);
        if (bytes[3] === 1) {
          this.letters = this.letters.filter((l) => l.id !== id);
          this.bodies.delete(id);
          this.emit({ kind: 'letters' });
        } else this.emit({ kind: 'notice', text: "Couldn't delete letter." });
        break;
      }
      default:
    }
  }

  // ReceiveFriendList (UI::Social::ReplaceFriendList).
  private friendList(bytes: Uint8Array): void {
    const letterCount = bytes[4];
    this.maxLetters = bytes[5];
    const count = bytes[6];
    const friends: Friend[] = [];
    for (let i = 0; i < count; i++) {
      const at = 7 + i * 11;
      if (at + 11 > bytes.length) break;
      friends.push({ name: text(bytes, at, 10), server: bytes[at + 10] });
    }
    this.friends = friends;
    this.sortFriends(0);
    this.sortFriends(1);
    this.serverEnabled = true;
    if (letterCount > 0) this.actions.message(`${letterCount} letters are saved in your mailbox (Max: ${this.maxLetters})`);
    this.emit({ kind: 'friends' });
  }

  private addFriendResult(bytes: Uint8Array): void {
    const result = bytes[3];
    const name = text(bytes, 4, 10);
    switch (result) {
      case 0x00: this.emit({ kind: 'notice', text: `${name}ID does not exist.` }); break;
      case 0x01:
        this.actions.message("The friend's status will be displayed as [Offline] until both parties are registered as friends");
        if (!this.friends.some((f) => f.name === name)) this.friends.push({ name, server: bytes[14] ?? 0xff });
        this.sortFriends();
        this.emit({ kind: 'friends' });
        break;
      case 0x03: this.emit({ kind: 'notice', text: 'You cannot add more. Please delete to add.' }); break;
      case 0x04: this.emit({ kind: 'notice', text: `${name}is already registered.` }); break;
      case 0x05: this.emit({ kind: 'notice', text: 'You cannot register your own ID.' }); break;
      case 0x06: this.emit({ kind: 'notice', text: 'The other character must be over level 6.' }); break;
      default:
    }
  }

  private letterSendResult(bytes: Uint8Array): void {
    const windowId = (bytes[4] | (bytes[5] << 8) | (bytes[6] << 16) | (bytes[7] << 24)) >>> 0;
    const failed: Record<number, string> = {
      0: 'The letter could not be sent. Please try again.',
      2: "The letter can't be sent because the receiver's mail box is full.",
      3: 'Either the receiver does not exist or there is no mail box.',
      4: 'You cannot send a letter to yourself.',
      6: 'The other character must be over level 6.',
      7: 'You are short of Zen.',
    };
    if (bytes[3] === 1) {
      this.emit({ kind: 'letterSent', windowId });
      this.actions.message(`Letter has been sent (cost: ${LETTER_COST} zen)`);
    } else if (failed[bytes[3]]) {
      this.emit({ kind: 'letterFailed', windowId, notice: failed[bytes[3]] });
    }
  }

  // ReceiveLetter: a letter of the box (state 0/1) or one just arrived (2).
  private letterListed(bytes: Uint8Array): void {
    if (bytes.length < 107) return;
    const id = bytes[4] | (bytes[5] << 8);
    const stamp = text(bytes, 16, 30);
    const state = bytes[106];
    if (this.letters.some((l) => l.id === id)) return;
    const letter: Letter = {
      id, sender: text(bytes, 6, 10), subject: [...text(bytes, 46, 60)].slice(0, MAX_SUBJECT_LENGTH).join(''),
      date: stamp.slice(0, 10), time: stamp.slice(11, 19), read: state === 1,
    };
    if (state === 2) {
      // A new letter: unread, the box sorted by the current order, the alert sound.
      letter.read = false;
      this.actions.sound('SOUND_FRIEND_MAIL_ALERT');
      this.actions.message('New mail has arrived.');
      this.newMailAlert = true;
      this.letters.push(letter);
      this.sortLetters();
      this.emit({ kind: 'newLetter' });
    } else if (state <= 1) {
      this.letters.push(letter);
      this.sortLetters(2);
    }
    if (this.letters.length >= this.maxLetters && this.maxLetters > 0) this.actions.message('Your mailbox is full. You must delete letters to receive new ones.');
    this.emit({ kind: 'letters' });
  }

  // ReceiveLetterText (OpenLetterExtended): the text, the sender's pose and appearance.
  private letterText(bytes: Uint8Array): void {
    if (bytes.length < 50) return;
    const id = bytes[4] | (bytes[5] << 8);
    const body: LetterBody = {
      id, appearance: bytes.slice(6, 48), rotation: bytes[48], animation: bytes[49],
      text: [...text(bytes, 50, bytes.length - 50)].slice(0, MAX_LETTER_TEXT).join(''),
    };
    this.bodies.set(id, body);
    const letter = this.letters.find((l) => l.id === id);
    if (letter) letter.read = true;
    this.emit({ kind: 'letters' });
    this.emit({ kind: 'letterBody', body });
  }

  sortFriends(type = -1): void {
    if (type !== -1) this.friendSort = type;
    if (this.friendSort === 0) this.friends.sort(order<Friend>((a, b) => after(a.name, b.name)));
    else if (this.friendSort === 1) this.friends.sort(order<Friend>((a, b) => a.server > b.server));
  }

  sortLetters(type = -1): void {
    if (type !== -1) this.letterSort = type;
    const sorts: ((a: Letter, b: Letter) => boolean)[] = [
      (a, b) => a.read && !b.read,
      (a, b) => after(a.sender, b.sender),
      (a, b) => (a.date === b.date ? after(b.time, a.time) : after(b.date, a.date)),
      (a, b) => after(a.subject, b.subject),
    ];
    const first = sorts[this.letterSort];
    if (first) this.letters.sort(order(first));
  }

  requestList(): void { this.connection.send('ClientToServer', 'FriendListRequest', {}); }
  addFriend(name: string): void { this.connection.send('ClientToServer', 'FriendAddRequest', { FriendName: name }); }
  answerFriend(name: string, accepted: boolean): void { this.connection.send('ClientToServer', 'FriendAddResponse', { Accepted: accepted, FriendRequesterName: name }); }
  deleteFriend(name: string): void { this.connection.send('ClientToServer', 'FriendDelete', { FriendName: name }); }
  /** Refuse chat (0) or accept it again (1) (SendSetFriendOnlineState). */
  setOnlineState(online: boolean): void { this.connection.send('ClientToServer', 'SetFriendOnlineState', { OnlineState: online }); }
  readLetter(id: number): void { this.connection.send('ClientToServer', 'LetterReadRequest', { LetterIndex: id }); }
  deleteLetter(id: number): void { this.connection.send('ClientToServer', 'LetterDeleteRequest', { LetterIndex: id }); }

  /** SendLetterSendRequest: the write window's id comes back with the result. */
  sendLetter(windowId: number, receiver: string, subject: string, message: string, rotation: number, animation: number): void {
    const length = new TextEncoder().encode(message).length;
    this.connection.send('ClientToServer', 'LetterSendRequest', {
      LetterId: windowId, Receiver: receiver, Title: subject, Rotation: rotation, Animation: animation, MessageLength: length, Message: message,
    });
  }

  /** The neighbouring letter in the box's order (GetPrevLetterID / GetNextLetterID), -1 without one. */
  neighbour(id: number, direction: -1 | 1): number {
    const index = this.letters.findIndex((l) => l.id === id);
    if (index < 0) return -1;
    const target = direction < 0 ? index + 1 : index - 1;
    return this.letters[target]?.id ?? -1;
  }
}
