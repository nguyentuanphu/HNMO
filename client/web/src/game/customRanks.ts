import { currentLanguage } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import type { Connection } from '../net/connection.js';

export interface CustomRankTierSnapshot {
  id: string;
  priority: number;
  titleEn: string;
  titleVi: string;
  colorHex: string;
  minLevel: number;
  minMasterLevel: number;
  minResets: number;
  minVipLevel: number;
  minMissionsClaimed: number;
  requiredAchievementKey: string;
}

export interface CustomRankBadge {
  name: string;
  rankId: string;
  titleEn: string;
  titleVi: string;
  colorHex: string;
}

export interface CustomRanksStatePayload {
  v: number;
  enabled: boolean;
  showByNameplate: boolean;
  showInChat: boolean;
  me: {
    character: string;
    selectedRankId: string;
    resolvedRankId: string;
    titleEn: string;
    titleVi: string;
    colorHex: string;
    unlockedIds: string[];
  } | null;
  tiers: CustomRankTierSnapshot[];
  players: CustomRankBadge[];
}

/**
 * Tracks player custom rank / title badges delivered by HnmoCustomRanksState (C2 F5 49).
 */
export class CustomRanksManager {
  private readonly send: (name: string, fields: PacketValues) => void;
  private state: CustomRanksStatePayload | null = null;
  private readonly byName = new Map<string, CustomRankBadge>();
  private readonly listeners = new Set<() => void>();

  constructor(options: { send: (name: string, fields: PacketValues) => void }) {
    this.send = options.send;
  }

  /** Requests the initial snapshot once the hero has entered the world. */
  start(): void {
    this.send('HnmoCustomRanksCommand', { Action: 0, RankId: '' });
  }

  /** Sends a rank selection change (empty or 'auto' returns to automatic priority). */
  selectRank(rankId: string): void {
    this.send('HnmoCustomRanksCommand', { Action: 1, RankId: rankId });
  }

  /** Registers a listener invoked whenever custom rank state updates. */
  onChange(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /** Handles an incoming server packet; returns true if it was HnmoCustomRanksState (C2 F5 49). */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x49) return false;
    const pkt = read<{ Json?: string }>('ServerToClient', 'HnmoCustomRanksState', bytes);
    if (!pkt?.Json) return true;
    this.applyJson(pkt.Json);
    return true;
  }

  /** Applies a serialized CustomRanksStatePayload JSON snapshot directly (also used in tests). */
  applyJson(json: string): void {
    let parsed: CustomRanksStatePayload;
    try {
      parsed = JSON.parse(json) as CustomRanksStatePayload;
    } catch {
      return;
    }
    this.state = parsed;
    this.byName.clear();
    for (const p of parsed.players ?? []) {
      if (p?.name) this.byName.set(p.name.toLowerCase(), p);
    }
    for (const listener of this.listeners) listener();
  }

  get current(): CustomRanksStatePayload | null {
    return this.state;
  }

  /** Returns the localized nameplate badge for name, or null if hidden or absent. */
  nameplateBadge(name: string): { text: string; colorHex: string } | null {
    if (!this.state?.enabled || !this.state.showByNameplate) return null;
    const badge = this.byName.get(name.toLowerCase());
    if (!badge) return null;
    const text = (currentLanguage() === 'vi' ? badge.titleVi : badge.titleEn).trim();
    if (!text) return null;
    return { text, colorHex: badge.colorHex || '#ffd166' };
  }

  /** Returns the localized chat prefix badge for name, or null. */
  chatPrefix(name: string): string | null {
    if (!this.state?.enabled || !this.state.showInChat) return null;
    const badge = this.byName.get(name.toLowerCase());
    if (!badge) return null;
    const text = (currentLanguage() === 'vi' ? badge.titleVi : badge.titleEn).trim();
    return text || null;
  }
}

/** Convenience helper wiring a Connection directly into CustomRanksManager. */
export function createCustomRanks(connection: Connection): CustomRanksManager {
  return new CustomRanksManager({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
  });
}
