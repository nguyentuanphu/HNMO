// The Arena Herald: an NPC next to where players appear in Lorencia (and Devias, Noria, Elbeland). It shows with its
// name, and talking to it opens the Arena window, as Alt+A does.
import { test, expect } from '@playwright/test';
import { enterWorld } from './helpers.js';

interface Npc { id: number; name: string; kind: string; cell: { x: number; y: number } }
interface HeraldGame {
  mapNumber: number;
  online: { characters: Map<number, Npc>; say(text: string): void; talkTo(character: unknown): void; walkTo(cell: { x: number; y: number }): boolean };
}
type Win = Window & { __game: HeraldGame };

test('the Arena Herald in Lorencia opens the Arena window', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test9');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), { timeout: 120_000 }).toBe(0);

  // Walk over to where it stands (next to the town's spawn area).
  await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 135, y: 121 }));

  // The Herald is there, with its own name, at (133, 117).
  const herald = () => page.evaluate(() => [...(window as unknown as Win).__game.online.characters.values()]
    .find((c) => c.name === 'Arena Herald') ?? null);
  await expect.poll(async () => {
    const found = await herald();
    if (!found) await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 135, y: 121 }));
    return found?.cell ?? null;
  }, { timeout: 120_000, intervals: [3000] }).toEqual({ x: 133, y: 117 });

  await page.screenshot({ path: 'test-results/arena-herald-npc.png' });

  // Talking to it opens the Arena window.
  await page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    const npc = [...g.online.characters.values()].find((c) => c.name === 'Arena Herald');
    g.online.talkTo(npc);
  });
  await expect(page.locator('p.hnmo-muted', { hasText: 'Battles across servers and seasons.' })).toBeVisible({ timeout: 90_000 });
  await expect(page.locator('#arena-go')).toBeVisible();
  expect(await page.evaluate(() => document.body.innerText)).not.toContain('is not implemented');
  await page.screenshot({ path: 'test-results/arena-herald.png' });

  // Next to the Herald, its model is drawn (the window closed again).
  await page.evaluate(() => (window as unknown as { __game: { arena?: { close(): void } } }).__game.arena?.close());
  await expect.poll(() => page.evaluate(() => {
    const g = (window as unknown as { __game: { online: { characters: Map<number, { name: string; group?: { visible: boolean; children: unknown[] } }> } } }).__game;
    const npc = [...g.online.characters.values()].find((c) => c.name === 'Arena Herald');
    return !!npc?.group?.visible && (npc.group.children.length ?? 0) > 0;
  }), { timeout: 120_000 }).toBe(true);
  await page.screenshot({ path: 'test-results/arena-herald-next-to.png' });
  expect(errors).toEqual([]);
});
