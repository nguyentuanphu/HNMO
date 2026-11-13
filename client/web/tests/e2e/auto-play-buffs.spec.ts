// Auto play casts the buffs of its settings like the desktop's helper (CMuHelper::Buff): a character
// that knows a self buff turns auto play on outside town, the helper casts the buff skill on the hero
// and the server's MagicEffectStatus puts the buff on the hero's buff strip. testgm's characters know
// every skill of their class (the game master test account), so its first one has a buff to cast.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface BuffsGame {
  online: { playerId: number; hero: { isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  autoPlay: { active: boolean; setConfig(config: unknown): void; requestStop(): void; stop(): void };
  skills: { skills: Map<number, number> };
  buffs: { has(id: number, buff: number): boolean };
}
type Win = Window & { __game: BuffsGame };

// The helper's buff skills and the buff each gives (BuffTarget's table), most classes' first.
const SELF_BUFFS: [skill: number, buff: number][] = [[48, 8], [16, 4], [27, 2], [28, 1], [77, 6], [64, 5], [233, 82], [218, 81]];

const walking = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking);
const active = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.autoPlay.active);

test('auto play casts its buff on the hero', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  // TargetedSkill (C3 07 19, skill big endian) the client sends.
  const casts: number[] = [];
  page.on('websocket', (socket) => socket.on('framesent', (frame) => {
    const p = frame.payload;
    if (typeof p !== 'string' && p[0] === 0xc3 && p[2] === 0x19) casts.push((p[3] << 8) | p[4]);
  }));
  await enterWorld(page, 'testgm');
  await expect(page.locator('.rml-mu_helper_bar #panel')).toBeVisible();
  const known = await page.evaluate(() => [...(window as unknown as Win).__game.skills.skills.values()]);
  const pick = SELF_BUFFS.find(([skill]) => known.includes(skill));
  expect(pick, `a self buff among the known skills ${known.join(', ')}`).toBeTruthy();
  const [skill, buff] = pick!;
  // Out of Lorencia's safe zone (it stops auto play).
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => walking(page), { timeout: 60_000 }).toBe(false);
  }
  // Only the buff: no attack skill, no fallback attack, no pick-up; a timed refresh every 60 seconds
  // (so the buff is cast at the start even when an earlier run left it on).
  await page.evaluate((buffSkill) => {
    (window as unknown as Win).__game.autoPlay.setConfig({
      huntingRange: 1, longRangeCounterAttack: false, returnToOriginalPosition: false, maxSecondsAway: 0,
      skills: [0, 0, 0], skillConditions: [0, 0, 0], skillIntervals: [0, 0, 0], useCombo: false,
      buffs: [buffSkill, 0, 0], buffDuration: false, buffDurationParty: false, buffCastInterval: 60,
      autoHeal: false, healThreshold: 0, supportParty: false, autoHealParty: false, healPartyThreshold: 0,
      useHealPotion: false, potionThreshold: 0, useDrainLife: false, useDarkRaven: false, darkRavenMode: 0,
      repairItem: false, obtainingRange: 0, pickAllItems: false, pickSelectItems: false, pickJewel: false,
      pickZen: false, pickAncient: false, pickExcellent: false, pickExtraItems: false, extraItems: [],
      useSelfDefense: false, autoAcceptFriend: false, autoAcceptGuild: false, fallbackBasicAttack: false,
    });
  }, skill);
  await page.keyboard.press('Home');
  await expect.poll(() => active(page), { timeout: 30_000 }).toBe(true);
  // The helper casts the buff skill, and the server puts the buff on the hero.
  await expect.poll(() => casts.includes(skill), { timeout: 60_000 }).toBe(true);
  await expect.poll(() => page.evaluate((b) => { const g = (window as unknown as Win).__game; return g.buffs.has(g.online.playerId, b); }, buff), { timeout: 30_000 }).toBe(true);
  await expect(page.locator('.rml-buff_strip .buff-slot').first()).toBeVisible();
  await page.screenshot({ path: 'test-results/auto-play-buffs.png' });
  // With the buff on, it isn't cast again before the refresh (one cast, or two while the first was on its way).
  const castsSoFar = casts.filter((c) => c === skill).length;
  await page.waitForTimeout(10_000);
  expect(casts.filter((c) => c === skill).length).toBeLessThanOrEqual(castsSoFar + 1);
  await page.keyboard.press('Home');
  await expect.poll(() => active(page), { timeout: 30_000 }).toBe(false);
  // Back to town for the next test.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(5_000);
  await expectNoPageErrors(errors);
});
