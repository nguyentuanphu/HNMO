// The game's language: English (the default) or Vietnamese. The interface texts are the desktop's own
// English texts; the Vietnamese ones (public/data/lang/vi.json) were matched to them line by line.
// Instead of touching every window, the page's text is translated where it appears: each text node
// and the title / placeholder / aria-label attributes whose whole text is a known English text, or
// one with numbers and names in it ("Level: %d"), are shown in Vietnamese. Players' chat and names
// are left alone. Switching back to English puts the original texts back.

import { gameName } from './gameName.js';

export type Language = 'en' | 'vi';

const STORAGE_KEY = 'hnmo.language';
const ATTRIBUTES = ['title', 'placeholder', 'aria-label'] as const;
/** Elements whose text is never translated: what players type and say. */
const SKIP = '#chatLog, #chatInput, input, textarea, script, style, [data-no-i18n]';
const MAX_CACHE = 8000;

interface LanguageFile { texts: Record<string, string> }
interface Pattern { regex: RegExp; template: string }

let language: Language = 'en';
let texts = new Map<string, string>();
let patterns: Pattern[] = [];
const cache = new Map<string, string | null>();
/** The English original of each translated text node and attribute, to switch back. */
const originalText = new WeakMap<Text, string>();
const originalAttributes = new WeakMap<Element, Map<string, string>>();
let observer: MutationObserver | null = null;
let busy = false;
const listeners = new Set<(language: Language) => void>();

const FORMAT = /%(?:I64d|lld|[-+ #0]*\d*(?:\.\d+)?[dsfuxXlc])/g;

export function currentLanguage(): Language {
  return language;
}

export function onLanguageChange(listener: (language: Language) => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** The Vietnamese of an English text (whole text, or one with values in it); null when unknown. */
export function translate(english: string): string | null {
  if (language === 'en') return null;
  const key = english.trim();
  if (!key || key.length > 400 || !/[A-Za-z]/.test(key)) return null;
  const cached = cache.get(key);
  if (cached !== undefined) return cached;
  let result = texts.get(key) ?? null;
  if (result === null) {
    for (const { regex, template } of patterns) {
      const match = regex.exec(key);
      if (!match) continue;
      let index = 1;
      result = template.replace(FORMAT, () => match[index++] ?? '');
      break;
    }
  }
  if (result !== null) {
    result = result.replaceAll('{game}', gameName());
    // Keep the spaces around the text as they were (layout depends on them).
    const lead = english.match(/^\s*/)![0], trail = english.match(/\s*$/)![0];
    result = lead + result + trail;
  }
  if (cache.size > MAX_CACHE) cache.clear();
  cache.set(key, result);
  return result;
}

function escapeRegex(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function buildPatterns(entries: Record<string, string>): Pattern[] {
  const list: Pattern[] = [];
  for (const [english, vietnamese] of Object.entries(entries)) {
    const parts = english.split(FORMAT);
    if (parts.length < 2) continue;
    const literal = parts.join('').replace(/\W/g, '');
    if (literal.length < 3) continue; // "%d", "%s %s": too general to recognise
    const source = parts.map(escapeRegex).join('(.+?)');
    list.push({ regex: new RegExp(`^${source}$`), template: vietnamese });
  }
  // Longer texts first, so "Level: %d (%d)" wins over "Level: %d".
  return list.sort((a, b) => b.regex.source.length - a.regex.source.length);
}

function skipped(node: Node): boolean {
  const element = node instanceof Element ? node : node.parentElement;
  return !element || element.closest(SKIP) !== null;
}

function translateTextNode(node: Text): void {
  if (skipped(node)) return;
  const english = originalText.get(node) ?? node.data;
  if (language === 'en') {
    if (originalText.has(node) && node.data !== english) node.data = english;
    return;
  }
  // A window that changed its own text since: that's the new English.
  const shown = node.data;
  const known = originalText.get(node);
  const source = known !== undefined && shown !== translate(known) ? shown : english;
  const vietnamese = translate(source);
  if (vietnamese === null) { originalText.delete(node); return; }
  originalText.set(node, source);
  if (shown !== vietnamese) node.data = vietnamese;
}

function translateAttributes(element: Element): void {
  if (element.closest('script, style, [data-no-i18n]')) return;
  for (const name of ATTRIBUTES) {
    const value = element.getAttribute(name);
    if (value === null) continue;
    let originals = originalAttributes.get(element);
    const stored = originals?.get(name);
    if (language === 'en') {
      if (stored !== undefined && value !== stored) element.setAttribute(name, stored);
      continue;
    }
    const english = stored !== undefined && value === translate(stored) ? stored : value;
    const vietnamese = translate(english);
    if (vietnamese === null) continue;
    if (!originals) originalAttributes.set(element, (originals = new Map()));
    originals.set(name, english);
    if (value !== vietnamese) element.setAttribute(name, vietnamese);
  }
}

function translateTree(root: Node): void {
  if (root instanceof Text) { translateTextNode(root); return; }
  if (!(root instanceof Element) && !(root instanceof Document) && !(root instanceof DocumentFragment)) return; // ShadowRoot is a DocumentFragment
  if (root instanceof Element) translateAttributes(root);
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    if (node instanceof Text) translateTextNode(node);
    else if (node instanceof Element) translateAttributes(node);
  }
}

const OBSERVE: MutationObserverInit = { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: [...ATTRIBUTES] };
/** The game windows' shadow roots (rml.ts): the page's observer doesn't see into them. */
const shadowRoots = new Set<ShadowRoot>();

function ensureObserver(): MutationObserver {
  observer ??= new MutationObserver((records) => {
    if (busy || language === 'en' && !records.some((record) => record.type !== 'childList')) return;
    busy = true;
    try {
      for (const record of records) {
        if (record.type === 'characterData') translateTextNode(record.target as Text);
        else if (record.type === 'attributes') translateAttributes(record.target as Element);
        else record.addedNodes.forEach(translateTree);
      }
    } finally {
      busy = false;
    }
  });
  return observer;
}

function observe(): void {
  ensureObserver().observe(document.body, OBSERVE);
  for (const root of shadowRoots) ensureObserver().observe(root, OBSERVE);
}

/** A game window's shadow root: translated now and whenever its texts change. */
export function watchRoot(root: ShadowRoot): void {
  for (const known of shadowRoots) if (!known.host.isConnected) shadowRoots.delete(known);
  shadowRoots.add(root);
  if (language === 'en') return;
  busy = true;
  try { translateTree(root); } finally { busy = false; }
  ensureObserver().observe(root, OBSERVE);
}

async function loadTexts(): Promise<void> {
  if (texts.size) return;
  const response = await fetch('/data/lang/vi.json');
  if (!response.ok) throw new Error(`${response.status} /data/lang/vi.json`);
  const file = (await response.json()) as LanguageFile;
  texts = new Map(Object.entries(file.texts));
  patterns = buildPatterns(file.texts);
}

/** Switches the language, remembers it in this browser, and redraws the page's texts. */
export async function setLanguage(next: Language): Promise<void> {
  if (next === 'vi') await loadTexts();
  language = next;
  cache.clear();
  try { localStorage.setItem(STORAGE_KEY, next); } catch { /* private mode: only for now */ }
  document.documentElement.lang = next;
  busy = true;
  try {
    translateTree(document.body);
    for (const root of shadowRoots) {
      if (root.host.isConnected) translateTree(root);
      else shadowRoots.delete(root);
    }
  } finally { busy = false; }
  observe();
  for (const listener of listeners) listener(next);
}

/** The language this browser chose last time (English when none). */
export function savedLanguage(): Language {
  try { return localStorage.getItem(STORAGE_KEY) === 'vi' ? 'vi' : 'en'; } catch { return 'en'; }
}

/** Starts with the saved language; a failed load of the Vietnamese texts leaves English. */
export async function initLanguage(): Promise<void> {
  const saved = savedLanguage();
  if (saved === 'en') return;
  try { await setLanguage(saved); } catch (error) { console.warn('Vietnamese texts could not be loaded', error); }
}
