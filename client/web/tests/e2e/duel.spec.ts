// A duel like the desktop's: A challenges B (CommandDual: DuelStartRequest), B is asked over the duel art
// (generic_confirm_dialog with "[name]") and accepts, both are taken to the duel arena with the score window
// (duel_window: both names and scores), A ends it (DuelStopRequest) and both are told the duel is over.
// Both characters are sent back to Lorencia at the end.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface DuelGame {
  online: { characters: Map<number, { id: number; name: string; kind: string }>; say(text: string): void };
  party: { members: { name: string }[]; leave(index: number): void };
  mapNumber: number;
  duelAndGens: {
    duel: { enabled: boolean; players: { name: string; score: number }[]; challenge(target: unknown): void; stop(): void };
    score: { isOpen: boolean };
  };
}
type Win = Window & { __game: DuelGame };

const OPEN = { timeout: 90_000 };
const enabled = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.duelAndGens.duel.enabled);
const heroName = (page: Page) => page.evaluate(() => document.querySelector('#heroName')?.textContent ?? '');

test('a duel of two: challenge, the question, the score window, the end', async ({ browser }) => {
  test.setTimeout(10 * 60_000);
  const contextA = await browser.newContext(), contextB = await browser.newContext();
  const a = await contextA.newPage(), b = await contextB.newPage();
  const errors: string[] = [];
  for (const page of [a, b]) page.on('pageerror', (error) => errors.push(error.message));
  try {
    await enterWorld(a, 'test7');
    await enterWorld(b, 'test8');
    for (const page of [a, b]) await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
    await a.waitForTimeout(10_000);
    const aName = await heroName(a), bName = await heroName(b);

    // A sees B and challenges it.
    const seesB = () => a.evaluate((name) => [...(window as unknown as Win).__game.online.characters.values()].some((c) => c.kind === 'player' && c.name === name), bName);
    await expect.poll(seesB, OPEN).toBe(true);
    await a.evaluate((name) => {
      const g = (window as unknown as Win).__game;
      const target = [...g.online.characters.values()].find((c) => c.kind === 'player' && c.name === name);
      if (target) g.duelAndGens.duel.challenge(target);
    }, bName);

    // B is asked over the duel art, with A's name over it, and accepts.
    const question = b.locator('.rml-generic_confirm_dialog');
    await expect(question.locator('.gcd-line')).toHaveText(['You are challenged to a duel.', 'Would you like to accept the challenge?'], OPEN);
    await expect(question.locator('.gcd-portrait2d-text')).toHaveText(`[${aName}]`);
    await b.screenshot({ path: 'test-results/duel-question.png' });
    await question.locator('#gcd_btn_primary').click();

    // Both are in the duel: the score window shows each side's name and score.
    for (const page of [a, b]) await expect.poll(() => enabled(page), OPEN).toBe(true);
    for (const [page, me, enemy] of [[a, aName, bName], [b, bName, aName]] as const) {
      const score = page.locator('.rml-duel_window');
      await expect(score.locator('#hero_name')).toHaveText(me, OPEN);
      await expect(score.locator('#enemy_name')).toHaveText(enemy);
      await expect(score.locator('#hero_score')).toHaveText('0');
      await expect(score.locator('#enemy_score')).toHaveText('0');
    }
    await expect(a.locator('.chat-log')).toContainText(`${bName} has accepted your challenge.`);
    await a.waitForTimeout(3_000);
    console.log('map after the start:', await a.evaluate(() => (window as unknown as Win).__game.mapNumber));
    await a.screenshot({ path: 'test-results/duel-score.png' });

    // A ends it: the score window closes for both, the chat says so.
    await a.evaluate(() => (window as unknown as Win).__game.duelAndGens.duel.stop());
    for (const page of [a, b]) {
      await expect.poll(() => enabled(page), OPEN).toBe(false);
      await expect(page.locator('.rml-duel_window')).toHaveCount(0);
      await expect(page.locator('.chat-log')).toContainText('The duel has been canceled.');
    }
    await expectNoPageErrors(errors);
  } finally {
    for (const page of [a, b]) await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia')).catch(() => undefined);
    await a.waitForTimeout(5_000);
    await contextA.close();
    await contextB.close();
  }
});
