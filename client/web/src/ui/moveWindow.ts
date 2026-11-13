// The move window (M, CMoveCommandWindow, move_command.rml): the warp list with each map's level and
// zen; a click on a map the hero may enter warps there (WarpCommandRequest with the server's key).

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { canWarp, loadWarps, warpLevel, type Warp, type WarpCheck } from '../game/warps.js';

interface Row { index: number; strife_text: string; map_name: string; req_level: string; req_zen: string; can_move: boolean; level_unmet: boolean; zen_unmet: boolean }
interface MoveModel extends RmlModel {
  text_px: number; title_text: string; head_strife: string; head_map: string; head_level: string; head_zen: string; close_text: string;
  rows: Row[]; movecommand_warp: (index: number) => void; movecommand_close: () => void; movecommand_wheel: () => void;
}

export class MoveWindow {
  private doc: RmlDocument | null = null;
  private warps: Warp[] = [];
  private readonly model: MoveModel;

  constructor(private readonly actions: { check: () => WarpCheck; warp: (warp: Warp) => void }) {
    this.model = {
      text_px: 12, title_text: 'Move', head_strife: 'Battle Zone', head_map: 'Map', head_level: 'Level', head_zen: 'Zen', close_text: 'Close',
      rows: [],
      movecommand_warp: (index: number) => this.choose(index),
      movecommand_close: () => this.close(),
      movecommand_wheel: () => undefined,
    };
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => { if (event.key === 'Escape' && this.doc) this.close(); });
  }

  get isOpen(): boolean { return this.doc !== null; }

  async toggle(): Promise<void> {
    if (this.doc) this.close(); else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.warps = await loadWarps();
    this.doc = await openDocument('move_command', this.model, { parent: document.body });
    this.doc.host.style.zIndex = '40';
    const panel = this.doc.$('#panel');
    if (panel) panel.style.pointerEvents = 'auto';
    this.place();
    this.refresh();
  }

  close(): void {
    this.doc?.close();
    this.doc = null;
  }

  private place(): void {
    if (!this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    this.model.text_px = Math.round(12 * scale);
    const panel = this.doc.$('#panel');
    if (panel) Object.assign(panel.style, { transform: `scale(${scale})`, transformOrigin: 'left top', left: `${Math.max(0, (window.innerWidth - (panel.offsetWidth || 220) * scale) / 2)}px`, top: `${Math.round(40 * scale)}px` });
    this.doc.update();
  }

  /** The rows as SettingCanMoveMap and the RML model fill them. */
  refresh(): void {
    if (!this.doc) return;
    const check = this.actions.check();
    this.model.rows = this.warps.map((warp) => {
      const level = warpLevel(warp, check.classIndex);
      return {
        index: warp.index, strife_text: warp.index === 42 ? '*' : '', map_name: warp.mapName, req_level: String(level), req_zen: String(warp.zen),
        can_move: canWarp(warp, check), level_unmet: level > check.level, zen_unmet: warp.zen > check.money,
      };
    });
    this.doc.update();
  }

  private choose(index: number): void {
    const warp = this.warps.find((w) => w.index === index);
    if (!warp || !canWarp(warp, this.actions.check())) return;
    this.actions.warp(warp);
    this.close();
  }
}
