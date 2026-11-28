// Diagnostics for the social spec (social.spec.ts): what each of the two pages' game sockets did, with UTC times, so a
// failure like "the /move had no effect" shows where it stopped: the socket closed, the ping went unanswered, the
// server never sent MapChanged. The spec attaches the events to its report ('social-diagnostics').
import type { Page } from '@playwright/test';

export interface SocialEvent {
  utc: string;
  player: string;
  kind: string;
  [detail: string]: unknown;
}

export interface SocialWatch {
  events: SocialEvent[];
  mark(kind: string, detail?: Record<string, unknown>): void;
}

type FrameDescription = { packet: string; detail?: Record<string, unknown> };

/** Names the few packets that matter here from a game frame (C1 and C3 headers are 2 bytes, C2 and C4 are 3). */
function describeFrame(direction: 'sent' | 'received', payload: Buffer): FrameDescription | null {
  const header = payload[0] === 0xc2 || payload[0] === 0xc4 ? 3 : 2;
  const code = payload[header];
  const sub = payload[header + 1];
  const isC1 = payload[0] === 0xc1;
  if (direction === 'sent') {
    if (code === 0x00 && payload.subarray(13, 19).toString() === '/move ') return { packet: 'chat /move' };
    if (code === 0xf3 && sub === 0x12) return { packet: 'ready after map change' };
    if (isC1 && code === 0x71) return { packet: 'ping response' };
    return null;
  }
  if (isC1 && code === 0x71) return { packet: 'ping request' };
  if (payload[0] === 0xc3 && payload.length >= 9 && code === 0x1c && sub === 0x0f) {
    return { packet: 'map changed', detail: { map: payload.readUInt16BE(5), x: payload[7], y: payload[8] } };
  }
  return null;
}

/** Records the page's game sockets from now on: call it before the page opens one. */
export function watchSocial(page: Page, player: 'A' | 'B'): SocialWatch {
  const events: SocialEvent[] = [];
  const mark = (kind: string, detail: Record<string, unknown> = {}) => { events.push({ utc: new Date().toISOString(), player, kind, ...detail }); };
  let sockets = 0;
  page.on('websocket', (socket) => {
    const socketId = ++sockets;
    mark('socket opened', { socketId });
    socket.on('close', () => mark('socket closed', { socketId }));
    socket.on('socketerror', () => mark('socket error', { socketId }));
    const frame = (direction: 'sent' | 'received') => ({ payload }: { payload: string | Buffer }) => {
      if (typeof payload === 'string') return;
      const found = describeFrame(direction, payload);
      if (found) mark(direction, { socketId, packet: found.packet, ...found.detail });
    };
    socket.on('framesent', frame('sent'));
    socket.on('framereceived', frame('received'));
  });
  return { events, mark };
}
