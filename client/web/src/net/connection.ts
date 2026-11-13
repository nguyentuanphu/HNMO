// A connection to a game server through the WebSocket relay (web/bridge).
// Each WebSocket message carries one whole, unencrypted packet; the relay
// encrypts and decrypts like the desktop client's ClientLibrary.

import { read, write, packetCode } from './packets.js';
import type { PacketValues } from './packets.js';

/** Where the relay connects to. */
export interface ServerAddress {
  host: string;
  port: number;
}

export interface ConnectionTarget extends ServerAddress {
  /** Whether the relay encrypts the traffic (game servers) or not (connect server). */
  encrypted: boolean;
}

export type PacketHandler = (bytes: Uint8Array) => void;

interface Waiter {
  matches: (bytes: Uint8Array) => boolean;
  resolve: (bytes: Uint8Array) => void;
  reject: (error: Error) => void;
  timer?: ReturnType<typeof setTimeout>;
}

const MAX_BACKLOG = 64;

export class Connection {
  url: string;
  handlers: Set<PacketHandler>;
  waiters: Waiter[];
  backlog: Uint8Array[];
  closed: boolean;
  onClose: (() => void) | null;
  socket?: WebSocket;
  /** The player's object id, set by login (session.js). */
  playerId?: number;

  constructor(relayUrl: string, { host, port, encrypted }: ConnectionTarget) {
    this.url = `${relayUrl}?host=${encodeURIComponent(host)}&port=${port}&encrypted=${encrypted ? 1 : 0}`;
    this.handlers = new Set();
    this.waiters = [];
    this.backlog = []; // packets no waiter took yet, for a waiter that starts a moment later
    this.closed = false;
    this.onClose = null;
  }

  open(): Promise<this> {
    return new Promise((resolve, reject) => {
      const socket = new WebSocket(this.url);
      socket.binaryType = 'arraybuffer';
      socket.onopen = () => resolve(this);
      socket.onerror = () => reject(new Error(`Couldn't reach the relay at ${this.url.split('?')[0]}`));
      socket.onmessage = (event) => this.dispatch(new Uint8Array(event.data));
      socket.onclose = () => {
        this.closed = true;
        for (const waiter of this.waiters) waiter.reject(new Error('The server closed the connection.'));
        this.waiters = [];
        this.onClose?.();
      };
      this.socket = socket;
    });
  }

  dispatch(bytes: Uint8Array): void {
    // One handler's error mustn't keep the packet from the others.
    for (const handler of this.handlers) {
      try { handler(bytes); } catch (error) { console.error('Packet handler failed', bytes[0].toString(16), bytes[2].toString(16), error); }
    }
    const waiter = this.waiters.find((w) => w.matches(bytes));
    if (!waiter) {
      this.backlog.push(bytes);
      if (this.backlog.length > MAX_BACKLOG) this.backlog.shift();
      return;
    }
    clearTimeout(waiter.timer);
    this.waiters = this.waiters.filter((w) => w !== waiter);
    waiter.resolve(bytes);
  }

  onPacket(handler: PacketHandler): () => boolean {
    this.handlers.add(handler);
    return () => this.handlers.delete(handler);
  }

  // Waits for the next packet with this code (and sub code, if given).
  waitFor(code: number, subCode?: number, timeoutMs = 10000): Promise<Uint8Array> {
    return new Promise((resolve, reject) => {
      const matches = (bytes: Uint8Array) => {
        const header = packetCode(bytes);
        return header.code === code && (subCode === undefined || header.subCode === subCode);
      };
      const early = this.backlog.findIndex(matches);
      if (early >= 0) {
        resolve(this.backlog.splice(early, 1)[0]);
        return;
      }
      const waiter: Waiter = { matches, resolve, reject };
      waiter.timer = setTimeout(() => {
        this.waiters = this.waiters.filter((w) => w !== waiter);
        reject(new Error(`No answer from the server (packet ${code.toString(16)}${subCode !== undefined ? ` ${subCode.toString(16)}` : ''}).`));
      }, timeoutMs);
      this.waiters.push(waiter);
    });
  }

  send(group: string, name: string, values?: PacketValues): void {
    if (this.closed) return;
    this.socket!.send(write(group, name, values));
  }

  /** Sends a packet built by hand (for structures the writer doesn't build, e.g. AreaSkillHit's targets). */
  sendBytes(bytes: Uint8Array): void {
    if (this.closed) return;
    this.socket!.send(bytes);
  }

  // T is the shape the caller expects of the answer (see read in packets.js).
  async request<T extends object = PacketValues>(group: string, name: string, values: PacketValues, answerGroup: string, answerName: string): Promise<T> {
    const { PACKETS } = await import('./packets.generated.js');
    const answer = PACKETS[answerGroup].packets[answerName];
    const waiting = this.waitFor(answer.code, answer.hasSubCode ? answer.subCode : undefined);
    this.send(group, name, values);
    return read<T>(answerGroup, answerName, await waiting);
  }

  close(): void {
    this.closed = true;
    this.socket?.close();
  }
}
