// Which meshes of an item are drawn, how and with what texture: the desktop's item looks
// (Render/Items/LookDrawing.cpp) from Data/Effects/ItemEffects.json (an item's "itemEffect": the values it
// starts with, such as its glowing mesh and hidden mesh) and Data/Effects/RenderStyles.json (an item's
// "renderStyle": its layers, each drawing some meshes, plainly or glowing, maybe with another texture).
// A mesh drawn as the "glowing mesh" (BMD BlendMesh) or by a "glow" layer is drawn additively, so its
// black parts don't show (ZzzBMD.cpp ResolveMaterial: BlendMesh, RENDER_BRIGHT). The moving values (waves, bounces,
// ramps, zigzags, scrolls, frames: ShapeAtClock in Render/Items/LookDrawing.cpp) move on the world's clock through a
// drawing's lightAt / scrollAt; reflection layers come as chrome passes in their motion (MotionFlag), drawn glowing.
// A random value takes its middle; a "turn" (by the object's angle) its start.

/** One drawing of a mesh. */
export interface MeshDraw {
  /** Added to what is behind it (the glowing mesh, a glow layer). */
  additive: boolean;
  /** Another texture, "Item/Deye_R.jpg" under Data; none: the mesh's own. */
  texture?: string;
  /** The light (body light) it is drawn in, times the glow strength for a glowing mesh (its value at rest). */
  light: [number, number, number];
  /** The light at the world's clock (milliseconds), when its light, glow strength or opacity moves. */
  lightAt?: (ms: number) => [number, number, number];
  /** The texture's scroll (u, v) at the world's clock, when it scrolls (scrollU / scrollV). */
  scrollAt?: (ms: number) => [number, number];
  /** A reflection layer: a chrome pass in this motion, with its own texture ("Effect/bab2.jpg") or the motion's. */
  reflection?: { motion: string; texture?: string };
}

/** A moving value of a look (Data::Looks::Shape). */
interface Shape {
  shape: string;
  from?: number; to?: number; seconds?: number; perSecond?: number; count?: number; step?: number; secondsEach?: number;
  timesItem?: boolean;
}

const isShape = (value: unknown): value is Shape => isRecord(value) && typeof value.shape === 'string';

/** Milliseconds, rounded to the microsecond (0.08 seconds count 80). */
const millis = (seconds: number): number => Math.round(seconds * 1e6) / 1e3;

/** ShapeAtClock: a moving value at the world's clock (milliseconds). */
export function shapeAt(shape: Shape, ms: number): number {
  const from = shape.from ?? 0, to = shape.to ?? from;
  switch (shape.shape) {
    case 'wave': return Math.sin(ms * (2 * Math.PI / ((shape.seconds ?? 1) * 1000))) * (to - from) / 2 + (to + from) / 2;
    case 'bounce': return from + Math.abs(Math.sin(ms * (Math.PI / ((shape.seconds ?? 1) * 1000)))) * (to - from);
    case 'scroll': return ms * ((shape.perSecond ?? 0) / 1000);
    case 'ramp': {
      const period = Math.max(1, Math.trunc(millis(shape.seconds ?? 1)));
      return from + (Math.trunc(ms) % period) * ((to - from) / period);
    }
    case 'zigzag': {
      const rise = Math.abs(to - from);
      if (rise === 0) return from;
      const at = (ms * (rise / Math.max(1, millis(shape.seconds ?? 1)))) % (2 * rise);
      const folded = at < rise ? at : 2 * rise - at;
      return from + (to < from ? -folded : folded);
    }
    case 'frames': {
      const each = Math.max(1, millis(shape.secondsEach ?? shape.seconds ?? 1));
      return (Math.floor(ms / each) % Math.max(1, shape.count ?? 1)) * (shape.step ?? 0);
    }
    case 'random': {
      const count = Math.max(1, Math.round((to - from) / (shape.step ?? 1)) + 1);
      return from + Math.floor((count - 1) / 2) * (shape.step ?? 0);
    }
    default: return from; // turn: by the object's angle, at rest its start
  }
}

/** A value now: a number, a moving shape at the clock, or the item's own. */
function valueAt(value: unknown, ms: number, fallback: number): number {
  if (typeof value === 'number') return value;
  if (isShape(value)) return shapeAt(value, ms);
  return fallback;
}

/** Whether a (resolved) value moves with the clock. */
function moves(value: unknown): boolean {
  if (isShape(value)) return value.shape !== 'random' && value.shape !== 'turn';
  if (Array.isArray(value)) return value.some(moves);
  if (isRecord(value)) return moves(value.tint) || moves(value.brightness);
  return false;
}

/** Where the item is drawn: worn by a character, in a window or on the ground. */
export type ItemPlace = 'worn' | 'inventory' | 'ground';

/** What a look needs to know of the item. */
export interface LookItem {
  itemEffect?: string;
  itemEffectValues?: Record<string, unknown>;
  renderStyle?: string;
  renderStyleValues?: Record<string, unknown>;
  level: number;
}

/** What a look needs to know of the model: per mesh, its texture index and whether a texture script or name hides it. */
export interface LookModel {
  meshTextures: number[];
  hiddenByTexture: (mesh: number) => boolean;
}

type Json = Record<string, unknown>;
interface LooksFile { looks?: Json[] }

let itemEffects = new Map<string, Json>();
let renderStyles = new Map<string, Json>();
let loading: Promise<void> | null = null;

async function readLooks(url: string): Promise<Map<string, Json>> {
  try {
    const response = await fetch(url);
    if (!response.ok) return new Map();
    const file = (await response.json()) as LooksFile;
    return new Map((file.looks ?? []).filter((look) => typeof look.name === 'string').map((look) => [look.name as string, look]));
  } catch {
    return new Map();
  }
}

/** Loads the looks once (with the item catalog). */
export function loadItemLooks(): Promise<void> {
  loading ??= Promise.all([readLooks('/data/Effects/ItemEffects.json'), readLooks('/data/Effects/RenderStyles.json')])
    .then(([effects, styles]) => { itemEffects = effects; renderStyles = styles; });
  return loading;
}

const isRecord = (value: unknown): value is Json => typeof value === 'object' && value !== null && !Array.isArray(value);

/** A value of a look: a number, a shape (its middle) or { eachItem, default } (the item's own value). */
function resolve(value: unknown, item: LookItem, own: Record<string, unknown> | undefined): unknown {
  if (isRecord(value) && typeof value.eachItem === 'string') {
    const mine = own?.[value.eachItem];
    return mine !== undefined ? mine : value.default;
  }
  return value;
}

function numberOf(value: unknown, fallback: number): number {
  if (typeof value === 'number') return value;
  if (isRecord(value) && typeof value.from === 'number') {
    const to = typeof value.to === 'number' ? value.to : value.from;
    // A wave, bounce or random value: its middle; a ramp or frames: where it starts.
    return value.shape === 'ramp' || value.shape === 'frames' || value.shape === 'scroll' ? value.from : (value.from + to) / 2;
  }
  return fallback;
}

/** GlowMeshOf: a mesh number, -2 for every mesh, -1 for none. */
function glowMeshOf(value: unknown, fromItem: number): number {
  if (value === undefined) return fromItem;
  if (value === 'all') return -2;
  if (value === 'none') return -1;
  return typeof value === 'number' ? value : fromItem;
}

function holds(when: unknown, item: LookItem, place: ItemPlace, hiddenMesh: number): boolean {
  if (!isRecord(when)) return true;
  if (typeof when.place === 'string') {
    const ok = when.place === 'worn' ? place === 'worn'
      : when.place === 'inventoryAndGround' ? place !== 'worn'
        : when.place === place;
    if (!ok) return false;
  }
  if (Array.isArray(when.level) && (item.level < Number(when.level[0]) || item.level > Number(when.level[1]))) return false;
  if (typeof when.hiddenMesh === 'number' && when.hiddenMesh !== hiddenMesh) return false;
  // A buff ("infinityArrow"): not for an item in a window or on the ground.
  if (when.buff !== undefined) return false;
  return true;
}

/** The item's values before its layers (ItemEffects.json itemValuesBefore). */
function itemValues(item: LookItem): { glowMesh: number; glowStrength: number; hiddenMesh: number } {
  const values = { glowMesh: -1, glowStrength: 1, hiddenMesh: -1 };
  const effect = item.itemEffect ? itemEffects.get(item.itemEffect) : undefined;
  const before = effect?.itemValuesBefore;
  if (!Array.isArray(before)) return values;
  for (const entry of before) {
    if (!isRecord(entry) || !holds(entry.onlyWhen, item, 'inventory', values.hiddenMesh)) continue;
    if (entry.glowMesh !== undefined) values.glowMesh = glowMeshOf(resolve(entry.glowMesh, item, item.itemEffectValues), values.glowMesh);
    if (entry.glowStrength !== undefined) values.glowStrength = numberOf(resolve(entry.glowStrength, item, item.itemEffectValues), 1);
    if (typeof entry.hiddenMesh === 'number') values.hiddenMesh = entry.hiddenMesh;
  }
  return values;
}

/** The meshes a layer draws (DrawsMesh). */
function layerMeshes(value: unknown, count: number, model: LookModel, hiddenMesh: number, own: Record<string, unknown> | undefined): number[] {
  const all = Array.from({ length: count }, (_, mesh) => mesh);
  const meshes = isRecord(value) && typeof value.eachItem === 'string' ? (own?.[value.eachItem] ?? value.default) : value;
  if (meshes === 'none') return [];
  if (meshes === undefined) return all.filter((mesh) => mesh !== hiddenMesh && !model.hiddenByTexture(mesh));
  if (meshes === 'allEvenHidden') return all.filter((mesh) => !model.hiddenByTexture(mesh));
  if (typeof meshes === 'number') return [meshes];
  if (Array.isArray(meshes)) return meshes.filter((mesh): mesh is number => typeof mesh === 'number');
  if (isRecord(meshes) && Array.isArray(meshes.allBut)) {
    const left = meshes.allBut;
    return all.filter((mesh) => !left.includes(mesh) && !model.hiddenByTexture(mesh));
  }
  return all;
}

function lightOf(value: unknown, current: [number, number, number], item: LookItem, own: Record<string, unknown> | undefined, ms?: number): [number, number, number] {
  const light = resolve(value, item, own);
  const n = (v: unknown): number => (ms === undefined ? numberOf(v, 1) : valueAt(v, ms, 1));
  if (Array.isArray(light)) return [n(light[0]), n(light[1]), n(light[2])];
  if (isRecord(light) && Array.isArray(light.tint)) {
    return [n(light.tint[0]) * current[0], n(light.tint[1]) * current[1], n(light.tint[2]) * current[2]];
  }
  if (isRecord(light) && light.brightness !== undefined) {
    const brightness = n(light.brightness);
    const brightest = Math.max(...current);
    return current.map((channel) => (brightest > 0 ? channel * brightness / brightest : brightness)) as [number, number, number];
  }
  return current;
}

/**
 * How each mesh of the item is drawn (in the order of its draws); an empty list: the mesh isn't drawn.
 * A reflection layer's drawing is a chrome pass (`reflection`), drawn glowing over the mesh.
 */
export function meshDraws(item: LookItem, model: LookModel, place: ItemPlace): MeshDraw[][] {
  const count = model.meshTextures.length;
  const draws: MeshDraw[][] = Array.from({ length: count }, () => []);
  const values = itemValues(item);
  const found = item.renderStyle ? renderStyles.get(item.renderStyle) : undefined;
  // A look only for the weapons in a monster's hands (notFor "outsideMonsterHands") isn't the item's.
  const style = Array.isArray(found?.notFor) && found.notFor.includes('outsideMonsterHands') ? undefined : found;
  const layers = Array.isArray(style?.layers) ? style.layers.filter(isRecord) : [{ kind: 'texture' }];
  const own = item.renderStyleValues;
  let light: [number, number, number] = [1, 1, 1];
  // The light layers set before a drawing, kept to evaluate a moving light at the clock.
  let lightLayers: unknown[] = [];
  for (const layer of layers) {
    if (!holds(layer.onlyWhen, item, place, values.hiddenMesh)) continue;
    if (layer.light !== undefined) {
      light = lightOf(layer.light, light, item, own);
      lightLayers = [...lightLayers, layer.light];
    }
    if (layer.kind !== 'texture' && layer.kind !== 'reflection') continue; // the flat colour pass is left out
    const glowMesh = glowMeshOf(layer.glowMesh, values.glowMesh);
    const strengthValue = layer.glowStrength !== undefined ? resolve(layer.glowStrength, item, own) : undefined;
    const glowStrength = strengthValue !== undefined ? numberOf(strengthValue, values.glowStrength) : values.glowStrength;
    const opacityValue = resolve(layer.opacity, item, own);
    const opacity = typeof opacityValue === 'number' ? opacityValue : isShape(opacityValue) ? numberOf(opacityValue, 1) : 1;
    const texture = resolve(layer.texture, item, own);
    const scrollU = resolve(layer.scrollU, item, own), scrollV = resolve(layer.scrollV, item, own);
    const reflection = layer.kind === 'reflection' ? { motion: typeof layer.motion === 'string' ? layer.motion : 'slowDrift', texture: typeof texture === 'string' ? texture : undefined } : undefined;
    const lights = lightLayers;
    const lightMoves = lights.some((value) => moves(resolve(value, item, own)));
    for (const mesh of layerMeshes(layer.meshes, count, model, values.hiddenMesh, own)) {
      if (mesh < 0 || mesh >= count) continue;
      // ResolveMaterial: every mesh glows below -1, else the mesh whose texture is the glowing one.
      const glowing = glowMesh <= -2 || model.meshTextures[mesh] === glowMesh;
      const bright = layer.blend === 'glow' || reflection !== undefined;
      const additive = glowing || bright;
      // An additive drawing's opacity darkens it (its colour is added); a plain one keeps its colour.
      const fade = additive ? opacity : 1;
      const strength = glowing ? glowStrength : 1;
      const draw: MeshDraw = {
        additive,
        texture: reflection || typeof texture !== 'string' ? undefined : texture,
        light: [light[0] * strength * fade, light[1] * strength * fade, light[2] * strength * fade],
      };
      if (reflection) draw.reflection = reflection;
      if (lightMoves || (glowing && moves(strengthValue)) || (additive && moves(opacityValue))) {
        draw.lightAt = (ms) => {
          let now: [number, number, number] = [1, 1, 1];
          for (const value of lights) now = lightOf(value, now, item, own, ms);
          const s = glowing ? (strengthValue !== undefined ? valueAt(strengthValue, ms, values.glowStrength) : values.glowStrength) : 1;
          const o = additive ? valueAt(opacityValue, ms, 1) : 1;
          return [now[0] * s * o, now[1] * s * o, now[2] * s * o];
        };
      }
      if (!reflection && (moves(scrollU) || moves(scrollV) || typeof scrollU === 'number' || typeof scrollV === 'number')) {
        draw.scrollAt = (ms) => [valueAt(scrollU, ms, 0), valueAt(scrollV, ms, 0)];
      }
      draws[mesh].push(draw);
    }
  }
  return draws;
}
