// Object blurs: the streaks an effect leaves behind a moving part of it (a flame's edge, a creature's blade), as the
// desktop's ObjectBlur records (CreateObjectBlur, AddObjectBlur, MoveObjectBlurs, RenderObjectBlurs, RemoveObjectBlurs
// in Render/Effects/ZzzEffectBlurSpark.cpp). An effect adds two points (the edge) a few times a tick; every tick the
// samples age, the oldest falls off, and a blur ends when its life time runs out (30 ticks, 15 when short).
//
// The records are pooled (built once, reused) and drawn into the effect system's quad batches: no allocation per tick.

import * as THREE from 'three';
import type { BitmapName } from './core.js';
import type { EffectLayer } from './effectSystem.js';
import type { QuadBatches } from './quadBatch.js';

/** MAX_OBJECT_BLUR_TAILS (the desktop keeps 600; a blur here never holds more than its life's samples). */
const MAX_TAILS = 600;
/** MAX_OBJECT_BLUR_LIFETIME. */
const LIFETIME = 30;
const SHORT_LIFETIME = 15;

/** The texture of a blur Type (BITMAP_BLUR + Type; 3 BITMAP_BLUR2, 4 BITMAP_BLUR, 5 BITMAP_LAVA). */
const TYPE_TEXTURES: Record<number, BitmapName> = { 0: 'blur1', 1: 'blur2', 2: 'blur3', 4: 'blur1', 5: 'lava' };

interface ObjectBlur {
  live: boolean;
  owner: object | null;
  subType: number;
  type: number;
  lifeTime: number;
  number: number;
  light: THREE.Vector3;
  p1: THREE.Vector3[];
  p2: THREE.Vector3[];
}

export class ObjectBlurs implements EffectLayer {
  private readonly blurs: ObjectBlur[] = [];
  private readonly colour = new THREE.Vector3();

  /**
   * CreateObjectBlur: adds the edge p1-p2 to the owner's blur of that SubType (any of the owner's when SubType is 0),
   * or starts one.
   */
  add(owner: object, p1: THREE.Vector3, p2: THREE.Vector3, light: THREE.Vector3, type: number, short = false, subType = 0): void {
    let blur = this.blurs.find((b) => b.live && b.owner === owner && (subType <= 0 || b.subType === subType));
    if (!blur) {
      blur = this.blurs.find((b) => !b.live);
      if (!blur) {
        blur = { live: false, owner: null, subType: 0, type: 0, lifeTime: 0, number: 0, light: new THREE.Vector3(), p1: [], p2: [] };
        this.blurs.push(blur);
      }
      blur.live = true;
      blur.owner = owner;
      blur.subType = subType;
      blur.number = 0;
      blur.lifeTime = short ? SHORT_LIFETIME : LIFETIME;
    }
    blur.type = type;
    blur.light.copy(light);
    // AddBlurSample: the samples move back one and the new one goes first.
    const count = Math.min(blur.number, MAX_TAILS - 2);
    for (let i = count; i > 0; i--) {
      const p = blur.p1[i] ?? (blur.p1[i] = new THREE.Vector3());
      const q = blur.p2[i] ?? (blur.p2[i] = new THREE.Vector3());
      p.copy(blur.p1[i - 1]);
      q.copy(blur.p2[i - 1]);
    }
    (blur.p1[0] ?? (blur.p1[0] = new THREE.Vector3())).copy(p1);
    (blur.p2[0] ?? (blur.p2[0] = new THREE.Vector3())).copy(p2);
    blur.number = Math.min(blur.number + 1, MAX_TAILS - 1);
  }

  /** RemoveObjectBlurs: ends the owner's blurs (of that SubType, or all of them for 0). */
  remove(owner: object, subType = 0): void {
    for (const blur of this.blurs) if (blur.live && blur.owner === owner && (subType <= 0 || blur.subType === subType)) blur.live = false;
  }

  /** MoveObjectBlurs: once per tick the samples age and the oldest falls off. */
  tick(): void {
    for (const blur of this.blurs) {
      if (!blur.live) continue;
      blur.lifeTime--;
      blur.number = Math.max(blur.number - 1, 0);
      if (blur.lifeTime <= 0) {
        blur.number = 0;
        blur.live = false;
        blur.owner = null;
        continue;
      }
      for (let i = blur.number - 1; i >= 0; i--) {
        (blur.p1[i + 1] ?? (blur.p1[i + 1] = new THREE.Vector3())).copy(blur.p1[i]);
        (blur.p2[i + 1] ?? (blur.p2[i + 1] = new THREE.Vector3())).copy(blur.p2[i]);
      }
    }
  }

  frame(): void { /* the samples come with the effects' ticks */ }

  /** RenderObjectBlurs: glowing quads between the samples, fading toward the oldest. */
  draw(quads: QuadBatches): void {
    for (const blur of this.blurs) {
      if (!blur.live || blur.number < 2) continue;
      const texture = TYPE_TEXTURES[blur.type] ?? 'blur1';
      for (let j = 0; j < blur.number - 1; j++) {
        const l1 = (blur.number - j) / blur.number, l2 = (blur.number - (j + 1)) / blur.number;
        const u1 = j / blur.number, u2 = (j + 1) / blur.number;
        // The two samples' lights differ; the quad takes their mean (the batches colour per quad).
        this.colour.copy(blur.light).multiplyScalar((l1 + l2) / 2);
        quads.quad(texture, 'glow', [blur.p1[j], blur.p2[j], blur.p2[j + 1], blur.p1[j + 1]], [u1, u1, u2, u2], [1, 0, 0, 1], this.colour);
      }
    }
  }

  /** How many blurs are drawn (for tests). */
  get count(): number {
    return this.blurs.filter((blur) => blur.live).length;
  }

  clear(): void {
    for (const blur of this.blurs) {
      blur.live = false;
      blur.owner = null;
    }
  }
}
