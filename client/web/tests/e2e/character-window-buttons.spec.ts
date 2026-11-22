// Character window (#btn_pet, #btn_quest, #btn_exit, #btn_masterlevel) -> PetInfoWindow (pet_info.rml).
import { test, expect } from '@playwright/test';
import { dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

const OPEN = { timeout: 60_000 };

test('Character Window Pet button opens Pet Info window and closes with Character Window', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test400');
  await dismissOnboarding(page);

  await page.keyboard.press('c');
  const charWin = page.locator('.rml-character_info');
  await expect(charWin.locator('#panel')).toBeVisible(OPEN);

  // Clicking #btn_pet opens Pet Info window (pet_info.rml) beside Character Info.
  await charWin.locator('#btn_pet').click();
  const petWin = page.locator('.rml-pet_info');
  await expect(petWin.locator('#panel')).toBeVisible(OPEN);
  await expect(charWin.locator('#panel')).toBeVisible();

  // Dark Horse tab (0) and Dark Spirit tab (1) switch cleanly.
  await petWin.locator('.tab-btn-darkspirit').click();
  await expect(petWin.locator('.tab-btn-darkspirit')).toHaveClass(/\bactive\b/);
  await petWin.locator('.tab-btn-darkhorse').click();
  await expect(petWin.locator('.tab-btn-darkhorse')).toHaveClass(/\bactive\b/);

  // Closing Pet Info via #btn_exit leaves Character Info open.
  await petWin.locator('#btn_exit').click();
  await expect(petWin).toHaveCount(0);
  await expect(charWin.locator('#panel')).toBeVisible();

  // Re-open Pet Info, then closing Character Info closes Pet Info too (WindowSystem.cpp).
  await charWin.locator('#btn_pet').click();
  await expect(petWin.locator('#panel')).toBeVisible(OPEN);
  await charWin.locator('#btn_exit').click();
  await expect(charWin).toHaveCount(0);
  await expect(petWin).toHaveCount(0);

  // #btn_quest opens My Quest Info.
  await page.keyboard.press('c');
  await expect(charWin.locator('#panel')).toBeVisible(OPEN);
  await charWin.locator('#btn_quest').click();
  await expect(page.locator('.rml-my_quest_info #panel')).toBeVisible(OPEN);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-my_quest_info')).toHaveCount(0);

  await expectNoPageErrors(errors);
});
