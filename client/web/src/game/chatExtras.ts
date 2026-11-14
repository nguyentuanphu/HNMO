// HNMO's own additions to the chat (game/chat.ts), kept apart from the desktop's behaviour:
//  - the blocked players (kept in the browser): their lines and whispers don't show;
//  - the word filter: rude words in other players' lines show as ***, English and Vietnamese
//    (with or without the accents), switchable in the settings;
//  - the chat history of a character, kept in the browser, so a reload or the next login shows
//    the last lines again.

const BLOCKED_KEY = 'hnmo.chat.blocked';
const FILTER_KEY = 'hnmo.chat.filter';
const HISTORY_KEY = 'hnmo.chat.history.';

function read<T>(key: string, fallback: T): T {
  try {
    const text = localStorage.getItem(key);
    return text === null ? fallback : JSON.parse(text) as T;
  } catch {
    return fallback;
  }
}

function write(key: string, value: unknown): void {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* private window or full storage */ }
}

// ---------- blocked players ----------

const blocked = new Set<string>(read<string[]>(BLOCKED_KEY, []).map((name) => name.toLowerCase()));

export function isBlocked(name: string): boolean { return !!name && blocked.has(name.toLowerCase()); }
export function blockedNames(): string[] { return [...blocked].sort(); }
export function setBlocked(name: string, on: boolean): void {
  if (!name) return;
  if (on) blocked.add(name.toLowerCase()); else blocked.delete(name.toLowerCase());
  write(BLOCKED_KEY, [...blocked]);
}

// ---------- the word filter ----------

// Whole words that are replaced, English and Vietnamese. Vietnamese words count only with their
// accents (without them most are everyday words: "di", "cac", "lon"); the slang below is written
// without accents anyway.
const WORDS = [
  'fuck', 'fucking', 'fucker', 'shit', 'bitch', 'cunt', 'dick', 'pussy', 'asshole', 'bastard', 'motherfucker', 'nigger', 'retard', 'whore', 'slut',
  'địt', 'đụ', 'đéo', 'cặc', 'lồn', 'buồi', 'đĩ', 'óc chó', 'mẹ mày', 'con mẹ mày', 'thằng chó', 'chó chết', 'súc vật',
  'đm', 'đmm', 'dmm', 'đcm', 'dcm', 'đcmm', 'dcmm', 'vcl', 'vkl', 'clm', 'cmm', 'đkm', 'dkm',
];

const escape = (word: string) => word.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const variants = [...WORDS].sort((a, b) => b.length - a.length);
// Letters of both alphabets count as word characters, so "class" isn't hit by "ass".
const LETTER = 'A-Za-z0-9À-ỹđĐ';
const PATTERN = new RegExp(`(^|[^${LETTER}])(${variants.map(escape).join('|')})(?=$|[^${LETTER}])`, 'giu');

let filterOn = read<boolean>(FILTER_KEY, true);
export function wordFilterOn(): boolean { return filterOn; }
export function setWordFilter(on: boolean): void { filterOn = on; write(FILTER_KEY, on); }

/** The text with the filtered words as ***, when the filter is on. */
export function filterWords(text: string): string {
  if (!filterOn) return text;
  return text.normalize('NFC').replace(PATTERN, (_match, before: string, word: string) => before + '*'.repeat(Math.max(3, word.length)));
}

// ---------- the history ----------

export interface StoredLine { id: string; text: string; kind: string; time: number; count: number }

export function loadHistory(owner: string): { chat: StoredLine[]; system: StoredLine[] } {
  if (!owner) return { chat: [], system: [] };
  const saved = read<{ chat?: StoredLine[]; system?: StoredLine[] }>(HISTORY_KEY + owner.toLowerCase(), {});
  const ok = (line: StoredLine) => typeof line?.text === 'string' && typeof line.kind === 'string';
  return { chat: (saved.chat ?? []).filter(ok), system: (saved.system ?? []).filter(ok) };
}

export function saveHistory(owner: string, chat: StoredLine[], system: StoredLine[]): void {
  if (!owner) return;
  write(HISTORY_KEY + owner.toLowerCase(), { chat: chat.slice(-100), system: system.slice(-30) });
}
