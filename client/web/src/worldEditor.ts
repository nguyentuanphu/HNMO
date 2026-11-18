import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { TERRAIN_SIZE, TERRAIN_SCALE, TerrainFlag } from './formats.js';
import { setDataBaseUrl } from './bmd.js';
import { createWorld, loadWorldData } from './world.js';
import type { World } from './world.js';
import type { TerrainFog } from './terrain.js';

export type EditorTool =
  | 'inspect'
  | 'safezone'
  | 'blocked'
  | 'noground'
  | 'water'
  | 'clear'
  | 'spawn'
  | 'enterGate'
  | 'exitGate';

export type CameraMode = 'player' | 'orbit';

export interface EditorSpawnDto {
  id: string;
  monsterNumber: number;
  monsterName: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  quantity: number;
  direction: number;
  spawnTrigger: number;
}

export interface EditorEnterGateDto {
  id: string;
  number: number;
  levelRequirement: number;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  targetGateId: string | null;
  targetGateLabel: string;
}

export interface EditorExitGateDto {
  id: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  direction: number;
  isSpawnGate: boolean;
}

export interface DotNetEditorBridge {
  invokeMethodAsync(methodName: string, ...args: unknown[]): Promise<unknown>;
}

export interface WorldEditorOptions {
  dataBaseUrl?: string;
}

interface StrokeDelta {
  indices: number[];
  before: number[];
  after: number[];
}

const DEFAULT_FOG: TerrainFog = {
  color: new THREE.Color(0x161920),
  near: 1800,
  far: 7600,
};

const TOOL_FLAG_MAP: Partial<Record<EditorTool, number>> = {
  safezone: TerrainFlag.SAFEZONE,
  blocked: TerrainFlag.NOMOVE,
  noground: TerrainFlag.NOGROUND,
  water: TerrainFlag.WATER,
  clear: 0,
};

const PLAYER_CAMERA_OFFSET = new THREE.Vector3(0, 1250, 1380);
const CELLS = TERRAIN_SIZE * TERRAIN_SIZE;

function base64ToBytes(b64: string): Uint8Array {
  const bin = atob(b64);
  const bytes = new Uint8Array(CELLS);
  const len = Math.min(bin.length, CELLS);
  for (let i = 0; i < len; i++) {
    bytes[i] = bin.charCodeAt(i);
  }
  return bytes;
}

function bytesToBase64(bytes: Uint8Array): string {
  let out = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    out += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return btoa(out);
}

function buildFallbackTerrainMesh(heights: Float32Array): THREE.Mesh {
  const geometry = new THREE.BufferGeometry();
  const positions = new Float32Array(CELLS * 3);
  for (let y = 0; y < TERRAIN_SIZE; y++) {
    for (let x = 0; x < TERRAIN_SIZE; x++) {
      const i = y * TERRAIN_SIZE + x;
      positions.set([x * TERRAIN_SCALE, y * TERRAIN_SCALE, heights[i]], i * 3);
    }
  }
  const indices = new Uint32Array((TERRAIN_SIZE - 1) * (TERRAIN_SIZE - 1) * 6);
  let p = 0;
  for (let y = 0; y < TERRAIN_SIZE - 1; y++) {
    for (let x = 0; x < TERRAIN_SIZE - 1; x++) {
      const a = y * TERRAIN_SIZE + x;
      const b = a + 1;
      const c = a + TERRAIN_SIZE;
      const d = c + 1;
      indices.set([a, b, d, a, d, c], p);
      p += 6;
    }
  }
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setIndex(new THREE.BufferAttribute(indices, 1));
  geometry.computeVertexNormals();
  const material = new THREE.MeshLambertMaterial({ color: 0x45583a, side: THREE.DoubleSide });
  return new THREE.Mesh(geometry, material);
}

export class WorldEditorViewport {
  private readonly host: HTMLElement;
  private readonly dotNet: DotNetEditorBridge | null;
  private readonly canvas: HTMLCanvasElement;
  private readonly renderer: THREE.WebGLRenderer | null = null;
  private readonly scene = new THREE.Scene();
  private readonly root = new THREE.Group();
  private readonly camera: THREE.PerspectiveCamera;
  private readonly controls: OrbitControls | null = null;
  private readonly raycaster = new THREE.Raycaster();
  private readonly overlayGroup = new THREE.Group();
  private readonly markerGroup = new THREE.Group();
  private readonly cursorMesh: THREE.LineLoop;

  private world: World | null = null;
  private fallbackMesh: THREE.Mesh | null = null;
  private raycastTarget: THREE.Object3D | null = null;
  private heights = new Float32Array(CELLS);
  private tileFlags = new Uint8Array(CELLS);

  private readonly overlayBytes = new Uint8Array(CELLS * 4);
  private readonly overlayTexture: THREE.DataTexture;
  private readonly overlayMaterial: THREE.MeshBasicMaterial;
  private readonly overlayMesh: THREE.Mesh;

  private tool: EditorTool = 'inspect';
  private brushRadius = 2;
  private cameraMode: CameraMode = 'orbit';
  private showOverlay = true;
  private overlayOpacity = 0.52;
  private showSpawns = true;
  private showGates = true;

  private spawns: EditorSpawnDto[] = [];
  private enterGates: EditorEnterGateDto[] = [];
  private exitGates: EditorExitGateDto[] = [];

  private readonly undoStack: StrokeDelta[] = [];
  private readonly redoStack: StrokeDelta[] = [];
  private activeStroke: Map<number, { before: number; after: number }> | null = null;
  private isPainting = false;
  private animId = 0;
  private resizeObserver: ResizeObserver | null = null;
  private disposed = false;

  public constructor(host: HTMLElement, dotNet: DotNetEditorBridge | null, options: WorldEditorOptions = {}) {
    this.host = host;
    this.dotNet = dotNet;
    setDataBaseUrl(options.dataBaseUrl ?? 'api/world-editor');

    this.host.innerHTML = '';
    this.canvas = document.createElement('canvas');
    this.canvas.id = 'world-editor-canvas';
    this.canvas.style.width = '100%';
    this.canvas.style.height = '100%';
    this.canvas.style.display = 'block';
    this.canvas.style.touchAction = 'none';
    this.host.append(this.canvas);

    this.scene.background = DEFAULT_FOG.color;
    this.scene.add(new THREE.AmbientLight(0xffffff, 0.85));
    const sun = new THREE.DirectionalLight(0xfff4d6, 0.9);
    sun.position.set(16000, 22000, 14000);
    this.scene.add(sun);

    this.root.rotation.x = -Math.PI / 2;
    this.scene.add(this.root);

    this.camera = new THREE.PerspectiveCamera(35, 16 / 9, 10, 22000);
    this.focusTile(128, 128);

    try {
      this.renderer = new THREE.WebGLRenderer({ canvas: this.canvas, antialias: true, preserveDrawingBuffer: true });
      this.renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
      this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    } catch (error) {
      console.warn('WebGL unavailable in WorldEditor:', error);
    }

    if (this.renderer) {
      this.controls = new OrbitControls(this.camera, this.canvas);
      this.controls.enableDamping = true;
      this.controls.dampingFactor = 0.08;
      this.controls.maxDistance = 14000;
      this.controls.minDistance = 400;
      this.controls.target.set(128 * TERRAIN_SCALE, 0, -128 * TERRAIN_SCALE);
      this.controls.update();
    }

    // Terrain zone attribute overlay plane matching 256x256 grid slightly raised to avoid z-fighting
    this.overlayTexture = new THREE.DataTexture(this.overlayBytes, TERRAIN_SIZE, TERRAIN_SIZE, THREE.RGBAFormat);
    this.overlayTexture.magFilter = THREE.NearestFilter;
    this.overlayTexture.minFilter = THREE.NearestFilter;
    this.overlayTexture.needsUpdate = true;

    const overlayGeom = new THREE.PlaneGeometry(TERRAIN_SIZE * TERRAIN_SCALE, TERRAIN_SIZE * TERRAIN_SCALE, 1, 1);
    overlayGeom.translate((TERRAIN_SIZE * TERRAIN_SCALE) / 2, (TERRAIN_SIZE * TERRAIN_SCALE) / 2, 18);
    this.overlayMaterial = new THREE.MeshBasicMaterial({
      map: this.overlayTexture,
      transparent: true,
      opacity: this.overlayOpacity,
      depthWrite: false,
      side: THREE.DoubleSide,
    });
    this.overlayMesh = new THREE.Mesh(overlayGeom, this.overlayMaterial);
    this.overlayGroup.add(this.overlayMesh);
    this.root.add(this.overlayGroup);
    this.root.add(this.markerGroup);

    const ringGeom = new THREE.BufferGeometry().setFromPoints([
      new THREE.Vector3(-TERRAIN_SCALE / 2, -TERRAIN_SCALE / 2, 24),
      new THREE.Vector3(TERRAIN_SCALE / 2, -TERRAIN_SCALE / 2, 24),
      new THREE.Vector3(TERRAIN_SCALE / 2, TERRAIN_SCALE / 2, 24),
      new THREE.Vector3(-TERRAIN_SCALE / 2, TERRAIN_SCALE / 2, 24),
    ]);
    this.cursorMesh = new THREE.LineLoop(ringGeom, new THREE.LineBasicMaterial({ color: 0xffd54f }));
    this.cursorMesh.visible = false;
    this.root.add(this.cursorMesh);

    this.attachEvents();
    this.resize();
    this.startLoop();
  }

  public async setMap(
    mapNumber: number,
    tileFlagsBase64: string,
    spawns: EditorSpawnDto[],
    enterGates: EditorEnterGateDto[],
    exitGates: EditorExitGateDto[],
  ): Promise<{ mode: string; objectCount: number }> {
    this.tileFlags = base64ToBytes(tileFlagsBase64);
    this.spawns = spawns ?? [];
    this.enterGates = enterGates ?? [];
    this.exitGates = exitGates ?? [];
    this.undoStack.length = 0;
    this.redoStack.length = 0;

    if (this.world) {
      this.root.remove(this.world.group);
      this.world = null;
    }
    if (this.fallbackMesh) {
      this.root.remove(this.fallbackMesh);
      this.fallbackMesh.geometry.dispose();
      this.fallbackMesh = null;
    }

    let mode = 'terrain-attributes-fallback';
    let objectCount = 0;
    try {
      const worldData = await loadWorldData(mapNumber);
      this.heights = worldData.heights;
      this.world = await createWorld(worldData, DEFAULT_FOG);
      this.root.add(this.world.group);
      this.raycastTarget = this.world.terrain;
      objectCount = worldData.objects.length;
      mode = 'full-3d-world';
    } catch {
      this.heights = new Float32Array(CELLS);
      this.fallbackMesh = buildFallbackTerrainMesh(this.heights);
      this.root.add(this.fallbackMesh);
      this.raycastTarget = this.fallbackMesh;
    }

    this.rebuildOverlayTexture();
    this.rebuildMarkers();
    this.notifyHistory();
    return { mode, objectCount };
  }

  public setTool(tool: EditorTool, brushRadius: number): void {
    this.tool = tool;
    this.brushRadius = Math.max(1, Math.min(12, Math.round(brushRadius || 1)));
    if (this.controls) {
      this.controls.enabled = this.tool === 'inspect';
    }
  }

  public setOverlayOptions(showOverlay: boolean, opacity: number, showSpawns: boolean, showGates: boolean): void {
    this.showOverlay = showOverlay;
    this.overlayOpacity = Math.max(0.05, Math.min(1, opacity));
    this.overlayGroup.visible = this.showOverlay;
    this.overlayMaterial.opacity = this.overlayOpacity;
    this.showSpawns = showSpawns;
    this.showGates = showGates;
    this.rebuildMarkers();
  }

  public setCameraMode(mode: CameraMode, tileX?: number, tileY?: number): void {
    this.cameraMode = mode;
    this.focusTile(tileX ?? 128, tileY ?? 128);
  }

  public focusTile(tileX: number, tileY: number): void {
    const clampedX = Math.max(0, Math.min(TERRAIN_SIZE - 1, Math.round(tileX)));
    const clampedY = Math.max(0, Math.min(TERRAIN_SIZE - 1, Math.round(tileY)));
    const wx = clampedX * TERRAIN_SCALE;
    const wz = -clampedY * TERRAIN_SCALE;
    const wy = this.heights[clampedY * TERRAIN_SIZE + clampedX] || 0;

    if (this.cameraMode === 'player') {
      this.camera.position.set(wx + PLAYER_CAMERA_OFFSET.x, wy + PLAYER_CAMERA_OFFSET.y, wz + PLAYER_CAMERA_OFFSET.z);
    } else {
      this.camera.position.set(wx + 2400, wy + 3400, wz + 2800);
    }

    if (this.controls) {
      this.controls.target.set(wx, wy, wz);
      this.controls.update();
    } else {
      this.camera.lookAt(wx, wy, wz);
    }
  }

  public updateEntities(spawns: EditorSpawnDto[], enterGates: EditorEnterGateDto[], exitGates: EditorExitGateDto[]): void {
    this.spawns = spawns ?? [];
    this.enterGates = enterGates ?? [];
    this.exitGates = exitGates ?? [];
    this.rebuildMarkers();
  }

  public paintAtTile(tileX: number, tileY: number, toolOverride?: EditorTool, radiusOverride?: number): number {
    const activeTool = toolOverride ?? this.tool;
    const flag = TOOL_FLAG_MAP[activeTool];
    if (flag === undefined) return 0;

    const radius = Math.max(1, Math.min(12, Math.round(radiusOverride ?? this.brushRadius)));
    const stroke = this.activeStroke ?? new Map<number, { before: number; after: number }>();
    const half = radius - 1;
    let changed = 0;

    for (let dy = -half; dy <= half; dy++) {
      for (let dx = -half; dx <= half; dx++) {
        if (radius > 2 && dx * dx + dy * dy > half * half + 1) continue;
        const x = tileX + dx;
        const y = tileY + dy;
        if (x < 0 || y < 0 || x >= TERRAIN_SIZE || y >= TERRAIN_SIZE) continue;
        const idx = y * TERRAIN_SIZE + x;
        const prev = stroke.get(idx)?.before ?? this.tileFlags[idx];
        const next = flag;
        if (this.tileFlags[idx] !== next) {
          this.tileFlags[idx] = next;
          stroke.set(idx, { before: prev, after: next });
          changed++;
        }
      }
    }

    if (changed > 0) {
      this.rebuildOverlayTexture();
      if (!this.activeStroke) {
        this.pushStroke(stroke);
      }
    }
    return changed;
  }

  public undo(): boolean {
    const delta = this.undoStack.pop();
    if (!delta) return false;
    for (let i = 0; i < delta.indices.length; i++) {
      this.tileFlags[delta.indices[i]] = delta.before[i];
    }
    this.redoStack.push(delta);
    this.rebuildOverlayTexture();
    this.notifyHistory();
    return true;
  }

  public redo(): boolean {
    const delta = this.redoStack.pop();
    if (!delta) return false;
    for (let i = 0; i < delta.indices.length; i++) {
      this.tileFlags[delta.indices[i]] = delta.after[i];
    }
    this.undoStack.push(delta);
    this.rebuildOverlayTexture();
    this.notifyHistory();
    return true;
  }

  public getTileFlagsBase64(): string {
    return bytesToBase64(this.tileFlags);
  }

  public getStateSummary(): { tool: EditorTool; brushRadius: number; undoDepth: number; redoDepth: number; safezoneCount: number; blockedCount: number } {
    let safezoneCount = 0;
    let blockedCount = 0;
    for (let i = 0; i < CELLS; i++) {
      if ((this.tileFlags[i] & TerrainFlag.SAFEZONE) !== 0) safezoneCount++;
      if ((this.tileFlags[i] & TerrainFlag.NOMOVE) !== 0) blockedCount++;
    }
    return {
      tool: this.tool,
      brushRadius: this.brushRadius,
      undoDepth: this.undoStack.length,
      redoDepth: this.redoStack.length,
      safezoneCount,
      blockedCount,
    };
  }

  public dispose(): void {
    this.disposed = true;
    cancelAnimationFrame(this.animId);
    this.resizeObserver?.disconnect();
    this.controls?.dispose();
    this.overlayTexture.dispose();
    this.overlayMaterial.dispose();
    this.renderer?.dispose();
  }

  private pushStroke(stroke: Map<number, { before: number; after: number }>): void {
    if (stroke.size === 0) return;
    const indices: number[] = [];
    const before: number[] = [];
    const after: number[] = [];
    for (const [idx, val] of stroke) {
      if (val.before !== val.after) {
        indices.push(idx);
        before.push(val.before);
        after.push(val.after);
      }
    }
    if (indices.length === 0) return;
    this.undoStack.push({ indices, before, after });
    if (this.undoStack.length > 60) this.undoStack.shift();
    this.redoStack.length = 0;
    this.notifyHistory();
  }

  private notifyHistory(): void {
    this.dotNet?.invokeMethodAsync('OnHistoryChangedAsync', this.undoStack.length > 0, this.redoStack.length > 0).catch(() => undefined);
  }

  private rebuildOverlayTexture(): void {
    for (let i = 0; i < CELLS; i++) {
      const f = this.tileFlags[i];
      const o = i * 4;
      if ((f & TerrainFlag.NOMOVE) !== 0) {
        this.overlayBytes[o] = 210; this.overlayBytes[o + 1] = 60; this.overlayBytes[o + 2] = 60; this.overlayBytes[o + 3] = 210;
      } else if ((f & TerrainFlag.NOGROUND) !== 0) {
        this.overlayBytes[o] = 20; this.overlayBytes[o + 1] = 20; this.overlayBytes[o + 2] = 24; this.overlayBytes[o + 3] = 230;
      } else if ((f & TerrainFlag.WATER) !== 0) {
        this.overlayBytes[o] = 48; this.overlayBytes[o + 1] = 128; this.overlayBytes[o + 2] = 230; this.overlayBytes[o + 3] = 195;
      } else if ((f & TerrainFlag.SAFEZONE) !== 0) {
        this.overlayBytes[o] = 215; this.overlayBytes[o + 1] = 220; this.overlayBytes[o + 2] = 230; this.overlayBytes[o + 3] = 185;
      } else {
        this.overlayBytes[o] = 65; this.overlayBytes[o + 1] = 185; this.overlayBytes[o + 2] = 95; this.overlayBytes[o + 3] = 90;
      }
    }
    this.overlayTexture.needsUpdate = true;
  }

  private rebuildMarkers(): void {
    while (this.markerGroup.children.length > 0) {
      const child = this.markerGroup.children.pop()!;
      if (child instanceof THREE.Mesh) child.geometry.dispose();
    }

    if (this.showSpawns) {
      const mat = new THREE.MeshBasicMaterial({ color: 0xff9800, wireframe: true });
      for (const s of this.spawns) {
        const w = Math.max(1, s.x2 - s.x1 + 1) * TERRAIN_SCALE;
        const h = Math.max(1, s.y2 - s.y1 + 1) * TERRAIN_SCALE;
        const cx = ((s.x1 + s.x2) / 2 + 0.5) * TERRAIN_SCALE;
        const cy = ((s.y1 + s.y2) / 2 + 0.5) * TERRAIN_SCALE;
        const z = this.heights[Math.min(255, s.y1) * TERRAIN_SIZE + Math.min(255, s.x1)] + 60;
        const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, 110), mat);
        mesh.position.set(cx, cy, z);
        this.markerGroup.add(mesh);
      }
    }

    if (this.showGates) {
      const enterMat = new THREE.MeshBasicMaterial({ color: 0x00e5ff, wireframe: false, transparent: true, opacity: 0.72 });
      const exitMat = new THREE.MeshBasicMaterial({ color: 0xb388ff, wireframe: false, transparent: true, opacity: 0.72 });
      for (const g of this.enterGates) {
        const w = Math.max(1, g.x2 - g.x1 + 1) * TERRAIN_SCALE;
        const h = Math.max(1, g.y2 - g.y1 + 1) * TERRAIN_SCALE;
        const cx = ((g.x1 + g.x2) / 2 + 0.5) * TERRAIN_SCALE;
        const cy = ((g.y1 + g.y2) / 2 + 0.5) * TERRAIN_SCALE;
        const z = this.heights[Math.min(255, g.y1) * TERRAIN_SIZE + Math.min(255, g.x1)] + 75;
        const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, 150), enterMat);
        mesh.position.set(cx, cy, z);
        this.markerGroup.add(mesh);
      }
      for (const g of this.exitGates) {
        const w = Math.max(1, g.x2 - g.x1 + 1) * TERRAIN_SCALE;
        const h = Math.max(1, g.y2 - g.y1 + 1) * TERRAIN_SCALE;
        const cx = ((g.x1 + g.x2) / 2 + 0.5) * TERRAIN_SCALE;
        const cy = ((g.y1 + g.y2) / 2 + 0.5) * TERRAIN_SCALE;
        const z = this.heights[Math.min(255, g.y1) * TERRAIN_SIZE + Math.min(255, g.x1)] + 75;
        const mesh = new THREE.Mesh(new THREE.CylinderGeometry(Math.max(w, h) * 0.4, Math.max(w, h) * 0.4, 150, 12), exitMat);
        mesh.rotation.x = Math.PI / 2;
        mesh.position.set(cx, cy, z);
        this.markerGroup.add(mesh);
      }
    }
  }

  private pickTile(clientX: number, clientY: number): { x: number; y: number } | null {
    const target = this.raycastTarget ?? this.overlayMesh;
    const rect = this.canvas.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return null;
    const ndc = new THREE.Vector2(
      ((clientX - rect.left) / rect.width) * 2 - 1,
      -((clientY - rect.top) / rect.height) * 2 + 1,
    );
    this.raycaster.setFromCamera(ndc, this.camera);
    const hits = this.raycaster.intersectObject(target, false);
    if (hits.length === 0) return null;
    const local = this.root.worldToLocal(hits[0].point.clone());
    const x = Math.floor(local.x / TERRAIN_SCALE);
    const y = Math.floor(local.y / TERRAIN_SCALE);
    if (x < 0 || y < 0 || x >= TERRAIN_SIZE || y >= TERRAIN_SIZE) return null;
    return { x, y };
  }

  private attachEvents(): void {
    this.canvas.addEventListener('pointerdown', (e) => {
      if (e.button !== 0 || this.tool === 'inspect') return;
      const tile = this.pickTile(e.clientX, e.clientY);
      if (!tile) return;

      if (this.tool === 'spawn' || this.tool === 'enterGate' || this.tool === 'exitGate') {
        this.dotNet?.invokeMethodAsync('OnTerrainPlaceRequestedAsync', tile.x, tile.y, this.tool).catch(() => undefined);
        return;
      }

      this.isPainting = true;
      this.activeStroke = new Map();
      this.paintAtTile(tile.x, tile.y);
      this.canvas.setPointerCapture(e.pointerId);
    });

    this.canvas.addEventListener('pointermove', (e) => {
      const tile = this.pickTile(e.clientX, e.clientY);
      if (tile) {
        const idx = tile.y * TERRAIN_SIZE + tile.x;
        const z = this.heights[idx] || 0;
        this.cursorMesh.visible = true;
        this.cursorMesh.position.set((tile.x + 0.5) * TERRAIN_SCALE, (tile.y + 0.5) * TERRAIN_SCALE, z);
        this.cursorMesh.scale.setScalar(Math.max(1, this.brushRadius));
        this.dotNet?.invokeMethodAsync('OnCursorMovedAsync', tile.x, tile.y, this.tileFlags[idx], Math.round(z)).catch(() => undefined);
        if (this.isPainting && this.activeStroke) {
          this.paintAtTile(tile.x, tile.y);
        }
      } else {
        this.cursorMesh.visible = false;
      }
    });

    const endPaint = () => {
      if (!this.isPainting) return;
      this.isPainting = false;
      if (this.activeStroke) {
        this.pushStroke(this.activeStroke);
        this.activeStroke = null;
      }
    };
    this.canvas.addEventListener('pointerup', endPaint);
    this.canvas.addEventListener('pointercancel', endPaint);

    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => this.resize());
      this.resizeObserver.observe(this.host);
    }
  }

  private resize(): void {
    const w = Math.max(320, this.host.clientWidth || 800);
    const h = Math.max(240, this.host.clientHeight || 520);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer?.setSize(w, h, false);
  }

  private startLoop(): void {
    const tick = () => {
      if (this.disposed) return;
      this.controls?.update();
      this.renderer?.render(this.scene, this.camera);
      this.animId = requestAnimationFrame(tick);
    };
    this.animId = requestAnimationFrame(tick);
  }
}

export function createWorldEditor(host: HTMLElement, dotNet: DotNetEditorBridge | null, options: WorldEditorOptions = {}): WorldEditorViewport {
  return new WorldEditorViewport(host, dotNet, options);
}
