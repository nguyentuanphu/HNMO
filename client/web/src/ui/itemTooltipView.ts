// The item tooltip of the inventory and vault, as the desktop client shows it: the lines of
// game/itemTooltip.ts in the game's tooltip document (tooltip.rml), centred text in a black box
// centred on the item, below it, or above it when there's no room below (RmlTooltip's Place()).

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { loadTooltipData, tooltipLines, type TooltipCharacter, type TooltipLine } from '../game/itemTooltip.js';
import type { Item } from '../game/inventory.js';

const TEXT_UNITS = 12;        // the tooltip's text size in reference pixels
const LINE_UNITS = 15;        // a row's advance
const PADDING_UNITS = 2;      // RmlTooltip's kNativePaddingUnits, per side
const BORDER_UNITS = 1;       // RmlTooltip's kNativeBorderUnits

interface TooltipRow {
  text: string; bold: boolean; is_half_spacer: boolean; is_full_spacer: boolean;
  color_blue: boolean; color_gray: boolean; color_red: boolean; color_yellow: boolean; color_green: boolean;
  color_purple: boolean; color_redpurple: boolean; color_violet: boolean; color_orange: boolean;
  highlight_darkred: boolean; highlight_darkblue: boolean; highlight_darkyellow: boolean; highlight_greenblue: boolean;
}

interface TooltipModel extends RmlModel {
  pos_x: number;
  pos_y: number;
  center_text: boolean;
  lines: TooltipRow[];
}

export interface ScreenBox { left: number; top: number; width: number; height: number }

function row(line: TooltipLine): TooltipRow {
  const color = line.color.toLowerCase();
  const background = line.background?.toLowerCase() ?? '';
  return {
    text: line.text, bold: line.bold, is_half_spacer: line.spacer, is_full_spacer: false,
    color_blue: color === 'blue', color_gray: color === 'gray', color_red: color === 'red', color_yellow: color === 'yellow',
    color_green: color === 'green', color_purple: color === 'purple', color_redpurple: color === 'redpurple',
    color_violet: color === 'violet', color_orange: color === 'orange',
    highlight_darkred: background === 'darkred', highlight_darkblue: background === 'darkblue',
    highlight_darkyellow: background === 'darkyellow', highlight_greenblue: background === 'greenblue',
  };
}

export class ItemTooltipView {
  private doc: RmlDocument | null = null;
  private opening: Promise<RmlDocument> | null = null;
  private readonly model: TooltipModel = { pos_x: -10000, pos_y: -10000, center_text: true, lines: [] };
  private shownFor: Item | null = null;

  constructor(private readonly parent: HTMLElement, private readonly character: () => TooltipCharacter | null) {
    loadTooltipData().catch((error: unknown) => console.warn('Tooltip data', error));
  }

  private async document(): Promise<RmlDocument> {
    if (this.doc) return this.doc;
    this.opening ??= openDocument('tooltip', this.model, { parent: this.parent }).then((doc) => {
      doc.host.style.zIndex = '70';
      this.doc = doc;
      return doc;
    });
    return this.opening;
  }

  async show(item: Item, box: ScreenBox): Promise<void> {
    this.shownFor = item;
    const [doc] = await Promise.all([this.document(), loadTooltipData()]);
    if (this.shownFor !== item) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    const panel = doc.$('#tooltip_panel');
    if (!panel) return;
    Object.assign(panel.style, {
      fontSize: `${TEXT_UNITS * scale}px`,
      width: 'max-content',
      padding: `0 ${PADDING_UNITS * scale}px`,
      border: `${BORDER_UNITS * scale}px solid #000`,
    });
    panel.style.setProperty('--line-px', `${LINE_UNITS * scale}px`);
    panel.style.setProperty('--bold-line-px', `${LINE_UNITS * scale}px`);
    this.model.lines = tooltipLines(item, this.character()).map(row);
    this.model.pos_x = -10000;
    doc.update();
    // Place it once its size is known: centred on the item, below it or, without room, above.
    const size = panel.getBoundingClientRect();
    const centre = box.left + box.width / 2;
    const below = box.top + box.height;
    const top = below + size.height <= window.innerHeight ? below : Math.max(0, box.top - size.height);
    this.model.pos_x = Math.round(Math.max(0, Math.min(centre - size.width / 2, window.innerWidth - size.width)));
    this.model.pos_y = Math.round(top);
    doc.update();
  }

  hide(): void {
    this.shownFor = null;
    if (!this.doc) return;
    this.model.lines = [];
    this.model.pos_x = -10000;
    this.doc.update();
  }
}
