// How items glow by level, excellent options and ancient sets, ported from the
// client's RenderPartObjectEffect (Engine/Object/ZzzObject.cpp), its glow data
// (Render/Items/ItemGlow.cpp, Data/Items/Models "glow", Data/Effects/GlowColors.json)
// and the chrome texture coordinates of shaders/skinned_vertex.hlsli.

import * as THREE from 'three';
import { loadTexture } from '../bmd.js';
import type { AnimatedModel, LightFactor, ModelPart } from '../bmd.js';
import type { AppearanceItem, ItemGlowData } from './items.js';

// Texture coordinate modes of the skinning shader (SkinningTextureCoordinates).
const Mode = { MESH: 0, CHROME: 1, CHROME2: 2, CHROME3: 3, CHROME4: 4, METAL: 9 } as const;
type Mode = (typeof Mode)[keyof typeof Mode];
const MODE_TEXTURE: Record<number, string> = {
  [Mode.CHROME]: 'Chrome01.jpg', [Mode.CHROME2]: 'Chrome02.jpg', [Mode.CHROME3]: 'Chrome02.jpg',
  [Mode.CHROME4]: 'Chrome02.jpg', [Mode.METAL]: 'Shiny01.jpg',
};

/** A color as red, green and blue, 0..1. */
export type GlowColor = [number, number, number];

const DEFAULT_COLORS: Record<string, GlowColor> = { orange: [1, 0.5, 0], white: [1, 1, 1], azure: [0.1, 0.6, 1] };
let glowColors: Record<string, GlowColor> = DEFAULT_COLORS;

export async function loadGlowColors(): Promise<void> {
  try {
    const response = await fetch('/data/Effects/GlowColors.json');
    if (response.ok) glowColors = { ...DEFAULT_COLORS, ...((await response.json()) as { colors?: Record<string, GlowColor> }).colors };
  } catch {
    // keep the defaults
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

// How the item's own light changes with its level (+3 to +6 tint, +7 and up dim a little).
export function levelLightFactor(level: number): LightFactor | null {
  if (level >= 16 || level < 3) return null;
  const factor = new THREE.Color();
  if (level < 5) return (s: number) => factor.setRGB(LUMINOSITY(s), LUMINOSITY(s) * 0.6, LUMINOSITY(s) * 0.6);
  if (level < 7) return (s: number) => factor.setRGB(LUMINOSITY(s) * 0.5, LUMINOSITY(s) * 0.7, LUMINOSITY(s));
  const dim = level < 9 ? 0.8 : 0.9;
  return () => factor.setRGB(dim, dim, dim);
}

const vertexShader = /* glsl */ `
  uniform int mode;
  uniform float wave;
  uniform float wave2;
  uniform vec2 chromeLight;
  varying vec2 vUv;
  void main() {
    vec3 n = normalize(mat3(modelMatrix) * normal);
    n = vec3(n.x, -n.z, n.y); // back to the game's axes: x east, y north, z up
    if (mode == 1) vUv = vec2(n.z * 0.5 + wave, n.y * 0.5 + wave * 2.0);
    else if (mode == 2) vUv = vec2((n.z + n.x) * 0.8 + wave2 * 2.0, n.y + n.x + wave2 * 3.0);
    else if (mode == 3) { float u = dot(n, vec3(0.0, -0.1, -0.8)); vUv = vec2(u, 1.0 - u); }
    else if (mode == 4) {
      float lightDot = dot(n, vec3(chromeLight, 1.0));
      vUv = vec2(lightDot + n.y * 0.5 + chromeLight.y * 3.0, 1.0 - lightDot - n.z * 0.5 - wave * 3.0);
    }
    else if (mode == 9) vUv = vec2(n.z * 0.5 + 0.2, n.y * 0.5 + 0.5);
    else vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
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
}

type GlowMaterial = THREE.ShaderMaterial & { uniforms: GlowUniforms };

function passMaterial(mode: Mode, map: THREE.Texture, passColor: THREE.Color): GlowMaterial {
  return new THREE.ShaderMaterial({
    vertexShader, fragmentShader,
    uniforms: {
      mode: { value: mode }, map: { value: map }, color: { value: passColor.clone() }, opacity: { value: 1 },
      wave: { value: 0 }, wave2: { value: 0 }, chromeLight: { value: new THREE.Vector2() },
    },
    blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, depthFunc: THREE.LessEqualDepth,
    side: THREE.DoubleSide,
  }) as GlowMaterial;
}

// Which meshes of the item a glow is drawn on: "meshes" only these, or all but "hiddenMesh".
function meshFilter(only: number[] | number | undefined, hidden: number | undefined): (index: number) => boolean {
  if (Array.isArray(only) && only.length) return (index: number) => only.includes(index);
  if (typeof only === 'number') return (index: number) => index === only;
  return (index: number) => index !== hidden;
}

// Adds the glow of an item (level, excellent, ancient) to the given parts of a model.
// `glow` is the item model's "glow" data; `item` holds glowLevel, excellent and ancient.
export async function addItemGlow(model: AnimatedModel, parts: ModelPart[], glow: ItemGlowData = {}, item: AppearanceItem): Promise<void> {
  const level = glow.level === undefined ? item.glowLevel ?? 0 : (Array.isArray(glow.level) ? glow.level[Math.min(item.glowLevel ?? 0, 15)] : glow.level);
  const passes = levelPasses(level);
  const excellent = item.excellent && glow.excellent !== false;
  const ancient = item.ancient && !excellent;
  const lightFactor = levelLightFactor(level);
  for (const part of parts) part.lightFactor = lightFactor;
  if (!passes.length && !excellent && !ancient) return;

  const glowColor = color(glow.color, 'orange');
  const shineColor = glow.shineWhite ? color('white') : color(glow.shineColor, 'white');
  const onGlow = meshFilter(glow.meshes, glow.hiddenMesh);
  const onShine = meshFilter(glow.shineMeshes, glow.shineHiddenMesh);
  const meshes: THREE.Mesh[] = [];
  const materials: { material: GlowMaterial; kind: 'level' | 'excellent' | 'ancient' }[] = [];

  for (const [mode, tint] of passes) {
    const map = await loadTexture('Effect', MODE_TEXTURE[mode]);
    if (!map) continue;
    map.wrapS = map.wrapT = mode === Mode.CHROME ? THREE.RepeatWrapping : THREE.ClampToEdgeWrapping;
    const filter = tint === 'shine' ? onShine : onGlow;
    for (const part of parts.filter((p) => filter(p.meshIndex) && p.mesh.material.visible)) {
      const material = passMaterial(mode, map, tint === 'shine' ? shineColor : glowColor);
      materials.push({ material, kind: 'level' });
      meshes.push(new THREE.Mesh(part.geometry, material));
    }
  }

  if (excellent) {
    // The excellent glow: the item's own texture, added in a color that wanders from red to blue.
    const only = glow.excellentMesh;
    for (const part of parts.filter((p) => (only === undefined || p.meshIndex === only) && p.mesh.material.map)) {
      const material = passMaterial(Mode.MESH, part.mesh.material.map!, new THREE.Color()); // the filter keeps parts with a map
      materials.push({ material, kind: 'excellent' });
      meshes.push(new THREE.Mesh(part.geometry, material));
    }
  } else if (ancient) {
    const map = await loadTexture('Effect', MODE_TEXTURE[Mode.CHROME3]);
    if (map) {
      for (const part of parts.filter((p) => onShine(p.meshIndex) && p.mesh.material.visible)) {
        const material = passMaterial(Mode.CHROME3, map, color(glow.ancientColor, 'azure'));
        materials.push({ material, kind: 'ancient' });
        meshes.push(new THREE.Mesh(part.geometry, material));
      }
    }
  }

  for (const mesh of meshes) mesh.frustumCulled = false;
  model.addGlow({
    meshes,
    update(seconds) {
      const ms = seconds * 1000;
      const wave = (Math.floor(ms) % 10000) * 0.0001;
      const wave2 = (Math.floor(ms) % 5000) * 0.00024 - 0.4;
      for (const { material, kind } of materials) {
        const u = material.uniforms;
        u.wave.value = wave;
        u.wave2.value = wave2;
        u.chromeLight.value.set(Math.cos(ms * 0.001), Math.sin(ms * 0.002));
        if (kind === 'excellent') {
          const l = Math.sin(ms * 0.002) * 0.5 + 0.5;
          u.color.value.setRGB(l, l * 0.3, 1 - l);
        } else if (kind === 'ancient') {
          u.opacity.value = Math.max(0, Math.sin(ms * 0.001) * 0.5 + 0.4);
        }
      }
    },
  });
}
