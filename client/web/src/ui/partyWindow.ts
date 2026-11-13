// The party window (CPartyInfoWindow, party_info.rml), docked at the right edge: P or the HUD's party
// button. Asks the server for the list when it opens (PartyListRequest); each member's group box with
// the leader's flag, the map, the position, the life bar and "life / max"; the X for the leader (on
// everyone) or the hero (on itself) leaves or removes. Without a party, how to make one.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import type { Party } from '../game/party.js';
import type { Connection } from '../net/connection.js';

interface MemberRow {
  index: number; name: string; map_text: string; coord_text: string; hp_text: string; hp_percent: number;
  is_leader: boolean; show_kick: boolean;
}

interface PartyInfoModel extends RmlModel {
  text_px: number;
  window_title: string;
  exit_tooltip: string;
  has_party: boolean;
  empty_state_lines: { text: string }[];
  members: MemberRow[];
  party_kick_member: (index: number) => void;
  window_close: () => void;
}

const PANEL_WIDTH = 190; // the docked windows' width (PARTY_INFO_WINDOW_WIDTH)

// Game.en.resx: the lines shown without a party.
const EMPTY_LINES = [
  'Type /party with the mouse cursor on', 'the player you would like', 'to create a party with', 'and you can create',
  'a party with them.', 'You can share more Exp with', 'your party members based on level.',
];

export class PartyWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private stop: (() => void) | null = null;
  private readonly model: PartyInfoModel;

  constructor(private readonly options: {
    connection: Connection;
    party: Party;
    heroName: string;
    mapName: (map: number) => string;
    /** Opening it closes the inventory, the character window and the NPC windows (HideAllGroupA). */
    onOpen?: () => void;
    onToggle?: (open: boolean) => void;
    click?: () => void;
    parent?: HTMLElement;
  }) {
    this.model = {
      text_px: 12, window_title: 'Party', exit_tooltip: 'Close Party Window (P)', has_party: false,
      empty_state_lines: EMPTY_LINES.map((text) => ({ text })), members: [],
      party_kick_member: (index) => this.kick(index),
      window_close: () => this.close(),
    };
    prepareDocument('party_info').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { this.close(); this.options.click?.(); }
    });
    window.addEventListener('resize', () => this.place());
  }

  get isOpen(): boolean { return this.doc !== null; }

  async toggle(): Promise<void> {
    this.options.click?.();
    if (this.isOpen) this.close(); else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  private async load(): Promise<void> {
    this.options.onOpen?.();
    // OpenningProcess(): the newest list from the server.
    this.options.connection.send('ClientToServer', 'PartyListRequest', {});
    this.refresh();
    const doc = await openDocument('party_info', this.model, { parent: this.options.parent ?? document.body, scale: window.innerHeight / REFERENCE_HEIGHT });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    }
    this.stop = this.options.party.onChange(() => this.refresh());
    this.place();
    this.refresh();
    this.options.onToggle?.(true);
  }

  close(): void {
    if (!this.doc) return;
    this.stop?.();
    this.stop = null;
    this.doc.close();
    this.doc = null;
    this.options.onToggle?.(false);
  }

  /** LeaveParty(): the member's number goes to the server (PartyPlayerKickRequest). */
  private kick(index: number): void {
    const { party, heroName } = this.options;
    if (!party.canLeave(index, heroName)) return;
    this.options.click?.();
    party.leave(index);
  }

  /** SyncRmlModel(): one row per member. */
  private refresh(): void {
    const { party, heroName, mapName } = this.options;
    const leaderIsHero = party.members[0]?.name === heroName;
    this.model.has_party = party.count > 0;
    this.model.members = party.members.map((member, index) => ({
      index, name: member.name, map_text: mapName(member.map), coord_text: `(${member.x},${member.y})`,
      hp_percent: member.maxLife > 0 ? (member.life * 100) / member.maxLife : 0,
      hp_text: `${member.life} / ${member.maxLife}`,
      is_leader: index === 0, show_kick: leaderIsHero || member.name === heroName,
    }));
    this.doc?.update();
  }

  /** Docks it at the right edge (the dock-right column, where the character window would be). */
  place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top',
    });
    this.model.text_px = Math.round(12 * scale);
    this.doc.update();
  }
}
