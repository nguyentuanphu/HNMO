// The NPC dialogue and quest windows, the game's own RML documents docked at the right edge like the desktop's:
//  - npc_dialogue.rml (CNPCDialogue, UI/NPCs/NPCDialogue.cpp): the NPC's words with paging, then its answers (or,
//    after "Accept a quest", the NPC's quests) with paging; an answer leads to another state or does what the
//    desktop does (901 AvailableQuestsRequest, 902 NpcBuffRequest, 903-907 the gens requests); a quest is chosen with
//    QuestSelectRequest.
//  - quest_progress.rml / quest_progress_etc.rml (CQuestProgress, CQuestProgressByEtc): a quest step's words, the
//    player's answers (QuestProceedRequest), or the requirements and rewards with the OK button
//    (QuestCompletionRequest).
//  - my_quest_info.rml (CMyQuestInfoWindow, T): the hero's quests, the selected one's summary, requirements and
//    rewards (QuestStateRequest), giving up (QuestCancelRequest after the question), the class change and
//    castle/temple tabs.
//  - npc_quest.rml (CNPCQuest): the class change quests' NPC window: the dialog pages and answers, the conditions with
//    the kill counts, the cost, "Proceed with quest" (LegacyQuestStateSetRequest).
// The quest state and the packets are game/quests.ts.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { confirmInGame } from './confirmDialog.js';
import { formatGold, goldTier } from './itemWindows.js';
import type { Connection } from '../net/connection.js';
import type { Item } from '../game/inventory.js';
import { itemName } from '../game/items.js';
import { loadTooltipData } from '../game/itemTooltip.js';
import { divideStringByPixel, loadQuestScripts, reduceStringByPixel, setMeasureFont } from '../game/questData.js';
import { LEGACY_DIALOGS, type LegacyDialog } from '../game/legacyQuestDialog.generated.js';
import { QuestLog, formatText, monsterName, type QuestEvents, type QuestHero, type RewardRow } from '../game/quests.js';

const PANEL_WIDTH = 190;

// The desktop's texts (Localization/Game.en.resx).
const T = {
  Close: 'Close', OK: 'OK', Exit: 'Exit', Quest: 'Quest', ChangeClass: 'Change Class', CastleTemple: 'Castle/Temple',
  StartQuest: 'Start Quest', GiveUpQuest: 'Give Up Quest', NoActiveQuests: 'There are no active quests.',
  GiveUp: 'If you give up, you will not be able to continue with this or any related quests. Do you really want to give up?',
  QuestUnfinished: 'Quest: Unfinished', QuestInProgress: 'Quest: In Progress', QuestCompleted: 'Quest: Completed',
  BloodCastle: 'Blood Castle', IllusionTemple: 'Illusion Temple', EntranceAllowed: 'Entrance is allowed for %d times',
  EnterPerDay: 'You may enter only %d times per day.', ProceedWithQuest: 'Proceed with quest', Cost: 'Cost',
  GainContribution: 'Contribution: %lu',
};

/** A docked RML window: opened on demand, placed at the right edge (`column` panels from it), scaled to the screen. */
class DockedDocument<M extends RmlModel & { text_px: number }> {
  doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  column = 0;

  constructor(private readonly name: string, readonly model: M, private readonly rootSelector = '#panel', private readonly parent: HTMLElement = document.body) {
    prepareDocument(name).catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
  }

  get isOpen(): boolean { return this.doc !== null; }

  async open(): Promise<void> {
    if (this.doc) { this.update(); return; }
    this.opening ??= (async () => {
      const doc = await openDocument(this.name, this.model, { parent: this.parent, scale: window.innerHeight / REFERENCE_HEIGHT });
      if (this.doc) { doc.close(); return; }
      this.doc = doc;
      doc.host.style.zIndex = '30';
      const root = doc.$(this.rootSelector);
      if (root) {
        // The themes leave pointer events to the game (the desktop does its hit tests in C++).
        root.style.pointerEvents = 'auto';
        root.addEventListener('pointerdown', (event) => event.stopPropagation());
        // The texts wrap at the font the theme draws them with.
        setMeasureFont(getComputedStyle(doc.$('.sharp-text') ?? root).fontFamily);
      }
      this.place();
    })().finally(() => { this.opening = null; });
    await this.opening;
  }

  close(): void {
    this.doc?.close();
    this.doc = null;
  }

  update(): void { this.doc?.update(); }

  place(): void {
    if (!this.doc) return;
    const root = this.doc.$(this.rootSelector);
    if (!root) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    Object.assign(root.style, {
      left: `${window.innerWidth - (this.column + 1) * PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top',
    });
    this.model.text_px = Math.round(12 * scale);
    this.doc.update();
  }

  /** The screen box of an element of the document (for item tooltips). */
  box(selector: string): { left: number; top: number; width: number; height: number } | null {
    const element = this.doc?.$(selector);
    if (!element) return null;
    const rect = element.getBoundingClientRect();
    return { left: rect.left, top: rect.top, width: rect.width, height: rect.height };
  }
}

export interface QuestWindowsOptions {
  connection: Connection;
  hero: QuestHero;
  message: (text: string) => void;
  /** HideAllGroupA: the inventory, character, party and NPC windows close when an NPC window opens. */
  closeOthers?: () => void;
  /** A sound by the desktop's name (SOUND_CLICK01, SOUND_INTERFACE01). */
  sound?: (name: string) => void;
  /** Shows / hides an item's tooltip (a reward or requirement item clicked). */
  showItem?: (item: Item, box: { left: number; top: number; width: number; height: number }) => void;
  hideItem?: () => void;
}

// ---------------------------------------------------------------------------------------------------------------
// npc_dialogue.rml

const ND_NPC_LINES_PER_PAGE = 7, ND_ANSWER_LINES_PER_PAGE = 11, ND_WRAP = 160, ND_PAGE_LIMIT = 4, ND_MAX_ANSWERS = 10, ND_SEL_LINE_MAX = 42;

interface DialogueModel extends RmlModel {
  text_px: number; npc_name: string; npc_lines: { text: string }[]; npc_prev_enabled: boolean; npc_next_enabled: boolean;
  answers: { text: string; index: number }[]; ans_prev_enabled: boolean; ans_next_enabled: boolean; show_answers: boolean;
  show_contribute: boolean; contribute_text: string; exit_tooltip: string;
  npcdialogue_click_close: () => void; npcdialogue_npc_prev_page: () => void; npcdialogue_npc_next_page: () => void;
  npcdialogue_ans_prev_page: () => void; npcdialogue_ans_next_page: () => void; npcdialogue_select_answer: (index: number) => void;
}

class NpcDialogueWindow {
  readonly view: DockedDocument<DialogueModel>;
  private npcLines: string[] = [];
  private npcPage = 0;
  private maxNpcPage = 0;
  private showAnswers = false;
  private selTexts: string[][] = [];
  private selCount = 0;
  private selPage = 0;
  private maxSelPage = 0;
  private countPerPage: number[] = [];
  private questListMode = false;
  private dialogueState = 0;
  private questIndices: number[] = [];
  private canClick = false;
  contribution = 0;

  constructor(private readonly quests: QuestWindows) {
    this.view = new DockedDocument<DialogueModel>('npc_dialogue', {
      text_px: 12, npc_name: '', npc_lines: [], npc_prev_enabled: false, npc_next_enabled: false, answers: [], ans_prev_enabled: false,
      ans_next_enabled: false, show_answers: false, show_contribute: false, contribute_text: '', exit_tooltip: T.Close,
      npcdialogue_click_close: () => this.quests.hide(this),
      npcdialogue_npc_prev_page: () => { if (this.npcPage > 0) { this.npcPage--; this.quests.click(); this.sync(); } },
      npcdialogue_npc_next_page: () => {
        if (this.npcPage >= this.maxNpcPage) return;
        this.npcPage++;
        if (this.npcPage === this.maxNpcPage) this.showAnswers = true;
        this.quests.click();
        this.sync();
      },
      npcdialogue_ans_prev_page: () => { if (this.selPage > 0) { this.selPage--; this.quests.click(); this.sync(); } },
      npcdialogue_ans_next_page: () => { if (this.selPage < this.maxSelPage) { this.selPage++; this.quests.click(); this.sync(); } },
      npcdialogue_select_answer: (index) => this.selectAnswer(index),
    });
  }

  private get log(): QuestLog { return this.quests.log!; }

  /** ProcessOpening */
  async opening(): Promise<void> {
    this.questListMode = false;
    this.quests.sound('SOUND_INTERFACE01');
    await this.view.open();
    this.setContents(0);
  }

  /** ProcessClosing */
  closing(): void {
    this.dialogueState = 0;
    this.contribution = 0;
    this.questListMode = false;
    this.quests.closeNpc();
    this.quests.click();
  }

  private setContents(state: number): void {
    this.dialogueState = state;
    this.setNpcWords();
    this.setSelTexts();
    this.canClick = true;
    this.sync();
  }

  /** SetCurNPCWords */
  private setNpcWords(questListCount = 0): void {
    const text = this.questListMode ? this.log.words(questListCount > 0 ? 1501 : 1502) : this.log.npcDialogueWords(this.dialogueState);
    const lines = divideStringByPixel(text, ND_WRAP, 35);
    this.npcLines = lines;
    if (lines.length < 1) return;
    this.maxNpcPage = Math.floor((lines.length - 1) / ND_NPC_LINES_PER_PAGE);
    this.showAnswers = this.maxNpcPage < 1;
    this.npcPage = 0;
  }

  /** SetCurSelTexts */
  private setSelTexts(): void {
    this.selTexts = [];
    let lineSum = 0, i = 0;
    for (; i < ND_MAX_ANSWERS; i++) {
      const answer = this.log.npcDialogueAnswer(this.dialogueState, i);
      if (answer === undefined) break;
      const lines = divideStringByPixel(`${i + 1}. ${answer}`, ND_WRAP, 2, false);
      this.selTexts.push(lines);
      lineSum += lines.length;
      if (lineSum >= ND_SEL_LINE_MAX) { i++; break; }
    }
    this.selCount = this.selTexts.length;
    this.calculatePages();
  }

  /** SetQuestListText */
  private setQuestListText(indices: number[]): void {
    this.questIndices = indices.slice(0, 20);
    this.selTexts = [];
    const count = this.questIndices.length + 1;
    let rows = 0;
    for (let i = 0; i < count; i++) {
      const text = i === count - 1 ? `${i + 1}. ${this.log.words(1007) ?? ''}` : `${i + 1}. [Q]${this.log.subject(this.questIndices[i]) ?? ''}`;
      const lines = divideStringByPixel(text, ND_WRAP, 2, false);
      this.selTexts.push(lines);
      rows += lines.length;
      if (rows > ND_SEL_LINE_MAX) break;
    }
    this.selCount = count;
    this.calculatePages();
  }

  /** CalculateSelTextMaxPage */
  private calculatePages(): void {
    this.selPage = 0;
    this.maxSelPage = 0;
    this.countPerPage = new Array<number>(ND_PAGE_LIMIT).fill(0);
    const linesPerPage = new Array<number>(ND_PAGE_LIMIT).fill(0);
    for (const entry of this.selTexts) {
      this.countPerPage[this.maxSelPage]++;
      linesPerPage[this.maxSelPage] += entry.length;
      if (linesPerPage[this.maxSelPage] > ND_ANSWER_LINES_PER_PAGE) {
        this.countPerPage[this.maxSelPage]--;
        linesPerPage[this.maxSelPage] -= entry.length;
        if (++this.maxSelPage === ND_PAGE_LIMIT) { this.maxSelPage--; break; }
        this.countPerPage[this.maxSelPage]++;
        linesPerPage[this.maxSelPage] += entry.length;
      }
    }
  }

  /** ProcessQuestListReceive */
  questList(indices: number[]): void {
    if (!this.view.isOpen) return;
    this.questListMode = true;
    this.setNpcWords(indices.length);
    this.setQuestListText(indices);
    this.canClick = true;
    this.sync();
  }

  private selectAnswer(index: number): void {
    if (!this.showAnswers || !this.canClick || index < 0 || index >= this.selCount) return;
    this.canClick = false;
    this.quests.click();
    this.processResult(index + 1);
  }

  /** ProcessSelTextResult */
  private processResult(selected: number): void {
    if (this.questListMode) {
      if (selected === this.selCount) { this.questListMode = false; this.setContents(0); return; }
      this.log.sendQuest('QuestSelectRequest', this.questIndices[selected - 1], { SelectedTextIndex: selected });
      return;
    }
    const result = this.log.npcDialogueAnswerResult(this.dialogueState, selected - 1);
    if (result <= 900) { this.setContents(result); return; }
    switch (result) {
      case 901: this.log.send('AvailableQuestsRequest'); break;
      case 902: this.log.send('NpcBuffRequest'); this.quests.hide(this); break;
      case 903: this.log.send('GensJoinRequest', { GensType: 1 }); break;
      case 904: this.log.send('GensJoinRequest', { GensType: 2 }); break;
      case 905: this.log.send('GensLeaveRequest'); break;
      case 906: this.log.send('GensRewardRequest', { GensType: 1 }); break;
      case 907: this.log.send('GensRewardRequest', { GensType: 2 }); break;
      default: this.setContents(999);
    }
  }

  /** SyncRmlModel */
  sync(): void {
    const m = this.view.model;
    m.npc_name = this.log.npcName();
    m.npc_lines = this.npcLines.slice(this.npcPage * ND_NPC_LINES_PER_PAGE, (this.npcPage + 1) * ND_NPC_LINES_PER_PAGE).map((text) => ({ text }));
    m.npc_prev_enabled = this.npcPage > 0;
    m.npc_next_enabled = this.npcPage < this.maxNpcPage;
    m.show_answers = this.showAnswers;
    m.answers = [];
    if (this.showAnswers) {
      const start = this.countPerPage.slice(0, this.selPage).reduce((a, b) => a + b, 0);
      for (let i = 0; i < (this.countPerPage[this.selPage] ?? 0); i++) {
        const entry = this.selTexts[start + i];
        if (entry) m.answers.push({ text: entry.join('\n'), index: start + i });
      }
    }
    m.ans_prev_enabled = this.selPage > 0;
    m.ans_next_enabled = this.selPage < this.maxSelPage;
    // The gens banner: only at the own gens' NPC (543 Duprian, 544 Vanert); this web client keeps no gens membership yet.
    m.show_contribute = false;
    m.contribute_text = formatText(T.GainContribution, this.contribution);
    this.view.update();
  }
}

// ---------------------------------------------------------------------------------------------------------------
// quest_progress.rml / quest_progress_etc.rml

const QP_LINES_PER_PAGE = 7, QP_WRAP = 160;
type LowerView = 0 | 1 | 2; // NON_PLAYER_WORDS_MODE, PLAYER_WORDS_MODE, REQUEST_REWARD_MODE

interface RewardEntry { text: string; style: string; index: number; clickable: boolean }
interface ProgressModel extends RmlModel {
  text_px: number; subject: string; npc_name: string; player_name: string; npc_lines: { text: string }[]; prev_enabled: boolean; next_enabled: boolean;
  active_view: number; player_words_text: string; answers: { text: string; index: number }[]; reward_rows: RewardEntry[];
  request_complete: boolean; ok_label: string; exit_tooltip: string;
  [handler: string]: unknown;
}

class QuestProgressWindow {
  readonly view: DockedDocument<ProgressModel>;
  index = 0;
  private npcLines: string[] = [];
  private npcPage = 0;
  private maxNpcPage = 0;
  private lower: LowerView = 0;
  private requestComplete = false;
  private canClick = false;
  private rewardRows: RewardRow[] = [];

  constructor(private readonly quests: QuestWindows, readonly byEtc: boolean) {
    const prefix = byEtc ? 'questprogressetc' : 'questprogress';
    this.view = new DockedDocument<ProgressModel>(byEtc ? 'quest_progress_etc' : 'quest_progress', {
      text_px: 12, subject: '', npc_name: '', player_name: '', npc_lines: [], prev_enabled: false, next_enabled: false, active_view: 0,
      player_words_text: '', answers: [], reward_rows: [], request_complete: false, ok_label: T.OK, exit_tooltip: T.Close,
      [`${prefix}_click_close`]: () => this.quests.hide(this),
      [`${prefix}_prev_page`]: () => { if (this.npcPage > 0) { this.npcPage--; this.quests.click(); this.sync(); } },
      [`${prefix}_next_page`]: () => {
        if (this.npcPage < this.maxNpcPage) this.npcPage++;
        else if (this.lower === 0) this.lower = 1;
        else return;
        this.quests.click();
        this.sync();
      },
      [`${prefix}_select_answer`]: (answer: number) => this.selectAnswer(answer),
      [`${prefix}_select_reward`]: (row: number) => this.selectReward(row),
      [`${prefix}_complete`]: () => this.complete(),
    });
  }

  private get log(): QuestLog { return this.quests.log!; }

  /** SetContents */
  setContents(index: number): void {
    if (!index) return;
    this.index = index;
    this.quests.hideItem();
    this.npcLines = divideStringByPixel(this.log.npcWords(index), QP_WRAP, 35);
    if (this.npcLines.length >= 1) { this.maxNpcPage = Math.floor((this.npcLines.length - 1) / QP_LINES_PER_PAGE); this.npcPage = 0; }
    this.canClick = true;
    if (this.log.answer(index, 0) !== undefined) {
      this.lower = 0;
    } else {
      const { rows, complete } = this.log.requestRewardRows(index);
      this.rewardRows = rows;
      this.requestComplete = complete;
      this.lower = 2;
    }
    this.sync();
  }

  enableComplete(enable: boolean): void { this.requestComplete = enable; this.sync(); }

  /** ProcessClosing */
  closing(): void {
    if (this.byEtc) this.log.removeEtc(this.index);
    this.index = 0;
    this.quests.hideItem();
    this.quests.closeNpc();
    this.quests.click();
  }

  private selectAnswer(answer: number): void {
    if (this.lower !== 1 || !this.canClick || answer < 0 || answer >= 5) return;
    this.log.sendQuest('QuestProceedRequest', this.index, { ProceedAction: answer + 1 });
    this.quests.click();
    this.canClick = false;
  }

  private selectReward(row: number): void {
    const item = this.rewardRows[row]?.item;
    if (item) this.quests.showItem(item, this.view.box('#reward_popup_anchor'));
    else this.quests.hideItem();
  }

  private complete(): void {
    if (!this.requestComplete || !this.canClick) return;
    this.log.sendQuest('QuestCompletionRequest', this.index);
    this.quests.click();
    this.canClick = false;
  }

  sync(): void {
    if (!this.index) return;
    const m = this.view.model;
    m.subject = this.log.subject(this.index) ?? '';
    m.npc_name = this.log.npcName();
    m.npc_lines = this.npcLines.slice(this.npcPage * QP_LINES_PER_PAGE, (this.npcPage + 1) * QP_LINES_PER_PAGE).map((text) => ({ text }));
    m.prev_enabled = this.npcPage > 0;
    m.next_enabled = this.npcPage < this.maxNpcPage || this.lower === 0;
    m.active_view = this.lower;
    m.player_name = this.quests.hero.name;
    m.player_words_text = this.lower === 1 ? this.log.playerWords(this.index) ?? '' : '';
    m.answers = [];
    if (this.lower === 1) {
      for (let i = 0; i < 5; i++) {
        const answer = this.log.answer(this.index, i);
        if (answer === undefined) break;
        m.answers.push({ text: `${i + 1}.${answer}`, index: i });
      }
    }
    m.reward_rows = this.lower === 2 ? this.rewardRows.map((row, index) => ({ text: row.text, style: row.style, index, clickable: row.item !== null })) : [];
    m.request_complete = this.requestComplete;
    this.view.update();
  }
}

// ---------------------------------------------------------------------------------------------------------------
// my_quest_info.rml

interface MyQuestModel extends RmlModel {
  text_px: number; active_tab: number; tab_quest_label: string; tab_jobchange_label: string; tab_castletemple_label: string;
  quest_list_empty: boolean; empty_quest_lines: { text: string }[]; quests: { text: string; index: number; selected: boolean }[];
  contents: RewardEntry[]; open_enabled: boolean; giveup_enabled: boolean; open_tooltip: string; giveup_tooltip: string; exit_tooltip: string;
  jobchange_title: string; jobchange_lines: { text: string }[]; jobchange_state_lines: { text: string }[];
  castle_title: string; castle_line0: string; castle_line1: string; temple_title: string; temple_line0: string; temple_line1: string;
  myquest_select_tab: (tab: number) => void; myquest_select_quest: (index: number) => void; myquest_select_content: (index: number) => void;
  myquest_click_open: () => void; myquest_click_giveup: () => void; myquest_click_exit: () => void;
}

class MyQuestWindow {
  readonly view: DockedDocument<MyQuestModel>;
  tab = 0;
  selected = 0;
  private contentRows: RewardRow[] = [];

  constructor(private readonly quests: QuestWindows) {
    this.view = new DockedDocument<MyQuestModel>('my_quest_info', {
      text_px: 12, active_tab: 0, tab_quest_label: T.Quest, tab_jobchange_label: T.ChangeClass, tab_castletemple_label: T.CastleTemple,
      quest_list_empty: true, empty_quest_lines: [], quests: [], contents: [], open_enabled: false, giveup_enabled: false,
      open_tooltip: T.StartQuest, giveup_tooltip: T.GiveUpQuest, exit_tooltip: T.Exit, jobchange_title: '', jobchange_lines: [], jobchange_state_lines: [],
      castle_title: '', castle_line0: '', castle_line1: '', temple_title: '', temple_line0: '', temple_line1: '',
      myquest_select_tab: (tab) => this.selectTab(tab),
      myquest_select_quest: (index) => this.selectQuest(index >>> 0),
      myquest_select_content: (index) => {
        const item = this.contentRows[index]?.item;
        if (item) this.quests.showItem(item, this.view.box('#reward_popup_anchor')); else this.quests.hideItem();
      },
      myquest_click_open: () => {
        if (!this.view.model.open_enabled) return;
        this.quests.click();
        void this.quests.showProgress(this.selected, true);
      },
      myquest_click_giveup: () => {
        if (!this.view.model.giveup_enabled) return;
        this.quests.click();
        const index = this.selected;
        void confirmInGame(T.GiveUp).then((yes) => { if (yes) this.log.sendQuest('QuestCancelRequest', index); });
      },
      myquest_click_exit: () => this.quests.hide(this),
    });
  }

  private get log(): QuestLog { return this.quests.log!; }

  /** OpenningProcess: the class change tab's page (ShowQuestPreviewWindow). */
  async opening(): Promise<void> {
    this.log.showQuestPreviewWindow();
    await this.view.open();
    this.sync();
  }

  /** ClosingProcess */
  closing(): void {
    this.unselect();
    this.quests.closeNpc();
    this.quests.click();
  }

  /** UnselectQuestList / SetCurQuestList */
  unselect(): void {
    this.selected = 0;
    this.contentRows = [];
    this.quests.hideItem();
    this.view.model.open_enabled = false;
    this.view.model.giveup_enabled = false;
    this.sync();
  }

  private selectTab(tab: number): void {
    if (tab < 0 || tab > 2) return;
    this.tab = tab;
    this.quests.click();
    if (tab === 2) {
      this.log.send('MiniGameEventCountRequest', { MiniGame: 2 }); // BloodCastle
      this.log.send('MiniGameEventCountRequest', { MiniGame: 3 }); // CursedTemple
    }
    this.sync();
  }

  private selectQuest(index: number): void {
    if (index === this.selected) return;
    this.quests.click();
    this.selected = index;
    this.view.model.open_enabled = this.log.isQuestByEtc(index);
    this.view.model.giveup_enabled = true;
    this.setSummary();
    this.log.sendQuest('QuestStateRequest', index);
    this.sync();
  }

  /** SetSelQuestSummary */
  private setSummary(): void {
    this.contentRows = [];
    this.quests.hideItem();
    if (!this.selected) return;
    this.contentRows.push({ text: this.log.subject(this.selected) ?? '', style: 'subject', item: null });
    for (const line of divideStringByPixel(this.log.summary(this.selected), 150, 8)) this.contentRows.push({ text: line, style: 'summary', item: null });
  }

  /** SetSelQuestRequestReward */
  rewardChanged(): void {
    if (!this.selected || !this.log.isRequestRewardStep(this.selected)) return;
    this.setSummary();
    this.contentRows.push({ text: ' ', style: 'plain', item: null });
    this.contentRows.push(...this.log.requestRewardRows(this.selected).rows);
    this.sync();
  }

  sync(): void {
    if (!this.quests.log) return;
    const m = this.view.model;
    m.active_tab = this.tab;
    const list = this.log.current;
    m.quest_list_empty = list.length === 0;
    m.empty_quest_lines = list.length === 0 ? [{ text: T.NoActiveQuests }] : [];
    m.quests = list.map((index, i) => ({ text: reduceStringByPixel(`${i + 1}.${this.log.subject(index) ?? ''}`, 150), index, selected: index === this.selected }));
    m.contents = this.contentRows.map((row, index) => ({ text: row.text, style: row.style, index, clickable: row.item !== null }));
    m.jobchange_lines = divideStringByPixel(this.log.dialogText(), 140, 15, false).map((text) => ({ text }));
    if (!m.jobchange_title) m.jobchange_title = this.log.windowQuestTitle();
    if (this.tab === 1) {
      const state = this.log.windowQuestState();
      const text = state === QuestLog.LegacyState.ING ? T.QuestInProgress : state === QuestLog.LegacyState.END ? T.QuestCompleted : T.QuestUnfinished;
      m.jobchange_state_lines = divideStringByPixel(text, 140, 2).map((line) => ({ text: line }));
    }
    if (this.tab === 2) {
      m.castle_title = T.BloodCastle;
      m.castle_line0 = formatText(T.EntranceAllowed, this.log.eventCountOf(2));
      m.castle_line1 = formatText(T.EnterPerDay, 6);
      m.temple_title = T.IllusionTemple;
      m.temple_line0 = formatText(T.EntranceAllowed, this.log.eventCountOf(3));
      m.temple_line1 = formatText(T.EnterPerDay, 6);
    }
    this.view.update();
  }
}

// ---------------------------------------------------------------------------------------------------------------
// npc_quest.rml

interface NpcQuestModel extends RmlModel {
  text_px: number; npc_name: string; show_quest_title: boolean; quest_title: string; show_conditions: boolean;
  conditions: { text: string; color: string }[]; complete_enabled: boolean; complete_label: string; show_cost: boolean; cost_label: string;
  cost_amount: string; cost_tier: string; message_lines: { text: string }[]; answers: { text: string; index: number }[]; exit_tooltip: string;
  npcquest_click_close: () => void; npcquest_select_answer: (index: number) => void; npcquest_complete: () => void;
}

const MET = 'rgba(223,191,103,255)', UNMET = 'rgba(255,30,30,255)';

class NpcQuestWindow {
  readonly view: DockedDocument<NpcQuestModel>;
  private completeEnabled = false;

  constructor(private readonly quests: QuestWindows) {
    this.view = new DockedDocument<NpcQuestModel>('npc_quest', {
      text_px: 12, npc_name: '', show_quest_title: true, quest_title: '', show_conditions: false, conditions: [], complete_enabled: false,
      complete_label: T.ProceedWithQuest, show_cost: false, cost_label: T.Cost, cost_amount: '', cost_tier: 'base', message_lines: [], answers: [],
      exit_tooltip: T.Close,
      npcquest_click_close: () => this.quests.hide(this),
      npcquest_select_answer: (index) => this.answer(index),
      npcquest_complete: () => {
        if (!this.log.beQuestItem() || !this.completeEnabled) return;
        this.log.setLegacyActive();
        this.quests.sound('SOUND_INTERFACE01');
      },
    }, '#content_root');
  }

  private get log(): QuestLog { return this.quests.log!; }

  /** ProcessOpening */
  async opening(): Promise<void> {
    this.log.showQuestNpcWindow();
    await this.view.open();
    this.sync();
  }

  closing(): void { this.quests.closeNpc(); }

  /** RmlClickAnswer: the answer's code (1 go on, 2 close, 3 start) and the page it leads to. */
  private answer(index: number): void {
    const dialog = this.quests.legacyDialog(this.log.dialogIndex);
    if (!dialog || index < 0 || index >= dialog.numAnswer) return;
    const code = dialog.answers[index].returnCode;
    let error = false;
    if (code === 1) error = this.log.processNextProgress();
    else if (code === 2) { this.quests.hide(this); }
    else if (code === 3) this.log.setLegacyActive();
    this.quests.sound('SOUND_INTERFACE01');
    const next = dialog.answers[index].link;
    if (next > 0 && !error) this.log.showDialogText(next);
    this.sync();
  }

  /** BuildConditionRows */
  private conditionRows(): { rows: { text: string; color: string }[]; complete: boolean } {
    const quest = this.log.currentLegacyQuest();
    const rows: { text: string; color: string }[] = [];
    let complete = true;
    if (!quest) return { rows, complete: false };
    for (let i = 0; i < quest.conditionCount; i++) {
      const act = quest.acts[i];
      if (!act.requestClass[this.log.legacyClassIndex]) continue;
      if (act.questType === QuestLog.LEGACY_ACT_ITEM) {
        const missing = this.log.findQuestItems(act.itemType * QuestLog.MAX_ITEM_INDEX + act.itemSubType, act.itemNum, act.itemLevel);
        if (missing) complete = false;
        const name = itemName(act.itemType, act.itemSubType) + (act.itemLevel ? ` +${act.itemLevel}` : '');
        rows.push({ text: `${name} x ${act.itemNum}`, color: missing ? UNMET : MET });
      } else if (act.questType === QuestLog.LEGACY_ACT_MONSTER) {
        let kills = this.log.killCount(act.itemType);
        const met = act.itemNum <= kills;
        if (met) kills = act.itemNum; else complete = false;
        rows.push({ text: `${monsterName(act.itemType)} x ${kills}/${act.itemNum}`, color: met ? MET : UNMET });
      }
    }
    return { rows, complete };
  }

  sync(): void {
    if (!this.view.isOpen) return;
    const m = this.view.model;
    const index = this.log.currQuestIndex;
    const state = this.log.legacyState(index);
    const quest = this.log.legacyQuests[index];
    const darkNpc = [3, 4, 6].includes(this.quests.hero.baseClass) && this.log.checkNpc;
    m.npc_name = monsterName((darkNpc ? this.log.legacyQuests[2] : quest)?.npcType ?? 0);
    m.show_quest_title = !darkNpc;
    m.quest_title = quest?.name ?? '';
    m.show_conditions = state === QuestLog.LegacyState.ING;
    if (m.show_conditions) {
      const { rows, complete } = this.conditionRows();
      this.completeEnabled = complete;
      m.conditions = rows;
      m.complete_enabled = complete;
    } else {
      this.completeEnabled = false;
    }
    m.show_cost = state === QuestLog.LegacyState.NO;
    m.cost_amount = formatGold(this.log.needZen);
    m.cost_tier = goldTier(this.log.needZen);
    m.message_lines = divideStringByPixel(this.log.dialogText(), 160, 15, false).map((text) => ({ text }));
    m.answers = this.log.dialogAnswers().map((text, i) => ({ text, index: i }));
    this.view.update();
  }
}

// ---------------------------------------------------------------------------------------------------------------

type QuestWindow = NpcDialogueWindow | QuestProgressWindow | MyQuestWindow | NpcQuestWindow;

/** The quest windows and their packets; `handle` takes every packet (also the ones that came before it was made). */
export class QuestWindows implements QuestEvents {
  log: QuestLog | null = null;
  private readonly dialogue: NpcDialogueWindow;
  private readonly progress: QuestProgressWindow;
  private readonly progressEtc: QuestProgressWindow;
  private readonly myQuest: MyQuestWindow;
  private readonly npcQuest: NpcQuestWindow;
  private readonly pending: Uint8Array[] = [];
  readonly hero: QuestHero;

  constructor(private readonly options: QuestWindowsOptions) {
    this.hero = options.hero;
    this.dialogue = new NpcDialogueWindow(this);
    this.progress = new QuestProgressWindow(this, false);
    this.progressEtc = new QuestProgressWindow(this, true);
    this.myQuest = new MyQuestWindow(this);
    this.npcQuest = new NpcQuestWindow(this);
    options.connection.onPacket((bytes) => this.handle(bytes));
    void Promise.all([loadQuestScripts(), loadTooltipData()]).then(([scripts]) => {
      this.log = new QuestLog(options.connection, scripts, options.hero, this);
      for (const bytes of this.pending.splice(0)) this.log.handle(bytes);
    }).catch((error: unknown) => console.warn('Quest data', error));
    window.addEventListener('keydown', (event) => {
      if (event.target instanceof Element && event.target.closest('input, textarea')) return;
      if (event.key === 'Escape') {
        const open = this.windows().find((w) => w.view.isOpen);
        if (open) this.hide(open);
      } else if (event.key.toLowerCase() === 't' && !event.ctrlKey && !event.altKey && !event.metaKey) {
        event.preventDefault();
        this.toggleMyQuest();
      }
    });
  }

  /** Every packet; kept until the quest texts are loaded. */
  handle(bytes: Uint8Array): void {
    if (this.log) this.log.handle(bytes); else this.pending.push(bytes);
  }

  private windows(): QuestWindow[] { return [this.progressEtc, this.progress, this.dialogue, this.npcQuest, this.myQuest]; }

  // ---- helpers for the windows ----
  click(): void { this.sound('SOUND_CLICK01'); }
  sound(name: string): void { this.options.sound?.(name); }
  closeNpc(): void { this.options.connection.send('ClientToServer', 'CloseNpcRequest', {}); }
  showItem(item: Item, box: { left: number; top: number; width: number; height: number } | null): void { if (box) this.options.showItem?.(item, box); }
  hideItem(): void { this.options.hideItem?.(); }
  legacyDialog(index: number): LegacyDialog | undefined { return LEGACY_DIALOGS[index]; }

  /** Hide(window): closes it with its closing process (CloseNpcRequest …). */
  hide(window: QuestWindow): void {
    if (!window.view.isOpen) return;
    window.view.close();
    window.closing();
    this.placeAll();
  }

  /** HideAllGroupA: the other windows close without their closing process; the inventory and the character window close. */
  private hideGroupA(except?: QuestWindow): void {
    this.options.closeOthers?.();
    for (const window of this.windows()) if (window !== except && window.view.isOpen) window.view.close();
    this.hideItem();
  }

  private placeAll(): void {
    // The quest-by-other-means window opens beside the quest info window (it doesn't close it).
    this.progressEtc.view.column = this.myQuest.view.isOpen ? 1 : 0;
    this.progressEtc.view.place();
  }

  get isMyQuestOpen(): boolean { return this.myQuest.view.isOpen; }
  get isDialogueOpen(): boolean { return this.dialogue.view.isOpen; }

  /** T (HotKey.cpp) and the character window's quest button. */
  toggleMyQuest(): void {
    if (!this.log) return;
    if (this.myQuest.view.isOpen) this.hide(this.myQuest);
    else { this.click(); void this.myQuest.opening().then(() => this.placeAll()); }
  }

  // ---- QuestEvents ----
  openDialogue(contribution: number): void {
    if (this.dialogue.view.isOpen) return;
    this.hideGroupA(this.dialogue);
    this.dialogue.contribution = contribution;
    void this.dialogue.opening();
  }

  questList(indices: number[]): void { this.dialogue.questList(indices); }

  showProgress(index: number, byEtc: boolean): Promise<void> {
    const window = byEtc ? this.progressEtc : this.progress;
    if (window.view.isOpen) { window.setContents(index); return Promise.resolve(); }
    if (!byEtc) this.hideGroupA(window);
    this.sound('SOUND_INTERFACE01');
    window.index = index;
    return window.view.open().then(() => { window.setContents(index); this.placeAll(); });
  }

  hideProgress(): void {
    for (const window of [this.progress, this.progressEtc]) if (window.view.isOpen) this.hide(window);
  }

  disableComplete(): void {
    if (this.progress.view.isOpen) this.progress.enableComplete(false);
    else if (this.progressEtc.view.isOpen) this.progressEtc.enableComplete(false);
  }

  questListChanged(): void {
    // SetCurQuestList: the selection goes.
    this.myQuest.unselect();
  }

  selectedRewardChanged(): void { this.myQuest.rewardChanged(); }

  showLegacyQuest(): void {
    // HideAll, then the NPC quest window.
    this.hideGroupA(this.npcQuest);
    void this.npcQuest.opening();
  }

  legacyChanged(): void { this.npcQuest.sync(); }

  questCountLimit(lines: readonly string[]): void {
    this.hideGroupA();
    void confirmInGame(lines.join('\n'));
  }

  message(text: string): void { this.options.message(text); }

  /** Which quest the dialogue shows and the hero's quests, for tests. */
  get state(): { npc: number; current: number[]; progress: number; progressEtc: number } {
    return { npc: this.log?.npcIndex ?? 0, current: [...(this.log?.current ?? [])], progress: this.progress.index, progressEtc: this.progressEtc.index };
  }
}
