// Shows the game's interface documents (Data/Interface/RmlUi: .rml and .rcss, the same files
// the desktop client draws its windows from) in the browser. The RCSS is turned into CSS: its
// sprite sheets become images cut from the original art, theme tokens are filled in, and a
// window's data model drives the RML data bindings (data-for, data-if, {{…}}, data-attr-*,
// data-class-*, data-style-*, data-event-*).

import { decodeTGA } from '../formats.js';

const ROOT = '/data/Interface/RmlUi';
const THEMES: Record<string, string | null> = { classic: 'legacy', legacy: null, modern: null }; // a theme and the one it inherits from
export const REFERENCE_HEIGHT = 480; // the interface is laid out for a 640x480 screen and scaled up

// A window's data model: the values the RML binds to and its event handlers. The view calls
// __update after an event so the bindings show the new values.
export type RmlModel = Record<string, unknown> & { __update?: () => void };

// An opened interface document.
export interface RmlDocument {
  host: HTMLDivElement;
  root: HTMLDivElement;
  update(): void;
  $<E extends HTMLElement = HTMLElement>(selector: string): E | null;
  setScale(value: number): void;
  close(): void;
}

interface FoundFile { url: string; text: string }
// A sprite of a sheet: the image and the rectangle (x, y, w, h) on it.
interface Sprite { src: string | null; rect: number[] }
type Sprites = Map<string, Sprite>;
interface Sheet { url: string; width: number; height: number }
interface PreparedDocument { xml: Document; css: string }
// A bound value's updater; runs with the scope of names the expressions see.
type Scope = Record<string | symbol, unknown>;
type Updater = (scope: Scope) => void;
type Locals = Record<string, unknown>;

let theme = 'legacy';
const textCache = new Map<string, Promise<string | null>>();
const imageCache = new Map<string, Promise<HTMLCanvasElement | null>>();
const spriteCache = new Map<string, Promise<string | null>>();
let tokens: Record<string, string> | null = null;

async function fetchText(url: string): Promise<string | null> {
  if (!textCache.has(url)) {
    textCache.set(url, fetch(url).then((response) => (response.ok ? response.text() : null)));
  }
  return textCache.get(url)!;
}

function themeChain(): string[] {
  const chain: string[] = [];
  for (let t: string | null = theme; t; t = THEMES[t]) chain.push(t);
  return chain;
}


// A document as the desktop client finds it: the theme's fork, then its base theme's, then the shared one.
async function findDocument(name: string): Promise<FoundFile> {
  for (const t of themeChain()) {
    const url = `${ROOT}/themes/${t}/${name}.rml`;
    const text = await fetchText(url);
    if (text) return { url, text };
  }
  const url = `${ROOT}/${name}.rml`;
  const text = await fetchText(url);
  if (!text) throw new Error(`Interface document ${name}.rml not found`);
  return { url, text };
}

async function findStylesheet(href: string, documentUrl: string): Promise<FoundFile | null> {
  const relative = new URL(href, `http://x${documentUrl}`).pathname;
  const fileName = href.split('/').pop();
  const candidates = [relative, ...themeChain().map((t) => `${ROOT}/themes/${t}/${fileName}`), `${ROOT}/${fileName}`];
  for (const url of candidates) {
    const text = await fetchText(url);
    if (text) return { url, text };
  }
  return null;
}

async function loadTokens(): Promise<Record<string, string>> {
  if (tokens) return tokens;
  const values: Record<string, string> = {};
  tokens = values;
  for (const t of themeChain().reverse()) {
    const text = await fetchText(`${ROOT}/themes/${t}/tokens.ini`);
    for (const line of (text ?? '').split(/\r?\n/)) {
      const match = line.match(/^\s*([\w-]+)\s*=\s*(.*?)\s*$/);
      if (match && !line.trim().startsWith(';')) values[match[1]] = match[2];
    }
  }
  return values;
}

// ---------- images ----------

// An image of the game's data as a canvas: .jpg/.tga (served from the .OZJ/.OZT files).
export async function loadImage(url: string): Promise<HTMLCanvasElement | null> {
  if (!imageCache.has(url)) {
    imageCache.set(url, (async () => {
      const response = await fetch(url);
      if (!response.ok) return null;
      const bytes = new Uint8Array(await response.arrayBuffer());
      const canvas = document.createElement('canvas');
      if (bytes[0] === 0xff && bytes[1] === 0xd8) {
        const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/jpeg' }));
        canvas.width = bitmap.width;
        canvas.height = bitmap.height;
        canvas.getContext('2d')!.drawImage(bitmap, 0, 0);
      } else {
        const { width, height, pixels } = decodeTGA(bytes);
        canvas.width = width;
        canvas.height = height;
        canvas.getContext('2d')!.putImageData(new ImageData(new Uint8ClampedArray(pixels.buffer, pixels.byteOffset, pixels.length), width, height), 0, 0);
      }
      return canvas;
    })().catch((error) => { console.warn('Interface image', url, error); return null; }));
  }
  return imageCache.get(url)!;
}

// A whole sprite sheet as an object URL for CSS (one per image; sprites are cut with CSS).
const sheetCache = new Map<string, Promise<Sheet | null>>();
async function sheetOf(url: string): Promise<Sheet | null> {
  if (!sheetCache.has(url)) {
    sheetCache.set(url, (async () => {
      const image = await loadImage(url);
      if (!image) return null;
      const blob = await new Promise<Blob | null>((resolve) => image.toBlob(resolve, 'image/png'));
      return { url: URL.createObjectURL(blob!), width: image.width, height: image.height };
    })());
  }
  return sheetCache.get(url)!;
}

// CSS that stretches the rectangle (x, y, w, h) of a sheet over the element's box.
function spriteBackground(sheet: Sheet, rect: number[] | undefined, repeat = false): string {
  const [x, y, w, h] = rect ?? [0, 0, sheet.width, sheet.height];
  if (repeat && x === 0 && y === 0 && w === sheet.width && h === sheet.height) return `background: url("${sheet.url}") 0 0 repeat;`;
  const sizeX = (sheet.width / w) * 100, sizeY = (sheet.height / h) * 100;
  const posX = sheet.width === w ? 0 : (x / (sheet.width - w)) * 100;
  const posY = sheet.height === h ? 0 : (y / (sheet.height - h)) * 100;
  return `background: url("${sheet.url}") ${posX.toFixed(4)}% ${posY.toFixed(4)}% / ${sizeX.toFixed(4)}% ${sizeY.toFixed(4)}% no-repeat;`;
}

// A piece of an image as an object URL, for border-image and stacked backgrounds.
async function spriteUrl(url: string | null, rect: number[] | null | undefined): Promise<string | null> {
  const key = `${url}#${rect ? rect.join(',') : ''}`;
  if (!spriteCache.has(key)) {
    spriteCache.set(key, (async () => {
      const image = url ? await loadImage(url) : null;
      if (!image) return null;
      const [x, y, w, h] = rect ?? [0, 0, image.width, image.height];
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(w));
      canvas.height = Math.max(1, Math.round(h));
      canvas.getContext('2d')!.drawImage(image, x, y, w, h, 0, 0, w, h);
      const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/png'));
      return URL.createObjectURL(blob!);
    })());
  }
  return spriteCache.get(key)!;
}

// ---------- RCSS to CSS ----------

const px = (value: unknown): number => Number(String(value).replace(/px|dp/, ''));

function parseSpritesheets(rcss: string, sheetUrl: string): { sprites: Sprites; rest: string } {
  const sprites: Sprites = new Map();
  const blocks = rcss.replace(/@spritesheet\s+[\w-]+\s*\{([^}]*)\}/g, (_, body: string) => {
    let src: string | null = null;
    const rects: [string, number[]][] = [];
    for (const part of body.split(';')) {
      const [key, ...rest] = part.split(':');
      if (!key?.trim() || !rest.length) continue;
      const value = rest.join(':').trim();
      if (key.trim() === 'src') src = new URL(value.replace(/["']/g, ''), `http://x${sheetUrl}`).pathname;
      else if (key.trim() !== 'resolution') rects.push([key.trim(), value.split(/\s+/).slice(0, 4).map(px)]);
    }
    for (const [name, rect] of rects) sprites.set(name, { src, rect });
    return '';
  });
  return { sprites, rest: blocks };
}

// The image a decorator draws, stretched over the element (RmlUi's image decorator).
async function imageCss(this: { sprites: Sprites; sheetUrl: string }, arg: string): Promise<string> {
  const name = arg.trim().split(/\s+/)[0].replace(/["']/g, '');
  const sprite = this.sprites.get(name);
  const sheet = await sheetOf(sprite ? sprite.src ?? '' : new URL(name, `http://x${this.sheetUrl}`).pathname);
  if (!sheet) return 'background: none;';
  return spriteBackground(sheet, sprite?.rect, arg.includes(' tile'));
}

// RmlUi's ninepatch(outer, inner): the inner sprite marks the stretched middle of the outer one.
async function ninepatchCss(args: string, sprites: Sprites): Promise<string> {
  const [outerName, innerName] = args.split(',').map((s) => s.trim());
  const outer = sprites.get(outerName);
  const inner = sprites.get(innerName);
  if (!outer) return '';
  const url = await spriteUrl(outer.src, outer.rect);
  if (!inner) return `background: url("${url}") 0 0 / 100% 100% no-repeat;`;
  const [ox, oy, ow, oh] = outer.rect;
  const [ix, iy, iw, ih] = inner.rect;
  const top = iy - oy, left = ix - ox, right = ox + ow - (ix + iw), bottom = oy + oh - (iy + ih);
  return `border-style: solid; border-color: transparent; border-image: url("${url}") ${top} ${right} ${bottom} ${left} fill stretch; border-width: ${top}px ${right}px ${bottom}px ${left}px;`;
}

// tiled-box / tiled-horizontal(left, centre, right): drawn as stacked backgrounds.
async function tiledCss(kind: string, args: string, sprites: Sprites): Promise<string> {
  const names = args.split(',').map((s) => s.trim().split(/\s+/)[0]);
  const urls = await Promise.all(names.map((n) => { const sprite = sprites.get(n); return sprite ? spriteUrl(sprite.src, sprite.rect) : null; }));
  const sizes = names.map((n) => sprites.get(n)?.rect ?? [0, 0, 0, 0]);
  if (kind === 'tiled-horizontal' && urls.length >= 3) {
    return `background: url("${urls[0]}") left top / ${sizes[0][2]}px 100% no-repeat, url("${urls[2]}") right top / ${sizes[2][2]}px 100% no-repeat, url("${urls[1]}") left top / auto 100% repeat-x;`;
  }
  if (kind === 'tiled-box' && urls.length >= 9) {
    const [tl, t, tr, l, c, r, bl, b, br] = urls;
    const [, , tlw, tlh] = sizes[0];
    const [, , brw, brh] = sizes[8];
    return `background: url("${tl}") left top no-repeat, url("${tr}") right top no-repeat, url("${bl}") left bottom no-repeat, url("${br}") right bottom no-repeat,`
      + ` url("${t}") ${tlw}px top / calc(100% - ${tlw + brw}px) ${tlh}px no-repeat, url("${b}") ${tlw}px bottom / calc(100% - ${tlw + brw}px) ${brh}px no-repeat,`
      + ` url("${l}") left ${tlh}px / ${tlw}px calc(100% - ${tlh + brh}px) no-repeat, url("${r}") right ${tlh}px / ${brw}px calc(100% - ${tlh + brh}px) no-repeat,`
      + ` url("${c}") ${tlw}px ${tlh}px / calc(100% - ${tlw + brw}px) calc(100% - ${tlh + brh}px) no-repeat;`;
  }
  return urls[0] ? `background: url("${urls[0]}") 0 0 / 100% 100% no-repeat;` : '';
}

async function replaceAsync(text: string, pattern: RegExp, replacer: (match: string, ...groups: string[]) => Promise<string>): Promise<string> {
  const jobs: Promise<string>[] = [];
  text.replace(pattern, (match: string, ...groups: string[]) => { jobs.push(replacer(match, ...groups)); return ''; });
  const results = await Promise.all(jobs);
  let i = 0;
  return text.replace(pattern, () => results[i++]);
}

function outlineShadow(width: string, color: string): string {
  const w = px(width) || 1;
  return `text-shadow: ${[[-w, 0], [w, 0], [0, -w], [0, w], [-w, -w], [w, w], [-w, w], [w, -w]].map(([x, y]) => `${x}px ${y}px 0 ${color}`).join(', ')};`;
}

// Every sprite of the stylesheets loaded so far, for decorators a data model sets at run time
// (data-style-decorator="image(name)", e.g. the HUD's skill icons).
const knownSprites: Sprites = new Map();
const decoratorCss = new Map<string, Promise<string>>();

/** The CSS background for an RCSS decorator value ("image(sprite)", "none"). */
export function decoratorBackground(value: string): Promise<string> {
  if (!decoratorCss.has(value)) {
    const match = /^\s*(image|additive-image)\((.*)\)\s*$/.exec(value);
    decoratorCss.set(value, match ? imageCss.call({ sprites: knownSprites, sheetUrl: '/' }, match[2]) : Promise.resolve('background: none;'));
  }
  return decoratorCss.get(value)!;
}

function applyDecorator(element: HTMLElement, value: unknown): void {
  const text = value === undefined || value === null ? 'none' : String(value);
  if (element.dataset.decorator === text) return;
  element.dataset.decorator = text;
  void decoratorBackground(text).then((css) => {
    if (element.dataset.decorator !== text) return;
    element.style.background = css.replace(/^background:\s*/, '').replace(/;\s*$/, '');
  });
}

export async function rcssToCss(rcss: string, sheetUrl: string, sprites: Sprites = new Map()): Promise<string> {
  const tokenValues = await loadTokens();
  let text = rcss.replace(/\/\*[\s\S]*?\*\//g, '');
  const parsed = parseSpritesheets(text, sheetUrl);
  for (const [name, sprite] of parsed.sprites) { sprites.set(name, sprite); knownSprites.set(name, sprite); }
  text = parsed.rest
    .replace(/token\(([\w-]+)\)/g, (_, name: string) => tokenValues[name] ?? 'inherit')
    .replace(/(\d)dp\b/g, '$1px')
    .replace(/rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+(?:\.\d+)?)\s*\)/g, (all: string, r: string, g: string, b: string, a: string) => (a.includes('.') ? all : `rgba(${r}, ${g}, ${b}, ${(Number(a) / 255).toFixed(3)})`))
    // RCSS colours with alpha 0-255 in four hex pairs are the same as CSS's #rrggbbaa.
    .replace(/font-effect:\s*outline\(\s*([\d.]+px)\s+([^)]+)\)\s*;/g, (_, w: string, c: string) => outlineShadow(w, c.trim()))
    .replace(/font-effect:\s*shadow\(\s*([\d.-]+px)\s+([\d.-]+px)\s+([^)]+)\)\s*;/g, (_, x: string, y: string, c: string) => `text-shadow: ${x} ${y} 0 ${c.trim()};`)
    .replace(/font-effect:[^;]*;/g, '')
    .replace(/image-color:\s*([^;]+);/g, '')
    .replace(/tab-index:[^;]*;/g, '')
    .replace(/drag:[^;]*;/g, '')
    .replace(/nav[\w-]*:[^;]*;/g, '');
  text = await replaceAsync(text, /decorator:\s*([\w-]+)\(([^;]*)\)\s*;/g, async (_, kind, args) => {
    if (kind === 'image' || kind === 'additive-image') {
      const css = await imageCss.call({ sprites, sheetUrl }, args);
      return kind === 'additive-image' ? `${css} mix-blend-mode: screen;` : css;
    }
    if (kind === 'ninepatch') return ninepatchCss(args, sprites);
    return tiledCss(kind, args, sprites);
  });
  return text.replace(/decorator:\s*none\s*;/g, 'background-image: none;');
}

// ---------- data binding ----------

type Expression = (scope: Scope) => unknown;
const expressionCache = new Map<string, Expression>();

// RmlUi data expressions are close to JavaScript: names, literals, + - * / == != < > && || ! ?:,
// and "| transform" pipes, which are left out here (a single |, not ||).
function evaluate(expression: string, scope: Scope): unknown {
  const source = expression.replace(/(?<!\|)\|(?!\|)\s*[\w]+(\([^)]*\))?/g, '').trim();
  if (!expressionCache.has(source)) {
    try {
      expressionCache.set(source, new Function('scope', `with (scope) { return (${source}); }`) as Expression);
    } catch {
      expressionCache.set(source, () => undefined);
    }
  }
  try {
    return expressionCache.get(source)!(scope);
  } catch {
    return undefined;
  }
}

function scopeProxy(model: RmlModel, locals: Locals): Scope {
  return new Proxy<Scope>({}, {
    has: () => true,
    get: (_, key) => {
      if (key === Symbol.unscopables) return undefined;
      if (typeof key === 'symbol') return undefined;
      if (key in locals) return locals[key];
      if (key in model) return model[key];
      return (globalThis as unknown as Record<string, unknown>)[key] ?? '';
    },
  });
}

// Compiles an element tree into updaters; data-for repeats its element for each list entry.
function bindTree(node: Node, model: RmlModel, locals: Locals, updaters: Updater[]): void {
  if (node.nodeType === Node.TEXT_NODE) {
    const element = node;
    const template = element.textContent ?? '';
    if (template.includes('{{')) {
      updaters.push((scope) => {
        const value = template.replace(/\{\{(.*?)\}\}/g, (_, expr) => { const v = evaluate(expr, scope); return v === undefined || v === null ? '' : String(v); });
        if (element.textContent !== value) element.textContent = value;
      });
    }
    return;
  }
  if (!(node instanceof HTMLElement)) return;
  const element = node;

  const forAttr = element.getAttribute('data-for');
  if (forAttr) {
    const [itemName, listExpr] = forAttr.split(':').map((s) => s.trim());
    const [name, indexName = 'it_index'] = itemName.split(',').map((s) => s.trim());
    const template = element.cloneNode(true) as HTMLElement;
    template.removeAttribute('data-for');
    const anchor = document.createComment('data-for');
    element.replaceWith(anchor);
    const rendered: { node: HTMLElement; entryUpdaters: Updater[]; entryLocals: Locals; index: number }[] = [];
    updaters.push((scope) => {
      const value = evaluate(listExpr, scope);
      const list: unknown[] = Array.isArray(value) ? value : [];
      // Each entry keeps its element; new entries are bound, removed ones taken out.
      while (rendered.length > list.length) rendered.pop()!.node.remove();
      while (rendered.length < list.length) {
        const index = rendered.length;
        const node = template.cloneNode(true) as HTMLElement;
        const entryUpdaters: Updater[] = [];
        const entryLocals: Locals = { ...locals };
        bindTree(node, model, entryLocals, entryUpdaters);
        (rendered.length ? rendered[rendered.length - 1].node : anchor).after(node);
        rendered.push({ node, entryUpdaters, entryLocals, index });
      }
      for (const entry of rendered) {
        entry.entryLocals[name] = list[entry.index];
        entry.entryLocals[indexName] = entry.index;
        const entryScope = scopeProxy(model, entry.entryLocals);
        for (const update of entry.entryUpdaters) update(entryScope);
      }
    });
    return;
  }

  const ifExpr = element.getAttribute('data-if');
  if (ifExpr) updaters.push((scope) => { element.style.display = evaluate(ifExpr, scope) ? '' : 'none'; });
  const visibleExpr = element.getAttribute('data-visible');
  if (visibleExpr) updaters.push((scope) => { element.style.visibility = evaluate(visibleExpr, scope) ? '' : 'hidden'; });

  for (const attribute of [...element.attributes]) {
    const { name, value } = attribute;
    if (name.startsWith('data-attr-')) {
      const target = name.slice('data-attr-'.length);
      updaters.push((scope) => {
        const v = evaluate(value, scope);
        if (v === undefined || v === null || v === false) element.removeAttribute(target);
        else if (element.getAttribute(target) !== String(v)) element.setAttribute(target, String(v));
      });
    } else if (name.startsWith('data-class-')) {
      const className = name.slice('data-class-'.length);
      updaters.push((scope) => element.classList.toggle(className, Boolean(evaluate(value, scope))));
    } else if (name.startsWith('data-style-')) {
      const property = name.slice('data-style-'.length);
      if (property === 'decorator') { updaters.push((scope) => applyDecorator(element, evaluate(value, scope))); continue; }
      updaters.push((scope) => {
        const v = evaluate(value, scope);
        element.style.setProperty(property, v === undefined || v === null ? '' : String(v));
      });
    } else if (name.startsWith('data-event-')) {
      const eventName = name.slice('data-event-'.length);
      element.addEventListener(eventName, (event) => {
        const handler = model[value.trim()];
        if (typeof handler === 'function') handler.call(model, event, locals);
        else evaluate(value, scopeProxy(model, { ...locals, event }));
        model.__update?.();
      });
    } else if (name === 'data-value') {
      // Bound on form fields (input, textarea, select), which all have a value.
      const field = element as HTMLInputElement;
      element.addEventListener('input', () => { model[value] = field.value; model.__update?.(); });
      updaters.push((scope) => { const v = evaluate(value, scope); if (document.activeElement !== element) field.value = v === undefined || v === null ? '' : String(v); });
    }
  }
  for (const child of [...element.childNodes]) bindTree(child, model, locals, updaters);
}

// ---------- documents ----------

const BASE_CSS = `
:host { all: initial; position: absolute; left: 0; top: 0; width: 0; height: 0; }
.rml-body { position: absolute; left: 0; top: 0; color: #fff; font-family: Tahoma, Arial, sans-serif; font-size: 12px; line-height: 1.2; }
:where(.rml-body) :where(div, p, h1, h2, h3) { display: block; margin: 0; padding: 0; box-sizing: content-box; }
:where(.rml-body) :where(img) { display: block; }
:where(.rml-body) :where(img:not([src])) { visibility: hidden; }
:where(.rml-body) :where(.counter-scaled) { transform: scale(calc(1 / var(--root-scale, 1))); }
:where(.rml-body) :where(button, input) { font: inherit; }
.rml-body .hidden { display: none !important; }
`;

// Opens an interface document over the game: returns { host, root, update(), close() }.
// model: the window's data (names the RML binds to) and its event handlers.
const compiled = new Map<string, Promise<PreparedDocument>>();

// Loads a document and turns its stylesheets into CSS once; later opens reuse it.
export function prepareDocument(name: string): Promise<PreparedDocument> {
  if (!compiled.has(name)) {
    compiled.set(name, (async () => {
      const { url, text } = await findDocument(name);
      const xml = new DOMParser().parseFromString(text.replace(/&nbsp;/g, '&#160;'), 'text/html');
      const links = [...xml.querySelectorAll('link[type="text/rcss"], link[href$=".rcss"]')].map((link) => link.getAttribute('href') ?? '');
      const sprites: Sprites = new Map();
      const sheets = await Promise.all(links.map((href) => findStylesheet(href, url)));
      const styles: string[] = [];
      // Sprite sheets of earlier stylesheets are known to later ones, so convert in order.
      for (const sheet of sheets) if (sheet) styles.push(await rcssToCss(sheet.text, sheet.url, sprites));
      for (const style of xml.querySelectorAll('style')) styles.push(await rcssToCss(style.textContent ?? '', url, sprites));
      return { xml, css: BASE_CSS + styles.join('\n').replace(/(^|[\s,}])body(?=[\s,{.#:[])/g, '$1.rml-body') };
    })());
  }
  return compiled.get(name)!;
}

export async function openDocument(name: string, model: RmlModel, { parent = document.body as HTMLElement, scale = 1 } = {}): Promise<RmlDocument> {
  const { xml, css } = await prepareDocument(name);

  const host = document.createElement('div');
  host.className = `rml-document rml-${name}`;
  const shadow = host.attachShadow({ mode: 'open' });
  const style = document.createElement('style');
  style.textContent = css;
  shadow.append(style);
  const root = document.createElement('div');
  root.className = `rml-body ${xml.body.className}`;
  root.style.setProperty('--root-scale', String(scale));
  for (const child of [...xml.body.childNodes]) root.append(document.importNode(child, true));
  // <body>'s own bindings (e.g. a tooltip's position) go on the root that stands for it.
  for (const attribute of [...xml.body.attributes]) {
    if (attribute.name.startsWith('data-') && attribute.name !== 'data-model') root.setAttribute(attribute.name, attribute.value);
  }
  shadow.append(root);

  const updaters: Updater[] = [];
  bindTree(root, model, {}, updaters);
  const update = () => {
    const scope = scopeProxy(model, {});
    for (const updater of updaters) updater(scope);
  };
  model.__update = update;
  update();
  parent.append(host);
  return {
    host, root, update,
    $: <E extends HTMLElement = HTMLElement>(selector: string) => shadow.querySelector<E>(selector),
    setScale(value: number) { root.style.setProperty('--root-scale', String(value)); },
    close() { host.remove(); },
  };
}

export function setTheme(name: string): void {
  theme = THEMES[name] !== undefined ? name : 'legacy';
  tokens = null;
}
