// HNMO's world bosses in the web game and on the website. The window (N) and the line on the screen show the state the
// server sends (HnmoWorldBossesState, C2 F5 0C); announcements and rewards are told in the chat; the window's button
// sends HnmoWorldBossesAction. The state is fed to the client (the test server may run without world bosses), so
// what's checked is the window, the line and the packets; the server's rules are covered by its unit tests
// (WorldBossEngineTest). The website's section reads /website/world-bosses (answered here by the test).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    worldBosses?: { isOpen: boolean; hudLine: HTMLDivElement };
  };
  __sent: Sent[];
  __chat: string[];
  __window: { handle(bytes: Uint8Array): boolean; open(): void; toggle(): void; start(): void; isOpen: boolean; hudLine: HTMLDivElement };
};

const reward = (zen: number, coins: number, items: { group: number; number: number; level: number; count: number }[] = []) => ({ zen, coins, coinType: 'GoblinPoints', items });
const board = (event?: Record<string, unknown>, alive = true) => ({
  v: 1, now: Date.now(),
  bosses: [
    {
      id: 'red-dragon', nameEn: 'Red Dragon', nameVi: 'Rồng Đỏ', map: 0, mapName: 'Lorencia', nextSpawn: Date.now() + 20 * 3600_000,
      alive: alive ? {
        x: 130, y: 140, spawnedAt: Date.now() - 60_000, despawnAt: Date.now() + 29 * 60_000, maximumHealth: 15000, totalDamage: 9000,
        fighters: 4, myDamage: 2500, myRank: 2,
        top: [{ rank: 1, name: 'Hero', damage: 4000, share: 44.4 }, { rank: 2, name: 'test7Dk', damage: 2500, share: 27.8 }],
      } : undefined,
    },
    { id: 'ice-queen', nameEn: 'Ice Queen', nameVi: 'Nữ Hoàng Băng Giá', map: 2, mapName: 'Devias', nextSpawn: Date.now() + 90 * 60_000 },
  ],
  owed: [{ bossEn: 'Red Dragon', bossVi: 'Rồng Đỏ', tier: 'top2', reward: { ...reward(3000000, 40, [{ group: 14, number: 13, level: 0, count: 1 }]), coinsGiven: true } }],
  huntPoints: 34, huntRank: 3, weekEnds: Date.now() + 2 * 86400_000,
  event,
});

async function feed(page: Page, state: unknown): Promise<void> {
  await page.evaluate(async (json) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    const bytes = write('ServerToClient', 'HnmoWorldBossesState', { Json: json });
    if (w.__window) w.__window.handle(bytes); else w.__game.connection.dispatch(bytes);
  }, JSON.stringify(state));
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoWorldBosses')));
const chat = (page: Page) => page.evaluate(() => (window as unknown as Win).__chat);

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('the world boss window, the line on the screen and the chat notices', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/index.html');
  await page.evaluate(async () => {
    const { WorldBossWindow } = await import('/js/ui/worldBossWindow.js' as string) as { WorldBossWindow: new (options: unknown) => Win['__window'] };
    const { itemName, loadItemCatalog } = await import('/js/game/items.js' as string) as { itemName(g: number, n: number): string; loadItemCatalog(): Promise<void> };
    await loadItemCatalog().catch(() => undefined);
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    w.__window = new WorldBossWindow({
      send: (name: string, fields: Record<string, unknown>) => w.__sent.push({ name, fields: { ...fields } }),
      message: (text: string) => w.__chat.push(text),
      itemName: (g: number, n: number) => itemName(g, n),
    });
    w.__window.start();
  });
  await expect.poll(() => sent(page).then((list) => list.map((s) => s.name))).toEqual(['HnmoWorldBossesRequest']);

  // An announcement goes to the chat; the line shows the alive boss with the time left and the rank.
  await feed(page, board({ type: 'spawn', bossId: 'red-dragon', textEn: 'Red Dragon has appeared in Lorencia (130, 140)! It leaves in 30 minutes.', textVi: 'Rồng Đỏ đã xuất hiện ở Lorencia (130, 140)!' }));
  await expect.poll(() => chat(page)).toContain('[World boss] Red Dragon has appeared in Lorencia (130, 140)! It leaves in 30 minutes.');
  const line = page.locator('.hnmo-boss-line');
  await expect(line).toBeVisible();
  await expect(line).toContainText(/Red Dragon · Lorencia · 2[89]:\d\d · your rank 2/);

  // The window: both bosses, the fight with the damage table, the next spawn in Sydney and local time, the waiting
  // rewards (coins already given) and the boss-hunt points.
  await page.evaluate(() => (window as unknown as Win).__window.open());
  const win = page.locator('.rml-hnmo_world_bosses');
  await expect(win).toHaveCount(1);
  await expect(win.locator('.wb-row')).toHaveCount(2);
  const dragon = win.locator('.wb-row[data-boss="red-dragon"]');
  await expect(dragon).toContainText('On the map now at 130, 140: leaves in');
  await expect(dragon).toContainText('Your damage: 2,500 (rank 2 of 4)');
  await expect(dragon).toContainText('1. Hero · 4,000 (44.4%)');
  const queen = win.locator('.wb-row[data-boss="ice-queen"]');
  await expect(queen).toContainText(/Next: in 1 h (29|30) min/);
  await expect(queen).toContainText(/Sydney time \(.* your time\)/);
  await expect(win).toContainText('Red Dragon · Rank 2 · Reward: 3,000,000 zen');
  await expect(win).not.toContainText('40 Goblin Points', { useInnerText: true });
  await expect(win).toContainText('Boss-hunt points this week: 34 (rank 3).');
  await page.screenshot({ path: 'test-results/world-bosses-window.png' });

  await win.locator('.wb-claim').click();
  await expect.poll(() => sent(page).then((list) => list.find((s) => s.name === 'HnmoWorldBossesAction')?.fields)).toEqual({ Action: 1, Argument: '' });
  const after = board({ type: 'claim', result: 'Ok' });
  after.owed = [];
  await feed(page, after);
  await expect.poll(() => chat(page)).toContain('World boss rewards received.');
  await expect(win.locator('.wb-claim')).toBeHidden();
  await feed(page, board({ type: 'reward', result: 'BagFull', count: 1 }));
  await expect.poll(() => chat(page).then((lines) => lines.some((l) => l.startsWith('Your bag is full: the world boss rewards wait')))).toBe(true);

  // Vietnamese.
  await page.evaluate(async () => {
    const { setLanguage } = await import('/js/i18n.js' as string) as { setLanguage(language: string): Promise<void> };
    await setLanguage('vi');
  });
  await expect(win.locator('.wb-row[data-boss="red-dragon"]')).toContainText('Rồng Đỏ');
  await expect(win).toContainText('Điểm săn boss tuần này: 34 (hạng 3).');
  await expect(line).toContainText('Rồng Đỏ');
  await feed(page, board({ type: 'kill', bossId: 'red-dragon', textEn: 'Red Dragon was defeated', textVi: 'Rồng Đỏ đã bị hạ gục' }, false));
  await expect.poll(() => chat(page)).toContain('[Boss thế giới] Rồng Đỏ đã bị hạ gục');
  await page.screenshot({ path: 'test-results/world-bosses-vi.png' });
  await page.evaluate(async () => {
    const { setLanguage } = await import('/js/i18n.js' as string) as { setLanguage(language: string): Promise<void> };
    await setLanguage('en');
  });

  // The boss is gone: the line hides.
  await expect(line).toBeHidden();
  await page.keyboard.press('Escape');
  await expect(win).toHaveCount(0);
  await expectNoPageErrors(errors);
});

test('in the game, the world bosses are asked for, N opens the window and the touch menu has it', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(() => {
    // Collect what the game sends from the start (it asks for the world bosses when it enters the world).
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
  await enterWorld(page, 'test7');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoWorldBossesRequest')), { timeout: 60_000 }).toBe(true);
  const win = page.locator('.rml-hnmo_world_bosses');
  await expect(async () => {
    const open = await page.evaluate(() => !!(window as unknown as Win).__game.worldBosses?.isOpen);
    if (!open) await page.keyboard.press('n');
    await expect(win).toHaveCount(1, { timeout: 20_000 });
  }).toPass({ timeout: 90_000 });
  // Without a server answer (a server without world bosses), the state comes through the game's connection.
  await feed(page, board());
  await expect(win.locator('.wb-row')).toHaveCount(2);
  await expect(page.locator('.hnmo-boss-line')).toBeVisible();
  await page.screenshot({ path: 'test-results/world-bosses-game.png' });
  await page.keyboard.press('n');
  await expect(win).toHaveCount(0);
  await expect(page.locator('#touchMore button[title="World bosses"]')).toHaveCount(1);
  await page.goto('about:blank');
  await expectNoPageErrors(errors);
});

const PUBLIC = () => ({
  v: 1, now: Date.now(), timeZone: 'Australia/Sydney',
  bosses: [
    {
      id: 'red-dragon', nameEn: 'Red Dragon', nameVi: 'Rồng Đỏ', map: 0, mapName: 'Lorencia', schedule: 'fixed', everyHours: 4, times: '12:00, 20:00', days: [],
      nextSpawn: Date.now() + 2 * 3600_000 + 5 * 60_000, despawnMinutes: 30, rankBy: 'Player', huntPoints: 20, alive: null,
      rewards: {
        top1: reward(5000000, 60, [{ group: 14, number: 14, level: 0, count: 1 }]), top2: reward(3000000, 40), top3: reward(2000000, 30),
        top10: reward(1000000, 10), participation: reward(300000, 3), lastHit: reward(0, 20), participationMinimumPercent: 1, participationMinimumDamage: 0,
      },
      lastKill: { bossId: 'red-dragon', nameEn: 'Red Dragon', nameVi: 'Rồng Đỏ', endedAt: Date.now() - 3600_000, killed: true, lastHit: 'Helper', fighters: 3, top: [{ rank: 1, name: 'Hero', damage: 9000, share: 60 }, { rank: 2, name: 'Helper', damage: 6000, share: 40 }] },
    },
    {
      id: 'dark-phoenix', nameEn: 'Dark Phoenix', nameVi: 'Phượng Hoàng Bóng Tối', map: 10, mapName: 'Icarus', schedule: 'fixed', everyHours: 4, times: '21:00', days: ['Saturday', 'Sunday'],
      nextSpawn: Date.now() + 30 * 3600_000, despawnMinutes: 45, rankBy: 'Party', huntPoints: 50,
      alive: { x: 50, y: 60, spawnedAt: Date.now() - 60_000, despawnAt: Date.now() + 40 * 60_000, fighters: 2 },
      rewards: { top1: reward(0, 150), top2: reward(0, 0), top3: reward(0, 0), top10: reward(0, 0), participation: reward(0, 0), lastHit: reward(0, 0), participationMinimumPercent: 1, participationMinimumDamage: 0 },
      lastKill: null,
    },
  ],
  kills: [],
  announcements: [{ at: Date.now() - 3600_000, bossId: 'red-dragon', phase: 'kill', textEn: 'Red Dragon was defeated in Lorencia!', textVi: 'Rồng Đỏ đã bị hạ gục ở Lorencia!' }],
  week: { start: '2026-11-09', ends: Date.now() + 86400_000, rankings: { BossHunt: [{ rank: 1, name: 'Hero', points: 18 }], BloodCastle: [], DevilSquare: [], ChaosCastle: [] }, castleSiegeGuild: null },
  prizes: {
    BossHunt: { enabled: true, coinType: 'GoblinPoints', ranks: [{ rank: 1, coins: 300 }, { rank: 2, coins: 200 }], pointsFinish: 1, pointsWin: 3 },
    BloodCastle: { enabled: true, coinType: 'GoblinPoints', ranks: [{ rank: 1, coins: 200 }], pointsFinish: 1, pointsWin: 3 },
    DevilSquare: { enabled: true, coinType: 'GoblinPoints', ranks: [{ rank: 1, coins: 200 }], pointsFinish: 1, pointsWin: 3 },
    ChaosCastle: { enabled: false, coinType: 'GoblinPoints', ranks: [], pointsFinish: 1, pointsWin: 3 },
  },
  castleSiege: { enabled: true, coinType: 'GoblinPoints', coinsPerMember: 100, guildMasterBonus: 100 },
  lastWeek: { week: '2026-11-02', winners: { BossHunt: [{ rank: 1, name: 'Champion', points: 120, coins: 300, coinType: 'GoblinPoints' }], BloodCastle: [], DevilSquare: [], ChaosCastle: [] }, castleSiegeGuild: 'Lions', castleSiegeMembers: 12 },
});

test('the website shows the world bosses, the last kill and the weekly rankings, in both languages', async ({ page }) => {
  let available = true;
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  await page.route('**/website/data', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/details', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/missions', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/world-bosses', (route) => (available ? route.fulfill({ json: PUBLIC() }) : route.fulfill({ status: 503 })));
  await page.goto('/home.html');
  await page.locator('#en').click();
  const section = page.locator('#bosses');
  await expect(page.locator('nav a[href="#bosses"]')).toHaveText('World bosses');
  await expect(section.locator('.boss')).toHaveCount(2);
  const dragon = section.locator('.boss[data-boss="red-dragon"]');
  await expect(dragon).toContainText('Map: Lorencia');
  await expect(dragon).toContainText(/Next spawn: .* \(Sydney\) · in 2 h 0[45] min/);
  await expect(dragon).toContainText('Your time:');
  await expect(dragon).toContainText('Every day at 12:00, 20:00');
  await expect(dragon).toContainText('Rank 1: 5,000,000 Zen · 60 Goblin Points');
  await expect(dragon).toContainText('Everyone with at least 1% of the damage: 300,000 Zen · 3 Goblin Points');
  await expect(dragon).toContainText('last hit: Helper');
  await expect(dragon).toContainText('Hero — 9,000 (60%)');
  const phoenix = section.locator('.boss[data-boss="dark-phoenix"]');
  await expect(phoenix).toHaveClass(/alive/);
  await expect(phoenix).toContainText(/On the map now — leaves in (39|40):\d\d \(50, 60\)/);
  await expect(phoenix).toContainText('At 21:00 · On Sat, Sun');
  await expect(section.locator('#bosses-news')).toContainText('Red Dragon was defeated in Lorencia!');
  await expect(section.locator('#bosses-week [data-ranking="BossHunt"]')).toContainText('Hero — 18 points');
  await expect(section.locator('#bosses-week [data-ranking="BossHunt"]')).toContainText('Weekly prizes (top 10): 1. 300, 2. 200 Goblin Points');
  await expect(section.locator('#bosses-week [data-ranking="ChaosCastle"]')).toHaveCount(0);
  await expect(section.locator('#bosses-last-week [data-ranking="BossHunt"]')).toContainText('Champion — 120 points · 300 Goblin Points');
  await expect(section.locator('#bosses-siege')).toContainText('Last week\'s winners: Lions');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/world-bosses-website.png', fullPage: false });

  await page.locator('#vi').click();
  await expect(page.locator('nav a[href="#bosses"]')).toHaveText('Boss thế giới');
  await expect(dragon).toContainText('Rồng Đỏ');
  await expect(dragon).toContainText('Hạng 1:');
  await expect(section.locator('#bosses-news')).toContainText('Rồng Đỏ đã bị hạ gục ở Lorencia!');
  await expect(section.locator('#bosses-week [data-ranking="BossHunt"]')).toContainText('Săn boss');

  // The server doesn't answer: a notice instead of old data.
  available = false;
  await page.locator('#en').click();
  await page.locator('#refresh').click();
  await expect(section.locator('#bosses-notice')).toHaveText('The world bosses are unavailable: the server didn\'t answer.');
  await expect(section.locator('.boss')).toHaveCount(0);
});
