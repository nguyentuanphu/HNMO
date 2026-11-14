// Kalima, as the desktop has it (World/GameMaps/GMHellas: GetHellasLevel, RenderHellasItemInfo; the
// summons of WSclient.cpp's ReceiveCreateSummonViewport and ZzzCharacter's CreateHellGate):
//   - a Lost Map thrown on the ground (an ordinary item drop) makes the server open a gate, a summon
//     (C2 1F) of monster 152-158 named "Magic stone of <owner>" with "<name> .N to Kalima" over it;
//     walking into it (the server's GateNpc, 2 cells) takes the owner and the party to Kalima N;
//   - the Lost Map's and the Symbol of Kundun's tooltip lines (the level table, the count of symbols);
//   - the Kalima Ticket can't be used in a safe zone or in Kalima;
//   - SOUND_KUNDUN_ITEM_SOUND when a Lost Map arrives in the bag (ReceiveInventoryItemChanged, C1 F3 14).
// No DOM here: ui/fieldEventWindows.ts shows the gate's label.

import { format } from './textFormat.js';
import type { Item } from './inventory.js';
import type { TooltipLine } from './itemTooltip.js';

/** CMapManager::InHellas: Kalima 1-6 and Kalima 7 (the hidden one, "Lost Kalima"). */
export const KALIMA_MAPS = [24, 25, 26, 27, 28, 29, 36];
export const isKalimaMap = (map: number | null): boolean => map !== null && KALIMA_MAPS.includes(map);

export const LOST_MAP = { group: 14, number: 28 };
export const SYMBOL_OF_KUNDUN = { group: 14, number: 29 };
export const KALIMA_TICKET = { group: 13, number: 48 };
export const KALIMA_GATES = [152, 158] as const; // MONSTER_GATE_TO_KALIMA_1 .. 7

export const isKalimaGate = (type: number): boolean => type >= KALIMA_GATES[0] && type <= KALIMA_GATES[1];
const is = (item: Item, kind: { group: number; number: number }) => item.group === kind.group && item.number === kind.number;

export const KalimaText = {
  DToKalima: '%d to Kalima', MagicStoneOfOwner: 'Magic stone of %s', SummonedMonsterOwner: '%s of %s',
  CanTBeUsedInTheSafeZone: "Can't be used in the safe zone", Kalima: 'Kalima', Level: 'Level', MinLevel: 'Min. Level',
  MagicStoneWillAppearWhenYouThrowItInTheScreen: 'Magic stone will appear when you throw it in the screen',
  OnlyAboveLevelDCanUse: 'Only above level %d can use.', DD: '%d / %d', CanCreateLostMap: 'Can create lost map.',
  DIsLackingToCreateLostMap: '%d is lacking to create lost map.', LostKalima: 'Lost Kalima',
} as const;

// g_iKalimaLevel: the minimum level of Kalima 1-7; Magic Gladiator, Dark Lord and Rage Fighter have their own.
const KALIMA_LEVELS = [[40, 131, 181, 231, 281, 331, 350], [20, 111, 161, 211, 261, 311, 350]];
const MAX_LEVEL = 400;

/** The classes that start higher: 3 Magic Gladiator, 4 Dark Lord, 6 Rage Fighter. */
const levelsOf = (classIndex: number) => KALIMA_LEVELS[classIndex === 3 || classIndex === 4 || classIndex === 6 ? 1 : 0];

/** The name of a summon (CreateHellGate / ReceiveCreateSummonViewport): the gate's own, or "<monster> of <owner>". */
export function summonName(type: number, monsterName: string, owner: string): string {
  return isKalimaGate(type) ? format(KalimaText.MagicStoneOfOwner, owner) : format(KalimaText.SummonedMonsterOwner, monsterName, owner);
}

/** MoveCharacter's description over a gate (AddObjectDescription): "<name> .N to Kalima". */
export function gateLabel(name: string, type: number): string {
  return `${name} .${format(KalimaText.DToKalima, type - KALIMA_GATES[0] + 1)}`;
}

const tooltipLine = (text: string, background: string | null = null, color = 'white'): TooltipLine => ({ text, color, bold: false, background, spacer: false });
const spacer = (): TooltipLine => ({ text: '', color: 'white', bold: false, background: null, spacer: true });

/** RenderHellasItemInfo: the Lost Map's level table and the Symbol of Kundun's count; [] for other items. */
export function hellasTooltipLines(item: Item, hero: { classIndex?: number; level?: number } | null): TooltipLine[] {
  if (is(item, LOST_MAP)) {
    const levels = levelsOf(hero?.classIndex ?? 0);
    const lines = [tooltipLine(`${KalimaText.Kalima} ${KalimaText.Level}       ${KalimaText.MinLevel}    `)];
    levels.forEach((min, i) => {
      lines.push(tooltipLine(format('        %d             %3d~%3d     ', i + 1, min, MAX_LEVEL), item.level === i + 1 ? 'darkYellow' : null));
    });
    lines.push(spacer());
    lines.push(tooltipLine(KalimaText.MagicStoneWillAppearWhenYouThrowItInTheScreen, 'darkBlue'));
    if ((hero?.level ?? MAX_LEVEL) < levels[0]) {
      lines.push(spacer());
      lines.push(tooltipLine(format(KalimaText.OnlyAboveLevelDCanUse, levels[0]), 'darkRed'));
    }
    return lines;
  }
  if (is(item, SYMBOL_OF_KUNDUN)) {
    const lines = [tooltipLine(format(KalimaText.DD, item.durability, 5))];
    lines.push(tooltipLine(item.durability >= 5 ? KalimaText.CanCreateLostMap : format(KalimaText.DIsLackingToCreateLostMap, 5 - item.durability), null, 'yellow'));
    return lines;
  }
  return [];
}

/**
 * CInventoryActionController's Kalima Ticket: refused in a safe zone or in Kalima (the system log's
 * error line); true when the right click was handled here.
 */
export function kalimaTicketRefused(item: Item, safeZone: boolean, map: number | null): boolean {
  return is(item, KALIMA_TICKET) && (safeZone || isKalimaMap(map));
}

/** MapManager's map name of a Kalima map: Kalima for 1-6, Lost Kalima for the seventh. */
export function kalimaMapName(map: number): string {
  return map === 36 ? KalimaText.LostKalima : KalimaText.Kalima;
}
