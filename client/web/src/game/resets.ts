// HNMO's reset options in the web game: the server sends the reset state (HnmoResetsState, C2 F5 1E) after the game
// asked once (HnmoResetsRequest, F5 1D, when it enters the world) and whenever something changes: a reset with the
// reset NPC or /reset, a relife (/relife), the delegated reset switch (/autoreset, /uythac), /reset info, or delegated
// resets the server did while the character was offline leveling. The server answers the commands in the chat itself
// (blue lines, as for the desktop client); the web game adds a notice with today's count and limit after a reset or
// relife and after the delegated resets, and keeps the character window's reset count right when the reset doesn't go
// back to the character selection. HnmoResetsAction (F5 1F) asks for a reset, relife, the info or the switch.
// There is no desktop window for this, so there is none here either.

import { read, type PacketValues } from '../net/packets.js';
import { tr } from '../ui/missionsWindow.js';

export type ResetResult = 'Ok' | 'Off' | 'NoTier' | 'MaximumResets' | 'LevelTooLow' | 'DailyLimit' | 'DelegatedDailyLimit' | 'NotEnoughZen'
  | 'NotEnoughCoins' | 'NotEnoughItems' | 'RelifeOff' | 'ResetsTooLow' | 'MaximumRelifes' | 'RelifeDailyLimit' | 'Online' | 'NotAllowed';

export interface ResetPlanState {
  result: ResetResult; level: number; levelAfter: number; resetsAfter: number; relifesAfter: number; resetsNeeded?: number;
  zen: number; coins: { type: string; amount: number }[]; items: { group: number; number: number; level: number; count: number; name?: string }[];
  mode: 'Total' | 'Add' | 'Keep'; points: number; clearStats: boolean; vip: boolean; maximum: number;
}

export interface ResetCharacterState {
  name: string; level: number; resets: number; relifes: number; vip: boolean; today: number; dailyLimit: number;
  todayRelifes: number; relifeDailyLimit: number; month: number;
  delegated: { allowed: boolean; on: boolean; today: number; dailyLimit: number; blocked?: ResetResult };
  next: ResetPlanState; relife?: ResetPlanState;
}

export interface ResetsState {
  v: number; on: boolean; character: ResetCharacterState;
  event?: { type: 'reset' | 'relife' | 'away' | 'switch' | 'info' | 'delegated'; result?: ResetResult; count: number };
}

export interface ResetsOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A notice in the chat. */
  message: (text: string) => void;
  /** Sets the character's reset count (the character window). */
  setResets: (resets: number) => void;
}

/** The actions of HnmoResetsAction. */
export const RESET_ACTION = { Reset: 1, Relife: 2, DelegatedOn: 3, DelegatedOff: 4, Info: 5 } as const;

export class Resets {
  private current: ResetsState | null = null;

  constructor(private readonly options: ResetsOptions) {}

  /** The last state from the server (for tests and the guide). */
  get state(): ResetsState | null { return this.current; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoResetsRequest', {});
  }

  /** Asks the server for an action (a reset, relife, the info, the delegated reset switch). */
  act(action: number): void {
    this.options.send('HnmoResetsAction', { Action: action });
  }

  /** A packet from the server: the reset state (C2 F5 1E). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x1e) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoResetsState', bytes);
    let state: ResetsState;
    try {
      state = JSON.parse(Json) as ResetsState;
    } catch {
      console.warn('Resets: unreadable state');
      return true;
    }
    if (!state.character) return true;
    this.current = state;
    this.options.setResets(state.character.resets);
    const notice = state.event ? Resets.notice(state) : null;
    if (notice) this.options.message(notice);
    return true;
  }

  /** The notice of an event: a reset or relife done, delegated resets while away; nothing for the rest (the server tells). */
  static notice(state: ResetsState): string | null {
    const { character, event } = state;
    if (!event || event.result !== 'Ok') return null;
    const today = character.dailyLimit > 0 ? `${character.today}/${character.dailyLimit}` : String(character.today);
    switch (event.type) {
      case 'reset':
        return character.vip
          ? tr('Reset %d done (VIP). Today: %s resets.', character.resets, today)
          : tr('Reset %d done. Today: %s resets.', character.resets, today);
      case 'relife':
        return tr('Relife %d done. Resets now: %d.', character.relifes, character.resets);
      case 'away':
        return tr('Delegated reset: %d resets while you were offline leveling. Resets now: %d, today: %s.', event.count, character.resets, today);
      default:
        return null;
    }
  }
}
