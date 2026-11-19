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
