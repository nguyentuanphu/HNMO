// The buff strip (CBuffStrip, buff_strip.rml): the hero's buffs and debuffs as icons, a tooltip with
// the name and description on hover, and a right click that offers to cancel the buffs a skill gave.

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { buffDefinition, buffStripOrder } from '../game/buffs.js';
import type { TooltipLine } from '../game/itemTooltip.js';

interface StripModel extends RmlModel {
  buffs: { decorator: string }[];
  buff_cancel: (index: number) => void;
  buff_hover: (index: number) => void;
  buff_leave: () => void;
}

// BuildIconDecorator: 20x28 tiles of two atlases, ten per row.
function iconDecorator(shown: number): string {
  const buff = buffDefinition(shown)?.iconFrom ?? shown;
  const first = buff < 81;
  const tile = Math.min(Math.max(first ? buff - 1 : buff - 81, 0), 89);
  return `image(${first ? 'atlas1-' : 'atlas2-'}${tile})`;
}

export class BuffStrip {
  private doc: RmlDocument | null = null;
  private shown: number[] = [];
  private hovered: Element | null = null;
  private readonly model: StripModel;

  constructor(private readonly actions: {
    active: () => number[];
    /** Seconds left of a buff, null when unknown. */
    secondsLeft: (buff: number) => number | null;
    tooltip: (lines: TooltipLine[] | null, box: DOMRect | null) => void;
    cancel: (skill: number, name: string) => void;
    skillName: (skill: number) => string;
    /** Reference px the windows docked at the right take (the strip centres in the rest). */
    dockedRight?: () => number;
  }) {
    this.model = {
      buffs: [],
      buff_cancel: () => undefined, // the right button is told apart in open()
      buff_hover: (index: number) => this.hover(index),
      buff_leave: () => this.actions.tooltip(null, null),
    };
    window.addEventListener('resize', () => this.place());
  }

  async open(parent: HTMLElement = document.body): Promise<void> {
    this.doc = await openDocument('buff_strip', this.model, { parent });
    this.doc.host.style.zIndex = '22';
    this.doc.root.addEventListener('mouseover', (event) => { this.hovered = event.target instanceof Element ? event.target.closest('.buff-slot') : null; }, true);
    this.doc.root.addEventListener('contextmenu', (event) => event.preventDefault());
    this.doc.root.addEventListener('mouseup', (event) => {
      if (event.button !== 2 || !(event.target instanceof Element)) return;
      const slot = event.target.closest('.buff-slot');
      if (slot) this.cancel([...slot.parentElement!.querySelectorAll('.buff-slot')].indexOf(slot));
    });
    const panel = this.doc.$('#panel');
    if (panel) panel.style.pointerEvents = 'auto';
    this.place();
    this.refresh();
  }

  place(): void {
    if (!this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    const width = window.innerWidth / scale;
    Object.assign(this.doc.root.style, { width: `${width}px`, height: `${REFERENCE_HEIGHT}px`, transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none' });
    // The theme centres the strip between the world's left and right edges (the docks leave the rest).
    this.doc.root.style.setProperty('--world-left', '0px');
    this.doc.root.style.setProperty('--world-right', `${width - (this.actions.dockedRight?.() ?? 0)}px`);
    this.doc.root.style.setProperty('--hud-scale', '1');
  }

  refresh(): void {
    if (!this.doc) return;
    this.shown = buffStripOrder(this.actions.active());
    this.model.buffs = this.shown.map((buff) => ({ decorator: iconDecorator(buff) }));
    this.doc.update();
  }

  // The tooltip (RenderBuffTooltip): the name in bold blue, then the description.
  private hover(index: number): void {
    const buff = this.shown[index];
    const definition = buff === undefined ? null : buffDefinition(buff);
    if (!definition) { this.actions.tooltip(null, null); return; }
    const line = (text: string, color: string, bold = false): TooltipLine => ({ text, color, bold, background: null, spacer: false });
    const lines = [line(definition.displayName?.en ?? definition.name, 'blue', true)];
    for (const text of (definition.description?.en ?? '').split('\n')) if (text) lines.push(line(text, 'white'));
    // g_BuffStringTime: the time left, in purple.
    const left = this.actions.secondsLeft(buff);
    if (left !== null) {
      const h = Math.floor(left / 3600), m = Math.floor((left % 3600) / 60), sec = left % 60;
      const text = h > 0 ? `${h} hour ${m} min ${sec} sec` : m > 0 ? `${m} min ${sec} sec` : `${sec} sec`;
      lines.push(line(`Duration period: ${text}`, 'purple'));
    }
    this.actions.tooltip(lines, this.hovered?.getBoundingClientRect() ?? null);
  }

  // OnBuffRightClick: buffs a skill gave can be cancelled (MagicEffectCancelRequest) after asking.
  private cancel(index: number): void {
    const buff = this.shown[index];
    const skill = buff === undefined ? 0 : buffDefinition(buff)?.cancelSkill ?? 0;
    if (skill) this.actions.cancel(skill, this.actions.skillName(skill));
  }
}
