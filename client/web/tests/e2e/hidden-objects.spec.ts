// The objects the desktop never draws (HiddenMesh -2): Lorencia's pose boxes and light posts are markers only. Drawn,
// the pose boxes were large black slabs over Lorencia's buildings.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

type Win = Window & { __game: { mapNumber: number; online: { say(text: string): void }; worldGroup?: { children: { userData: { model?: string } }[] } } };

test('Lorencia draws no pose boxes and no light posts', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test9');
  if (await page.evaluate(() => (window as unknown as Win).__game.mapNumber) !== 0) {
    await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 90_000 }).toBe(0);
  }
  const models = () => page.evaluate(() => [...new Set(((window as unknown as Win).__game.worldGroup?.children ?? []).map((c) => c.userData.model ?? ''))]);
  // The world's objects are there (houses), but none of the hidden markers.
  await expect.poll(async () => (await models()).some((m) => /^House\d+\.bmd$/.test(m)), { timeout: 90_000 }).toBe(true);
  expect((await models()).filter((m) => /^(PoseBox|Light)\d+\.bmd$/.test(m))).toEqual([]);
  await expectNoPageErrors(errors);
});

// The other maps' markers (light, smoke and sound emitters the desktop hides every frame): each map's object file
// has them, and none is among the objects drawn.
test('the other maps draw none of the objects the desktop hides', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  await page.goto('/index.html');
  const maps = [1, 4, 7, 8, 10, 31, 33, 37, 38, 39, 56, 57, 63, 79];
  const result = await page.evaluate(async (list) => {
    const world = await import('/js/world.js' as string) as {
      HIDDEN_OBJECTS: Record<number, number[]>;
      loadWorldData(map: number): Promise<{ worldNumber: number; mapNumber: number; objects: { type: number }[] }>;
      drawnObjects(data: unknown): Map<number, unknown[]>;
    };
    const out: { map: number; hiddenPlaced: number; hiddenDrawn: number[]; drawnTypes: number }[] = [];
    for (const map of list) {
      const data = await world.loadWorldData(map);
      const hidden = world.HIDDEN_OBJECTS[map] ?? [];
      const drawn = world.drawnObjects(data);
      out.push({
        map,
        hiddenPlaced: data.objects.filter((o) => hidden.includes(o.type)).length,
        hiddenDrawn: [...drawn.keys()].filter((type) => hidden.includes(type)),
        drawnTypes: drawn.size,
      });
    }
    return out;
  }, maps);
  console.log(JSON.stringify(result));
  for (const map of result) {
    expect(map.hiddenDrawn, `map ${map.map}: hidden types drawn`).toEqual([]);
    expect(map.drawnTypes, `map ${map.map}: its other objects are drawn`).toBeGreaterThan(0);
  }
  // The list is about real objects: most of these maps place some of their hidden types.
  expect(result.filter((map) => map.hiddenPlaced > 0).length).toBeGreaterThanOrEqual(10);
});
