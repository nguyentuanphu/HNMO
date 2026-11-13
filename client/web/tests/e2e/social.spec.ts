// Guild, friends, letters and whispers between two players, like on the desktop:
//  - A talks to Devias' guild master, draws a mark and creates a guild (guild_make.rml);
//  - B asks A to join (/guild's request), A accepts in the game; both see the guild window (G) with
//    two members, the guild's tag over each other's heads and the guild chat ("@text");
//  - A adds B as a friend in the friend window (F), B accepts; A writes B a letter, B reads it;
//  - A whispers B through the whisper box;
//  - then everything is undone: the letter deleted, the friend removed, B leaves the guild, A
//    disbands it, both go back to Lorencia.
// Creating a guild needs level 100 (the server's rule), so A is test300; B is test4.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Npc { id: number; name: string; kind: string; monsterNumber: number | null; cell: { x: number; y: number } }
interface SocialGame {
  hero: { name: string };
  mapNumber: number;
  online: { characters: Map<number, Npc>; hero: { isWalking: boolean; cell: { x: number; y: number } }; walkTo(cell: { x: number; y: number }): boolean; talkTo(npc: unknown): void; say(text: string): void };
  social: {
    guild: { status: number; heroMark: { name: string } | null; members: { name: string }[]; requestJoin(master: unknown): void; kick(name: string, password: string): void; guildOf(id: number): unknown };
    messenger: { friends: { name: string; server: number }[]; letters: { id: number; sender: string; subject: string; read: boolean }[]; deleteFriend(name: string): void; deleteLetter(id: number): void };
    friendWindow: { isOpen: boolean; tab: number; openLetter(id: number): void; write(o: { to?: string }): { fill(v: { to?: string; subject?: string; body?: string }): void } };
    whisperTo(name: string): void;
  };
}
type Win = Window & { __game: SocialGame };

const game = <T>(page: Page, read: (g: SocialGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);
const NONE = 0xff, MASTER = 128;
const GUILD_MASTER_NPC = 241; // Devias (215, 45)

// Clicks an element of a game window (they live in shadow roots; Playwright's CSS reaches them).
async function press(page: Page, selector: string): Promise<void> {
  const box = await page.locator(selector).first().boundingBox();
  if (!box) throw new Error(`${selector} is not on the screen`);
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 3 });
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
}

const hasLetter = (page: Page, subject: string) => page.evaluate((s) => (window as unknown as Win).__game.social.messenger.letters.some((l) => l.subject === s), subject);
const chatLines = (page: Page) => page.evaluate(() => [...document.querySelectorAll('#chatLog .line')].map((line) => `${line.className}|${line.textContent ?? ''}`));

// Leaves a guild left over from an earlier run (the master disbands it), with the account's password.
async function leaveGuild(page: Page, password: string): Promise<void> {
  const status = await game(page, (g) => g.social.guild.status);
  if (status === NONE) return;
  await page.evaluate((pw) => { const g = (window as unknown as Win).__game; g.social.guild.kick(g.hero.name, pw); }, password);
  await expect.poll(() => game(page, (g) => g.social.guild.status), { timeout: 60_000 }).toBe(NONE);
}

test('guild, friends, a letter and a whisper between two players', async ({ browser }) => {
  test.setTimeout(14 * 60_000);
  const contextA = await browser.newContext(), contextB = await browser.newContext();
  const a = await contextA.newPage(), b = await contextB.newPage();
  const errors: string[] = [];
  for (const page of [a, b]) page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(a, 'test300');
  await enterWorld(b, 'test4');
  const nameA = await game(a, (g) => g.hero.name), nameB = await game(b, (g) => g.hero.name);

  // A clean start: no guild, not friends, no letter from A in B's box.
  await a.waitForTimeout(5_000); // the guild and the messenger arrive on entering
  await leaveGuild(b, 'test4');
  await leaveGuild(a, 'test300');
  if (await game(a, (g) => g.social.messenger.friends.length) > 0) {
    await a.evaluate((name) => (window as unknown as Win).__game.social.messenger.deleteFriend(name), nameB);
  }
  await b.evaluate((sender) => { const m = (window as unknown as Win).__game.social.messenger; for (const l of m.letters) if (l.sender === sender) m.deleteLetter(l.id); }, nameA);

  // Both go to Devias; A walks to the guild master and talks to it: its window opens.
  for (const page of [a, b]) await page.evaluate(() => (window as unknown as Win).__game.online.say('/move devias'));
  await expect.poll(() => game(a, (g) => g.mapNumber), { timeout: 90_000 }).toBe(2);
  await expect.poll(() => game(b, (g) => g.mapNumber), { timeout: 90_000 }).toBe(2);
  await a.waitForTimeout(5_000);
  await a.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 213, y: 47 }));
  await b.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 211, y: 47 }));
  const npc = () => a.evaluate((number) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.monsterNumber === number)?.id ?? null, GUILD_MASTER_NPC);
  await expect.poll(npc, { timeout: 120_000 }).not.toBeNull();
  // Talks once the hero stands next to it (a talk from too far is refused); asks again if the slow
  // machine lost the first one.
  for (let attempt = 0; attempt < 3 && !(await a.locator('.rml-guild_make #panel').isVisible()); attempt++) {
    await a.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await npc())!);
    await a.locator('.rml-guild_make #panel').waitFor({ timeout: 45_000 }).catch(() => undefined);
  }
  await expect(a.locator('.rml-guild_make #panel')).toBeVisible({ timeout: 60_000 });
  await a.screenshot({ path: 'test-results/social-guild-master.png' });

  // Create guild; the name; the mark: a few cells in the selected colour; Next shows the mark; Next creates it.
  const guildName = `Hn${String(Date.now() % 100000).padStart(5, '0')}`;
  await press(a, '.rml-guild_make #btn_make');
  await expect(a.locator('.rml-guild_make #name_field')).toBeVisible({ timeout: 30_000 });
  await a.locator('.rml-guild_make #name_field').fill(guildName);
  await press(a, '.rml-guild_make .gm-palette .gm-cell >> nth=4');
  for (const cell of [18, 19, 20, 21, 26, 29, 34, 37, 42, 43, 44, 45]) await press(a, `.rml-guild_make .gm-grid .gm-cell >> nth=${cell}`);
  await a.screenshot({ path: 'test-results/social-guild-mark.png' });
  await press(a, '.rml-guild_make #btn_next');
  await expect(a.locator('.rml-guild_make #result')).toContainText(guildName, { timeout: 30_000 });
  await press(a, '.rml-guild_make #btn_next');
  await expect.poll(() => game(a, (g) => g.social.guild.status), { timeout: 60_000 }).toBe(MASTER);
  await expect.poll(() => game(a, (g) => g.social.guild.heroMark?.name ?? ''), { timeout: 60_000 }).toBe(guildName);

  // B asks A to join (/guild facing A); A is asked in the game and accepts.
  const idOf = (page: Page, name: string) => page.evaluate((n) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.kind === 'player' && c.name === n)?.id ?? null, name);
  await expect.poll(() => idOf(b, nameA), { timeout: 90_000 }).not.toBeNull();
  await b.evaluate((name) => { const g = (window as unknown as Win).__game; g.social.guild.requestJoin([...g.online.characters.values()].find((c) => c.name === name)); }, nameA);
  await expect(a.locator('.confirm-dialog')).toContainText('You have received an offer to join a guild.', { timeout: 60_000 });
  await a.click('.confirm-dialog .confirm-ok');
  await expect.poll(() => game(b, (g) => g.social.guild.status), { timeout: 60_000 }).toBe(0);

  // G on both: the guild window with its two members.
  for (const page of [a, b]) {
    await page.keyboard.press('g');
    await expect(page.locator('.rml-guild_info #panel')).toBeVisible({ timeout: 90_000 });
    await expect(page.locator('.rml-guild_info .gi-member-row')).toHaveCount(2, { timeout: 60_000 });
    await expect(page.locator('.rml-guild_info #gi_name')).toContainText(guildName);
  }
  await expect(a.locator('.rml-guild_info .gi-member-row', { hasText: nameB })).toHaveCount(1);
  await a.waitForTimeout(1_000);
  await a.screenshot({ path: 'test-results/social-guild-window.png' });
  // The Guild tab: the mark and the master's Disband button.
  await press(a, '.rml-guild_info #gi_tab_hit_0');
  await expect(a.locator('.rml-guild_info #btn_guild_out')).toContainText('Disband', { timeout: 30_000 });
  await a.screenshot({ path: 'test-results/social-guild-info.png' });
  for (const page of [a, b]) {
    await page.keyboard.press('g');
    await expect(page.locator('.rml-guild_info')).toHaveCount(0, { timeout: 30_000 });
  }
  // The guild's tag over B's head on A's screen, and the guild chat.
  await expect.poll(() => a.evaluate((name) => [...document.querySelectorAll('#labels .label')].find((l) => l.textContent?.endsWith(name))?.querySelector('.guild-tag')?.textContent ?? '', nameB), { timeout: 60_000 })
    .toContain(`[${guildName}]`);
  await a.evaluate(() => (window as unknown as Win).__game.online.say('@hello guild'));
  await expect.poll(async () => (await chatLines(b)).some((line) => line.startsWith('line guild|') && line.includes('hello guild')), { timeout: 60_000 }).toBe(true);

  // F: A adds B as a friend; B is asked and accepts; each has the other in the list.
  await a.keyboard.press('f');
  await expect(a.locator('.rml-friend_shell #panel')).toBeVisible({ timeout: 90_000 });
  await press(a, '.rml-friend_shell .page .actions .btn >> nth=0'); // Add Friend
  await a.locator('.social-dialog-input').fill(nameB);
  await a.click('.social-dialog .confirm-ok');
  await expect(b.locator('.confirm-dialog')).toContainText('has requested to list you as a friend.', { timeout: 60_000 });
  await b.click('.confirm-dialog .confirm-ok');
  await expect.poll(() => game(a, (g) => g.social.messenger.friends.map((f) => f.name)), { timeout: 60_000 }).toContain(nameB);
  await expect.poll(() => game(b, (g) => g.social.messenger.friends.map((f) => f.name)), { timeout: 60_000 }).toContain(nameA);
  await expect(a.locator('.rml-friend_shell #friends_pane .row', { hasText: nameB })).toHaveCount(1, { timeout: 30_000 });
  // Both online: A sees B's server.
  await expect(a.locator('.rml-friend_shell #friends_pane .row', { hasText: nameB })).toContainText('Server', { timeout: 60_000 });
  await a.screenshot({ path: 'test-results/social-friends.png' });

  // A writes B a letter with Write (B selected), the fields typed, Send.
  await press(a, `.rml-friend_shell #friends_pane .row:has-text("${nameB}")`);
  await press(a, '.rml-friend_shell .page .actions .btn >> nth=3'); // Write
  await expect(a.locator('.rml-letter_write #panel')).toBeVisible({ timeout: 90_000 });
  await expect(a.locator('.rml-letter_write #mailto_field')).toHaveValue(nameB);
  const subject = `Hello ${guildName}`;
  await a.locator('.rml-letter_write #subject_field').fill(subject);
  await a.locator('.rml-letter_write #body_field').fill('A letter from the guild master.\nSee you in Devias.');
  await a.screenshot({ path: 'test-results/social-letter-write.png' });
  await press(a, '.rml-letter_write #footer .btn >> nth=0'); // Send
  await expect(a.locator('.rml-letter_write')).toHaveCount(0, { timeout: 60_000 });
  await expect.poll(async () => (await chatLines(a)).some((line) => line.includes('Letter has been sent')), { timeout: 30_000 }).toBe(true);

  // B gets it (new mail), opens the letter box with F and reads it with a double click.
  await expect.poll(() => hasLetter(b, subject), { timeout: 60_000 }).toBe(true);
  await b.keyboard.press('f'); // closes the window the friend request opened
  await expect.poll(() => game(b, (g) => g.social.friendWindow.isOpen), { timeout: 30_000 }).toBe(false);
  await b.keyboard.press('f');
  await expect(b.locator('.rml-friend_shell #mail_pane .row', { hasText: subject })).toBeVisible({ timeout: 90_000 });
  await b.locator('.rml-friend_shell #mail_pane .row', { hasText: subject }).first().dblclick();
  await expect(b.locator('.rml-letter_read #panel')).toBeVisible({ timeout: 90_000 });
  await expect(b.locator('.rml-letter_read #header')).toContainText(`Sender: ${nameA}`);
  await expect(b.locator('.rml-letter_read #body_pane')).toContainText('See you in Devias.');
  await b.waitForTimeout(1_000);
  await b.screenshot({ path: 'test-results/social-letter-read.png' });

  // A whispers B through the whisper box.
  await a.evaluate((name) => (window as unknown as Win).__game.social.whisperTo(name), nameB);
  await a.keyboard.type('psst, guild meeting');
  await a.keyboard.press('Enter');
  await expect.poll(async () => (await chatLines(b)).some((line) => line.startsWith('line whisper|') && line.includes(`${nameA}: psst, guild meeting`)), { timeout: 60_000 }).toBe(true);
  await a.evaluate(() => { const box = document.querySelector<HTMLInputElement>('#whisperInput'); if (box) box.value = ''; });

  // Clean up: B deletes the letter from its window (Delete, Yes); A removes the friend; B leaves the
  // guild and A disbands it (both with the password); both go back to Lorencia.
  await press(b, '.rml-letter_read #footer .btn >> nth=1');
  await b.click('.social-dialog .confirm-ok');
  await expect.poll(() => hasLetter(b, subject), { timeout: 60_000 }).toBe(false);
  await a.evaluate((name) => (window as unknown as Win).__game.social.messenger.deleteFriend(name), nameB);
  await expect.poll(() => game(a, (g) => g.social.messenger.friends.length), { timeout: 60_000 }).toBe(0);
  await leaveGuild(b, 'test4');
  await leaveGuild(a, 'test300');
  for (const page of [a, b]) await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await expect.poll(() => game(a, (g) => g.mapNumber), { timeout: 90_000 }).toBe(0);
  await expect.poll(() => game(b, (g) => g.mapNumber), { timeout: 90_000 }).toBe(0);
  await expectNoPageErrors(errors);
  await contextA.close();
  await contextB.close();
});
