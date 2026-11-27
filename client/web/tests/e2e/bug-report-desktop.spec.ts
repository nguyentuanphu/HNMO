// Reporting a bug from a PC or Mac browser: the 🐞 button shows in the top bar on a desktop window (not only with the
// touch controls) and opens the window; Alt+B (Option+B on a Mac, whose key gives "∫") opens it by the key's place (event.code).
import { test, expect } from '@playwright/test';
import { enterWorld } from './helpers.js';

test.use({ viewport: { width: 1280, height: 800 } });

test('the 🐞 button and Alt/Option+B open the bug report window on a desktop browser', async ({ page }) => {
  await enterWorld(page, 'test7');
  const button = page.locator('#hnmoBugButton');
  const panel = page.locator('#hnmo-bug-panel');
  await expect(button).toBeVisible();
  await page.screenshot({ path: 'test-results/bug-button-desktop.png' });

  await button.click();
  await expect(panel).toBeVisible();
  await expect(panel).toContainText(/Report a bug|Báo lỗi/);
  await page.screenshot({ path: 'test-results/bug-window-desktop.png' });
  await panel.locator('.hnmo-panel-close').click();
  await expect(panel).toBeHidden();

  // A Mac's Option+B: key "∫", code "KeyB".
  await page.evaluate(() => {
    window.dispatchEvent(new KeyboardEvent('keydown', { key: '∫', code: 'KeyB', altKey: true, bubbles: true }));
  });
  await expect(panel).toBeVisible();
  await page.evaluate(() => {
    window.dispatchEvent(new KeyboardEvent('keydown', { key: '∫', code: 'KeyB', altKey: true, bubbles: true }));
  });
  await expect(panel).toBeHidden();

  // A real Alt+B from the keyboard.
  await page.keyboard.press('Alt+KeyB');
  await expect(panel).toBeVisible();
});
