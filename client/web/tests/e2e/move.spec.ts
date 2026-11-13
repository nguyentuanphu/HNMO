// The move window like the desktop's: M opens the warp list with each map's level and zen; a click
// on a map the hero may enter warps there (WarpCommandRequest).
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

type Win = Window & { __game: { mapNumber: number } };

test('M opens the warp list and a click warps to Noria', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test9');
  await page.keyboard.press('m');
  const list = page.locator('.rml-move_command #panel');
  await expect(list).toBeVisible({ timeout: 90_000 });
  const noria = page.locator('.rml-move_command .mc-row', { hasText: 'Noria' }).first();
  await expect(noria).toBeVisible();
  await page.screenshot({ path: 'test-results/move.png' });
  await noria.click();
  await expect(list).toBeHidden();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 60_000 }).toBe(3);
  // The map name banner fades in with the new map's name (CUIMapName).
  await expect(page.locator('.rml-map_name #name_text')).toHaveText('Noria', { timeout: 30_000 });
  await page.waitForTimeout(2500);
  await page.screenshot({ path: 'test-results/map-name.png' });
  // Back to town, so the account starts there next time.
  await page.evaluate(() => (window as unknown as Win & { __game: { online: { say(text: string): void } } }).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  await expectNoPageErrors(errors);
});
