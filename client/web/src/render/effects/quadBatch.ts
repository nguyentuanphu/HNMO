// Draws the effect system's quads: billboards (RenderSprite in Render/Textures/ZzzOpenglUtil.cpp,
// used by sprites and particles), joint trail faces (DescribeJoint in ZzzEffectJoint.cpp) and
// ground decals (RenderTerrainAlphaBitmap in Render/Terrain/ZzzLodTerrain.cpp). Quads are
// collected every tick and drawn in one mesh per texture and blend, like the desktop's batches.

import * as THREE from 'three';
import { effectTexture } from './core.js';
import type { BitmapName } from './core.js';

/**
 * The desktop's blend states the ported effects use: glow (ONE, ONE), alpha (the alpha tested
 * TGA textures), subtract (ZERO, ONE_MINUS_SRC_COLOR: darkens), blend2 (ONE_MINUS_SRC_COLOR, ONE)
 * and blend3 (SRC_ALPHA, ONE_MINUS_SRC_ALPHA).
 */
export type Blend = 'glow' | 'alpha' | 'subtract' | 'blend2' | 'blend3';

/** Sets a material's blend state to the desktop's. */
export function applyBlend(material: THREE.Material, blend: Blend): void {
  material.transparent = true;
  material.depthWrite = false;
  switch (blend) {
    case 'glow': material.blending = THREE.AdditiveBlending; break;
    case 'alpha': material.blending = THREE.NormalBlending; material.alphaTest = 0.25; break;
    case 'blend3': material.blending = THREE.NormalBlending; break;
    case 'subtract':
    case 'blend2':
      material.blending = THREE.CustomBlending;
      material.blendEquation = THREE.AddEquation;
      material.blendSrc = blend === 'subtract' ? THREE.ZeroFactor : THREE.OneMinusSrcColorFactor;
      material.blendDst = blend === 'subtract' ? THREE.OneMinusSrcColorFactor : THREE.OneFactor;
      break;
  }
}

interface Batch {
  mesh: THREE.Mesh<THREE.BufferGeometry, THREE.MeshBasicMaterial>;
  positions: number[];
  uvs: number[];
  colors: number[];
  capacity: number;
}

const INITIAL_QUADS = 64;
const DECAL_LIFT = 1;
const TERRAIN_SCALE = 100;
const c0 = new THREE.Vector3();
const c1 = new THREE.Vector3();
const c2 = new THREE.Vector3();
const c3 = new THREE.Vector3();
const quadCorners = [c0, c1, c2, c3];

/** The ground height under a point. */
export type HeightAt = (x: number, y: number) => number;

export class QuadBatches {
  private readonly root: THREE.Object3D;
  private readonly batches = new Map<string, Batch>();
  /** The camera's right and up axes in the effect root's (world units) space, set by beginFrame. */
  private readonly right = new THREE.Vector3(1, 0, 0);
  private readonly up = new THREE.Vector3(0, 0, 1);

  constructor(root: THREE.Object3D) {
    this.root = root;
  }

  /** Empties every batch and takes the camera's axes for this frame's billboards. */
  beginFrame(camera: THREE.Camera | null): void {
    for (const batch of this.batches.values()) {
      batch.positions.length = 0;
      batch.uvs.length = 0;
      batch.colors.length = 0;
    }
    if (!camera) return;
    camera.updateMatrixWorld();
    this.root.updateMatrixWorld();
    const toRoot = new THREE.Quaternion();
    this.root.getWorldQuaternion(toRoot).invert();
    this.right.setFromMatrixColumn(camera.matrixWorld, 0).applyQuaternion(toRoot).normalize();
    this.up.setFromMatrixColumn(camera.matrixWorld, 1).applyQuaternion(toRoot).normalize();
  }

  /** Uploads the quads collected since beginFrame (only the used part of each buffer; nothing for an idle batch). */
  endFrame(): void {
    for (const batch of this.batches.values()) {
      const quads = batch.positions.length / 12;
      batch.mesh.visible = quads > 0;
      if (quads === 0) continue;
      if (quads > batch.capacity) this.grow(batch, quads);
      const geometry = batch.mesh.geometry;
      const upload = (name: string, values: number[]): void => {
        const attribute = geometry.getAttribute(name) as THREE.BufferAttribute;
        (attribute.array as Float32Array).set(values);
        attribute.clearUpdateRanges();
        attribute.addUpdateRange(0, values.length);
        attribute.needsUpdate = true;
      };
      upload('position', batch.positions);
      upload('uv', batch.uvs);
      upload('color', batch.colors);
      geometry.setDrawRange(0, quads * 6);
    }
  }

  /** Makes the batch of each bitmap and blend now (its material and buffers), so the first effect that draws one doesn't. */
  prepare(bitmap: BitmapName, blend: Blend): THREE.Mesh | null {
    return this.batch(bitmap, blend)?.mesh ?? null;
  }

  /** The texture's size in pixels, or null while it loads. */
  textureSize(bitmap: BitmapName): { width: number; height: number } | null {
    const texture = effectTexture(bitmap);
    return texture ? { width: texture.width, height: texture.height } : null;
  }

  /**
   * RenderSprite: a quad facing the camera, width x height world units around the position,
   * turned by rotation degrees, showing the texture rectangle (u, v, uWidth, vHeight).
   */
  billboard(bitmap: BitmapName, blend: Blend, position: THREE.Vector3, width: number, height: number,
    light: THREE.Vector3, rotation = 0, u = 0, v = 0, uWidth = 1, vHeight = 1): void {
    const batch = this.batch(bitmap, blend);
    if (!batch) return;
    const w = width * 0.5, h = height * 0.5;
    const radians = rotation * Math.PI / 180;
    const cos = Math.cos(radians), sin = Math.sin(radians);
    // Corners as in RenderSprite: p[0] bottom left ... p[3] top left; v grows downwards.
    const dx = [-w, w, w, -w], dy = [-h, -h, h, h];
    const cu = [u, u + uWidth, u + uWidth, u], cv = [v + vHeight, v + vHeight, v, v];
    for (let i = 0; i < 4; i++) {
      const x = dx[i] * cos - dy[i] * sin, y = dx[i] * sin + dy[i] * cos;
      quadCorners[i].copy(position).addScaledVector(this.right, x).addScaledVector(this.up, y);
    }
    this.pushQuad(batch, quadCorners, cu, cv, light);
  }

  /** One textured quad with its four corners and texture coordinates (a joint face). */
  quad(bitmap: BitmapName, blend: Blend, corners: THREE.Vector3[], us: number[], vs: number[], light: THREE.Vector3): void {
    const batch = this.batch(bitmap, blend);
    if (batch) this.pushQuad(batch, corners, us, vs, light);
  }

  /**
   * RenderTerrainAlphaBitmap: the texture laid on the ground, `size` terrain cells wide around
   * (x, y), turned by rotation degrees.
   */
  decal(bitmap: BitmapName, blend: Blend, x: number, y: number, size: number, light: THREE.Vector3,
    rotation: number, heightAt: HeightAt | null, groundZ: number, grid = 4): void {
    const batch = this.batch(bitmap, blend);
    if (!batch) return;
    const radians = rotation * Math.PI / 180;
    const cos = Math.cos(radians), sin = Math.sin(radians);
    const world = size * TERRAIN_SCALE;
    const setCorner = (out: THREE.Vector3, i: number, j: number): void => {
      const a = (i / grid - 0.5) * world, b = (j / grid - 0.5) * world;
      const px = x + a * cos - b * sin, py = y + a * sin + b * cos;
      out.set(px, py, (heightAt ? heightAt(px, py) : groundZ) + DECAL_LIFT);
    };
    const us = [0, 0, 0, 0];
    const vs = [0, 0, 0, 0];
    for (let j = 0; j < grid; j++) {
      const v0 = j / grid, v1 = (j + 1) / grid;
      vs[0] = v0; vs[1] = v0; vs[2] = v1; vs[3] = v1;
      for (let i = 0; i < grid; i++) {
        const u0 = i / grid, u1 = (i + 1) / grid;
        us[0] = u0; us[1] = u1; us[2] = u1; us[3] = u0;
        setCorner(c0, i, j);
        setCorner(c1, i + 1, j);
        setCorner(c2, i + 1, j + 1);
        setCorner(c3, i, j + 1);
        this.pushQuad(batch, quadCorners, us, vs, light);
      }
    }
  }

  dispose(): void {
    for (const batch of this.batches.values()) {
      batch.mesh.removeFromParent();
      batch.mesh.geometry.dispose();
      batch.mesh.material.dispose();
    }
    this.batches.clear();
  }

  private pushQuad(batch: Batch, corners: THREE.Vector3[], us: number[], vs: number[], light: THREE.Vector3): void {
    for (let i = 0; i < 4; i++) {
      batch.positions.push(corners[i].x, corners[i].y, corners[i].z);
      batch.uvs.push(us[i], vs[i]);
      batch.colors.push(light.x, light.y, light.z);
    }
  }

  private batch(bitmap: BitmapName, blend: Blend): Batch | null {
    const texture = effectTexture(bitmap);
    if (!texture) return null;
    const key = `${bitmap}|${blend}`;
    let batch = this.batches.get(key);
    if (!batch) {
      const material = new THREE.MeshBasicMaterial({ map: texture.texture, vertexColors: true, side: THREE.DoubleSide });
      applyBlend(material, blend);
      const mesh = new THREE.Mesh(new THREE.BufferGeometry(), material);
      mesh.frustumCulled = false;
      mesh.renderOrder = 2;
      batch = { mesh, positions: [], uvs: [], colors: [], capacity: 0 };
      this.grow(batch, INITIAL_QUADS);
      this.root.add(mesh);
      this.batches.set(key, batch);
    }
    return batch;
  }

  private grow(batch: Batch, quads: number): void {
    let capacity = Math.max(batch.capacity, INITIAL_QUADS);
    while (capacity < quads) capacity *= 2;
    const geometry = batch.mesh.geometry;
    geometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(capacity * 12), 3).setUsage(THREE.DynamicDrawUsage));
    geometry.setAttribute('uv', new THREE.BufferAttribute(new Float32Array(capacity * 8), 2).setUsage(THREE.DynamicDrawUsage));
    geometry.setAttribute('color', new THREE.BufferAttribute(new Float32Array(capacity * 12), 3).setUsage(THREE.DynamicDrawUsage));
    const index = new Uint32Array(capacity * 6);
    for (let q = 0; q < capacity; q++) index.set([q * 4, q * 4 + 1, q * 4 + 2, q * 4, q * 4 + 2, q * 4 + 3], q * 6);
    geometry.setIndex(new THREE.BufferAttribute(index, 1));
    batch.capacity = capacity;
  }
}
