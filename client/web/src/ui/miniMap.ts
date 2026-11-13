// The full-screen mini map (CMiniMap, mini_map.rml; Tab or the window menu's Map row): the world's
// mini_map picture and its NPC and gate markers turned 45 degrees around the window centre with the
// hero's cell there, the hero arrow, the frame, the close button and the hovered marker's name.
// The map is drawn in physical (window) pixels like the desktop's, with the frame under the
// screen's W/640 x H/480 stretch, and only outside the bottom HUD's band (the clips).

import { loadImage, openDocument, prepareDocument, type RmlDocument, type RmlModel } from './rml.js';
import { loadMiniMapData, type MiniMapMarker } from '../game/miniMapData.js';

// ---------- MiniMapLayout (the pure geometry of the desktop's mini map) ----------

/** A physical window pixel, y down. */
export interface Point { x: number; y: number }
/** CSS matrix(a, b, c, d, e, f) for an element at (0, 0) with transform-origin 0 0. */
export interface CssMatrix { a: number; b: number; c: number; d: number; e: number; f: number }
/** Where a texture's top-left, top-right and bottom-left corners land. */
export interface Quad { topLeft: Point; topRight: Point; bottomLeft: Point }
/** The window size and the screen overlay's stretch and offset. */
export interface Screen { width: number; height: number; scaleX: number; scaleY: number; offsetX: number; offsetY: number }

const REFERENCE_WIDTH = 640;
const REFERENCE_HEIGHT_PX = 480;
/** The map's side in map pixels (the only zoom level the desktop uses) and its turn. */
export const MAP_LENGTH = 800;
export const MAP_ROTATION = 45;
// RenderPointRotate() shifted every marker 25 physical pixels right after turning it.
const MARKER_SHIFT = 25;

// AngleMatrix() with only a z angle: a counter-clockwise turn in y-up space.
function rotate(x: number, y: number, degrees: number): Point {
  const radians = degrees * (Math.PI * 2 / 360);
  const c = Math.cos(radians), s = Math.sin(radians);
  return { x: c * x - s * y, y: s * x + c * y };
}

// A y-up point relative to the window centre, as a y-down window pixel.
function fromCentre(screen: Screen, p: Point): Point {
  return { x: p.x + screen.width / 2, y: screen.height - (p.y + screen.height / 2) };
}

export function elementToQuad(quad: Quad, elementWidth: number, elementHeight: number): CssMatrix {
  return {
    a: (quad.topRight.x - quad.topLeft.x) / elementWidth,
    b: (quad.topRight.y - quad.topLeft.y) / elementWidth,
    c: (quad.bottomLeft.x - quad.topLeft.x) / elementHeight,
    d: (quad.bottomLeft.y - quad.topLeft.y) / elementHeight,
    e: quad.topLeft.x,
    f: quad.topLeft.y,
  };
}

/** RenderBitRotate(): the map picture with the hero's map position at the window centre. */
export function mapQuad(screen: Screen, heroX: number, heroY: number, length: number, rotation: number): Quad {
  const x = screen.scaleX * (length - heroX);
  const width = screen.scaleX * length;
  const height = screen.scaleY * length;
  const y = height - screen.scaleY * (length - heroY);
  const cx = width / 2 - (width - x);
  const cy = height / 2 - (height - y);
  const ax = -width * 0.5 + cx, bx = width * 0.5 + cx;
  const ay = -height * 0.5 + cy, by = height * 0.5 + cy;
  return {
    topLeft: fromCentre(screen, rotate(ax, by, rotation)),
    bottomLeft: fromCentre(screen, rotate(ax, ay, rotation)),
    topRight: fromCentre(screen, rotate(bx, by, rotation)),
  };
}

/** RenderPointRotate(): a marker's quad and the name hint's box (x, y, width, height in reference units). */
export function markerQuad(screen: Screen, heroX: number, heroY: number, pointX: number, pointY: number, size: number,
  pointRotation: number, length: number, rotation: number, portal: boolean): { quad: Quad; hitBox: [number, number, number, number] } {
  const ix = screen.scaleX * pointX;
  const width = screen.scaleX * length;
  const height = screen.scaleY * length;
  const x = screen.scaleX * (length - heroX);
  const y = height - screen.scaleY * (length - heroY);
  const iy = height - screen.scaleY * pointY;
  const centre = rotate((ix - width * 0.5) + (width / 2 - (width - x)), (iy - height * 0.5) + (height / 2 - (height - y)), rotation);
  const half = size * 0.5;
  const corner = (px: number, py: number): Point => {
    const turned = rotate(px, py, pointRotation);
    return { x: turned.x + centre.x + MARKER_SHIFT, y: turned.y + centre.y };
  };
  const first = corner(-half, half);
  const quad: Quad = {
    topLeft: fromCentre(screen, first),
    bottomLeft: fromCentre(screen, corner(-half, -half)),
    topRight: fromCentre(screen, corner(half, half)),
  };
  // SetBtnPos() from the quad's first corner, back in reference units: gates their full size around
  // it, NPCs half their size from it.
  const dx = (first.x + screen.width / 2 - screen.offsetX) / screen.scaleX;
  const dy = (first.y + screen.height / 2 - screen.offsetY) / screen.scaleY;
  const hitBox: [number, number, number, number] = portal
    ? [dx - half, (REFERENCE_HEIGHT_PX - dy) - half, size, size]
    : [dx, REFERENCE_HEIGHT_PX - dy, half, half];
  return { quad, hitBox };
}

/** RenderBitmapRotate(): a width x height (reference units) quad centred on (x, y), turned. */
export function rotatedQuad(screen: Screen, x: number, y: number, width: number, height: number, rotation: number): Quad {
  const cx = x * screen.scaleX + screen.offsetX;
  const cyDown = y * screen.scaleY + screen.offsetY;
  const w = width * screen.scaleX, h = height * screen.scaleY;
  const corner = (px: number, py: number): Point => {
    const turned = rotate(px, py, rotation);
    return { x: cx + turned.x, y: cyDown - turned.y };
  };
  return { topLeft: corner(-w * 0.5, h * 0.5), bottomLeft: corner(-w * 0.5, -h * 0.5), topRight: corner(w * 0.5, h * 0.5) };
}

function matrixText(m: CssMatrix): string {
  return `matrix(${m.a.toFixed(5)}, ${m.b.toFixed(5)}, ${m.c.toFixed(5)}, ${m.d.toFixed(5)}, ${m.e.toFixed(3)}, ${m.f.toFixed(3)})`;
}

// ---------- the window ----------

// Marker sizes in physical px (the desktop never scales them).
const NPC_SIZE = 15;
const PORTAL_SIZE = 30;
// The side border tiles: 35 x 6 reference units, 20 down each side, turned a quarter; drawn by an
// element of the tile's texel rectangle (.edge-side in mini_map.rcss).
const TILE_WIDTH = 35;
const TILE_HEIGHT = 6;
const SIDE_TILES = 20;
const SIDE_ELEMENT_WIDTH = 41.7;
const SIDE_ELEMENT_HEIGHT = 8;
// The hint's text: the game font's size in reference units.
const FONT_SIZE = 12;
const FONT = `${FONT_SIZE}px Tahoma, Arial, sans-serif`;

interface Clip { left: number; top: number; width: number; height: number; world_left: number; world_top: number }
interface MarkerEntry { portal: boolean; size: number; transform: string }
interface MiniMapModel extends RmlModel {
  text_px: number;
  close_hint: string;
  clips: Clip[];
  map_source: string;
  map_transform: string;
  markers: MarkerEntry[];
  side_lines: string[];
  hint_visible: boolean;
  hint_text: string;
  hint_left: number; hint_top: number; hint_width: number; hint_height: number;
  minimap_close: () => void;
}

export interface MiniMapHero { cell: { x: number; y: number }; angle: number }
export interface MiniMapOptions {
  hero: () => MiniMapHero | null;
  mapNumber: () => number | null;
  parent?: HTMLElement;
  /** ClosingProcess(): the desktop tells the server it closed an NPC window (CloseNpcRequest). */
  closed?: () => void;
}

// The RCSS flips (flip-vertical, flip-horizontal, rotate-180 on the image decorator) that the
// interface view leaves out.
const FLIPS_CSS = `
.edge-top { transform: scaleY(-1); }
.corner-tr { transform: scaleX(-1); }
.corner-bl { transform: scaleY(-1); }
.corner-br { transform: rotate(180deg); }
`;

const pictureCache = new Map<string, Promise<string | null>>();
// The world's mini_map picture as an object URL for the .map <img>.
function pictureOf(url: string): Promise<string | null> {
  if (!pictureCache.has(url)) {
    pictureCache.set(url, (async () => {
      const canvas = await loadImage(url);
      if (!canvas) return null;
      const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/png'));
      return blob ? URL.createObjectURL(blob) : null;
    })());
  }
  return pictureCache.get(url)!;
}

let measureContext: CanvasRenderingContext2D | null = null;
function measureText(text: string): { cx: number; cy: number } {
  measureContext ??= document.createElement('canvas').getContext('2d');
  if (!measureContext) return { cx: text.length * 6, cy: FONT_SIZE + 2 };
  measureContext.font = FONT;
  const metrics = measureContext.measureText(text);
  const cy = Math.round((metrics.fontBoundingBoxAscent || FONT_SIZE) + (metrics.fontBoundingBoxDescent || 2));
  return { cx: Math.round(metrics.width), cy };
}

export class MiniMap {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private markers: MiniMapMarker[] = [];
  private hitBoxes: { name: string; box: [number, number, number, number] }[] = [];
  private pointer: Point | null = null;
  private lastKey = '';
  private loadedMap: number | null = null;
  private switching = false;
  private prefetchedMap: number | null = null;
  private readonly model: MiniMapModel;

  constructor(private readonly opts: MiniMapOptions) {
    this.model = {
      text_px: FONT_SIZE, close_hint: 'Close', clips: [], map_source: '', map_transform: '', markers: [], side_lines: [],
      hint_visible: false, hint_text: '', hint_left: 0, hint_top: 0, hint_width: 0, hint_height: 0,
      minimap_close: () => this.close(),
    };
    window.addEventListener('resize', () => { this.lastKey = ''; this.update(); });
    // UpdateKeyEvent(): Escape closes it (Tab, which also does, is the game's hot key).
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.doc) { event.preventDefault(); this.close(); }
    });
    window.addEventListener('mousemove', (event) => {
      this.pointer = { x: event.clientX, y: event.clientY };
      if (this.doc) this.update();
    });
  }

  get isOpen(): boolean { return this.doc !== null; }

  async toggle(): Promise<void> {
    if (this.doc) this.close(); else await this.open();
  }

  /** Opens the mini map of the current map; a world without a mini_map picture has none. */
  async open(): Promise<void> {
    if (this.doc) return;
    if (this.opening) return this.opening;
    this.opening = this.load().finally(() => { this.opening = null; });
    return this.opening;
  }

  private async load(): Promise<void> {
    const mapNumber = this.opts.mapNumber();
    if (mapNumber === null || !this.opts.hero()) return;
    const data = await loadMiniMapData(mapNumber);
    const picture = data ? await pictureOf(data.pictureUrl) : null;
    if (!data || !picture || this.opts.mapNumber() !== mapNumber) return;
    this.markers = data.markers;
    this.model.map_source = picture;
    this.loadedMap = mapNumber;
    const doc = await openDocument('mini_map', this.model, { parent: this.opts.parent ?? document.body });
    // Over the location bar, the logs and the buff strip, under the bottom HUD.
    doc.host.style.zIndex = '19';
    const style = document.createElement('style');
    style.textContent = FLIPS_CSS;
    doc.host.shadowRoot?.append(style);
    this.doc = doc;
    this.lastKey = '';
    this.update();
  }

  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    this.model.hint_visible = false;
    this.opts.closed?.();
  }

  private screen(): Screen {
    const width = window.innerWidth, height = window.innerHeight;
    return { width, height, scaleX: width / REFERENCE_WIDTH, scaleY: height / REFERENCE_HEIGHT_PX, offsetX: 0, offsetY: 0 };
  }

  /** Every frame: follows the hero (the map moves under the fixed hero arrow) and the map changes; while closed, loads the current map's mini map ahead. */
  update(): void {
    if (!this.doc) { this.prefetch(); return; }
    const hero = this.opts.hero();
    if (!hero) { this.close(); return; }
    const mapNumber = this.opts.mapNumber();
    if (mapNumber !== this.loadedMap) { void this.switchMap(mapNumber); return; }
    const screen = this.screen();
    const key = `${hero.cell.x},${hero.cell.y},${screen.width},${screen.height},${this.hudBand().join(',')}`;
    if (key !== this.lastKey) {
      this.lastKey = key;
      this.syncScreen(screen);
      this.syncClips(screen);
      this.syncMap(screen, hero);
    }
    this.syncHint(screen);
    this.doc.update();
  }

  // LoadImages() runs when a map loads: the picture and markers are fetched then, so Tab opens at once.
  private prefetch(): void {
    const mapNumber = this.opts.mapNumber();
    if (mapNumber === null || mapNumber === this.prefetchedMap) return;
    this.prefetchedMap = mapNumber;
    void prepareDocument('mini_map').catch(() => null);
    void loadMiniMapData(mapNumber).then((data) => (data ? pictureOf(data.pictureUrl) : null)).catch(() => null);
  }

  // LoadImages() on entering another map: its picture and markers; none there closes the window.
  private async switchMap(mapNumber: number | null): Promise<void> {
    if (this.switching) return;
    this.switching = true;
    try {
      const data = mapNumber === null ? null : await loadMiniMapData(mapNumber);
      const picture = data ? await pictureOf(data.pictureUrl) : null;
      if (!this.doc || this.opts.mapNumber() !== mapNumber) return;
      if (!data || !picture) { this.close(); return; }
      this.markers = data.markers;
      this.model.map_source = picture;
      this.loadedMap = mapNumber;
      this.lastKey = '';
    } finally {
      this.switching = false;
    }
  }

  private syncScreen(screen: Screen): void {
    if (!this.doc) return;
    this.doc.root.style.setProperty('--overlay-scale-x', String(screen.scaleX));
    this.doc.root.style.setProperty('--overlay-scale-y', String(screen.scaleY));
    this.doc.setScale(screen.scaleY);
    this.model.text_px = FONT_SIZE * screen.scaleY;
    const lines: string[] = [];
    for (let i = 0; i < SIDE_TILES; i++) {
      const y = i * (TILE_WIDTH - 3);
      lines.push(matrixText(elementToQuad(rotatedQuad(screen, TILE_HEIGHT / 2, y, TILE_WIDTH, TILE_HEIGHT, -90), SIDE_ELEMENT_WIDTH, SIDE_ELEMENT_HEIGHT)));
      lines.push(matrixText(elementToQuad(rotatedQuad(screen, REFERENCE_WIDTH - TILE_HEIGHT / 2, y, TILE_WIDTH, TILE_HEIGHT, 90), SIDE_ELEMENT_WIDTH, SIDE_ELEMENT_HEIGHT)));
    }
    this.model.side_lines = lines;
  }

  // The bottom HUD strip's box (left, top, right, bottom) in window px, or none.
  private hudBand(): number[] {
    const strip = document.querySelector('.rml-main_frame')?.shadowRoot?.querySelector('#hud_strip');
    if (!strip) return [];
    const rect = strip.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return [];
    return [Math.round(rect.left), Math.round(rect.top), Math.round(rect.right), Math.round(rect.bottom)];
  }

  // SyncClips(): the map paints only outside the bottom HUD's band — above it, beside it and below it.
  private syncClips(screen: Screen): void {
    const { width, height } = screen;
    const band = this.hudBand();
    const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v));
    const bandTop = band.length ? clamp(band[1], 0, height) : height;
    const bandBottom = band.length ? clamp(band[3], bandTop, height) : height;
    const bandLeft = band.length ? clamp(band[0], 0, width) : 0;
    const bandRight = band.length ? clamp(band[2], bandLeft, width) : width;
    const clips: Clip[] = [{ left: 0, top: 0, width, height: bandTop, world_left: 0, world_top: 0 }];
    if (bandLeft > 0 && bandBottom > bandTop) clips.push({ left: 0, top: bandTop, width: bandLeft, height: bandBottom - bandTop, world_left: 0, world_top: -bandTop });
    if (bandRight < width && bandBottom > bandTop) clips.push({ left: bandRight, top: bandTop, width: width - bandRight, height: bandBottom - bandTop, world_left: -bandRight, world_top: -bandTop });
    if (bandBottom < height) clips.push({ left: 0, top: bandBottom, width, height: height - bandBottom, world_left: 0, world_top: -bandBottom });
    this.model.clips = clips;
  }

  // SyncMap(): the map's and markers' turned quads around the hero's cell.
  private syncMap(screen: Screen, hero: MiniMapHero): void {
    const length = MAP_LENGTH;
    const heroY = (hero.cell.x / 256) * length;
    const heroX = (hero.cell.y / 256) * length;
    this.model.map_transform = matrixText(elementToQuad(mapQuad(screen, heroX, heroY, length, MAP_ROTATION), length, length));
    const entries: MarkerEntry[] = [];
    const boxes: { name: string; box: [number, number, number, number] }[] = [];
    for (const data of this.markers) {
      if (data.kind <= 0) break;
      if (data.kind !== 1 && data.kind !== 2) continue;
      const portal = data.kind === 2;
      const size = portal ? PORTAL_SIZE : NPC_SIZE;
      const pointY = (data.location[0] / 256) * length;
      const pointX = (data.location[1] / 256) * length;
      const marker = markerQuad(screen, heroX, heroY, pointX, pointY, size, data.rotation, length, MAP_ROTATION, portal);
      boxes.push({ name: data.name, box: marker.hitBox });
      entries.push({ portal, size, transform: matrixText(elementToQuad(marker.quad, size, size)) });
    }
    this.model.markers = entries;
    this.hitBoxes = boxes;
  }

  // Check_Btn(): the first marker whose box holds the pointer shows its name above the box, in a box
  // 6 units wider than the text, starting where the text would be centred on the marker box.
  private syncHint(screen: Screen): void {
    this.model.hint_visible = false;
    if (!this.pointer) return;
    const px = (this.pointer.x - screen.offsetX) / screen.scaleX;
    const py = (this.pointer.y - screen.offsetY) / screen.scaleY;
    const hit = this.hitBoxes.find(({ box }) => px > box[0] && px < box[0] + box[2] && py > box[1] && py < box[1] + box[3]);
    if (!hit) return;
    const size = measureText(hit.name);
    const x = Math.trunc(hit.box[0] + (hit.box[2] / 2 - Math.trunc(size.cx / 2)));
    const y = Math.trunc(hit.box[1] - (size.cy + 2));
    this.model.hint_visible = true;
    this.model.hint_text = hit.name;
    this.model.hint_left = x * screen.scaleX + screen.offsetX;
    this.model.hint_top = y * screen.scaleY + screen.offsetY;
    this.model.hint_width = (size.cx + 6) * screen.scaleX;
    this.model.hint_height = size.cy * screen.scaleY;
  }
}
