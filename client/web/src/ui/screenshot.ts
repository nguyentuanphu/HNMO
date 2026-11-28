import { canUseGameShortcut, matchesModifiers, panelKey } from '../game/keyboardShortcuts.js';
// Screenshots, as the desktop's Print Screen: the key saves a JPG of the game named Screen(MM_DD-HH_MM)-NNNN.jpg (the
// month, day, hour and minute of the player's clock and a rolling counter, as the desktop names them) and tells it in
// the chat with the game and the character, like the desktop's "screenshot saved" line with the server's title.
// HNMO extra: a share panel (the touch "more" menu's camera button, the community window's Screenshot button) with a
// preview, an optional caption (the game's name from gameName(), the character and the map) and a Share button (the
// Web Share API, mostly on phones) or Download when the browser can't share files. The picture is the 3D view: the
// interface is HTML over it and isn't part of the canvas.

import { gameName } from '../gameName.js';
import { tr } from './missionsWindow.js';

export interface ScreenshotOptions {
  /** Draws a frame now and returns a canvas with it (read in the same task, so the WebGL picture is still there). */
  capture: () => HTMLCanvasElement;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The character's name and the map, for the caption and the chat line. */
  where: () => { character: string; map: string };
}

const COUNTER_KEY = 'hnmo.screenshot.counter';
const CAPTION_KEY = 'hnmo.screenshot.caption';

const CSS = `
#hnmoShare { position: fixed; inset: 0; z-index: 120; display: none; align-items: center; justify-content: center; background: rgba(0,0,0,.55); }
#hnmoShare.open { display: flex; }
#hnmoShare .box { max-width: min(92vw, 720px); max-height: 92vh; display: flex; flex-direction: column; gap: 10px; padding: 14px;
  background: rgba(20,16,10,.96); border: 1px solid rgba(255,220,150,.45); border-radius: 10px; color: #f3e2bd; font: 14px system-ui, sans-serif; }
#hnmoShare h2 { margin: 0; font-size: 16px; color: #ffd98a; }
#hnmoShare img { max-width: 100%; max-height: 60vh; object-fit: contain; border: 1px solid rgba(255,220,150,.25); }
#hnmoShare .row { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
#hnmoShare button { min-height: 40px; padding: 0 14px; border-radius: 8px; border: 1px solid rgba(255,220,150,.45); background: rgba(60,45,25,.9);
  color: #f3e2bd; font: 600 14px system-ui, sans-serif; cursor: pointer; }
#hnmoShare button.main { background: rgba(140,100,40,.95); }
#hnmoShare label { display: flex; gap: 6px; align-items: center; }
`;

function two(n: number): string { return String(n).padStart(2, '0'); }

/** The desktop's file name: Screen(MM_DD-HH_MM)-NNNN.jpg, with a rolling counter. */
export function screenshotName(date: Date, counter: number): string {
  return `Screen(${two(date.getMonth() + 1)}_${two(date.getDate())}-${two(date.getHours())}_${two(date.getMinutes())})-${String(counter % 10000).padStart(4, '0')}.jpg`;
}

function storage(key: string): string | null {
  try { return localStorage.getItem(key); } catch { return null; }
}

function store(key: string, value: string): void {
  try { localStorage.setItem(key, value); } catch { /* private window */ }
}

export class Screenshots {
  private readonly panel: HTMLElement;
  private readonly image: HTMLImageElement;
  private readonly captionBox: HTMLInputElement;
  private readonly shareButton: HTMLButtonElement;
  private shot: HTMLCanvasElement | null = null;
  private blob: Blob | null = null;
  private url = '';
  private name = '';
  private lastKeyAt = 0;
  /** The name of the last saved or shared picture (for tests). */
  lastName = '';

  constructor(private readonly options: ScreenshotOptions) {
    const style = document.createElement('style');
    style.textContent = CSS;
    document.head.append(style);
    this.panel = document.createElement('div');
    this.panel.id = 'hnmoShare';
    this.panel.setAttribute('role', 'dialog');
    const box = document.createElement('div');
    box.className = 'box';
    const title = document.createElement('h2');
    this.image = document.createElement('img');
    this.image.alt = '';
    const captionLabel = document.createElement('label');
    this.captionBox = document.createElement('input');
    this.captionBox.type = 'checkbox';
    this.captionBox.className = 'caption';
    this.captionBox.checked = storage(CAPTION_KEY) !== '0';
    const captionText = document.createElement('span');
    captionLabel.append(this.captionBox, captionText);
    const row = document.createElement('div');
    row.className = 'row';
    this.shareButton = this.button('share main', () => { void this.share(); });
    const download = this.button('download', () => { void this.download(); });
    const close = this.button('close', () => this.close());
    row.append(this.shareButton, download, close);
    box.append(title, this.image, captionLabel, row);
    this.panel.append(box);
    this.panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    this.panel.addEventListener('keydown', (event) => { if (event.key !== 'Escape') event.stopPropagation(); });
    this.panel.addEventListener('click', (event) => { if (event.target === this.panel) this.close(); });
    this.captionBox.addEventListener('change', () => { store(CAPTION_KEY, this.captionBox.checked ? '1' : '0'); void this.render(); });
    document.body.append(this.panel);
    const texts = () => {
      title.textContent = tr('Screenshot');
      captionText.textContent = tr('Caption with the game\'s name');
      this.shareButton.textContent = tr('Share');
      download.textContent = tr('Download');
      close.textContent = tr('Close');
      this.panel.setAttribute('aria-label', tr('Screenshot'));
    };
    texts();
    this.texts = texts;

    // Print Screen: the desktop's key. Browsers on some systems only send its key-up.
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'PrintScreen' && event.code !== 'PrintScreen') return;
      if (!canUseGameShortcut(event) || !matchesModifiers(event)) return;
      const now = performance.now();
      if (now - this.lastKeyAt < 400) return;
      this.lastKeyAt = now;
      event.preventDefault();
      void this.saveNow();
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('keyup', onKey);
    window.addEventListener('keydown', (event) => { if (panelKey(event, 'Escape') && this.isOpen) this.close(); });
    window.addEventListener('hnmo-share-open', () => this.open());
  }

  /** Refreshes the texts after a language change. */
  readonly texts: () => void;

  get isOpen(): boolean { return this.panel.classList.contains('open'); }

  /** Takes a picture and saves it at once (Print Screen). */
  async saveNow(): Promise<string> {
    this.take();
    await this.render();
    return this.download();
  }

  /** Takes a picture and opens the share panel with it. */
  open(): void {
    this.take();
    this.shareButton.hidden = !this.canShare();
    this.panel.classList.add('open');
    void this.render();
  }

  close(): void {
    this.panel.classList.remove('open');
    // The game's hotkeys work again at once (the panel keeps its keys to itself while a button in it has the focus).
    if (document.activeElement instanceof HTMLElement && this.panel.contains(document.activeElement)) document.activeElement.blur();
  }

  private button(className: string, run: () => void): HTMLButtonElement {
    const element = document.createElement('button');
    element.type = 'button';
    element.className = className;
    element.addEventListener('click', (event) => { event.stopPropagation(); run(); });
    return element;
  }

  private take(): void {
    this.shot = this.options.capture();
    const counter = (Number(storage(COUNTER_KEY)) || 0) % 10000;
    store(COUNTER_KEY, String((counter + 1) % 10000));
    this.name = screenshotName(new Date(), counter);
  }

  private caption(): string {
    const { character, map } = this.options.where();
    return [gameName(), character, map].filter((p) => p).join(' · ');
  }

  /** The picture with or without the caption, as a JPG. */
  private async render(): Promise<Blob | null> {
    const shot = this.shot;
    if (!shot) return null;
    const canvas = document.createElement('canvas');
    canvas.width = shot.width;
    canvas.height = shot.height;
    const context = canvas.getContext('2d');
    if (!context) return null;
    context.drawImage(shot, 0, 0);
    if (this.captionBox.checked) {
      const text = this.caption();
      const size = Math.max(14, Math.round(canvas.height / 32));
      context.font = `600 ${size}px system-ui, sans-serif`;
      const width = context.measureText(text).width;
      const pad = Math.round(size / 2);
      context.fillStyle = 'rgba(0,0,0,0.55)';
      context.fillRect(canvas.width - width - pad * 3, canvas.height - size - pad * 3, width + pad * 2, size + pad * 2);
      context.fillStyle = '#ffd98a';
      context.textBaseline = 'top';
      context.fillText(text, canvas.width - width - pad * 2, canvas.height - size - pad * 2);
    }
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.92));
    this.blob = blob;
    if (this.url) URL.revokeObjectURL(this.url);
    this.url = blob ? URL.createObjectURL(blob) : '';
    this.image.src = this.url;
    return blob;
  }

  private canShare(): boolean {
    if (typeof navigator.share !== 'function' || typeof navigator.canShare !== 'function') return false;
    try {
      return navigator.canShare({ files: [new File([new Uint8Array(1)], 'x.jpg', { type: 'image/jpeg' })] });
    } catch {
      return false;
    }
  }

  private async download(): Promise<string> {
    const blob = this.blob ?? await this.render();
    if (!blob) return '';
    const anchor = document.createElement('a');
    anchor.href = this.url;
    anchor.download = this.name;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    this.lastName = this.name;
    const { character } = this.options.where();
    this.options.message(`${tr('Screenshot saved: %s', this.name)} [${gameName()}${character ? ` / ${character}` : ''}]`);
    return this.name;
  }

  private async share(): Promise<void> {
    const blob = this.blob ?? await this.render();
    if (!blob) return;
    const file = new File([blob], this.name, { type: 'image/jpeg' });
    try {
      await navigator.share({ files: [file], title: gameName(), text: this.captionBox.checked ? this.caption() : gameName() });
      this.lastName = this.name;
    } catch (error) {
      // Cancelled by the player, or the browser refused: the download still works.
      if (!(error instanceof DOMException && error.name === 'AbortError')) await this.download();
    }
  }
}
