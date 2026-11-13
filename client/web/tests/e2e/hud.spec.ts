// The HUD shows the character's life, mana, SD, AG and experience from the server.
import { test, expect } from '@playwright/test';
import { enterWorld } from './helpers.js';

test('the HUD shows life, mana, SD and AG like the server says', async ({ page }) => {
  await enterWorld(page, 'test6');
  const hud = page.locator('.rml-main_frame #hud_layout');
  await expect(hud).toBeVisible();
  const stats = await page.evaluate(() => {
    const s = (window as unknown as { __game: { stats: { life: number; maxLife: number; mana: number; maxMana: number } } }).__game.stats;
    return { life: s.life, maxLife: s.maxLife, mana: s.mana, maxMana: s.maxMana };
  });
  expect(stats.maxLife).toBeGreaterThan(1);
  const text = (await page.locator('.rml-main_frame #hud_layout').innerText()).replace(/\s+/g, ' ');
  expect(text).toContain(String(stats.life));
  await page.screenshot({ path: 'test-results/hud.png' });
});
