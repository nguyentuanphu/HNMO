// Dropping an item on the ground and picking it up again, as on the desktop: an item on the
// cursor clicked onto the ground lands there with its name label; clicking it picks it up.
import { test, expect } from '@playwright/test';
import { BAG, BAG_GRID_ORIGIN, bagSlots, clickAt, enterWorld, gridSlotPoint, openInventory } from './helpers.js';

interface Drop { id: number; cell: { x: number; y: number }; item: { group: number; number: number } | null; label: { textContent: string | null } }
type Win = Window & { __game: { ground: { drops: Map<number, Drop>; pickUp(d: Drop): void }; inventory: { items: Map<number, { group: number; number: number }> } } };

test('drops a bag item on the ground, shows its label and picks it up again', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test4');
  await openInventory(page);
  const slots = await bagSlots(page);
  const slot = slots.find((s) => s >= BAG.firstSlot);
  test.skip(slot === undefined, 'nothing in the bag');
  const item = await page.evaluate((s) => { const i = (window as unknown as Win).__game.inventory.items.get(s)!; return { group: i.group, number: i.number }; }, slot!);
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, slot!));
  // the ground a little left of the hero (the middle of the screen)
  const viewport = page.viewportSize()!;
  await clickAt(page, { x: viewport.width / 2 - 160, y: viewport.height / 2 + 40 });
  await expect.poll(() => bagSlots(page)).not.toContain(slot);
  await expect.poll(() => page.evaluate((it) => [...(window as unknown as Win).__game.ground.drops.values()].some((d) => d.item?.group === it.group && d.item?.number === it.number), item)).toBe(true);
  await page.keyboard.down('Alt');
  await page.waitForTimeout(1500);
  const label = await page.evaluate((it) => [...(window as unknown as Win).__game.ground.drops.values()].find((d) => d.item?.group === it.group && d.item?.number === it.number)?.label.textContent ?? '', item);
  console.log('label:', label);
  expect(label.length).toBeGreaterThan(0);
  await page.screenshot({ path: 'test-results/ground.png' });
  await page.keyboard.up('Alt');
  // pick it up again
  await page.evaluate((it) => { const g = (window as unknown as Win).__game; const d = [...g.ground.drops.values()].find((x) => x.item?.group === it.group && x.item?.number === it.number)!; g.ground.pickUp(d); }, item);
  await expect.poll(() => page.evaluate((it) => [...(window as unknown as Win).__game.inventory.items.values()].filter((i) => i.group === it.group && i.number === it.number).length, item), { timeout: 60_000 })
    .toBeGreaterThanOrEqual(1);
  await expect.poll(() => page.evaluate((it) => [...(window as unknown as Win).__game.ground.drops.values()].some((d) => d.item?.group === it.group && d.item?.number === it.number), item)).toBe(false);
  expect(errors).toEqual([]);
});
