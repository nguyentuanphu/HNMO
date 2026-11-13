// The inventory and vault work like the desktop client's: the server's items are shown, items
// move and equip by clicking, the vault keeper opens the vault, items go into the vault and back.
import { test, expect } from '@playwright/test';
import {
  BAG, BAG_GRID_ORIGIN, HELM_SLOT_CENTRE, VAULT, VAULT_GRID_ORIGIN,
  bagSlots, clickAt, enterWorld, expectNoPageErrors, gridSlotPoint, openInventory, panelPoint, type GameView,
} from './helpers.js';

type Win = Window & { __game: GameView };

test('logs in, enters the world and shows the inventory from the server', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test1');
  const hero = await page.evaluate(() => (window as unknown as Win).__game.hero.name);
  expect(hero.length).toBeGreaterThan(0);
  await openInventory(page);
  const state = await page.evaluate(() => {
    const inventory = (window as unknown as Win).__game.inventory;
    return { items: inventory.items.size, money: inventory.money };
  });
  expect(state.items).toBeGreaterThan(0);
  await expect(page.locator('.rml-my_inventory #panel')).toBeVisible();
  await expectNoPageErrors(errors);
});

test('moves an item inside the bag by clicking it and clicking a free place', async ({ page }) => {
  await enterWorld(page, 'test2');
  await openInventory(page);
  const before = await bagSlots(page);
  const from = before.find((slot) => slot >= BAG.firstSlot);
  test.skip(from === undefined, 'this account has nothing in its bag');
  const plan = await page.evaluate(([slot, bag]) => {
    const inventory = (window as unknown as Win).__game.inventory;
    const item = inventory.items.get(slot)!;
    for (let s = bag.firstSlot + bag.columns * bag.rows - 1; s >= bag.firstSlot; s--) {
      if (s !== slot && inventory.fits(inventory.items, bag, s, item, slot)) return { to: s, width: item.width, height: item.height };
    }
    return null;
  }, [from!, BAG] as const);
  test.skip(!plan, 'no free place in the bag');
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, from!));
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, plan!.to, plan!));
  await expect.poll(() => bagSlots(page)).toContain(plan!.to);
  expect(await bagSlots(page)).not.toContain(from);
});

// A level 400 character, so the server lets it wear the helm again (it refuses items whose
// requirements the character doesn't meet, as test0-9's starting gear can be).
test('takes the helm off into the bag and puts it back on; the character changes with it', async ({ page }) => {
  await enterWorld(page, 'test400');
  await openInventory(page);
  const hasHelm = (await bagSlots(page)).includes(2);
  test.skip(!hasHelm, 'this character wears no helm');
  const heroBefore = await page.evaluate(() => (window as unknown as Win).__game.hero.group.uuid);
  const free = await page.evaluate((bag) => {
    const inventory = (window as unknown as Win).__game.inventory;
    const helm = inventory.items.get(2)!;
    for (let s = bag.firstSlot; s < bag.firstSlot + bag.columns * bag.rows; s++) if (inventory.fits(inventory.items, bag, s, helm, null)) return { slot: s, width: helm.width, height: helm.height };
    return null;
  }, BAG);
  test.skip(!free, 'no room in the bag for the helm');
  await clickAt(page, await panelPoint(page, 'my_inventory', HELM_SLOT_CENTRE.x, HELM_SLOT_CENTRE.y));
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, free!.slot, free!));
  await expect.poll(() => bagSlots(page)).toContain(free!.slot);
  expect(await bagSlots(page)).not.toContain(2);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.hero.group.uuid)).not.toBe(heroBefore);

  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, free!.slot));
  await clickAt(page, await panelPoint(page, 'my_inventory', HELM_SLOT_CENTRE.x, HELM_SLOT_CENTRE.y));
  await expect.poll(() => bagSlots(page)).toContain(2);
});

test('the vault keeper opens the vault; an item goes into the vault and comes back', async ({ page }) => {
  await enterWorld(page, 'test300');
  // To Lorencia's town, next to the vault keeper at (146, 110), wherever the character was.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win & { __game: { mapNumber: number } }).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  await page.waitForTimeout(5_000);
  await page.evaluate(() => (window as unknown as Win & { __game: { online: { walkTo(cell: { x: number; y: number }): void } } }).__game.online.walkTo({ x: 144, y: 113 }));
  // The vault keeper of the map the character is on (Lorencia, Devias, Noria, … all have one).
  await expect.poll(() => page.evaluate(() => [...(window as unknown as Win).__game.online.characters.values()].some((c) => /Safety Guardian/.test(c.name))), { timeout: 90_000 }).toBe(true);
  await page.evaluate(() => {
    const game = (window as unknown as Win).__game;
    const keeper = [...game.online.characters.values()].find((c) => /Safety Guardian/.test(c.name));
    game.online.talkTo(keeper);
  });
  await page.waitForFunction(() => document.querySelector('.rml-storage'), null, { timeout: 120_000 });
  await expect(page.locator('.rml-my_inventory #panel')).toBeVisible();

  const plan = await page.evaluate(([bag, vault]) => {
    const inventory = (window as unknown as Win).__game.inventory;
    const from = [...inventory.items.keys()].filter((s) => s >= bag.firstSlot).sort((a, b) => a - b)[0];
    if (from === undefined) return null;
    const item = inventory.items.get(from)!;
    for (let s = 0; s < vault.columns * vault.rows; s++) if (inventory.fits(inventory.vault, vault, s, item, null)) return { from, to: s, width: item.width, height: item.height };
    return null;
  }, [BAG, VAULT] as const);
  test.skip(!plan, 'nothing to move or no room in the vault');
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, plan!.from));
  await clickAt(page, await gridSlotPoint(page, 'storage', VAULT, VAULT_GRID_ORIGIN.left, VAULT_GRID_ORIGIN.top, plan!.to, plan!));
  await expect.poll(() => page.evaluate((slot) => (window as unknown as Win).__game.inventory.vault.has(slot), plan!.to)).toBe(true);

  // and back into the same bag place
  await clickAt(page, await gridSlotPoint(page, 'storage', VAULT, VAULT_GRID_ORIGIN.left, VAULT_GRID_ORIGIN.top, plan!.to));
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, plan!.from, plan!));
  await expect.poll(() => bagSlots(page)).toContain(plan!.from);
});
