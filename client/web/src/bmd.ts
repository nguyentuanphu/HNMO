// Turns parsed BMD models into three.js geometry and materials.
// Bone math follows the client: each key's rotation is an Euler angle in radians
// turned into a quaternion as in AngleQuaternion (Core/Math), which matches
// three.js' 'ZYX' Euler order; a bone's matrix is its parent's times its own.

import * as THREE from 'three';
import { KTX2Loader } from 'three/examples/jsm/loaders/KTX2Loader.js';
import { parseBMD, decodeTGA } from './formats.js';
import { getTextureQuality } from './ui/settingsWindow.js';
import type { BMDModel, BMDMesh, BMDBoneFrames } from './formats.js';
import { onSeasonChange, seasonDataUrl } from './dataRoot.js';

const ALPHA_TEST = 0.25;

const fileCache = new Map<string, Promise<Uint8Array>>();
const textureCache = new Map<string, Promise<THREE.Texture | null>>();
let ktx2Loader: KTX2Loader | null = null;
let dataBasePrefix = '';

/**
 * Sets an optional URL prefix for `/data/...` asset fetches (used when embedding the
 * renderer outside the web client's origin/root, such as `/world-editor` in AdminPanel).
 */
export function setDataBaseUrl(prefix: string): void {
  dataBasePrefix = prefix.replace(/\/+$/, '');
  fileCache.clear();
  textureCache.clear();
  modelCache.clear();
}

/** Resolves a `/data/...` asset URL: the season of the world being played, then the configured base prefix. */
export function resolveDataUrl(url: string): string {
  if (!url.startsWith('/data/')) return url;
  const seasonal = seasonDataUrl(url);
  return dataBasePrefix ? `${dataBasePrefix}${seasonal}` : seasonal;
}

// Another season's files have the same names: nothing loaded for one is shown in the other.
onSeasonChange(() => {
  fileCache.clear();
  textureCache.clear();
  modelCache.clear();
});

/** Initializes the KTX2 GPU-compressed texture transcoder for the active WebGL renderer. */
export function setKtx2Renderer(renderer: THREE.WebGLRenderer): void {
  ktx2Loader?.dispose();
  ktx2Loader = new KTX2Loader().setTranscoderPath('/vendor/basis/').detectSupport(renderer);
}

/** Clears the decoded texture cache (for example after changing texture quality). */
export function clearTextureCache(): void {
  textureCache.clear();
}

export async function fetchBytes(url: string): Promise<Uint8Array> {
  const resolved = resolveDataUrl(url);
  if (!fileCache.has(resolved)) {
    fileCache.set(resolved, fetch(resolved).then(async (response) => {
      if (!response.ok) throw new Error(`${response.status} ${resolved}`);
      return new Uint8Array(await response.arrayBuffer());
    }));
  }
  return fileCache.get(resolved)!;
}

// Parsed once per file: every player shares player.bmd (3 MB, ~25 ms to parse), so parsing it for each
// character that came into view froze the game for a moment (the hero stopped mid-walk). Nothing
// changes a parsed model; the geometry built from it is each character's own.
const modelCache = new Map<string, Promise<BMDModel>>();

export function loadModel(dir: string, fileName: string): Promise<BMDModel> {
  const url = `/data/${dir}/${fileName}`;
  let model = modelCache.get(url);
  if (!model) {
    model = fetchBytes(url).then(parseBMD);
    model.catch(() => modelCache.delete(url)); // a failed load may be tried again
    modelCache.set(url, model);
  }
  return model;
}

// Texture scripts in the file name (TextureScript.cpp): the letters after the first
// "_" can be R (bright), H (hidden mesh), S (stream mesh) and N (no blending).
// Hair is drawn as hair, not bright, like the client's IsHair textures.
interface TextureKind {
  isTga: boolean;
  additive: boolean;
  hidden: boolean;
}

// TextureScriptParsing::parsingTScriptA: reads up to 4 flag characters after '_' up to '.' ([RHSN] or DC/DT),
// aborting (clearing all flags) if any unexpected character appears.
export function textureKind(name: string): TextureKind {
  const lower = name.toLowerCase();
  const isTga = lower.endsWith('.tga');
  const underscore = name.indexOf('_');
  if (underscore < 0) return { isTga, additive: false, hidden: false };
  const after = name.slice(underscore + 1);
  const dot = after.indexOf('.');
  const script = dot >= 0 ? after.slice(0, dot) : after;
  const len = Math.min(script.length, 4);
  if (len === 0) return { isTga, additive: false, hidden: false };
  let bright = false;
  let hidden = false;
  let noneBlend = false;
  for (let i = 0; i < len; i++) {
    const ch = script[i];
    if (ch === 'R') {
      bright = true;
    } else if (ch === 'H') {
      hidden = true;
    } else if (ch === 'S' || ch === 'N') {
      if (ch === 'N') noneBlend = true;
    } else if (ch === 'D' && i + 1 < len && (script[i + 1] === 'C' || script[i + 1] === 'T')) {
      i++;
    } else {
      return { isTga, additive: false, hidden: false };
    }
  }
  const isHair = lower.startsWith('hair');
  return {
    isTga,
    additive: bright && !noneBlend && !isHair,
    hidden,
  };
}

async function decodeTexture(url: string, isTga: boolean): Promise<THREE.Texture> {
  const bytes = await fetchBytes(url);
  if (isTga) {
    const { width, height, pixels } = decodeTGA(bytes);
    const texture = new THREE.DataTexture(pixels, width, height, THREE.RGBAFormat);
    texture.needsUpdate = true;
    return texture;
  }
  const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/jpeg' }));
  const texture = new THREE.Texture(bitmap);
  texture.flipY = false;
  texture.needsUpdate = true;
  return texture;
}

async function tryLoadKtx2(url: string): Promise<THREE.Texture | null> {
  if (!ktx2Loader) return null;
  const ktx2Url = url.replace(/\.(ozj|ozt|jpe?g|tga)$/i, '.ktx2');
  if (ktx2Url === url) return null;
  try {
    const response = await fetch(resolveDataUrl(ktx2Url));
    if (!response.ok) return null;
    const blobUrl = URL.createObjectURL(await response.blob());
    try {
      const texture = await ktx2Loader.loadAsync(blobUrl);
      texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
      texture.flipY = false;
      texture.needsUpdate = true;
      return texture;
    } finally {
      URL.revokeObjectURL(blobUrl);
    }
  } catch {
    return null;
  }
}

function loadTextureFrom(dir: string, name: string): Promise<THREE.Texture | null> {
  const url = `/data/${dir}/${name}`;
  const quality = getTextureQuality();
  const cacheKey = `${quality}:${url}`;
  if (!textureCache.has(cacheKey)) {
    const { isTga } = textureKind(name);
    const load = (async (): Promise<THREE.Texture | null> => {
      if (quality === 'compressed') {
        const ktx2 = await tryLoadKtx2(url);
        if (ktx2) return ktx2;
      }
      const texture = await decodeTexture(url, isTga);
      texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
      texture.generateMipmaps = true;
      texture.minFilter = THREE.LinearMipmapLinearFilter;
      texture.magFilter = THREE.LinearFilter;
      return texture;
    })().catch(() => null);
    textureCache.set(cacheKey, load);
  }
  return textureCache.get(cacheKey)!;
}

// A texture comes from the first of the folders that has it (LoadTextureFromFolders).
export async function loadTexture(dirs: string | string[], name: string): Promise<THREE.Texture | null> {
  const folders = Array.isArray(dirs) ? dirs : [dirs];
  for (const dir of folders) {
    const texture = await loadTextureFrom(dir, name);
    if (texture) return texture;
  }
  console.warn('Texture missing:', name, 'in', folders.join(', '));
  return null;
}

// Meshes whose texture name starts with "hid" are never drawn (Data::Items::IsHiddenTexture).
const HIDDEN_TEXTURE_PREFIX = 'hid';

/** Whether a mesh with this texture is never drawn (a "hid…" name or the H texture script). */
export function isHiddenTexture(textureName: string): boolean {
  return textureKind(textureName).hidden || textureName.startsWith(HIDDEN_TEXTURE_PREFIX);
}

export interface MaterialOptions {
  /** Draw additively, like a texture whose name asks for it. */
  glow?: boolean;
}

export async function createMaterial(dir: string | string[], textureName: string, { glow = false }: MaterialOptions = {}): Promise<THREE.MeshBasicMaterial> {
  const { isTga, additive: additiveByName, hidden } = textureKind(textureName);
  const additive = glow || additiveByName;
  if (hidden || textureName.startsWith(HIDDEN_TEXTURE_PREFIX)) return new THREE.MeshBasicMaterial({ visible: false });
  const map = await loadTexture(dir, textureName);
  const material = new THREE.MeshBasicMaterial({ map, color: 0xffffff, side: THREE.DoubleSide });
  if (!map) material.color.set(0x808080);
  if (additive) {
    Object.assign(material, { blending: THREE.AdditiveBlending, transparent: true, depthWrite: false });
  } else if (isTga) {
    material.alphaTest = ALPHA_TEST;
  }
  return material;
}

// ---------- bones ----------

const tmpQuatA = new THREE.Quaternion();
const tmpQuatB = new THREE.Quaternion();
const tmpEuler = new THREE.Euler(0, 0, 0, 'ZYX');
const tmpPos = new THREE.Vector3();
const tmpPosB = new THREE.Vector3();
const unitScale = new THREE.Vector3(1, 1, 1);

function boneKey(frames: BMDBoneFrames, key: number, outPosition: THREE.Vector3, outQuaternion: THREE.Quaternion): void {
  const i = key * 3;
  outPosition.set(frames.positions[i], frames.positions[i + 1], frames.positions[i + 2]);
  tmpEuler.set(frames.rotations[i], frames.rotations[i + 1], frames.rotations[i + 2], 'ZYX');
  outQuaternion.setFromEuler(tmpEuler);
}

/**
 * The keys an action loops over. The desktop loops a locked action (the walks and runs) over one key less
 * (ZzzBMD.cpp, PlayAnimation: NumAnimationKeys - 1), its last key being where the cycle comes back to.
 */
export function loopKeyCount(model: BMDModel, actionIndex: number): number {
  const action = model.actions[actionIndex] ?? model.actions[0];
  const numKeys = Math.max(1, action?.numKeys || 1);
  return action?.lockPositions && numKeys > 1 ? numKeys - 1 : numKeys;
}

// World matrices of every bone for an action at a (fractional, looping) key.
export function computeBoneMatrices(model: BMDModel, actionIndex: number, time: number, out?: THREE.Matrix4[]): THREE.Matrix4[] {
  const action = model.actions[actionIndex] ? actionIndex : 0;
  const numKeys = Math.max(1, model.actions[action]?.numKeys || 1);
  const loopKeys = loopKeyCount(model, action);
  const locked = model.actions[action]?.lockPositions === true;
  const k0 = Math.floor(time) % loopKeys;
  // A locked action blends its key loopKeys - 1 into its last key (key 0's copy); others wrap round to key 0.
  const k1 = (k0 + 1) % numKeys;
  const t = time - Math.floor(time);
  const matrices = out || model.bones.map(() => new THREE.Matrix4());
  const resolved = new Uint8Array(model.bones.length);
  model.bones.forEach((bone, index) => {
    const local = matrices[index];
    if (bone.dummy || !bone.frames[action] || bone.frames[action].positions.length === 0) {
      local.identity();
    } else {
      const frames = bone.frames[action];
      boneKey(frames, k0, tmpPos, tmpQuatA);
      boneKey(frames, k1, tmpPosB, tmpQuatB);
      tmpPos.lerp(tmpPosB, t);
      tmpQuatA.slerp(tmpQuatB, t);
      // A locked root stays on key 0's x and y; only its height moves (ZzzBMD.cpp, Animation: lockedRoot).
      // Following the keys made the body slide away from where the character stands and snap back each loop.
      if (index === 0 && locked) tmpPos.set(frames.positions[0], frames.positions[1], tmpPos.z);
      local.compose(tmpPos, tmpQuatA, unitScale);
    }
  });
  const resolveBone = (index: number): void => {
    if (resolved[index] !== 0) return;
    resolved[index] = 1; // guard against cyclic parent chains
    const parent = model.bones[index].parent;
    if (parent >= 0 && parent < model.bones.length && parent !== index) {
      if (resolved[parent] === 0) resolveBone(parent);
      if (resolved[parent] === 2) matrices[index].premultiply(matrices[parent]);
    }
    resolved[index] = 2;
  };
  for (let i = 0; i < model.bones.length; i++) resolveBone(i);
  return matrices;
}

// ---------- geometry ----------

const TRIANGLE_FAN = [[0, 1, 2], [0, 2, 3]];

/** A mesh expanded to one entry per drawn corner, with each corner's bone and bind pose. */
export interface MeshSkin {
  nodes: Int16Array;
  local: Float32Array;
  normalNodes: Int16Array;
  localNormals: Float32Array;
  uvs: Float32Array;
}

/** One mesh of a model as three.js geometry (see buildGeometries). */
export interface GeometryPart {
  geometry: THREE.BufferGeometry;
  skin: MeshSkin;
  textureName: string;
  meshIndex: number;
}

/** The light multiplier of an item level at a time in seconds. */
export type LightFactor = (seconds: number) => THREE.Color;

/** A drawn part of an AnimatedModel. */
export interface ModelPart extends GeometryPart {
  mesh: THREE.Mesh<THREE.BufferGeometry, THREE.MeshBasicMaterial>;
  lightFactor?: LightFactor | null;
  /** The light its look draws it in (the item's render style), times the model's light. */
  tint?: THREE.Color;
}

/** Extra meshes drawn over a model's parts (game/itemGlow.js), updated every frame. */
export interface ModelGlow {
  meshes: THREE.Mesh[];
  update(seconds: number): void;
}

// Expands a mesh's triangles and quads into flat vertex arrays. Keeps each vertex's
// bone and bind position (and normal), so an animated model can be re-posed every frame.
function expandMesh(mesh: BMDMesh): MeshSkin {
  const nodes: number[] = [];
  const local: number[] = [];
  const normalNodes: number[] = [];
  const localNormals: number[] = [];
  const uvs: number[] = [];
  for (const tri of mesh.triangles) {
    const fans = tri.corners === 4 ? TRIANGLE_FAN : TRIANGLE_FAN.slice(0, 1);
    for (const fan of fans) {
      for (const corner of fan) {
        const vertex = mesh.vertices[tri.vertex[corner]];
        if (!vertex) continue;
        const uv = mesh.texCoords[tri.texCoord[corner]] || [0, 0];
        const normal = mesh.normals[tri.normal[corner]] || { node: vertex.node, normal: [0, 0, 1] };
        nodes.push(vertex.node);
        local.push(vertex.position[0], vertex.position[1], vertex.position[2]);
        normalNodes.push(normal.node);
        localNormals.push(normal.normal[0], normal.normal[1], normal.normal[2]);
        uvs.push(uv[0], uv[1]);
      }
    }
  }
  return {
    nodes: Int16Array.from(nodes), local: Float32Array.from(local),
    normalNodes: Int16Array.from(normalNodes), localNormals: Float32Array.from(localNormals),
    uvs: Float32Array.from(uvs),
  };
}

const tmpVec = new THREE.Vector3();
const tmpNormalMatrix = new THREE.Matrix3();

export function poseVertices(skin: MeshSkin, boneMatrices: THREE.Matrix4[], positions: THREE.TypedArray): void {
  for (let i = 0; i < skin.nodes.length; i++) {
    tmpVec.set(skin.local[i * 3], skin.local[i * 3 + 1], skin.local[i * 3 + 2]);
    const bone = boneMatrices[skin.nodes[i]];
    if (bone) tmpVec.applyMatrix4(bone);
    positions[i * 3] = tmpVec.x;
    positions[i * 3 + 1] = tmpVec.y;
    positions[i * 3 + 2] = tmpVec.z;
  }
}

// Normals turn with their bone (rotation only), for the chrome glow passes.
export function poseNormals(skin: MeshSkin, boneMatrices: THREE.Matrix4[], normals: THREE.TypedArray): void {
  for (let i = 0; i < skin.normalNodes.length; i++) {
    tmpVec.set(skin.localNormals[i * 3], skin.localNormals[i * 3 + 1], skin.localNormals[i * 3 + 2]);
    const bone = boneMatrices[skin.normalNodes[i]];
    if (bone) tmpVec.applyMatrix3(tmpNormalMatrix.setFromMatrix4(bone));
    normals[i * 3] = tmpVec.x;
    normals[i * 3 + 1] = tmpVec.y;
    normals[i * 3 + 2] = tmpVec.z;
  }
}

// One geometry per mesh of the model, posed with the given bone matrices.
export function buildGeometries(model: BMDModel, boneMatrices: THREE.Matrix4[]): GeometryPart[] {
  return model.meshes.map((mesh, meshIndex) => {
    const skin = expandMesh(mesh);
    const positions = new Float32Array(skin.local.length);
    const normals = new Float32Array(skin.localNormals.length);
    poseVertices(skin, boneMatrices, positions);
    poseNormals(skin, boneMatrices, normals);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
    geometry.setAttribute('uv', new THREE.BufferAttribute(skin.uvs, 2));
    geometry.computeBoundingSphere();
    return { geometry, skin, textureName: mesh.textureName, meshIndex };
  });
}

// ---------- shared, GPU-skinned geometry ----------

/**
 * Each mesh of a model as one geometry with its corners in their bone's space and the bone as the skin index: drawn
 * skinned on the GPU, so every character of the model shares it (one upload per model file, kept while the page lives:
 * never disposed when a character leaves).
 */
const sharedGeometryCache = new WeakMap<BMDModel, GeometryPart[]>();

export function sharedGeometries(model: BMDModel): GeometryPart[] {
  let parts = sharedGeometryCache.get(model);
  if (!parts) {
    parts = model.meshes.map((mesh, meshIndex) => {
      const skin = expandMesh(mesh);
      const count = skin.nodes.length;
      const skinIndex = new Uint16Array(count * 4);
      const skinWeight = new Float32Array(count * 4);
      for (let i = 0; i < count; i++) {
        skinIndex[i * 4] = Math.max(0, skin.nodes[i]);
        skinWeight[i * 4] = 1;
      }
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.BufferAttribute(skin.local, 3));
      geometry.setAttribute('normal', new THREE.BufferAttribute(skin.localNormals, 3));
      geometry.setAttribute('uv', new THREE.BufferAttribute(skin.uvs, 2));
      geometry.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(skinIndex, 4));
      geometry.setAttribute('skinWeight', new THREE.BufferAttribute(skinWeight, 4));
      geometry.boundingSphere = new THREE.Sphere(new THREE.Vector3(), 1e6); // never culled, never raycast
      return { geometry, skin, textureName: mesh.textureName, meshIndex };
    });
    sharedGeometryCache.set(model, parts);
  }
  return parts;
}

/**
 * The bones of one character for the skinning shader: its model's matrices (computeBoneMatrices, model space) as
 * they are, without three.js bone objects. Released skeletons are kept by size and given to the next character, so
 * a character that comes into view again makes no new bone texture.
 */
class MatrixSkeleton extends THREE.Skeleton {
  source: THREE.Matrix4[] = [];
  version = 0;
  private copied = -1;

  constructor(count: number) {
    super(Array.from({ length: count }, () => new THREE.Bone()));
  }

  override update(): void {
    if (this.copied === this.version) return;
    this.copied = this.version;
    for (let i = 0; i < this.source.length; i++) this.source[i].toArray(this.boneMatrices, i * 16);
    if (this.boneTexture) this.boneTexture.needsUpdate = true;
  }
}

const skeletonPool = new Map<number, MatrixSkeleton[]>();
const IDENTITY_BIND = new THREE.Matrix4();

function takeSkeleton(count: number, source: THREE.Matrix4[]): MatrixSkeleton {
  const skeleton = skeletonPool.get(count)?.pop() ?? new MatrixSkeleton(count);
  skeleton.source = source;
  skeleton.version++;
  return skeleton;
}

function skinnedMesh(geometry: THREE.BufferGeometry, material: THREE.Material, skeleton: MatrixSkeleton): THREE.SkinnedMesh {
  const mesh = new THREE.SkinnedMesh(geometry, material);
  mesh.bindMode = THREE.DetachedBindMode;
  mesh.bind(skeleton, IDENTITY_BIND);
  mesh.frustumCulled = false;
  return mesh;
}

export interface AnimatedModelOptions {
  /** Draw with the model's shared geometries, skinned on the GPU (the parts must come from sharedGeometries). */
  skinned?: boolean;
}

// An animated model whose meshes follow a skeleton (its own, or a shared one such as Player.bmd).
// Glow passes are extra meshes on the same geometry (see game/itemGlow.js).
export class AnimatedModel {
  skeleton: BMDModel;
  group: THREE.Group;
  matrices: THREE.Matrix4[];
  parts: ModelPart[];
  glows: ModelGlow[];
  lightFactor: LightFactor | null;
  /** Drawn in the light it is given, without its parts' level light (a debuff colours the body). */
  plainLight = false;
  /** The GPU skeleton of a skinned model (null: its vertices are posed on the CPU). */
  private gpuSkeleton: MatrixSkeleton | null;

  constructor(skeleton: BMDModel, parts: GeometryPart[], materials: THREE.MeshBasicMaterial[], { skinned = false }: AnimatedModelOptions = {}) {
    this.skeleton = skeleton;
    this.group = new THREE.Group();
    this.matrices = computeBoneMatrices(skeleton, 0, 0);
    this.parts = [];
    this.glows = [];
    this.lightFactor = null; // a function of time giving the light multiplier of the item level
    this.gpuSkeleton = skinned ? takeSkeleton(Math.max(1, skeleton.bones.length), this.matrices) : null;
    parts.forEach((part, index) => {
      const mesh = this.overlay(part.geometry, materials[index]) as THREE.Mesh<THREE.BufferGeometry, THREE.MeshBasicMaterial>;
      this.group.add(mesh);
      this.parts.push({ ...part, mesh });
    });
  }

  /** Whether its meshes are skinned on the GPU with shared geometry. */
  get skinned(): boolean { return this.gpuSkeleton !== null; }

  /** A mesh drawn over a part's geometry that follows the model's bones (a glow pass, a look's second drawing). */
  overlay(geometry: THREE.BufferGeometry, material: THREE.Material): THREE.Mesh {
    const mesh = this.gpuSkeleton ? skinnedMesh(geometry, material, this.gpuSkeleton) : new THREE.Mesh(geometry, material);
    mesh.frustumCulled = false;
    return mesh;
  }

  pose(actionIndex: number, time: number): void {
    computeBoneMatrices(this.skeleton, actionIndex, time, this.matrices);
    if (this.gpuSkeleton) {
      this.gpuSkeleton.version++;
      return;
    }
    const withNormals = this.glows.length > 0;
    for (const part of this.parts) {
      const attribute = part.geometry.getAttribute('position');
      poseVertices(part.skin, this.matrices, attribute.array);
      attribute.needsUpdate = true;
      if (withNormals) {
        const normals = part.geometry.getAttribute('normal');
        poseNormals(part.skin, this.matrices, normals.array);
        normals.needsUpdate = true;
      }
    }
  }

  addGlow(glow: ModelGlow): void {
    this.glows.push(glow);
    for (const mesh of glow.meshes) this.group.add(mesh);
  }

  setLight(color: THREE.Color, seconds = 0): void {
    for (const part of this.parts) {
      if (part.mesh.material.blending === THREE.AdditiveBlending) continue;
      part.mesh.material.color.copy(color);
      if (part.tint) part.mesh.material.color.multiply(part.tint);
      const factor = this.plainLight ? null : part.lightFactor?.(seconds);
      if (factor) part.mesh.material.color.multiply(factor);
    }
    for (const glow of this.glows) glow.update(seconds);
  }

  /**
   * The model left the scene for good: its GPU skeleton goes back to the pool. The shared geometry and the cached
   * textures stay (the next character of the model draws with them).
   */
  release(): void {
    const skeleton = this.gpuSkeleton;
    if (!skeleton) return;
    this.gpuSkeleton = null;
    skeleton.source = [];
    const pool = skeletonPool.get(skeleton.bones.length) ?? [];
    pool.push(skeleton);
    skeletonPool.set(skeleton.bones.length, pool);
  }
}
