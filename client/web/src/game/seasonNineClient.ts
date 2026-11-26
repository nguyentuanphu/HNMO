// A Season 9 realm's client settings (F5 58 / F5 59): the web game asks once it is in the world of a Season 9
// realm, and the server answers with what its admin panel set (today: whether the Season 9 minimap markers are shown).
// Outside an active Season 9 realm the server doesn't answer and the defaults stay.

import { currentSeason } from '../dataRoot.js';
import { read, type PacketValues } from '../net/packets.js';
import { setSeasonNineMinimapMarkers } from './miniMapData.js';

const SUBCODE_STATE = 0x59;

/** The settings the server sends. */
export interface SeasonNineClientState {
  v: number;
  minimap: boolean;
}

/** Parses the state packet; anything else gives null. */
export function parseSeasonNineClientState(bytes: Uint8Array): SeasonNineClientState | null {
  if (bytes.length < 5 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) return null;
  const parsed = read('ServerToClient', 'HnmoSeasonNineClientState', bytes);
  const json = typeof parsed.Json === 'string' ? parsed.Json : '';
  try {
    const value = JSON.parse(json) as Partial<SeasonNineClientState>;
    return { v: Number(value.v ?? 1), minimap: value.minimap !== false };
  } catch {
    return null;
  }
}

/** Applies a state packet; returns whether it was one. */
export function handleSeasonNineClientState(bytes: Uint8Array): boolean {
  const state = parseSeasonNineClientState(bytes);
  if (!state) return false;
  setSeasonNineMinimapMarkers(state.minimap);
  return true;
}

/** Asks the server for its Season 9 client settings, in a Season 9 realm only. */
export function requestSeasonNineClientState(send: (name: string, fields: PacketValues) => void): void {
  if (currentSeason() === 's9') send('HnmoSeasonNineClientRequest', {});
}
