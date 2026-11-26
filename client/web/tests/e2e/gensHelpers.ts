// Joining a gens at Lorencia's Duprian steward, the way gens.spec.ts does it: guilds need the same gens since the owner's
// decision of 26 Nov 2026 (GensConfiguration.GuildRequiresGens), so tests that make a guild join one first.
import { expect, type Page } from '@playwright/test';

const DUPRIAN_STEWARD = 543; // Lorencia (141, 143)
const OPEN = { timeout: 90_000 };

interface StewardGame {
  online: { characters: Map<number, { monsterNumber: number | null }>; talkTo(npc: unknown): void; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  duelAndGens: { gens: { influence: number } };
  mapNumber: number;
  party: { members: { name: string }[]; leave(index: number): void };
}
type Win = Window & { __game: StewardGame };

/** The hero's gens (0 = none, 1 = Duprian, 2 = Vanert). */
export const gensOf = (page: Page): Promise<number> => page.evaluate(() => (window as unknown as Win).__game.duelAndGens.gens.influence);

async function talkToSteward(page: Page): Promise<void> {
  const steward = () => page.evaluate((number) => [...(window as unknown as Win).__game.online.characters.entries()].find(([, c]) => c.monsterNumber === number)?.[0] ?? null, DUPRIAN_STEWARD);
  await expect.poll(steward, OPEN).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await steward())!);
  await expect(page.locator('.rml-npc_dialogue #panel')).toBeVisible(OPEN);
}

async function choose(page: Page, pattern: RegExp): Promise<void> {
  const answers = page.locator('.rml-npc_dialogue .nd-answer-row');
  for (let i = 0; i < 6 && !(await answers.first().isVisible()); i++) {
    await page.locator('.rml-npc_dialogue #btn_npc_next').click();
    await page.waitForTimeout(500);
  }
  const row = answers.filter({ hasText: pattern });
  await expect(row.first()).toBeVisible(OPEN);
  await row.first().click();
}

/** Makes the hero a member of the Duprian gens (it must be out of a party and a guild, level 50 or more). */
export async function joinDuprian(page: Page): Promise<void> {
  if (await gensOf(page) === 1) return;
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), OPEN).toBe(0);
  await page.waitForTimeout(5_000);
  // Not in a party (the server refuses a member of one).
  await page.evaluate(() => { const g = (window as unknown as Win).__game; const me = document.querySelector('#heroName')?.textContent; const i = g.party.members.findIndex((m) => m.name === me); if (i >= 0) g.party.leave(i); });
  await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 140, y: 140 }));
  await talkToSteward(page);
  await choose(page, /Join Gens Duprian/);
  await page.waitForTimeout(2_000);
  await choose(page, /Yes\. I wish to join/);
  await expect.poll(() => gensOf(page), OPEN).toBe(1);
  await page.keyboard.press('Escape');
}
