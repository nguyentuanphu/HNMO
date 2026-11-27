// HNMO's top races in the web game and on the website. The top races window (F9, and the touch "more" menu) shows the
// state the server sends (HnmoTopRacesState, C2 F5 18): the running races with what they count, who takes part, the
// time left, the player's rank and value, the top 10 of the player's table and the prizes; the races to come, the past
// winners and the waiting prizes with Take (HnmoTopRacesAction). Starts, ends and prizes are told in the chat. The
// state is fed through the game's connection (the test server may run without the top races), so what's checked is the
// window, the chat and the packets; the server's rules are covered by its unit tests (TopRaceEngineTest). The website's
// Top races section reads /website/top-races (answered here by the test).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    topRaces?: { isOpen: boolean; state: { races: unknown[] } | null; handle(bytes: Uint8Array): boolean };
    chat?: { entries: { text: string }[] };
  };
  __sent: Sent[];
};

const HOUR = 3_600_000;

const RACES = () => [
  {
    race: {
      id: 'weekly-resets', titleEn: 'Weekly reset race', titleVi: 'Đua top reset tuần', kind: 'ResetsGained', event: 'Any', scope: 'AllClasses', cls: 0,
      period: 'Weekly', minimum: 1,
      prizes: [
        { from: 1, to: 1, coins: 300, coinType: 'GoblinPoints', zen: 0, items: [] },
        { from: 4, to: 10, coins: 30, coinType: 'GoblinPoints', zen: 0, items: [] },
      ],
    },
    start: Date.now() - 50 * HOUR, end: Date.now() + 26 * HOUR + 5 * 60_000, eligible: true, table: -1, myRank: 2, myValue: 7,
    top: [{ rank: 1, name: 'Ann', value: 9, cls: 1 }, { rank: 2, name: 'Me', value: 7, cls: 0 }],
  },
  {
    race: {
      id: 'monthly-levels', titleEn: 'Monthly level race (per class)', titleVi: 'Đua top cấp tháng (theo lớp nhân vật)', kind: 'LevelsGained', event: 'Any',
      scope: 'PerClass', cls: 0, period: 'Monthly', minimum: 50,
      prizes: [{ from: 1, to: 1, coins: 250, coinType: 'GoblinPoints', zen: 0, items: [{ group: 14, number: 13, level: 0, count: 1, name: 'Jewel of Bless' }] }],
    },
    start: Date.now() - 300 * HOUR, end: Date.now() + 400 * HOUR, eligible: true, table: 1, myRank: 0, myValue: 0,
    top: [{ rank: 1, name: 'Knighty', value: 120, cls: 1 }],
  },
  {
    race: {
      id: 'summoners', titleEn: 'Summoner sprint', titleVi: 'Thuật sĩ tăng tốc', kind: 'LevelReached', event: 'Any', scope: 'OneClass', cls: 5,
      period: 'Daily', minimum: 1, prizes: [],
    },
    start: Date.now() - HOUR, end: Date.now() + 5 * HOUR, eligible: false, table: 5, myRank: 0, myValue: 0, top: [],
  },
];

const STATE = (event?: Record<string, unknown>, owed = false) => ({
  v: 1, now: Date.now(), myClass: 1,
  races: RACES(),
  upcoming: [{ race: { ...RACES()[0].race, id: 'xmas', titleEn: 'Christmas race', titleVi: 'Đua top Giáng sinh', period: 'Custom' }, start: Date.now() + 72 * HOUR, end: Date.now() + 240 * HOUR }],
  history: [{
    raceId: 'weekly-boss-hunt', titleEn: 'Weekly boss hunt', titleVi: 'Đua top săn boss tuần', kind: 'BossHuntPoints', period: 'Weekly', periodKey: 'w2026-11-02',
    start: Date.now() - 300 * HOUR, end: Date.now() - 130 * HOUR, paid: true,
    tables: [{ cls: -1, lines: [{ rank: 1, name: 'Hunter', value: 85, cls: 2, prized: true, coins: 200, coinType: 'GoblinPoints', zen: 0, items: [] }] }],
  }],
  owed: owed ? [{ raceId: 'weekly-resets', titleEn: 'Weekly reset race', titleVi: 'Đua top reset tuần', period: 'w2026-11-02', rank: 1, reward: { zen: 50000, items: [{ group: 14, number: 13, level: 0, count: 1 }] } }] : [],
  event,
});

/** The HnmoTopRacesState packet (C2 F5 18) of a state; the server escapes non-ASCII text, so the JSON is plain ASCII. */
function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-￿]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x18, ...json, 0];
}

async function feed(page: Page, value: unknown): Promise<void> {
  await page.evaluate((bytes) => {
    const w = window as unknown as Win & { __feeding?: boolean };
    w.__feeding = true;
    try { w.__game.connection.dispatch(new Uint8Array(bytes)); } finally { w.__feeding = false; }
  }, packet(value));
}

/** From now on the window takes only the states this test feeds (a server with the top races also sends its own). */
async function onlyFedStates(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as Win & { __feeding?: boolean };
    const window_ = w.__game.topRaces;
    if (!window_) return;
    const handle = window_.handle.bind(window_);
    window_.handle = (bytes: Uint8Array) => {
      const state = bytes[0] === 0xc2 && bytes[3] === 0xf5 && bytes[4] === 0x18;
      if (state && !w.__feeding) return true;
      return handle(bytes);
    };
  });
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoTopRaces')));
const chat = (page: Page) => page.evaluate(() => ((window as unknown as Win).__game.chat?.entries ?? []).map((e) => e.text));

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('in the game: the top races window with F9, the chat notices and Take', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(() => {
    // Collect what the game sends from the start (it asks for the top races when it enters the world).
    const w = window as unknown as Win;
    w.__sent = [];
    const timer = setInterval(() => {
      const connection = w.__game?.connection;
      if (!connection || (connection as unknown as { __wrapped?: boolean }).__wrapped) return;
      const send = connection.send.bind(connection);
      // Later requests are only recorded: against a server with the top races, its real answers would replace the
      // states this test feeds.
      connection.send = (group, name, fields) => {
        const again = name === 'HnmoTopRacesRequest' && w.__sent.some((s) => s.name === name);
        w.__sent.push({ name, fields: { ...(fields ?? {}) } });
        if (!again) send(group, name, fields);
      };
      (connection as unknown as { __wrapped?: boolean }).__wrapped = true;
      clearInterval(timer);
    }, 5);
  });
  await enterWorld(page, 'test8');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoTopRacesRequest')), { timeout: 60_000 }).toBe(true);
  await page.waitForFunction(() => !!(window as unknown as Win).__game.chat, null, { timeout: 120_000 });
  await onlyFedStates(page);

  // A race starts (no golden notice from the server): the chat tells it.
  await feed(page, STATE({ type: 'start', raceId: 'weekly-resets', textEn: 'The top race "Weekly reset race" has started!', textVi: 'Đua top "Đua top reset tuần" đã bắt đầu!', inGame: false }));
  await expect.poll(() => chat(page)).toContain('[Top race] The top race "Weekly reset race" has started!');

  // F9 opens the window.
  await page.mouse.move(640, 300);
  await page.keyboard.press('F9');
  const panel = page.locator('.rml-hnmo_top_races');
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  const resets = panel.locator('.tr-row[data-race="weekly-resets"]');
  await expect(resets.locator('.tr-name')).toHaveText('Weekly reset race');
  await expect(resets).toContainText('Resets done · weekly · all classes');
  await expect(resets.locator('.tr-time')).toHaveText(/^Ends in 1 d 2 h/);
  await expect(resets).toContainText('Sydney time');
  await expect(resets.locator('.tr-mine')).toHaveText('Your rank: 2 · 7 resets');
  await expect(resets).toContainText('1. Ann · 9 resets');
  await expect(resets).toContainText('Rank 1: 300 Goblin Points');
  await expect(resets).toContainText('Ranks 4-10: 30 Goblin Points');
  const levels = panel.locator('.tr-row[data-race="monthly-levels"]');
  await expect(levels).toContainText('Levels gained · monthly · a table per class (Dark Knight)');
  await expect(levels.locator('.tr-mine')).toHaveText('You are not ranked yet.');
  await expect(levels).toContainText('Rank 1: 250 Goblin Points · Jewel of Bless x1');
  await expect(levels).toContainText('At least 50 levels for a prize.');
  await expect(panel.locator('.tr-row[data-race="summoners"] .tr-mine')).toHaveText('Only for Summoner.');
  await expect(panel.locator('.tr-upcoming')).toContainText('Christmas race');
  await expect(panel.locator('.tr-history')).toContainText('Weekly boss hunt');
  await expect(panel.locator('.tr-history')).toContainText('Hunter (85 points)');
  await expect(panel.locator('.tr-claim')).toBeHidden();
  await page.screenshot({ path: 'test-results/top-races-window.png' });

  // A prize, then a full bag: the chat tells it; Take sends the action.
  await feed(page, STATE({ type: 'prize', result: 'Ok', raceId: 'weekly-resets', textEn: 'Weekly reset race', textVi: 'Đua top reset tuần', rank: 1, coins: 300 }, true));
  await expect.poll(() => chat(page)).toContain('Top race "Weekly reset race": you are rank 1 and got 300 coins.');
  await feed(page, STATE({ type: 'goods', result: 'BagFull' }, true));
  await expect.poll(() => chat(page)).toContain('Your bag is full: the top race prizes wait. Make room, then take them in the top races window (F9).');
  await expect(panel.locator('.tr-owed')).toContainText('Weekly reset race · Rank 1 · Reward: 50,000 zen');
  await panel.locator('.tr-claim').click();
  await expect.poll(() => sent(page).then((list) => list.find((s) => s.name === 'HnmoTopRacesAction')?.fields)).toEqual({ Action: 1, Argument: '' });

  // Vietnamese.
  if (await page.locator('#hudToolsToggle').isVisible()) await page.locator('#hudToolsToggle').click();
  await page.locator('.language-picker button[data-lang="vi"]:visible').first().click();
  await expect(resets.locator('.tr-name')).toHaveText('Đua top reset tuần');
  await expect(resets.locator('.tr-mine')).toHaveText('Hạng của bạn: 2 · 7 lần reset');
  await expect(resets).toContainText('Số lần reset · hằng tuần · mọi lớp nhân vật');
  await page.locator('.language-picker button[data-lang="en"]:visible').first().click();
  await expect(resets.locator('.tr-name')).toHaveText('Weekly reset race');

  // F9 closes it; the touch "more" menu's event opens it again.
  await page.mouse.move(640, 300);
  await page.keyboard.press('F9');
  await expect(panel).toHaveCount(0);
  await page.evaluate(() => window.dispatchEvent(new Event('hnmo-top-races-open')));
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await page.goto('about:blank');
  await expectNoPageErrors(errors);
});

const PUBLIC = () => ({
  v: 1, enabled: true, now: Date.now(), timeZone: 'Australia/Sydney',
  races: [
    { race: RACES()[0].race, start: Date.now() - 50 * HOUR, end: Date.now() + 26 * HOUR, tables: [{ cls: -1, lines: [{ rank: 1, name: 'Ann', value: 9, cls: 1 }, { rank: 2, name: 'Bob', value: 7, cls: 0 }] }] },
    { race: RACES()[1].race, start: Date.now() - 300 * HOUR, end: Date.now() + 400 * HOUR, tables: [{ cls: 1, lines: [{ rank: 1, name: 'Knighty', value: 120, cls: 1 }] }, { cls: 5, lines: [{ rank: 1, name: 'Summy', value: 80, cls: 5 }] }] },
  ],
  upcoming: [{ race: { ...RACES()[0].race, id: 'xmas', titleEn: 'Christmas race', titleVi: 'Đua top Giáng sinh', period: 'Custom' }, start: Date.now() + 72 * HOUR, end: Date.now() + 240 * HOUR }],
  history: STATE().history,
});

test('the website shows the running races with their tables and prizes, the coming races and the past winners, in both languages', async ({ page }) => {
  let available = true;
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  for (const path of ['data', 'details', 'missions', 'world-bosses', 'onboarding', 'guilds', 'community']) await page.route(`**/website/${path}`, (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/top-races', (route) => (available ? route.fulfill({ json: PUBLIC() }) : route.fulfill({ status: 503 })));
  await page.goto('/home.html');
  await page.locator('#en').click();
  const section = page.locator('#races');
  await expect(page.locator('nav a[href="#races"]')).toHaveText('Top races');
  await expect(section.locator('h2')).toHaveText('Top races');
  const resets = section.locator('.race-card[data-race="weekly-resets"]');
  await expect(resets.locator('h4')).toHaveText('Weekly reset race');
  await expect(resets).toContainText('Resets done · weekly · all classes');
  await expect(resets.locator('.race-time')).toContainText('Ends in 1 d 2 h');
  await expect(resets.locator('.race-time')).toContainText('Sydney time');
  await expect(resets.locator('.race-time')).toContainText('your time');
  await expect(resets.locator('tr').nth(1)).toContainText('Ann');
  await expect(resets.locator('.race-prizes')).toContainText('Rank 1: 300 Goblin Points');
  await expect(resets.locator('.race-prizes')).toContainText('Ranks 4-10: 30 Goblin Points');
  const levels = section.locator('.race-card[data-race="monthly-levels"]');
  await expect(levels.locator('h5').first()).toHaveText('Dark Knight');
  await expect(levels).toContainText('Summoner');
  await expect(levels).toContainText('Summy');
  await expect(levels.locator('.race-prizes')).toContainText('Jewel of Bless');
  await expect(levels.locator('.race-prizes')).toContainText('At least 50 for a prize.');
  await expect(section.locator('#races-upcoming')).toContainText('Christmas race');
  await expect(section.locator('#races-history')).toContainText('Weekly boss hunt: Hunter (85)');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/top-races-website.png', fullPage: false });

  await page.locator('#vi').click();
  await expect(page.locator('nav a[href="#races"]')).toHaveText('Đua top');
  await expect(resets.locator('h4')).toHaveText('Đua top reset tuần');
  await expect(resets).toContainText('Số lần reset · hằng tuần · mọi lớp nhân vật');
  await expect(resets.locator('.race-time')).toContainText('giờ Sydney');
  await expect(levels.locator('h5').first()).toHaveText('Chiến Binh');
  await expect(resets.locator('.race-prizes')).toContainText('Hạng 4-10');
  await expect(section.locator('#races-history')).toContainText('Đua top săn boss tuần');

  // The server doesn't answer: a notice instead of old data.
  available = false;
  await page.locator('#en').click();
  await page.locator('#refresh').click();
  await expect(section.locator('#races-notice')).toHaveText('The top races are unavailable: the server didn\'t answer.');
  await expect(section.locator('#races-running')).not.toContainText('Ann');
});
