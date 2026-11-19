// HNMO's bigger stacks: the vault's Stacks panel lists the kinds (potions free, jewels to unlock for 500 WCoin (C));
// an unlock without enough coins is refused (or, with them, takes effect); a potion dropped on the same potion joins its
// stack; Shift + click splits a stack again into a free slot. The paid unlock itself is covered by ItemStackRulesTest.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors, gridSlotPoint, BAG, BAG_GRID_ORIGIN } from './helpers.js';

interface StackItem { group: number; number: number; durability: number }
interface StackGame {
  mapNumber: number;
  online: { say(text: string): void; walkTo(cell: { x: number; y: number }): boolean; talkTo(c: unknown): void; characters: Map<number, { name: string }> };
  inventory: { items: Map<number, StackItem>; move(fromStorage: number, fromSlot: number, toStorage: number, toSlot: number): void };
}
type Win = Window & { __game: StackGame & { itemStacks?: { state: { balance: number; message?: string | null } | null } } };
const game = <T>(page: Page, f: (g: StackGame) => T) => page.evaluate(`(${f.toString()})(window.__game)`) as Promise<T>;

test('stack kinds, the unlock, potions stacking and Shift + click splitting', async ({ page }) => {
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

  // The jewels unlock: refused without 500 coins, done with them.
  const balance = await page.evaluate(() => (window as unknown as Win).__game.itemStacks?.state?.balance ?? 0);
  await unlockJewels.click();
  await page.locator('.confirm-dialog #gcd_btn_primary').click();
  if (balance < 500) {
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.itemStacks?.state?.message ?? ''), { timeout: 30_000 }).toContain('needs 500');
    await expect(panel.locator('button[data-kind="jewels"]')).toBeVisible();
  } else {
    await expect(panel.locator('.hnmo-stacks-row', { hasText: 'Jewels' })).toContainText('Unlocked', { timeout: 30_000 });
  }
  await page.locator('.hnmo-stacks-panel .hnmo-stacks-close').click();

  // Two stacks of the same potion in the bag: one moved onto the other joins it.
  const potions = () => game(page, (g) => [...g.inventory.items.entries()].filter(([slot, i]) => slot >= 12 && i.group === 14 && i.number <= 6).map(([slot, i]) => ({ slot, number: i.number, durability: i.durability })));
  const all = await potions();
  const pair = all.flatMap((p) => all.filter((q) => q.number === p.number && q.slot > p.slot && p.durability + q.durability <= 255).map((q) => [p, q] as const))[0];
  test.skip(pair === undefined, 'test300 needs two stacks of the same potion');
  const [a, b] = pair!;
  const same = async () => (await potions()).filter((p) => p.number === a.number);
  const total = (await same()).reduce((sum, p) => sum + p.durability, 0);
  const count = (await same()).length;
  await page.evaluate(([from, to]) => (window as unknown as Win).__game.inventory.move(0, from, 0, to), [a.slot, b.slot]);
  await expect.poll(async () => (await same()).find((p) => p.slot === b.slot)?.durability, { timeout: 30_000 }).toBe(a.durability + b.durability);
  await expect.poll(async () => (await same()).reduce((sum, p) => sum + p.durability, 0)).toBe(total);
  await page.screenshot({ path: 'test-results/item-stacks.png' });

  // Shift + click the stack: split one off into a free slot; the pieces stay the same.
  const point = await gridSlotPoint(page, 'my_inventory', BAG, BAG_GRID_ORIGIN.left, BAG_GRID_ORIGIN.top, b.slot);
  await page.keyboard.down('Shift');
  await page.mouse.click(point.x, point.y);
  await page.keyboard.up('Shift');
  const ask = page.locator('.confirm-dialog');
  await expect(ask.locator('.ask-input')).toBeVisible();
  await ask.locator('.ask-input').fill('1');
  await ask.locator('#gcd_btn_primary').click();
  await expect.poll(async () => (await same()).length, { timeout: 30_000 }).toBe(count);
  await expect.poll(async () => (await same()).find((p) => p.slot === b.slot)?.durability).toBe(a.durability + b.durability - 1);
  await expect.poll(async () => (await same()).reduce((sum, p) => sum + p.durability, 0)).toBe(total);
  await expectNoPageErrors(errors);
});
