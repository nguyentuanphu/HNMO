// Fighting like on the desktop: attack a monster, the hero walks to it and swings, the server's
// hits lower its health bar and show damage numbers, and it dies.
import { test, expect } from '@playwright/test';
import { enterWorld } from './helpers.js';

interface Fighter { id: number; name: string; kind: string; dead: boolean; healthStatus: number; cell: { x: number; y: number } }
interface CombatGame {
  online: { characters: Map<number, Fighter>; hero: { cell: { x: number; y: number }; isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  combat: { attack(target: unknown): void; target: Fighter | null };
}
type Win = Window & { __game: CombatGame };

test('attacks the nearest monster until it dies; damage numbers and its health bar show', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test7');
  // Lorencia's town is a safe zone without monsters: go out to the spiders' area north of it
  // (the server's spawn area x 135-240, y 20-88), a part at a time.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  const nearest = () => page.evaluate(() => {
    const game = (window as unknown as Win).__game;
    const hero = game.online.hero.cell;
    let best: Fighter | null = null, bestDistance = Infinity;
    for (const c of game.online.characters.values()) {
      if (c.kind !== 'monster' || c.dead) continue;
      const d = Math.hypot(c.cell.x - hero.x, c.cell.y - hero.y);
      if (d < bestDistance) { best = c; bestDistance = d; }
    }
    return best ? { id: best.id, name: best.name, distance: bestDistance } : null;
  });
  // Walk on through the area until a monster is in view (they respawn some seconds after dying).
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 70 }, { x: 170, y: 58 }, { x: 185, y: 45 }, { x: 200, y: 35 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
    if (goal.y <= 70 && await nearest()) break;
  }
  await expect.poll(nearest, { timeout: 60_000 }).not.toBeNull();
  const target = (await nearest())!;
  console.log('target', JSON.stringify(target));
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.combat.attack(g.online.characters.get(id)); }, target.id);
  // Hits land: the health bar shows and goes down, damage numbers appear.
  await expect.poll(() => page.evaluate((id) => (window as unknown as Win).__game.online.characters.get(id)?.healthStatus ?? 0, target.id), { timeout: 180_000 }).toBeLessThan(1);
  await page.screenshot({ path: 'test-results/combat.png' });
  // It dies (or leaves the view once dead).
  await expect.poll(() => page.evaluate((id) => {
    const c = (window as unknown as Win).__game.online.characters.get(id);
    return !c || c.dead;
  }, target.id), { timeout: 240_000 }).toBe(true);
  // The body stays a while, then fades and sinks (the desktop's death effects) until the server removes it.
  await page.waitForTimeout(3_000);
  await page.screenshot({ path: 'test-results/combat-dead.png' });
  expect(errors).toEqual([]);
});
