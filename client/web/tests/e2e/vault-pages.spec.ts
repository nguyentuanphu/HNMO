// HNMO's vault pages: page buttons above the open vault window (F5 4E / 4F). Each account has two free pages; a
// switch shows the other page's items in the open window, and switching back shows the first page's items again.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface VaultGame {
  mapNumber: number;
  online: { say(text: string): void; walkTo(cell: { x: number; y: number }): boolean; talkTo(c: unknown): void; characters: Map<number, { name: string }> };
  inventory: { vault: Map<number, unknown> };
}
type Win = Window & { __game: VaultGame };
const game = <T>(page: Page, f: (g: VaultGame) => T) => page.evaluate(`(${f.toString()})(window.__game)`) as Promise<T>;

test('the vault window has page buttons; a page shows its own items', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test300');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => game(page, (g) => g.mapNumber), { timeout: 60_000 }).toBe(0);
  // Walk to the vault keeper (again until the walk is taken: right after the move the map is still loading), look
  // it up and talk to it in one step until it is in view.
  await expect.poll(() => page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    g.online.walkTo({ x: 144, y: 113 });
    const keeper = [...g.online.characters.values()].find((c) => /Safety Guardian/.test(c.name));
    if (keeper) g.online.talkTo(keeper);
    return keeper !== undefined;
  }), { timeout: 90_000 }).toBe(true);
  await page.waitForFunction(() => document.querySelector('.rml-storage'), null, { timeout: 120_000 });

  const bar = page.locator('.hnmo-vault-pages');
  await expect(bar).toBeVisible({ timeout: 30_000 });
  await expect(bar.locator('.hnmo-vault-page')).toHaveCount(2);
  await expect(bar.locator('.hnmo-vault-page[data-page="0"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(bar.locator('.hnmo-vault-page-buy')).toBeVisible();
  await expect(bar.locator('.hnmo-vault-page-buy')).toHaveAttribute('title', /200 WCoin \(C\)/);
  const firstPage = await game(page, (g) => [...g.inventory.vault.keys()].sort((a, b) => a - b));

  // Page 2: its own items (none at first), in the same open window.
  await bar.locator('.hnmo-vault-page[data-page="1"]').click();
  await expect(bar.locator('.hnmo-vault-page[data-page="1"]')).toHaveAttribute('aria-pressed', 'true', { timeout: 30_000 });
  await expect(page.locator('.rml-storage')).toHaveCount(1);
  await expect.poll(() => game(page, (g) => g.inventory.vault.size), { timeout: 30_000 }).toBe(0);
  await page.screenshot({ path: 'test-results/vault-pages.png' });

  // Back to page 1: the first page's items again.
  await bar.locator('.hnmo-vault-page[data-page="0"]').click();
  await expect(bar.locator('.hnmo-vault-page[data-page="0"]')).toHaveAttribute('aria-pressed', 'true', { timeout: 30_000 });
  await expect.poll(() => game(page, (g) => [...g.inventory.vault.keys()].sort((a, b) => a - b)), { timeout: 30_000 }).toEqual(firstPage);

  // Closing the vault hides the page buttons.
  await page.keyboard.press('Escape');
  await expect(bar).toBeHidden();
  await expectNoPageErrors(errors);
});
