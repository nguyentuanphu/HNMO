// Ultimate Autoplay HUD bar (#hnmo-ultimate-hud): Start / Town / Ultimate buttons must keep
// stable DOM elements across live refresh ticks (no 500ms innerHTML rebuild detaching clicks) and
// respond cleanly to real pointer clicks.
import { test, expect } from '@playwright/test';
import { chatText, clickAt, dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

test('Ultimate HUD bar buttons (Start, Town, Ultimate) respond reliably across refresh ticks', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));

  await enterWorld(page, 'test400');
  await dismissOnboarding(page);

  const hud = page.locator('#hnmo-ultimate-hud');
  await expect(hud).toBeVisible({ timeout: 30_000 });

  const startBtn = hud.locator('[data-uap-hud="toggle"]');
  const townBtn = hud.locator('[data-uap-hud="town"]');
  const openBtn = hud.locator('[data-uap-hud="open"]');

  // Button DOM elements must survive refresh ticks (500ms interval) without detaching mid-click.
  await page.evaluate(() => {
    const h = document.querySelector('#hnmo-ultimate-hud');
    (window as unknown as { __uapBtns?: Element[] }).__uapBtns = [
      h?.querySelector('[data-uap-hud="toggle"]')!,
      h?.querySelector('[data-uap-hud="town"]')!,
      h?.querySelector('[data-uap-hud="open"]')!,
    ];
  });
  await page.waitForTimeout(1200);
  const sameNodes = await page.evaluate(() => {
    const prev = (window as unknown as { __uapBtns?: Element[] }).__uapBtns ?? [];
    return prev.length === 3 && prev.every((el) => el && el.isConnected);
  });
  expect(sameNodes, 'Ultimate HUD buttons detached during refresh tick').toBe(true);

  // Ultimate button opens Window 1 directly on the Ultimate tab, and Close dismisses it.
  const openBox = (await openBtn.boundingBox())!;
  await clickAt(page, { x: openBox.x + openBox.width / 2, y: openBox.y + openBox.height / 2 });
  const helperCfg = page.locator('.rml-mu_helper_config');
  await expect(helperCfg.locator('#panel')).toBeVisible();
  await expect(helperCfg.locator('#tab_4')).toHaveClass(/\bactive\b/);
  await expect(helperCfg.locator('#hnmo_page_ultimate')).toBeVisible();
  await expect(page.locator('#hnmo-ultimate-autoplay-panel')).toHaveCount(0);
  await helperCfg.locator('#btn_exit').click();
  await expect(helperCfg).toHaveCount(0);

  // Town starts the town trip and notifies in chat.
  const townBox = (await townBtn.boundingBox())!;
  await clickAt(page, { x: townBox.x + townBox.width / 2, y: townBox.y + townBox.height / 2 });
  await expect.poll(() => chatText(page)).toContain('returning to Lorencia');

  // Start responds cleanly (starts hunting outside safezone or shows safezone notice).
  const startBox = (await startBtn.boundingBox())!;
  await clickAt(page, { x: startBox.x + startBox.width / 2, y: startBox.y + startBox.height / 2 });
  await expect.poll(() => chatText(page)).toContain('Auto play can’t start in a safe zone.');

  await expectNoPageErrors(errors);
});
