// Jewels like on the desktop: a Jewel of Bless picked up and dropped on a worn item below +6 is used
// on it (ConsumeItemRequest with the target slot); the item goes up a level and the jewel is gone.
import { test, expect, type Page } from '@playwright/test';
import { BAG, BAG_GRID_ORIGIN, clickAt, enterWorld, expectNoPageErrors, gridSlotPoint, openInventory, panelPoint } from './helpers.js';

interface JewelGame { inventory: { items: Map<number, { group: number; number: number; level: number }> } }
type Win = Window & { __game: JewelGame };

// my_inventory.rcss: the equipment slots' boxes (left, top, width, height in panel px).
const SLOT_BOXES: Record<number, [number, number, number, number]> = {
  2: [75, 44, 46, 46], 3: [75, 87, 46, 66], 4: [75, 150, 46, 46], 5: [15, 150, 46, 46], 6: [135, 150, 46, 46],
};

const items = (page: Page) => page.evaluate(() => [...(window as unknown as Win).__game.inventory.items].map(([slot, i]) => ({ slot, group: i.group, number: i.number, level: i.level })));

test('a Jewel of Bless dropped on a worn item raises its level', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test1'); // Jewels of Bless (14, 13) in the bag
  await openInventory(page);
  const all = await items(page);
  const bless = all.find((i) => i.slot >= 12 && i.group === 14 && i.number === 13);
  const target = all.find((i) => SLOT_BOXES[i.slot] && i.level < 6);
  test.skip(!bless || !target, 'no Jewel of Bless or no worn item below +6 left');
  const blessCount = all.filter((i) => i.group === 14 && i.number === 13).length;
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, bless!.slot));
  const [left, top, width, height] = SLOT_BOXES[target!.slot];
  await clickAt(page, await panelPoint(page, 'my_inventory', left + width / 2, top + height / 2));
  await expect.poll(async () => (await items(page)).find((i) => i.slot === target!.slot)?.level, { timeout: 30_000 }).toBe(target!.level + 1);
  await expect.poll(async () => (await items(page)).filter((i) => i.group === 14 && i.number === 13).length).toBe(blessCount - 1);
  await expectNoPageErrors(errors);
});
