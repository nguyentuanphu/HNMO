// A monster viewer and audit for development and the browser tests (tests/e2e/monster-looks.spec.ts): it builds the
// monsters and NPCs of Data/Monsters/Monsters.json with the game's own code (createMonsterCharacter), without a game
// server, reports what each one is drawn with (model, skin, body parts, weapons, textures) and what the web leaves
// out, and draws a row of them for a picture. Open it from the page with `await import('/js/dev/monsterViewer.js')`.

import * as THREE from 'three';
import { TERRAIN_SCALE, TERRAIN_SIZE } from '../formats.js';
import { createMonsterCharacter, setPlayerAnimationSpeeds } from '../game/characters.js';
import { loadItemCatalog } from '../game/items.js';
import { loadGlowColors } from '../game/itemGlow.js';
import { describeMonster, loadMonsterCatalog, monsterCatalog, SETUP_FIELDS } from '../game/monsterData.js';
import type { Character, CharacterWorld } from '../game/characters.js';
import type { MonsterLook } from '../game/monsterData.js';

/**
 * The setup fields the web draws or uses. The others are listed as ignored for each monster that has one; the
 * web has no character shadows and no pointer outlines at all (outline, shadow, hideShadow).
 */
export const SUPPORTED_SETUP_FIELDS: readonly string[] = [
  'scale', 'subType', 'lookLevel', 'class', 'classStep', 'equipment', 'glowMesh', 'glowLight', 'hiddenMesh', 'alphaTarget', 'light',
  'startAction', 'startFrame', 'heightOffset', 'height', 'positionOffset', 'facing', 'live', 'noBloodPool', 'deathEffect', 'spawnEffects',
];

export interface MonsterAuditRow {
  number: number;
  name: string;
  kind: string;
  model: string;
  variant: number;
  unknown: string | null;
  error: string | null;
  /** Console warnings while it was built (missing files, textures, items). */
  warnings: string[];
  meshes: number;
  visibleMeshes: number;
  /** Meshes drawn without a texture (grey). */
  untextured: number;
  attachments: string[];
  /** The skin a player-shaped one is drawn with, or its body part files. */
  body: string[];
  scale: number;
  playerShaped: boolean;
  ignored: string[];
}

function fakeWorld(mapNumber: number): CharacterWorld {
  const light = new Float32Array(TERRAIN_SIZE * TERRAIN_SIZE * 3).fill(1);
  return { heights: new Float32Array(TERRAIN_SIZE * TERRAIN_SIZE), light, flags: new Uint8Array(TERRAIN_SIZE * TERRAIN_SIZE), mapNumber };
}

export async function ready(): Promise<void> {
  await Promise.all([loadMonsterCatalog(), loadItemCatalog(), loadGlowColors()]);
  setPlayerAnimationSpeeds(0, 0);
}

/** The maps a number appears on (its "maps"), or null. */
function firstMap(number: number): number | null {
  return monsterCatalog()?.byNumber.get(number)?.maps?.[0] ?? null;
}

function ignoredFields(look: MonsterLook): string[] {
  const ignored: string[] = [];
  for (const field of SETUP_FIELDS) {
    if (look.setup[field] === undefined) continue;
    if (!SUPPORTED_SETUP_FIELDS.includes(field)) ignored.push(field);
    else if (field === 'subType' && !look.player) ignored.push('subType (model code)');
    else if (field === 'lookLevel' && !(look.player && look.player.skinLevel > 0)) ignored.push('lookLevel (model code)');
  }
  if (look.setup.equipment?.helper?.mountLook) ignored.push('equipment.helper.mountLook');
  return ignored;
}

/** Builds one monster (a random look by index) and reports it. */
export async function auditOne(number: number, variant = 0, map: number | null = firstMap(number)): Promise<{ row: MonsterAuditRow; character: Character | null }> {
  const warnings: string[] = [];
  const warn = console.warn;
  console.warn = (...args: unknown[]) => { warnings.push(args.map(String).join(' ')); };
  const looks = (monsterCatalog()?.byNumber.get(number)?.randomVariants?.length ?? 0) + 1;
  const random = () => (variant + 0.5) / looks;
  const look = describeMonster(number, { map, random });
  let character: Character | null = null;
  let error: string | null = null;
  try {
    character = await createMonsterCharacter({ id: number, type: number, map, random });
  } catch (caught) {
    error = (caught as Error).message;
  } finally {
    console.warn = warn;
  }
  const parts = character?.model.parts ?? [];
  const row: MonsterAuditRow = {
    number, name: look.name, kind: look.kind, model: character?.look?.model ?? look.model, variant: look.variant,
    unknown: character?.look?.unknown ?? look.unknown, error, warnings,
    meshes: parts.length, visibleMeshes: parts.filter((part) => part.mesh.visible && part.mesh.material.visible).length,
    untextured: parts.filter((part) => part.mesh.visible && part.mesh.material.visible && !part.mesh.material.map).length,
    attachments: character?.attachments.map((attachment) => `${attachment.slot} ${attachment.item.group},${attachment.item.number} bone ${attachment.bone}`) ?? [],
    body: look.player?.skin ? [`skin ${look.player.skin.dir}/${look.player.skin.fileName}`] : Object.values(look.bodyParts).map((file) => `${file!.dir}/${file!.fileName}`),
    scale: character?.group.scale.x ?? look.scale, playerShaped: Boolean(look.player), ignored: ignoredFields(look),
  };
  return { row, character };
}

/** Every number of the list, each of its looks. */
export async function auditAll(): Promise<MonsterAuditRow[]> {
  await ready();
  const rows: MonsterAuditRow[] = [];
  for (const [number, entry] of monsterCatalog()!.byNumber) { // ready() loaded it
    const looks = (entry.randomVariants?.length ?? 0) + 1;
    for (let variant = 0; variant < looks; variant++) {
      const { row, character } = await auditOne(number, variant);
      character?.release();
      rows.push(row);
    }
  }
  return rows;
}

export interface Viewer {
  renderer: THREE.WebGLRenderer;
  characters: Character[];
  render(seconds: number): void;
}

/**
 * Draws the monsters in a row on a grey floor, facing the camera, standing (`time` seconds into their stand), each
 * at its own size. `map` is the map they appear on (map variants, texture folders).
 */
export async function showRow(canvas: HTMLCanvasElement, numbers: number[], map: number | null = null, action?: number): Promise<Viewer> {
  await ready();
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, preserveDrawingBuffer: true });
  renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
  renderer.setSize(canvas.width, canvas.height, false);
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x30343a);
  const root = new THREE.Group();
  root.rotation.x = -Math.PI / 2;
  scene.add(root);
  const spacing = 180;
  const width = (numbers.length - 1) * spacing;
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(width + 600, 900), new THREE.MeshBasicMaterial({ color: 0x55585e }));
  floor.position.set(100 + width / 2, 1000, -1);
  root.add(floor);
  const world = fakeWorld(map ?? 0);
  const characters: Character[] = [];
  for (const [index, number] of numbers.entries()) {
    const { character } = await auditOne(number, 0, map ?? firstMap(number));
    if (!character) continue;
    character.position.set(100 + index * spacing, 1000);
    character.cell = { x: Math.floor(character.position.x / TERRAIN_SCALE), y: 10 };
    character.angle = 0; // facing the camera
    if (action !== undefined) character.playOnce(action, undefined, true);
    character.update(0, world);
    root.add(character.group);
    characters.push(character);
  }
  const camera = new THREE.PerspectiveCamera(30, canvas.width / canvas.height, 10, 20000);
  const center = new THREE.Vector3(100 + width / 2, 110, -1000);
  camera.position.set(center.x, 260, -1000 + Math.max(700, width * 1.15 + 300));
  camera.lookAt(center);
  return {
    renderer, characters,
    render(seconds: number) {
      for (const character of characters) {
        if (character.fixedAngle === null) character.angle = 0;
        character.time = 0;
        character.update(seconds, world);
      }
      renderer.render(scene, camera);
    },
  };
}
