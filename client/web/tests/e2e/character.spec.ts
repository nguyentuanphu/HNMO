// The character window (C): the hero's name, level and stats, and a stat's "+" spending a level-up point.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface StatsView { level: number; levelUpPoints: number; strength: number; agility: number }

function heroStats(page: Page): Promise<StatsView> {
  return page.evaluate(() => {
    const s = (window as unknown as { __game: { stats: StatsView } }).__game.stats;
    return { level: s.level, levelUpPoints: s.levelUpPoints, strength: s.strength, agility: s.agility };
  });
}

test('the character window shows the hero and spends a point on a stat', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  // test3's first character (test3Dk, level 31) has level-up points to spend.
  await enterWorld(page, 'test3');
  const name = await page.evaluate(() => (window as unknown as { __game: { hero: { name: string } } }).__game.hero.name);
  const before = await heroStats(page);
  expect(before.levelUpPoints).toBeGreaterThan(0);

  await page.keyboard.press('c');
  const window = page.locator('.rml-character_info');
  await expect(window.locator('#name')).toHaveText(name);
  await expect(window.locator('.level-text')).toContainText(`Level: ${before.level}`);
  await expect(window.locator('.attr-value-str')).toHaveText(String(before.strength));
  await expect(window.locator('.levelup-point-text')).toHaveText(`Point: ${before.levelUpPoints}`);
  await expect(window.locator('.attr-lines-str span').first()).toContainText('Dmg');
  await page.screenshot({ path: 'test-results/character.png' });

  // Strength's "+": the server answers and the window shows one more strength and one point less.
  await window.locator('#btn_stat_str').click();
  await expect(window.locator('.attr-value-str')).toHaveText(String(before.strength + 1));
  await expect(window.locator('.levelup-point-text')).toHaveText(`Point: ${before.levelUpPoints - 1}`);
  const after = await heroStats(page);
  expect(after.strength).toBe(before.strength + 1);
  expect(after.levelUpPoints).toBe(before.levelUpPoints - 1);

  // The close button closes it.
  await window.locator('#btn_exit').click();
  await expect(page.locator('.rml-character_info')).toHaveCount(0);
  await expectNoPageErrors(errors);
});
