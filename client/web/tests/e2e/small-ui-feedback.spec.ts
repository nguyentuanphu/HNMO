// Small UI feedback items:
// 9. Vault zen uses an in-game dialog instead of window.prompt().
// 10. Alt toggles always-show ground item names.
// 11. Ultimate HUD can be minimized and expanded.
import { test, expect } from '@playwright/test';
import { dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

interface UiFeedbackGame {
  hero: { cell: { x: number; y: number } };
  ground: { namesShown: boolean };
  itemWindows: { askMoney(toVault: boolean): Promise<void> };
  trade: { askGold(): Promise<void> };
  connection: { dispatch(bytes: Uint8Array): void };
}

test('vault and trade zen in-game dialogs, Alt ground-names toggle, and Ultimate HUD minimize', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test0');
  await dismissOnboarding(page);

  // 9a. Vault zen uses .vault-zen-dialog instead of browser prompt()
  let promptCalled = false;
  page.on('dialog', () => { promptCalled = true; });
  await page.evaluate(() => { void (window as unknown as { __game: UiFeedbackGame }).__game.itemWindows.askMoney(true); });
  await expect(page.locator('.vault-zen-dialog #panel')).toBeVisible({ timeout: 30_000 });
  expect(promptCalled).toBe(false);
  await page.locator('.vault-zen-dialog #gcd_btn_cancel').click();
  await expect(page.locator('.vault-zen-dialog')).toHaveCount(0);

  // 9b. Trade zen uses .trade-zen-dialog instead of browser prompt()
  await page.evaluate(() => { void (window as unknown as { __game: UiFeedbackGame }).__game.trade.askGold(); });
  await expect(page.locator('.trade-zen-dialog #panel')).toBeVisible({ timeout: 30_000 });
  expect(promptCalled).toBe(false);
  await page.locator('.trade-zen-dialog #gcd_btn_cancel').click();
  await expect(page.locator('.trade-zen-dialog')).toHaveCount(0);

  // 10. Alt alone toggles always-show names; Alt+Z chord does NOT toggle them
  await page.evaluate(async () => {
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    const g = (window as unknown as { __game: UiFeedbackGame }).__game;
    g.connection.dispatch(write('ServerToClient', 'MoneyDroppedExtended', { IsFreshDrop: true, Id: 0x2234, PositionX: g.hero.cell.x, PositionY: g.hero.cell.y, Amount: 1000 }));
  });
  await page.keyboard.press('Alt+z');
  expect(await page.evaluate(() => (window as unknown as { __game: UiFeedbackGame }).__game.ground.namesShown)).toBe(false);
  await page.keyboard.press('Alt+z');
  await page.keyboard.press('Alt');
  expect(await page.evaluate(() => (window as unknown as { __game: UiFeedbackGame }).__game.ground.namesShown)).toBe(true);
  await page.keyboard.press('Alt');
  expect(await page.evaluate(() => (window as unknown as { __game: UiFeedbackGame }).__game.ground.namesShown)).toBe(false);

  // 11. Floating Ultimate HUD is hidden by default and, once opted in, can be minimized and expanded.
  const hud = page.locator('#hnmo-ultimate-hud');
  await expect(hud).toBeHidden({ timeout: 30_000 });
  await page.keyboard.press('Alt+z');
  const helperCfg = page.locator('.rml-mu_helper_config');
  await expect(helperCfg.locator('#panel')).toBeVisible({ timeout: 30_000 });
  await helperCfg.locator('#hnmo_page_ultimate input[data-uap-key="showUltimateHud"]').click();
  await helperCfg.locator('#btn_save').click();
  await expect(helperCfg).toHaveCount(0);
  await expect(hud).toBeVisible({ timeout: 30_000 });
  const minBtn = hud.locator('[data-uap-hud="minimize"]');
  await minBtn.click();
  await expect(hud).toHaveAttribute('data-minimized', 'true');
  await expect(hud.locator('[data-uap-hud="open"]')).toBeHidden();
  await minBtn.click();
  await expect(hud).toHaveAttribute('data-minimized', 'false');
  await expect(hud.locator('[data-uap-hud="open"]')).toBeVisible();

  await expectNoPageErrors(errors);
});
