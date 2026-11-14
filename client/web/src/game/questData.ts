// The quest texts and scripts the desktop client loads (CQuestMng::LoadQuestScript, CSQuest::OpenQuestScript),
// read the same way: every record xor'ed with the BUX code.
//  - Data/Local/NPCDialogue.bmd: each NPC's dialogue states (NPC number * 0x10000 + state): the NPC's words and
//    up to ten answers, each with what it leads to (another state, or 901+ for an action).
//  - Data/Local/QuestProgress.bmd: each quest step (group * 0x10000 + step): window kind, the NPC's and the
//    player's words, up to five answers, the subject and the summary.
//  - Data/Local/Eng/QuestWords_eng.bmd: the texts the two above point at, by number.
//  - Data/Local/Eng/Quest_eng.bmd: the class change quests (CSQuest, QUEST_ATTRIBUTE).
// And the desktop's text wrapping (DivideStringByPixel: ';' starts a new line, each line gets a leading space,
// then words are wrapped to a pixel width).

import { fetchBytes } from '../bmd.js';

const BUX = [0xfc, 0xcf, 0xab];

function bux(bytes: Uint8Array): Uint8Array {
  const out = new Uint8Array(bytes.length);
  for (let i = 0; i < bytes.length; i++) out[i] = bytes[i] ^ BUX[i % 3];
  return out;
}

export interface NpcDialogueEntry { npcWords: number; /** Ten pairs: answer text, result. */ answers: number[] }
export interface QuestProgressEntry {
  uiType: number; npcWords: number; playerWords: number; answers: number[]; subject: number; summary: number;
}

/** QUEST_CLASS_ACT: one condition of a class change quest. */
export interface LegacyQuestAct {
  live: number; questType: number; itemType: number; itemSubType: number; itemLevel: number; itemNum: number;
  requestType: number; requestClass: number[]; startText: number[];
}
/** QUEST_CLASS_REQUEST: what a class may start it with. */
export interface LegacyQuestRequest {
  live: number; type: number; completeQuestIndex: number; levelMin: number; levelMax: number; strength: number; zen: number; errorText: number;
}
/** QUEST_ATTRIBUTE. */
export interface LegacyQuest { conditionCount: number; requestCount: number; npcType: number; name: string; acts: LegacyQuestAct[]; requests: LegacyQuestRequest[] }

export interface QuestScripts {
  dialogues: Map<number, NpcDialogueEntry>;
  progress: Map<number, QuestProgressEntry>;
  words: Map<number, string>;
  legacy: LegacyQuest[];
}

const decoder = new TextDecoder();
const cString = (bytes: Uint8Array) => { const end = bytes.indexOf(0); return decoder.decode(end < 0 ? bytes : bytes.subarray(0, end)); };

function readDialogues(file: Uint8Array): Map<number, NpcDialogueEntry> {
  const RECORD = 4 + 4 + 20 * 4;
  const map = new Map<number, NpcDialogueEntry>();
  for (let at = 0; at + RECORD <= file.length; at += RECORD) {
    const record = bux(file.subarray(at, at + RECORD));
    const view = new DataView(record.buffer);
    const answers: number[] = [];
    for (let i = 0; i < 20; i++) answers.push(view.getInt32(8 + i * 4, true));
    map.set(view.getUint32(0, true), { npcWords: view.getInt32(4, true), answers });
  }
  return map;
}

function readProgress(file: Uint8Array): Map<number, QuestProgressEntry> {
  const RECORD = 4 + 37; // index, then SQuestProgress packed to one byte
  const map = new Map<number, QuestProgressEntry>();
  for (let at = 0; at + RECORD <= file.length; at += RECORD) {
    const record = bux(file.subarray(at, at + RECORD));
    const view = new DataView(record.buffer);
    map.set(view.getUint32(0, true), {
      uiType: record[4], npcWords: view.getInt32(5, true), playerWords: view.getInt32(9, true),
      answers: [0, 1, 2, 3, 4].map((i) => view.getInt32(13 + i * 4, true)),
      subject: view.getInt32(33, true), summary: view.getInt32(37, true),
    });
  }
  return map;
}

function readWords(file: Uint8Array): Map<number, string> {
  const map = new Map<number, string>();
  let at = 0;
  while (at + 6 <= file.length) {
    const header = new DataView(bux(file.subarray(at, at + 6)).buffer);
    const index = header.getInt32(0, true), length = header.getInt16(4, true);
    at += 6;
    if (length < 0 || at + length > file.length) break;
    map.set(index, cString(bux(file.subarray(at, at + length))));
    at += length;
  }
  return map;
}

// QuestAttributeFile: 744 bytes with the C++ alignment (acts of 24 bytes from 38, requests of 20 bytes from 424).
function readLegacyQuests(file: Uint8Array): LegacyQuest[] {
  const RECORD = 744, MAX_CONDITION = 16, MAX_REQUEST = 16, MAX_CLASS = 7;
  const quests: LegacyQuest[] = [];
  for (let at = 0; at + RECORD <= file.length && quests.length < 200; at += RECORD) {
    const r = bux(file.subarray(at, at + RECORD));
    const v = new DataView(r.buffer);
    const acts: LegacyQuestAct[] = [];
    for (let i = 0; i < MAX_CONDITION; i++) {
      const o = 38 + i * 24;
      acts.push({
        live: r[o], questType: r[o + 1], itemType: v.getUint16(o + 2, true), itemSubType: r[o + 4], itemLevel: r[o + 5], itemNum: r[o + 6],
        requestType: r[o + 7], requestClass: [...r.subarray(o + 8, o + 8 + MAX_CLASS)], startText: [0, 1, 2, 3].map((k) => v.getInt16(o + 16 + k * 2, true)),
      });
    }
    const requests: LegacyQuestRequest[] = [];
    for (let i = 0; i < MAX_REQUEST; i++) {
      const o = 424 + i * 20;
      requests.push({
        live: r[o], type: r[o + 1], completeQuestIndex: v.getUint16(o + 2, true), levelMin: v.getUint16(o + 4, true), levelMax: v.getUint16(o + 6, true),
        strength: v.getUint16(o + 8, true), zen: v.getUint32(o + 12, true), errorText: v.getInt16(o + 16, true),
      });
    }
    quests.push({ conditionCount: v.getInt16(0, true), requestCount: v.getInt16(2, true), npcType: v.getUint16(4, true), name: cString(r.subarray(6, 38)), acts, requests });
  }
  return quests;
}

// HNMO's trial quests (group 40 at the Elf Soldier, from level 250; NEW_SKILLS.md): their steps'
// texts aren't in the game's files. Step 0 starts it, 1 is the quest, 2 is the answer to a refusal.
const TRIAL_GROUP = 40;
const TRIAL_WORDS_FIRST = 9_400_000;
function addTrialQuestTexts(progress: Map<number, QuestProgressEntry>, words: Map<number, string>): void {
  const texts = [
    'Trial of Strength', // 0: the subject
    'Defeat 200 of the monsters I name and bring me five Jewels of Chaos. Then I will teach you the hidden skill of your class.', // 1: the summary
    'You have grown strong. Strong enough to learn what few of your kind ever learn. Will you take the trial?', // 2
    'I will take the trial.', // 3
    'Not now.', // 4
    'Have you done what I asked? Show me the proof of your strength.', // 5
    'Come back when you are ready for it.', // 6
    'I am ready.', // 7
  ];
  texts.forEach((text, i) => words.set(TRIAL_WORDS_FIRST + i, text));
  const w = (i: number) => TRIAL_WORDS_FIRST + i;
  const index = (number: number) => ((TRIAL_GROUP << 16) | number) >>> 0;
  if (!progress.has(index(0))) progress.set(index(0), { uiType: 0, npcWords: w(2), playerWords: w(7), answers: [w(3), w(4), 0, 0, 0], subject: w(0), summary: w(1) });
  if (!progress.has(index(1))) progress.set(index(1), { uiType: 0, npcWords: w(5), playerWords: 0, answers: [0, 0, 0, 0, 0], subject: w(0), summary: w(1) });
  if (!progress.has(index(2))) progress.set(index(2), { uiType: 0, npcWords: w(6), playerWords: 0, answers: [0, 0, 0, 0, 0], subject: w(0), summary: w(1) });
}

let scripts: Promise<QuestScripts> | null = null;

export function loadQuestScripts(): Promise<QuestScripts> {
  scripts ??= (async () => {
    const [dialogues, progress, words, legacy] = await Promise.all([
      fetchBytes('/data/Local/NPCDialogue.bmd'), fetchBytes('/data/Local/QuestProgress.bmd'),
      fetchBytes('/data/Local/Eng/QuestWords_eng.bmd'), fetchBytes('/data/Local/Eng/Quest_eng.bmd'),
    ]);
    const result = { dialogues: readDialogues(dialogues), progress: readProgress(progress), words: readWords(words), legacy: readLegacyQuests(legacy) };
    addTrialQuestTexts(result.progress, result.words);
    return result;
  })();
  return scripts;
}

// ---------- text wrapping ----------

let measureContext: CanvasRenderingContext2D | null = null;
let measureFamily = 'Tahoma, Arial, sans-serif';
/** The font family the windows draw their texts with (read from an opened document), for textWidth. */
export function setMeasureFont(family: string): void { if (family) measureFamily = family; }
/** A text's width at the interface's normal font (12 reference pixels). */
export function textWidth(text: string): number {
  measureContext ??= document.createElement('canvas').getContext('2d');
  if (!measureContext) return text.length * 6;
  measureContext.font = `12px ${measureFamily}`;
  return measureContext.measureText(text).width;
}

/** CutStr: words wrapped to `width` pixels (a word longer than a line is cut). */
function cutText(text: string, width: number): string[] {
  const lines: string[] = [];
  let line = '';
  for (const word of text.split(/(?<= )/)) {
    if (line && textWidth(line + word.trimEnd()) > width) { lines.push(line.trimEnd()); line = ''; }
    let rest = word;
    while (!line && textWidth(rest.trimEnd()) > width && rest.length > 1) {
      let cut = rest.length - 1;
      while (cut > 1 && textWidth(rest.slice(0, cut)) > width) cut--;
      lines.push(rest.slice(0, cut));
      rest = rest.slice(cut);
    }
    line += rest;
  }
  if (line.trim()) lines.push(line.trimEnd());
  return lines;
}

/** DivideStringByPixel: ';' breaks lines, each gets a leading space (spaceInsert), then words wrap to `width`. */
export function divideStringByPixel(text: string | undefined, width: number, maxLines = 35, spaceInsert = true): string[] {
  if (!text) return [];
  const lines: string[] = [];
  for (const part of text.split(';')) {
    if (!part) continue;
    lines.push(...cutText(spaceInsert ? ` ${part}` : part, width));
  }
  return lines.slice(0, maxLines);
}

/** ReduceStringByPixel: the text cut to fit `width` pixels, with "..." when it was cut. */
export function reduceStringByPixel(text: string, width: number): string {
  if (textWidth(text) <= width) return text;
  let end = text.length;
  while (end > 0 && textWidth(`${text.slice(0, end)}...`) > width) end--;
  return `${text.slice(0, end)}...`;
}
