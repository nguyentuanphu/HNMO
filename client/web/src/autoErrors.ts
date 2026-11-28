// Automatic error reports (an HNMO extra; owner 2026-11-27): when the page meets an error (an uncaught error, an
// unhandled promise rejection, a packet its handlers failed on, a lost WebGL context, an unexpected disconnect), the game
// tells the server's owner on its own (F5 60). The owner sees them grouped on the admin panel (Bug reports, Automatic)
// and chooses what goes on to the HNMO developer. Each error goes once per page load, at most 5 an hour per browser.
// Reports carry an error kind, a known source file and line where available, and coarse client metadata.

import { HNMO_BUILD_DATE } from './build.js';

export type AutoErrorKind = 'error' | 'rejection' | 'packet' | 'webgl' | 'disconnect';

/** What the server gets (JSON of F5 60). */
export interface AutoErrorPayload {
  fingerprint: string;
  kind: AutoErrorKind;
  version: string;
  browser: string;
  os: string;
}

/** Reports a browser sends in an hour, at most. */
export const MAX_PER_HOUR = 5;
const HOUR_MS = 60 * 60 * 1000;
const RATE_KEY = 'hnmo.autoErrors.sent';
const LEGACY_PENDING_KEY = 'hnmo.autoErrors.pending';
const PENDING_PREFIX = 'hnmo.autoErrors.pending.v2.';
const SOURCE_FILES = new Set(['main.js', 'connection.js', 'session.js', 'world.js', 'renderer.js']);
const MAX_QUEUE = 5;

/** Minimal storage (localStorage, or a stand-in in tests). */
export interface SmallStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

/**
 * A script's path as it goes in a fingerprint: no origin, query string or hash (the address may hold a login ticket),
 * and no numbers (build hashes and versions in file names), so one error in two builds is one error.
 */
export function cleanPath(path: string): string {
  let p = (path || '').trim();
  p = p.replace(/[?#].*$/, '');
  p = p.replace(/^[a-z][a-z0-9+.-]*:\/\/[^/]*/i, '');
  p = p.replace(/\d+/g, '');
  return p.slice(0, 160);
}

/** Message, file and line of an error, as one key: the same error from any page load or build gives the same one. */
export function fingerprintOf(message: string, file: string, line: number | string): string {
  const text = scrub(String(message || '')).replace(/\s+/g, ' ').trim().slice(0, 200);
  return `${text}|${cleanPath(file)}|${Number(line) || 0}`;
}

/** URLs in a text without their query strings and hashes; lines naming a password or ticket dropped. */
export function scrub(text: string): string {
  return String(text || '')
    .split('\n')
    .filter((line) => !/password|ticket/i.test(line))
    .join('\n')
    .replace(/(https?:\/\/[^\s?#)]*)[?#][^\s)]*/gi, '$1');
}

/** The first "file:line" of a stack. */
export function firstFrame(stack: string): { file: string; line: number } | null {
  const match = /((?:https?:\/\/|\/)[^\s()]+?):(\d+)(?::\d+)?\)?\s*$/m.exec(String(stack || ''));
  return match ? { file: match[1], line: Number(match[2]) } : null;
}

/** The hourly limit of a browser, kept in its storage (a page without storage keeps it in memory). */
export class HourlyLimit {
  private memory: number[] = [];
  private storageFailed = false;

  constructor(private readonly storage: SmallStorage | null, private readonly now: () => number = () => Date.now(), private readonly max = MAX_PER_HOUR) {}

  /** Whether one more report may go now; if so, it is counted. */
  take(): boolean {
    const now = this.now();
    const recent = this.read().filter((t) => now - t < HOUR_MS && t <= now);
    if (recent.length >= this.max) return false;
    recent.push(now);
    this.write(recent);
    return true;
  }

  private read(): number[] {
    if (!this.storage || this.storageFailed) return this.memory;
    try {
      const parsed: unknown = JSON.parse(this.storage.getItem(RATE_KEY) ?? '[]');
      return Array.isArray(parsed) ? parsed.filter((t): t is number => typeof t === 'number') : [];
    } catch {
      return this.memory;
    }
  }

  private write(times: number[]): void {
    this.memory = times;
    if (!this.storage) return;
    try { this.storage.setItem(RATE_KEY, JSON.stringify(times)); } catch { this.storageFailed = true; }
  }
}

/** JSON with only ASCII characters (the server reads \u escapes). */
function asciiJson(value: AutoErrorPayload): string {
  return JSON.stringify(value).replace(/[\u007f-￿]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
}

function browserName(ua: string): string {
  const pick = (re: RegExp, name: string) => { const m = re.exec(ua); return m ? `${name} ${m[1]}` : null; };
  return pick(/Edg\/(\d+)/, 'Edge') ?? pick(/OPR\/(\d+)/, 'Opera') ?? pick(/Firefox\/(\d+)/, 'Firefox')
    ?? pick(/Chrome\/(\d+)/, 'Chrome') ?? pick(/Version\/(\d+)[^ ]* .*Safari/, 'Safari') ?? 'other';
}

function osName(ua: string): string {
  if (/Android/i.test(ua)) return 'Android';
  if (/iPhone|iPad|iPod/i.test(ua)) return 'iOS';
  if (/Mac OS X|Macintosh/i.test(ua)) return navigator.maxTouchPoints > 1 ? 'iPadOS' : 'macOS';
  if (/Windows/i.test(ua)) return 'Windows';
  if (/CrOS/i.test(ua)) return 'ChromeOS';
  if (/Linux/i.test(ua)) return 'Linux';
  return 'other';
}

function storageOrNull(): SmallStorage | null {
  try { return window.localStorage; } catch { return null; }
}

type Sender = (json: string) => void;

const sentThisLoad = new Set<string>();
let sender: Sender | null = null;
let pendingKey: string | null = null;
let queue: string[] = [];
let limit: HourlyLimit | null = null;
let installed = false;
let leaving = false;

/** Binds queued reports to the authenticated account and endpoint within the tab. */
export async function autoErrorScope(host: string, port: number, username: string): Promise<string | null> {
  try {
    const identity = JSON.stringify([host.toLowerCase(), port, username.trim().toLowerCase()]);
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(identity));
    return Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, '0')).join('');
  } catch { return null; }
}

/** Returns a signature composed only of a known diagnostic kind, source file and line. */
export function diagnosticSignature(kind: AutoErrorKind, file: string, line: number | string): string {
  const name = cleanPath(file).split('/').pop() ?? '';
  const number = Number(line);
  return SOURCE_FILES.has(name) && Number.isInteger(number) && number > 0 && number <= 50000
    ? `${kind}|${name}|${number}` : kind;
}

function readPending(key: string): string[] {
  try {
    const raw = window.sessionStorage.getItem(key) ?? '[]';
    if (raw.length > 8192) return [];
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === 'string' && item.length <= 512).slice(0, MAX_QUEUE) : [];
  } catch { return []; }
}

function saveQueue(): void {
  if (!pendingKey) return;
  try {
    if (queue.length) window.sessionStorage.setItem(pendingKey, JSON.stringify(queue));
    else window.sessionStorage.removeItem(pendingKey);
  } catch { /* Keep the in-memory queue when storage is unavailable. */ }
}

/** Sends a coarse error report for the current authenticated game connection. */
export function reportAutoError(kind: AutoErrorKind, _message: string, stack = '', file = '', line: number | string = 0): boolean {
  if (!sender || leaving) return false;
  const frame = !file ? firstFrame(stack) : null;
  const fingerprint = diagnosticSignature(kind, file || frame?.file || '', line || frame?.line || 0);
  if (sentThisLoad.has(fingerprint)) return false;
  limit ??= new HourlyLimit(storageOrNull());
  if (!limit.take()) return false;
  sentThisLoad.add(fingerprint);

  const ua = navigator.userAgent;
  const json = asciiJson({ fingerprint, kind, version: `web ${HNMO_BUILD_DATE}`, browser: browserName(ua), os: osName(ua) });
  if (kind !== 'disconnect') {
    try { sender(json); return true; } catch { /* Retry on the next authenticated connection for this scope. */ }
  }

  queue.push(json);
  queue = queue.slice(-MAX_QUEUE);
  saveQueue();
  return true;
}

/** Installs a sender after authentication, then drains the matching queue until sending fails. */
export function setAutoErrorSender(send: Sender, scope: string | null): () => void {
  const key = scope ? PENDING_PREFIX + scope : null;
  if (key !== pendingKey || !key) {
    queue = key ? readPending(key) : [];
    pendingKey = key;
    sentThisLoad.clear();
  }

  sender = send;
  while (queue.length) {
    try { send(queue[0]); } catch { break; }
    queue.shift();
    saveQueue();
  }

  return () => {
    if (sender !== send) return;
    reportAutoError('disconnect', '');
    sender = null;
  };
}

/** Listens for the page's uncaught errors, unhandled rejections and lost WebGL contexts (called once at start). */
export function installAutoErrorReports(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  try { storageOrNull()?.removeItem(LEGACY_PENDING_KEY); } catch { /* Storage is optional. */ }
  window.addEventListener('error', (event) => {
    // A resource that failed to load (an image, a script tag) has no message: not an error of the game's code.
    if (!event.message) return;
    const stack = event.error instanceof Error ? event.error.stack ?? '' : '';
    reportAutoError('error', event.message, stack, event.filename, event.lineno);
  });
  window.addEventListener('unhandledrejection', (event) => {
    const reason: unknown = event.reason;
    const message = reason instanceof Error ? `${reason.name}: ${reason.message}` : String(reason);
    reportAutoError('rejection', message, reason instanceof Error ? reason.stack ?? '' : '');
  });
  window.addEventListener('beforeunload', () => { leaving = true; });
  window.addEventListener('pagehide', () => { leaving = true; });
  window.addEventListener('pageshow', () => { leaving = false; });
  // webglcontextlost doesn't bubble: caught on its way down.
  window.addEventListener('webglcontextlost', () => { reportAutoError('webgl', 'WebGL context lost', '', 'webgl', 0); }, true);
}
