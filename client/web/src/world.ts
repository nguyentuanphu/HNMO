// Loads a world: terrain, walkability and the static objects placed on it.
// Lorencia's object types follow the model table of MapManager.cpp (world 0);
// the other worlds name their models Object01.bmd, Object02.bmd, ... by type + 1.

import * as THREE from 'three';
import { parseTerrainMapping, parseTerrainAttributes, parseTerrainHeight, parseWorldObjects } from './formats.js';
import { fetchBytes, loadModel, computeBoneMatrices, buildGeometries, createMaterial } from './bmd.js';
import { createTerrain, loadLightMap, sampleLight } from './terrain.js';
import type { TerrainMapping, WorldObject } from './formats.js';
import type { TerrainFog, TerrainMesh } from './terrain.js';

/** Everything loaded for a map before it is built into the scene. */
export interface WorldData {
  mapNumber: number;
  /** The data folder number (WorldN, ObjectN). */
  worldNumber: number;
  dir: string;
  mapping: TerrainMapping;
  /** Walkability flags per cell (TerrainFlag). */
  flags: Uint16Array;
  heights: Float32Array;
  objects: WorldObject[];
  /** The light map: r, g, b per cell. */
  light: Float32Array;
}

export interface World {
  group: THREE.Group;
  terrain: TerrainMesh;
  /** The roofs that fade out while the hero is inside a building, null on maps without them. */
  indoorFade: IndoorFade | null;
}

/**
 * The objects of a map that fade out while the hero stands on a building's floor tile, so the hero is seen
 * inside (ZzzObject.cpp, RenderObject: AlphaTarget 0 when HeroTile is a floor, else 1). By map number.
 */
const INDOOR_FADES: Record<number, { types: number[]; indoors: (tile: number) => boolean }> = {
  0: { types: [125, 126], indoors: (tile) => tile === 4 },                         // Lorencia: MODEL_HOUSE_WALL01 + 4 and + 5, on the wooden floor
  2: { types: [81, 82, 96, 98, 99], indoors: (tile) => tile === 3 || tile >= 10 }, // Devias
};

export interface IndoorFade {
  meshes: THREE.InstancedMesh[];
  indoors: (tile: number) => boolean;
  alpha: number;
}

/** The desktop eases an object's alpha 10% of the way to its target per frame at 25 frames a second (Alpha, ZzzAI.cpp). */
const FADE_STEPS_PER_SECOND = 25;
const FADE_STEP = 0.1;

/** Fades the roofs out while the hero stands on the floor tile `tile` inside a building, and back in outside. */
export function updateIndoorFade(fade: IndoorFade, tile: number, dt: number): void {
  const target = fade.indoors(tile) ? 0 : 1;
  if (fade.alpha === target) return;
  fade.alpha += (target - fade.alpha) * (1 - Math.pow(1 - FADE_STEP, dt * FADE_STEPS_PER_SECOND));
  if (Math.abs(target - fade.alpha) < 0.01) fade.alpha = target;
  for (const mesh of fade.meshes) {
    const material = mesh.material as THREE.MeshBasicMaterial;
    const base = mesh.userData.baseLook as { transparent: boolean; depthWrite: boolean; opacity: number };
    const solid = fade.alpha >= 1;
    material.transparent = solid ? base.transparent : true;
    material.depthWrite = solid ? base.depthWrite : false;
    material.opacity = base.opacity * fade.alpha;
    material.needsUpdate = true;
    mesh.visible = fade.alpha >= 0.01;
  }
}

const LORENCIA_MODELS: [name: string, firstType: number, count: number][] = [
  ['Tree', 0, 13], ['Grass', 20, 8], ['Stone', 30, 5], ['StoneStatue', 40, 3], ['SteelStatue', 43, 1],
  ['Tomb', 44, 3], ['FireLight', 50, 2], ['Bonfire', 52, 1], ['DoungeonGate', 55, 1], ['MerchantAnimal', 56, 2],
  ['TreasureDrum', 58, 1], ['TreasureChest', 59, 1], ['Ship', 60, 1], ['SteelWall', 65, 3], ['SteelDoor', 68, 1],
  ['StoneWall', 69, 6], ['StoneMuWall', 75, 4], ['Bridge', 80, 1], ['Fence', 81, 4], ['BridgeStone', 85, 1],
  ['StreetLight', 90, 1], ['Cannon', 91, 3], ['Curtain', 95, 1], ['Sign', 96, 2], ['Carriage', 98, 4],
  ['Straw', 102, 2], ['Waterspout', 105, 1], ['Well', 106, 4], ['Hanging', 110, 1], ['Stair', 111, 1],
  ['House', 115, 5], ['Tent', 120, 1], ['HouseWall', 121, 6], ['HouseEtc', 127, 3], ['Light', 130, 3],
  ['PoseBox', 133, 1], ['Furniture', 140, 7], ['Candle', 150, 1], ['Beer', 151, 3],
];

const MAX_WORLD_OBJECTS = 160;
const DEGREES = Math.PI / 180;

function modelFileName(name: string, number: number): string {
  return `${name}${String(number).padStart(2, '0')}.bmd`;
}

export function worldModelFiles(worldNumber: number): Map<number, string> {
  const files = new Map<number, string>();
  if (worldNumber === 1) {
    for (const [name, firstType, count] of LORENCIA_MODELS) {
      for (let i = 0; i < count; i++) files.set(firstType + i, modelFileName(name, i + 1));
    }
  } else {
    for (let type = 0; type < MAX_WORLD_OBJECTS; type++) files.set(type, modelFileName('Object', type + 1));
  }
  return files;
}

// The data folder a server map loads from (CMapManager::GetFilesFolder): events with
// several maps share one folder.
const SHARED_FOLDERS = [
  { maps: [11, 12, 13, 14, 15, 16, 17, 52], folder: 12 }, // Blood Castle
  { maps: [18, 19, 20, 21, 22, 23, 53], folder: 19 },     // Chaos Castle
  { maps: [24, 25, 26, 27, 28, 29, 36], folder: 25 },     // Kalima
  { maps: [45, 46, 47, 48, 49, 50], folder: 47 },         // Illusion Temple
  { maps: [9, 32], folder: 10 },                          // Devil Square (32 is the second one, GetLoadedWorld)
];

export function filesFolder(mapNumber: number): number {
  return SHARED_FOLDERS.find((entry) => entry.maps.includes(mapNumber))?.folder ?? mapNumber + 1;
}

const MAP_NAMES: Record<number, string> = {
  0: 'Lorencia', 1: 'Dungeon', 2: 'Devias', 3: 'Noria', 4: 'Lost Tower', 6: 'Arena', 7: 'Atlans', 8: 'Tarkan',
  9: 'Devil Square', 10: 'Icarus', 11: 'Blood Castle', 18: 'Chaos Castle', 24: 'Kalima', 30: 'Valley of Loren',
  31: 'Land of Trials', 36: 'Lost Kalima', 33: 'Aida', 34: 'Crywolf Fortress', 37: 'Kanturu', 38: 'Kanturu Relics', 39: 'Kanturu Refinery Tower',
  40: 'Silent Map', 41: 'Barracks of Balgass', 42: 'Balgass Refuge', 45: 'Illusion Temple', 51: 'Elveland',
  56: 'Swamp of Calmness', 57: 'Raklion', 58: 'Raklion Hatchery', 62: 'Santa Village', 63: 'Vulcanus', 64: 'Duel Arena',
  65: 'Doppelganger', 69: 'Imperial Guardian', 79: 'Loren Market', 80: 'Karutan 1', 81: 'Karutan 2',
};

export const KNOWN_MAPS = Object.keys(MAP_NAMES).map(Number);

// FindMapName's event maps: every map of an event has the event's name (EventMapName).
const EVENT_MAP_NAMES = [
  { maps: [12, 13, 14, 15, 16, 17, 52], name: 'Blood Castle' }, { maps: [19, 20, 21, 22, 23, 53], name: 'Chaos Castle' }, { maps: [32], name: 'Devil Square' },
  { maps: [45, 46, 47, 48, 49, 50], name: 'Illusion Temple' }, { maps: [65, 66, 67, 68], name: 'Doppelganger' },
  { maps: [69, 70, 71, 72], name: 'Varka' }, { maps: [25, 26, 27, 28, 29], name: 'Kalima' },
];

export function mapName(mapNumber: number): string {
  // The event names come first (CMapManager::FindMapName).
  return EVENT_MAP_NAMES.find((entry) => entry.maps.includes(mapNumber))?.name ?? MAP_NAMES[mapNumber] ?? `Map ${mapNumber}`;
}

export async function loadWorldData(mapNumber: number): Promise<WorldData> {
  const worldNumber = filesFolder(mapNumber);
  const dir = `World${worldNumber}`;
  const [mapBytes, attBytes, objBytes, heightBytes, light] = await Promise.all([
    fetchBytes(`/data/${dir}/EncTerrain${worldNumber}.map`),
    fetchBytes(`/data/${dir}/EncTerrain${worldNumber}.att`),
    fetchBytes(`/data/${dir}/EncTerrain${worldNumber}.obj`),
    fetchBytes(`/data/${dir}/TerrainHeight.OZB`),
    loadLightMap(`${dir}/TerrainLight.jpg`),
  ]);
  return {
    mapNumber,
    worldNumber,
    dir,
    mapping: parseTerrainMapping(mapBytes),
    flags: parseTerrainAttributes(attBytes),
    heights: parseTerrainHeight(heightBytes),
    objects: parseWorldObjects(objBytes).objects,
    light,
  };
}

function objectMatrix(object: WorldObject): THREE.Matrix4 {
  const [x, y, z] = object.position;
  const rotation = new THREE.Euler(object.angle[0] * DEGREES, object.angle[1] * DEGREES, object.angle[2] * DEGREES, 'ZYX');
  return new THREE.Matrix4().compose(
    new THREE.Vector3(x, y, z),
    new THREE.Quaternion().setFromEuler(rotation),
    new THREE.Vector3(object.scale, object.scale, object.scale),
  );
}

// One instanced mesh per model mesh, so the 2,000+ objects of a map draw in a few hundred calls.
async function buildObjectType(objectDir: string, fileName: string, placements: WorldObject[], light: Float32Array): Promise<THREE.InstancedMesh[]> {
  let model;
  try {
    model = await loadModel(objectDir, fileName);
  } catch (error) {
    console.warn('Model missing:', `${objectDir}/${fileName}`, (error as Error).message);
    return [];
  }
  const parts = buildGeometries(model, computeBoneMatrices(model, 0, 0));
  const color = new THREE.Color();
  return Promise.all(parts.map(async (part) => {
    const material = await createMaterial(objectDir, part.textureName);
    const mesh = new THREE.InstancedMesh(part.geometry, material, placements.length);
    placements.forEach((object, index) => {
      mesh.setMatrixAt(index, objectMatrix(object));
      mesh.setColorAt(index, sampleLight(light, object.position[0], object.position[1], color));
    });
    mesh.computeBoundingSphere();
    mesh.userData.model = fileName;
    return mesh;
  }));
}

export async function createWorld(data: WorldData, fog: TerrainFog, onProgress?: (progress: number) => void): Promise<World> {
  const group = new THREE.Group();
  const terrain = await createTerrain({ worldDir: data.dir, heights: data.heights, mapping: data.mapping, light: data.light, fog });
  group.add(terrain);

  const files = worldModelFiles(data.worldNumber);
  const byType = new Map<number, WorldObject[]>();
  for (const object of data.objects) {
    if (!files.has(object.type)) continue;
    if (!byType.has(object.type)) byType.set(object.type, []);
    byType.get(object.type)!.push(object);
  }
  const objectDir = `Object${data.worldNumber}`;
  const fadeRule = INDOOR_FADES[data.mapNumber] as (typeof INDOOR_FADES)[number] | undefined;
  const indoorFade: IndoorFade | null = fadeRule ? { meshes: [], indoors: fadeRule.indoors, alpha: 1 } : null;
  let done = 0;
  await Promise.all([...byType].map(async ([type, placements]) => {
    const meshes = await buildObjectType(objectDir, files.get(type)!, placements, data.light);
    meshes.forEach((mesh) => group.add(mesh));
    if (indoorFade && fadeRule?.types.includes(type)) {
      for (const mesh of meshes) {
        // Each roof mesh has its own material (createMaterial makes one per part), so fading it touches nothing else.
        const material = mesh.material as THREE.MeshBasicMaterial;
        mesh.userData.baseLook = { transparent: material.transparent, depthWrite: material.depthWrite, opacity: material.opacity };
        indoorFade.meshes.push(mesh);
      }
    }
    onProgress?.(++done / byType.size);
  }));
  return { group, terrain, indoorFade: indoorFade && indoorFade.meshes.length ? indoorFade : null };
}
