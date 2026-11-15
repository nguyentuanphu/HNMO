// HNMO's guild levels in the web game and on the website. The guild levels panel opens with the guild window (G) and
// shows the state the server sends (HnmoGuildsState, C2 F5 12): the guild's level, the experience bar, this week's
// ladder points and rank, the player's part, the buff (also on the buff strip, in English or Vietnamese), the top
// guilds and the waiting rewards; level-ups and ladder rewards are told in the chat; the panel's button sends
// HnmoGuildsAction. The state is fed through the game's connection (the test server may run without the guild levels),
// so what's checked is the panel, the buff strip, the chat and the packets; the server's rules are covered by its unit
// tests (GuildEngineTest). The website's Guilds section reads /website/guilds (answered here by the test).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    guildLevels?: { isOpen: boolean; activeBuffs(): number[]; state: { level: number } | null };
    chat?: { entries: { text: string }[] };
  };
  __sent: Sent[];
};

const STATE = (event?: Record<string, unknown>, owed = false) => ({
  v: 1, now: Date.now(), inGuild: true, guildName: 'Dragons', level: 3, maxLevel: 10,
  xp: 6500, levelXp: 6000, nextXp: 12000, weekXp: 1800, rank: 2, myWeekXp: 420, today: 120, dailyCap: 400,
  weekEnds: Date.now() + 3 * 86_400_000, buffNameEn: 'Guild blessing', buffNameVi: 'Phúc lành bang hội',
  buff: { level: 3, exp: 2, zen: 2, defense: 0 }, nextBuff: { level: 4, exp: 2, zen: 3, defense: 5 }, ladderOn: true,
  ladder: [{ rank: 1, name: 'Wolves', xp: 2500, level: 5 }, { rank: 2, name: 'Dragons', xp: 1800, level: 3 }],
  rewards: [{ from: 1, to: 1, coins: 150, masterBonus: 100, coinType: 'GoblinPoints', zen: 0, items: [] }, { from: 2, to: 2, coins: 100, masterBonus: 50, coinType: 'GoblinPoints', zen: 0, items: [] }],
  owed: owed ? [{ week: '2026-11-09', rank: 1, reward: { zen: 50000, items: [{ group: 14, number: 13, level: 0, count: 1 }] } }] : [],
  event,
});

/** The HnmoGuildsState packet (C2 F5 12) of a state; the server escapes non-ASCII text, so the JSON is plain ASCII. */
function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-\uffff]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x12, ...json, 0];
}

async function feed(page: Page, value: unknown): Promise<void> {
  await page.evaluate((bytes) => (window as unknown as Win).__game.connection.dispatch(new Uint8Array(bytes)), packet(value));
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoGuilds')));
const chat = (page: Page) => page.evaluate(() => ((window as unknown as Win).__game.chat?.entries ?? []).map((e) => e.text));

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('in the game: the guild levels panel with G, the buff strip, the chat notices and the button', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(() => {
    // Collect what the game sends from the start (it asks for the guild levels when it enters the world).
    const w = window as unknown as Win;
    w.__sent = [];
    const timer = setInterval(() => {
      const connection = w.__game?.connection;
      if (!connection || (connection as unknown as { __wrapped?: boolean }).__wrapped) return;
      const send = connection.send.bind(connection);
      connection.send = (group, name, fields) => { w.__sent.push({ name, fields: { ...(fields ?? {}) } }); send(group, name, fields); };
      (connection as unknown as { __wrapped?: boolean }).__wrapped = true;
      clearInterval(timer);
    }, 5);
  });
  await enterWorld(page, 'test6');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoGuildsRequest')), { timeout: 60_000 }).toBe(true);
  await page.waitForFunction(() => !!(window as unknown as Win).__game.chat, null, { timeout: 120_000 });

  // A level-up: told in the chat (the server's guild notice didn't), the buff is on the strip.
  await feed(page, STATE({ type: 'levelup', level: 3, inGame: false }));
  await expect.poll(() => chat(page)).toContain('Your guild Dragons reached level 3!');
  await expect.poll(() => chat(page)).toContain('Guild buff: +2% EXP · +2% zen from monsters');
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.guildLevels?.activeBuffs())).toEqual([190]);
  const strip = page.locator('.rml-buff_strip');
  await expect.poll(() => strip.evaluate((host) => host.shadowRoot?.querySelectorAll('.buff-slot').length ?? 0)).toBeGreaterThan(0);

  // G opens the guild window and the panel next to it.
  await page.mouse.move(640, 300);
  await page.keyboard.press('g');
  const panel = page.locator('.rml-hnmo_guild_levels');
  await expect(page.locator('.rml-guild_info')).toHaveCount(1, { timeout: 90_000 });
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await expect(panel.locator('.gl-level-text')).toHaveText('Level 3');
  await expect(panel.locator('.gl-xp')).toHaveText('Guild XP: 6,500 / 12,000 (level 4)');
  await expect(panel.locator('.gl-rank')).toContainText('This week: 1,800 points, rank 2.');
  await expect(panel.locator('.gl-mine')).toHaveText('Your part this week: 420 · today 120 / 400');
  await expect(panel.locator('.gl-buff')).toHaveText('Guild blessing: +2% EXP · +2% zen from monsters');
  await expect(panel.locator('.gl-next')).toHaveText('Level 4: +2% EXP · +3% zen from monsters · +5 defense');
  await expect(panel).toContainText('1. Wolves · level 5 · 2,500');
  await expect(panel).toContainText('#1 150 Goblin Points');
  await expect(panel.locator('.gl-fill')).toHaveAttribute('style', /width: 8%/);
  await page.screenshot({ path: 'test-results/guilds-panel.png' });

  // A ladder reward with a full bag: the chat tells it; Take sends the action.
  await feed(page, STATE({ type: 'ladder', result: 'Ok', rank: 1, coins: 250, week: '2026-11-09' }, true));
  await expect.poll(() => chat(page)).toContain('Guild ladder: your guild was rank 1 last week. Reward: 250 Goblin Points.');
  await feed(page, STATE({ type: 'goods', result: 'BagFull' }, true));
  await expect.poll(() => chat(page)).toContain('Your bag is full: the guild ladder rewards wait. Make room, then take them in the guild window (G).');
  await expect(panel).toContainText('Rank 1 · Reward: 50,000 zen');
  await panel.locator('.gl-claim').click();
  await expect.poll(() => sent(page).then((list) => list.find((s) => s.name === 'HnmoGuildsAction')?.fields)).toEqual({ Action: 1, Argument: '' });

  // Vietnamese.
  await page.locator('.language-picker button[data-lang="vi"]:visible').first().click();
  await expect(panel.locator('.gl-level-text')).toHaveText('Cấp 3');
  await expect(panel.locator('.gl-buff')).toHaveText('Phúc lành bang hội: +2% KN · +2% zen từ quái vật');
  await page.locator('.language-picker button[data-lang="en"]:visible').first().click();
  await expect(panel.locator('.gl-level-text')).toHaveText('Level 3');

  // G closes both; without a guild the buff goes.
  await page.mouse.move(640, 300);
  await page.keyboard.press('g');
  await expect(panel).toHaveCount(0);
  await feed(page, { ...STATE(), inGuild: false, guildName: undefined, buff: undefined, level: 1, xp: 0 });
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.guildLevels?.activeBuffs())).toEqual([]);
  await page.goto('about:blank');
  await expectNoPageErrors(errors);
});

const PUBLIC = () => ({
  v: 1, enabled: true, now: Date.now(), weekEnds: Date.now() + 2 * 86_400_000, dailyCap: 400, buffsEnabled: true,
  buffNameEn: 'Guild blessing', buffNameVi: 'Phúc lành bang hội',
  levels: [{ level: 1, xp: 0, exp: 0, zen: 0, defense: 0 }, { level: 2, xp: 2000, exp: 1, zen: 0, defense: 0 }, { level: 5, xp: 25000, exp: 3, zen: 5, defense: 10 }],
  sources: [{ source: 'MonsterKill', xp: 1 }, { source: 'OnlineTime', xp: 5 }, { source: 'CastleSiege', xp: 3000 }],
  onlineMinutes: 10, siegeLadderBonus: 5000, ladderOn: true, ladderMinimumXp: 100,
  rewards: [{ from: 1, to: 1, coins: 150, masterBonus: 100, coinType: 'GoblinPoints', zen: 0, items: [] }, { from: 4, to: 5, coins: 30, masterBonus: 0, coinType: 'GoblinPoints', zen: 0, items: [] }],
  ladder: [{ rank: 1, name: 'Wolves', xp: 2500, level: 5, members: 12 }, { rank: 2, name: 'Dragons', xp: 1800, level: 3, members: 7 }],
  lastWeek: { week: '2026-11-09', lines: [{ rank: 1, name: 'Dragons', xp: 9100, coins: 150, coinType: 'GoblinPoints', paid: 7 }] },
  guilds: [{ name: 'Wolves', level: 5, xp: 31000, members: 12 }, { name: 'Dragons', level: 3, xp: 6500, members: 7 }],
  sieges: [{ at: Date.UTC(2026, 10, 14, 11, 0), name: 'Dragons' }],
});

test('the website shows the guild ladder, last week\'s winners, the guilds and the castle sieges, in both languages', async ({ page }) => {
  let available = true;
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  for (const path of ['data', 'details', 'missions', 'world-bosses', 'onboarding']) await page.route(`**/website/${path}`, (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/guilds', (route) => (available ? route.fulfill({ json: PUBLIC() }) : route.fulfill({ status: 503 })));
  await page.goto('/home.html');
  await page.locator('#en').click();
  const section = page.locator('#guilds');
  await expect(page.locator('nav a[href="#guilds"]')).toHaveText('Guilds');
  await expect(section.locator('h2')).toHaveText('Guild levels and the weekly ladder');
  await expect(section.locator('#guilds-ladder')).toContainText('Wolves');
  await expect(section.locator('#guilds-ladder tr').nth(2)).toContainText('Dragons');
  await expect(section.locator('#guilds-ladder tr').nth(1)).toContainText('12');
  await expect(section.locator('#guilds-rewards')).toContainText('Monday rewards for every member: #1 150 Goblin Points (the guild master gets 100 more) · #4–5 30 Goblin Points');
  await expect(section.locator('#guilds-last-week-of')).toHaveText('Week of 2026-11-09');
  await expect(section.locator('#guilds-last-week')).toContainText('9,100');
  await expect(section.locator('#guilds-all')).toContainText('31,000');
  await expect(section.locator('#guilds-sieges')).toContainText('Dragons');
  await expect(section.locator('#guilds-levels')).toContainText('+3% EXP · +5% zen from monsters · +10 defense');
  await expect(section.locator('#guilds-sources')).toContainText('per 10 minutes in the game +5');
  await expect(section.locator('#guilds-sources')).toContainText('up to 400 a day per member');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/guilds-website.png', fullPage: false });

  await page.locator('#vi').click();
  await expect(page.locator('nav a[href="#guilds"]')).toHaveText('Bang hội');
  await expect(section.locator('h2')).toHaveText('Cấp bang hội và bảng xếp hạng tuần');
  await expect(section.locator('#guilds-levels')).toContainText('+3% KN · +5% zen từ quái vật · +10 phòng thủ');
  await expect(section.locator('#guilds-rewards')).toContainText('Thưởng thứ Hai cho mỗi thành viên');
  await expect(section.locator('#guilds-last-week-of')).toHaveText('Tuần từ 2026-11-09');

  // The server doesn't answer: a notice instead of old data.
  available = false;
  await page.locator('#en').click();
  await page.locator('#refresh').click();
  await expect(section.locator('#guilds-notice')).toHaveText('The guilds are unavailable: the server didn\'t answer.');
  await expect(section.locator('#guilds-ladder')).not.toContainText('Wolves');
});
