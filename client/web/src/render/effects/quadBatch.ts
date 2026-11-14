// Draws the effect system's quads: billboards (RenderSprite in Render/Textures/ZzzOpenglUtil.cpp,
// used by sprites and particles), joint trail faces (DescribeJoint in ZzzEffectJoint.cpp) and
// ground decals (RenderTerrainAlphaBitmap in Render/Terrain/ZzzLodTerrain.cpp). Quads are
// collected every tick and drawn in one mesh per texture and blend, like the desktop's batches.

import * as THREE from 'three';
import { effectTexture } from './core.js';
import type { BitmapName } from './core.js';

/** The desktop's blend states the ported effects use. */
export type Blend = 'glow' | 'alpha';

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

/** The ground height under a point. */
export type HeightAt = (x: number, y: number) => number;

export class QuadBatches {
  private readonly root: THREE.Object3D;
  private readonly batches = new Map<string, Batch>();
  /** The camera's right and up axes in the effect root's (world units) space, set by beginFrame. */
  private readonly right = new THREE.Vector3(1, 0, 0);
  private readonly up = new THREE.Vector3(0, 0, 1);
  private readonly corner = new THREE.Vector3();

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

  /** Uploads the quads collected since beginFrame. */
  endFrame(): void {
    for (const batch of this.batches.values()) {
      const quads = batch.positions.length / 12;
      if (quads > batch.capacity) this.grow(batch, quads);
      const geometry = batch.mesh.geometry;
      (geometry.getAttribute('position').array as Float32Array).set(batch.positions);
      (geometry.getAttribute('uv').array as Float32Array).set(batch.uvs);
      (geometry.getAttribute('color').array as Float32Array).set(batch.colors);
      geometry.getAttribute('position').needsUpdate = true;
      geometry.getAttribute('uv').needsUpdate = true;
      geometry.getAttribute('color').needsUpdate = true;
      geometry.setDrawRange(0, quads * 6);
      batch.mesh.visible = quads > 0;
    }
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
    const points: THREE.Vector3[] = [];
    for (let i = 0; i < 4; i++) {
      const x = dx[i] * cos - dy[i] * sin, y = dx[i] * sin + dy[i] * cos;
      points.push(this.corner.copy(position).addScaledVector(this.right, x).addScaledVector(this.up, y).clone());
    }
    this.pushQuad(batch, points, cu, cv, light);
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
    const at = (i: number, j: number): THREE.Vector3 => {
      const a = (i / grid - 0.5) * world, b = (j / grid - 0.5) * world;
      const px = x + a * cos - b * sin, py = y + a * sin + b * cos;
      return new THREE.Vector3(px, py, (heightAt ? heightAt(px, py) : groundZ) + DECAL_LIFT);
    };
    for (let j = 0; j < grid; j++) {
      for (let i = 0; i < grid; i++) {
        this.pushQuad(batch, [at(i, j), at(i + 1, j), at(i + 1, j + 1), at(i, j + 1)],
          [i / grid, (i + 1) / grid, (i + 1) / grid, i / grid], [j / grid, j / grid, (j + 1) / grid, (j + 1) / grid], light);
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
      const material = new THREE.MeshBasicMaterial({
        map: texture.texture, vertexColors: true, transparent: true, depthWrite: false, side: THREE.DoubleSide,
        blending: blend === 'glow' ? THREE.AdditiveBlending : THREE.NormalBlending,
      });
      if (blend === 'alpha') material.alphaTest = 0.25;
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
