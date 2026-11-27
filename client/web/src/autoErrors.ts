// Automatic error reports (an HNMO extra; owner 2026-11-27): when the page meets an error (an uncaught error, an
// unhandled promise rejection, a packet its handlers failed on, a lost WebGL context, an unexpected disconnect), the game
// tells the server's owner on its own (F5 60). The owner sees them grouped on the admin panel (Bug reports, Automatic)
// and chooses what goes on to the HNMO developer. Each error goes once per page load, at most 5 an hour per browser.
// A report carries the error, the top of its stack, the build, the browser, the device, the map and the GPU: never
// the login, the ticket, a password or chat text.

import { HNMO_BUILD_DATE } from './build.js';

export type AutoErrorKind = 'error' | 'rejection' | 'packet' | 'webgl' | 'disconnect';

/** What the server gets (JSON of F5 60). */
export interface AutoErrorPayload {
  fingerprint: string;
  kind: AutoErrorKind;
  message: string;
  stack: string;
  version: string;
  browser: string;
  os: string;
  device: string;
  map: string;
  gpu: string;
}

/** Reports a browser sends in an hour, at most. */
export const MAX_PER_HOUR = 5;
const HOUR_MS = 60 * 60 * 1000;
const RATE_KEY = 'hnmo.autoErrors.sent';
const PENDING_KEY = 'hnmo.autoErrors.pending';
const MAX_QUEUE = 5;
const STACK_LINES = 6;

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
    if (!this.storage) return this.memory;
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
    try { this.storage.setItem(RATE_KEY, JSON.stringify(times)); } catch { /* storage full or blocked: memory only */ }
  }
}

/** JSON with only ASCII characters (the server reads \u escapes). */
function asciiJson(value: unknown): string {
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

let gpuCache: string | null = null;
function gpuName(): string {
  if (gpuCache !== null) return gpuCache;
  try {
    const gl = document.createElement('canvas').getContext('webgl2');
    const info = gl?.getExtension('WEBGL_debug_renderer_info');
    gpuCache = gl ? String(info ? gl.getParameter(info.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER)) : 'no WebGL2';
  } catch {
    gpuCache = 'unknown';
  }
  return gpuCache;
}

function storageOrNull(): SmallStorage | null {
  try { return window.localStorage; } catch { return null; }
}

type Sender = (json: string) => void;

const sentThisLoad = new Set<string>();
const queue: string[] = [];
let sender: Sender | null = null;
let mapName: () => string = () => '';
let limit: HourlyLimit | null = null;
let installed = false;
let leaving = false;

function readPending(): string[] {
  const storage = storageOrNull();
  if (!storage) return [];
  try {
    const parsed: unknown = JSON.parse(storage.getItem(PENDING_KEY) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter((s): s is string => typeof s === 'string').slice(0, MAX_QUEUE) : [];
  } catch {
    return [];
  }
}

function writePending(items: string[]): void {
  const storage = storageOrNull();
  if (!storage) return;
  try {
    if (items.length) storage.setItem(PENDING_KEY, JSON.stringify(items.slice(-MAX_QUEUE)));
    else storage.removeItem(PENDING_KEY);
  } catch { /* nothing to keep it in */ }
}

function flush(): void {
  if (!sender) return;
  const items = [...readPending(), ...queue.splice(0)];
  writePending([]);
  for (const json of items) {
    try { sender(json); } catch { /* the connection went: dropped */ }
  }
}

/**
 * Reports an error on its own (once per fingerprint and page load, within the hourly limit). Before the game is
 * connected, or after a disconnect, the report waits for the next connection.
 */
export function reportAutoError(kind: AutoErrorKind, message: string, stack = '', file = '', line: number | string = 0): boolean {
  if (typeof window === 'undefined') return false;
  // Leaving the page (reload, close, another address) closes the connection: that's no error.
  if (kind === 'disconnect' && leaving) return false;
  const frame = !file ? firstFrame(stack) : null;
  const fingerprint = fingerprintOf(message, file || frame?.file || kind, line || frame?.line || 0);
  if (sentThisLoad.has(fingerprint)) return false;
  sentThisLoad.add(fingerprint);
  limit ??= new HourlyLimit(storageOrNull());
  if (!limit.take()) return false;
  const ua = navigator.userAgent;
  const payload: AutoErrorPayload = {
    fingerprint,
    kind,
    message: scrub(message).slice(0, 500),
    stack: scrub(stack).split('\n').map((l) => l.trim()).filter(Boolean).slice(0, STACK_LINES).join('\n').slice(0, 1500),
    version: `web ${HNMO_BUILD_DATE}`,
    browser: browserName(ua),
    os: osName(ua),
    device: `${screen.width}x${screen.height} dpr ${devicePixelRatio} touch ${navigator.maxTouchPoints > 0 ? 'yes' : 'no'}`,
    map: (() => { try { return mapName(); } catch { return ''; } })(),
    gpu: gpuName(),
  };
  const json = asciiJson(payload);
  if (sender && kind !== 'disconnect') {
    try { sender(json); return true; } catch { /* falls through to the queue */ }
  }
  if (kind === 'disconnect') writePending([...readPending(), json]);
  else { queue.push(json); if (queue.length > MAX_QUEUE) queue.shift(); }
  return true;
}

/** The connection reports go through (after login), or null when it closed. Waiting reports go at once. */
export function setAutoErrorSender(send: Sender | null, map?: () => string): void {
  sender = send;
  if (map) mapName = map;
  flush();
}

/** Listens for the page's uncaught errors, unhandled rejections and lost WebGL contexts (called once at start). */
export function installAutoErrorReports(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
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
