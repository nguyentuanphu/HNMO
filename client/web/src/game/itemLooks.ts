// Which meshes of an item are drawn, how and with what texture: the desktop's item looks
// (Render/Items/LookDrawing.cpp) from Data/Effects/ItemEffects.json (an item's "itemEffect": the values it
// starts with, such as its glowing mesh and hidden mesh) and Data/Effects/RenderStyles.json (an item's
// "renderStyle": its layers, each drawing some meshes, plainly or glowing, maybe with another texture).
// A mesh drawn as the "glowing mesh" (BMD BlendMesh) or by a "glow" layer is drawn additively, so its
// black parts don't show (ZzzBMD.cpp ResolveMaterial: BlendMesh, RENDER_BRIGHT). The moving parts of the
// looks (reflection layers, scrolling textures, waves) are left out; the values take their middle.

/** One drawing of a mesh. */
export interface MeshDraw {
  /** Added to what is behind it (the glowing mesh, a glow layer). */
  additive: boolean;
  /** Another texture, "Item/Deye_R.jpg" under Data; none: the mesh's own. */
  texture?: string;
  /** The light (body light) it is drawn in, times the glow strength for a glowing mesh. */
  light: [number, number, number];
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

function lightOf(value: unknown, current: [number, number, number], item: LookItem, own: Record<string, unknown> | undefined): [number, number, number] {
  const light = resolve(value, item, own);
  if (Array.isArray(light)) return [numberOf(light[0], 1), numberOf(light[1], 1), numberOf(light[2], 1)];
  if (isRecord(light) && Array.isArray(light.tint)) {
    return [numberOf(light.tint[0], 1) * current[0], numberOf(light.tint[1], 1) * current[1], numberOf(light.tint[2], 1) * current[2]];
  }
  if (isRecord(light) && light.brightness !== undefined) {
    const brightness = numberOf(light.brightness, 1);
    const brightest = Math.max(...current);
    return current.map((channel) => (brightest > 0 ? channel * brightness / brightest : brightness)) as [number, number, number];
  }
  return current;
}

/**
 * How each mesh of the item is drawn (in the order of its draws); an empty list: the mesh isn't drawn.
 * `noneBlendMeshes` matter only for the reflection passes, which are left out.
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
  for (const layer of layers) {
    if (!holds(layer.onlyWhen, item, place, values.hiddenMesh)) continue;
    if (layer.light !== undefined) light = lightOf(layer.light, light, item, own);
    if (layer.kind !== 'texture') continue; // reflection and flat colour passes are left out
    const glowMesh = glowMeshOf(layer.glowMesh, values.glowMesh);
    const glowStrength = layer.glowStrength !== undefined ? numberOf(resolve(layer.glowStrength, item, own), values.glowStrength) : values.glowStrength;
    const texture = resolve(layer.texture, item, own);
    for (const mesh of layerMeshes(layer.meshes, count, model, values.hiddenMesh, own)) {
      if (mesh < 0 || mesh >= count) continue;
      // ResolveMaterial: every mesh glows below -1, else the mesh whose texture is the glowing one.
      const glowing = glowMesh <= -2 || model.meshTextures[mesh] === glowMesh;
      const bright = layer.blend === 'glow';
      const strength = glowing ? glowStrength : 1;
      draws[mesh].push({
        additive: glowing || bright,
        texture: typeof texture === 'string' ? texture : undefined,
        light: [light[0] * strength, light[1] * strength, light[2] * strength],
      });
    }
  }
  return draws;
}
