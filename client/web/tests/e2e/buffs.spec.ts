// The buff strip like the desktop's: the hero's buffs as icons above the HUD; hovering one shows its
// name and description. No test character can get a buff from the server yet (no elf buff skill,
// no buff item), so the buff is set as MagicEffectStatus would set it.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface BuffGame { buffs: { set(id: number, effect: number, on: boolean): void }; online: { playerId: number } }
type Win = Window & { __game: BuffGame };

test('a buff shows on the buff strip with its name', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test6');
  // Buff 1, "Increase Attack Power" (Data/Buffs/Buffs.json), on the hero.
  await page.evaluate(() => { const g = (window as unknown as Win).__game; g.buffs.set(g.online.playerId, 1, true); });
  const icon = page.locator('.rml-buff_strip .buff-slot').first();
  await expect(icon).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.rml-buff_strip .buff-icon-art').first()).toHaveAttribute('data-decorator', 'image(atlas1-0)');
  const box = (await icon.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 4 });
  await expect(page.locator('.rml-tooltip')).toContainText('Increase Attack Power');
  await page.screenshot({ path: 'test-results/buffs.png' });
  // Off again: the icon goes.
  await page.evaluate(() => { const g = (window as unknown as Win).__game; g.buffs.set(g.online.playerId, 1, false); });
  await expect(icon).toBeHidden();
  await expectNoPageErrors(errors);
});
