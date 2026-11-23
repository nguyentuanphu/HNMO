// ObjectMessage (C1 01): NPCs that answer with an overhead speech bubble (Leo the Helper, Sebina,
// Apostle Devin, Royal Guard Captain Lorence, Marlon) show their message above their head like the desktop.
import { test, expect } from '@playwright/test';
import { dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

interface NpcGame {
  mapNumber: number | null;
  online: {
    characters: Map<number, { id: number; name: string; kind: string; cell: { x: number; y: number } }>;
    say(text: string): void;
    talkTo(npc: unknown): void;
  };
}

test('ObjectMessage (C1 01) from Leo the Helper shows an overhead speech bubble', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test0');
  await page.evaluate(() => (window as unknown as { __game: NpcGame }).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as { __game: NpcGame }).__game.mapNumber), { timeout: 60_000 }).toBe(0);
  await dismissOnboarding(page);

  // Leo the Helper (371) stands in Lorencia at (130, 126).
  await expect.poll(() => page.evaluate(() => {
    const g = (window as unknown as { __game: NpcGame }).__game;
    return [...g.online.characters.values()].some((c) => c.kind === 'npc' && /leo/i.test(c.name));
  }), { timeout: 60_000 }).toBe(true);

  await page.evaluate(() => {
    const g = (window as unknown as { __game: NpcGame }).__game;
    const leo = [...g.online.characters.values()].find((c) => c.kind === 'npc' && /leo/i.test(c.name));
    if (leo) g.online.talkTo(leo);
  });

  const bubble = page.locator('#labels .label.speech-bubble');
  await expect(bubble).toBeVisible({ timeout: 60_000 });
  await expect(bubble).toHaveText(/\S+/);
  await expectNoPageErrors(errors);
});
