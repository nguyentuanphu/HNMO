// HNMO's own additions to the chat (game/chat.ts), kept apart from the desktop's behaviour:
//  - the blocked players (kept in the browser): their lines and whispers don't show;
//  - the word filter: rude words in other players' lines show as ***, English and Vietnamese
//    (with or without the accents), switchable in the settings;
//  - the chat history of a character, kept in the browser, so a reload or the next login shows
//    the last lines again.

const BLOCKED_KEY = 'hnmo.chat.blocked';
const FILTER_KEY = 'hnmo.chat.filter';
const HISTORY_KEY = 'hnmo.chat.history.';
const CHAT_SIZE_KEY = 'hnmo.chat.size';
const WINDOW_SIZE_KEY = 'hnmo.window.size.';

export interface ChatWindowSize { width: number; lines: number }
export interface BoxWindowSize { width: number; height: number }
export interface BoxWindowBounds { minW: number; maxW: number; minH: number; maxH: number }

export const CHAT_WIDTH_BOUNDS = { min: 260, max: 640, defaultWidth: 281 } as const;
export const CHAT_LINES_BOUNDS = { min: 3, max: 24, defaultLines: 6 } as const;

export function clampChatSize(size: Partial<ChatWindowSize> | null | undefined): ChatWindowSize {
  const rawW = Number(size?.width);
  const rawL = Number(size?.lines);
  const width = Number.isFinite(rawW)
    ? Math.max(CHAT_WIDTH_BOUNDS.min, Math.min(CHAT_WIDTH_BOUNDS.max, Math.round(rawW)))
    : CHAT_WIDTH_BOUNDS.defaultWidth;
  const lines = Number.isFinite(rawL)
    ? Math.max(CHAT_LINES_BOUNDS.min, Math.min(CHAT_LINES_BOUNDS.max, Math.round(rawL)))
    : CHAT_LINES_BOUNDS.defaultLines;
  return { width, lines };
}

export function loadChatSize(): ChatWindowSize {
  return clampChatSize(read<Partial<ChatWindowSize> | null>(CHAT_SIZE_KEY, null));
}

export function saveChatSize(size: Partial<ChatWindowSize>): ChatWindowSize {
  const clamped = clampChatSize(size);
  write(CHAT_SIZE_KEY, clamped);
  return clamped;
}

export function clampBoxWindowSize(size: Partial<BoxWindowSize> | null | undefined, bounds: BoxWindowBounds): BoxWindowSize | null {
  if (!size) return null;
  const rawW = Number(size.width);
  const rawH = Number(size.height);
  if (!Number.isFinite(rawW) || !Number.isFinite(rawH) || rawW <= 0 || rawH <= 0) return null;
  return {
    width: Math.max(bounds.minW, Math.min(bounds.maxW, Math.round(rawW))),
    height: Math.max(bounds.minH, Math.min(bounds.maxH, Math.round(rawH))),
  };
}

export function loadWindowSize(windowKey: string, bounds: BoxWindowBounds): BoxWindowSize | null {
  if (!windowKey) return null;
  return clampBoxWindowSize(read<Partial<BoxWindowSize> | null>(WINDOW_SIZE_KEY + windowKey, null), bounds);
}

export function saveWindowSize(windowKey: string, size: BoxWindowSize, bounds: BoxWindowBounds): BoxWindowSize | null {
  if (!windowKey) return null;
  const clamped = clampBoxWindowSize(size, bounds);
  if (!clamped) return null;
  write(WINDOW_SIZE_KEY + windowKey, clamped);
  return clamped;
}

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
