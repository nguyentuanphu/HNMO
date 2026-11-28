// The character choice as a 3D stage, as the desktop's CharacterScene draws it (PARITY.md section 1): world 74 (files of
// World75/Object75), a fixed camera, up to five characters standing on the scene's five places in their stand pose,
// a click picks one (white light, two turning aurora rings on the ground, the click sound), a double click or Enter
// enters the game. The values are the desktop's: CameraConfig.h ForCharacterScene and DefaultCamera::UpdateFrustum
// (camera), WSclient.cpp ReceiveCharacterList (places), CharacterScene.cpp PlaceCharactersForDraw and
// RenderSelectedCharacterEffects (light and rings), ZzzCharacter.cpp CreateHero (scale) and SoundEvents.json
// (characterSelected -> SOUND_CLICK01).

import * as THREE from 'three';
import { loadWorldData, createWorld, type World, type WorldData } from '../world.js';
import { createPlayerCharacter, type Character } from '../game/characters.js';
import { loadTexture } from '../bmd.js';
import { sampleHeight } from '../terrain.js';
import type { EquipmentAppearance } from '../game/items.js';

/** The world the scene loads (WD_74NEW_CHARACTER_SCENE; its files are World75 and Object75). */
export const STAGE_MAP = 74;

/** Where the five characters stand (world units) and which way they face (degrees, the desktop's Angle[2]). */
export const STAGE_SLOTS: readonly { x: number; y: number; angle: number }[] = [
  { x: 8008, y: 18885, angle: 115 },
  { x: 7986, y: 19145, angle: 90 },
  { x: 8046, y: 19400, angle: 75 },
  { x: 8133, y: 19645, angle: 60 },
  { x: 8282, y: 19845, angle: 35 },
];

/** The characters' height in the scene (CharacterScene forces it every frame). */
export const STAGE_HEIGHT = 163;

/** The camera of the scene (CameraConfig.h ForCharacterScene): place, angles in degrees, horizontal FOV, planes, fog. */
export const STAGE_CAMERA = {
  position: [9758.93, 18913.11, 675.5] as const,
  angle: [-84.5, 0, -75] as const,
  hFov: 40,
  near: 20,
  far: 4100,
  fogNear: 4100,
  fogFar: 5125,
};

/**
 * The view direction and up vector of desktop camera angles (DefaultCamera::UpdateFrustum: R = Ry(A1)·Rx(A0)·Rz(A2),
 * forward = Rᵀ·(0,0,-1), up = Rᵀ·(0,1,0)), in game coordinates (x east, y north, z up).
 */
export function cameraBasis(angleDegrees: readonly [number, number, number]): { forward: [number, number, number]; up: [number, number, number] } {
  const [a0, a1, a2] = angleDegrees.map((a) => (a * Math.PI) / 180);
  const s0 = Math.sin(a0), c0 = Math.cos(a0), s1 = Math.sin(a1), c1 = Math.cos(a1), s2 = Math.sin(a2), c2 = Math.cos(a2);
  const norm = (v: [number, number, number]): [number, number, number] => { const l = Math.hypot(...v) || 1; return [v[0] / l, v[1] / l, v[2] / l]; };
  return {
    forward: norm([s1 * c2 - c1 * s0 * s2, -(s1 * s2 + c1 * s0 * c2), -c1 * c0]),
    up: norm([c0 * s2, c0 * c2, -s0]),
  };
}

/** The vertical FOV of a horizontal one (CameraConfig.h HFovToVFov: always on the 4:3 reference). */
export function verticalFov(hFovDegrees: number): number {
  return (2 * Math.atan(Math.tan((hFovDegrees * Math.PI) / 360) / (4 / 3)) * 180) / Math.PI;
}

/** The light of a character: the scene's base light, plus white for the picked one (PlaceCharactersForDraw). */
export function stageLight(selected: boolean): number {
  return selected ? 1 : 0.45;
}

/** The brightness of the rings under the picked character at a time in ms (sin(t·0.0015)·0.3 + 0.5). */
export function ringBrightness(timeMs: number): number {
  return Math.sin(timeMs * 0.0015) * 0.3 + 0.5;
}

/** One character of the account's list. */
export interface StageCharacter {
  slot: number;
  name: string;
  classIndex: number;
  /** Shown on the label over the character. */
  level?: number;
  /** The class's name on the label. */
  className?: string;
  equipment?: EquipmentAppearance;
}

interface StageEntry {
  slot: number;
  name: string;
  character: Character;
  /** The name, level and class over the character (an HTML label that follows it). */
  label: HTMLDivElement | null;
}

export interface CharacterStageOptions {
  /** The group in game coordinates (main.ts root). */
  root: THREE.Group;
  camera: THREE.PerspectiveCamera;
  canvas: HTMLCanvasElement;
  /** A character was picked (a click). */
  onSelect?: (slot: number, name: string) => void;
  /** The picked character is to enter the game (a double click, or Enter). */
  onEnter?: (slot: number, name: string) => void;
  /** The pick changed, by a click or by the game (the window's buttons follow it). */
  onSelectionChange?: (slot: number | null) => void;
  /** Where the labels over the characters go (none without it). */
  labelParent?: HTMLElement;
  /** Plays a sound of the game's list (SOUND_CLICK01). */
  playSound?: (name: string) => void;
}

const box = new THREE.Box3();
const corner = new THREE.Vector3();

export class CharacterStage {
  private world: World | null = null;
  private data: WorldData | null = null;
  private readonly entries: StageEntry[] = [];
  private readonly rings: THREE.Mesh[] = [];
  private readonly listeners: [string, EventListener][] = [];
  private selected: number | null = null;
  /** The class of a character being created, standing on the first free place. */
  private preview: StageEntry | null = null;
  private previewRequest = 0;
  private hovered: number | null = null;
  private time = 0;
  private savedCamera: { fov: number; near: number; far: number } | null = null;
  private disposed = false;

  constructor(private readonly options: CharacterStageOptions) {}

  /** The picked character's slot, or null. */
  get selectedSlot(): number | null { return this.selected; }

  /** The characters standing, by slot. */
  get slots(): number[] { return this.entries.map((e) => e.slot); }

  /** Loads the scene's world and the rings' texture (world 74 first: the characters stand on it). */
  async load(): Promise<void> {
    const fog = { color: new THREE.Color(0x000000), near: STAGE_CAMERA.fogNear, far: STAGE_CAMERA.fogFar };
    const data = await loadWorldData(STAGE_MAP);
    const world = await createWorld(data, fog);
    if (this.disposed) { disposeGroup(world.group); return; }
    this.data = data;
    this.world = world;
    this.options.root.add(world.group);
    const texture = await loadTexture('Skill', 'gmmzine.jpg');
    for (const size of [180, 120]) {
      // RenderTerrainAlphaBitmap(BITMAP_GM_AURORA, x, y, 1.8 / 1.2 cells): drawn bright (added) on the ground.
      // Drawn on the ground like the desktop's terrain bitmap: under the feet, so the legs and anything in front of it
      // hide it (depth test on); pulled a little towards the camera so the ground itself doesn't flicker through it.
      if (!texture) console.warn('Character stage: the rings\' texture (Skill/gmmzine.jpg) is missing.');
      const material = new THREE.MeshBasicMaterial({
        map: texture, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, depthTest: true, color: 0xffffff,
        polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4,
      });
      const ring = new THREE.Mesh(new THREE.PlaneGeometry(size, size), material);
      ring.visible = false;
      ring.renderOrder = 2;
      ring.name = 'character-stage-ring';
      this.rings.push(ring);
      this.options.root.add(ring);
    }
    this.placeCamera();
    this.listen();
  }

  /** Shows the account's characters on their places (the ones already standing are kept). */
  async setCharacters(list: readonly StageCharacter[]): Promise<void> {
    const wanted = new Set(list.map((c) => `${c.slot}:${c.name}`));
    for (const entry of [...this.entries]) {
      if (!wanted.has(`${entry.slot}:${entry.name}`)) this.removeEntry(entry);
    }
    await Promise.all(list.filter((c) => !this.entries.some((e) => e.slot === c.slot && e.name === c.name)).map(async (c) => {
      const entry = await this.standOn(c.slot, c.name, c.classIndex, c.equipment, [c.level !== undefined ? `Lv ${c.level}` : '', c.className ?? ''].filter(Boolean).join(' · '));
      if (!entry) return;
      entry.label?.setAttribute('data-slot', String(c.slot));
      this.entries.push(entry);
    }));
    if (this.selected !== null && !this.entries.some((e) => e.slot === this.selected)) this.select(null);
    else this.refreshLights();
  }

  /**
   * The class a new character will have, standing on the first free place in full light (the desktop's CharMakeWin
   * shows the class's model; here it stands where the character will stand). Null takes it away.
   */
  async showPreview(classIndex: number | null, className = ''): Promise<void> {
    const request = ++this.previewRequest;
    if (this.preview) { this.removeEntry(this.preview); this.preview = null; }
    if (classIndex === null) return;
    const slot = STAGE_SLOTS.findIndex((_, index) => !this.entries.some((e) => e.slot === index));
    if (slot < 0) return;
    const entry = await this.standOn(slot, 'new', classIndex, undefined, className);
    if (!entry) return;
    if (request !== this.previewRequest) { this.removeEntry(entry); return; }
    entry.character.bodyTint?.setScalar(1);
    entry.label?.classList.add('preview');
    entry.label?.setAttribute('data-preview', '1');
    const nameLine = entry.label?.querySelector('b');
    if (nameLine) nameLine.textContent = '✦';
    this.preview = entry;
  }

  /** The class shown for a new character, or null. */
  get previewClass(): number | null { return this.preview?.character.classIndex ?? null; }

  /** A character on a place of the scene, with its label (not yet in the list). */
  private async standOn(slot: number, name: string, classIndex: number, equipment: EquipmentAppearance | undefined, info: string): Promise<StageEntry | null> {
    const place = STAGE_SLOTS[slot];
    if (!place) return null;
    const character = await createPlayerCharacter({ id: 0x7f00 + slot, name, bodySet: classIndex + 1, equipment });
    if (this.disposed) return null;
    character.position.set(place.x, place.y);
    character.cell = { x: Math.floor(place.x / 100), y: Math.floor(place.y / 100) };
    character.angle = (place.angle * Math.PI) / 180;
    character.fixedAngle = character.angle;
    character.fixedHeight = STAGE_HEIGHT;
    // CreateHero: 1.2, a Rage Fighter 1.35.
    character.group.scale.setScalar(classIndex === 6 ? 1.35 : 1.2);
    character.bodyTint = new THREE.Color().setScalar(stageLight(false));
    character.group.name = `character-stage-${slot}`;
    let label: HTMLDivElement | null = null;
    if (this.options.labelParent) {
      // Name, level and class over the character (owner 2026-11-28: on each character, not in a box).
      label = document.createElement('div');
      label.className = 'stage-label';
      const nameLine = document.createElement('b');
      nameLine.textContent = name;
      const infoLine = document.createElement('small');
      infoLine.textContent = info;
      label.append(nameLine, infoLine);
      label.style.visibility = 'hidden';
      this.options.labelParent.append(label);
    }
    this.options.root.add(character.group);
    return { slot, name, character, label };
  }

  /** Picks a character by slot (null: none), with the click sound when asked. */
  select(slot: number | null, sound = false): void {
    if (slot !== null && !this.entries.some((e) => e.slot === slot)) return;
    this.selected = slot;
    this.refreshLights();
    this.options.onSelectionChange?.(slot);
    if (sound && slot !== null) this.options.playSound?.('SOUND_CLICK01');
  }

  /** The slot of the character under a point of the page, or null (the nearest one to the camera wins). */
  slotAt(clientX: number, clientY: number): number | null {
    const rect = this.options.canvas.getBoundingClientRect();
    let best: { slot: number; distance: number } | null = null;
    for (const entry of this.entries) {
      entry.character.pickBounds(box);
      if (box.isEmpty()) continue;
      let left = Infinity, right = -Infinity, top = Infinity, bottom = -Infinity, behind = false;
      for (let i = 0; i < 8; i++) {
        corner.set(i & 1 ? box.max.x : box.min.x, i & 2 ? box.max.y : box.min.y, i & 4 ? box.max.z : box.min.z).project(this.options.camera);
        if (corner.z < -1 || corner.z > 1) { behind = true; break; }
        const x = rect.left + ((corner.x + 1) / 2) * rect.width, y = rect.top + ((1 - corner.y) / 2) * rect.height;
        left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);
      }
      if (behind || clientX < left || clientX > right || clientY < top || clientY > bottom) continue;
      const distance = this.options.camera.position.distanceTo(entry.character.group.getWorldPosition(corner));
      if (!best || distance < best.distance) best = { slot: entry.slot, distance };
    }
    return best?.slot ?? null;
  }

  /** Where a character's chest is on the page (tests click there), or null when it isn't standing or isn't in view. */
  screenPointOf(slot: number): { x: number; y: number } | null {
    const entry = this.entries.find((e) => e.slot === slot);
    if (!entry) return null;
    entry.character.group.updateWorldMatrix(true, false);
    const point = entry.character.group.localToWorld(new THREE.Vector3(0, 0, 110)).project(this.options.camera);
    if (point.z < -1 || point.z > 1) return null;
    const rect = this.options.canvas.getBoundingClientRect();
    return { x: rect.left + ((point.x + 1) / 2) * rect.width, y: rect.top + ((1 - point.y) / 2) * rect.height };
  }

  /** One frame: the characters stand and breathe, the rings turn and glow. */
  update(dt: number): void {
    if (!this.data) return;
    this.time += dt * 1000;
    this.placeCamera();
    const world = { heights: this.data.heights, flags: this.data.flags, light: this.data.light, mapNumber: STAGE_MAP };
    for (const entry of this.entries) entry.character.update(dt, world);
    this.preview?.character.update(dt, world);
    this.placeLabels();
    const picked = this.entries.find((e) => e.slot === this.selected);
    const lumi = ringBrightness(this.time);
    this.rings.forEach((ring, index) => {
      ring.visible = !!picked;
      if (!picked) return;
      const ground = this.data ? sampleHeight(this.data.heights, picked.character.position.x, picked.character.position.y) : STAGE_HEIGHT;
      ring.position.set(picked.character.position.x, picked.character.position.y, Math.max(ground, STAGE_HEIGHT) + 2 + index);
      // The big ring turns one way, the small one the other (WorldTime·0.01 degrees).
      ring.rotation.set(0, 0, ((index === 0 ? 1 : -1) * this.time * 0.01 * Math.PI) / 180);
      (ring.material as THREE.MeshBasicMaterial).color.setScalar(lumi);
    });
  }

  /** Puts the world, characters and listeners away and gives the camera back its settings. */
  dispose(): void {
    this.disposed = true;
    for (const [type, listener] of this.listeners) this.options.canvas.removeEventListener(type, listener);
    this.listeners.length = 0;
    for (const entry of [...this.entries]) this.removeEntry(entry);
    if (this.preview) { this.removeEntry(this.preview); this.preview = null; }
    for (const ring of this.rings) {
      this.options.root.remove(ring);
      ring.geometry.dispose();
      (ring.material as THREE.Material).dispose();
    }
    this.rings.length = 0;
    if (this.world) { this.options.root.remove(this.world.group); disposeGroup(this.world.group); }
    this.world = null;
    this.data = null;
    this.options.canvas.style.cursor = '';
    if (this.savedCamera) {
      Object.assign(this.options.camera, this.savedCamera);
      this.options.camera.updateProjectionMatrix();
      this.savedCamera = null;
    }
  }

  private placeCamera(): void {
    const camera = this.options.camera;
    this.savedCamera ??= { fov: camera.fov, near: camera.near, far: camera.far };
    const fov = verticalFov(STAGE_CAMERA.hFov);
    if (camera.fov !== fov || camera.near !== STAGE_CAMERA.near || camera.far !== STAGE_CAMERA.far) {
      camera.fov = fov;
      camera.near = STAGE_CAMERA.near;
      camera.far = STAGE_CAMERA.far;
      camera.updateProjectionMatrix();
    }
    const { forward, up } = cameraBasis(STAGE_CAMERA.angle);
    const [x, y, z] = STAGE_CAMERA.position;
    // Game coordinates (x, y, z up) to the scene's (x, z, -y), as main.ts toScene.
    camera.position.set(x, z, -y);
    camera.up.set(up[0], up[2], -up[1]);
    camera.lookAt(x + forward[0], z + forward[2], -(y + forward[1]));
  }

  private refreshLights(): void {
    for (const entry of this.entries) {
      entry.character.bodyTint?.setScalar(stageLight(entry.slot === this.selected));
      entry.label?.classList.toggle('picked', entry.slot === this.selected);
    }
  }

  /** The labels follow the characters' heads on the page. */
  private placeLabels(): void {
    const canvasRect = this.options.canvas.getBoundingClientRect();
    const parentRect = this.options.labelParent?.getBoundingClientRect();
    if (!parentRect) return;
    for (const entry of this.preview ? [...this.entries, this.preview] : this.entries) {
      if (!entry.label) continue;
      entry.character.group.updateWorldMatrix(true, false);
      // A little over the head of the scaled body.
      const point = entry.character.group.localToWorld(corner.set(0, 0, 215)).project(this.options.camera);
      const shown = point.z >= -1 && point.z <= 1;
      entry.label.style.visibility = shown ? 'visible' : 'hidden';
      if (!shown) continue;
      const x = canvasRect.left + ((point.x + 1) / 2) * canvasRect.width - parentRect.left;
      const y = canvasRect.top + ((1 - point.y) / 2) * canvasRect.height - parentRect.top;
      entry.label.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px) translate(-50%, -100%)`;
    }
  }

  private removeEntry(entry: StageEntry): void {
    entry.label?.remove();
    this.options.root.remove(entry.character.group);
    const index = this.entries.indexOf(entry);
    if (index >= 0) this.entries.splice(index, 1);
  }

  private listen(): void {
    const canvas = this.options.canvas;
    const on = (type: string, listener: (event: MouseEvent) => void) => {
      const wrapped = listener as EventListener;
      canvas.addEventListener(type, wrapped);
      this.listeners.push([type, wrapped]);
    };
    on('pointermove', (event) => {
      this.hovered = this.slotAt(event.clientX, event.clientY);
      canvas.style.cursor = this.hovered !== null ? 'pointer' : '';
    });
    on('click', (event) => {
      const slot = this.slotAt(event.clientX, event.clientY);
      if (slot === null) return;
      this.select(slot, true);
      const entry = this.entries.find((e) => e.slot === slot);
      if (entry) this.options.onSelect?.(slot, entry.name);
    });
    on('dblclick', (event) => {
      const slot = this.slotAt(event.clientX, event.clientY);
      const entry = this.entries.find((e) => e.slot === slot);
      if (entry) this.options.onEnter?.(entry.slot, entry.name);
    });
  }
}

function disposeGroup(group: THREE.Object3D): void {
  group.traverse((object) => {
    const mesh = object as THREE.Mesh;
    mesh.geometry?.dispose();
  });
}
