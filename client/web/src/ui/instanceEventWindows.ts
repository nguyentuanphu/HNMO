// The windows of Illusion Temple, Doppelganger and Imperial Guardian, the desktop's own RML documents
// (Data/Interface/RmlUi):
//   cursed_temple_enter      CCursedTempleEnter: centred, 80 units down the 640x480 stage
//   cursed_temple_system     CCursedTempleSystem: the time, the mini map, the skill panel, the score, the tutorial
//   cursed_temple_result     CCursedTempleResult: centred, 120 units down
//   message_box_view         CCursedTempleProgressMsgBox: gaining or storing the sacred item (10 s)
//   doppelganger_enter       CDoppelGangerWindow (EventItemEntryView), docked at the right
//   doppelganger_frame       CDoppelGangerFrame, the HUD slot (413, 342)
//   empire_guardian_enter    CEmpireGuardianNPC (EventItemEntryView), docked at the right
//   empire_guardian_timer    CEmpireGuardianTimer, the HUD slot (507, 342)
// setUpInstanceEvents() wires them to game/instanceEvents.ts with one call from main.ts.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { fitPx } from './guildWindows.js';
import { drawItem } from './itemIcons.js';
import {
  InstanceEvents, imperialTimerLines, templeEntry, Team, TempleSkill, T,
  ILLUSION_TEMPLE_MAPS, type TempleResult,
} from '../game/instanceEvents.js';
import { format, type EventHero } from '../game/events.js';
import type { EventSystem } from './eventWindows.js';
import type { Connection } from '../net/connection.js';
import type { Item } from '../game/inventory.js';
import type { Character } from '../game/characters.js';
import { skillInfo, loadSkillData } from '../game/skills.js';
import { textWidth } from '../game/questData.js';
import { PlayerActions } from '../game/combatLook.js';
import type { MapAudioState } from '../audio/index.js';

const PANEL_WIDTH = 190;
const TEXT_PX = 12;
const BIG_TEXT_PX = 15;
const LINE_HEIGHT = 14;

const screenScale = () => window.innerHeight / REFERENCE_HEIGHT;

/** Lays a document's root out as the 640x480 reference screen stretched to the window. */
function placeBoard(doc: RmlDocument): number {
  const scale = screenScale();
  doc.setScale(1);
  Object.assign(doc.root.style, {
    width: `${window.innerWidth / scale}px`, height: `${REFERENCE_HEIGHT}px`, transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none',
  });
  doc.root.style.setProperty('--ui-scale', '1');
  doc.root.style.setProperty('--hud-scale', '1');
  return window.innerWidth / scale;
}

/** A HUD window of the event HUD slots: its .screen centred, its #panel at (x, y) of the 640x480 frame. */
function placeHudPanel(doc: RmlDocument, x: number, y: number): void {
  const width = placeBoard(doc);
  const screen = doc.$('.screen');
  if (screen) Object.assign(screen.style, { left: `${(width - 640) / 2}px`, top: '0px' });
  const panel = doc.$('#panel');
  if (panel) Object.assign(panel.style, { left: `${x}px`, top: `${y}px` });
}

/** A window the stage centres across the 640x480 screen at the original's height (the workspace's .panel-stage). */
function placeCentred(doc: RmlDocument, width: number, top: number): number {
  const panel = doc.$('#panel');
  const scale = screenScale();
  doc.setScale(scale);
  if (panel) {
    Object.assign(panel.style, {
      left: `${(window.innerWidth - width * scale) / 2}px`, top: `${(window.innerHeight - 480 * scale) / 2 + top * scale}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'auto',
    });
  }
  return scale;
}

/** Docked at the right edge, like the other right-docked windows. */
function placeDocked(doc: RmlDocument): number {
  const panel = doc.$('#panel');
  const scale = screenScale();
  doc.setScale(scale);
  if (panel) {
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'auto',
    });
  }
  return scale;
}

const stopPointer = (element: HTMLElement | null) => element?.addEventListener('pointerdown', (event) => event.stopPropagation());

// The original's font at the 640x480 reference measures about four fifths of the web's 12 px one (its
// Doppelganger line "Only those in possession of a" fits CutStr's 135 units; textWidth's would not).
const referenceWidth = (text: string) => textWidth(text) * 0.8;

/** CutStr: the text cut at word ends into lines narrower than `width` - 5 units, each a fresh line of the buffer. */
function cutStr(source: string, width: number, maxLines: number, into: string[]): void {
  let rest = source, done = 0, line = 0;
  while (rest && line < maxLines) {
    if (referenceWidth(rest) >= width - 5) {
      const space = rest.lastIndexOf(' ');
      rest = rest.slice(0, space < 0 ? rest.length - 1 : space);
      continue;
    }
    into[line++] = rest;
    done += rest.length;
    if (done >= source.length) break;
    rest = source.slice(done);
  }
}

// ---------- Illusion Temple: the entry window ----------

interface TempleEnterModel extends RmlModel {
  text_px: number; bold_text_px: number; line_height_px: number; label_line_px: number; title: string; eligible: boolean;
  temple_line: { text: string; text_px: number }; members_line: { text: string; text_px: number }; notice: { text: string; text_px: number };
  bands: { text: string; text_px: number; hero: boolean }[]; enter_text: string; close_text: string;
  temple_enter: () => void; temple_close: () => void;
}

class TempleEntryWindow {
  private doc: RmlDocument | null = null;
  private opening = false;
  private readonly model: TempleEnterModel;

  constructor(private readonly options: { hero: () => EventHero; members: () => number; enter: () => void; onClose: () => void; click: () => void }) {
    this.model = {
      text_px: TEXT_PX, bold_text_px: TEXT_PX, line_height_px: LINE_HEIGHT, label_line_px: LINE_HEIGHT, title: '', eligible: false,
      temple_line: { text: '', text_px: TEXT_PX }, members_line: { text: '', text_px: TEXT_PX }, notice: { text: '', text_px: TEXT_PX },
      bands: [], enter_text: T.Enter, close_text: T.Close,
      temple_enter: () => { this.options.click(); this.options.enter(); },
      temple_close: () => { this.options.click(); this.close(); },
    };
    prepareDocument('cursed_temple_enter').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => { if (event.key === 'Escape' && this.isOpen) this.close(); });
  }

  get isOpen(): boolean { return this.doc !== null || this.opening; }

  async open(): Promise<void> {
    if (this.isOpen) { this.sync(); return; }
    this.opening = true;
    const doc = await openDocument('cursed_temple_enter', this.model, { parent: document.body, scale: screenScale() }).finally(() => { this.opening = false; });
    this.doc = doc;
    doc.host.style.zIndex = '30';
    stopPointer(doc.$('#panel'));
    this.place();
  }

  /** SyncLines, each frame while open (the members count can change). */
  sync(): void {
    if (!this.doc) return;
    const scale = screenScale();
    const px = Math.round(TEXT_PX * scale);
    const content = templeEntry(this.options.hero(), this.options.members());
    const line = (text: string, box: number) => ({ text, text_px: fitPx(text, px, box, scale) });
    Object.assign(this.model, {
      text_px: px, bold_text_px: px, line_height_px: Math.round(LINE_HEIGHT * scale), label_line_px: Math.round(LINE_HEIGHT * scale),
      title: content.title, eligible: content.eligible, temple_line: line(content.templeLine, 220), members_line: line(content.membersLine, 220),
      notice: line(content.notice, 230), bands: content.bands.map((band) => ({ ...line(band.text, 220), hero: band.hero })),
    });
    this.doc.update();
  }

  close(notify = true): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    if (notify) this.options.onClose();
  }

  private place(): void {
    if (!this.doc) return;
    placeCentred(this.doc, 230, 80);
    this.sync();
  }
}

// ---------- Illusion Temple: the HUD ----------

type DigitCells = string[] & { size: number };
/** UI::RmlBridge::DigitCells: a number's digits as cells of a digit strip. */
function digitCells(value: number, width: number, height: number): DigitCells {
  const cells = [...String(value)].map((c) => (c >= '0' && c <= '9' ? `${Number(c) * width} 0 ${width} ${height}` : ''));
  return Object.assign(cells, { size: cells.length });
}

interface TempleSprite { left: number; top: number; width: number; height: number; src: string; rect: string; opacity: number }
interface TempleHudModel extends RmlModel {
  panels_shown: boolean; score_shown: boolean; skill_icon_src: string; skill_icon_rect: string;
  allied_tens_src: string; allied_ones_src: string; illusion_tens_src: string; illusion_ones_src: string;
  allied_two_digits: boolean; illusion_two_digits: boolean;
  minute_digits: DigitCells; second_digits: DigitCells; alpha_digits: DigitCells; allied_digits: DigitCells; illusion_digits: DigitCells;
  kills_needed_digits: DigitCells; kills_digits: DigitCells; sprites: TempleSprite[]; alpha: number;
  tutorial_lines: { text: string; text_px: number; title: boolean }[];
  ct_alpha_click: () => void; ct_skill_up_click: () => void; ct_skill_down_click: () => void;
}

// The mini map's projection (MiniMapPos) from the original's (464, 299).
const MINI_X = [146, 192, 200, 138, 128, 210, 170], MINI_Y = [42, 128, 116, 54, 126, 44, 84];
const MINI_SCALE = 1.56;
const miniX = (x: number, y: number) => (x - (MINI_Y[6] - y)) / MINI_SCALE;
const miniY = (x: number, y: number) => (125 - (y + (MINI_X[6] - x))) / MINI_SCALE;
const IMG = (file: string) => `../../../${file}`;

class TempleHud {
  private doc: RmlDocument | null = null;
  private opening = false;
  private tooltip: HTMLDivElement | null = null;
  private readonly model: TempleHudModel;

  constructor(private readonly events: InstanceEvents, private readonly options: { heroId: () => number; heroCell: () => { x: number; y: number } | null; click: () => void }) {
    const empty = () => digitCells(0, 0, 0).slice(0, 0) as DigitCells;
    this.model = {
      panels_shown: true, score_shown: false, skill_icon_src: '', skill_icon_rect: '',
      allied_tens_src: '', allied_ones_src: '', illusion_tens_src: '', illusion_ones_src: '', allied_two_digits: false, illusion_two_digits: false,
      minute_digits: empty(), second_digits: empty(), alpha_digits: empty(), allied_digits: empty(), illusion_digits: empty(), kills_needed_digits: empty(), kills_digits: empty(),
      sprites: [], alpha: 1, tutorial_lines: [],
      // The transparency button: the buttons half see-through, or opaque again.
      ct_alpha_click: () => { this.options.click(); this.events.temple.alpha = this.events.temple.alpha !== 1 ? 1 : 0.51; },
      ct_skill_up_click: () => { this.options.click(); this.events.temple.stepSkill(true); },
      ct_skill_down_click: () => { this.options.click(); this.events.temple.stepSkill(false); },
    };
    prepareDocument('cursed_temple_system').catch((error: unknown) => console.warn('Interface', error));
    void loadSkillData().catch(() => undefined);
    window.addEventListener('resize', () => { if (this.doc) { placeBoard(this.doc); this.doc.update(); } });
  }

  get isOpen(): boolean { return this.doc !== null; }

  show(on: boolean): void {
    if (!on) { this.doc?.close(); this.doc = null; this.hideTooltip(); return; }
    if (this.doc) { this.sync(); return; }
    if (this.opening) return;
    this.opening = true;
    void openDocument('cursed_temple_system', this.model, { parent: document.body }).then((doc) => {
      if (!this.events.temple.shown) { doc.close(); return; }
      this.doc = doc;
      // Behind the other windows (the original drew it under nearly every panel).
      doc.host.style.zIndex = '11';
      doc.host.style.pointerEvents = 'none';
      for (const id of ['#ct_hud_area', '#ct_hint_skill', '#ct_hint_kills_needed', '#ct_hint_kills']) {
        const area = doc.$(id);
        if (!area) continue;
        area.style.pointerEvents = 'auto';
        stopPointer(area);
      }
      for (const [id, lines] of [['#ct_hint_skill', 'skill'], ['#ct_hint_kills_needed', 'needed'], ['#ct_hint_kills', 'kills']] as const) {
        const zone = doc.$(id);
        zone?.addEventListener('pointerenter', () => this.showTooltip(zone, lines));
        zone?.addEventListener('pointerleave', () => this.hideTooltip());
      }
      for (const button of doc.root.querySelectorAll<HTMLElement>('.ct-btn')) stopPointer(button);
      placeBoard(doc);
      this.sync();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = false; });
  }

  /** SyncView, each frame. */
  sync(): void {
    if (!this.doc) return;
    const temple = this.events.temple;
    const model = this.model;
    const skill = skillInfo(temple.skill);
    const killCount = skill?.killCount ?? 0;
    model.minute_digits = digitCells(Math.floor(temple.time / 60), 12, 14);
    model.second_digits = digitCells(temple.time % 60, 12, 14);
    model.skill_icon_src = IMG(temple.skillPoints >= killCount ? 'newui_skill2.jpg' : 'newui_non_skill2.jpg');
    model.skill_icon_rect = `${(8 + (temple.skill - TempleSkill.PROTECTION)) * 20} 0 20 28`;
    model.kills_needed_digits = digitCells(killCount, 12, 14);
    model.kills_digits = digitCells(temple.skillPoints, 12, 14);
    // SyncMiniMap: the generals and storages, the party, the sacred item's carrier, the hero.
    const sprites: TempleSprite[] = [];
    const add = (left: number, top: number, w: number, h: number, file: string) => sprites.push({ left, top, width: w, height: h, src: IMG(file), rect: `0 0 ${w} ${h}`, opacity: 1 });
    const marker = (x: number, y: number, w: number, h: number, file: string) => add(miniX(x, y), miniY(x, y), w, h, file);
    marker(138, 44, 9, 9, 'newui_ctminmap_TeamB_npc.tga');
    marker(138, 58, 9, 8, 'newui_ctminmap_TeamB_box.tga');
    marker(192, 113, 9, 8, 'newui_ctminmap_TeamA_box.tga');
    marker(193, 126, 9, 9, 'newui_ctminmap_TeamA_npc.tga');
    const heroId = this.options.heroId();
    for (const mate of temple.party) {
      if (mate.id === heroId || mate.id === temple.carrier) continue;
      add(miniX(mate.x, mate.y) - 3, miniY(mate.x, mate.y) - 3, 7, 7, temple.myTeam === Team.ALLIED ? 'newui_ctminmap_TeamB_member.tga' : 'newui_ctminmap_TeamA_member.tga');
    }
    if (temple.carrier !== 0xffff && temple.carrier !== heroId) add(miniX(temple.carrierX, temple.carrierY) - 5, miniY(temple.carrierX, temple.carrierY) - 5, 14, 14, 'newui_ctminmap_Relic.tga');
    const cell = this.options.heroCell();
    if (cell) add(miniX(cell.x, cell.y) - 4, miniY(cell.x, cell.y) - 4, 11, 11, 'newui_ctminmap_Hero.tga');
    model.sprites = sprites;
    model.alpha_digits = digitCells(Math.floor(temple.alpha * 100), 16, 16);
    model.allied_digits = digitCells(temple.allied, 16, 16);
    model.illusion_digits = digitCells(temple.illusion, 16, 16);
    model.alpha = temple.alpha;
    // SyncScore: each team's points in big digits while the score effect shows.
    model.score_shown = temple.score.active;
    const digit = (team: string, value: number) => IMG(`newui_ctscore${team}num${value}.tga`);
    model.allied_two_digits = Math.floor(temple.allied / 10) !== 0;
    model.illusion_two_digits = Math.floor(temple.illusion / 10) !== 0;
    model.allied_tens_src = digit('allied', Math.floor(temple.allied / 10) % 10);
    model.allied_ones_src = digit('allied', temple.allied % 10);
    model.illusion_tens_src = digit('illusion', Math.floor(temple.illusion / 10) % 10);
    model.illusion_ones_src = digit('illusion', temple.illusion % 10);
    // SyncTutorialStep: the step's title and three lines (the second row left empty).
    model.tutorial_lines = temple.tutorial.active
      ? T.TutorialSteps[temple.tutorial.step].map((text, i) => ({ text, text_px: fitPx(text, TEXT_PX, 300, 1), title: i === 0 }))
      : [];
    this.doc.update();
  }

  // The three hover tooltips of SyncSkill, 20 units above the hovered zone.
  private showTooltip(zone: HTMLElement, kind: 'skill' | 'needed' | 'kills'): void {
    this.hideTooltip();
    const temple = this.events.temple;
    const tip = document.createElement('div');
    tip.className = 'hnmo-temple-tooltip';
    Object.assign(tip.style, {
      position: 'fixed', zIndex: '96', pointerEvents: 'none', background: 'rgba(0, 0, 0, 0.8)', border: '1px solid #555',
      padding: '3px 6px', font: '12px "DejaVu Sans", Tahoma, sans-serif', textAlign: 'center', whiteSpace: 'nowrap',
    });
    const row = (text: string, color: string) => { const div = document.createElement('div'); div.textContent = text; div.style.color = color; tip.append(div); };
    if (kind === 'skill') {
      row(skillInfo(temple.skill)?.name ?? '', '#64aaff');
      row(' ', '#fff');
      row(T.SkillDescriptions[temple.skill - TempleSkill.PROTECTION] ?? '', '#5078c8');
    } else {
      row(kind === 'needed' ? T.RequiredKillPoint : T.AchievedKillPoint, '#fff');
    }
    document.body.append(tip);
    const box = zone.getBoundingClientRect();
    tip.style.left = `${box.left + box.width / 2 - tip.offsetWidth / 2}px`;
    tip.style.top = `${box.top - 20 * screenScale() - tip.offsetHeight}px`;
    this.tooltip = tip;
  }

  private hideTooltip(): void {
    this.tooltip?.remove();
    this.tooltip = null;
  }
}

// ---------- Illusion Temple: the result ----------

const CLASS_NAMES: string[][] = [
  ['Dark Wizard', 'Soul Master', 'Grand Master'], ['Dark Knight', 'Blade Knight', 'Blade Master'], ['Elf', 'Muse Elf', 'High Elf'],
  ['Magic Gladiator', 'Magic Gladiator', 'Dual Master'], ['Dark Lord', 'Dark Lord', 'Lord Emperor'],
  ['Summoner', 'Bloody Summoner', 'Dimension Master'], ['Rage Fighter', 'Rage Fighter', 'Fist Master'],
];

interface TempleResultRow { team: string; name: string; class_name: string; added_exp: string; point: string; hero: boolean }
interface TempleResultModel extends RmlModel {
  text_px: number; line_height_px: number; label_line_px: number;
  hero_list_label: { text: string; text_px: number }; column_header: { text: string; text_px: number };
  camp_label: string; character_label: string; class_label: string; exp_label: string; point_label: string;
  reward_hint: { text: string; text_px: number }; allied_rows: TempleResultRow[]; illusion_rows: TempleResultRow[];
  close_text: string; banner: number; banner_alpha: number; result_close: () => void;
}

class TempleResultWindow {
  private doc: RmlDocument | null = null;
  private opening = false;
  private fade = 0;
  private readonly model: TempleResultModel;

  constructor(private readonly options: { heroName: string; onClose: () => void; click: () => void }) {
    this.model = {
      text_px: TEXT_PX, line_height_px: LINE_HEIGHT, label_line_px: LINE_HEIGHT,
      hero_list_label: { text: '', text_px: TEXT_PX }, column_header: { text: '', text_px: TEXT_PX },
      camp_label: T.Camp, character_label: T.Character, class_label: T.Class, exp_label: T.EXP, point_label: T.Point,
      reward_hint: { text: '', text_px: TEXT_PX }, allied_rows: [], illusion_rows: [], close_text: T.Close, banner: 0, banner_alpha: 0,
      result_close: () => { this.options.click(); this.close(); },
    };
    prepareDocument('cursed_temple_result').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => { if (event.key === 'Escape' && this.doc) this.close(); });
  }

  get isOpen(): boolean { return this.doc !== null; }

  async show(result: TempleResult): Promise<void> {
    this.fade = 0;
    const rows = (team: number, point: number): TempleResultRow[] => result.players.filter((p) => (team === Team.ALLIED ? p.team === Team.ALLIED : p.team !== Team.ALLIED)).map((p) => ({
      team: p.team === Team.ALLIED ? T.MUAlliance : T.IllusionSorcery, name: p.name, class_name: CLASS_NAMES[p.classLine]?.[p.classStep] ?? '',
      added_exp: String(p.experience), point: String(point), hero: p.name === this.options.heroName,
    }));
    this.model.allied_rows = rows(Team.ALLIED, result.allied);
    this.model.illusion_rows = rows(Team.ILLUSION, result.illusion);
    this.model.banner = result.winState;
    if (this.doc || this.opening) { this.sync(); return; }
    this.opening = true;
    const doc = await openDocument('cursed_temple_result', this.model, { parent: document.body, scale: screenScale() }).finally(() => { this.opening = false; });
    this.doc = doc;
    doc.host.style.zIndex = '31';
    stopPointer(doc.$('#panel'));
    this.place();
    const step = () => {
      if (!this.doc) return;
      // UpdateResult: the banner fades in; under the alpha test until past a quarter.
      if (this.model.banner) this.fade = Math.min(1, this.fade + 0.015 * 2);
      this.sync();
      requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  private sync(): void {
    if (!this.doc) return;
    const scale = screenScale();
    const px = Math.round(TEXT_PX * scale);
    const line = (text: string) => ({ text, text_px: fitPx(text, px, 230, scale) });
    Object.assign(this.model, {
      text_px: px, line_height_px: Math.round(LINE_HEIGHT * scale), label_line_px: Math.round(LINE_HEIGHT * scale),
      hero_list_label: line(T.HeroList), column_header: line(`  ${T.Camp}           ${T.Character}        ${T.Class}     ${T.EXP}    ${T.Point}`),
      reward_hint: line(T.YouMayBeCompensatedByClickingOnTheCloseButton), banner_alpha: this.fade > 0.25 ? this.fade : 0,
    });
    this.doc.update();
  }

  /** ClosingProcess: the reward request. */
  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    this.options.onClose();
  }

  private place(): void {
    if (!this.doc) return;
    placeCentred(this.doc, 230, 120);
    this.sync();
  }
}

// ---------- Illusion Temple: gaining and storing the sacred item ----------

interface ProgressModel extends RmlModel {
  kind: string; text_px: number; bold_text_px: number; line_height: number; bold_line_height: number;
  strips: { kind: string }[]; lines: { role: string; bold: boolean; cells: { text: string; bold: boolean; color: string; text_px: number }[] }[];
  buttons: { label: string; index: number; enabled: boolean; ok_art: boolean }[];
  progress_shown: boolean; progress_width: number; list_shown: boolean; list_rows: { text: string; index: number; selected: boolean }[];
  message_box_button: (index: number) => void;
}

/** CCursedTempleProgressMsgBox: ten seconds standing still, then the NPC is talked to; moving cancels it. */
class SacredItemProgress {
  private doc: RmlDocument | null = null;
  private start = 0;
  private npc = 0;
  private frame = 0;
  private readonly model: ProgressModel = {
    kind: 'progress', text_px: TEXT_PX, bold_text_px: TEXT_PX, line_height: LINE_HEIGHT, bold_line_height: LINE_HEIGHT, strips: [], lines: [], buttons: [],
    progress_shown: true, progress_width: 0, list_shown: false, list_rows: [], message_box_button: () => undefined,
  };

  constructor(private readonly options: { busy: () => boolean; shown: () => boolean; complete: (npc: number) => void; click: () => void }) {
    prepareDocument('message_box_view').catch((error: unknown) => console.warn('Interface', error));
  }

  get isOpen(): boolean { return this.doc !== null; }

  async open(npcId: number, text: string): Promise<void> {
    if (this.doc) return;
    this.npc = npcId;
    this.start = performance.now();
    this.model.lines = [{ role: 'message', bold: false, cells: [{ text, bold: false, color: 'rgb(255, 255, 255)', text_px: 0 }] }];
    this.model.progress_width = 0;
    const doc = await openDocument('message_box_view', this.model, { parent: document.body });
    this.doc = doc;
    doc.host.style.zIndex = '90';
    placeBoard(doc);
    const step = () => {
      if (!this.doc) return;
      const elapsed = performance.now() - this.start;
      this.model.progress_width = Math.min(150, (150 * elapsed) / 10_000);
      this.doc.update();
      if (elapsed >= 10_000) { this.close(); this.options.complete(this.npc); return; }
      if (this.options.busy() || !this.options.shown()) { this.close(); return; }
      this.frame = requestAnimationFrame(step);
    };
    this.frame = requestAnimationFrame(step);
  }

  close(): void {
    if (!this.doc) return;
    cancelAnimationFrame(this.frame);
    this.options.click();
    this.doc.close();
    this.doc = null;
  }
}

// ---------- Doppelganger and Imperial Guardian: the entry windows ----------

interface ItemEntryModel extends RmlModel {
  text_px: number; bold_text_px: number; input_value: string;
  texts: { text: string; text_px: number }[];
  buttons: { label: string; locked: boolean; bold: boolean; label_line_px: number; hint: string }[];
  item_entry_press: (index: number) => void; window_close: () => void;
}

interface EntryText { text: string; width: number; bold?: boolean }

/** EventItemEntryView: the docked frame, the entry item's 3D preview, the texts and the two buttons. */
class ItemEntryWindow {
  private doc: RmlDocument | null = null;
  private opening = false;
  private readonly model: ItemEntryModel;

  constructor(private readonly options: {
    document: string; item: { group: number; number: number };
    texts: () => EntryText[]; enterLocked: () => boolean; enter: () => void; onClose: () => void; click: () => void;
  }) {
    this.model = {
      text_px: TEXT_PX, bold_text_px: TEXT_PX, input_value: '', texts: [], buttons: [],
      item_entry_press: (index) => this.press(index),
      window_close: () => { this.options.click(); this.close(); },
    };
    prepareDocument(options.document).catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => { if (event.key === 'Escape' && this.doc) { this.close(); this.options.click(); } });
  }

  get isOpen(): boolean { return this.doc !== null || this.opening; }

  async open(): Promise<void> {
    if (this.isOpen) { this.sync(); return; }
    this.opening = true;
    const doc = await openDocument(this.options.document, this.model, { parent: document.body, scale: screenScale() }).finally(() => { this.opening = false; });
    this.doc = doc;
    doc.host.style.zIndex = '30';
    stopPointer(doc.$('#panel'));
    this.place();
  }

  private press(index: number): void {
    const button = this.model.buttons[index];
    if (!button || button.locked) return;
    this.options.click();
    if (index === 0) this.options.enter();
    else this.close();
  }

  /** SyncView: the texts shrunk to their boxes, Enter locked or not. */
  sync(): void {
    if (!this.doc) return;
    const scale = screenScale();
    const px = Math.round(TEXT_PX * scale);
    this.model.text_px = px;
    this.model.bold_text_px = px;
    this.model.texts = this.options.texts().map((t) => ({ text: t.text, text_px: fitPx(t.text, px, t.width, scale) }));
    const lineHeight = Math.round(LINE_HEIGHT * scale);
    this.model.buttons = [
      { label: T.Enter, locked: this.options.enterLocked(), bold: false, label_line_px: lineHeight, hint: '' },
      { label: T.Close, locked: false, bold: false, label_line_px: lineHeight, hint: '' },
    ];
    this.doc.update();
    // The locked look (image-color #646464).
    for (const button of this.doc.root.querySelectorAll<HTMLElement>('.entry-btn')) button.style.filter = button.classList.contains('locked') ? 'brightness(0.39)' : '';
  }

  /** ClosingProcess. */
  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    this.options.onClose();
  }

  private place(): void {
    if (!this.doc) return;
    const scale = placeDocked(this.doc);
    this.sync();
    // The entry item, drawn in its box.
    const box = this.doc.$('.entry-item-box');
    if (!box) return;
    let canvas = box.querySelector('canvas');
    if (!canvas) { canvas = document.createElement('canvas'); Object.assign(canvas.style, { width: '100%', height: '100%', display: 'block' }); box.append(canvas); }
    canvas.width = Math.max(1, Math.round(20 * scale * devicePixelRatio));
    canvas.height = Math.max(1, Math.round(27 * scale * devicePixelRatio));
    const item: Item = {
      group: this.options.item.group, number: this.options.item.number, level: 0, durability: 1, luck: false, skill: false, guardian: false,
      optionLevel: 0, optionType: 0, excellent: 0, ancientSet: 0, ancientBonus: 0, harmony: 0, sockets: [], socketBonus: 0, length: 0, width: 1, height: 1,
    };
    void drawItem(canvas, item).catch(() => false);
  }
}

// ---------- Doppelganger: the frame ----------

interface DoppelgangerModel extends RmlModel {
  passed_line: { text: string; text_px: number }; passed_state: string; time_label: { text: string; text_px: number }; time_line: { text: string; text_px: number };
  bars: { src: string; rect: string; left: number; width: number }[]; ice_walker_visible: boolean; ice_walker_left: number;
  markers: { left: number; hero: boolean }[];
}

class DoppelgangerHud {
  private doc: RmlDocument | null = null;
  private opening = false;
  private readonly model: DoppelgangerModel = {
    passed_line: { text: '', text_px: TEXT_PX }, passed_state: 'none', time_label: { text: '', text_px: TEXT_PX }, time_line: { text: '', text_px: BIG_TEXT_PX },
    bars: [], ice_walker_visible: false, ice_walker_left: 0, markers: [],
  };

  constructor(private readonly events: InstanceEvents, private readonly heroId: () => number) {
    prepareDocument('doppelganger_frame').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => { if (this.doc) { placeHudPanel(this.doc, 413, 342); this.doc.update(); } });
  }

  get isOpen(): boolean { return this.doc !== null; }

  show(on: boolean): void {
    if (!on) { this.doc?.close(); this.doc = null; return; }
    if (this.doc) { this.sync(); return; }
    if (this.opening) return;
    this.opening = true;
    void openDocument('doppelganger_frame', this.model, { parent: document.body }).then((doc) => {
      if (!this.events.doppelganger.shown) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '12';
      doc.host.style.pointerEvents = 'none';
      placeHudPanel(doc, 413, 342);
      this.sync();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = false; });
  }

  /** SyncView, each frame. */
  private sync(): void {
    if (!this.doc) return;
    const frame = this.events.doppelganger;
    frame.step();
    const entry = (text: string, px: number) => ({ text, text_px: fitPx(text, px, 110, 1) });
    this.model.passed_state = frame.entered === 1 ? 'one' : frame.entered >= 2 ? 'several' : 'none';
    this.model.passed_line = entry(format(T.MonstersPassedDD, frame.entered, frame.max), TEXT_PX);
    this.model.time_label = entry(T.TimeLeft, TEXT_PX);
    const hundredths = frame.timerStopped ? 0 : 99 - (Math.floor(performance.now()) % 100);
    this.model.time_line = entry(format('%.2d:%.2d:%.2d', Math.floor(frame.time / 60), frame.time % 60, hundredths), BIG_TEXT_PX);
    // The gauge: yellow up to the first monster, then yellow under orange, then orange under red.
    const piece = (file: string, fill: number) => ({ src: IMG(file), rect: `${165 * (1 - fill)} 0 ${165 * fill} 6`, left: 59 + 167 * (1 - fill), width: 167 * fill });
    const yellow = 'Double_bar(Y).jpg', orange = 'Double_bar(O).jpg', red = 'Double_bar(R).jpg';
    this.model.bars = frame.entered === 0 ? [piece(yellow, frame.gauge)]
      : frame.entered === 1 ? [piece(yellow, 1), piece(orange, frame.gauge)] : [piece(orange, 1), piece(red, frame.gauge)];
    this.model.ice_walker_visible = frame.iceWalker.enabled;
    this.model.ice_walker_left = 59 - 6.5 + 167 * frame.iceWalker.position;
    const hero = this.heroId();
    this.model.markers = [...frame.party].filter(([, m]) => m.received !== -1).map(([id, m]) => ({ left: 59 - 4.5 + 167 * m.position, hero: id === hero }));
    this.doc.update();
  }
}

// ---------- Imperial Guardian: the timer ----------

interface TimerModel extends RmlModel {
  text_px: number; big_text_px: number;
  kills_text: string; kills_state: string; time_left_text: string; time_left_state: string; time_text: string; time_state: string;
}

class ImperialTimerHud {
  private doc: RmlDocument | null = null;
  private opening = false;
  private readonly model: TimerModel = {
    text_px: TEXT_PX, big_text_px: BIG_TEXT_PX, kills_text: '', kills_state: 'plain', time_left_text: '', time_left_state: 'standby', time_text: '', time_state: 'normal',
  };

  constructor(private readonly events: InstanceEvents) {
    prepareDocument('empire_guardian_timer').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => { if (this.doc) { placeHudPanel(this.doc, 507, 342); this.doc.update(); } });
  }

  get isOpen(): boolean { return this.doc !== null; }

  show(on: boolean): void {
    if (!on) { this.doc?.close(); this.doc = null; return; }
    if (this.doc) { this.sync(); return; }
    if (this.opening) return;
    this.opening = true;
    void openDocument('empire_guardian_timer', this.model, { parent: document.body }).then((doc) => {
      if (!this.events.imperial.shown) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '12';
      doc.host.style.pointerEvents = 'none';
      placeHudPanel(doc, 507, 342);
      this.sync();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = false; });
  }

  private sync(): void {
    if (!this.doc) return;
    const lines = imperialTimerLines(this.events.imperial);
    // Each line centred on 110 units and shrunk to them (they share one size here).
    const px = Math.min(fitPx(lines.round, TEXT_PX, 110, 1), fitPx(lines.caption, TEXT_PX, 110, 1));
    Object.assign(this.model, {
      text_px: px, big_text_px: fitPx(lines.time, BIG_TEXT_PX, 110, 1),
      kills_text: lines.round, kills_state: 'plain', time_left_text: lines.caption, time_left_state: lines.captionState, time_text: lines.time, time_state: lines.timeState,
    });
    this.doc.update();
  }
}

// ---------- wiring ----------

/** What setUpInstanceEvents needs of the game (main.ts). */
export interface InstanceEventOptions {
  connection: Connection;
  /** The OK boxes of the other events (generic_confirm_dialog.rml, one at a time). */
  events: EventSystem;
  heroName: string;
  hero: () => EventHero;
  heroCharacter: () => Character | null;
  heroId: () => number;
  characters: () => Iterable<Character>;
  mapNumber: () => number | null;
  message: (text: string, kind?: string) => void;
  audio: { play: (id: string) => void };
  /** The music rule's event part (the temple's state, the Doppelganger running). */
  sounds: { eventMusicState: () => Partial<MapAudioState> };
  /** HideAll: the inventory, the character, party and NPC windows. */
  closeOthers: () => void;
  bag: { interceptRightClick?: (slot: number, item: Item) => boolean };
  inventory: { openWindow: number | null; items: Map<number, Item> };
  /** The NPC talk (OnlineWorld.interceptTalk) and its request. */
  online: { interceptTalk?: (npc: Character) => boolean };
  /** The player under the pointer (Shift + right click's target). */
  pickPlayer: (event: MouseEvent) => Character | null;
}

export interface InstanceEventSystem {
  events: InstanceEvents;
  templeEntry: TempleEntryWindow;
  templeHud: TempleHud;
  templeResult: TempleResultWindow;
  sacredItem: SacredItemProgress;
  doppelgangerEntry: ItemEntryWindow;
  doppelgangerHud: DoppelgangerHud;
  imperialEntry: ItemEntryWindow;
  imperialTimer: ImperialTimerHud;
}

const MERCHANT_OR_VAULT = [0, 1, 2];
const FIRST_BAG_SLOT = 12;
const NPC_WINDOWS = [0x14, 0x23, 0x24];

export function setUpInstanceEvents(options: InstanceEventOptions): InstanceEventSystem {
  const click = () => options.audio.play('SOUND_CLICK01');
  const closeNpc = () => {
    options.connection.send('ClientToServer', 'CloseNpcRequest', {});
    if (options.inventory.openWindow !== null && NPC_WINDOWS.includes(options.inventory.openWindow)) options.inventory.openWindow = null;
  };
  let windows: InstanceEventSystem | null = null;
  const closeEntries = () => {
    windows?.templeEntry.close(false);
    windows?.doppelgangerEntry.close();
    windows?.imperialEntry.close();
  };
  const events = new InstanceEvents(options.connection, {
    mapNumber: options.mapNumber, hero: options.hero, heroId: options.heroId,
    ok: (lines) => { void options.events.dialogs.ask(lines, false); },
    message: (text, error = false) => options.message(text, error ? 'error' : 'system'),
    openEntry: (kind) => {
      options.closeOthers();
      if (kind === 'illusionTemple') void windows?.templeEntry.open();
      else if (kind === 'doppelganger') void windows?.doppelgangerEntry.open();
      else void windows?.imperialEntry.open();
    },
    closeWindows: () => { options.closeOthers(); closeEntries(); },
    play: (id) => options.audio.play(id),
    skillLook: (skill, source, target) => {
      // ResolveSkill: the user's PLAYER_ATTACK_REMOVAL (the hero's was set on the request); sublimation shocks its target.
      const find = (id: number) => (id === options.heroId() ? options.heroCharacter() : [...options.characters()].find((c) => c.id === id) ?? null);
      const user = find(source);
      if (user && source !== options.heroId()) user.playOnce(PlayerActions.ATTACK_REMOVAL);
      if (skill === TempleSkill.SUBLIMATION) find(target)?.playOnce(PlayerActions.SHOCK);
    },
  });
  const templeEntryWindow = new TempleEntryWindow({ hero: options.hero, members: () => events.temple.entryCount, enter: () => events.enterTemple(), onClose: closeNpc, click });
  const templeHud = new TempleHud(events, { heroId: options.heroId, heroCell: () => options.heroCharacter()?.cell ?? null, click });
  const templeResult = new TempleResultWindow({ heroName: options.heroName, onClose: () => events.requestTempleReward(), click });
  const sacredItem = new SacredItemProgress({
    busy: () => options.heroCharacter()?.isWalking ?? false,
    shown: () => events.temple.shown,
    complete: (npc) => options.connection.send('ClientToServer', 'TalkToNpcRequest', { NpcId: npc }),
    click,
  });
  const doppelgangerEntry = new ItemEntryWindow({
    document: 'doppelganger_enter', item: { group: 14, number: 111 },
    texts: () => {
      // The two descriptions are cut into one buffer, the second over the first (the desktop's own).
      const cut: string[] = ['', ''];
      cutStr(T.OnlyThoseInPossessionOfAMirrorOfDimensions, 140, 2, cut);
      const first = [...cut];
      cutStr(T.MayPassThroughTheDoppelgangerGate, 100, 2, cut);
      const time = events.doppelgangerEntry.time;
      return [
        { text: T.Lugard, width: 160, bold: true }, { text: first[0], width: 190 }, { text: first[1], width: 190 },
        { text: cut[0], width: 190 }, { text: cut[1], width: 190 }, { text: T.WillYouShowMeYourMirror, width: 190 },
        { text: T.MirrorOfDimensions, width: 190, bold: true }, { text: T.EntryTime, width: 190 },
        { text: time === 0 ? T.YouMayNowEnter : format(T.EnterAfterDMinutes, time), width: 190 },
      ];
    },
    enterLocked: () => events.doppelgangerEntry.locked,
    enter: () => events.enterDoppelganger(),
    onClose: closeNpc,
    click,
  });
  const imperialEntry = new ItemEntryWindow({
    document: 'empire_guardian_enter', item: { group: 14, number: 102 },
    texts: () => {
      const cut: string[] = ['', ''];
      cutStr(T.YouCannotEnterTheFortressOfEmpireGuardians, 150, 2, cut);
      const first = [...cut];
      cutStr(T.YouCanOnlyEnterAsAMemberOfAParty, 155, 2, cut);
      return [
        { text: T.JerintTheAssistant, width: 110, bold: true }, { text: T.WithoutGaionSOrder, width: 190 }, { text: first[0], width: 190 },
        { text: first[1], width: 190 }, { text: T.WillYouShowMeTheOrder, width: 190 }, { text: T.GaionSOrder, width: 110, bold: true },
        { text: T.Warning, width: 110, bold: true }, { text: T.TheRound7MapSundayCanOnly, width: 200 }, { text: T.BeAccessedIfYouHaveA, width: 200 },
        { text: T.CompleteSecromicon, width: 200 }, { text: cut[0], width: 200 }, { text: cut[1], width: 200 },
      ];
    },
    enterLocked: () => false,
    enter: () => events.enterImperialGuardian(),
    // CEmpireGuardianNPC::ClosingProcess sends nothing; the server's NPC window is left open as the desktop leaves it.
    onClose: () => { if (options.inventory.openWindow === 0x24) options.inventory.openWindow = null; },
    click,
  });
  const doppelgangerHud = new DoppelgangerHud(events, options.heroId);
  const imperialTimer = new ImperialTimerHud(events);
  windows = { events, templeEntry: templeEntryWindow, templeHud, templeResult, sacredItem, doppelgangerEntry, doppelgangerHud, imperialEntry, imperialTimer };

  // The tickets in the bag (not while a merchant or the vault is open).
  const previous = options.bag.interceptRightClick;
  options.bag.interceptRightClick = (slot, item) => {
    if (previous?.(slot, item)) return true;
    if (slot < FIRST_BAG_SLOT || (options.inventory.openWindow !== null && MERCHANT_OR_VAULT.includes(options.inventory.openWindow))) return false;
    return events.useTicket(item);
  };

  // The temple's own NPCs (CheckTalkProgressNpc).
  const previousTalk = options.online.interceptTalk;
  options.online.interceptTalk = (npc) => {
    if (previousTalk?.(npc)) return true;
    if (npc.monsterNumber === null) return false;
    // ITEM_CURSED_CASTLE_WATER, the sacred item, in the bag.
    const sacred = [...options.inventory.items.values()].some((item) => item.group === 14 && item.number === 64);
    const answer = events.templeTalk(npc.monsterNumber, sacred, options.events.dialogs.isOpen);
    if (answer === 'gain') void sacredItem.open(npc.id, T.YouAreCurrentGainingTheSacredItem);
    else if (answer === 'store') void sacredItem.open(npc.id, T.YouAreCurrentlyStoringTheSacredItem);
    return answer !== null;
  };

  // The temple skills: the wheel steps them, Shift + right click uses the current one (on the HUD).
  const inTemple = () => events.temple.shown && ILLUSION_TEMPLE_MAPS.includes(options.mapNumber() ?? -1);
  window.addEventListener('wheel', (event) => {
    if (!inTemple() || !(event.target instanceof HTMLCanvasElement)) return;
    event.preventDefault();
    event.stopPropagation();
    events.temple.stepSkill(event.deltaY < 0);
  }, { capture: true, passive: false });
  window.addEventListener('pointerdown', (event) => {
    if (event.button !== 2 || !event.shiftKey || !inTemple() || !(event.target instanceof HTMLCanvasElement)) return;
    event.stopPropagation();
    const hero = options.heroCharacter();
    const skill = skillInfo(events.temple.skill);
    if (!hero) return;
    const target = options.pickPlayer(event);
    if (events.useTempleSkill(target ? { id: target.id, cell: target.cell } : null, hero.cell, skill?.killCount ?? 0, skill?.distance ?? 0)) {
      hero.playOnce(PlayerActions.ATTACK_REMOVAL);
    }
  }, { capture: true });

  // CursedTemple::PlayBGM and CGMDoppelGanger1::PlayBGM (the end keeps the game's track playing).
  const TEMPLE_MUSIC = ['none', 'wait', 'ready', 'play', 'play'] as const;
  options.sounds.eventMusicState = () => ({ cursedTempleState: TEMPLE_MUSIC[events.temple.state] ?? 'none', doppelGangerEnabled: events.doppelganger.enabled });

  // Every frame, as the desktop updates them.
  let last = performance.now();
  const frame = () => {
    const now = performance.now();
    events.syncMap();
    events.temple.step(now, ((now - last) / 1000) * 25);
    last = now;
    if (templeEntryWindow.isOpen) templeEntryWindow.sync();
    templeHud.show(events.temple.shown);
    if (events.temple.result && !templeResult.isOpen) void templeResult.show(events.temple.result);
    doppelgangerHud.show(events.doppelganger.shown);
    imperialTimer.show(events.imperial.shown);
    if (doppelgangerEntry.isOpen) doppelgangerEntry.sync();
    requestAnimationFrame(frame);
  };
  requestAnimationFrame(frame);
  return windows;
}
