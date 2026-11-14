// The Gens windows:
//   gens_ranking.rml   CGensRanking (B): the family's mark for the hero's rank, the family, the title,
//                      the ranking and the contribution, how much the next rank needs, the rewards text.
//                      Opening asks the server for the newest standing (GensRankingRequest).
//   GensMarks          the family mark beside a player's name over its head (CHAT::Render's RanderMark
//                      in MARK_BOOLEAN size, 0.8 of the 50 x 69 cell), on the player under the pointer.
// The gens state is game/gens.ts.

import * as THREE from 'three';
import { loadImage, openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { divideStringByPixel, textWidth } from '../game/questData.js';
import { GENS_DUPRIAN, GENS_VANERT, GensText, gensMarkCell, gensTitle, type Gens } from '../game/gens.js';
import type { Character } from '../game/characters.js';
import { fitTextPx, nativeTextPx } from './nativeText.js';

const PANEL_WIDTH = 190;
const TEXT_PX = 12;

interface Line { text: string; text_px: number }
interface RankingModel extends RmlModel {
  mark_sprite: string; title: Line; gens_label: Line; gens_name: Line; level_label: Line; title_name: Line; rank_label: Line; rank_value: Line;
  contrib_label: Line; contrib_value: Line; desc_label: Line; promo_lines: Line[]; desc_lines: Line[]; desc_line_height: number; exit_tooltip: string;
  gens_ranking_exit: () => void;
}

/** CGensRanking, docked right; B (HotKey.cpp) toggles it, only for a gens member. */
export class GensRankingWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private readonly model: RankingModel;
  private textPx = TEXT_PX;

  constructor(private readonly options: { gens: Gens; send: () => void; message: (text: string) => void; click: () => void; closeOthers: () => void }) {
    const line = (text = ''): Line => ({ text, text_px: TEXT_PX });
    this.model = {
      mark_sprite: '', title: line(), gens_label: line(), gens_name: line(), level_label: line(), title_name: line(), rank_label: line(),
      rank_value: line(), contrib_label: line(), contrib_value: line(), desc_label: line(), promo_lines: [], desc_lines: [], desc_line_height: TEXT_PX,
      exit_tooltip: GensText.Close,
      gens_ranking_exit: () => { this.options.click(); this.close(); },
    };
    prepareDocument('gens_ranking').catch((error: unknown) => console.warn('Interface', error));
    options.gens.onChange(() => this.sync());
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.target instanceof Element && event.target.closest('input, textarea')) return;
      if (event.key === 'Escape' && this.isOpen) { this.close(); return; }
      if (event.key.toLowerCase() === 'b' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void this.toggle(); }
    });
  }

  get isOpen(): boolean { return this.doc !== null; }

  /** B: SetGensInfo() first (a hero without a family is told so), then Toggle with the click. */
  async toggle(): Promise<void> {
    if (!this.options.gens.familyName) { this.options.message(GensText.NotJoined); return; }
    this.options.click();
    if (this.isOpen) this.close(); else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  private async load(): Promise<void> {
    // HideAllGroupA, then OpenningProcess(): the newest standing from the server.
    this.options.closeOthers();
    this.options.send();
    this.sync();
    const doc = await openDocument('gens_ranking', this.model, { parent: document.body, scale: window.innerHeight / REFERENCE_HEIGHT });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    const panel = doc.$('#panel');
    panel?.addEventListener('pointerdown', (event) => event.stopPropagation());
    for (const id of ['#btn_exit', '#frame_corner_close']) { const button = doc.$(id); if (button) button.style.pointerEvents = 'auto'; }
    this.place();
  }

  close(): void {
    this.doc?.close();
    this.doc = null;
  }

  /** SyncContent(): the original's RenderTexts() and RenderMark(). */
  sync(): void {
    const { gens } = this.options;
    const px = this.textPx;
    // NativeTextPxInBox: a line wider than its box (reference px) is drawn smaller to fit it.
    const line = (text: string, box = PANEL_WIDTH, bold = false): Line => {
      const width = textWidth(text) * (bold ? 1.1 : 1);
      return { text, text_px: fitTextPx(px, width, box) };
    };
    const m = this.model;
    m.title = line(GensText.InfoWindow, PANEL_WIDTH, true);
    m.gens_label = line(GensText.Gens, PANEL_WIDTH, true);
    m.gens_name = line(gens.familyName);
    m.level_label = line(GensText.Level);
    m.title_name = line(gensTitle(gens.rank), PANEL_WIDTH - 16);
    m.rank_label = line(GensText.Ranking, 74, true);
    m.rank_value = line(gens.ranking, PANEL_WIDTH - 20);
    m.contrib_label = line(GensText.GainContribution, 74, true);
    m.contrib_value = line(String(gens.contribution), PANEL_WIDTH - 20);
    m.desc_label = line(GensText.Description, 58, true);
    m.promo_lines = gens.nextContribution > 0
      ? divideStringByPixel(GensText.NextRank.replace('%d', String(gens.nextContribution)), 140, 7).map((text) => line(text, 180)) : [];
    // CTextBox: '#' breaks the line, words wrap to the box.
    m.desc_lines = [GensText.RewardsGivenOut, GensText.RewardsClaimed]
      .flatMap((text) => text.split('#').flatMap((part) => (part.trim() ? divideStringByPixel(part.trim(), 150, 35, false) : [''])))
      .map((text) => line(text, 150));
    m.desc_line_height = px;
    const cell = gensMarkCell(gens.rank);
    m.mark_sprite = gens.influence === GENS_DUPRIAN && cell >= 0 ? `gens-d-${cell}` : gens.influence === GENS_VANERT && cell >= 0 ? `gens-v-${cell}` : '';
    this.doc?.update();
  }

  private place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    Object.assign(panel.style, { left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top' });
    this.textPx = nativeTextPx();
    this.sync();
  }
}

// ---------- the marks over players ----------

const MARK = { width: 50, height: 69, columns: 5, size: 0.8 } as const; // GENSMARK_WIDTH / HEIGHT, m_fBooleanSize
const MARK_SHEETS: Record<number, string> = { [GENS_DUPRIAN]: '/data/Interface/Gens_mark_D_new.tga', [GENS_VANERT]: '/data/Interface/Gens_mark_V_new.tga' };

/**
 * The name over the player under the pointer (CNameWindow's CreateChat for SelectedCharacter) with its
 * family's mark at the left of the name box, the rank's cell at 0.8 size, centred on the box's height.
 */
export class GensMarks {
  private readonly tag: HTMLDivElement;
  private readonly name: HTMLDivElement;
  private readonly mark: HTMLDivElement;
  private readonly sheets = new Map<number, { url: string; width: number; height: number }>();
  private hovered: Character | null = null;
  private readonly box = new THREE.Box3();

  constructor(private readonly options: {
    gens: Gens; camera: THREE.Camera; canvas: HTMLCanvasElement; players: () => Iterable<Character>;
  }) {
    this.tag = document.createElement('div');
    this.tag.className = 'player-name-tag';
    Object.assign(this.tag.style, { position: 'fixed', zIndex: '6', pointerEvents: 'none', display: 'none', transform: 'translate(-50%, -100%)' });
    this.name = document.createElement('div');
    Object.assign(this.name.style, {
      background: 'rgba(0, 0, 0, 0.6)', color: '#fff', font: 'bold 12px Tahoma, Arial, sans-serif', padding: '1px 4px', whiteSpace: 'nowrap',
    });
    this.mark = document.createElement('div');
    this.mark.className = 'gens-mark';
    Object.assign(this.mark.style, { position: 'absolute', top: '50%', transform: 'translateY(-50%)', display: 'none', backgroundRepeat: 'no-repeat' });
    this.tag.append(this.mark, this.name);
    document.body.append(this.tag);
    for (const [gens, url] of Object.entries(MARK_SHEETS)) {
      void loadImage(url).then((canvas) => { if (canvas) this.sheets.set(Number(gens), { url: canvas.toDataURL(), width: canvas.width, height: canvas.height }); });
    }
    options.canvas.addEventListener('pointermove', (event) => { this.hovered = this.pick(event); });
    options.canvas.addEventListener('pointerleave', () => { this.hovered = null; });
    const frame = () => { this.update(); requestAnimationFrame(frame); };
    requestAnimationFrame(frame);
  }

  /** The player whose box is under the pointer (the main view's picking of characters). */
  private pick(event: PointerEvent): Character | null {
    const { camera, canvas } = this.options;
    const rect = canvas.getBoundingClientRect();
    let best: { character: Character; distance: number } | null = null;
    for (const character of this.options.players()) {
      if (character.kind !== 'player' || character.dead) continue;
      this.box.setFromObject(character.group);
      if (this.box.isEmpty()) continue;
      const a = this.box.max.clone().project(camera), b = this.box.min.clone().project(camera);
      const x1 = rect.left + (a.x + 1) / 2 * rect.width, x2 = rect.left + (b.x + 1) / 2 * rect.width;
      const y1 = rect.top + (1 - a.y) / 2 * rect.height, y2 = rect.top + (1 - b.y) / 2 * rect.height;
      const left = Math.min(x1, x2), right = Math.max(x1, x2), top = Math.min(y1, y2), bottom = Math.max(y1, y2);
      if (event.clientX < left - 8 || event.clientX > right + 8 || event.clientY < top - 8 || event.clientY > bottom + 8) continue;
      const distance = Math.hypot(event.clientX - (left + right) / 2, event.clientY - (top + bottom) / 2);
      if (!best || distance < best.distance) best = { character, distance };
    }
    return best?.character ?? null;
  }

  /** The player under the pointer. */
  get hoveredPlayer(): Character | null { return this.hovered; }

  /** The member's mark (gens and rank) for a character, null for none. */
  markOf(character: Character): { gens: number; rank: number } | null {
    const member = this.options.gens.members.get(character.id);
    return member && (member.gens === GENS_DUPRIAN || member.gens === GENS_VANERT) ? { gens: member.gens, rank: member.rank } : null;
  }

  private update(): void {
    const character = this.hovered;
    if (!character || !character.group.parent) { this.tag.style.display = 'none'; return; }
    const { camera, canvas } = this.options;
    const rect = canvas.getBoundingClientRect();
    this.box.setFromObject(character.group);
    const head = new THREE.Vector3((this.box.min.x + this.box.max.x) / 2, this.box.max.y, (this.box.min.z + this.box.max.z) / 2).project(camera);
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    Object.assign(this.tag.style, { display: 'block', left: `${rect.left + (head.x + 1) / 2 * rect.width}px`, top: `${rect.top + (1 - head.y) / 2 * rect.height - 4}px` });
    this.name.style.fontSize = `${nativeTextPx()}px`;
    if (this.name.textContent !== character.name) this.name.textContent = character.name;
    const mark = this.markOf(character);
    const sheet = mark ? this.sheets.get(mark.gens) : undefined;
    const cell = mark ? gensMarkCell(mark.rank) : -1;
    if (!mark || !sheet || cell < 0) { this.mark.style.display = 'none'; this.mark.dataset.gens = ''; return; }
    const size = MARK.size * scale;
    Object.assign(this.mark.style, {
      display: 'block', width: `${MARK.width * size}px`, height: `${MARK.height * size}px`, right: '100%', marginRight: '1px',
      backgroundImage: `url("${sheet.url}")`, backgroundSize: `${sheet.width * size}px ${sheet.height * size}px`,
      backgroundPosition: `-${(cell % MARK.columns) * MARK.width * size}px -${Math.floor(cell / MARK.columns) * MARK.height * size}px`,
    });
    this.mark.dataset.gens = String(mark.gens);
  }
}
