// Kalima, Crywolf and the castle siege like the desktop's (ui/fieldEventWindows.ts). The account `socket`
// (TestAccounts/Socket.cs) has a level 380 Dark Knight first (socketDk) and no other test uses it; it is no game master and has no
// Lost Map, so a Kalima gate, a running Crywolf battle and a running siege come as recorded packets
// (window.__game.connection.dispatch), while the maps (Kalima, Crywolf, the Valley of Loren), the move
// commands, the Lorencia gate to the Valley and the guardsman's status are the server's own.
// The account is left in Lorencia.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface FieldGame {
  mapNumber: number | null;
  world: { mapNumber: number; worldNumber: number; flags: Uint16Array } | null;
  hero: { cell: { x: number; y: number } };
  connection: { dispatch(bytes: Uint8Array): void };
  online: {
    characters: Map<number, { id: number; name: string; kind: string; monsterNumber: number | null; cell: { x: number; y: number } }>;
    say(text: string): void; talkTo(character: unknown): void; walkTo(cell: { x: number; y: number }): boolean;
  };
  fieldEvents: {
    kalimaTooltip(item: { group: number; number: number; level: number; durability: number }, hero: { classIndex: number; level: number }): { text: string; background: string | null }[];
    crywolf: { state: number; occupation: number };
    dialogs: { isOpen: boolean; lines: string[] };
    progress: { isOpen: boolean; lines: string[] };
    guard: { isOpen: boolean };
    siege: { phase: number; battleStarted: boolean };
  };
}
type Win = Window & { __game: FieldGame };

const OPEN = { timeout: 90_000 };
const game = <T>(page: Page, read: (g: FieldGame) => T) => page.evaluate((source) => {
  // eslint-disable-next-line no-new-func
  const fn = new Function('g', `return (${source})(g);`) as (g: FieldGame) => unknown;
  return fn((window as unknown as Win).__game);
}, read.toString()) as Promise<T>;
const feed = (page: Page, bytes: number[]) => page.evaluate((b) => (window as unknown as Win).__game.connection.dispatch(new Uint8Array(b)), bytes);
const say = (page: Page, text: string) => page.evaluate((t) => (window as unknown as Win).__game.online.say(t), text);
const shadowText = (page: Page, doc: string, selector: string) => page.locator(`.rml-${doc} ${selector}`).allTextContents().then((all) => all.map((t) => t.trim()).filter(Boolean));
const c1 = (code: number, sub: number, ...data: number[]) => [0xc1, 4 + data.length, code, sub, ...data];
const u32le = (v: number) => [v & 0xff, (v >> 8) & 0xff, (v >> 16) & 0xff, (v >>> 24) & 0xff];
const name = (text: string, length: number) => [...Array.from({ length }, (_, i) => text.charCodeAt(i) || 0)];

async function moveTo(page: Page, map: string, number: number): Promise<void> {
  await say(page, `/move ${map}`);
  await expect.poll(() => game(page, (g) => g.mapNumber), OPEN).toBe(number);
  await expect.poll(() => game(page, (g) => g.world?.mapNumber ?? null), OPEN).toBe(number);
  await page.waitForTimeout(2_000);
}

async function walkLegs(page: Page, legs: { x: number; y: number }[], map: number): Promise<void> {
  for (const leg of legs) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), leg);
    await expect.poll(() => page.evaluate(() => { const g = (window as unknown as Win).__game as unknown as { mapNumber: number; online: { hero: { isWalking: boolean } } }; return { map: g.mapNumber, walking: g.online.hero.isWalking }; })
      .then((s) => s.map !== map || !s.walking), { timeout: 120_000 }).toBe(true);
    if (await game(page, (g) => g.mapNumber) !== map) return;
  }
}

async function closeDialog(page: Page): Promise<void> {
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_primary').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);
}

test('Kalima, Crywolf and the castle siege', async ({ page }) => {
  test.setTimeout(20 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'socket');
  await moveTo(page, 'lorencia', 0);

  // ---- Kalima ----
  // The Lost Map's tooltip: the Kalima level table (the Dark Lord's), the +3 row lit.
  const lines = await game(page, (g) => g.fieldEvents.kalimaTooltip({ group: 14, number: 28, level: 3, durability: 1 }, { classIndex: 4, level: 380 }));
  expect(lines.map((l) => l.text.trim())).toContain('Magic stone will appear when you throw it in the screen');
  expect(lines.filter((l) => l.background === 'darkYellow').map((l) => l.text.replace(/\s+/g, ' ').trim())).toEqual(['3 161~400']);
  const symbol = await game(page, (g) => g.fieldEvents.kalimaTooltip({ group: 14, number: 29, level: 0, durability: 3 }, null as unknown as { classIndex: number; level: number }));
  expect(symbol.map((l) => l.text)).toEqual(['3 / 5', '2 is lacking to create lost map.']);

  // A gate a thrown Lost Map +2 opened (the summon of monster 153, C2 1F) next to the hero.
  const cell = await game(page, (g) => g.hero.cell);
  const gate = [0x2f, 0x01, 0x00, 153, cell.x + 2, cell.y, cell.x + 2, cell.y, 0x10, ...name('Tester', 10), 0];
  await feed(page, [0xc2, 0, 5 + gate.length, 0x1f, 1, ...gate]);
  await expect.poll(() => game(page, (g) => { const c = g.online.characters.get(0x2f01); return c ? `${c.kind}|${c.name}` : null; }), OPEN).toBe('kalimaGate|Magic stone of Tester .2 to Kalima');
  await expect(page.locator('#labels .label.kalimaGate')).toHaveText('Magic stone of Tester .2 to Kalima', OPEN);
  await page.screenshot({ path: 'test-results/field-kalima-gate.png' });

  // Passing a gate takes the hero to Kalima: its map loads from World25 (the map change the server sends).
  await feed(page, [0xc3, 15, 0x1c, 0x0f, 1, 0, 24, 10, 16, 0, 0, 0, 0, 0, 0]);
  await expect.poll(() => game(page, (g) => g.world ? `${g.world.mapNumber}/${g.world.worldNumber}` : null), OPEN).toBe('24/25');
  await page.waitForTimeout(3_000);
  await page.screenshot({ path: 'test-results/field-kalima-map.png' });
  await moveTo(page, 'lorencia', 0);

  // ---- Crywolf ----
  await moveTo(page, 'crywolf', 34);
  const baseFlags = await game(page, (g) => [...(g.world?.flags ?? [])].reduce((a, b) => (a * 31 + b) >>> 0, 7));
  // The ready state (C1 BD 00: peace, Ready), the altars and the statue (BD 02), Balgass and the dark elves (BD 05).
  await feed(page, c1(0xbd, 0x00, 0, 3));
  await feed(page, c1(0xbd, 0x02, ...u32le(80), 0x12, 0x02, 0x01, 0x00, 0x02, 0, 0, 0));
  await feed(page, c1(0xbd, 0x05, ...u32le(60), 5, 0, 0, 0));
  await expect(page.locator('.rml-crywolf #main')).toBeVisible(OPEN);
  await expect.poll(() => shadowText(page, 'crywolf', '#dark_elf_text'), OPEN).toEqual(['Dark Elf (5/12)']);
  // The notice board: four lines, a page every ten seconds (the guardian lines first, then the missions and rules).
  const heads = ['You have been registered to be a guardian to protect the wolf.', '< Mission Point : 1. Defend the Wolf statue >',
    '< Mission Point : 2. Defeat Balgass >', '<Success reparation >', '<Failure penalty>'];
  await expect.poll(async () => { const n = await shadowText(page, 'crywolf', '.notice'); return n.length >= 3 && heads.includes(n[0]); }, OPEN).toBe(true);
  await page.waitForTimeout(2_000);
  await page.screenshot({ path: 'test-results/field-crywolf-ready.png' });
  // The battle (Start), the time (BD 04: 0 h 12 min), Balgass on the field.
  await feed(page, c1(0xbd, 0x00, 0, 4));
  await feed(page, c1(0xbd, 0x04, 0, 12));
  await feed(page, c1(0xbd, 0x05, ...u32le(60), 5, 0, 0, 0));
  await expect.poll(() => shadowText(page, 'crywolf', '#balgass_text'), OPEN).toEqual(['Balgass']);
  await expect(page.locator('.rml-crywolf .notice')).toHaveCount(0);
  // A refused contract (BD 03, result 0) for a level 380 hero: "try again".
  await feed(page, c1(0xbd, 0x03, 0, 0, 0x01, 0x3d));
  await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText('Please try again in a while.', OPEN);
  await page.screenshot({ path: 'test-results/field-crywolf-battle.png' });
  await closeDialog(page);
  // The fortress taken (occupation 1, war): EncTerrain351.att replaces the walkable cells.
  await feed(page, c1(0xbd, 0x00, 1, 4));
  await expect.poll(() => game(page, (g) => [...(g.world?.flags ?? [])].reduce((a, b) => (a * 31 + b) >>> 0, 7)), OPEN).not.toBe(baseFlags);
  // The end, defended (peace, End), then as the server sends them: the rank (BD 07: rank A, 123456 exp), the heroes (BD 08), the
  // banner, then the ranking box.
  await feed(page, c1(0xbd, 0x00, 0, 5));
  await feed(page, c1(0xbd, 0x07, 3, 0, 0, 0, ...u32le(123456)));
  const hero = [0, ...name('Hero1', 10), 0, ...u32le(900), 8, 0, 0, 0];
  await feed(page, [0xc1, 5 + hero.length, 0xbd, 0x08, 1, ...hero]);
  await expect(page.locator('.rml-crywolf #rank_table')).toBeVisible(OPEN);
  await expect.poll(() => game(page, (g) => g.fieldEvents.dialogs.lines[1] ?? ''), { timeout: 180_000 }).toBe('1      Hero1      Elf      900');
  expect(await game(page, (g) => g.fieldEvents.dialogs.lines.slice(-2))).toEqual(['Monster strength decreased 10%.', '5% increase in castle and arena invitation combine rate.']);
  await page.screenshot({ path: 'test-results/field-crywolf-result.png' });
  await closeDialog(page);
  await moveTo(page, 'lorencia', 0);

  // ---- the castle siege ----
  // The Lorencia gate (239, 14) to the Valley of Loren, then the guardsman (224) at (86, 61).
  // A few legs: long paths take a while at test frame rates.
  await walkLegs(page, [{ x: 134, y: 94 }, { x: 165, y: 72 }, { x: 200, y: 38 }, { x: 239, y: 15 }], 0);
  await expect.poll(() => game(page, (g) => g.mapNumber), OPEN).toBe(30);
  await expect.poll(() => game(page, (g) => g.world?.mapNumber ?? null), OPEN).toBe(30);
  await expect(page.locator('.rml-siege_warfare #frame_art')).toBeVisible(OPEN);
  await page.waitForTimeout(3_000);
  await walkLegs(page, [{ x: 65, y: 39 }, { x: 88, y: 58 }], 30);
  const guardsman = () => game(page, (g) => [...g.online.characters.values()].find((c) => c.monsterNumber === 224)?.id ?? null);
  await expect.poll(guardsman, { timeout: 300_000, intervals: [5_000] }).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await guardsman())!);
  await expect(page.locator('.rml-guard_window #panel')).toBeVisible(OPEN);
  await expect.poll(() => shadowText(page, 'guard_window', '#heading'), OPEN).toEqual(['Guard NPC']);
  expect((await shadowText(page, 'guard_window', '.gd-tab-label')).length).toBe(3);
  expect((await shadowText(page, 'guard_window', '#status_start'))[0]).toMatch(/^Starting \d+-\d+-\d+ \d+ : \d+$/);
  await page.waitForTimeout(1_500);
  await page.screenshot({ path: 'test-results/field-siege-guardsman.png' });
  await page.locator('.rml-guard_window .gd-tab >> nth=2').click({ force: true });
  await page.waitForTimeout(1_000);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-guard_window')).toHaveCount(0);

  // The senior (NPC window 0C) with a recorded gate list (C2 B3: gate 1 alive, 1.9 M of 2.5 M).
  await feed(page, [0xc3, 4, 0x30, 0x0c]);
  await expect(page.locator('.rml-castle_window #panel')).toBeVisible(OPEN);
  const npc = [...u32le(277), ...u32le(1), ...u32le(1), ...u32le(0), ...u32le(2_500_000), ...u32le(1_900_000), 93, 114, 1, 0];
  await feed(page, [0xc2, 0, 12 + npc.length, 0xb3, 1, 0, 0, 0, ...u32le(1), ...npc]);
  await expect.poll(() => shadowText(page, 'castle_window', '#stat_hp'), OPEN).toEqual(['DUR : 1,900,000/2,500,000']);
  expect(await shadowText(page, 'castle_window', '#stat_defense')).toEqual(['DP : 180']);
  await page.screenshot({ path: 'test-results/field-siege-senior.png' });
  await page.locator('.rml-castle_window #btn_repair').click({ force: true });
  await expect(page.locator('.rml-generic_confirm_dialog .gcd-line')).toHaveText(['To repair selected castle gate', '4,000,000 zen is required.', 'Would you like to purchase?'], OPEN);
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel').click();
  await page.locator('.rml-castle_window .cw-tab >> nth=2').click({ force: true });
  await expect.poll(() => shadowText(page, 'castle_window', '#tax_title'), OPEN).toEqual(['Adjust tax rate']);
  await page.keyboard.press('Escape');

  // The gatekeeper (B9 03: a guest, open, 10,000 zen), the gate switch (B2 11), the catapult (B7 00).
  await feed(page, [0xc1, 20, 0xb9, 0x03, 1, 1, 0, 0, ...u32le(10_000), ...u32le(300_000), ...u32le(10_000)]);
  await expect.poll(() => shadowText(page, 'gateman', '#guest_fee'), OPEN).toEqual(['Entrance fee 10,000zen']);
  await page.screenshot({ path: 'test-results/field-siege-gatekeeper.png' });
  await page.keyboard.press('Escape');
  await feed(page, c1(0xb2, 0x11, 1, 0x01, 0x20));
  await expect.poll(() => shadowText(page, 'gate_switch', '#title'), OPEN).toEqual(['Castle Gate Switch']);
  expect(await shadowText(page, 'gate_switch', '.gs-label')).toEqual(['Open']);
  await page.keyboard.press('Escape');
  await feed(page, c1(0xb7, 0x00, 1, 1, 0x01, 0x21));
  await expect.poll(() => shadowText(page, 'catapult', '.cp-title'), OPEN).toEqual(['Weapon for Invading team']);
  expect(await shadowText(page, 'catapult', '.cp-area-label')).toEqual(['Castle Gate 1', 'Castle Gate 2', 'Castle Gate 3', 'Front yard']);
  await page.screenshot({ path: 'test-results/field-siege-catapult.png' });
  await page.keyboard.press('Escape');

  // The battle (B2 17), its time (B2 1E: 1 h 5 min) on the HUD; the crown's shield removed (B2 16): a progress box.
  await feed(page, c1(0xb2, 0x17, 1));
  await feed(page, c1(0xb2, 0x1e, 1, 5));
  await expect.poll(() => shadowText(page, 'siege_warfare', '#time'), OPEN).toEqual(['1:05']);
  await feed(page, c1(0xb2, 0x16, 0));
  await expect.poll(() => game(page, (g) => g.fieldEvents.progress.lines), OPEN).toEqual(['Shield of the crown has been removed']);
  await page.screenshot({ path: 'test-results/field-siege-battle.png' });
  await feed(page, c1(0xb2, 0x17, 0));
  await expect.poll(() => game(page, (g) => g.fieldEvents.progress.isOpen), OPEN).toBe(false);

  await moveTo(page, 'lorencia', 0);
  await expect(page.locator('.rml-siege_warfare')).toHaveCount(0);
  await expectNoPageErrors(errors);
});
