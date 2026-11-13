// The creation values of Data/Effects/EffectTypes.json, applied as the desktop's
// ApplyCreateParams does (Render/Effects/EffectRegistry.cpp): first the values, then the
// offsets, then the copies; a row's variant for the SubType goes on top of the row.

import * as THREE from 'three';
import type { Effect } from './types.js';

type JsonVector = number[] | { x?: number; y?: number; z?: number };

interface CreateRow {
  subType?: number;
  subTypes?: number[];
  lifeTime?: number;
  scale?: number;
  velocity?: number;
  gravity?: number;
  hiddenMesh?: number;
  blendMesh?: number;
  blendMeshLight?: number;
  alpha?: number;
  light?: number[];
  position?: JsonVector;
  angle?: JsonVector;
  direction?: JsonVector;
  startPosition?: JsonVector;
  offset?: { lifeTime?: number; position?: JsonVector; angle?: JsonVector; startPosition?: JsonVector };
  copy?: { direction?: string; startPosition?: string; scale?: string };
  variants?: CreateRow[];
}

interface CatalogueFile {
  types: { name: string; create?: CreateRow }[];
}

/** What the CreateEffect call passed, for the catalogue's copies. */
export interface CreateCall {
  position: THREE.Vector3;
  angle: THREE.Vector3;
  light: THREE.Vector3;
  scale: number;
}

export class EffectCatalogue {
  private readonly rows = new Map<string, CreateRow>();

  static async load(url = '/data/Effects/EffectTypes.json'): Promise<EffectCatalogue> {
    const catalogue = new EffectCatalogue();
    try {
      const response = await fetch(url);
      if (!response.ok) throw new Error(`${response.status} ${url}`);
      const file = await response.json() as CatalogueFile;
      for (const type of file.types) if (type.create) catalogue.rows.set(type.name, type.create);
    } catch (error) {
      console.warn('Effect catalogue not loaded', (error as Error).message);
    }
    return catalogue;
  }

  /** Whether the type's creation comes from the catalogue (the desktop then skips its switch). */
  has(name: string): boolean {
    return this.rows.has(name);
  }

  /** Applies the type's row (and its SubType's variant) to a freshly initialised effect. */
  apply(name: string, effect: Effect, call: CreateCall): void {
    const row = this.rows.get(name);
    if (!row) return;
    const variant = row.variants?.find((v) => v.subType === effect.subType || v.subTypes?.includes(effect.subType));
    if (row.variants && !variant && row.variants.length > 0 && !hasValues(row)) return;
    for (const part of variant ? [row, variant] : [row]) applyValues(part, effect);
    for (const part of variant ? [row, variant] : [row]) applyOffsets(part, effect);
    for (const part of variant ? [row, variant] : [row]) applyCopies(part, effect, call);
  }
}

function hasValues(row: CreateRow): boolean {
  return Object.keys(row).some((key) => key !== 'variants');
}

function setVector(target: THREE.Vector3, value: JsonVector | undefined): void {
  if (!value) return;
  if (Array.isArray(value)) {
    target.set(value[0] ?? target.x, value[1] ?? target.y, value[2] ?? target.z);
    return;
  }
  if (value.x !== undefined) target.x = value.x;
  if (value.y !== undefined) target.y = value.y;
  if (value.z !== undefined) target.z = value.z;
}

function addVector(target: THREE.Vector3, value: JsonVector | undefined): void {
  if (!value) return;
  if (Array.isArray(value)) {
    target.x += value[0] ?? 0;
    target.y += value[1] ?? 0;
    target.z += value[2] ?? 0;
    return;
  }
  target.x += value.x ?? 0;
  target.y += value.y ?? 0;
  target.z += value.z ?? 0;
}

function applyValues(row: CreateRow, o: Effect): void {
  if (row.lifeTime !== undefined) o.lifeTime = row.lifeTime;
  if (row.scale !== undefined) o.scale = row.scale;
  if (row.velocity !== undefined) o.velocity = row.velocity;
  if (row.gravity !== undefined) o.gravity = row.gravity;
  if (row.hiddenMesh !== undefined) o.hiddenMesh = row.hiddenMesh;
  if (row.blendMesh !== undefined) o.blendMesh = row.blendMesh;
  if (row.blendMeshLight !== undefined) o.blendMeshLight = row.blendMeshLight;
  if (row.alpha !== undefined) o.alpha = row.alpha;
  if (row.light) o.light.set(row.light[0], row.light[1], row.light[2]);
  setVector(o.position, row.position);
  setVector(o.angle, row.angle);
  setVector(o.direction, row.direction);
  setVector(o.startPosition, row.startPosition);
}

function applyOffsets(row: CreateRow, o: Effect): void {
  if (!row.offset) return;
  if (row.offset.lifeTime !== undefined) o.lifeTime += row.offset.lifeTime;
  addVector(o.position, row.offset.position);
  addVector(o.angle, row.offset.angle);
}

function applyCopies(row: CreateRow, o: Effect, call: CreateCall): void {
  const copy = row.copy;
  if (copy) {
    if (copy.direction === 'light') o.direction.copy(o.light);
    else if (copy.direction === 'callAngle') o.direction.copy(call.angle);
    if (copy.startPosition === 'position') o.startPosition.copy(o.position);
    else if (copy.startPosition === 'light') o.startPosition.copy(o.light);
    else if (copy.startPosition === 'callPosition') o.startPosition.copy(call.position);
    if (copy.scale === 'callScale') o.scale = call.scale;
  }
  addVector(o.startPosition, row.offset?.startPosition);
}
