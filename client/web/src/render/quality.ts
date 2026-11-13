// HNMO extra: the low-spec switches of the settings window (ui/settingsWindow.ts). They work on
// what the web game draws with: the renderer's pixel ratio (render scale), a frame cap the frame
// loop asks before it draws (frameDue), the terrain's fog distance with the camera's far plane
// (view distance), and the skill effects' group (effects on / off). The frame cap uses the
// desktop option window's FPS limit choices (s_FpsCapValues, 0 = uncapped).

import type * as THREE from 'three';

export interface QualitySettings {
  /** Share of the screen's pixel ratio the game draws at: 1, 0.75 or 0.5. */
  renderScale: number;
  /** Frames per second at most; 0 = no cap. */
  fpsCap: number;
  /** Fog and far plane distance factor: 0.6 near, 1 normal. */
  viewDistance: number;
  /** Skill effects drawn. */
  effects: boolean;
}

export const RENDER_SCALES = [1, 0.75, 0.5];
export const FPS_CAPS = [24, 30, 60, 120, 144, 0];
export const VIEW_DISTANCES = [0.6, 0.8, 1];
export const DEFAULT_QUALITY: QualitySettings = { renderScale: 1, fpsCap: 0, viewDistance: 1, effects: true };
/** The low-spec mode in one switch. */
export const LOW_SPEC: QualitySettings = { renderScale: 0.5, fpsCap: 30, viewDistance: 0.6, effects: false };

export interface QualityTarget {
  renderer: THREE.WebGLRenderer;
  scene: THREE.Object3D;
  camera: THREE.PerspectiveCamera;
  /** The skill effects' group, once the game has one. */
  effects?: () => THREE.Object3D | undefined;
}

/** The highest pixel ratio the game draws at (main.ts: Math.min(devicePixelRatio, 2)). */
const MAX_PIXEL_RATIO = 2;
/** The far plane stays this much beyond the fog's end. */
const FAR_PAST_FOG = 1.25;

let current: QualitySettings = { ...DEFAULT_QUALITY };
let target: QualityTarget | null = null;
let baseFar: number | null = null;
let lastShown = 0;

export function isLowSpec(settings: QualitySettings): boolean {
  return settings.renderScale === LOW_SPEC.renderScale && settings.fpsCap === LOW_SPEC.fpsCap
    && settings.viewDistance === LOW_SPEC.viewDistance && settings.effects === LOW_SPEC.effects;
}

/** Applies the settings to the renderer, camera and scene (again after a map loads: refreshQuality). */
export function applyQuality(to: QualityTarget, settings: QualitySettings): void {
  target = to;
  current = { ...settings };
  const ratio = Math.min(window.devicePixelRatio || 1, MAX_PIXEL_RATIO) * settings.renderScale;
  if (to.renderer.getPixelRatio() !== ratio) to.renderer.setPixelRatio(ratio);
  refreshQuality();
}

/** Puts the view distance and effects switch on what was added since (new map, effect system). */
export function refreshQuality(): void {
  if (!target) return;
  const effects = target.effects?.();
  if (effects) effects.visible = current.effects;
  let fogFar = 0;
  target.scene.traverse((object) => {
    const material = (object as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
    for (const one of Array.isArray(material) ? material : material ? [material] : []) {
      const uniforms = (one as THREE.ShaderMaterial).uniforms as Record<string, THREE.IUniform<unknown>> | undefined;
      const near = uniforms?.fogNear, far = uniforms?.fogFar;
      if (!near || !far || typeof near.value !== 'number' || typeof far.value !== 'number') continue;
      const base = (one.userData.hnmoFog ??= { near: near.value, far: far.value }) as { near: number; far: number };
      near.value = base.near * current.viewDistance;
      far.value = base.far * current.viewDistance;
      fogFar = Math.max(fogFar, base.far);
    }
  });
  const camera = target.camera;
  baseFar ??= camera.far;
  const far = current.viewDistance >= 1 || fogFar === 0 ? baseFar : Math.min(baseFar, fogFar * current.viewDistance * FAR_PAST_FOG);
  if (camera.far !== far) { camera.far = far; camera.updateProjectionMatrix(); }
}

/**
 * The frame cap: the frame loop calls this with requestAnimationFrame's time and draws only when
 * it returns true (otherwise it just asks for the next frame).
 */
export function frameDue(now: number): boolean {
  if (current.fpsCap <= 0) return true;
  const interval = 1000 / current.fpsCap;
  // A little slack, so a 60 Hz screen still shows 30 frames at a cap of 30.
  if (now - lastShown < interval - 2) return false;
  lastShown = now;
  return true;
}
