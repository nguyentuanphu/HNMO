// Command window (D): all 12 buttons (Trade..Duel arm/disarm, Special opens Chat Command list chat_command.rml).
import { test, expect } from '@playwright/test';
import { dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

const OPEN = { timeout: 60_000 };

test('Command Window Special button opens Chat Command list and all 12 buttons respond', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test400');
  await dismissOnboarding(page);

  await page.keyboard.press('d');
  const cmdWin = page.locator('.rml-command_window');
  await expect(cmdWin.locator('#panel')).toBeVisible(OPEN);
  await expect(cmdWin.locator('.cmd-btn')).toHaveCount(12);

  // Clicking buttons 0..10 arms them cleanly (matches CCommandWindow::PressCommandButton).
  for (let i = 0; i < 11; i++) {
    await cmdWin.locator('.cmd-btn').nth(i).click();
    await expect(cmdWin.locator('.cmd-btn').nth(i)).toHaveClass(/\bselected\b/);
  }

  // Clicking button 11 (Special) closes Command window and opens Chat Command window (chat_command.rml).
  await cmdWin.locator('.cmd-btn').nth(11).click();
  await expect(cmdWin).toHaveCount(0);

  const chatCmd = page.locator('.rml-chat_command');
  await expect(chatCmd.locator('#panel')).toBeVisible(OPEN);
  await expect.poll(() => chatCmd.locator('.cc-row').count(), OPEN).toBeGreaterThan(0);

  // Templates button (#btn_right) switches to Templates page, Back (#btn_left) returns.
  await chatCmd.locator('#btn_right').click();
  await expect(chatCmd.locator('#btn_left')).toBeVisible();
  await chatCmd.locator('#btn_left').click();
  await expect(chatCmd.locator('#btn_right')).toBeVisible();

  // #btn_exit closes Chat Command window.
  await chatCmd.locator('#btn_exit').click();
  await expect(chatCmd).toHaveCount(0);

  // Shift+J toggles Chat Command window open and Escape closes it.
  await page.keyboard.press('Shift+j');
  await expect(chatCmd.locator('#panel')).toBeVisible(OPEN);
  await page.keyboard.press('Escape');
  await expect(chatCmd).toHaveCount(0);

  await expectNoPageErrors(errors);
});
