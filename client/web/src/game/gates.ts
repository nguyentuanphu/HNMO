// Gates you walk into (the desktop's CheckGate): Data/gate.bmd holds 512 gates of 14 bytes each, xor'ed
// with the BUX code (flag, map, x1, y1, x2, y2, target, angle, level, max level). Standing inside an
// entrance (flag 1) of the current map asks the server to take the hero through it (EnterGateRequest),
// after the same checks: the target's level (two thirds for Magic Gladiators, Dark Lords and Rage
// Fighters; gate 28 only by level), no Uniria or Dinorant into Atlans (gates 45-49, 55-56), flight
// equipment and no Uniria for Icarus (gates 62-65).

import { fetchBytes } from '../bmd.js';
import type { Item } from './inventory.js';

export interface Gate { index: number; flag: number; map: number; x1: number; y1: number; x2: number; y2: number; target: number; level: number }

const BUX = [0xfc, 0xcf, 0xab];
const RECORD = 14;
const MAX_GATES = 512;
/** g_dwLatestZoneMoving: no new gate request within 3 s of the last one. */
const RESEND_MS = 3000;

let gates: Gate[] | null = null;

export async function loadGates(): Promise<Gate[]> {
  if (gates) return gates;
  const bytes = await fetchBytes('/data/gate.bmd');
  const list: Gate[] = [];
  for (let i = 0; i < MAX_GATES && (i + 1) * RECORD <= bytes.length; i++) {
    const r = new Uint8Array(RECORD);
    for (let j = 0; j < RECORD; j++) r[j] = bytes[i * RECORD + j] ^ BUX[j % 3];
    const v = new DataView(r.buffer);
    list.push({ index: i, flag: r[0], map: r[1], x1: r[2], y1: r[3], x2: r[4], y2: r[5], target: v.getUint16(6, true), level: v.getUint16(10, true) });
  }
  gates = list;
  return list;
}

export interface GateHero {
  map: number; x: number; y: number; level: number; classIndex: number;
  helper: Item | undefined; wings: Item | undefined;
}

export type GateDecision = { kind: 'enter'; gate: number } | { kind: 'message'; text: string } | null;

const isHorse = (helper: Item | undefined, numbers: number[]) => helper?.group === 13 && numbers.includes(helper.number);

/** What standing at the hero's cell does (CheckGate), without the timing. */
export function gateAt(list: readonly Gate[], hero: GateHero): GateDecision {
  for (const gate of list) {
    if (gate.flag !== 1 || gate.map !== hero.map) continue;
    if (hero.x < gate.x1 || hero.y < gate.y1 || hero.x > gate.x2 || hero.y > gate.y2) continue;
    const target = list[gate.target];
    if (!target) return null;
    const level = [3, 4, 6].includes(hero.classIndex) ? Math.floor((target.level * 2) / 3) : target.level;
    if (gate.index === 28 && hero.level < level) return null;
    const i = gate.index;
    if (((i >= 45 && i <= 49) || i === 55 || i === 56) && isHorse(hero.helper, [2, 3])) {
      return { kind: 'message', text: 'You cannot go to Atlans while riding a Uniria or Dinorant.' };
    }
    const flying = (hero.wings !== undefined && hero.wings.durability > 0) || (isHorse(hero.helper, [3, 37]) && (hero.helper?.durability ?? 0) > 0);
    if (i >= 62 && i <= 65 && !flying) {
      const text = 'You can enter Icarus only with wings, a Dinorant or a Fenrir.';
      return { kind: 'message', text: hero.level < level ? `${text}\nOnly characters over level ${level} can enter.` : text };
    }
    if (i >= 62 && i <= 65 && isHorse(hero.helper, [2])) return { kind: 'message', text: 'You cannot warp while riding on a Uniria.' };
    if (hero.level < level) return { kind: 'message', text: `Only characters over level ${level} can enter.` };
    return { kind: 'enter', gate: i };
  }
  return null;
}

/** Checks the hero's cell every frame and sends the request once per entrance. */
export class GateWatcher {
  private lastSent = 0;
  private lastCell = '';

  constructor(private readonly actions: {
    hero: () => GateHero | null;
    /** Whether a map is loading (LoadingWorld): no gates then. */
    busy: () => boolean;
    enter: (gate: number) => void;
    message: (text: string) => void;
  }) {
    void loadGates().catch((error: unknown) => console.warn('Gates', error));
  }

  update(now = performance.now()): void {
    if (!gates || this.actions.busy()) return;
    const hero = this.actions.hero();
    if (!hero) return;
    // Once per cell: standing still in an entrance asks once, and a refusal's message isn't repeated.
    const cell = `${hero.map}:${hero.x}:${hero.y}`;
    if (cell === this.lastCell) return;
    const decision = gateAt(gates, hero);
    if (!decision) { this.lastCell = cell; return; }
    if (decision.kind === 'message') { this.lastCell = cell; this.actions.message(decision.text); return; }
    if (now - this.lastSent < RESEND_MS) return;
    this.lastCell = cell;
    this.lastSent = now;
    this.actions.enter(decision.gate);
  }
}
