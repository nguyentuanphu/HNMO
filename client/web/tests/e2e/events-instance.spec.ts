// Illusion Temple, Doppelganger and Imperial Guardian like the desktop's: in Devias, Mirage opens the
// temple's entry window (cursed_temple_enter.rml) with the hero's temple and band, Lugard the
// Doppelganger one (doppelganger_enter.rml), Jerint the Imperial Guardian one (empire_guardian_enter.rml);
// fed recorded packets of running events: the temple's HUD (cursed_temple_system.rml: time, points,
// tutorial, score effect), its result (cursed_temple_result.rml), the Doppelganger frame
// (doppelganger_frame.rml) and its boxes, the Imperial Guardian timer (empire_guardian_timer.rml) and
// its boxes (generic_confirm_dialog.rml). The account `quest2`'s characters are level 220
// (TestAccounts/Quest220.cs): the first temple. A real start with the game master `testgm2`
// (/startit, /skipwait) runs at the end when the server lets it in.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface InstanceGame {
  mapNumber: number | null;
  connection: { dispatch(bytes: Uint8Array): void };
  online: { playerId: number; characters: Map<number, { id: number; kind: string; monsterNumber?: number | null }>; say(text: string): void; talkTo(character: unknown): void; walkTo(cell: { x: number; y: number }): boolean };
  instanceEvents: { events: { temple: { shown: boolean; state: number }; lastBox: string[] | null } };
  events: { dialogs: { isOpen: boolean; lines: string[] } };
}
type Win = Window & { __game: InstanceGame };

const OPEN = { timeout: 90_000 };
const texts = (page: Page, selector: string) => page.locator(selector).allTextContents().then((all) => all.map((t) => t.trim()));
const feed = (page: Page, bytes: number[]) => page.evaluate((b) => (window as unknown as Win).__game.connection.dispatch(new Uint8Array(b)), bytes);
const u16 = (v: number) => [v & 0xff, (v >> 8) & 0xff];
const u32 = (v: number) => [v & 0xff, (v >> 8) & 0xff, (v >> 16) & 0xff, (v >>> 24) & 0xff];
const name10 = (name: string) => [...Buffer.from(name)].concat(new Array(10).fill(0)).slice(0, 10);
const setMap = (page: Page, map: number | null) => page.evaluate((m) => { (window as unknown as Win).__game.mapNumber = m; }, map);

// Walks to the NPC and talks to it until its window opens (a walk can stop short on a slow machine).
async function walkAndTalk(page: Page, near: { x: number; y: number }, number: number, windowSelector: string): Promise<void> {
  const npc = () => page.evaluate((n) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.monsterNumber === n)?.id ?? null, number);
  await expect(async () => {
    if (await npc() === null) await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), near);
    await expect.poll(npc, { timeout: 30_000 }).not.toBeNull();
  }).toPass({ timeout: 300_000 });
  const id = (await npc())!;
  await expect(async () => {
    if (await page.locator(windowSelector).count() === 0) {
      await page.evaluate((i) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(i)); }, id);
    }
    await expect(page.locator(windowSelector)).toBeVisible({ timeout: 20_000 });
  }).toPass({ timeout: 240_000 });
}

async function closeDialog(page: Page): Promise<void> {
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_primary').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0, OPEN);
}

async function moveTo(page: Page, map: string, number: number): Promise<void> {
  await page.evaluate((m) => (window as unknown as Win).__game.online.say(`/move ${m}`), map);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), OPEN).toBe(number);
  await page.waitForTimeout(3_000);
}

test('the Illusion Temple, Doppelganger and Imperial Guardian windows', async ({ page }) => {
  test.setTimeout(15 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'quest2');
  await moveTo(page, 'devias', 2);

  // Mirage (385): the temple of the hero's level (220: the first) and its band enabled.
  await walkAndTalk(page, { x: 198, y: 51 }, 385, '.rml-cursed_temple_enter #panel');
  await expect(page.locator('.rml-cursed_temple_enter #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-cursed_temple_enter #title')).toHaveText('Do you wish to go to the Illusion Temple?');
  await expect(page.locator('.rml-cursed_temple_enter #ct_temple')).toHaveText('The 1 Illusion Temple');
  expect(await texts(page, '.rml-cursed_temple_enter .temple-band .temple-line')).toEqual([
    'Level 220-270 Entrance enabled.', 'Level 271-320 Entrance disabled.', 'Level 321-350 Entrance disabled.',
    'Level 351-380 Entrance disabled.', 'Level 381-400 Entrance disabled.', 'Master Level Entrance disabled.',
  ]);
  // The members of each temple (C1 BF 03): the hero's temple's count.
  await feed(page, [0xc1, 10, 0xbf, 0x03, 7, 1, 2, 3, 4, 5]);
  await expect(page.locator('.rml-cursed_temple_enter #ct_members')).toHaveText('Current members: 7', OPEN);
  await page.waitForTimeout(1_000);
  await page.screenshot({ path: 'test-results/events-temple-entry.png' });
  await page.locator('.rml-cursed_temple_enter #btn_close').click({ force: true });
  await expect(page.locator('.rml-cursed_temple_enter')).toHaveCount(0);

  // Lugard (540): the mirror's lines, Enter open ("You may now enter.").
  await walkAndTalk(page, { x: 231, y: 64 }, 540, '.rml-doppelganger_enter #panel');
  await expect(page.locator('.rml-doppelganger_enter #panel')).toBeVisible(OPEN);
  const lugard = await texts(page, '.rml-doppelganger_enter .entry-text');
  expect(lugard[0]).toBe('Lugard');
  expect(lugard).toContain('Will you show me your mirror?');
  expect(lugard).toContain('Mirror of Dimensions');
  expect(lugard[lugard.length - 1]).toBe('You may now enter.');
  expect(await texts(page, '.rml-doppelganger_enter .entry-label')).toEqual(['Enter', 'Close']);
  await expect(page.locator('.rml-doppelganger_enter .entry-btn.locked')).toHaveCount(0);
  // A refusal (C1 BF 0E 02) locks Enter.
  await feed(page, [0xc1, 5, 0xbf, 0x0e, 2]);
  await expect(page.locator('.rml-doppelganger_enter .entry-btn.locked')).toHaveCount(1, OPEN);
  await page.waitForTimeout(1_000);
  await page.screenshot({ path: 'test-results/events-doppelganger-entry.png' });
  await page.locator('.rml-doppelganger_enter #frame_corner_close').click({ force: true });
  await expect(page.locator('.rml-doppelganger_enter')).toHaveCount(0);

  // Jerint (522), next to the Devias 3 warp: Gaion's Order, the warning and the party line.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move devias3'));
  await expect.poll(() => page.evaluate(() => { const g = (window as unknown as Win).__game as unknown as { hero: { cell: { y: number } } }; return g.hero.cell.y; }), OPEN).toBeGreaterThan(200);
  await page.waitForTimeout(3_000);
  await walkAndTalk(page, { x: 228, y: 223 }, 522, '.rml-empire_guardian_enter #panel');
  await expect(page.locator('.rml-empire_guardian_enter #panel')).toBeVisible({ timeout: 180_000 });
  const jerint = await texts(page, '.rml-empire_guardian_enter .entry-text');
  expect(jerint[0]).toBe('Jerint the Assistant');
  expect(jerint).toContain("Gaion's Order");
  expect(jerint).toContain('!! Warning !!');
  expect(jerint).toContain('Complete Secromicon.');
  await page.waitForTimeout(1_000);
  await page.screenshot({ path: 'test-results/events-imperial-guardian-entry.png' });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-empire_guardian_enter')).toHaveCount(0);

  const map = await page.evaluate(() => (window as unknown as Win).__game.mapNumber);
  const heroId = await page.evaluate(() => (window as unknown as Win).__game.online.playerId);
  try {
    // A running Illusion Temple (the map as if in the first temple): the ready phase (C1 BF 09, state 1)
    // opens the HUD with the tutorial; the state (C1 BF 01): 10 minutes, the hero on the allied team.
    await setMap(page, 45);
    await feed(page, [0xc1, 6, 0xbf, 0x09, 1, 1]);
    await expect(page.locator('.rml-cursed_temple_system #game_time_frame')).toBeVisible(OPEN);
    // One of the three tutorial steps shows (they cycle every 10 s; on a slow, loaded machine the
    // window can open past step 1, so accept any step rather than racing the timer).
    await expect(page.locator('.rml-cursed_temple_system .tutorial-line').first()).toContainText('STEP', OPEN);
    const status = (allied: number, illusion: number) => [0xc1, 20, 0xbf, 0x01, ...u16(600), 0xff, 0xff, 0, 0, allied, illusion, 0, 1, ...u16(heroId), 45, 150, 80, 0];
    await feed(page, status(0, 0));
    // 10:00, drawn twice like the original: two runs of two minute digits and of the seconds' one.
    await expect(page.locator('.rml-cursed_temple_system .ct-time-minutes .ct-digit')).toHaveCount(4, OPEN);
    await expect(page.locator('.rml-cursed_temple_system .ct-time-seconds .ct-digit')).toHaveCount(2);
    await expect(page.locator('.rml-cursed_temple_system #mini_map_markers .sprite')).toHaveCount(5);
    // A point for the hero's team: the score effect with its digits.
    await feed(page, status(1, 0));
    await expect(page.locator('.rml-cursed_temple_system #score_allied_ones')).toBeVisible(OPEN);
    // Kill points (C1 BF 06).
    await feed(page, [0xc1, 5, 0xbf, 0x06, 5]);
    await expect(page.locator('.rml-cursed_temple_system #ct_kills .ct-digit')).toHaveCount(1, OPEN);
    await page.waitForTimeout(2_000);
    await page.screenshot({ path: 'test-results/events-temple-hud.png' });
    // The result (C1 BF 04): the allies 2, the Illusion Sorcery 1, the hero on the winning side.
    const player = (who: string, team: number, packedClass: number, exp: number) => [...name10(who), 45, team, packedClass, 0, 0, 0, ...u32(exp)];
    const result = [0xc1, 7 + 40, 0xbf, 0x04, 2, 1, 2, ...player('quest2Dl', 0, 0x34, 152340), ...player('Rival', 1, 0x00, 64210)];
    await feed(page, result);
    await expect(page.locator('.rml-cursed_temple_result #panel')).toBeVisible(OPEN);
    await expect(page.locator('.rml-cursed_temple_result #ct_hero_list')).toHaveText('Hero List');
    expect(await texts(page, '.rml-cursed_temple_result .rows-allied .result-row .result-cell')).toEqual(['HNMO alliance', 'quest2Dl', 'Lord Emperor', '152340', '2']);
    expect(await texts(page, '.rml-cursed_temple_result .rows-illusion .result-row .result-cell')).toEqual(['Illusion Sorcery', 'Rival', 'Dark Wizard', '64210', '1']);
    await expect(page.locator('.rml-cursed_temple_result .result-banner.success')).toBeVisible();
    await expect(page.locator('.rml-cursed_temple_system')).toHaveCount(0, OPEN);
    await page.waitForTimeout(2_000);
    await page.screenshot({ path: 'test-results/events-temple-result.png' });
    await page.locator('.rml-cursed_temple_result #btn_close').click({ force: true });
    await expect(page.locator('.rml-cursed_temple_result')).toHaveCount(0);
    await feed(page, [0xc1, 6, 0xbf, 0x09, 1, 3]);
    // An entry refusal (C1 BF 00 03) is a red system line.
    await feed(page, [0xc1, 5, 0xbf, 0x00, 3]);
    await expect(page.getByText('The admission and scroll levels do not match.').first()).toBeAttached(OPEN);

    // A running Doppelganger: the start (C1 BF 10 02) opens the frame and its box.
    await setMap(page, 65);
    await feed(page, [0xc1, 5, 0xbf, 0x10, 2]);
    await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText([
      '3 monsters reaching the magic circle,', '', 'the character dying, the server disconnecting, or using the warp command', '', 'will result in Doppelganger defense failure.',
    ], OPEN);
    await closeDialog(page);
    await expect(page.locator('.rml-doppelganger_frame #back')).toBeVisible(OPEN);
    // One of three monsters through (C1 BF 14), the time and the hero on the path (C1 BF 12), the Ice
    // Walker (C1 BF 11) and the monsters' gauge (C1 BF 0F).
    await feed(page, [0xc1, 6, 0xbf, 0x14, 3, 1]);
    await feed(page, [0xc1, 12, 0xbf, 0x12, ...u16(300), 1, 0, ...u16(heroId), 65, 11]);
    await feed(page, [0xc1, 6, 0xbf, 0x11, 0, 5]);
    await feed(page, [0xc1, 5, 0xbf, 0x0f, 11]);
    await expect(page.locator('.rml-doppelganger_frame #dg_passed')).toHaveText('Monsters Passed: ( 1/3 )', OPEN);
    await expect(page.locator('.rml-doppelganger_frame #dg_passed')).toHaveClass(/passed-one/);
    await expect(page.locator('.rml-doppelganger_frame #dg_time_label')).toHaveText('Time Left');
    await expect(page.locator('.rml-doppelganger_frame #dg_time')).toHaveText(/^05:00:\d\d$/);
    await expect(page.locator('.rml-doppelganger_frame .marker.hero')).toHaveCount(1);
    await expect(page.locator('.rml-doppelganger_frame #icewalker')).toBeVisible();
    await expect(page.locator('.rml-doppelganger_frame .bar')).toHaveCount(2);
    await page.waitForTimeout(2_000);
    await page.screenshot({ path: 'test-results/events-doppelganger-frame.png' });
    // The defense held (C1 BF 13 00).
    await feed(page, [0xc1, 12, 0xbf, 0x13, 0, 0, 0, 0, ...u32(0)]);
    await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText(['Congratulations!', '', "You've successfully defended Doppelganger."], OPEN);
    await expect(page.locator('.rml-doppelganger_frame #dg_time')).toHaveText('00:00:00');
    await closeDialog(page);

    // A running Imperial Guardian: entered round 3, zone 2 (C1 F7 02), the timer (C1 F7 04: time attack, 3:20, 7 monsters).
    await setMap(page, 69);
    await expect(page.locator('.rml-doppelganger_frame')).toHaveCount(0, OPEN);
    await feed(page, [0xc1, 12, 0xf7, 0x02, 0, 3, 2, 0, ...u32(600_000)]);
    await feed(page, [0xc1, 16, 0xf7, 0x04, 2, 0, 0, 0, ...u32(200_000), 7, 0, 0, 0]);
    await expect(page.locator('.rml-empire_guardian_timer #kills')).toHaveText('Round 3 (Zone 2)', OPEN);
    await expect(page.locator('.rml-empire_guardian_timer #time_left')).toHaveText('Time Left (Remaining Monsters)');
    await expect(page.locator('.rml-empire_guardian_timer #time')).toHaveText('03:20(7)');
    await page.waitForTimeout(1_000);
    await page.screenshot({ path: 'test-results/events-imperial-guardian-timer.png' });
    // The zone cleared (C1 F7 06 01): its box, the timer gone.
    await feed(page, [0xc1, 12, 0xf7, 0x06, 1, 0, 0, 0, ...u32(0)]);
    await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText(['Fortress of Empire Guardians Round 3', 'Zone 2 cleared'], OPEN);
    await expect(page.locator('.rml-empire_guardian_timer')).toHaveCount(0, OPEN);
    await closeDialog(page);
    // An entry refusal (C1 F7 02 05).
    await feed(page, [0xc1, 12, 0xf7, 0x02, 5, 0, 0, 0, ...u32(0)]);
    await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText('You can only enter as a member of a party.', OPEN);
    await closeDialog(page);
  } finally {
    await setMap(page, map);
  }

  // Back to Lorencia for the other tests.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), OPEN).toBe(0);
  await expectNoPageErrors(errors);
});

// The real Illusion Temple with the game master testgm2 (a level 400 Dimension Master: the master
// temple): /startit opens the entrance (the server's announcement in the chat), Mirage's Enter sends
// the request (C1 BF 00) and the server answers. The master temple's fee is 8,000,000 zen, more than
// testgm2 has (and /setmoney is off by default), so the server refuses with result 3: the red line.
// A whole match needs two players with the fee, which these test accounts don't have.
test('a real Illusion Temple entry request (game master)', async ({ page }) => {
  test.setTimeout(15 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'testgm2');
  const say = (text: string) => page.evaluate((t) => (window as unknown as Win).__game.online.say(t), text);
  await moveTo(page, 'devias', 2);
  await say('/item 13 61');
  await page.waitForTimeout(3_000);
  await say('/startit');
  await walkAndTalk(page, { x: 198, y: 51 }, 385, '.rml-cursed_temple_enter #panel');
  await expect(page.locator('.rml-cursed_temple_enter #ct_temple')).toHaveText('The 6 Illusion Temple');
  await expect(page.locator('.rml-cursed_temple_enter .band-hero:visible')).toHaveCount(1);
  await expect(page.getByText(/Illusion Temple entrance is open/).first()).toBeAttached(OPEN);
  await page.locator('.rml-cursed_temple_enter #btn_enter').click({ force: true });
  await expect(page.getByText('The admission and scroll levels do not match.').first()).toBeAttached(OPEN);
  await page.waitForTimeout(1_000);
  await page.screenshot({ path: 'test-results/events-temple-real-request.png' });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-cursed_temple_enter')).toHaveCount(0);
  await say('/move lorencia');
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), OPEN).toBe(0);
  await expectNoPageErrors(errors);
});
