// Ultimate Autoplay with a real server: a player without VIP gets the admin's Free/VIP policy on entering the world
// (the VIP-only functions are locked in the Alt+Z panel), and when the admin panel's "Ultimate Autoplay" page
// switches a function to Free and saves, the player in the game gets it at once, without entering again.
// Needs the admin panel on the host computer (ADMIN_URL, default http://127.0.0.1:8095), signed in or open.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Extras { policy: { enabled: boolean; isVip: boolean }; isFeatureAllowed(key: string): boolean; isFeatureVipLocked(key: string): boolean }
type Win = Window & { __game: { autoPlayExtras?: Extras } };

const adminUrl = (process.env.ADMIN_URL ?? 'http://127.0.0.1:8095').replace(/\/$/, '');
const allowed = (page: Page, key: string) => page.evaluate((k) => (window as unknown as Win).__game.autoPlayExtras?.isFeatureAllowed(k) ?? null, key);
const vipLocked = (page: Page, key: string) => page.evaluate((k) => (window as unknown as Win).__game.autoPlayExtras?.isFeatureVipLocked(k) ?? null, key);

/** Sets the potions function on the admin page and saves; the page answers once its live connection is up. */
async function setPotions(admin: Page, mode: 'Free' | 'VipOnly'): Promise<void> {
  await admin.goto(`${adminUrl}/ultimate-autoplay`, { waitUntil: 'networkidle' });
  await expect(admin.locator('#uap-mode-potions')).toBeVisible({ timeout: 60_000 });
  await admin.selectOption('#uap-mode-potions', mode === 'Free' ? '1' : '2');
  await admin.click('#uap-admin-save');
  await expect(admin.locator('.blazored-toast, .toast, [role=alert]').filter({ hasText: /Saved|Đã lưu/ }).first()).toBeVisible({ timeout: 30_000 });
}

test('the admin page sets Free or VIP, and a player in the game gets it at once', async ({ page, browser }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const admin = await (await browser.newContext()).newPage();
  await setPotions(admin, 'VipOnly');
  await admin.screenshot({ path: 'test-results/ultimate-autoplay-admin.png', fullPage: true });

  await enterWorld(page, 'test9');
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.autoPlayExtras?.policy.enabled ?? false), { timeout: 30_000 }).toBe(true);
  expect(await page.evaluate(() => (window as unknown as Win).__game.autoPlayExtras?.policy.isVip)).toBe(false);
  expect(await allowed(page, 'sparkleGuide')).toBe(true);
  expect(await allowed(page, 'manaSdPotions')).toBe(false);
  expect(await vipLocked(page, 'manaSdPotions')).toBe(true);
  await page.keyboard.press('Alt+z');
  const helperCfg = page.locator('.rml-mu_helper_config');
  await expect(helperCfg.locator('#panel')).toBeVisible();
  await expect(helperCfg.locator('#tab_4')).toHaveClass(/\bactive\b/);
  await expect(helperCfg.locator('#hnmo_page_ultimate')).toContainText(/VIP ONLY|CHỈ VIP/);
  await page.screenshot({ path: 'test-results/ultimate-autoplay-locked.png' });
  await page.keyboard.press('Alt+z');
  await expect(helperCfg).toHaveCount(0);

  // The admin makes the potions free: the player gets it without entering the world again or opening the panel.
  await setPotions(admin, 'Free');
  await expect.poll(() => allowed(page, 'manaSdPotions'), { timeout: 30_000 }).toBe(true);
  expect(await vipLocked(page, 'manaSdPotions')).toBe(false);

  await setPotions(admin, 'VipOnly');
  await expect.poll(() => allowed(page, 'manaSdPotions'), { timeout: 30_000 }).toBe(false);
  await admin.context().close();
  await expectNoPageErrors(errors);
});
