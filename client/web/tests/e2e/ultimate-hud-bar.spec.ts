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
  // Floating toolbar is hidden by default because all features live inside Window 1 (mu_helper_config).
  await expect(hud).toBeHidden({ timeout: 60_000 });

  // Enable the opt-in floating toolbar via Window 1 Tab 4 and verify its buttons stay responsive across ticks.
  await page.keyboard.press('Alt+z');
  const setupCfg = page.locator('.rml-mu_helper_config');
  await expect(setupCfg.locator('#panel')).toBeVisible({ timeout: 90_000 });
  await setupCfg.locator('#hnmo_page_ultimate input[data-uap-key="showUltimateHud"]').click();
  await setupCfg.locator('#btn_save').click();
  await expect(setupCfg).toHaveCount(0);
  await expect(hud).toBeVisible({ timeout: 60_000 });

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

  // Town requires VIP (Bronze+); grant VIP Lv 1 policy and verify clicking Town starts the town trip.
  await page.evaluate(async () => {
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    const g = (window as unknown as { __game: { connection: { dispatch(b: Uint8Array): void } } }).__game;
    g.connection.dispatch(write('ServerToClient', 'HnmoUltimateAutoPlayPolicy', {
      Json: JSON.stringify({
        v: 2, enabled: true, isVip: true, vipLevel: 1,
        features: {
          sparkleGuide: { mode: 1, allowed: true, vipRequired: false, requiredVipLevel: 0 },
          extrasTown: { mode: 2, allowed: true, vipRequired: false, requiredVipLevel: 1 },
        },
      }),
    }));
  });
  const townBox = (await townBtn.boundingBox())!;
  await clickAt(page, { x: townBox.x + townBox.width / 2, y: townBox.y + townBox.height / 2 });
  await expect.poll(() => chatText(page)).toContain('returning to Lorencia');

  // Start responds cleanly (starts hunting outside safezone or shows safezone notice).
  const startBox = (await startBtn.boundingBox())!;
  await clickAt(page, { x: startBox.x + startBox.width / 2, y: startBox.y + startBox.height / 2 });
  await expect.poll(() => chatText(page)).toContain('Auto play can’t start in a safe zone.');

  await expectNoPageErrors(errors);
});
