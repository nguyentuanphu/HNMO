// Inventory extension toggle (#btn_expand on my_inventory): opens inventory_extension docked beside
// the inventory like CInventoryExtension, showing unlocked grids and locked pages, and closes cleanly.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors, openInventory } from './helpers.js';

const OPEN = { timeout: 60_000 };

test('Inventory Expand button opens, toggles, and closes the Inventory Extension window', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test400');
  await openInventory(page);

  const inv = page.locator('.rml-my_inventory');
  const ext = page.locator('.rml-inventory_extension');

  // Open via Expand button.
  await inv.locator('#btn_expand').click();
  await expect(ext.locator('#panel')).toBeVisible(OPEN);
  await expect(ext.locator('#title')).not.toHaveText('');

  // Docked immediately to the left of the inventory panel.
  const invBox = await inv.locator('#panel').boundingBox();
  const extBox = await ext.locator('#panel').boundingBox();
  expect(invBox).not.toBeNull();
  expect(extBox).not.toBeNull();
  expect(extBox!.x + extBox!.width).toBeLessThanOrEqual(invBox!.x + 2);

  // At least one extension box or locked marker is rendered for the 4 pages.
  const pageMarkers = await ext.locator('#panel .item-grid:not(.hidden), #panel .ext-locked-page, #panel .ext-locked, #panel [data-locked-page]').count();
  expect(pageMarkers).toBeGreaterThan(0);

  // Close via extension #btn_exit (keeps inventory open).
  await ext.locator('#btn_exit').click();
  await expect(ext).toHaveCount(0);
  await expect(inv.locator('#panel')).toBeVisible();

  // Clicking #btn_expand opens and clicking #btn_expand again closes the extension window.
  await inv.locator('#btn_expand').click();
  await expect(ext.locator('#panel')).toBeVisible(OPEN);
  await inv.locator('#btn_expand').click();
  await expect(ext).toHaveCount(0);

  // Pressing K while Inventory is open toggles Inventory Extension too (desktop MyInventory shortcut).
  await page.keyboard.press('k');
  await expect(ext.locator('#panel')).toBeVisible(OPEN);

  // Escape closes both extension and inventory cleanly.
  await page.keyboard.press('Escape');
  await expect(ext).toHaveCount(0);
  await expect(inv).toHaveCount(0);

  await expectNoPageErrors(errors);
});
