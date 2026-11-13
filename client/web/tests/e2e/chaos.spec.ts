// The chaos machine like on the desktop: talking to the Chaos Goblin in Noria opens the mix window next to the
// inventory with the method menu; a Jewel of Bless (or Soul) goes into the grid by a click and by a right click,
// the window shows the recipe (Potion of bless / soul), its success rate and zen; the mix button asks, the server
// mixes, the grid holds the result and the zen went down. The result goes back to the bag and Escape closes.
import { test, expect, type Page } from '@playwright/test';
import { BAG, BAG_GRID_ORIGIN, clickAt, enterWorld, expectNoPageErrors, gridSlotPoint } from './helpers.js';

interface MixItem { group: number; number: number; level: number; width: number; height: number }
interface ChaosGame {
  online: { characters: Map<number, { id: number; monsterNumber: number | null }>; walkTo(cell: { x: number; y: number }): boolean; talkTo(npc: unknown): void; say(text: string): void };
  inventory: { items: Map<number, MixItem>; mix: Map<number, MixItem>; money: number; openWindow: number | null };
  mapNumber: number;
}
type Win = Window & { __game: ChaosGame };

const CHAOS_GOBLIN = 238; // Noria (180, 103)
const MIX_GRID = { firstSlot: 0, columns: 8, rows: 4 };
const MIX_GRID_ORIGIN = { left: 15, top: 110 }; // mix_inventory.rcss #item_grid

const bag = (page: Page) => page.evaluate(() => [...(window as unknown as Win).__game.inventory.items].filter(([slot]) => slot >= 12).map(([slot, i]) => ({ slot, group: i.group, number: i.number })));
const mix = (page: Page) => page.evaluate(() => [...(window as unknown as Win).__game.inventory.mix].map(([slot, i]) => ({ slot, group: i.group, number: i.number })));
const money = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.inventory.money);
const mixText = (page: Page, selector: string) => page.evaluate((s) => document.querySelector('.rml-mix_inventory')?.shadowRoot?.querySelector(s)?.textContent?.trim() ?? '', selector);

test('mixes a jewel into a potion at the Chaos Goblin', async ({ page }) => {
  test.setTimeout(10 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test1'); // test1Dk: Jewels of Bless and Soul, lots of zen
  // A known place: Noria, next to the Chaos Goblin.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move noria'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 90_000 }).toBe(3);
  await page.waitForTimeout(5_000);
  const goblin = () => page.evaluate((number) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.monsterNumber === number)?.id ?? null, CHAOS_GOBLIN);
  await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 181, y: 105 }));
  await expect.poll(goblin, { timeout: 90_000 }).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await goblin())!);

  // The machine opens next to the inventory, with the method menu (ShowChaosMixMenuDialog): regular combination.
  const panel = page.locator('.rml-mix_inventory #panel');
  await expect(panel).toBeVisible({ timeout: 90_000 });
  await expect(page.locator('.rml-my_inventory #panel')).toBeVisible();
  await expect(page.locator('.mix-method-dialog')).toBeVisible({ timeout: 30_000 });
  await page.click('.mix-method-dialog .choose-option-0');
  await expect(page.locator('.mix-method-dialog')).toBeHidden();
  await expect.poll(() => mixText(page, '#title')).toBe('Regular Combination');
  await expect.poll(() => mixText(page, '.mix-list')).toContain('Please upload the assembly items.');

  // A jewel of the bag: Bless makes a Potion of bless, Soul a Potion of soul.
  const items = await bag(page);
  const jewel = items.find((i) => i.group === 14 && i.number === 13) ?? items.find((i) => i.group === 14 && i.number === 14);
  test.skip(!jewel, 'test1 has no Jewel of Bless or Soul left');
  const potion = jewel!.number === 13 ? 'Potion of bless' : 'Potion of soul';
  const zen = jewel!.number === 13 ? '100,000' : '50,000';

  // Into the grid by a click (picked up in the bag, put down on the grid's first cell).
  await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, jewel!.slot));
  await clickAt(page, await gridSlotPoint(page, 'mix_inventory', MIX_GRID, MIX_GRID_ORIGIN.left, MIX_GRID_ORIGIN.top, 0));
  await expect.poll(() => mix(page), { timeout: 60_000 }).toEqual([{ slot: 0, group: 14, number: jewel!.number }]);
  // The recipe, its success rate and zen (MixMgr: mix.bmd's Potion of bless / soul, 100%).
  await expect.poll(() => mixText(page, '.mix-head')).toContain(potion);
  expect(await mixText(page, '.mix-success')).toBe('Combining Success rate: 100%');
  expect(await mixText(page, '.mix-zen')).toBe(`Required zen for potion: ${zen}(${zen})`);
  await page.screenshot({ path: 'test-results/chaos-ready.png' });

  // A right click sends it back to the bag, a right click in the bag puts it in again.
  const inMix = await gridSlotPoint(page, 'mix_inventory', MIX_GRID, MIX_GRID_ORIGIN.left, MIX_GRID_ORIGIN.top, 0);
  await page.mouse.move(inMix.x, inMix.y, { steps: 4 });
  await page.mouse.click(inMix.x, inMix.y, { button: 'right' });
  await expect.poll(() => mix(page), { timeout: 60_000 }).toEqual([]);
  const back = (await bag(page)).find((i) => i.group === 14 && i.number === jewel!.number)!;
  const inBag = await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, back.slot);
  await page.mouse.move(inBag.x, inBag.y, { steps: 4 });
  await page.mouse.click(inBag.x, inBag.y, { button: 'right' });
  await expect.poll(() => mix(page).then((m) => m.length), { timeout: 60_000 }).toBe(1);

  // The mix button asks first, then the server mixes: the grid holds the result, the zen went down.
  const before = await money(page);
  await page.locator('.rml-mix_inventory').evaluate((host) => (host.shadowRoot!.querySelector('#btn_mix') as HTMLElement).click());
  await expect(page.locator('.confirm-dialog')).toBeVisible({ timeout: 30_000 });
  expect(await page.locator('.confirm-dialog').textContent()).toContain('Do you want to combine your items?');
  await page.click('.confirm-dialog .confirm-ok');
  await expect.poll(async () => (await mix(page)).some((i) => !(i.group === 14 && i.number === jewel!.number)) || (await mix(page)).length === 0, { timeout: 90_000 }).toBe(true);
  await expect.poll(() => money(page), { timeout: 30_000 }).toBeLessThan(before);
  // Finished: the recipe text and the mix button are gone (MIX_FINISHED).
  await expect.poll(() => page.locator('.rml-mix_inventory').evaluate((host) => getComputedStyle(host.shadowRoot!.querySelector('#btn_mix')!).display)).toBe('none');
  await page.screenshot({ path: 'test-results/chaos-result.png' });

  // The result goes back to the bag (picked up and put down in an empty bag cell), then Escape closes the machine.
  for (const { slot } of await mix(page)) {
    const item = await page.evaluate((s) => { const i = (window as unknown as Win).__game.inventory.mix.get(s)!; return { width: i.width, height: i.height }; }, slot);
    await clickAt(page, await gridSlotPoint(page, 'mix_inventory', MIX_GRID, MIX_GRID_ORIGIN.left, MIX_GRID_ORIGIN.top, slot, item));
    const free = await page.evaluate((size) => {
      const inv = (window as unknown as Win).__game.inventory as unknown as { items: Map<number, unknown>; firstEmptySlot(s: Map<number, unknown>, g: unknown, i: unknown): number | null };
      return inv.firstEmptySlot(inv.items, { firstSlot: 12, columns: 8, rows: 8 }, size);
    }, item);
    await clickAt(page, await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, free!, item));
    await expect.poll(() => mix(page).then((m) => m.some((i) => i.slot === slot)), { timeout: 60_000 }).toBe(false);
  }
  await page.keyboard.press('Escape');
  await expect(panel).toBeHidden({ timeout: 30_000 });
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.inventory.openWindow)).toBeNull();

  // Back to town for the other tests.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 90_000 }).toBe(0);
  await expectNoPageErrors(errors);
});
