// Gens like on the desktop: talking to Lorencia's Duprian steward opens its dialogue; the join answer sends
// GensJoinRequest and the server's answer (GensJoinResponse) moves the dialogue to its "joined" words;
// B opens the Gens Info window (gens_ranking.rml) with the family, the title and the contribution; leaving
// at the steward (GensLeaveRequest) ends the membership, so the next run starts the same.
// test7's first character is level 71: the server's minimum is 50 (GensConfiguration), and it must not be in a party.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Npc { id: number; kind: string; monsterNumber: number | null; cell: { x: number; y: number } }
interface GensGame {
  online: { characters: Map<number, Npc>; hero: { cell: { x: number; y: number } }; walkTo(cell: { x: number; y: number }): boolean; talkTo(npc: unknown): void; say(text: string): void };
  party: { members: { name: string }[]; leave(index: number): void };
  duelAndGens: { gens: { influence: number; rank: number; contribution: number } };
}
type Win = Window & { __game: GensGame };

const DUPRIAN_STEWARD = 543; // Lorencia (141, 143)
const OPEN = { timeout: 90_000 };

const texts = async (page: Page, selector: string) => (await page.locator(selector).allTextContents()).map((text) => text.replace(/\s*\n\s*/g, ' ').trim());
const influence = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.duelAndGens.gens.influence);

async function talkToSteward(page: Page): Promise<void> {
  const steward = () => page.evaluate((number) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.monsterNumber === number)?.id ?? null, DUPRIAN_STEWARD);
  await expect.poll(steward, OPEN).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await steward())!);
  await expect(page.locator('.rml-npc_dialogue #panel')).toBeVisible(OPEN);
}

async function pageToAnswers(page: Page): Promise<void> {
  const answers = page.locator('.rml-npc_dialogue .nd-answer-row');
  for (let i = 0; i < 6 && !(await answers.first().isVisible()); i++) {
    await page.locator('.rml-npc_dialogue #btn_npc_next').click();
    await page.waitForTimeout(500);
  }
  await expect(answers.first()).toBeVisible();
}

/** Clicks the answer whose text matches, paging the answers when needed. */
async function choose(page: Page, pattern: RegExp): Promise<void> {
  await pageToAnswers(page);
  const row = page.locator('.rml-npc_dialogue .nd-answer-row').filter({ hasText: pattern });
  await expect(row.first()).toBeVisible(OPEN);
  await row.first().click();
}

/** "Leave Gens Duprian", then the steward's "Yes" (answer 905) when it asks again. */
async function leaveGens(page: Page): Promise<void> {
  await choose(page, /Leave Gens/i);
  await page.waitForTimeout(2_000);
  console.log('leave question:', await texts(page, '.rml-npc_dialogue .nd-answer-row'));
  const yes = page.locator('.rml-npc_dialogue .nd-answer-row').filter({ hasText: /^1\. Yes/ });
  if (await yes.count()) await yes.first().click();
}

test('joins the Duprian gens at its steward, shows the Gens window, leaves again', async ({ page }) => {
  test.setTimeout(10 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test7');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  // Not in a party (the server refuses a member of one).
  await page.evaluate(() => { const g = (window as unknown as Win).__game; const me = document.querySelector('#heroName')?.textContent; const i = g.party.members.findIndex((m) => m.name === me); if (i >= 0) g.party.leave(i); });

  await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 140, y: 140 }));
  await talkToSteward(page);
  await expect(page.locator('.rml-npc_dialogue #title')).toContainText('Duprian');
  await pageToAnswers(page);
  console.log('steward answers:', await texts(page, '.rml-npc_dialogue .nd-answer-row'));
  await page.screenshot({ path: 'test-results/gens-steward.png' });

  // A membership left from an earlier run is ended first.
  if (await influence(page) !== 0) {
    await leaveGens(page);
    await expect.poll(() => influence(page), OPEN).toBe(0);
    await page.keyboard.press('Escape');
    await talkToSteward(page);
  }

  // B without a family: the chat says so, no window.
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-npc_dialogue')).toHaveCount(0);
  await page.keyboard.press('b');
  await expect(page.locator('.chat-log')).toContainText('You have not joined a gens.');
  await expect(page.locator('.rml-gens_ranking')).toHaveCount(0);

  // Joining: the answer goes to the server, its answer changes the dialogue and the hero's family.
  await talkToSteward(page);
  await choose(page, /^4\. Join Gens Duprian/);
  await page.waitForTimeout(2_000);
  // The steward asks again; "Yes" sends the join request (answer 903).
  await page.screenshot({ path: 'test-results/gens-join-question.png' });
  await choose(page, /Yes\. I wish to join/);
  await expect.poll(() => influence(page), OPEN).toBe(1);
  await page.waitForTimeout(1_000);
  console.log('after joining:', await texts(page, '.rml-npc_dialogue .nd-line'));
  await page.screenshot({ path: 'test-results/gens-joined.png' });
  await page.keyboard.press('Escape');

  // B: the Gens Info window with the family, the title and the contribution (the server's GensInfo).
  await page.keyboard.press('b');
  await expect(page.locator('.rml-gens_ranking #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-gens_ranking #gr_gens_name')).toHaveText('Duprian', { timeout: 30_000 });
  await expect(page.locator('.rml-gens_ranking #gr_title_name')).toHaveText('Private');
  await expect(page.locator('.rml-gens_ranking #gr_contrib_value')).toHaveText(/^\d+$/);
  await page.waitForTimeout(2_000);
  await page.screenshot({ path: 'test-results/gens-window.png' });
  await page.keyboard.press('b');
  await expect(page.locator('.rml-gens_ranking')).toHaveCount(0);

  // Leaving at the steward: the family is gone, the next run starts the same.
  await talkToSteward(page);
  await leaveGens(page);
  await expect.poll(() => influence(page), OPEN).toBe(0);
  await page.keyboard.press('Escape');
  await expectNoPageErrors(errors);
});
