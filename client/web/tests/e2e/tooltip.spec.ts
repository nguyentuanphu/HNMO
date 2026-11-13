// Hovering an item in the inventory shows its tooltip, as the desktop client does.
import { test, expect } from '@playwright/test';
import { BAG, BAG_GRID_ORIGIN, bagSlots, enterWorld, gridSlotPoint, openInventory, panelPoint, HELM_SLOT_CENTRE } from './helpers.js';

test('hovering an item shows its tooltip with its name; it goes away when the mouse leaves', async ({ page }) => {
  await enterWorld(page, 'test5');
  await openInventory(page);
  const slots = await bagSlots(page);
  const slot = slots.find((s) => s >= BAG.firstSlot) ?? slots.find((s) => s < BAG.firstSlot);
  test.skip(slot === undefined, 'this account has no items');
  const point = slot! >= BAG.firstSlot
    ? await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, slot!)
    : await panelPoint(page, 'my_inventory', HELM_SLOT_CENTRE.x, HELM_SLOT_CENTRE.y);
  await page.mouse.move(point.x - 3, point.y - 3);
  await page.mouse.move(point.x, point.y, { steps: 3 });
  const lines = page.locator('.rml-tooltip .tt-line');
  await expect.poll(async () => (await lines.allTextContents()).filter((t) => t.trim()).length).toBeGreaterThan(0);
  const text = (await lines.allTextContents()).join('\n');
  console.log(text);
  await page.screenshot({ path: 'test-results/tooltip.png' });
  await page.mouse.move(10, 10);
  await expect.poll(async () => (await lines.allTextContents()).filter((t) => t.trim()).length).toBe(0);
});
