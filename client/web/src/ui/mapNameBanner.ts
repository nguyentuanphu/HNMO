// The map name banner (CUIMapName, map_name.rml): on entering a map the name fades in with its
// ornaments and the map's icon (Data/Interface/MapNameOrnament: MapIcons.json, Layout.json), stays
// five seconds and fades out (0.015 alpha a frame at the desktop's 25 frames a second).

import { loadImage, openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';

interface BannerModel extends RmlModel {
  alpha: number; map_name: string; language: string; icon: boolean; icon_source: string; icon_style: string; strife: boolean; strife_source: string;
}
interface IconPlace { x: number; y: number; width: number; height: number }

const SHOW_MS = 5000;
const STEP_PER_SECOND = 0.015 * 25;
const ORNAMENTS = '/data/Interface/MapNameOrnament';

let icons: { maps: Map<number, string>; places: Record<string, IconPlace> } | null = null;
async function loadIcons(): Promise<NonNullable<typeof icons>> {
  if (!icons) {
    const [mapIcons, layout] = await Promise.all([
      fetch(`${ORNAMENTS}/MapIcons.json`).then((r) => r.json() as Promise<{ maps: { map: number; icon: string }[] }>),
      fetch(`${ORNAMENTS}/Layout.json`).then((r) => r.json() as Promise<{ icons: Record<string, IconPlace> }>),
    ]);
    icons = { maps: new Map(mapIcons.maps.map((m) => [m.map, m.icon])), places: layout.icons };
  }
  return icons;
}

export class MapNameBanner {
  private doc: RmlDocument | null = null;
  private state: 'hide' | 'in' | 'show' | 'out' = 'hide';
  private shownFor = 0;
  private readonly model: BannerModel = { alpha: 0, map_name: '', language: 'en', icon: false, icon_source: '', icon_style: '', strife: false, strife_source: '' };

  async show(mapNumber: number, name: string): Promise<void> {
    if (!this.doc) {
      this.doc = await openDocument('map_name', this.model, { parent: document.body });
      this.doc.host.style.zIndex = '15';
      this.doc.host.style.pointerEvents = 'none';
      window.addEventListener('resize', () => this.place());
      this.place();
    }
    const data = await loadIcons().catch(() => null);
    const icon = data?.maps.get(mapNumber);
    const place = icon ? data?.places[icon] : undefined;
    this.model.map_name = name;
    this.model.icon = Boolean(icon && place);
    this.model.icon_source = icon ? `${ORNAMENTS}/${icon}.tga` : '';
    this.model.icon_style = place ? `left: ${place.x}px; top: ${place.y}px; width: ${place.width}px; height: ${place.height}px;` : '';
    this.model.alpha = 0.2;
    this.state = name ? 'in' : 'hide';
    this.shownFor = 0;
    this.doc.update();
    await this.loadImages();
  }

  // <img> sources are the game's .tga files: drawn through the interface image loader.
  private async loadImages(): Promise<void> {
    for (const img of this.doc?.root.querySelectorAll<HTMLImageElement>('img') ?? []) {
      const src = img.getAttribute('src') ?? '';
      if (!src || src.startsWith('data:') || src.startsWith('blob:')) continue;
      const canvas = await loadImage(src.replace(/^\/Data\//, '/data/'));
      if (canvas) img.src = canvas.toDataURL();
    }
  }

  private place(): void {
    if (!this.doc) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    Object.assign(this.doc.root.style, { width: `${window.innerWidth / scale}px`, height: `${REFERENCE_HEIGHT}px`, transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none' });
  }

  /** Every frame: the fade. */
  update(dt: number): void {
    if (!this.doc || this.state === 'hide') return;
    if (this.state === 'in') {
      this.model.alpha = Math.min(1, this.model.alpha + STEP_PER_SECOND * dt);
      if (this.model.alpha >= 1) this.state = 'show';
    } else if (this.state === 'show') {
      this.shownFor += dt * 1000;
      if (this.shownFor > SHOW_MS) this.state = 'out';
    } else {
      this.model.alpha = Math.max(0, this.model.alpha - STEP_PER_SECOND * dt);
      if (this.model.alpha <= 0) this.state = 'hide';
    }
    this.doc.update();
  }
}
