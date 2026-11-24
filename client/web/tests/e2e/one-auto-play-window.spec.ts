// Unified Auto Play window (Part 6): Window 1 (mu_helper_config) merges the desktop helper
// settings (Hunting / Obtaining / Other Settings) with Extras (Shift+Z) and Ultimate (Alt+Z) as
// two new web-side tabs, fitted cleanly to Window 1, with unified Save, EN/VI localization,
// preserved Free/VIP locks, and the compact Ultimate HUD bar opening the Ultimate tab.
import { test, expect } from '@playwright/test';
import { dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

interface GameAutoView {
  __game: {
    hero: { name: string };
    autoPlay: { config: { huntingRange: number; obtainingRange: number; maxSecondsAway: number } };
    autoPlayExtras: {
      settings: { returnWhenBagFull: boolean; potionsToBuy: number; stopAfterMinutes: number; showUltimateHud: boolean; sparkleGuide: boolean; patrolIdleSeconds: number };
      policy: { enabled: boolean; isVip: boolean };
    };
  };
}

const OPEN = { timeout: 90_000 };

test('Window 1 unifies five tabs, shortcuts, Save, switcher, EN/VI texts, and HUD bar', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));

  await enterWorld(page, 'test9');
  await dismissOnboarding(page);

  const win = page.locator('.rml-mu_helper_config');
  const panel = win.locator('#panel');
  const oldExtras = page.locator('.auto-play-extras');
  const oldUltimate = page.locator('#hnmo-ultimate-autoplay-panel');
  const hudOpen = page.locator('#hnmo-ultimate-hud [data-uap-hud="open"]');
  await expect(hudOpen).toBeVisible({ timeout: 30_000 });

  // 1. Z opens Window 1 on Tab 0 (Hunting); clicking across all 5 tabs stays inside Window 1 and captures screenshots.
  await page.keyboard.press('z');
  await expect(panel).toBeVisible(OPEN);
  await expect(win.locator('#tab_0')).toHaveClass(/\bactive\b/);
  await expect(oldExtras).toHaveCount(0);
  await expect(oldUltimate).toHaveCount(0);
  await page.screenshot({ path: 'test-results/one-autoplay-tab0.png' });

  await win.locator('#tab_1').click();
  await expect(win.locator('#tab_1')).toHaveClass(/\bactive\b/);
  await page.screenshot({ path: 'test-results/one-autoplay-tab1.png' });

  await win.locator('#tab_2').click();
  await expect(win.locator('#tab_2')).toHaveClass(/\bactive\b/);
  await page.screenshot({ path: 'test-results/one-autoplay-tab2.png' });

  await win.locator('#tab_3').click();
  await expect(win.locator('#tab_3')).toHaveClass(/\bactive\b/);
  await expect(win.locator('#hnmo_page_extras')).toBeVisible();
  await expect(win.locator('#tab_3_label')).toHaveText('Extras');
  await page.screenshot({ path: 'test-results/one-autoplay-tab3.png' });

  await win.locator('#tab_4').click();
  await expect(win.locator('#tab_4')).toHaveClass(/\bactive\b/);
  await expect(win.locator('#hnmo_page_ultimate')).toBeVisible();
  await expect(win.locator('#hnmo_page_ultimate')).toContainText(/VIP ONLY|CHỈ VIP/);

  // Single-column layout of Tab 4 fits within the body without horizontal overflow.
  const ultOverflow = await page.evaluate(() => {
    const pageEl = document.querySelector('.rml-mu_helper_config')?.shadowRoot?.querySelector<HTMLElement>('#hnmo_page_ultimate');
    if (!pageEl) return -1;
    return pageEl.scrollWidth - pageEl.clientWidth;
  });
  expect(ultOverflow).toBeLessThanOrEqual(2);
  await page.screenshot({ path: 'test-results/one-autoplay-tab4.png' });

  // Ultimate small switcher switches cleanly between Survival / Combat / Stats inside Tab 4.
  await win.locator('#hnmo_page_ultimate [data-uap-tab="combat"]').click();
  await expect(win.locator('#hnmo_page_ultimate #uap-add-spot')).toBeVisible();
  await win.locator('#hnmo_page_ultimate [data-uap-tab="stats"]').click();
  await expect(win.locator('#hnmo_page_ultimate #uap-reset-stats')).toBeVisible();
  await win.locator('#hnmo_page_ultimate [data-uap-tab="survival"]').click();
  await expect(win.locator('#hnmo_page_ultimate [data-uap-preset="balanced"]')).toBeVisible();

  // Z closes Window 1 from any active tab.
  await page.keyboard.press('z');
  await expect(win).toHaveCount(0);

  // 2. Shift+Z opens directly on Extras; Alt+Z switches to Ultimate; Alt+Z again closes. Old windows never open.
  await page.keyboard.press('Shift+Z');
  await expect(panel).toBeVisible(OPEN);
  await expect(win.locator('#tab_3')).toHaveClass(/\bactive\b/);
  await expect(win.locator('#hnmo_page_extras')).toBeVisible();
  await expect(oldExtras).toHaveCount(0);

  await page.keyboard.press('Alt+z');
  await expect(win.locator('#tab_4')).toHaveClass(/\bactive\b/);
  await expect(win.locator('#hnmo_page_ultimate')).toBeVisible();
  await expect(oldUltimate).toHaveCount(0);

  await page.keyboard.press('Alt+z');
  await expect(win).toHaveCount(0);

  // 3. Compact HUD bar button opens Ultimate tab directly in Window 1.
  await hudOpen.click();
  await expect(panel).toBeVisible(OPEN);
  await expect(win.locator('#tab_4')).toHaveClass(/\bactive\b/);
  await expect(oldUltimate).toHaveCount(0);

  // 4. EN / VI switch translates Extras & Ultimate tab labels and controls live.
  await page.evaluate(async () => {
    const i18n = await import('/js/i18n.js' as string) as { setLanguage(lang: string): Promise<void> };
    await i18n.setLanguage('vi');
  });
  await expect(win.locator('#tab_3_label')).toHaveText('Tuỳ chọn thêm');
  await expect(win.locator('#hnmo_page_ultimate [data-uap-tab="survival"]')).toHaveText('Sinh tồn');
  await page.evaluate(async () => {
    const i18n = await import('/js/i18n.js' as string) as { setLanguage(lang: string): Promise<void> };
    await i18n.setLanguage('en');
  });
  await expect(win.locator('#tab_3_label')).toHaveText('Extras');

  // 5. Save persists Desktop huntingRange + Extras potionsToBuy/stopAfterMinutes + Ultimate flag together.
  await win.locator('#tab_0').click();
  const initialRange = await page.evaluate(() => (window as unknown as GameAutoView).__game.autoPlay.config.huntingRange);
  await win.locator(initialRange > 1 ? '#btn_hunt_minus' : '#btn_hunt_add').click();
  const expectedRange = initialRange > 1 ? initialRange - 1 : initialRange + 1;

  await win.locator('#tab_3').click();
  const initialBagFull = await page.evaluate(() => (window as unknown as GameAutoView).__game.autoPlayExtras.settings.returnWhenBagFull);
  await win.locator('#hnmo_page_extras [data-extras-toggle="returnWhenBagFull"]').click();
  await win.locator('#hnmo_page_extras input[data-extras-num="potionsToBuy"]').fill('37');
  await win.locator('#hnmo_page_extras input[data-extras-num="stopAfterMinutes"]').fill('45');

  await win.locator('#tab_4').click();
  await win.locator('#hnmo_page_ultimate [data-uap-tab="combat"]').click();
  const initialSparkle = await page.evaluate(() => (window as unknown as GameAutoView).__game.autoPlayExtras.settings.sparkleGuide);
  await win.locator('#hnmo_page_ultimate input[data-uap-key="sparkleGuide"]').click();

  await win.locator('#btn_save').click();
  await expect(win).toHaveCount(0);

  const savedState = await page.evaluate(() => {
    const g = (window as unknown as GameAutoView).__game;
    const raw = localStorage.getItem(`hnmo.autoPlayExtras.${g.hero.name}`);
    return {
      huntingRange: g.autoPlay.config.huntingRange,
      settings: g.autoPlayExtras.settings,
      persisted: raw ? JSON.parse(raw) : null,
    };
  });
  expect(savedState.huntingRange).toBe(expectedRange);
  expect(savedState.settings.returnWhenBagFull).toBe(!initialBagFull);
  expect(savedState.settings.potionsToBuy).toBe(37);
  expect(savedState.settings.stopAfterMinutes).toBe(45);
  expect(savedState.settings.sparkleGuide).toBe(!initialSparkle);
  expect(savedState.persisted?.potionsToBuy).toBe(37);
  expect(savedState.persisted?.stopAfterMinutes).toBe(45);
  expect(savedState.persisted?.sparkleGuide).toBe(!initialSparkle);

  await expectNoPageErrors(errors);
});
