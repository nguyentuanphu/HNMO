// Season 9 pentagram and errtels: the state the server sends (HnmoPentagramState, F5 59), the tooltip lines of
// pentagrams and errtels, and the colours of the elements. The desktop client has no pentagram window or tooltip (only
// the pentagram jewels' buff effects), so this follows the web game's own window and tooltip patterns.

import { currentLanguage } from '../i18n.js';

export interface PentagramOption {
  index: number;
  level: number;
  value: number;
  name: string;
}

export interface PentagramErrtel {
  number: number;
  element: number;
  options: PentagramOption[];
}

export interface PentagramWorn {
  number: number;
  element: number;
  usable: number;
  errtels: (PentagramErrtel | null)[];
}

export interface PentagramBagItem {
  slot: number;
  number: number;
  element: number;
  /** A pentagram's errtel slots. */
  errtels?: (PentagramErrtel | null)[];
  usable?: number;
  /** An errtel's own options. */
  errtel?: PentagramErrtel | null;
}

export interface PentagramState {
  v: number;
  enabled: boolean;
  worn: PentagramWorn | null;
  items: PentagramBagItem[];
  costs?: { levelUpZenPerLevel: number; rankUpZen: number; extractZen: number; mixNpc: number };
  result?: string | null;
}

/** The pentagram slot of the inventory's item storage (after the personal store). */
export const PENTAGRAM_SLOT = 236;

const ELEMENT_NAMES: Record<number, [string, string]> = {
  1: ['Fire', 'Lửa'],
  2: ['Water', 'Nước'],
  3: ['Earth', 'Đất'],
  4: ['Wind', 'Gió'],
  5: ['Darkness', 'Bóng tối'],
};

const ELEMENT_COLORS: Record<number, [number, number, number]> = {
  1: [1, 0.35, 0.2],
  2: [0.3, 0.6, 1],
  3: [0.75, 0.6, 0.3],
  4: [0.45, 1, 0.55],
  5: [0.75, 0.45, 1],
};

function vi(): boolean {
  return currentLanguage() === 'vi';
}

/** The name of an element in the player's language ("" for none). */
export function elementName(element: number): string {
  const names = ELEMENT_NAMES[element];
  return names ? (vi() ? names[1] : names[0]) : '';
}

/** The colour of an element's damage number (white for none). */
export function elementColor(element: number): [number, number, number] {
  return ELEMENT_COLORS[element] ?? [1, 1, 1];
}

/** An option's text with its value. */
export function optionText(option: PentagramOption): string {
  const text = option.name.includes('%d') ? option.name.replace('%d', String(option.value)).replace('%%', '%') : `${option.name} ${option.value}`;
  return `${text} (+${option.level})`;
}

/** Whether the item is a pentagram (12/200..214) or an errtel (12/221, 231, 241, 251, 261). */
export function isPentagramItem(group: number, number: number): boolean {
  return group === 12 && ((number >= 200 && number <= 214) || [221, 231, 241, 251, 261].includes(number));
}

/** The extra tooltip lines of a pentagram or errtel: its element, and its errtels or options. */
export function pentagramTooltipTexts(entry: PentagramBagItem | PentagramWorn | null): { text: string; color: string }[] {
  if (!entry) return [];
  const lines: { text: string; color: string }[] = [];
  if (entry.element > 0) lines.push({ text: `${vi() ? 'Nguyên tố' : 'Element'}: ${elementName(entry.element)}`, color: 'gold' });
  const own = 'errtel' in entry ? entry.errtel : null;
  if (own) {
    own.options.forEach((option, rank) => lines.push({ text: `${vi() ? 'Bậc' : 'Rank'} ${rank + 1}: ${optionText(option)}`, color: 'blue' }));
  }

  const slots = 'errtels' in entry ? entry.errtels : undefined;
  if (slots) {
    const usable = ('usable' in entry ? entry.usable : undefined) ?? slots.length;
    for (let i = 0; i < usable; i++) {
      const errtel = slots[i] ?? null;
      const label = `${vi() ? 'Ô' : 'Slot'} ${i + 1}`;
      if (!errtel) {
        lines.push({ text: `${label}: ${vi() ? 'trống' : 'empty'}`, color: 'gray' });
        continue;
      }

      lines.push({ text: `${label}: ${errtel.options.map(optionText).join(', ')}`, color: 'blue' });
    }
  }

  return lines;
}

let latest: PentagramState | null = null;
const listeners = new Set<(state: PentagramState) => void>();

/** Takes a new state from the server. */
export function setPentagramState(state: PentagramState): void {
  latest = state;
  for (const listener of listeners) listener(state);
}

/** The last state the server sent, or null. */
export function pentagramState(): PentagramState | null {
  return latest;
}

/** Calls back on each new state; returns the unsubscribe. */
export function onPentagramState(listener: (state: PentagramState) => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

let boundItems: Map<number, { group: number; number: number }> | null = null;

/** Binds the inventory's slot map, so a tooltip can find an item's slot. */
export function bindPentagramInventory(items: Map<number, { group: number; number: number }>): void {
  boundItems = items;
}

/** The pentagram tooltip lines of an inventory item ([] for other items or before the first state). */
export function pentagramTooltipFor(item: { group: number; number: number }): { text: string; color: string }[] {
  if (!latest?.enabled || !isPentagramItem(item.group, item.number) || !boundItems) return [];
  for (const [slot, candidate] of boundItems) {
    if (candidate === item) return pentagramTooltipTexts(pentagramEntryOfSlot(slot));
  }

  return [];
}

/** The state's entry for a bag slot (a pentagram or an errtel), or the worn pentagram for its slot. */
export function pentagramEntryOfSlot(slot: number): PentagramBagItem | PentagramWorn | null {
  if (!latest) return null;
  if (slot === PENTAGRAM_SLOT) return latest.worn;
  return latest.items.find((item) => item.slot === slot) ?? null;
}
