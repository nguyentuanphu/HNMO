// Draws items as the desktop client shows them in its windows (RenderItem3D / RenderObjectScreen): the
// item's own 3D model under the item camera (UI::Items::DrawUnderItemCamera: a 1 degree field of view
// over the whole window, so practically flat), placed by its inventory display of Data/Items/Models:
// its origin on the box's "anchor" point moved by "offset", turned by "rotation" (degrees, x then y then
// z like AngleMatrix) and drawn at "scale", lit plain white (LightEnable off, light 1), with its level,
// excellent and ancient glow. The size doesn't depend on the box: an item is as big in an equipment slot
// as in the grid. A hovered item turns.

import * as THREE from 'three';
import { itemModel, type InventoryDisplay } from '../game/items.js';
import { createItemModel } from '../game/characters.js';
import type { AnimatedModel } from '../bmd.js';
import type { Item } from '../game/inventory.js';
import { REFERENCE_HEIGHT, typographyScale } from './nativeText.js';

const DEG = Math.PI / 180;
/** SelectedItemTurnSpeed: 0.45 degrees per millisecond while the pointer is on the item. */
const TURN_DEGREES_PER_SECOND = 450;
const FILL = 0.85; // fitted drawing (no display scale known): the share of the box the model's larger side takes

// ItemInventoryDisplay's defaults.
const DEFAULT_ANCHOR: [number, number] = [0.5, 0.6];
const DEFAULT_ROTATION: [number, number, number] = [270, -10, 0];
const DEFAULT_SCALE = 0.0025;
/** The item camera: the item stands at a tenth of RENDER_ITEMVIEW_FAR (2000) in front of it, 1 degree field of view. */
const ITEM_DEPTH = 200;
const ITEM_FIELD_OF_VIEW = 1;

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
    }, 'inventory').then((model) => { if (model) keepGlowSeeThrough(model.group); return model; })
      .catch((error: Error) => { console.warn('Item model', key, error.message); return null; }));
  }
  return models.get(key)!;
}

/**
 * An additive drawing adds light to what is behind it; on the icon's clear canvas that would leave its black
 * parts opaque black. Drawn here, such a drawing adds its colour and covers as much as its brightest channel,
 * so a black part stays see-through and the icon looks the same over any background.
 */
function keepGlowSeeThrough(root: THREE.Object3D): void {
  root.traverse((object) => {
    if (!(object instanceof THREE.Mesh)) return;
    const materials: THREE.Material[] = Array.isArray(object.material) ? object.material : [object.material];
    for (const material of materials) {
      if (material.blending !== THREE.AdditiveBlending) continue;
      Object.assign(material, {
        blending: THREE.CustomBlending, blendEquation: THREE.AddEquation,
        blendSrc: THREE.OneFactor, blendDst: THREE.OneFactor,
        blendSrcAlpha: THREE.OneFactor, blendDstAlpha: THREE.OneMinusSrcAlphaFactor,
      });
      material.onBeforeCompile = (shader) => {
        const end = shader.fragmentShader.lastIndexOf('}');
        shader.fragmentShader = `${shader.fragmentShader.slice(0, end)}
  gl_FragColor.a = clamp(max(max(gl_FragColor.r, gl_FragColor.g), gl_FragColor.b) * gl_FragColor.a, 0.0, 1.0);
}`;
      };
      material.needsUpdate = true;
    }
  });
}

/**
 * The item camera's reference pixels per world unit: the window's height in reference pixels over the
 * height the 1 degree field of view covers at the item's depth.
 */
export function itemPixelsPerUnit(): number {
  const referenceHeight = window.innerHeight / typographyScale() || REFERENCE_HEIGHT;
  return referenceHeight / (2 * ITEM_DEPTH * Math.tan(ITEM_FIELD_OF_VIEW / 2 * DEG));
}

export interface DrawItemOptions {
  /** Seconds the item has been turning (the pointer is on it); null: still. */
  spin?: number | null;
  time?: number;
  /**
   * The canvas's pixels per reference pixel (the panel's scale): the item is drawn at the desktop's size
   * and place in its box. Without it the model is fitted into the canvas.
   */
  pixelsPerReference?: number;
}

// Draws the item into the canvas (its box); spin: seconds of turning.
export async function drawItem(canvas: HTMLCanvasElement, item: Item, { spin = null, time = 0, pixelsPerReference }: DrawItemOptions = {}): Promise<boolean> {
  const model = await modelOf(item);
  const context = canvas.getContext('2d')!;
  context.clearRect(0, 0, canvas.width, canvas.height);
  if (!model) return false;
  const display: InventoryDisplay = itemModel(item.group, item.number)?.inventory ?? {};
  const [rx, ry, rz] = display.rotation ?? DEFAULT_ROTATION;
  const scene = new THREE.Scene();
  const holder = new THREE.Group();
  holder.add(model.group);
  scene.add(holder);
  model.pose(0, 0);
  model.setLight(white, time);
  // While the pointer is on it the item turns around its y axis (RenderObjectScreen's angle[1]).
  model.group.rotation.set(rx * DEG, (spin === null ? ry : spin * TURN_DEGREES_PER_SECOND) * DEG, rz * DEG, 'ZYX');

  let camera: THREE.OrthographicCamera;
  if (pixelsPerReference && pixelsPerReference > 0) {
    // The box in reference pixels and the model's origin on its anchor, moved by the offset (world units).
    const boxWidth = canvas.width / pixelsPerReference;
    const boxHeight = canvas.height / pixelsPerReference;
    const [anchorX, anchorY] = display.anchor ?? DEFAULT_ANCHOR;
    const [offsetX = 0, offsetY = 0, offsetZ = 0] = (display.offset ?? []) as number[];
    const scale = display.scale ?? DEFAULT_SCALE;
    model.group.scale.setScalar(scale);
    // Armour stands at its height on the character's skeleton (BMD BodyHeight: the root bone's z).
    const body = new THREE.Vector3(0, 0, (display.bodyHeight ?? 0) * scale).applyEuler(model.group.rotation);
    model.group.position.set(offsetX + body.x, offsetY + body.y, offsetZ + body.z);
    model.group.updateMatrixWorld(true);
    const unit = itemPixelsPerUnit();
    camera = new THREE.OrthographicCamera(
      -anchorX * boxWidth / unit, (1 - anchorX) * boxWidth / unit,
      anchorY * boxHeight / unit, -(1 - anchorY) * boxHeight / unit, -ITEM_DEPTH, ITEM_DEPTH);
    camera.position.set(0, 0, 0);
    camera.lookAt(0, 0, -1);
  } else {
    model.group.scale.setScalar(1);
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
    camera = new THREE.OrthographicCamera(-half * aspect, half * aspect, half, -half, -10000, 10000);
    camera.position.set(center.x, center.y, center.z + 5000);
    camera.lookAt(center);
  }

  const gl = getRenderer();
  gl.setSize(canvas.width, canvas.height, false);
  gl.render(scene, camera);
  context.drawImage(gl.domElement, 0, 0);
  holder.remove(model.group);
  model.group.scale.setScalar(1);
  model.group.position.set(0, 0, 0);
  return true;
}
