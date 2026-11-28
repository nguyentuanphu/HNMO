import { expect, test, type Locator, type Page } from '@playwright/test';
import { dismissOnboarding, enterWorld } from '../e2e/helpers.js';

test.skip(process.env.SHORTCUTS_IN_GAME !== '1', 'Requires an isolated demo server and bridge');

const option = (page: Page, letter: string) => page.evaluate((letter) => {
  window.dispatchEvent(new KeyboardEvent('keydown', {
    code: 'Key' + letter, key: 'Dead', altKey: true, bubbles: true, cancelable: true,
  }));
}, letter);

async function clickHudControl(page: Page, control: Locator): Promise<void> {
  await expect(control).toBeVisible();
  // The live HUD follows the Autoplay bar every frame. Use its visible hit box for the pointer.
  const bounds = await control.boundingBox();
  expect(bounds).not.toBeNull();
  await page.mouse.click(bounds!.x + bounds!.width / 2, bounds!.y + bounds!.height / 2);
}

test.beforeEach(async ({ page }) => {
  // Keep software-rendered runs responsive without changing the shortcut or effects settings.
  await page.addInitScript(() => localStorage.setItem('hnmo.settings', JSON.stringify({
    quality: { renderScale: 0.5, fpsCap: 30 },
  })));
  await enterWorld(page, 'test9');
  await dismissOnboarding(page);
});

test('desktop HUD opens bug reports and Escape only closes its drawer', async ({ page }) => {
  const tools = page.locator('#hudToolsToggle');
  const bug = page.locator('#hnmoBugButton');
  await expect(bug).toBeHidden();
  await clickHudControl(page, tools);
  await expect(bug).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(tools).toHaveAttribute('aria-expanded', 'false');
  await expect(page.locator('.rml-sys_menu')).toHaveCount(0);
  await clickHudControl(page, tools);
  await clickHudControl(page, bug);
  await expect(page.locator('#hnmo-bug-panel')).toBeVisible();
  await expect(tools).toHaveAttribute('aria-expanded', 'false');
});

test('Option code opens game windows and changes performance settings', async ({ page }) => {
  for (const [letter, selector] of [
    ['A', '#hnmo-arena-panel'],
    ['B', '#hnmo-bug-panel'],
    ['M', '#hnmo-giftbox-panel'],
    ['U', '#hnmo-vip-panel'],
  ]) {
    await option(page, letter);
    await expect(page.locator(selector)).toBeVisible({ timeout: 60_000 });
    await option(page, letter);
    await expect(page.locator(selector)).toBeHidden();
  }
  const quality = (key: string) => page.evaluate((key) => JSON.parse(localStorage.getItem('hnmo.settings') ?? '{}').quality?.[key], key);
  for (const [letter, setting] of [['E', 'reducedEffects'], ['O', 'hideOtherPlayers'], ['W', 'hideWingsPets']]) {
    await option(page, letter);
    await expect.poll(() => quality(setting)).toBe(true);
    await option(page, letter);
    await expect.poll(() => quality(setting)).toBe(false);
  }
  await option(page, 'P');
  for (const setting of ['reducedEffects', 'hideOtherPlayers', 'hideWingsPets']) {
    await expect.poll(() => quality(setting)).toBe(true);
  }
});

test('Window 1 uses Z, Shift+Z and Option+Z and starts with the Ultimate HUD hidden', async ({ page }) => {
  const config = page.locator('.rml-mu_helper_config');
  const hud = page.locator('#hnmo-ultimate-hud');
  await expect(hud).toBeHidden();
  await page.keyboard.press('z');
  await expect(config.locator('#tab_0')).toHaveClass(/\bactive\b/, { timeout: 60_000 });
  await page.keyboard.press('Shift+z');
  await expect(config.locator('#tab_3')).toHaveClass(/\bactive\b/);
  await option(page, 'Z');
  await expect(config.locator('#tab_4')).toHaveClass(/\bactive\b/);
  await expect(config).toHaveCount(1);
  await config.locator('input[data-uap-key="showUltimateHud"]').click();
  await config.locator('#btn_save').click();
  await expect(config).toHaveCount(0);
  await expect(hud).toBeVisible();
});

test('J and U do not collide with shifted shortcuts and composing chat keeps the keys', async ({ page }) => {
  await page.keyboard.press('j');
  await expect(page.locator('.rml-hnmo_missions')).toBeVisible({ timeout: 60_000 });
  await expect(page.locator('.rml-chat_command')).toHaveCount(0);
  await page.keyboard.press('j');
  await page.keyboard.press('Shift+j');
  await expect(page.locator('.rml-chat_command #panel')).toBeVisible({ timeout: 60_000 });
  await page.keyboard.press('Escape');

  await page.keyboard.press('u');
  await expect.poll(() => page.evaluate(() => Boolean((window as unknown as { __game: { windowMenu: { isOpen: boolean } } }).__game.windowMenu.isOpen))).toBe(true);
  await page.keyboard.press('u');
  await page.keyboard.press('Shift+u');
  await expect(page.locator('#hnmo-mount-chain-panel')).toBeVisible();
  await page.keyboard.press('Escape');
  await page.keyboard.press('Shift+t');
  await expect(page.locator('#hnmo_topup_milestones')).toBeVisible();
  await page.keyboard.press('Escape');

  await page.waitForFunction(() => Boolean((window as unknown as { __game: { chat: { input: unknown } } }).__game.chat.input));
  await page.keyboard.press('Enter');
  const chat = page.locator('.rml-chat_input #chat_field');
  await expect(chat).toBeFocused();
  await chat.blur();
  await page.keyboard.press('i');
  await expect(page.locator('.rml-inventory')).toHaveCount(0);
  await chat.focus();
  await chat.fill('Chào bạn');
  await chat.evaluate((e) => e.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true, composed: true })));
  await page.keyboard.press('Escape');
  await page.keyboard.press('Enter');
  await expect(chat).toBeFocused();
  await expect(chat).toHaveValue('Chào bạn');
  await chat.evaluate((e) => e.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true, composed: true })));
  await page.keyboard.press('Tab');
  await expect(page.locator('.rml-chat_input #whisper_field')).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(chat).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(chat).toBeHidden();
  await expect(page.locator('.rml-chat_log .chat-line--chat').last()).toContainText('Chào bạn');
});
