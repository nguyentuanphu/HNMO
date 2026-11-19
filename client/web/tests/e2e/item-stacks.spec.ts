// HNMO's bigger stacks: the vault's Stacks panel lists the kinds; jewels stack only after the account unlocked them
// (500 WCoin (C)), then a jewel dropped on another jewel joins its stack and shows the count; Shift + click splits a
// stack again into a free slot.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors, gridSlotPoint, BAG, BAG_GRID_ORIGIN } from './helpers.js';

interface StackItem { group: number; number: number; durability: number }
interface StackGame {
  mapNumber: number;
  online: { say(text: string): void; walkTo(cell: { x: number; y: number }): boolean; talkTo(c: unknown): void; characters: Map<number, { name: string }> };
  inventory: { items: Map<number, StackItem>; move(fromStorage: number, fromSlot: number, toStorage: number, toSlot: number): void };
}
type Win = Window & { __game: StackGame };
const game = <T>(page: Page, f: (g: StackGame) => T) => page.evaluate(`(${f.toString()})(window.__game)`) as Promise<T>;

test('jewels stack after the unlock; Shift + click splits a stack', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test300');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => game(page, (g) => g.mapNumber), { timeout: 60_000 }).toBe(0);
  await expect.poll(() => page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    g.online.walkTo({ x: 144, y: 113 });
    const keeper = [...g.online.characters.values()].find((c) => /Safety Guardian/.test(c.name));
    if (keeper) g.online.talkTo(keeper);
    return keeper !== undefined;
  }), { timeout: 90_000 }).toBe(true);
  await page.waitForFunction(() => document.querySelector('.rml-storage'), null, { timeout: 120_000 });

  // The Stacks panel: five kinds; potions free, jewels to unlock for 500.
  await page.locator('.hnmo-vault-pages .hnmo-stacks-button').click();
  const panel = page.locator('.hnmo-stacks-panel');
  await expect(panel).toBeVisible();
  await expect(panel.locator('.hnmo-stacks-row')).toHaveCount(5);
  await expect(panel.locator('.hnmo-stacks-row').first()).toContainText('Free');
  const unlockJewels = panel.locator('button[data-kind="jewels"]');
  await expect(unlockJewels).toHaveText('Unlock 500');

  // Two single Jewels of Bless (14:13) in the bag.
  const jewels = () => game(page, (g) => [...g.inventory.items.entries()].filter(([slot, i]) => slot >= 12 && i.group === 14 && i.number === 13).map(([slot, i]) => ({ slot, durability: i.durability })));
  const before = await jewels();
  test.skip(before.length < 2, 'test300 needs two Jewels of Bless');
  const total = before.reduce((sum, j) => sum + j.durability, 0);

  // Unlock jewels; then a jewel moved onto another joins its stack.
  await unlockJewels.click();
  await page.locator('.confirm-dialog #gcd_btn_primary').click();
  await expect(panel.locator('.hnmo-stacks-row', { hasText: 'Jewels' })).toContainText('Unlocked', { timeout: 30_000 });
  const [a, b] = await jewels();
  await page.evaluate(([from, to]) => (window as unknown as Win).__game.inventory.move(0, from, 0, to), [a.slot, b.slot]);
  await expect.poll(async () => (await jewels()).find((j) => j.slot === b.slot)?.durability, { timeout: 30_000 }).toBe(a.durability + b.durability);
  await expect.poll(async () => (await jewels()).reduce((sum, j) => sum + j.durability, 0)).toBe(total);
  await page.screenshot({ path: 'test-results/item-stacks.png' });

  // Shift + click the stack: split one off into a free slot; the pieces stay the same.
  await page.locator('.hnmo-stacks-panel .hnmo-stacks-close').click();
  const point = await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, b.slot);
  await page.keyboard.down('Shift');
  await page.mouse.click(point.x, point.y);
  await page.keyboard.up('Shift');
  const ask = page.locator('.confirm-dialog');
  await expect(ask.locator('.ask-input')).toBeVisible();
  await ask.locator('.ask-input').fill('1');
  await ask.locator('#gcd_btn_primary').click();
  await expect.poll(async () => (await jewels()).length, { timeout: 30_000 }).toBe(before.length);
  await expect.poll(async () => (await jewels()).reduce((sum, j) => sum + j.durability, 0)).toBe(total);
  await expectNoPageErrors(errors);
});
