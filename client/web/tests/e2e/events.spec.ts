// The events like the desktop's: the Messenger of Archangel in Devias opens the Blood Castle entry window
// (blood_castle_enter.rml) with the castle of the hero's level enabled, Charon in Noria the Devil Square
// one (devil_square_enter.rml); and, fed recorded packets of a running event, the time window
// (blood_castle_time.rml), the countdown line (hud_status.rml), the result box (message_box_view.rml),
// the entry refusals and the Chaos Castle question (generic_confirm_dialog.rml). The account `ancient`'s
// first character is a level 330 Blade Knight (TestAccounts/Ancient.cs): Castle 6 and Square 5.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface EventsGame {
  mapNumber: number | null;
  online: { characters: Map<number, { id: number; kind: string; monsterNumber?: number }>; say(text: string): void; talkTo(character: unknown): void; walkTo(cell: { x: number; y: number }): boolean };
  events: {
    matches: { handle(bytes: Uint8Array): void; lastResult: { kind: string } | null };
    dialogs: { isOpen: boolean; lines: string[] };
  };
}
type Win = Window & { __game: EventsGame };

const OPEN = { timeout: 90_000 };
const texts = (page: Page, selector: string) => page.locator(selector).allTextContents().then((all) => all.map((t) => t.trim()));
const feed = (page: Page, bytes: number[]) => page.evaluate((b) => (window as unknown as Win).__game.events.matches.handle(new Uint8Array(b)), bytes);

async function talkToNpc(page: Page, map: string, mapNumber: number, near: { x: number; y: number }, number: number): Promise<void> {
  await page.evaluate((m) => (window as unknown as Win).__game.online.say(`/move ${m}`), map);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), OPEN).toBe(mapNumber);
  await page.waitForTimeout(3_000);
  await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), near);
  const npc = () => page.evaluate((n) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.monsterNumber === n)?.id ?? null, number);
  await expect.poll(npc, OPEN).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await npc())!);
}

test('the event entry windows, time, countdown and results', async ({ page }) => {
  test.setTimeout(12 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'ancient');

  // Blood Castle: the Messenger of Archangel (233) in Devias.
  await talkToNpc(page, 'devias', 2, { x: 214, y: 30 }, 233);
  await expect(page.locator('.rml-blood_castle_enter #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-blood_castle_enter #title')).toHaveText('Messenger of Archangel');
  expect(await texts(page, '.rml-blood_castle_enter .entry-label')).toEqual([
    'Castle 1 (level 15-80)', 'Castle 2 (level 81-130)', 'Castle 3 (level 131-180)', 'Castle 4 (level 181-230)',
    'Castle 5 (level 231-280)', 'Castle 6 (level 281-330)', 'Castle 7 (level 331-400)', 'Castle no. 8 (Master Level)',
  ]);
  expect(await texts(page, '.rml-blood_castle_enter .entry-btn.enabled .entry-label')).toEqual(['Castle 6 (level 281-330)']);
  await page.waitForTimeout(1_000);
  await page.screenshot({ path: 'test-results/events-blood-castle-entry.png' });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-blood_castle_enter')).toHaveCount(0);

  // Devil Square: Charon (237) in Noria.
  await talkToNpc(page, 'noria', 3, { x: 173, y: 106 }, 237);
  await expect(page.locator('.rml-devil_square_enter #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-devil_square_enter #title')).toHaveText('Devil Square');
  await expect(page.locator('.rml-devil_square_enter .entry-line')).toHaveCount(6);
  expect(await texts(page, '.rml-devil_square_enter .entry-label')).toHaveLength(7);
  expect(await texts(page, '.rml-devil_square_enter .entry-btn.enabled .entry-label')).toEqual(['The 5 Square (281-330 level)']);
  await page.waitForTimeout(1_000);
  await page.screenshot({ path: 'test-results/events-devil-square-entry.png' });
  await page.locator('.rml-devil_square_enter #frame_corner_close').click({ force: true });
  await expect(page.locator('.rml-devil_square_enter')).toHaveCount(0);

  // An entry refusal (C1 9A 02): an OK box.
  await feed(page, [0xc1, 4, 0x9a, 2]);
  await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText('The time to enter Blood Castle has passed.', OPEN);
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_primary').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);

  // Chaos Castle's opening state (C1 91 04, 0 minutes, 12 inside): the question with OK and Cancel.
  await feed(page, [0xc1, 7, 0x91, 4, 0, 12, 0]);
  await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText(['You can enter Chaos Castle now.', 'In Chaos Castle, Currently [12/100] entered.'], OPEN);
  await expect(page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel')).toBeVisible();
  await page.screenshot({ path: 'test-results/events-chaos-castle-question.png' });
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);

  // A running Blood Castle (the map number as if the hero were in Castle 6): its state (C1 9B 01, 600 s
  // left, 3 of 40 monsters), the countdown (C1 92 05: "Event ends") and the result (C1 93 01 FF).
  const map = await page.evaluate(() => (window as unknown as Win).__game.mapNumber);
  try {
    await page.evaluate(() => { (window as unknown as Win).__game.mapNumber = 16; });
    await feed(page, [0xc1, 13, 0x9b, 1, 600 & 0xff, 600 >> 8, 40, 0, 3, 0, 0xff, 0xff, 0xff]);
    await expect(page.locator('.rml-blood_castle_time #panel')).toBeVisible(OPEN);
    await expect(page.locator('.rml-blood_castle_time #kills')).toHaveText('Monster: ( 3/40 )');
    await expect(page.locator('.rml-blood_castle_time #time_left')).toHaveText('Time Left');
    await expect(page.locator('.rml-blood_castle_time #time')).toContainText('10:00:');
    await feed(page, [0xc1, 4, 0x92, 5]);
    await expect(page.locator('.rml-hud_status .countdown-text')).toHaveText(/^Blood Castle Event ends \(in (30|29|28) seconds\)$/, OPEN);
    await page.waitForTimeout(1_000);
    await page.screenshot({ path: 'test-results/events-blood-castle-time.png' });
    const name = [...Buffer.from('ancientDk')].concat([0, 0, 0, 0, 0, 0, 0]).slice(0, 12);
    const u32 = (v: number) => [v & 0xff, (v >> 8) & 0xff, (v >> 16) & 0xff, (v >>> 24) & 0xff];
    await feed(page, [0xc1, 29, 0x93, 1, 0xff, ...name, ...u32(12345), ...u32(250000), ...u32(1000000)]);
    await expect(page.locator('.rml-message_box_view #panel')).toBeVisible(OPEN);
    expect(await texts(page, '.rml-message_box_view .box-line')).toEqual([
      'Congratulations! You have successfully completed the Blood Castle Quest!', 'Rewarded Exp: 250000', 'Rewarded Zen: 1000000', 'Blood Castle Point: 12345',
    ]);
    await page.waitForTimeout(1_000);
    await page.screenshot({ path: 'test-results/events-blood-castle-result.png' });
    await page.locator('.rml-message_box_view .box-btn').click();
    await expect(page.locator('.rml-message_box_view')).toHaveCount(0);
    // The event over (C1 9B 02): the time window goes.
    await feed(page, [0xc1, 13, 0x9b, 2, 0, 0, 0, 0, 0, 0, 0xff, 0xff, 0xff]);
    await expect(page.locator('.rml-blood_castle_time')).toHaveCount(0, OPEN);
  } finally {
    await page.evaluate((m) => { (window as unknown as Win).__game.mapNumber = m; }, map);
  }

  // Back to Lorencia for the other tests.
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.mapNumber), OPEN).toBe(0);
  await expectNoPageErrors(errors);
});
