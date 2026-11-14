// Using items like on the desktop: the potion slots Q W E R (bound from the server's key
// configuration, Ctrl + key over a bag item rebinds), right-clicking a potion in the bag, and the
// (i) guide.
import { test, expect, type Page } from '@playwright/test';
import { BAG, BAG_GRID_ORIGIN, enterWorld, expectNoPageErrors, gridSlotPoint, openInventory } from './helpers.js';

interface UseView {
  inventory: { items: Map<number, { group: number; number: number; durability: number }> };
  hotkeys: { types: number[]; levels: number[] };
}

const POTION = 14 * 512;
const HEALING = [0, 1, 2, 3];
const MANA = [4, 5, 6];

// How many potions of these numbers the bag holds (each counts its durability, as the slots do).
function potions(page: Page, numbers: number[]): Promise<number> {
  return page.evaluate((wanted) => {
    let total = 0;
    for (const [slot, item] of (window as unknown as { __game: UseView }).__game.inventory.items) {
      if (slot >= 12 && item.group === 14 && wanted.includes(item.number)) total += item.durability;
    }
    return total;
  }, numbers);
}

function bagSlotOf(page: Page, numbers: number[]): Promise<number | null> {
  return page.evaluate((wanted) => {
    for (const [slot, item] of (window as unknown as { __game: UseView }).__game.inventory.items) {
      if (slot >= 12 && item.group === 14 && wanted.includes(item.number)) return slot;
    }
    return null;
  }, numbers);
}

const slotCount = (page: Page, slot: number) => page.locator(`.rml-main_frame #item_slot_${slot} .item-stack-label`).innerText();

test('potions on Q W E R and right-click use in the bag', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test1'); // test1's first character carries healing, mana and shield potions
  await expect(page.locator('.rml-main_frame #item_slots')).toBeVisible();

  // The key configuration arrived; Q drinks healing potions and shows how many are left.
  const healing = await potions(page, HEALING);
  expect(healing, 'test1 needs a healing potion (each local run drinks one)').toBeGreaterThan(0);
  await expect.poll(() => slotCount(page, 0)).toBe(String(healing));
  await expect(page.locator('.rml-main_frame #item_slot_0 canvas.item-icon')).toBeVisible();

  // Ctrl + W over a mana potion binds W to the mana potions.
  await openInventory(page);
  const manaSlot = await bagSlotOf(page, MANA);
  expect(manaSlot).not.toBeNull();
  const manaPoint = await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, manaSlot!);
  await page.mouse.move(manaPoint.x, manaPoint.y, { steps: 4 });
  await page.keyboard.press('Control+w');
  await expect.poll(() => page.evaluate(() => (window as unknown as { __game: UseView }).__game.hotkeys.types[1])).toBeGreaterThanOrEqual(POTION + 4);
  const mana = await potions(page, MANA);
  await expect.poll(() => slotCount(page, 1)).toBe(String(mana));
  await page.screenshot({ path: 'test-results/potion-slots.png' });

  // Q drinks one healing potion (ConsumeItemRequest); the server takes it from the bag.
  await page.mouse.move(640, 300);
  await page.keyboard.press('q');
  await expect.poll(() => potions(page, HEALING)).toBe(healing - 1);
  await expect.poll(() => slotCount(page, 0)).toBe(String(healing - 1));

  // Right-clicking a mana potion in the bag drinks it too (after the server's potion cooldown).
  await page.waitForTimeout(2_000);
  const manaSlotNow = await bagSlotOf(page, MANA);
  const point = await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, manaSlotNow!);
  await page.mouse.move(point.x, point.y, { steps: 4 });
  await page.mouse.click(point.x, point.y, { button: 'right' });
  await expect.poll(() => potions(page, MANA)).toBe(mana - 1);
  await expectNoPageErrors(errors);
});

test('the (i) button opens the guide', async ({ page }) => {
  await page.goto('/');
  await page.click('#loginGuide'); // on the login screen; in the game the (i) button next to the name
  await expect(page.locator('#guide iframe')).toBeVisible();
  await page.click('#guide .close');
  await enterWorld(page, 'test300');
  await page.click('#guideButton');
  const guide = page.frameLocator('#guide iframe');
  await expect(guide.locator('h1')).toHaveText('HNMO Guide');
  await expect(guide.locator('#controls')).toBeVisible();
  await page.screenshot({ path: 'test-results/guide.png' });
  await page.keyboard.press('Escape');
  await expect(page.locator('#guide')).toBeHidden();
});
