// The warp list (/move and the move window M): Data/Local/Eng/MoveReq_eng.bmd as the desktop reads it
// (CMoveCommandData: a count, then 84-byte records xor'ed with the BUX code), and who may warp where
// (CMoveCommandWindow::SettingCanMoveMap).

import { fetchBytes } from '../bmd.js';
import type { Item } from './inventory.js';

export interface Warp { index: number; mapName: string; subName: string; level: number; maxLevel: number; zen: number; gate: number }

const BUX = [0xfc, 0xcf, 0xab];
const RECORD = 84; // index, main name[32], sub name[32], level, max level, zen, gate
let warps: Warp[] | null = null;

export async function loadWarps(): Promise<Warp[]> {
  if (warps) return warps;
  const bytes = await fetchBytes('/data/Local/Eng/MoveReq_eng.bmd');
  const count = new DataView(bytes.buffer, bytes.byteOffset).getInt32(0, true);
  const decoder = new TextDecoder();
  const text = (r: Uint8Array) => { const end = r.indexOf(0); return decoder.decode(r.subarray(0, end < 0 ? r.length : end)); };
  const list: Warp[] = [];
  for (let i = 0; i < count && 4 + (i + 1) * RECORD <= bytes.length; i++) {
    const r = new Uint8Array(RECORD);
    for (let j = 0; j < RECORD; j++) r[j] = bytes[4 + i * RECORD + j] ^ BUX[j % 3];
    const v = new DataView(r.buffer);
    list.push({
      index: v.getInt32(0, true), mapName: text(r.subarray(4, 36)), subName: text(r.subarray(36, 68)),
      level: v.getInt32(68, true), maxLevel: v.getInt32(72, true), zen: v.getInt32(76, true), gate: v.getInt32(80, true),
    });
  }
  warps = list;
  return list;
}

export interface WarpCheck {
  classIndex: number; level: number; money: number; outlaw: boolean;
  wings: Item | undefined; helper: Item | undefined; rings: (Item | undefined)[];
}

// SettingCanMoveMap: Magic Gladiators, Dark Lords and Rage Fighters need two thirds of the level
// (not for level-400 maps).
export function warpLevel(warp: Warp, classIndex: number): number {
  return [3, 4, 6].includes(classIndex) && warp.level !== 400 ? Math.floor((warp.level * 2) / 3) : warp.level;
}

export function canWarp(warp: Warp, check: WarpCheck): boolean {
  if (check.level < warpLevel(warp, check.classIndex) || check.money < warp.zen || check.outlaw) return false;
  const helper = check.helper;
  if (warp.mapName === 'Icarus') {
    // Wings, a Dinorant or a Fenrir (not a Uniria) — HasFlightEquipment; the transformation rings that
    // forbid Icarus come with the rings.
    const flying = Boolean(check.wings) || (helper?.group === 13 && (helper.number === 3 || helper.number === 37));
    return flying && !(helper?.group === 13 && helper.number === 2);
  }
  if (warp.mapName.startsWith('Atlans')) return !(helper?.group === 13 && (helper.number === 2 || helper.number === 3));
  return true;
}
