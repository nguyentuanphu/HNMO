// Quest window (my_quest_info.rml, T): CashShop / docked exclusion so bottom buttons (#btn_exit,
// #btn_quest_open, #btn_quest_giveup) are never covered, and locked buttons give clear feedback.
import { test, expect, type Page } from '@playwright/test';
import { chatText, clickAt, dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

const OPEN = { timeout: 60_000 };

async function clickQuestButton(page: Page, selector: string): Promise<void> {
  const box = await page.locator(`.rml-my_quest_info ${selector}`).boundingBox();
  if (!box) throw new Error(`Missing Quest button: ${selector}`);
  const center = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
  const occluded = await page.evaluate(({ x, y, sel }) => {
    const top = document.elementFromPoint(x, y);
    if (!top) return 'elementFromPoint returned null';
    const host = document.querySelector('.rml-my_quest_info');
    if (top !== host) return `covered by ${top.tagName}.${top.className}`;
    const btn = host?.shadowRoot?.querySelector(sel);
    if (!btn) return `missing inner ${sel}`;
    const innerTop = host?.shadowRoot?.elementFromPoint(x, y);
    return innerTop && (innerTop === btn || btn.contains(innerTop)) ? null : `shadow covered by ${innerTop?.tagName}#${innerTop?.id}`;
  }, { ...center, sel: selector });
  expect(occluded, `${selector} should not be covered`).toBeNull();
  await clickAt(page, center);
}

test('Quest Window closes CashShop overlay and all tabs + bottom buttons respond', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test400');
  await dismissOnboarding(page);

  // Open CashShop (X) first — previously zIndex 45 CashShop stayed open behind/over Quest Window (T).
  await page.keyboard.press('x');
  await expect(page.locator('.rml-in_game_shop #panel')).toBeVisible(OPEN);

  // Press T: Quest Window opens AND CashShop closes so bottom buttons are never occluded.
  await page.keyboard.press('t');
  await expect(page.locator('.rml-my_quest_info #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-in_game_shop')).toHaveCount(0);

  // Tabs switch cleanly.
  await page.locator('.rml-my_quest_info .tab-btn-jobchange').click();
  await expect(page.locator('.rml-my_quest_info .tab-btn-jobchange')).toHaveClass(/\bactive\b/);
  await page.locator('.rml-my_quest_info .tab-btn-castletemple').click();
  await expect(page.locator('.rml-my_quest_info .tab-btn-castletemple')).toHaveClass(/\bactive\b/);
  await page.locator('.rml-my_quest_info .tab-btn-quest').click();
  await expect(page.locator('.rml-my_quest_info .tab-btn-quest')).toHaveClass(/\bactive\b/);

  // Bottom Scroll (#btn_quest_open) & Book (#btn_quest_giveup) are unoccluded and explain when locked.
  await clickQuestButton(page, '#btn_quest_open');
  await expect.poll(() => chatText(page)).toMatch(/quest/i);
  const afterOpenLen = (await chatText(page)).length;

  await clickQuestButton(page, '#btn_quest_giveup');
  await expect.poll(async () => (await chatText(page)).length).toBeGreaterThan(afterOpenLen);

  // Bottom Exit (#btn_exit) closes Quest Window on real click.
  await clickQuestButton(page, '#btn_exit');
  await expect(page.locator('.rml-my_quest_info')).toHaveCount(0);

  await expectNoPageErrors(errors);
});
