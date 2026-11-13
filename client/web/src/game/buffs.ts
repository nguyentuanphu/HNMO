// Buffs and debuffs: the definitions (Data/Buffs/Buffs.json, the desktop's BuffDatabase) and who has
// which (MagicEffectStatus, C1 07: an effect switched on or off on a character).

import type { Connection } from '../net/connection.js';

export interface BuffDefinition {
  number: number;
  name: string;
  kind: string;
  group?: number;
  displayName?: Record<string, string>;
  description?: Record<string, string>;
  family?: string;
  tier?: number;
  hidden?: boolean;
  cancelSkill?: number;
}

let definitions: Map<number, BuffDefinition> | null = null;
/** Per family, its lowest buff number (BuffDatabase's m_families). */
const familyOf = new Map<number, number>();

export async function loadBuffData(): Promise<void> {
  if (definitions) return;
  const response = await fetch('/data/Buffs/Buffs.json');
  if (!response.ok) throw new Error(`${response.status} Buffs.json`);
  const file = (await response.json()) as { buffs: BuffDefinition[] };
  definitions = new Map(file.buffs.map((buff) => [buff.number, buff]));
  const lowest = new Map<string, number>();
  for (const buff of file.buffs) if (buff.family) lowest.set(buff.family, Math.min(lowest.get(buff.family) ?? Infinity, buff.number));
  for (const buff of file.buffs) if (buff.family) familyOf.set(buff.number, lowest.get(buff.family)!);
}

export function buffDefinition(number: number): BuffDefinition | null {
  return definitions?.get(number) ?? null;
}

// BuffDatabase::PickShownOnStrip: shown unless hidden, and of a family only its highest tier.
export function buffsShownOnStrip(active: number[]): number[] {
  const best = new Map<number, number>();
  for (const number of active) {
    const definition = buffDefinition(number);
    if (!definition || definition.hidden) continue;
    const family = familyOf.get(number);
    if (family === undefined) continue;
    const current = best.get(family);
    if (current === undefined || (buffDefinition(current)?.tier ?? 0) < (definition.tier ?? 0)) best.set(family, number);
  }
  return active.filter((number) => {
    const definition = buffDefinition(number);
    if (!definition || definition.hidden) return false;
    const family = familyOf.get(number);
    return family === undefined || best.get(family) === number;
  });
}

// BuffSort: debuffs after the buffs, each in the order they came.
export function buffStripOrder(active: number[]): number[] {
  const shown = buffsShownOnStrip(active);
  const buffs: number[] = [], debuffs: number[] = [];
  for (const number of shown) (buffDefinition(number)?.kind === 'debuff' ? debuffs : buffs).unshift(number);
  return [...buffs, ...debuffs.reverse()];
}

/** Who has which buff, from the server's MagicEffectStatus. */
export class BuffTracker {
  private readonly active = new Map<number, number[]>();
  private readonly listeners = new Set<(id: number) => void>();
  /** When the hero's timed buffs end (performance.now() ms), from EffectItemConsumption. */
  private readonly endsAt = new Map<number, number>();

  constructor(connection: Connection, private readonly selfId: () => number) {
    connection.onPacket((bytes) => {
      if (bytes[0] === 0xc1 && bytes[2] === 0x07 && bytes.length >= 7) {
        // MagicEffectStatus: an effect on or off on a character
        this.set(((bytes[4] << 8) | bytes[5]) & 0x7fff, bytes[6], bytes[3] !== 0);
      } else if (bytes[0] === 0xc1 && bytes[2] === 0x2d && bytes.length >= 17) {
        // EffectItemConsumption (ReceiveBuffState): an item's buff on the hero, with its time
        const add = bytes[8] === 0, seconds = (bytes[12] | (bytes[13] << 8) | (bytes[14] << 16) | (bytes[15] << 24)) >>> 0, effect = bytes[16];
        if (add && seconds > 0) this.endsAt.set(effect, performance.now() + seconds * 1000);
        else this.endsAt.delete(effect);
        this.set(this.selfId(), effect, add);
      }
    });
  }

  /** Switches an effect on a character on or off (what MagicEffectStatus does). */
  set(id: number, effect: number, on: boolean): void {
    const list = this.active.get(id) ?? [];
    const at = list.indexOf(effect);
    if (on && at < 0) list.push(effect);
    if (!on && at >= 0) list.splice(at, 1);
    this.active.set(id, list);
    for (const listener of this.listeners) listener(id);
  }

  /** Seconds left of one of the hero's timed buffs, null when it has no known end. */
  secondsLeft(effect: number): number | null {
    const end = this.endsAt.get(effect);
    return end === undefined ? null : Math.max(0, Math.round((end - performance.now()) / 1000));
  }

  onChange(listener: (id: number) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  of(id: number): number[] {
    return this.active.get(id) ?? [];
  }

  has(id: number, buff: number): boolean {
    return this.of(id).includes(buff);
  }

  /** Forgets a character (out of view). */
  forget(id: number): void {
    this.active.delete(id);
  }
}
