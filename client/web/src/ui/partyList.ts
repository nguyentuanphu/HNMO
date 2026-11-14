// The party list (CPartyListWindow, party_list.rml): a card per member at the world's right edge —
// the leader's flag, the name, the health bar in tenths, red when the member isn't in sight; the
// leave button for the hero (and for every member when the hero leads).

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import type { Party } from '../game/party.js';
import { fitTextPx, nativeTextPx } from './nativeText.js';
import { textWidth } from '../game/questData.js';

interface Card {
  index: number; name: string; name_text_px: number; leader: boolean; absent: boolean; defense_buff: boolean;
  selected: boolean; show_leave: boolean; health_length: number;
}
interface ListModel extends RmlModel { cards: Card[]; party_hover: (index: number) => void; party_unhover: () => void; party_leave: (index: number) => void }

const HEALTH_BAR_WIDTH = 69;
const HEALTH_STEPS = 10;

export class PartyList {
  private doc: RmlDocument | null = null;
  private opening: Promise<RmlDocument> | null = null;
  private hovered = -1;
  private readonly model: ListModel;

  constructor(private readonly party: Party, private readonly actions: {
    heroName: string;
    /** Whether a member is in sight (a character of that name in view, or the hero). */
    inSight: (name: string) => boolean;
    dockedRight?: () => number;
  }) {
    this.model = {
      cards: [],
      party_hover: (index: number) => { this.hovered = index; this.refresh(); },
      party_unhover: () => { this.hovered = -1; this.refresh(); },
      party_leave: (index: number) => party.leave(index),
    };
    party.onChange(() => void this.refresh());
    window.addEventListener('resize', () => this.place());
  }

  async refresh(): Promise<void> {
    if (this.party.count === 0) {
      if (this.opening) await this.opening;
      if (this.doc && this.party.count === 0) { this.doc.close(); this.doc = null; }
      return;
    }
    if (!this.doc) {
      // One document even when several updates arrive while it opens.
      this.opening ??= openDocument('party_list', this.model, { parent: document.body }).then((doc) => {
        doc.host.style.zIndex = '21';
        const panel = doc.$('#panel');
        if (panel) panel.style.pointerEvents = 'auto';
        this.doc = doc;
        this.place();
        return doc;
      }).finally(() => { this.opening = null; });
      await this.opening;
      if (!this.doc) return;
    }
    this.model.cards = this.party.members.map((member, index) => {
      const absent = !this.actions.inSight(member.name);
      return {
        // NativeTextPxInBox: the name shrunk to its box (48 units for the leader, 58 for the others).
        index, name: member.name, name_text_px: fitTextPx(nativeTextPx(), textWidth(member.name), index === 0 ? 48 : 58), leader: index === 0, absent, defense_buff: false,
        selected: !absent && this.hovered === index, show_leave: this.party.canLeave(index, this.actions.heroName),
        health_length: (Math.min(HEALTH_STEPS, Math.max(0, member.healthStep)) / HEALTH_STEPS) * HEALTH_BAR_WIDTH,
      };
    });
    this.doc.update();
  }

  place(): void {
    if (!this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    const width = window.innerWidth / scale;
    this.doc.setScale(scale);
    Object.assign(this.doc.root.style, { width: `${width}px`, height: `${REFERENCE_HEIGHT}px`, transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none' });
    this.doc.root.style.setProperty('--world-left', '0px');
    this.doc.root.style.setProperty('--world-right', `${width - (this.actions.dockedRight?.() ?? 0)}px`);
    this.doc.root.style.setProperty('--hud-scale', '1');
    this.doc.root.style.setProperty('--board-scale', String(scale));
  }
}
