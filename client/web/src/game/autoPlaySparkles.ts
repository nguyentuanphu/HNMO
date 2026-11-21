// 3D Ground Sparkle Trail & Hunting-Range Ring for Ultimate Autoplay:
// Renders animated color-coded sparkles along the terrain path (findPath) from the hero to:
// - Gold: active Autoplay target, rare drop, patrol spot, or town trip destination
// - Cyan: active Quest objective / NPC on the current map
// - Crimson/Violet: live World Boss on the current map
// Also draws a subtle glowing rune ring around the Autoplay hunting origin.

import * as THREE from 'three';
import { sampleHeight } from '../terrain.js';
import { cellCenter } from './characters.js';
import { findPath } from './pathfinding.js';

export type SparkleGoalKind = 'autoplay' | 'quest' | 'boss';

export interface SparkleGoal {
  kind: SparkleGoalKind;
  cell: { x: number; y: number };
}

export interface SparkleWorld {
  scene: THREE.Scene;
  heights: Float32Array;
  flags: Uint16Array;
  heroCell: () => { x: number; y: number };
  originCell: () => { x: number; y: number } | null;
  huntingRange: () => number;
  autoPlayActive: () => boolean;
  goal: () => SparkleGoal | null;
  enabled: () => boolean;
}

const COLORS: Record<SparkleGoalKind, number> = {
  autoplay: 0xf4d06f, // gold
  quest: 0x38bdf8,    // cyan
  boss: 0xf43f5e,     // crimson
};

const MAX_SPARKLES = 48;
const RING_SEGMENTS = 48;

export class AutoPlaySparkles {
  private readonly points: THREE.Points;
  private readonly pointGeom: THREE.BufferGeometry;
  private readonly pointMat: THREE.PointsMaterial;
  private readonly positions = new Float32Array(MAX_SPARKLES * 3);

  private readonly destRing: THREE.LineLoop;
  private readonly rangeRing: THREE.LineLoop;

  private lastPathAt = 0;
  private cachedPath: { x: number; y: number }[] = [];
  private cachedKey = '';

  constructor(private readonly world: SparkleWorld) {
    this.pointGeom = new THREE.BufferGeometry();
    this.pointGeom.setAttribute('position', new THREE.BufferAttribute(this.positions, 3));
    this.pointMat = new THREE.PointsMaterial({
      color: COLORS.autoplay,
      size: 26,
      transparent: true,
      opacity: 0.92,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    this.points = new THREE.Points(this.pointGeom, this.pointMat);
    this.points.frustumCulled = false;
    this.points.visible = false;
    world.scene.add(this.points);

    this.destRing = makeRing(COLORS.autoplay, 0.75);
    this.rangeRing = makeRing(0xd4af37, 0.35);
    world.scene.add(this.destRing);
    world.scene.add(this.rangeRing);
  }

  update(now = performance.now()): void {
    if (!this.world.enabled()) {
      this.points.visible = false;
      this.destRing.visible = false;
      this.rangeRing.visible = false;
      return;
    }

    // 1. Hunting-range ring around Autoplay origin
    const origin = this.world.originCell();
    const active = this.world.autoPlayActive();
    if (active && origin) {
      const ox = cellCenter(origin.x);
      const oy = cellCenter(origin.y);
      const oz = sampleHeight(this.world.heights, ox, oy) + 12;
      const radius = Math.max(1, this.world.huntingRange()) * 100;
      this.rangeRing.position.set(ox, oy, oz);
      this.rangeRing.scale.set(radius, radius, 1);
      this.rangeRing.visible = true;
    } else {
      this.rangeRing.visible = false;
    }

    // 2. Ground sparkle trail to current goal (autoplay / quest / boss)
    const goal = this.world.goal();
    if (!goal) {
      this.points.visible = false;
      this.destRing.visible = false;
      return;
    }

    const hero = this.world.heroCell();
    const key = `${hero.x},${hero.y}->${goal.cell.x},${goal.cell.y}`;
    if (key !== this.cachedKey && now - this.lastPathAt > 250) {
      this.cachedKey = key;
      this.lastPathAt = now;
      const path = findPath(this.world.flags, hero, goal.cell);
      this.cachedPath = path.length > 0 ? [hero, ...path] : [hero, goal.cell];
    }

    const path = this.cachedPath;
    if (path.length < 2) {
      this.points.visible = false;
      this.destRing.visible = false;
      return;
    }

    const color = COLORS[goal.kind];
    this.pointMat.color.setHex(color);
    (this.destRing.material as THREE.LineBasicMaterial).color.setHex(color);

    const t = now * 0.0022;
    const count = Math.min(MAX_SPARKLES, Math.max(8, path.length * 3));
    for (let i = 0; i < MAX_SPARKLES; i++) {
      if (i >= count) {
        this.positions[i * 3] = 0;
        this.positions[i * 3 + 1] = 0;
        this.positions[i * 3 + 2] = -99999;
        continue;
      }
      const frac = ((i / count) + (t % 1)) % 1;
      const idxFloat = frac * (path.length - 1);
      const i0 = Math.floor(idxFloat);
      const i1 = Math.min(path.length - 1, i0 + 1);
      const alpha = idxFloat - i0;
      const cx = cellCenter(path[i0].x + (path[i1].x - path[i0].x) * alpha);
      const cy = cellCenter(path[i0].y + (path[i1].y - path[i0].y) * alpha);
      const jitter = Math.sin(t * 3 + i * 1.7) * 18;
      const bob = 20 + Math.abs(Math.sin(t * 4 + i)) * 28;
      const cz = sampleHeight(this.world.heights, cx, cy) + bob;
      this.positions[i * 3] = cx + jitter;
      this.positions[i * 3 + 1] = cy - jitter * 0.6;
      this.positions[i * 3 + 2] = cz;
    }
    this.pointGeom.attributes.position.needsUpdate = true;
    this.points.visible = true;

    const gx = cellCenter(goal.cell.x);
    const gy = cellCenter(goal.cell.y);
    const gz = sampleHeight(this.world.heights, gx, gy) + 14;
    const pulse = 55 + Math.sin(now * 0.006) * 15;
    this.destRing.position.set(gx, gy, gz);
    this.destRing.scale.set(pulse, pulse, 1);
    this.destRing.visible = true;
  }

  dispose(): void {
    this.world.scene.remove(this.points);
    this.world.scene.remove(this.destRing);
    this.world.scene.remove(this.rangeRing);
    this.pointGeom.dispose();
    this.pointMat.dispose();
    this.destRing.geometry.dispose();
    (this.destRing.material as THREE.Material).dispose();
    this.rangeRing.geometry.dispose();
    (this.rangeRing.material as THREE.Material).dispose();
  }
}

function makeRing(color: number, opacity: number): THREE.LineLoop {
  const pts: THREE.Vector3[] = [];
  for (let i = 0; i < RING_SEGMENTS; i++) {
    const a = (i / RING_SEGMENTS) * Math.PI * 2;
    pts.push(new THREE.Vector3(Math.cos(a), Math.sin(a), 0));
  }
  const geom = new THREE.BufferGeometry().setFromPoints(pts);
  const mat = new THREE.LineBasicMaterial({ color, transparent: true, opacity, depthWrite: false });
  const loop = new THREE.LineLoop(geom, mat);
  loop.frustumCulled = false;
  loop.visible = false;
  return loop;
}
