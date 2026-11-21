// Ultimate Autoplay's ground guide with a real server: while auto play hunts, the hunting-range ring, the sparkle
// trail and the goal ring are shown. They live in the game's root group (z up) like every other game object; in the
// scene itself they were drawn standing up, out of sight.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Fighter { id: number; kind: string; dead: boolean; cell: { x: number; y: number } }
interface AutoGame {
  online: { characters: Map<number, Fighter>; hero: { cell: { x: number; y: number }; isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  autoPlay: { active: boolean; setConfig(config: unknown): void };
}
type Win = Window & { __game: AutoGame };

const active = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.autoPlay.active);

test('sparkles: the hunting ring and the trail show while auto play hunts', async ({ page }) => {
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const kills: number[] = [];
  page.on('websocket', (socket) => socket.on('framereceived', (frame) => {
    // ObjectGotKilled (C1 09 17) with the hero (0x0200) as the killer.
    if (typeof frame.payload !== 'string' && frame.payload[0] === 0xc1 && frame.payload[2] === 0x17 && frame.payload[7] === 0x02 && frame.payload[8] === 0x00) kills.push(Date.now());
  }));
  await enterWorld(page, 'test400');
  await expect(page.locator('.rml-mu_helper_bar #panel')).toBeVisible();
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(8_000);
  // Out of town to the spiders (the safe zone stops auto play).
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 70 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
  }
  // The settings: hunt within 6 cells with Cyclone (22), pick up everything within 3 cells.
  await page.evaluate(() => {
    const g = (window as unknown as Win).__game;
    g.autoPlay.setConfig({
      huntingRange: 6, longRangeCounterAttack: false, returnToOriginalPosition: true, maxSecondsAway: 10,
      skills: [22, 0, 0], skillConditions: [0, 0, 0], skillIntervals: [0, 0, 0], useCombo: false,
      buffs: [0, 0, 0], buffDuration: false, buffDurationParty: false, buffCastInterval: 0,
      autoHeal: false, healThreshold: 0, supportParty: false, autoHealParty: false, healPartyThreshold: 0,
      useHealPotion: true, potionThreshold: 40, useDrainLife: false, useDarkRaven: false, darkRavenMode: 0,
      repairItem: false, obtainingRange: 3, pickAllItems: true, pickSelectItems: false, pickJewel: false,
      pickZen: false, pickAncient: false, pickExcellent: false, pickExtraItems: false, extraItems: [],
      useSelfDefense: true, autoAcceptFriend: false, autoAcceptGuild: false, fallbackBasicAttack: true,
    });
  });
  await page.keyboard.press('Home');
  await expect.poll(() => active(page), { timeout: 30_000 }).toBe(true);
  type S = { rangeRing: { visible: boolean }; points: { visible: boolean }; destRing: { visible: boolean } };
  const sparkles = () => page.evaluate(() => { const s = (window as unknown as { __game: { autoPlaySparkles?: S } }).__game.autoPlaySparkles; return s ? { ring: s.rangeRing.visible, trail: s.points.visible, dest: s.destRing.visible } : null; });
  await expect.poll(async () => (await sparkles())?.ring, { timeout: 30_000 }).toBe(true);
  await expect.poll(async () => (await sparkles())?.trail, { timeout: 120_000 }).toBe(true);
  console.log('SPARKLES', JSON.stringify(await sparkles()));
  await page.screenshot({ path: 'test-results/ultimate-sparkles.png' });
  // It kills a monster by itself.
  await expect.poll(() => kills.length, { timeout: 240_000 }).toBeGreaterThan(0);
  await page.keyboard.press('Home');
  await expect.poll(() => active(page), { timeout: 30_000 }).toBe(false);
  await expectNoPageErrors(errors);
});

