// The world objects' textures (world.ts, objectTextureFolders), checked without a game server: the map is built
// with the game's own code. Devias's weapon racks (Object3) name item textures (sword14, hound_shield1, shield05, ...)
// that only Item/ has; the desktop finds them by name, the web drew them grey until the map check of 2026-11-20.
import { test, expect, type Page } from '@playwright/test';

const PAGE = '/map-objects.html';

async function openPage(page: Page): Promise<void> {
  await page.route(`**${PAGE}`, (route) => route.fulfill({
    contentType: 'text/html',
    body: '<!doctype html><html><head><script type="importmap">{"imports":{"three":"/vendor/three/build/three.module.js","three/":"/vendor/three/"}}</script></head><body></body></html>',
  }));
  await page.goto(PAGE);
}

/** The object meshes of a map drawn without a texture, by model file. */
const untexturedObjects = (page: Page, map: number): Promise<{ meshes: number; untextured: Record<string, number> }> => page.evaluate(async (mapNumber) => {
  const THREE = await import('three');
  const world = await import('/js/world.js');
  const data = await world.loadWorldData(mapNumber);
  const built = await world.createWorld(data, { color: new THREE.Color(0), near: 2600, far: 4200 });
  const untextured: Record<string, number> = {};
  let meshes = 0;
  for (const child of built.group.children) {
    const model = child.userData.model as string | undefined;
    if (!model) continue;
    meshes++;
    const material = (child as unknown as { material: { visible: boolean; map: unknown } }).material;
    if (material.visible && !material.map) untextured[model] = (untextured[model] ?? 0) + 1;
  }
  return { meshes, untextured };
}, map);

test('Devias\'s weapon racks are drawn with the items\' textures', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  page.on('pageerror', (error) => { throw error; });
  await openPage(page);
  const devias = await untexturedObjects(page, 2);
  expect(devias.meshes).toBeGreaterThan(100);
  expect(devias.untextured).toEqual({});
});
