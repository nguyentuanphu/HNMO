// The math, timing and texture helpers the effect system shares, ported from the desktop
// client (client/src/source):
//   AngleMatrix / VectorRotate              Core/Math (angles in degrees, applied z, y, x)
//   CreateAngle, CreateAngle2D, TurnAngle2  Engine/AI/AngleMath.cpp
//   MoveHumming                             Engine/AI/ZzzAI.cpp
//   GetMagicScrewAt, ReferenceFrameClock    Render/Effects/ZzzEffectJoint.cpp
// Everything is in the desktop's units: x and y on the ground (100 per terrain cell), z up,
// angles in degrees, and one tick per 25 FPS frame (40 ms).

import * as THREE from 'three';
import { loadTexture } from '../../bmd.js';

/** The desktop advances effects once per reference frame: 25 per second. */
export const TICKS_PER_SECOND = 25;
export const TICK_SECONDS = 1 / TICKS_PER_SECOND;

const DEGREES = Math.PI / 180;

/** Random::Rand() % n. */
export function randInt(n: number): number {
  return Math.floor(Math.random() * n);
}

/** AngleMatrix: degrees around x, y and z, applied z, y, x (three.js 'ZYX'). */
export function angleEuler(angle: THREE.Vector3, out = new THREE.Euler()): THREE.Euler {
  return out.set(angle.x * DEGREES, angle.y * DEGREES, angle.z * DEGREES, 'ZYX');
}

const rotateEuler = new THREE.Euler();

/** VectorRotate(v, AngleMatrix(angle)): v turned by the angle, written to out. */
export function rotateByAngle(v: THREE.Vector3, angle: THREE.Vector3, out = new THREE.Vector3()): THREE.Vector3 {
  return out.copy(v).applyEuler(angleEuler(angle, rotateEuler));
}

function normalizeDegrees(angle: number): number {
  const a = angle % 360;
  return a < 0 ? a + 360 : a;
}

/** CreateAngle: the heading from (x1, y1) to (x2, y2) in degrees, 0 facing -y. */
export function createAngle(x1: number, y1: number, x2: number, y2: number): number {
  const dx = x2 - x1, dy = y2 - y1;
  if (Math.abs(dx) < Number.EPSILON && Math.abs(dy) < Number.EPSILON) return 0;
  return normalizeDegrees(Math.atan2(dx, -dy) / DEGREES);
}

/** CreateAngle2D. */
export function createAngle2D(from: THREE.Vector3, to: THREE.Vector3): number {
  return createAngle(from.x, from.y, to.x, to.y);
}

/** TurnAngle2: turns angle towards a by at most d degrees. */
export function turnAngle2(angle: number, a: number, d: number): number {
  if (d <= 0) return normalizeDegrees(angle);
  let delta = normalizeDegrees(a) - normalizeDegrees(angle);
  if (delta > 180) delta -= 360;
  else if (delta < -180) delta += 360;
  return normalizeDegrees(angle + Math.max(-d, Math.min(d, delta)));
}

/** MoveHumming: turns angle (x pitch, z heading) towards the target by `turn` degrees; returns the distance. */
export function moveHumming(position: THREE.Vector3, angle: THREE.Vector3, target: THREE.Vector3, turn: number): number {
  angle.z = turnAngle2(angle.z, createAngle(position.x, position.y, target.x, target.y), turn);
  const rx = position.x - target.x, ry = position.y - target.y, rz = position.z - target.z;
  const flat = Math.sqrt(rx * rx + ry * ry);
  angle.x = turnAngle2(angle.x, 360 - createAngle(position.z, flat, target.z, 0), turn);
  return Math.sqrt(rx * rx + ry * ry + rz * rz);
}

/** GetMagicScrewAt: a point on a slowly turning unit sphere, for orbits. */
export function magicScrewAt(frame: number, speedRate: number, out = new THREE.Vector3()): THREE.Vector3 {
  const s0 = 0.048 * speedRate, s1 = 0.0613 * speedRate, s2 = 0.1113 * speedRate;
  const tilt = Math.sin((frame + 55555) * s0);
  const t0 = tilt * Math.cos(frame * s1), t1 = tilt * Math.sin(frame * s1), t2 = Math.cos((frame + 55555) * s0);
  const sinAdd = Math.sin((frame + 11111) * s2), cosAdd = Math.cos((frame + 11111) * s2);
  return out.set(cosAdd * t1 - sinAdd * t2, sinAdd * t1 + cosAdd * t2, t0);
}

// ---------------------------------------------------------------------------
// Textures of the desktop's BITMAP_* numbers (LoadBitmap in Engine/Object/ZzzOpenData.cpp).
// ---------------------------------------------------------------------------

/** The effect textures this port draws, by the name of the desktop's bitmap. */
export const BITMAPS = {
  energy: 'Thunder01.jpg', // BITMAP_ENERGY
  spark: 'Spark02.jpg', // BITMAP_SPARK
  spark3: 'Spark03.jpg', // BITMAP_SPARK+1
  fire: 'Fire01.jpg', // BITMAP_FIRE
  smoke: 'smoke01.jpg', // BITMAP_SMOKE
  blood2: 'blood.tga', // BITMAP_BLOOD+1
  explosion: 'Explotion01.jpg', // BITMAP_EXPLOTION
  light: 'flare01.jpg', // BITMAP_LIGHT
  shiny2: 'Shiny02.jpg', // BITMAP_SHINY+1
  flareBlue: 'flareBlue.jpg', // BITMAP_FLARE_BLUE
  magicGround2: 'Magic_Ground2.jpg', // BITMAP_MAGIC+1
  jointThunder: 'JointThunder01.jpg', // BITMAP_JOINT_THUNDER
  jointHealing: 'JointEnergy01.jpg', // BITMAP_JOINT_HEALING
} as const;

export type BitmapName = keyof typeof BITMAPS;

/** A loaded effect texture and its size in pixels (sprites are Width x Height times their scale). */
export interface EffectTexture {
  texture: THREE.Texture;
  width: number;
  height: number;
  /** Three colour components (a JPEG): drawn glowing (additive) as particles. */
  opaque: boolean;
}

const effectTextures = new Map<BitmapName, EffectTexture | null>();
const effectTextureLoads = new Map<BitmapName, Promise<EffectTexture | null>>();

function imageSize(image: unknown): { width: number; height: number } {
  if (image && typeof image === 'object' && 'width' in image && 'height' in image) {
    const { width, height } = image as { width: unknown; height: unknown };
    if (typeof width === 'number' && typeof height === 'number') return { width, height };
  }
  return { width: 0, height: 0 };
}

/** The texture of a bitmap once loaded (undefined while loading, null if missing). Starts the load. */
export function effectTexture(name: BitmapName): EffectTexture | null | undefined {
  if (!effectTextureLoads.has(name)) {
    const file = BITMAPS[name];
    effectTextureLoads.set(name, loadTexture('Effect', file).then((texture) => {
      const loaded = texture ? { texture, ...imageSize(texture.image), opaque: !file.toLowerCase().endsWith('.tga') } : null;
      effectTextures.set(name, loaded);
      return loaded;
    }));
  }
  return effectTextures.get(name);
}

/** Starts loading the given textures; resolves when all are loaded (or missing). */
export function preloadEffectTextures(names: readonly BitmapName[]): Promise<void> {
  for (const name of names) effectTexture(name);
  return Promise.all(names.map((name) => effectTextureLoads.get(name))).then(() => undefined);
}
