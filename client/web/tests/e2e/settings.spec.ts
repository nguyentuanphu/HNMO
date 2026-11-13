// The settings window (HNMO extra, src/ui/settingsWindow.ts): O opens the game's option window;
// the music volume and the text size are kept after a reload, and the low-spec mode lowers the
// render scale (the canvas is drawn with fewer pixels).
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

const WINDOW = '.rml-option_window';
const OPEN_TIMEOUT = 90_000;

interface AudioView { getSettings(): { musicLevel: number; effectsLevel: number; muted: boolean } }

const musicLevel = (page: Page) => page.evaluate(async () => {
  const audio = (await import('/js/audio/index.js' as string)) as { audioSystem(): AudioView };
  return audio.audioSystem().getSettings().musicLevel;
});
const textScale = (page: Page) => page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--hnmo-text-scale').trim());
// Drawn pixels per CSS pixel of the game's canvas: the renderer's pixel ratio.
const pixelRatio = (page: Page) => page.evaluate(() => {
  const canvas = document.getElementById('view') as HTMLCanvasElement;
  return canvas.width / canvas.clientWidth;
});

async function openSettings(page: Page): Promise<void> {
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.keyboard.press('o');
  await page.waitForSelector(`${WINDOW} #panel`, { timeout: OPEN_TIMEOUT });
}

// The filled part of the music volume bar (level-N classes, N = 0..10).
const musicFill = (page: Page) => page.locator(`${WINDOW} .level-gauge:has([data-event-mousedown="option_gauge(1)"]) .level-gauge-fill`);
const tab = (page: Page, index: number) => page.locator(`${WINDOW} .option-tab-btn`).nth(index);

test('the settings window keeps the music volume and text size, and low-spec mode lowers the render scale', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test2');
  const fullRatio = await pixelRatio(page);

  await openSettings(page);

  // Audio: a press at 80 % of the music bar sets level 8 (levels 0..10 like the desktop's).
  await tab(page, 2).click();
  const musicBar = page.locator(`${WINDOW} [data-event-mousedown="option_gauge(1)"]`);
  const box = await musicBar.boundingBox();
  if (!box) throw new Error('music bar not shown');
  await page.mouse.click(box.x + box.width * 0.8, box.y + box.height / 2);
  await expect.poll(() => musicLevel(page)).toBe(8);
  await expect(musicFill(page)).toHaveClass(/level-8/);

  // Display: text size Large.
  await tab(page, 0).click();
  await page.locator(`${WINDOW} [data-event-click="option_toggle_dropdown(5)"]`).click();
  await page.locator(`${WINDOW} .option-dropdown-option`, { hasText: 'Large' }).click();
  await expect.poll(() => textScale(page)).toBe('1.2');

  // Graphics: low-spec mode draws at half the pixel ratio.
  await tab(page, 1).click();
  await page.locator(`${WINDOW} #hnmo_low_spec`).click();
  await expect(page.locator(`${WINDOW} #hnmo_low_spec .checkbox-box`)).toHaveClass(/checked/);
  await expect.poll(() => pixelRatio(page)).toBeCloseTo(fullRatio * 0.5, 1);
  // ... and with the near view distance the camera sees less far.
  await expect.poll(() => page.evaluate(() => (window as unknown as { __game: { camera: { far: number } } }).__game.camera.far)).toBeLessThan(9000);
  await page.screenshot({ path: test.info().outputPath('low-spec.png') });

  // After a reload everything is applied again before the window opens, and the window shows it.
  await page.reload();
  await expect.poll(() => textScale(page), { timeout: 60_000 }).toBe('1.2');
  await expect.poll(() => musicLevel(page)).toBe(8);
  await expect.poll(() => pixelRatio(page)).toBeCloseTo(fullRatio * 0.5, 1);
  await openSettings(page);
  await expect(page.locator(`${WINDOW} [data-event-click="option_toggle_dropdown(5)"]`)).toHaveText('Large');
  await tab(page, 2).click();
  await expect(musicFill(page)).toHaveClass(/level-8/);

  // Low-spec mode off again: full pixel ratio.
  await tab(page, 1).click();
  await page.locator(`${WINDOW} #hnmo_low_spec`).click();
  await expect.poll(() => pixelRatio(page)).toBeCloseTo(fullRatio, 1);
  await page.keyboard.press('Escape');
  await expect(page.locator(WINDOW)).toHaveCount(0);
  await expectNoPageErrors(errors);
});
