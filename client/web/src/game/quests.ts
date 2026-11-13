// Quests as the desktop client keeps them:
//  - CQuestMng (GameLogic/Quests/QuestMng.cpp): the NPC being talked to, its dialogue states, the quest steps' texts,
//    each quest's requirements and rewards from the server (QuestProgress / QuestState), the list of the hero's
//    quests (QuestStateList) and of the quests started by other means (QuestEventResponse), and the
//    requirement/reward lines (GetRequestRewardText).
//  - CSQuest (GameLogic/Quests/CSQuest.cpp): the class change quests (LegacyQuestStateList, LegacyQuestStateDialog,
//    LegacySetQuestStateResponse, LegacyQuestMonsterKillInfo), their states, conditions and dialog pages.
//  - The packets' handlers of WSclient.cpp (ReceiveNPCDlgUIStart, ReceiveQuestByNPCEPList, ReceiveQuestQSSelSentence,
//    ReceiveQuestQSRequestReward, ReceiveQuestCompleteResult, ReceiveQuestGiveUp, ReceiveProgressQuestList,
//    ReceiveProgressQuestRequestReward, ReceiveProgressQuestListReady, ReceiveQuestHistory, ReceiveQuestState,
//    ReceiveQuestResult, ReceiveQuestMonKillInfo, ReceiveQuestLimitResult, ReceiveQuestByEtcEPList).
// The windows (ui/questWindows.ts) are told through QuestEvents.

import { packetCode } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import { decodeItem, type Item } from './inventory.js';
import { itemName } from './items.js';
import { itemNameText } from './itemTooltip.js';
import { describeMonster } from './monsterData.js';
import { buffDefinition } from './buffs.js';
import { skillInfo } from './skills.js';
import { LEGACY_DIALOGS } from './legacyQuestDialog.generated.js';
import type { LegacyQuest, QuestScripts } from './questData.js';

/** The desktop's texts (Localization/Game.en.resx). */
export const QT = {
  None: 'None', Requirements: 'Requirements', Reward: 'Reward', RandomReward: 'Random Reward (%lu different kinds)',
  RewardExp: 'Exp.: %lu', RewardZen: 'Zen: %lu', RewardItem: 'Item: %ls x %lu', RewardBuff: 'Bonus: %ls for %lu minutes', Contribution: 'Contribution: %lu',
  ReqMonster: 'Mon.: %ls x %lu/%lu', ReqItem: 'Item: %ls x %lu/%lu', ReqLevel: 'Level: %lu or higher', ReqZen: 'Zen: %lu', ReqSkill: 'Skill: %ls',
  ReqBuff: 'Bonus: %ls', ReqPvp: 'Enemy Gens Member x %lu/%lu',
  CCGuardsman: 'Chaos Castle Lv.%lu Guardsman x %lu/%lu', BCGate: 'Blood Castle Lv.%lu Gate Destruction x %lu/%lu', CCPlayer: 'Chaos Castle Lv.%lu Player x %lu/%lu',
  DSPoint: 'Devil Square Lv.%lu Point x %lu/%lu', BCCleared: 'Blood Castle Lv.%lu Cleared', CCCleared: 'Chaos Castle Lv.%lu Cleared',
  DSCleared: 'Devil Square Lv.%lu Cleared', ITCleared: 'Illusion Temple Lv.%lu Cleared',
  OpenCharacter: 'Open Character Stats (C) Window', OpenInventory: 'Open Inventory (I/V) Window',
  Completed: "You've successfully completed the quest.", ZenLimit: 'You have reached your Zen limit.', InventoryFull: 'Inventory is full.',
  SameItem: 'the same item that you want to trade.', ConversationIsOver: 'Conversation is over',
  CountLimit: ['You cannot accept any more quest.', 'You can proceed maximum 10 quests', 'at the same time.', 'You need to clear at least 1 quest to', 'accept this one.'],
} as const;

/** mu_swprintf with %lu / %ls / %d / %s. */
export function formatText(text: string, ...values: (string | number)[]): string {
  let index = 0;
  return text.replace(/%%|%l?[usd]|%ls/g, (token) => (token === '%%' ? '%' : String(values[index++] ?? '')));
}

// QUEST_REQUEST_TYPE (the desktop's numbers) and what the server's ConditionType numbers are in them.
export const Request = {
  NONE: 0, MONSTER: 1, SKILL: 2, ITEM: 3, LEVEL: 4, TUTORIAL: 5, BUFF: 6, EVENT_MAP_USER_KILL: 7, EVENT_MAP_MON_KILL: 8,
  EVENT_MAP_BLOOD_GATE: 9, CLEAR_BLOOD: 10, CLEAR_CHAOS: 11, CLEAR_DEVIL: 12, CLEAR_ILLUSION: 13, DEVIL_POINT: 14, ZEN: 15, PVP_POINT: 16, NPC_TALK: 17,
} as const;
const SERVER_CONDITION: Record<number, number> = {
  0: 0, 1: 1, 2: 2, 4: 3, 8: 4, 16: 5, 32: 6, 64: 7, 65: 8, 66: 9, 256: 10, 257: 11, 258: 12, 259: 13, 260: 14, 261: 15, 262: 16, 263: 17,
};
export const Reward = { NONE: 0, EXP: 1, ZEN: 2, ITEM: 4, BUFF: 8, CONTRIBUTE: 0x10, RANDOM: 0x20 } as const;

interface QuestRequest { type: number; index: number; value: number; current: number; item: Item | null }
interface QuestReward { type: number; index: number; value: number; item: Item | null }
interface RequestReward { requestCount: number; generalRewardCount: number; randRewardCount: number; randGiveCount: number; requests: QuestRequest[]; rewards: QuestReward[] }

/** The style of a requirement/reward line (RewardModel::RowStyle). */
export type RowStyle = 'plain' | 'subject' | 'summary' | 'heading' | 'requirement' | 'requirement-unmet' | 'reward' | 'random-reward';
/** A line of a quest's requirements and rewards, with the item an item line shows. */
export interface RewardRow { text: string; style: RowStyle; item: Item | null }

/** What the windows are told. */
export interface QuestEvents {
  /** ReceiveNPCDlgUIStart: open the NPC dialogue. */
  openDialogue(contribution: number): void;
  /** ReceiveQuestByNPCEPList: the NPC's quests to choose from. */
  questList(indices: number[]): void;
  /** SetCurQuestProgress: show a quest step (byEtc: quest_progress_etc). */
  showProgress(index: number, byEtc: boolean): void;
  hideProgress(): void;
  disableComplete(): void;
  /** The hero's quest list changed (SetCurQuestList). */
  questListChanged(): void;
  /** The selected quest's requirements and rewards came (RefreshSelectedQuestReward). */
  selectedRewardChanged(): void;
  /** ReceiveQuestState / ReceiveQuestResult: show the class change quest window. */
  showLegacyQuest(): void;
  /** The kill counts of a class change quest changed. */
  legacyChanged(): void;
  questCountLimit(lines: readonly string[]): void;
  message(text: string): void;
}

/** What the quests read of the hero. */
export interface QuestHero {
  level(): number;
  money(): number;
  /** The base class (CLASS_WIZARD … CLASS_RAGEFIGHTER = 0 … 6). */
  baseClass: number;
  name: string;
  bagItems(): Item[];
}

export const questGroup = (index: number) => index >>> 16;
export const questNumber = (index: number) => index & 0xffff;
const questIndexOf = (group: number, number: number) => ((group << 16) | number) >>> 0;

export function monsterName(number: number): string {
  try { return describeMonster(number).name; } catch { return `Monster ${number}`; }
}

function itemText(item: Item | null): string {
  if (!item) return '';
  try { return itemNameText(item); } catch { return itemName(item.group, item.number); }
}

// ---- class change quests (CSQuest) ----
const LegacyState = { NONE: 0, ING: 1, END: 2, NO: 3, ITEM: 4, ERROR: 5 } as const;
const LEGACY_ACT_ITEM = 1, LEGACY_ACT_MONSTER = 2;
const QUEST_COMBO = 3;
const MAX_ITEM_INDEX = 512;
const BASE = { KNIGHT: 1, DARK: 3, DARK_LORD: 4, RAGE_FIGHTER: 6 } as const;

export class QuestLog {
  // CQuestMng
  npcIndex = 0;
  private readonly requestRewards = new Map<number, RequestReward>();
  private readonly epState = new Map<number, boolean>();
  private etcList: number[] = [];
  /** m_listCurQuestIndex: the hero's quests, sorted. */
  current: number[] = [];

  // CSQuest
  private legacyList = new Uint8Array(50);
  private legacyClass = 255;
  currQuestIndex = 0;
  private currQuestIndexWnd = 0;
  private currState = 0;
  private currPage = 0;
  needZen = 0;
  private killMobType = [-1, -1, -1, -1, -1];
  private killMobCount = [-1, -1, -1, -1, -1];
  private eventCount = [0, 0, 0, 0];
  /** g_iCurrentDialogScript */
  dialogIndex = -1;
  /** Shown when talking to the Dark Lord / Magic Gladiator quest NPC (bCheckNPC); not set by this server. */
  checkNpc = false;

  constructor(private readonly connection: Connection, readonly scripts: QuestScripts, private readonly hero: QuestHero, private readonly events: QuestEvents) {}

  // ---------- texts (CQuestMng getters) ----------

  words(index: number): string | undefined { return this.scripts.words.get(index); }

  npcName(): string { return this.npcIndex ? monsterName(this.npcIndex) : ''; }

  private dialogue(state: number) { return this.npcIndex ? this.scripts.dialogues.get(((this.npcIndex * 0x10000) + state) >>> 0) : undefined; }
  npcDialogueWords(state: number): string | undefined { const d = this.dialogue(state); return d ? this.words(d.npcWords) : undefined; }
  npcDialogueAnswer(state: number, answer: number): string | undefined { const d = this.dialogue(state); return d && d.answers[answer * 2] ? this.words(d.answers[answer * 2]) : undefined; }
  npcDialogueAnswerResult(state: number, answer: number): number {
    if (!this.npcIndex) return 0;
    const d = this.dialogue(state);
    if (!d || !d.answers[answer * 2]) return -1;
    return d.answers[answer * 2 + 1];
  }

  private progress(index: number) { return this.scripts.progress.get(index); }
  npcWords(index: number) { const p = this.progress(index); return p ? this.words(p.npcWords) : undefined; }
  playerWords(index: number) { const p = this.progress(index); return p ? this.words(p.playerWords) : undefined; }
  answer(index: number, answer: number) { const p = this.progress(index); return p && p.answers[answer] ? this.words(p.answers[answer]) : undefined; }
  subject(index: number) { const p = this.progress(index); return p ? this.words(p.subject) : undefined; }
  summary(index: number) { const p = this.progress(index); return p ? this.words(p.summary) : undefined; }
  /** IsRequestRewardQS: a step without answers shows its requirements and rewards. */
  isRequestRewardStep(index: number): boolean { return (this.progress(index)?.answers[0] ?? 0) === 0; }
  isQuestByEtc(index: number): boolean { return this.progress(index)?.uiType === 1; }
  hasRequestReward(index: number): boolean { return this.requestRewards.has(index); }
  get etcQuests(): readonly number[] { return this.etcList; }
  eventCountOf(type: number): number { return this.eventCount[type] ?? 0; }

  // ---------- requests ----------

  send(name: string, values: Record<string, number> = {}): void { this.connection.send('ClientToServer', name, values); }
  sendQuest(name: string, index: number, extra: Record<string, number> = {}): void {
    this.send(name, { QuestNumber: questNumber(index), QuestGroup: questGroup(index), ...extra });
  }

  // ---------- packets ----------

  handle(bytes: Uint8Array): void {
    const { code, subCode } = packetCode(bytes);
    const at = bytes[0] === 0xc2 || bytes[0] === 0xc4 ? 4 : 3; // the first byte after the code
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const u16 = (o: number) => (o + 2 <= bytes.length ? view.getUint16(o, true) : 0);
    const u32 = (o: number) => (o + 4 <= bytes.length ? view.getUint32(o, true) : 0);
    if (code === 0xf9 && subCode === 0x01 && bytes[0] === 0xc3) {
      // ReceiveNPCDlgUIStart (only when the dialogue isn't open; the window checks)
      this.npcIndex = u16(4);
      this.events.openDialogue(u32(8));
      return;
    }
    if (code === 0xf6) {
      const sub = subCode;
      const body = at + 1; // after the sub code
      switch (sub) {
        case 0x00: this.events.questCountLimit(QT.CountLimit); return; // ReceiveQuestLimitResult
        case 0x03: { // ReceiveQuestByEtcEPList: two quest identifications
          const list: number[] = [];
          for (let i = 0; i < 2; i++) { const number = u16(body + i * 4), group = u16(body + i * 4 + 2); if (number || group) list.push(questIndexOf(group, number)); }
          this.etcList = list;
          return;
        }
        case 0x04: { // ReceiveQuestByItemUseEP: select it right away
          const index = u32(8);
          this.sendQuest('QuestSelectRequest', index, { SelectedTextIndex: 0 });
          return;
        }
        case 0x0a: { // ReceiveQuestByNPCEPList
          const count = u16(body + 2);
          const list: number[] = [];
          for (let i = 0; i < count && i < 20; i++) list.push(questIndexOf(u16(body + 4 + i * 4 + 2), u16(body + 4 + i * 4)));
          this.events.questList(list);
          return;
        }
        case 0x0b: this.setCurrentProgress(questIndexOf(u16(body + 2), u16(body))); return; // ReceiveQuestQSSelSentence
        case 0x0c: case 0x1b: { // ReceiveQuestQSRequestReward / ReceiveProgressQuestRequestReward
          const index = this.setRequestReward(bytes);
          if (index === null) return;
          this.epState.set(questGroup(index), true);
          if (sub === 0x0c) this.setCurrentProgress(index);
          else this.events.selectedRewardChanged();
          return;
        }
        case 0x0d: { // ReceiveQuestCompleteResult
          const index = questIndexOf(u16(body + 2), u16(body));
          const result = bytes[body + 4];
          if (result === 1) {
            this.events.hideProgress();
            this.epState.set(questGroup(index), false);
            this.removeCurrent(index);
          } else if (result === 2) {
            this.events.disableComplete();
            this.events.message(QT.ZenLimit);
          } else if (result === 3) {
            this.events.disableComplete();
            this.events.message(QT.InventoryFull);
            this.events.message(QT.SameItem);
          }
          return;
        }
        case 0x0f: this.removeCurrent(questIndexOf(u16(body + 2), u16(body))); return; // ReceiveQuestGiveUp
        case 0x1a: { // ReceiveProgressQuestList
          const count = bytes[body] ?? 0;
          const list: number[] = [];
          for (let i = 0; i < count; i++) list.push(questIndexOf(u16(body + 1 + i * 4 + 2), u16(body + 1 + i * 4)));
          this.current = list.filter((index) => this.subject(index) !== undefined);
          this.events.questListChanged();
          return;
        }
        case 0x20: // ReceiveProgressQuestListReady
          this.etcList = [];
          this.send('ActiveQuestListRequest');
          this.send('EventQuestStateListRequest');
          return;
        default: return;
      }
    }
    if (bytes[0] !== 0xc1) return;
    if (code === 0xa0) this.setLegacyLists(bytes.subarray(4, 4 + Math.ceil(bytes[3] / 4)), bytes[3]); // ReceiveQuestHistory
    else if (code === 0xa1 && bytes.length >= 5) { this.setLegacyQuest(bytes[3], bytes[4]); this.events.showLegacyQuest(); } // ReceiveQuestState
    else if (code === 0xa2 && bytes.length >= 6) { if (bytes[4] === 0) { this.setLegacyQuest(bytes[3], bytes[5]); this.events.showLegacyQuest(); } } // ReceiveQuestResult
    else if (code === 0xa4 && bytes.length >= 48) { // ReceiveQuestMonKillInfo
      for (let i = 0; i < 5; i++) { this.killMobType[i] = view.getInt32(8 + i * 8, true); this.killMobCount[i] = view.getInt32(12 + i * 8, true); }
      this.events.legacyChanged();
    } else if (code === 0x9f && bytes.length >= 5) { if (bytes[3] < this.eventCount.length) this.eventCount[bytes[3]] = bytes[4]; } // ReceiveEventCount
  }

  /** SetQuestRequestReward of QuestProgressExtended / QuestStateExtended (or their short forms); the quest index. */
  private setRequestReward(bytes: Uint8Array): number | null {
    const extended = bytes[0] === 0xc2;
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const head = extended ? { count: 5, number: 8, group: 10, conditions: 12, rewards: 152, condSize: 28, rewSize: 24, itemAt: 12, rewItemAt: 8, idAt: 2, valAt: 4, curAt: 8, rIdAt: 2, rValAt: 4 }
      : { count: 8, number: 4, group: 6, conditions: 11, rewards: 141, condSize: 26, rewSize: 22, itemAt: 14, rewItemAt: 10, idAt: 4, valAt: 6, curAt: 10, rIdAt: 4, rValAt: 6 };
    if (bytes.length < head.rewards) return null;
    const requestCount = bytes[head.count], rewardCount = bytes[head.count + 1], randomCount = bytes[head.count + 2];
    const index = questIndexOf(view.getUint16(head.group, true), view.getUint16(head.number, true));
    const decode = (at: number): Item | null => { try { return decodeItem(bytes, at); } catch { return null; } };
    const entry: RequestReward = { requestCount: 0, generalRewardCount: 0, randRewardCount: 0, randGiveCount: 0, requests: [], rewards: [] };
    const firstType = bytes[head.conditions]; // the enums are written as one byte
    if (firstType === 0 || requestCount === 0) {
      entry.requestCount = 1;
      entry.requests.push({ type: Request.NONE, index: 0, value: 0, current: 0, item: null });
    } else {
      entry.requestCount = Math.min(5, requestCount);
      for (let i = 0; i < entry.requestCount; i++) {
        const at = head.conditions + i * head.condSize;
        const type = SERVER_CONDITION[bytes[at]] ?? Request.NONE;
        entry.requests.push({
          type, index: view.getUint16(at + head.idAt, true), value: view.getUint32(at + head.valAt, true), current: view.getUint32(at + head.curAt, true),
          item: type === Request.ITEM ? decode(at + head.itemAt) : null,
        });
      }
    }
    if (bytes[head.rewards] === Reward.NONE || rewardCount === 0) {
      entry.generalRewardCount = 1;
      entry.rewards.push({ type: Reward.NONE, index: 0, value: 0, item: null });
    } else {
      entry.randGiveCount = randomCount;
      const random: QuestReward[] = [];
      for (let i = 0; i < Math.min(5, rewardCount); i++) {
        const at = head.rewards + i * head.rewSize;
        if (at + head.rewSize > bytes.length) break;
        const rawType = bytes[at];
        const reward = { type: rawType, index: view.getUint16(at + head.rIdAt, true), value: view.getUint32(at + head.rValAt, true), item: null as Item | null };
        if ((rawType & 0xffe0) === Reward.RANDOM) {
          reward.type = rawType & 0x1f;
          if (reward.type === Reward.ITEM) reward.item = decode(at + head.rewItemAt);
          random.push(reward);
        } else {
          if (rawType === Reward.ITEM) reward.item = decode(at + head.rewItemAt);
          entry.rewards.push(reward);
        }
      }
      entry.generalRewardCount = entry.rewards.length;
      entry.randRewardCount = random.length;
      entry.rewards.push(...random);
    }
    this.requestRewards.set(index, entry);
    return index;
  }

  /** CQuestMng::SetCurQuestProgress */
  setCurrentProgress(index: number): void {
    this.addCurrent(index);
    if (questNumber(index) === 0x00ff) {
      this.events.hideProgress();
      this.events.message(QT.Completed);
      return;
    }
    const progress = this.progress(index);
    if (!progress) { console.warn(`Quest progress entry missing for index 0x${index.toString(16)}`); return; }
    this.events.showProgress(index, progress.uiType !== 0);
  }

  /** AddCurQuestIndexList: one quest per group; steps 0 and 0xFF leave the list. */
  private addCurrent(index: number): void {
    const group = questGroup(index), number = questNumber(index);
    const at = this.current.findIndex((i) => questGroup(i) === group);
    if (at >= 0) this.current.splice(at, 1);
    if (number !== 0 && number !== 0xff) this.current.push(index);
    this.current.sort((a, b) => a - b);
    this.events.questListChanged();
  }

  private removeCurrent(index: number): void {
    const at = this.current.findIndex((i) => questGroup(i) === questGroup(index));
    if (at >= 0) this.current.splice(at, 1);
    this.events.questListChanged();
  }

  /** DelQuestIndexByEtcList */
  removeEtc(index: number): void {
    if (questNumber(index) === 0) return;
    const at = this.etcList.findIndex((i) => questGroup(i) === questGroup(index));
    if (at >= 0) this.etcList.splice(at, 1);
  }

  /** GetRequestRewardText + RewardModel::BuildRows: the lines and whether every requirement is met. */
  requestRewardRows(index: number): { rows: RewardRow[]; complete: boolean } {
    const entry = this.requestRewards.get(index);
    if (!index || !entry) return { rows: [], complete: false };
    const rows: RewardRow[] = [{ text: QT.Requirements, style: 'heading', item: null }];
    let complete = true;
    for (const request of entry.requests.slice(0, entry.requestCount)) {
      const current = Math.min(request.current, request.value);
      let met = request.current >= request.value;
      let text = '';
      switch (request.type) {
        case Request.NONE: met = true; text = QT.None; break;
        case Request.MONSTER: text = formatText(QT.ReqMonster, monsterName(request.index), current, request.value); break;
        case Request.ITEM: text = formatText(QT.ReqItem, itemText(request.item), current, request.value); break;
        case Request.LEVEL: text = formatText(QT.ReqLevel, request.value); break;
        case Request.ZEN: text = formatText(QT.ReqZen, request.value); break;
        case Request.PVP_POINT: text = formatText(QT.ReqPvp, current, request.value); break;
        case Request.SKILL: met = request.current !== 0; text = formatText(QT.ReqSkill, skillInfo(request.index)?.name ?? ''); break;
        case Request.TUTORIAL:
          met = request.current === 1;
          text = index === 0x10009 ? QT.OpenCharacter : index === 0x1000f ? QT.OpenInventory : '';
          break;
        case Request.BUFF: met = request.current !== 0; text = formatText(QT.ReqBuff, buffDefinition(request.index)?.name ?? ''); break;
        case Request.EVENT_MAP_MON_KILL: text = formatText(QT.CCGuardsman, request.index, current, request.value); break;
        case Request.EVENT_MAP_BLOOD_GATE: text = formatText(QT.BCGate, request.index, current, request.value); break;
        case Request.EVENT_MAP_USER_KILL: text = formatText(QT.CCPlayer, request.index, current, request.value); break;
        case Request.DEVIL_POINT: text = formatText(QT.DSPoint, request.index, current, request.value); break;
        case Request.CLEAR_BLOOD: met = request.current !== 0; text = formatText(QT.BCCleared, request.index); break;
        case Request.CLEAR_CHAOS: met = request.current !== 0; text = formatText(QT.CCCleared, request.index); break;
        case Request.CLEAR_DEVIL: met = request.current !== 0; text = formatText(QT.DSCleared, request.index); break;
        case Request.CLEAR_ILLUSION: met = request.current !== 0; text = formatText(QT.ITCleared, request.index); break;
        default: met = true;
      }
      if (!met) complete = false;
      rows.push({ text: text.slice(0, 63), style: met ? 'requirement' : 'requirement-unmet', item: request.type === Request.ITEM ? request.item : null });
    }
    const general = entry.rewards.slice(0, entry.generalRewardCount);
    const random = entry.rewards.slice(entry.generalRewardCount, entry.generalRewardCount + entry.randRewardCount);
    for (const [list, heading, style] of [[general, QT.Reward, 'reward'], [random, formatText(QT.RandomReward, entry.randGiveCount), 'random-reward']] as const) {
      if (!list.length) continue;
      rows.push({ text: ' ', style: 'plain', item: null });
      rows.push({ text: heading, style: 'heading', item: null });
      for (const reward of list) rows.push({ text: this.rewardText(reward).slice(0, 63), style, item: reward.type === Reward.ITEM ? reward.item : null });
    }
    return { rows, complete };
  }

  private rewardText(reward: QuestReward): string {
    switch (reward.type) {
      case Reward.NONE: return QT.None;
      case Reward.EXP: return formatText(QT.RewardExp, reward.value);
      case Reward.ZEN: return formatText(QT.RewardZen, reward.value);
      case Reward.ITEM: return formatText(QT.RewardItem, itemText(reward.item), reward.value);
      case Reward.BUFF: return formatText(QT.RewardBuff, buffDefinition(reward.index)?.name ?? '', reward.value);
      case Reward.CONTRIBUTE: return formatText(QT.Contribution, reward.value);
      default: return '';
    }
  }

  // ---------- class change quests (CSQuest) ----------

  get legacyQuests(): LegacyQuest[] { return this.scripts.legacy; }
  currentLegacyQuest(): LegacyQuest | undefined { return this.scripts.legacy[this.currQuestIndex]; }

  legacyState(index: number): number {
    const byte = this.legacyList[index >> 2] ?? 0;
    return (byte >> ((index & 3) * 2)) & 3;
  }

  /** setQuestLists */
  private setLegacyLists(list: Uint8Array, count: number): void {
    this.legacyClass = this.hero.baseClass;
    this.legacyList = new Uint8Array(50);
    this.legacyList.set(list.subarray(0, 50));
    let i = this.legacyClass === BASE.KNIGHT ? 0 : [BASE.DARK, BASE.DARK_LORD, BASE.RAGE_FIGHTER].includes(this.legacyClass as 3 | 4 | 6) ? 4 : 0;
    for (; i < count; i++) {
      if (this.legacyClass !== BASE.KNIGHT && this.legacyClass !== BASE.DARK && this.legacyClass !== BASE.DARK_LORD && this.legacyClass !== BASE.RAGE_FIGHTER && i === QUEST_COMBO) continue;
      if (this.legacyState(i) !== LegacyState.END) break;
    }
    this.currQuestIndex = i;
    this.currQuestIndexWnd = i;
  }

  /** setQuestList: the byte holds the four quests of the index's group. */
  private setLegacyQuest(index: number, stateByte: number): void {
    if (index >= 200) return;
    this.currQuestIndex = index;
    this.currQuestIndexWnd = Math.max(index, this.currQuestIndexWnd);
    this.legacyList[index >> 2] = stateByte;
  }

  get legacyClassIndex(): number { return this.legacyClass === 255 ? this.hero.baseClass : this.legacyClass; }

  private findQuestContext(quest: LegacyQuest, which: number): number {
    for (let i = 0; i < quest.conditionCount; i++) if (quest.acts[i].requestClass[this.legacyClassIndex] >= 1) return quest.acts[i].startText[which];
    this.currQuestIndex = Math.max(0, this.currQuestIndex - 1);
    this.checkQuestState();
    return this.currPage;
  }

  private checkRequestCondition(quest: LegacyQuest, lastCheck = false): boolean {
    for (let i = 0; i < quest.conditionCount; i++) {
      if (quest.acts[i].requestClass[this.legacyClassIndex] === 0) continue;
      const requestType = quest.acts[i].requestType;
      for (let j = 0; j < quest.requestCount; j++) {
        const request = quest.requests[j];
        if (request.type !== requestType && request.type !== 255) continue;
        const fail = () => { this.currPage = request.errorText; this.currState = LegacyState.ERROR; return false; };
        if (request.completeQuestIndex !== 65535 && this.legacyState(request.completeQuestIndex) !== LegacyState.END) return fail();
        if (request.levelMin > 0 && request.levelMin > this.hero.level()) return fail();
        if (request.levelMax > 0 && request.levelMax < this.hero.level()) return fail();
        if (request.zen > 0) {
          this.needZen = request.zen;
          if (lastCheck && this.needZen > this.hero.money()) return fail();
        }
      }
    }
    return true;
  }

  /** FindQuestItemsInInven: how many are still missing (0: enough). */
  findQuestItems(type: number, count: number, level = -1): number {
    let found = 0;
    for (const item of this.hero.bagItems()) {
      if (item.group * MAX_ITEM_INDEX + item.number !== type || (level !== -1 && level !== item.level)) continue;
      if (count <= ++found) return 0;
    }
    return count - found;
  }

  killCount(monster: number): number {
    const at = this.killMobType.indexOf(monster);
    return at >= 0 ? this.killMobCount[at] : 0;
  }

  private checkActCondition(quest: LegacyQuest): boolean {
    for (let i = 0; i < quest.conditionCount; i++) {
      const act = quest.acts[i];
      if (act.requestClass[this.legacyClassIndex] < 1) continue;
      if (act.questType === LEGACY_ACT_ITEM && this.findQuestItems(act.itemType * MAX_ITEM_INDEX + act.itemSubType, act.itemNum, act.itemLevel)) {
        this.currPage = this.findQuestContext(quest, 1);
        return false;
      }
      if (act.questType === LEGACY_ACT_MONSTER && !this.killMobType.some((type, k) => type === act.itemType && this.killMobCount[k] >= act.itemNum)) {
        this.currPage = this.findQuestContext(quest, 1);
        return false;
      }
    }
    return true;
  }

  /** CheckQuestState */
  checkQuestState(): number {
    if (this.legacyClass === 255) this.legacyClass = this.hero.baseClass;
    const quest = this.scripts.legacy[this.currQuestIndex];
    this.currState = this.legacyState(this.currQuestIndex);
    if (!quest) return this.currState;
    if (this.currState === LegacyState.NO) {
      if (this.checkRequestCondition(quest)) this.currPage = this.findQuestContext(quest, 0);
    } else if (this.currState === LegacyState.ING) {
      if (this.checkActCondition(quest)) { this.currPage = this.findQuestContext(quest, 2); this.currState = LegacyState.ITEM; }
    } else if (this.currState === LegacyState.END) {
      this.currPage = this.findQuestContext(quest, 3);
    }
    return this.currState;
  }

  /** ShowDialogText: the dialog page shown (its text and numbered answers come from dialogLines/dialogAnswers). */
  showDialogText(index: number): void { this.dialogIndex = index; }

  dialogText(): string { return LEGACY_DIALOGS[this.dialogIndex]?.text ?? ''; }
  dialogAnswers(): string[] {
    const dialog = LEGACY_DIALOGS[this.dialogIndex];
    if (!dialog || dialog.numAnswer === 0) return [`1) ${QT.ConversationIsOver}`];
    return dialog.answers.slice(0, dialog.numAnswer).map((answer, i) => `${i + 1}) ${answer.text}`);
  }

  /** ShowQuestNpcWindow */
  showQuestNpcWindow(): void { this.checkQuestState(); this.showDialogText(this.currPage); }

  /** ShowQuestPreviewWindow (the quest info window's class change tab): the window's quest, not the NPC's. */
  showQuestPreviewWindow(): void {
    const previous = this.currQuestIndex;
    this.currQuestIndex = this.currQuestIndexWnd;
    this.checkQuestState();
    this.showDialogText(this.currPage);
    this.currQuestIndex = previous;
  }

  /** getCurrQuestState of the window's quest (the class change tab's state line). */
  windowQuestState(): number {
    const previous = this.currQuestIndex;
    this.currQuestIndex = this.currQuestIndexWnd;
    const state = this.checkQuestState();
    this.currQuestIndex = previous;
    return state;
  }

  windowQuestTitle(): string { return this.scripts.legacy[this.currQuestIndexWnd]?.name ?? ''; }

  /** ProcessNextProgress: false when the quest starts (LegacyQuestStateSetRequest), true when a condition fails. */
  processNextProgress(): boolean {
    const quest = this.currentLegacyQuest();
    if (!quest) return true;
    if (!this.checkRequestCondition(quest, true)) { this.showDialogText(this.currPage); return true; }
    this.setLegacyActive();
    return false;
  }

  setLegacyActive(): void { this.send('LegacyQuestStateSetRequest', { QuestNumber: this.currQuestIndex, NewState: 1 }); }

  /** BeQuestItem: whether the active quest's conditions are all met. */
  beQuestItem(): boolean {
    if (this.currState !== LegacyState.ING) return false;
    const quest = this.currentLegacyQuest();
    if (!quest) return false;
    let complete = false;
    for (let i = 0; i < quest.conditionCount; i++) {
      const act = quest.acts[i];
      if (act.requestClass[this.legacyClassIndex] <= 0) continue;
      if (act.questType === LEGACY_ACT_ITEM) {
        complete = true;
        if (this.findQuestItems(act.itemType * MAX_ITEM_INDEX + act.itemSubType, act.itemNum, act.itemLevel)) return false;
      } else if (act.questType === LEGACY_ACT_MONSTER) {
        complete = true;
        if (!this.killMobType.some((type, k) => type === act.itemType && this.killMobCount[k] >= act.itemNum)) return false;
      }
    }
    return complete;
  }

  static readonly LegacyState = LegacyState;
  static readonly LEGACY_ACT_ITEM = LEGACY_ACT_ITEM;
  static readonly LEGACY_ACT_MONSTER = LEGACY_ACT_MONSTER;
  static readonly MAX_ITEM_INDEX = MAX_ITEM_INDEX;
}
