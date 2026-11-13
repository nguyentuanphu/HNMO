// Draws items as the desktop client shows them in its windows (RenderItem3D / RenderObjectScreen):
// the item's own 3D model, turned by its inventory display rotation (Data/Items/Models,
// "inventory": rotation in degrees, x then y then z like AngleMatrix), lit plain white, with its
// level, excellent and ancient glow, fitted into the item's cells. A hovered item turns.

import * as THREE from 'three';
import { itemModel } from '../game/items.js';
import { createItemModel } from '../game/characters.js';
import type { AnimatedModel } from '../bmd.js';
import type { Item } from '../game/inventory.js';

const DEG = Math.PI / 180;
const TURN_DEGREES_PER_SECOND = 90;
const FILL = 0.85; // the share of the box the model's larger side takes

let renderer: THREE.WebGLRenderer | null = null;
const models = new Map<string, Promise<AnimatedModel | null>>(); // item key -> model
const white = new THREE.Color(1, 1, 1);

function itemKey(item: Item): string {
  return `${item.group}:${item.number}:${item.level ?? 0}:${item.excellent ? 1 : 0}:${item.ancientSet ? 1 : 0}`;
}

function getRenderer(): THREE.WebGLRenderer {
  if (!renderer) {
    renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true, preserveDrawingBuffer: true });
    renderer.setClearColor(0x000000, 0);
    renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
  }
  return renderer;
}

async function modelOf(item: Item): Promise<AnimatedModel | null> {
  const key = itemKey(item);
  if (!models.has(key)) {
    models.set(key, createItemModel({
      group: item.group, number: item.number,
      glowLevel: item.level ?? 0, excellent: Boolean(item.excellent), ancient: Boolean(item.ancientSet),
    }).catch((error: Error) => { console.warn('Item model', key, error.message); return null; }));
  }
  return models.get(key)!;
}

// Draws the item into the canvas (sized in CSS pixels as the item's box); spin: seconds of turning.
export async function drawItem(canvas: HTMLCanvasElement, item: Item, { spin = null as number | null, time = 0 } = {}): Promise<boolean> {
  const model = await modelOf(item);
  const context = canvas.getContext('2d')!;
  context.clearRect(0, 0, canvas.width, canvas.height);
  if (!model) return false;
  const display = itemModel(item.group, item.number)?.inventory;
  const [rx, ry, rz] = display?.rotation ?? [0, 0, 0];
  const scene = new THREE.Scene();
  const holder = new THREE.Group();
  holder.add(model.group);
  scene.add(holder);
  model.pose(0, 0);
  model.setLight(white, time);
  model.group.rotation.set(rx * DEG, (spin === null ? ry : ry + spin * TURN_DEGREES_PER_SECOND) * DEG, rz * DEG, 'ZYX');
  model.group.position.set(0, 0, 0);
  model.group.updateMatrixWorld(true);

  // Fit the turned model into the box, centred.
  const box = new THREE.Box3();
  for (const part of model.parts) {
    part.mesh.geometry.computeBoundingBox();
    box.union(part.mesh.geometry.boundingBox!.clone().applyMatrix4(part.mesh.matrixWorld));
  }
  const size = box.getSize(new THREE.Vector3());
  const center = box.getCenter(new THREE.Vector3());
  const aspect = canvas.width / canvas.height;
  const half = Math.max(size.x / aspect, size.y) / 2 / FILL || 1;
  const camera = new THREE.OrthographicCamera(-half * aspect, half * aspect, half, -half, -10000, 10000);
  camera.position.set(center.x, center.y, center.z + 5000);
  camera.lookAt(center);

  const gl = getRenderer();
  gl.setSize(canvas.width, canvas.height, false);
  gl.render(scene, camera);
  context.drawImage(gl.domElement, 0, 0);
  holder.remove(model.group);
  return true;
}
