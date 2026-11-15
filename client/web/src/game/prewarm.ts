// Gets a character's GPU work done before it is drawn: its textures uploaded (renderer.initTexture) and its
// materials' programs compiled (renderer.compileAsync, which waits on KHR_parallel_shader_compile where the
// browser has it), so the frame it first shows in does not stall on an upload or a program link. The textures
// and the shared geometry stay cached, so a character of a warmed model costs no upload when it appears again.

import * as THREE from 'three';
import { createMonsterCharacter } from './characters.js';
import { monsterCatalog } from './monsterData.js';
import type { Character } from './characters.js';

const uploaded = new WeakSet<THREE.Texture>();
const warmedNumbers = new Set<string>();

export interface Warmer {
  renderer: THREE.WebGLRenderer;
  camera: THREE.Camera;
  scene: THREE.Scene;
}

/** Uploads the object's textures and compiles its programs; resolves when they are ready. */
export async function warmObject({ renderer, camera, scene }: Warmer, object: THREE.Object3D): Promise<void> {
  object.traverse((child) => {
    if (!(child instanceof THREE.Mesh)) return;
    const materials: THREE.Material[] = Array.isArray(child.material) ? child.material : [child.material];
    for (const material of materials) {
      const map = (material as THREE.MeshBasicMaterial).map ?? (material as THREE.ShaderMaterial).uniforms?.map?.value as THREE.Texture | undefined;
      if (map instanceof THREE.Texture && !uploaded.has(map)) {
        uploaded.add(map);
        renderer.initTexture(map);
      }
    }
  });
  object.updateMatrixWorld(true);
  await renderer.compileAsync(object, camera, scene);
}

/**
 * Builds one character of each look the map's monsters and NPCs appear with (Monsters.json "maps"), off screen, and
 * warms it: the first of them that comes into view draws at once. Runs one at a time in the background.
 */
export async function prewarmMapMonsters(warmer: Warmer, map: number, stillOnMap: () => boolean): Promise<number> {
  const catalog = monsterCatalog();
  if (!catalog) return 0;
  let count = 0;
  for (const [number, entry] of catalog.byNumber) {
    if (!entry.maps?.includes(map)) continue;
    const looks = (entry.randomVariants?.length ?? 0) + 1;
    for (let look = 0; look < looks; look++) {
      const key = `${number}:${map}:${look}`;
      if (warmedNumbers.has(key)) continue;
      if (!stillOnMap()) return count;
      warmedNumbers.add(key);
      let character: Character | null = null;
      try {
        character = await createMonsterCharacter({ id: -1, type: number, map, random: () => (look + 0.5) / looks });
        await warmObject(warmer, character.group);
        count++;
      } catch {
        // drawn with the unknown look, or not at all, when it appears
      } finally {
        character?.release();
      }
    }
  }
  return count;
}
