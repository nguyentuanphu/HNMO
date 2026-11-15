// How items glow by level, excellent options and ancient sets, ported from the
// client's RenderPartObjectEffect (Engine/Object/ZzzObject.cpp), its glow data
// (Render/Items/ItemGlow.cpp, Data/Items/Models "glow", Data/Effects/GlowColors.json)
// the shines below +3 (Data/Effects/Shines.json, Render/Items/ItemGlow.cpp FindShineBelowPlus3), the
// cloak's chrome (eBuff_Cloaking) and the chrome texture coordinates of shaders/skinned_vertex.hlsli.
//
// The pass materials are shared by every item of the same look (one per mode, texture and colour), so
// a new item costs no program or material; their waves follow the world's clock (WorldTime), as the
// desktop's do. `prewarmItemGlow` warms them a few seconds after entering the world.

import * as THREE from 'three';
import { loadTexture } from '../bmd.js';
import type { AnimatedModel, LightFactor, ModelPart } from '../bmd.js';
import type { AppearanceItem, ItemGlowData } from './items.js';

// Texture coordinate modes of the skinning shader (SkinningTextureCoordinates).
const Mode = { MESH: 0, CHROME: 1, CHROME2: 2, CHROME3: 3, CHROME4: 4, CHROME5: 5, CHROME6: 6, CHROME7: 7, OIL: 8, METAL: 9 } as const;
type Mode = (typeof Mode)[keyof typeof Mode];
// The texture a chrome pass binds without one of its own (BMD::RenderMesh: BITMAP_CHROME2, _CHROME6, _CHROME, _SHINY).
const MODE_TEXTURE: Record<number, string> = {
  [Mode.CHROME]: 'Chrome01.jpg', [Mode.CHROME2]: 'Chrome02.jpg', [Mode.CHROME3]: 'Chrome02.jpg',
  [Mode.CHROME4]: 'Chrome02.jpg', [Mode.CHROME5]: 'Chrome02.jpg', [Mode.CHROME6]: 'Chrome06.jpg',
  [Mode.CHROME7]: 'Chrome01.jpg', [Mode.OIL]: 'Chrome01.jpg', [Mode.METAL]: 'Shiny01.jpg',
};
// A look's reflection motion and its pass (LookDrawing.cpp MotionFlag).
const MOTION_MODE: Record<string, Mode> = {
  slowDrift: Mode.CHROME, fastSweep: Mode.CHROME2, followsLight: Mode.CHROME3, swirl: Mode.CHROME4,
  strongSwirl: Mode.CHROME5, stripes: Mode.CHROME6, verySlowDrift: Mode.CHROME7, oilSheen: Mode.OIL, stillShine: Mode.METAL,
};

/** A color as red, green and blue, 0..1. */
export type GlowColor = [number, number, number];

const DEFAULT_COLORS: Record<string, GlowColor> = { orange: [1, 0.5, 0], white: [1, 1, 1], azure: [0.1, 0.6, 1] };
let glowColors: Record<string, GlowColor> = DEFAULT_COLORS;

/** A shine below +3 (Data/Effects/Shines.json): the item's light factor and the passes drawn after it. */
interface ShinePass { kind?: string; motion?: string; opacity?: number; brightness?: number }
interface Shine { name: string; light?: number; passes?: ShinePass[] }
let shines = new Map<string, Shine>();

export async function loadGlowColors(): Promise<void> {
  try {
    const response = await fetch('/data/Effects/GlowColors.json');
    if (response.ok) glowColors = { ...DEFAULT_COLORS, ...((await response.json()) as { colors?: Record<string, GlowColor> }).colors };
  } catch {
    // keep the defaults
  }
  try {
    const response = await fetch('/data/Effects/Shines.json');
    if (response.ok) shines = new Map(((await response.json()) as { shines?: Shine[] }).shines?.map((shine) => [shine.name, shine]) ?? []);
  } catch {
    // no shines below +3
  }
}

// String() keeps the lookup of a missing name as it was (the key "undefined").
const color = (name?: string, fallback?: string) => new THREE.Color(...(glowColors[String(name)] ?? glowColors[String(fallback)]));

// The passes an item level draws on top of the item (+7 and up), each with its
// texture coordinates and the color it takes: the glow color or the shine color.
type PassTint = 'shine' | 'glow';

function levelPasses(level: number): [Mode, PassTint][] {
  if (level >= 13 && level <= 15) return [[Mode.CHROME4, 'shine'], [Mode.METAL, 'glow'], [Mode.CHROME, 'glow']];
  if (level >= 11) return [[Mode.CHROME2, 'shine'], [Mode.METAL, 'glow'], [Mode.CHROME, 'glow']];
  if (level >= 9) return [[Mode.CHROME, 'glow'], [Mode.METAL, 'glow']];
  if (level >= 7) return [[Mode.CHROME, 'glow']];
  return [];
}

const LUMINOSITY = (seconds: number) => Math.sin(seconds * 4) * 0.15 + 0.6; // g_Luminosity, WorldTime in ms

// How the item's own light changes with its level (+3 to +6 tint, +7 and up dim a little; below +3 a shine's light).
export function levelLightFactor(level: number, shine?: Shine): LightFactor | null {
  if (level < 3 && shine?.light !== undefined) {
    const light = new THREE.Color(shine.light, shine.light, shine.light);
    return () => light;
  }
  if (level >= 16 || level < 3) return null;
  const factor = new THREE.Color();
  if (level < 5) return (s: number) => factor.setRGB(LUMINOSITY(s), LUMINOSITY(s) * 0.6, LUMINOSITY(s) * 0.6);
  if (level < 7) return (s: number) => factor.setRGB(LUMINOSITY(s) * 0.5, LUMINOSITY(s) * 0.7, LUMINOSITY(s));
  const dim = level < 9 ? 0.8 : 0.9;
  return () => factor.setRGB(dim, dim, dim);
}

// Skinned on the GPU when drawn over a skinned model's part (the skinning chunks do nothing otherwise).
const vertexShader = /* glsl */ `
  #include <common>
  #include <skinning_pars_vertex>
  uniform int mode;
  uniform float wave;
  uniform float wave2;
  uniform vec2 chromeLight;
  uniform float drift;
  varying vec2 vUv;
  void main() {
    vec3 objectNormal = vec3(normal);
    #include <skinbase_vertex>
    #include <skinnormal_vertex>
    vec3 transformed = vec3(position);
    #include <skinning_vertex>
    vec3 n = normalize(mat3(modelMatrix) * objectNormal);
    n = vec3(n.x, -n.z, n.y); // back to the game's axes: x east, y north, z up
    if (mode == 1) vUv = vec2(n.z * 0.5 + wave, n.y * 0.5 + wave * 2.0);
    else if (mode == 2) vUv = vec2((n.z + n.x) * 0.8 + wave2 * 2.0, n.y + n.x + wave2 * 3.0);
    else if (mode == 3) { float u = dot(n, vec3(0.0, -0.1, -0.8)); vUv = vec2(u, 1.0 - u); }
    else if (mode == 4) {
      float lightDot = dot(n, vec3(chromeLight, 1.0));
      vUv = vec2(lightDot + n.y * 0.5 + chromeLight.y * 3.0, 1.0 - lightDot - n.z * 0.5 - wave * 3.0);
    }
    else if (mode == 5) {
      float lightDot = dot(n, vec3(chromeLight, 1.0));
      vUv = vec2(lightDot + n.y * 3.0 + chromeLight.y * 5.0, 1.0 - lightDot - n.z * 2.5 - wave);
    }
    else if (mode == 6) vUv = vec2((n.z + n.x) * 0.8 + wave2 * 2.0);
    else if (mode == 7) vUv = vec2((n.z + n.x) * 0.8 + drift);
    else if (mode == 8) vUv = n.xy;
    else if (mode == 9) vUv = vec2(n.z * 0.5 + 0.2, n.y * 0.5 + 0.5);
    else vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(transformed, 1.0);
  }
`;

const fragmentShader = /* glsl */ `
  uniform sampler2D map;
  uniform vec3 color;
  uniform float opacity;
  varying vec2 vUv;
  void main() {
    gl_FragColor = vec4(texture2D(map, vUv).rgb * color * opacity, 1.0);
  }
`;

interface GlowUniforms {
  [uniform: string]: THREE.IUniform;
  mode: THREE.IUniform<number>;
  map: THREE.IUniform<THREE.Texture>;
  color: THREE.IUniform<THREE.Color>;
  opacity: THREE.IUniform<number>;
  wave: THREE.IUniform<number>;
  wave2: THREE.IUniform<number>;
  chromeLight: THREE.IUniform<THREE.Vector2>;
  drift: THREE.IUniform<number>;
}

type GlowMaterial = THREE.ShaderMaterial & { uniforms: GlowUniforms };

/** What a pass's colour does over time: stays, wanders red to blue (excellent), pulses (ancient). */
type PassKind = 'still' | 'excellent' | 'ancient';

// The shared pass materials, one per mode, texture, colour and kind; `icon` ones are changed by the item icons
// (their blending keeps black see-through on a clear canvas), so they are kept apart.
const sharedMaterials = new Map<string, { material: GlowMaterial; kind: PassKind }>();
let lastUpdate = -1;

function passMaterial(mode: Mode, map: THREE.Texture, passColor: THREE.Color, kind: PassKind, icon: boolean): GlowMaterial {
  const key = `${mode}|${map.uuid}|${passColor.r.toFixed(4)},${passColor.g.toFixed(4)},${passColor.b.toFixed(4)}|${kind}|${icon ? 1 : 0}`;
  const shared = sharedMaterials.get(key);
  if (shared) return shared.material;
  const material = new THREE.ShaderMaterial({
    vertexShader, fragmentShader,
    uniforms: {
      mode: { value: mode }, map: { value: map }, color: { value: passColor.clone() }, opacity: { value: 1 },
      wave: { value: 0 }, wave2: { value: 0 }, chromeLight: { value: new THREE.Vector2() }, drift: { value: 0 },
    },
    blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, depthFunc: THREE.LessEqualDepth,
    side: THREE.DoubleSide,
  }) as GlowMaterial;
  sharedMaterials.set(key, { material, kind });
  updateShared(performance.now() / 1000, true);
  return material;
}

/** Moves every shared pass on to the world's time (once a frame, whoever asks first). */
function updateShared(seconds: number, force = false): void {
  if (!force && seconds === lastUpdate) return;
  lastUpdate = seconds;
  const ms = seconds * 1000;
  const wave = (Math.floor(ms) % 10000) * 0.0001;
  const wave2 = (Math.floor(ms) % 5000) * 0.00024 - 0.4;
  for (const { material, kind } of sharedMaterials.values()) {
    const u = material.uniforms;
    u.wave.value = wave;
    u.wave2.value = wave2;
    u.drift.value = ms * 0.00006;
    u.chromeLight.value.set(Math.cos(ms * 0.001), Math.sin(ms * 0.002));
    if (kind === 'excellent') {
      const l = Math.sin(ms * 0.002) * 0.5 + 0.5;
      u.color.value.setRGB(l, l * 0.3, 1 - l);
    } else if (kind === 'ancient') {
      u.opacity.value = Math.max(0, Math.sin(ms * 0.001) * 0.5 + 0.4);
    }
  }
}

const worldSeconds = () => performance.now() / 1000;

// Which meshes of the item a glow is drawn on: "meshes" only these, or all but "hiddenMesh".
function meshFilter(only: number[] | number | undefined, hidden: number | undefined): (index: number) => boolean {
  if (Array.isArray(only) && only.length) return (index: number) => only.includes(index);
  if (typeof only === 'number') return (index: number) => index === only;
  return (index: number) => index !== hidden;
}

/**
 * What a glow mesh is, for the debuffs that leave some out (RenderPartObjectEffect): `level` the level's passes and
 * the shines below +3 (not drawn while a debuff colours the body), `special` the excellent and ancient glow (not
 * drawn while hardened or cloaked).
 */
type GlowKind = 'level' | 'special';
const glowKinds = new WeakMap<THREE.Object3D, GlowKind>();

/** Where the item is drawn: in a window or on the ground the skin is hidden (BMD HideSkin). */
export type GlowPlace = 'worn' | 'inventory' | 'ground';

// Adds the glow of an item (level, excellent, ancient) to the given parts of a model.
// `glow` is the item model's "glow" data; `item` holds glowLevel, excellent and ancient.
export async function addItemGlow(model: AnimatedModel, parts: ModelPart[], glow: ItemGlowData = {}, item: AppearanceItem, place: GlowPlace = 'worn'): Promise<void> {
  const level = glow.level === undefined ? item.glowLevel ?? 0 : (Array.isArray(glow.level) ? glow.level[Math.min(item.glowLevel ?? 0, glow.level.length - 1)] : glow.level);
  const passes = levelPasses(level);
  const excellent = item.excellent && glow.excellent !== false;
  const ancient = item.ancient && !excellent;
  const shine = level < 3 && glow.shineBelowPlus3 ? shines.get(glow.shineBelowPlus3) : undefined;
  const lightFactor = levelLightFactor(level, shine);
  for (const part of parts) part.lightFactor = lightFactor;
  if (!passes.length && !excellent && !ancient && !shine) return;
  const icon = place === 'inventory';

  const glowColor = color(glow.color, 'orange');
  // PartObjectColor2: the shine colour in the item's light (+11 and up draw in 0.9 of it), or white.
  const shineColor = glow.shineWhite ? color('white') : color(glow.shineColor, 'white');
  const onGlow = meshFilter(glow.meshes, glow.hiddenMesh);
  const onShine = meshFilter(glow.shineMeshes, glow.shineHiddenMesh);
  const meshes: THREE.Mesh[] = [];
  const add = (part: ModelPart, material: GlowMaterial, kind: GlowKind) => {
    const mesh = model.overlay(part.geometry, material);
    glowKinds.set(mesh, kind);
    meshes.push(mesh);
  };
  const shown = (part: ModelPart) => part.mesh.material.visible;

  for (const [mode, tint] of passes) {
    const map = await chromeTexture(mode);
    if (!map) continue;
    const filter = tint === 'shine' ? onShine : onGlow;
    const passColor = tint === 'shine' ? (glow.shineWhite ? shineColor : shineColor.clone().multiplyScalar(0.9)) : glowColor;
    for (const part of parts.filter((p) => filter(p.meshIndex) && shown(p))) add(part, passMaterial(mode, map, passColor, 'still', icon), 'level');
    // RenderGlow: the mesh of "glowAgainInWhite" glows once more, in white.
    if (tint === 'glow' && glow.glowAgainInWhite !== undefined) {
      const again = parts.find((p) => p.meshIndex === glow.glowAgainInWhite && shown(p));
      if (again) add(again, passMaterial(mode, map, color('white'), 'still', icon), 'level');
    }
  }

  if (shine) {
    // RenderPartObjectBodyColor2 per pass: glowing in the shine colour times its brightness and opacity, on the
    // shine meshes; a "texture" pass with the meshes' own texture, a "reflection" pass moving as its motion says.
    for (const pass of shine.passes ?? []) {
      const strength = (pass.brightness ?? 1) * (pass.opacity ?? 1);
      const passColor = glow.shineWhite ? color('white') : shineColor.clone().multiplyScalar(strength * (shine.light ?? 1));
      if (pass.kind === 'texture') {
        for (const part of parts.filter((p) => onShine(p.meshIndex) && shown(p) && p.mesh.material.map)) {
          add(part, passMaterial(Mode.MESH, part.mesh.material.map!, passColor, 'still', icon), 'level'); // the filter keeps parts with a map
        }
        continue;
      }
      const mode = MOTION_MODE[pass.motion ?? 'slowDrift'] ?? Mode.CHROME;
      const map = await chromeTexture(mode);
      if (!map) continue;
      for (const part of parts.filter((p) => onShine(p.meshIndex) && shown(p))) add(part, passMaterial(mode, map, passColor, 'still', icon), 'level');
    }
  }

  if (excellent) {
    // The excellent glow: the item's own texture, added in a color that wanders from red to blue. Without the skin
    // (a window, the ground) a model may glow on another mesh (excellentMeshWithoutSkin).
    const only = place !== 'worn' && glow.excellentMeshWithoutSkin !== undefined ? glow.excellentMeshWithoutSkin : glow.excellentMesh;
    for (const part of parts.filter((p) => (only === undefined || p.meshIndex === only) && p.mesh.material.map)) {
      add(part, passMaterial(Mode.MESH, part.mesh.material.map!, new THREE.Color(), 'excellent', icon), 'special'); // the filter keeps parts with a map
    }
  } else if (ancient) {
    const map = await chromeTexture(Mode.CHROME3);
    if (map) {
      for (const part of parts.filter((p) => onShine(p.meshIndex) && shown(p))) {
        add(part, passMaterial(Mode.CHROME3, map, color(glow.ancientColor, 'azure'), 'ancient', icon), 'special');
      }
    }
  }

  for (const mesh of meshes) mesh.frustumCulled = false;
  model.addGlow({ meshes, update: () => updateShared(worldSeconds()) });
}

const lookTextures = new Map<string, Promise<THREE.Texture | null>>();

/**
 * A reflection layer of an item's look (Data/Effects/RenderStyles.json "reflection"): a chrome pass in its motion
 * (MotionFlag: slowDrift RENDER_CHROME, swirl RENDER_CHROME4, stillShine RENDER_METAL...), with the layer's own texture
 * ("Effect/bab2.jpg") or the motion's, glowing in the layer's light. Shared like the level's passes.
 */
export async function reflectionMaterial(motion: string, texture: string | undefined, light: readonly [number, number, number], icon: boolean): Promise<THREE.Material | null> {
  const mode = MOTION_MODE[motion] ?? Mode.CHROME;
  let map: THREE.Texture | null;
  if (texture) {
    let load = lookTextures.get(texture);
    if (!load) {
      const slash = texture.lastIndexOf('/');
      load = loadTexture(texture.slice(0, slash), texture.slice(slash + 1));
      lookTextures.set(texture, load);
    }
    map = await load;
  } else {
    map = await chromeTexture(mode);
  }
  return map ? passMaterial(mode, map, new THREE.Color(light[0], light[1], light[2]), 'still', icon) : null;
}

/** Keeps a look's chrome passes moving with the world's clock (their model's glow update). */
export function updateReflections(): void {
  updateShared(worldSeconds());
}

const chromeTextures = new Map<Mode, Promise<THREE.Texture | null>>();
/** A chrome pass's texture (Data/Effect), loaded once. */
function chromeTexture(mode: Mode): Promise<THREE.Texture | null> {
  let texture = chromeTextures.get(mode);
  if (!texture) {
    texture = loadTexture('Effect', MODE_TEXTURE[mode]).then((map) => {
      // Chrome01 repeats (GL_REPEAT); the others clamp (ZzzOpenData: GL_CLAMP_TO_EDGE).
      if (map && mode !== Mode.CHROME && mode !== Mode.CHROME7 && mode !== Mode.OIL) {
        map.wrapS = map.wrapT = THREE.ClampToEdgeWrapping;
        map.needsUpdate = true;
      } else if (map) {
        map.wrapS = map.wrapT = THREE.RepeatWrapping;
        map.needsUpdate = true;
      }
      return map;
    });
    chromeTextures.set(mode, texture);
  }
  return texture;
}

/**
 * The parts of RenderPartObjectEffect a character's state leaves out: while a debuff colours the body (poison,
 * freeze, stun, harden, Blow of Destruction) the item draws in that colour without its level's passes and light;
 * hardened (or cloaked) it also loses its excellent and ancient glow.
 */
export function setItemGlowState(model: AnimatedModel, { debuffed, hardened }: { debuffed: boolean; hardened: boolean }): void {
  model.plainLight = debuffed;
  for (const glow of model.glows) {
    for (const mesh of glow.meshes) {
      const kind = glowKinds.get(mesh);
      if (kind === 'level') mesh.visible = !debuffed;
      else if (kind === 'special') mesh.visible = !hardened;
    }
  }
}

const cloaks = new WeakMap<AnimatedModel, THREE.Mesh[]>();

/**
 * eBuff_Cloaking (Invisibility): every part is drawn only as a see-through chrome shell (RENDER_BRIGHT |
 * RENDER_CHROME5 with BITMAP_CHROME2, white, at alpha 0.5), without its body, glow or shadow.
 */
export async function setCloaked(model: AnimatedModel, on: boolean): Promise<void> {
  let shell = cloaks.get(model);
  if (on && !shell) {
    const map = await chromeTexture(Mode.CHROME5);
    if (!map || cloaks.has(model)) return;
    const material = passMaterial(Mode.CHROME5, map, new THREE.Color(0.5, 0.5, 0.5), 'still', false);
    shell = model.parts.filter((part) => part.mesh.material.visible).map((part) => {
      const mesh = model.overlay(part.geometry, material);
      mesh.visible = false;
      model.group.add(mesh);
      return mesh;
    });
    cloaks.set(model, shell);
  }
  if (!shell) return;
  for (const mesh of shell) mesh.visible = on;
  for (const part of model.parts) part.mesh.visible = !on;
  for (const glow of model.glows) for (const mesh of glow.meshes) {
    if (on) mesh.visible = false;
    else if (!glowKinds.has(mesh)) mesh.visible = true; // the look's own extra drawings; the glow's come back with setItemGlowState
  }
}

/**
 * Builds the pass materials of every glow look (the levels' chromes and metal, the shines, excellent, ancient, the
 * cloak) and hands a model of each to `warm` (program compile and texture upload), skinned and posed on the CPU.
 * The window icons draw with a renderer of their own, so they aren't warmed here.
 */
export async function prewarmItemGlow(build: (level: number, excellent: boolean, ancient: boolean, place: GlowPlace) => Promise<AnimatedModel | null>,
  warm: (object: THREE.Object3D) => Promise<void>): Promise<void> {
  for (const place of ['worn', 'ground'] as GlowPlace[]) {
    for (const [level, excellent, ancient] of [[7, false, false], [9, false, false], [11, false, true], [13, true, false], [15, false, false]] as [number, boolean, boolean][]) {
      const model = await build(level, excellent, ancient, place).catch(() => null);
      if (!model) continue;
      if (place === 'worn') await setCloaked(model, true);
      model.group.traverse((object) => { object.visible = true; });
      await warm(model.group).catch(() => undefined);
      model.release();
    }
  }
}

/** How many glow pass materials exist (one per look, shared by every item): for the tests. */
export function glowMaterialCount(): number {
  return sharedMaterials.size;
}
