// Jewels like on the desktop: a Jewel of Bless picked up and dropped on an item below +6 is used on
// it (ConsumeItemRequest with the target slot); the item goes up a level and the jewel is gone. The
// server takes jewels only on items in the bag.
import { test, expect, type Page } from '@playwright/test';
import { BAG, BAG_GRID_ORIGIN, clickAt, enterWorld, expectNoPageErrors, gridSlotPoint, openInventory } from './helpers.js';

interface JewelItem { group: number; number: number; level: number; width: number; height: number }
interface JewelGame { inventory: { items: Map<number, JewelItem> } }
type Win = Window & { __game: JewelGame };

const items = (page: Page) => page.evaluate(() => [...(window as unknown as Win).__game.inventory.items].map(([slot, i]) => ({ slot, group: i.group, number: i.number, level: i.level })));
const blessCount = async (page: Page) => (await items(page)).filter((i) => i.group === 14 && i.number === 13).length;

test('a Jewel of Bless dropped on an item in the bag raises its level', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test1'); // Jewels of Bless (14, 13) and a helm in the bag
  await openInventory(page);
  const all = await items(page);
  // An armour piece below +6 in the bag (the server takes jewels only on bag items).
  const target = all.find((i) => i.slot >= 12 && i.group >= 7 && i.group <= 11 && i.level < 6);
  const bless = all.find((i) => i.slot >= 12 && i.group === 14 && i.number === 13);
  test.skip(!target || !bless, 'no armour below +6 in the bag or no Jewel of Bless left');
  const box = await page.evaluate((slot) => { const i = (window as unknown as Win).__game.inventory.items.get(slot)!; return { width: i.width, height: i.height }; }, target!.slot);
  // The jewel on it.
  const jewels = await blessCount(page);
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, bless!.slot));
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, target!.slot, box));
  await expect.poll(async () => (await items(page)).find((i) => i.slot === target!.slot)?.level, { timeout: 90_000 }).toBe(target!.level + 1);
  await expect.poll(() => blessCount(page)).toBe(jewels - 1);
  await expectNoPageErrors(errors);
});
