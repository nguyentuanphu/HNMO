// The full-screen mini map's data (CMiniMap::LoadImages): the world folder whose mini_map texture it
// shows, and the markers of Data/Local/Eng/Minimap/Minimap_WorldN_eng.bmd — 100 records of 116 bytes
// (Kind, 3 bytes padding, Location[2], Rotation as int32, Name[100] in UTF-8), each xor'ed with the
// BUX code, then 45 unused bytes and a GenerateCheckSum2 (key 0x2BC1) of everything before it.

import { fetchBytes } from '../bmd.js';
import { filesFolder } from '../world.js';

/** A marker: Kind 1 an NPC, Kind 2 a gate; location in cells (Location[0] is the cell's x). */
export interface MiniMapMarker {
  kind: number;
  location: [number, number];
  rotation: number;
  name: string;
}

export interface MiniMapData {
  /** The world folder, e.g. "World1". */
  worldName: string;
  /** The world's mini_map picture (Data/WorldN/mini_map.ozt, served as .tga). */
  pictureUrl: string;
  markers: MiniMapMarker[];
}

const MAX_MINI_MAP_DATA = 100;
const RECORD = 116;
const TRAILER = 45;
const CHECKSUM_KEY = 0x2bc1;
const BUX = [0xfc, 0xcf, 0xab];
const LANGUAGE = 'Eng';
// MapManager: these worlds keep the mini map of the world before them.
const KEEPS_PREVIOUS = new Set([74, 75]);

/** GenerateCheckSum2 (ZzzInfomation.h). */
export function generateCheckSum2(buffer: Uint8Array, size: number, key: number): number {
  const view = new DataView(buffer.buffer, buffer.byteOffset, buffer.byteLength);
  let result = (key << 9) >>> 0;
  for (let checked = 0; checked <= size - 4; checked += 4) {
    const value = view.getUint32(checked, true);
    if (((checked / 4 + key) % 2) === 0) result = (result ^ value) >>> 0;
    else result = (result + value) >>> 0;
    if (checked % 16 === 0) result = (result ^ (((key + result) >>> 0) >>> (((checked / 4) % 8) + 1))) >>> 0;
  }
  return result;
}

/** The marker records of a Minimap_*.bmd file; empty when it is short or its checksum is wrong. */
export function parseMiniMapMarkers(bytes: Uint8Array): MiniMapMarker[] {
  const size = RECORD * MAX_MINI_MAP_DATA + TRAILER;
  if (bytes.length < size + 4) return [];
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  if (view.getUint32(size, true) !== generateCheckSum2(bytes, size, CHECKSUM_KEY)) {
    console.warn('Mini map markers: file corrupted.');
    return [];
  }
  const decoder = new TextDecoder();
  const markers: MiniMapMarker[] = [];
  for (let i = 0; i < MAX_MINI_MAP_DATA; i++) {
    const record = new Uint8Array(RECORD);
    for (let j = 0; j < RECORD; j++) record[j] = bytes[i * RECORD + j] ^ BUX[j % 3];
    const r = new DataView(record.buffer);
    const name = record.subarray(16, RECORD);
    const end = name.indexOf(0);
    markers.push({
      kind: record[0],
      location: [r.getInt32(4, true), r.getInt32(8, true)],
      rotation: r.getInt32(12, true),
      name: decoder.decode(end < 0 ? name : name.subarray(0, end)),
    });
  }
  return markers;
}

const cache = new Map<number, Promise<MiniMapData | null>>();
let previous: Promise<MiniMapData | null> = Promise.resolve(null);

async function loadFolder(folder: number): Promise<MiniMapData | null> {
  const worldName = `World${folder}`;
  // LoadImages(): a world without a mini_map picture has no mini map (the view finds that out
  // when the picture does not load).
  const pictureUrl = `/data/${worldName}/mini_map.tga`;
  const lang = LANGUAGE.toLowerCase();
  const bytes = await fetchBytes(`/data/Local/${LANGUAGE}/Minimap/Minimap_${worldName}_${lang}.bmd`).catch(() => null);
  return { worldName, pictureUrl, markers: bytes ? parseMiniMapMarkers(bytes) : [] };
}

/** The mini map data of a map number (its world folder, picture URL and markers). */
export function loadMiniMapData(mapNumber: number): Promise<MiniMapData | null> {
  const folder = filesFolder(mapNumber);
  if (KEEPS_PREVIOUS.has(folder)) return previous;
  if (!cache.has(folder)) cache.set(folder, loadFolder(folder));
  previous = cache.get(folder)!;
  return previous;
}
