import type { BrowserContext, Page } from '@playwright/test';

type EventDetails = Record<string, unknown>;
type Event = EventDetails & { utc: string; kind: string; player: string };

interface SocialState {
  mapNumber: number | null;
  inventory?: { money: number };
  stats?: { level: number; heroState: number };
  connection?: { closed: boolean; socket?: WebSocket };
  online?: { hero: { cell: { x: number; y: number } }; say(text: string): void };
  social?: { guild: { status: number } };
}

type FocusSample = { utc: string; visibility: DocumentVisibilityState; focus: boolean };
type SocialWindow = Window & { __game?: SocialState; __socialFocus?: FocusSample[] };

export function observeSocialFocus(context: BrowserContext): Promise<void> {
  return context.addInitScript((softwareRender) => {
    if (window !== window.top) return;
    if (softwareRender && (location.protocol === 'http:' || location.protocol === 'https:')) {
      localStorage.setItem('hnmo.settings', JSON.stringify({ quality: { renderScale: 0.5, fpsCap: 30 } }));
    }
    const samples: FocusSample[] = [];
    (window as SocialWindow).__socialFocus = samples;
    const sample = () => {
      samples.push({ utc: new Date().toISOString(), visibility: document.visibilityState, focus: document.hasFocus() });
      if (samples.length > 40) samples.shift();
    };
    document.addEventListener('visibilitychange', sample);
    window.addEventListener('focus', sample);
    window.addEventListener('blur', sample);
    sample();
  }, process.env.HNMO_SOCIAL_SOFTWARE_RENDER === '1');
}

export function watchSocial(page: Page, player: 'A' | 'B') {
  const events: Event[] = [];
  const mark = (kind: string, detail: EventDetails = {}) => events.push({ utc: new Date().toISOString(), player, kind, ...detail });
  let sockets = 0;
  page.on('websocket', (socket) => {
    const socketId = ++sockets;
    mark('socket-opened', { socketId });
    socket.on('close', () => mark('socket-closed', { socketId }));
    socket.on('socketerror', () => mark('socket-error', { socketId }));
    const frame = (direction: 'sent' | 'received', payload: string | Buffer) => {
      if (typeof payload === 'string') return;
      const index = payload[0] === 0xc2 || payload[0] === 0xc4 ? 3 : 2;
      const code = payload[index], sub = payload[index + 1];
      let packet: string | undefined;
      if (direction === 'sent') {
        if (code === 0x00 && payload.subarray(13, 19).toString() === '/move ') packet = 'PublicChatMessage /move';
        if (code === 0xf3 && sub === 0x12) packet = 'ClientReadyAfterMapChange';
        if (payload[0] === 0xc3 && code === 0x0e && sub === 0) packet = 'PeriodicPing';
        if (payload[0] === 0xc1 && code === 0x71) packet = 'PingResponse';
      } else {
        if (payload[0] === 0xc3 && payload.length >= 9 && code === 0x1c && sub === 0x0f) packet = 'MapChanged';
        if (payload[0] === 0xc1 && code === 0x71) packet = 'PingRequest';
        if (code === 0x0d) packet = 'ServerMessage';
        if (payload[0] === 0xc1 && payload.length === 5 && code === 0x56) packet = 'GuildCreationResult';
      }
      if (!packet) return;
      const details = packet === 'MapChanged' ? { map: payload.readUInt16BE(5), x: payload[7], y: payload[8], isMapChange: (payload[4] & 1) !== 0 }
        : packet === 'GuildCreationResult' ? { success: payload[3] !== 0, result: payload[4] }
        : {};
      mark(direction, { socketId, packet, code, ...details });
    };
    socket.on('framesent', ({ payload }) => frame('sent', payload));
    socket.on('framereceived', ({ payload }) => frame('received', payload));
  });

  const snapshot = (destination?: 'devias' | 'lorencia') => page.evaluate((map) => {
    const { __game: game, __socialFocus: focusHistory } = window as SocialWindow;
    const state = {
      utc: new Date().toISOString(),
      map: game?.mapNumber,
      cell: game?.online?.hero.cell,
      zen: game?.inventory?.money,
      level: game?.stats?.level,
      pk: game?.stats?.heroState,
      guildStatus: game?.social?.guild.status,
      closed: game?.connection?.closed,
      readyState: game?.connection?.socket?.readyState,
      visibility: document.visibilityState,
      focus: document.hasFocus(),
      focusHistory,
    };
    if (map) game!.online!.say('/move ' + map);
    return state;
  }, destination);
  return { events, mark, snapshot };
}

export async function sendSocialWarp(watch: ReturnType<typeof watchSocial>, destination: 'devias' | 'lorencia') {
  const state = await watch.snapshot(destination);
  watch.mark('before-warp', { utc: state.utc, destination, state });
}
