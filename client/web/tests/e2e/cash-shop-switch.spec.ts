// The cash shop switch of a season (owner 2026-11-28, classic play without pay to win): the admin panel's Cash Shop
// Catalog page turns the cash shop off; a player who enters the game keeps the SHOP button in the frame (no gap) but it,
// and X, open nothing, and the phone menus' entries are gone. Turned on again, the button opens the shop. The switch is always turned on again at the end.
import { test, expect, type Page, type Browser } from '@playwright/test';
import { enterWorld } from './helpers.js';

const ADMIN = process.env.ADMIN_URL ?? 'http://127.0.0.1:8095';

async function setSwitch(browser: Browser, on: boolean): Promise<void> {
  const admin = await browser.newPage();
  try {
    await admin.goto(`${ADMIN}/cash-shop-catalog`);
    const toggle = admin.locator('#cash-shop-enabled');
    await expect(toggle).toBeVisible({ timeout: 60_000 });
    await expect(admin.locator('#cash-shop-note')).toContainText(/classic/i);
    // The page answers clicks once its live connection is up: click until the state shows.
    await expect(async () => {
      if ((await toggle.isChecked()) !== on) await toggle.click();
      await expect(admin.locator('#cash-shop-state')).toHaveText(on ? /On|Bật/ : /Off|Tắt/, { timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    await admin.screenshot({ path: `test-results/cash-shop-switch-${on ? 'on' : 'off'}.png` });
  } finally {
    await admin.close();
  }
}

async function shopButtonShown(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    const button = document.querySelector('.rml-main_frame')?.shadowRoot?.querySelector<HTMLElement>('#btn_cshop');
    return !!button && getComputedStyle(button).visibility !== 'hidden';
  });
}

async function clickShopButton(page: Page): Promise<void> {
  await page.locator('.rml-main_frame #btn_cshop').click();
}

test('a season whose cash shop is off keeps the SHOP button, but it and X open nothing', async ({ page, browser }) => {
  test.setTimeout(8 * 60_000);
  try {
    await setSwitch(browser, false);
    await enterWorld(page, 'test5');
    await expect(page.locator('.rml-main_frame #hud_layout')).toBeVisible({ timeout: 60_000 });
    await page.waitForTimeout(3_000);
    await expect(page.locator('body.hnmo-no-cash-shop')).toHaveCount(1);
    // The button stays in the frame (no gap) but opens nothing; neither does X.
    expect(await shopButtonShown(page)).toBe(true);
    await clickShopButton(page);
    await page.keyboard.press('x');
    await page.waitForTimeout(2_000);
    await expect(page.locator('.rml-in_game_shop')).toHaveCount(0);
    await expect(page.locator('.message-dialog')).toHaveCount(0);
    await page.screenshot({ path: 'test-results/cash-shop-off-game.png' });
    // No free coins either: the missions window (J) promises none.
    await page.keyboard.press('j');
    const missionsText = () => page.evaluate(() => document.querySelector('.rml-hnmo_missions')?.shadowRoot?.textContent ?? '');
    await expect.poll(async () => (await missionsText()).length, { timeout: 60_000 }).toBeGreaterThan(40);
    await page.waitForTimeout(2_000);
    expect(await missionsText()).not.toMatch(/Goblin Point|W Coin/);
    await page.screenshot({ path: 'test-results/cash-shop-off-missions.png' });
    await page.keyboard.press('j');
  } finally {
    await setSwitch(browser, true);
  }

  // On again: a player entering the game has the button.
  await enterWorld(page, 'test5');
  await expect(page.locator('.rml-main_frame #hud_layout')).toBeVisible({ timeout: 60_000 });
  await expect(page.locator('body.hnmo-no-cash-shop')).toHaveCount(0, { timeout: 30_000 });
  expect(await shopButtonShown(page)).toBe(true);
  await clickShopButton(page);
  await expect(page.locator('.rml-in_game_shop #panel')).toBeVisible({ timeout: 90_000 });
});
