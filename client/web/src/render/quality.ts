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
  /** Hide other players (models, attachments, labels) in crowded scenes. */
  hideOtherPlayers: boolean;
  /** Hide wings and follower pets / companions. */
  hideWingsPets: boolean;
  /** Reduce skill trails and item glow/sparkle passes. */
  reducedEffects: boolean;
}

export const RENDER_SCALES = [1, 0.75, 0.5];
export const FPS_CAPS = [24, 30, 60, 120, 144, 0];
export const VIEW_DISTANCES = [0.6, 0.8, 1];
export const DEFAULT_QUALITY: QualitySettings = {
  renderScale: 1, fpsCap: 0, viewDistance: 1, effects: true,
  hideOtherPlayers: false, hideWingsPets: false, reducedEffects: false,
};
/** The low-spec mode in one switch. */
export const LOW_SPEC: QualitySettings = {
  renderScale: 0.5, fpsCap: 30, viewDistance: 0.6, effects: false,
  hideOtherPlayers: false, hideWingsPets: false, reducedEffects: true,
};
/** Crowded-event performance preset: hides other players, wings/pets, and heavy effects without capping FPS. */
export const PERFORMANCE_MODE: Partial<QualitySettings> = {
  viewDistance: 0.6, hideOtherPlayers: true, hideWingsPets: true, reducedEffects: true,
};

export interface QualityTarget {
  renderer: THREE.WebGLRenderer;
  scene: THREE.Object3D;
  camera: THREE.PerspectiveCamera;
  /** The skill effects' group, once the game has one. */
  effects?: () => THREE.Object3D | undefined;
}

/** The highest pixel ratio the game draws at (main.ts: Math.min(devicePixelRatio, 2)). */
const MAX_PIXEL_RATIO = 2;
/** The most pixels the 3D world is drawn at, at the 100 % render scale (2560 x 1440). */
export const MAX_DRAWN_PIXELS = 2560 * 1440;
/** The far plane stays this much beyond the fog's end. */
const FAR_PAST_FOG = 1.25;

let current: QualitySettings = { ...DEFAULT_QUALITY };
let target: QualityTarget | null = null;
let baseFar: number | null = null;

export function isLowSpec(settings: QualitySettings): boolean {
  return settings.renderScale === LOW_SPEC.renderScale && settings.fpsCap === LOW_SPEC.fpsCap
    && settings.viewDistance === LOW_SPEC.viewDistance && settings.effects === LOW_SPEC.effects;
}

export function isPerformanceMode(settings: QualitySettings): boolean {
  return settings.hideOtherPlayers && settings.hideWingsPets && settings.reducedEffects;
}

/** Live crowd-performance flags checked per frame by characters, companions, and effects. */
export function performanceFlags(): { hideOtherPlayers: boolean; hideWingsPets: boolean; reducedEffects: boolean } {
  return {
    hideOtherPlayers: current.hideOtherPlayers,
    hideWingsPets: current.hideWingsPets,
    reducedEffects: current.reducedEffects,
  };
}

/** Applies the settings to the renderer, camera and scene (again after a map loads: refreshQuality). */
export function applyQuality(to: QualityTarget, settings: QualitySettings): void {
  target = to;
  current = { ...settings };
  applyPixelRatio();
  refreshQuality();
}

/**
 * The renderer's pixel ratio: the screen's (at most 2) times the render scale, kept within a pixel budget
 * (2560 x 1440 at 100 %): a very large window or a high-density screen draws the 3D world at that many
 * pixels and the browser stretches it, while the interface (HTML over the canvas) stays sharp. main.ts
 * calls this again when the window changes size.
 */
export function applyPixelRatio(): void {
  if (!target) return;
  const canvas = target.renderer.domElement;
  const width = canvas.clientWidth || window.innerWidth, height = canvas.clientHeight || window.innerHeight;
  const screenRatio = Math.min(window.devicePixelRatio || 1, MAX_PIXEL_RATIO);
  const budgetRatio = Math.sqrt(MAX_DRAWN_PIXELS / Math.max(1, width * height));
  const ratio = Math.round(Math.min(screenRatio, budgetRatio) * current.renderScale * 1000) / 1000;
  if (target.renderer.getPixelRatio() !== ratio) target.renderer.setPixelRatio(ratio);
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
 * it returns true (otherwise it just asks for the next frame). Frames are paced by rAF's own
 * timestamps (never setTimeout) on a whole number of screen refreshes: the cap's interval rounded
 * to the nearest count of refreshes, so every drawn frame stays on screen equally long. A 60 FPS cap
 * draws every refresh of a 60 Hz screen and every second refresh of a 120 / 144 Hz one (60 / 72 FPS);
 * a "N ms since the last frame" test instead alternates 2 and 3 refreshes there, which judders.
 */
export function frameDue(now: number): boolean {
  const delta = now - lastSeen;
  lastSeen = now;
  // The screen's refresh interval: the average rAF step, leaving out steps that missed a refresh
  // (a much shorter step means a faster screen: the average follows it at once).
  if (delta > 0.5 && delta < 100) {
    if (delta < refreshMs * 0.7) refreshMs = delta;
    else if (delta < refreshMs * 1.4) refreshMs += (delta - refreshMs) * 0.05;
    else if (++slowSteps > 30) { refreshMs = delta; slowSteps = 0; } // a slower screen (or the game is slower than it)
    if (delta < refreshMs * 1.4) slowSteps = 0;
  }
  if (current.fpsCap <= 0) return true;
  const refreshes = Math.max(1, Math.round(1000 / current.fpsCap / refreshMs));
  if (refreshes === 1 || now < lastDrawn || now - lastDrawn >= (refreshes - 0.5) * refreshMs) { lastDrawn = now; return true; }
  return false;
}

let lastSeen = 0;
let lastDrawn = 0;
let slowSteps = 0;
let refreshMs = 1000 / 60;
