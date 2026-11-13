// The party like the desktop's: one player invites another (PartyInviteRequest), the other is asked
// and accepts, both get the member list and its cards (party_list.rml), and leaving ends it.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface PartyGame {
  online: { characters: Map<number, { id: number; name: string; kind: string }>; say(text: string): void };
  party: { members: { name: string }[]; invite(target: unknown): void; leave(index: number): void };
}
type Win = Window & { __game: PartyGame };

const members = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.party.members.map((m) => m.name));

test('a party of two: invite, accept, the cards, leave', async ({ browser }) => {
  test.setTimeout(6 * 60_000);
  const a = await (await browser.newContext()).newPage();
  const b = await (await browser.newContext()).newPage();
  const errors: string[] = [];
  for (const page of [a, b]) page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(a, 'test7');
  await enterWorld(b, 'test8');
  for (const page of [a, b]) await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await a.waitForTimeout(10_000); // both land in town before looking for each other
  // A party kept from an earlier run comes back on entering: leave it first.
  for (const page of [a, b]) {
    await page.evaluate(() => { const g = (window as unknown as Win).__game; const me = document.querySelector('#heroName')?.textContent; const i = g.party.members.findIndex((m) => m.name === me); if (i >= 0) g.party.leave(i); });
    await expect.poll(() => members(page), { timeout: 30_000 }).toHaveLength(0);
  }
  // A sees B's character.
  const bName = await b.evaluate(() => [...document.querySelectorAll('#heroName')].map((e) => e.textContent ?? '')[0]);
  await expect.poll(() => a.evaluate((name) => [...(window as unknown as Win).__game.online.characters.values()].some((c) => c.kind === 'player' && c.name === name), bName), { timeout: 90_000 }).toBe(true);
  await a.evaluate((name) => {
    const g = (window as unknown as Win).__game;
    const target = [...g.online.characters.values()].find((c) => c.kind === 'player' && c.name === name);
    if (target) g.party.invite(target);
  }, bName);
  await b.click('.confirm-dialog .confirm-ok', { timeout: 60_000 }); // B is asked in the game and accepts
  await expect.poll(() => members(a), { timeout: 60_000 }).toHaveLength(2);
  await expect.poll(() => members(b), { timeout: 60_000 }).toHaveLength(2);
  await expect(a.locator('.rml-party_list .card')).toHaveCount(2, { timeout: 60_000 });
  await a.screenshot({ path: 'test-results/party.png' });
  // B leaves: the party is over for both (a party of one isn't kept).
  await b.evaluate(() => { const g = (window as unknown as Win).__game; g.party.leave(g.party.members.findIndex((m) => m.name === document.querySelector('#heroName')?.textContent)); });
  await expect.poll(() => members(b), { timeout: 60_000 }).toHaveLength(0);
  await expectNoPageErrors(errors);
});
