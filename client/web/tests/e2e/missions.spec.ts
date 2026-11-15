// HNMO's missions window (J): it asks the server for the missions (HnmoMissionsRequest), shows the state the
// server sends (HnmoMissionsState, C2 F5 09) with progress bars and claim buttons, the streak, the login calendar
// and the earned coins, and its buttons send HnmoMissionsAction. A finished mission is told in the chat.
// The state is fed to the client (the test server may run without the missions), so what's checked is the window
// and the packets it sends; the server's rules are covered by its unit tests (MissionEngineTest).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    missions?: { isOpen: boolean; handle(bytes: Uint8Array): boolean };
  };
  __sent: Sent[];
  __chat: string[];
  __window: { handle(bytes: Uint8Array): boolean; open(): void; toggle(): void; isOpen: boolean };
};

const reward = (zen: number, coins: number, items: { group: number; number: number; level: number; count: number }[] = []) => ({ zen, coins, coinType: 'GoblinPoints', items });
const board = (event?: Record<string, unknown>) => ({
  v: 1, now: Date.now(), day: '2026-11-15', week: '2026-11-09', nextDaily: Date.now() + 5 * 3600_000, nextWeekly: Date.now() + 30 * 3600_000,
  missions: [
    { id: 'daily-kill', period: 'daily', kind: 'KillMonsters', titleEn: 'Defeat 300 monsters', titleVi: 'Hạ gục 300 quái vật', value: 300, target: 300, claimed: false, reward: reward(500000, 10) },
    { id: 'daily-mix', period: 'daily', kind: 'ChaosMachineMix', titleEn: 'Make 3 successful mixes at the chaos machine', titleVi: 'Pha chế thành công 3 lần ở máy Chaos', value: 1, target: 3, claimed: false, reward: reward(300000, 5) },
    { id: 'daily-zen', period: 'daily', kind: 'PickUpZen', titleEn: 'Pick up 1,000,000 zen', titleVi: 'Nhặt 1.000.000 Zen', value: 1000000, target: 1000000, claimed: true, reward: reward(0, 5) },
    { id: 'weekly-kill', period: 'weekly', kind: 'KillMonsters', titleEn: 'Defeat 3,000 monsters', titleVi: 'Hạ gục 3.000 quái vật', value: 1200, target: 3000, claimed: false, reward: reward(0, 40, [{ group: 14, number: 14, level: 0, count: 2 }]) },
  ],
  streak: { days: 6, todayCounted: false, required: 3, bonusDay: 7, bonusClaimable: true, bonus: reward(0, 50, [{ group: 14, number: 13, level: 0, count: 1 }]) },
  calendar: { enabled: true, month: '2026-11', claimedDays: 4, claimedToday: false, daysInMonth: 30, coinType: 'GoblinPoints', rewards: Array.from({ length: 30 }, (_, i) => (i === 6 ? 20 : i === 13 ? 30 : i === 27 ? 60 : 5)) },
  online: { enabled: true, coinsToday: 15, cap: 40, minutesToday: 190, coinsPerHour: 5, coinType: 'GoblinPoints', autoPlayHalf: true },
  earned: { today: 35, cap: 300, owed: 0 },
  milestones: { enabled: true, coinType: 'GoblinPoints', levels: [{ level: 100, coins: 20 }, { level: 200, coins: 30 }], coinsPerReset: 50 },
  invite: { enabled: true, code: 'K7M2QX', rewarded: false, requiredLevel: 150, inviterCoins: 100, invitedCoins: 30, coinType: 'GoblinPoints' },
  event,
});

// The state packet, as the server sends it, to the window (or through the game's connection).
async function feed(page: Page, state: unknown): Promise<void> {
  await page.evaluate(async (json) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    const bytes = write('ServerToClient', 'HnmoMissionsState', { Json: json });
    if (w.__window) w.__window.handle(bytes); else w.__game.connection.dispatch(bytes);
  }, JSON.stringify(state));
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoMissions')));
const chat = (page: Page) => page.evaluate(() => (window as unknown as Win).__chat);

/** Whether a game server answers (the connect server's port). */
function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('the missions window shows the missions and sends the claims', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/index.html');
  // The window on its own, as the game makes it (main.ts), with its packets and chat lines collected.
  await page.evaluate(async () => {
    const { MissionsWindow } = await import('/js/ui/missionsWindow.js' as string) as { MissionsWindow: new (options: unknown) => Win['__window'] };
    const { itemName, loadItemCatalog } = await import('/js/game/items.js' as string) as { itemName(g: number, n: number): string; loadItemCatalog(): Promise<void> };
    await loadItemCatalog().catch(() => undefined);
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    w.__window = new MissionsWindow({
      send: (name: string, fields: Record<string, unknown>) => w.__sent.push({ name, fields: { ...fields } }),
      message: (text: string) => w.__chat.push(text),
      itemName: (g: number, n: number) => itemName(g, n),
    });
  });

  // A finished mission is told in the chat, also with the window closed.
  await feed(page, board({ type: 'finished', result: 'Ok', ids: ['daily-kill'] }));
  await expect.poll(() => chat(page)).toContain('Mission complete: Defeat 300 monsters. Claim the reward in the missions window (J).');

  // Opening asks for the state.
  await page.evaluate(() => (window as unknown as Win).__window.open());
  const window = page.locator('.rml-hnmo_missions');
  await expect(window).toHaveCount(1);
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoMissionsRequest'))).toBe(true);
  await feed(page, board());

  // The daily tab: three rows, the finished one with a claim button, the others with their state.
  await expect(window.locator('.mi-row')).toHaveCount(3);
  await expect(window.locator('.mi-row', { hasText: 'Defeat 300 monsters' }).locator('.mi-btn.claim')).toBeVisible();
  await expect(window.locator('.mi-row', { hasText: 'chaos machine' })).toContainText('1 / 3');
  await expect(window.locator('.mi-row', { hasText: 'Pick up' })).toContainText('Claimed');
  await expect(window.getByText(/Starts again in [45] h/)).toBeVisible();
  const width = await window.locator('.mi-row', { hasText: 'chaos machine' }).locator('.mi-fill').evaluate((e) => (e as HTMLElement).style.width);
  expect(width).toBe('33%');
  await page.screenshot({ path: 'test-results/missions-daily.png' });

  await window.locator('.mi-row', { hasText: 'Defeat 300 monsters' }).locator('.mi-btn.claim').click();
  await expect.poll(() => sent(page).then((list) => list.find((s) => s.name === 'HnmoMissionsAction')?.fields)).toEqual({ Action: 1, Argument: 'daily-kill' });

  // The answer: claimed, and a full bag is told.
  const claimed = board({ type: 'claim', result: 'Ok', ids: ['daily-kill'], coins: 10 });
  claimed.missions[0].claimed = true;
  await feed(page, claimed);
  await expect(window.locator('.mi-row', { hasText: 'Defeat 300 monsters' })).toContainText('Claimed');
  await expect.poll(() => chat(page)).toContain('Reward of "Defeat 300 monsters" received.');
  await feed(page, board({ type: 'claim', result: 'BagFull', ids: ['daily-kill'] }));
  await expect.poll(() => chat(page).then((lines) => lines.some((l) => l.startsWith('Your bag is full')))).toBe(true);

  // The weekly tab.
  await window.locator('.mi-tab', { hasText: 'Weekly' }).click();
  await expect(window.locator('.mi-row')).toHaveCount(1);
  await expect(window.locator('.mi-row')).toContainText('1,200 / 3,000');
  await expect(window.locator('.mi-row')).toContainText('x2');

  // The coins tab: the streak bonus, the calendar with the next day marked, the online coins, the invite.
  await window.locator('.mi-tab', { hasText: 'Coins' }).click();
  await expect(window.locator('.mi-day')).toHaveCount(30);
  await expect(window.locator('.mi-day.claimed')).toHaveCount(4);
  await expect(window.locator('.mi-day.next')).toContainText('Day 5');
  await expect(window.getByText('Online time: 15 / 40 Goblin Points today', { exact: false })).toBeVisible();
  await expect(window.getByText('All earned coins today: 35 / 300.')).toBeVisible();
  await expect(window.getByText(/Your code: K7M2QX/)).toBeVisible();
  await page.screenshot({ path: 'test-results/missions-coins.png' });
  await window.locator('.mi-streak-claim').click();
  await window.locator('.mi-calendar-claim').click();
  await window.locator('.mi-invite-input').fill('friend9');
  await window.locator('.mi-invite-send').click();
  await expect.poll(() => sent(page).then((list) => list.filter((s) => s.name === 'HnmoMissionsAction').map((s) => s.fields))).toEqual([
    { Action: 1, Argument: 'daily-kill' }, { Action: 2, Argument: '' }, { Action: 3, Argument: '' }, { Action: 4, Argument: 'friend9' },
  ]);
  await feed(page, board({ type: 'calendar', result: 'Ok', coins: 5 }));
  await expect.poll(() => chat(page)).toContain('Login calendar: 5 Goblin Points received.');

  // Vietnamese: the window's texts and the missions' own Vietnamese titles.
  await page.evaluate(async () => {
    const { setLanguage } = await import('/js/i18n.js' as string) as { setLanguage(language: string): Promise<void> };
    await setLanguage('vi');
  });
  await window.locator('.mi-tab', { hasText: 'Hằng ngày' }).click();
  await expect(window.getByText('Hạ gục 300 quái vật')).toBeVisible();
  await expect(window.locator('.mi-row', { hasText: 'Nhặt' })).toContainText('Đã nhận');
  await expect(window.getByText(/Bắt đầu lại sau/)).toBeVisible();
  await page.screenshot({ path: 'test-results/missions-vi.png' });
  await page.evaluate(async () => {
    const { setLanguage } = await import('/js/i18n.js' as string) as { setLanguage(language: string): Promise<void> };
    await setLanguage('en');
  });
  await expect(window.getByText('Defeat 300 monsters')).toBeVisible();

  // Escape closes it.
  await page.keyboard.press('Escape');
  await expect(window).toHaveCount(0);
  await expectNoPageErrors(errors);
});

test('in the game, J opens the missions window and the touch menu has it', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test9');
  await page.evaluate(() => {
    const w = window as unknown as Win;
    w.__sent = [];
    const connection = w.__game.connection;
    const send = connection.send.bind(connection);
    connection.send = (group, name, fields) => {
      w.__sent.push({ name, fields: { ...(fields ?? {}) } });
      send(group, name, fields);
    };
  });
  const window = page.locator('.rml-hnmo_missions');
  await expect(async () => {
    if (!(await window.count())) await page.keyboard.press('j');
    await expect(window).toHaveCount(1, { timeout: 5_000 });
  }).toPass({ timeout: 60_000 });
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoMissionsRequest'))).toBe(true);
  // Without a server answer (an older server), the state comes through the game's connection like the server's.
  if (!(await window.locator('.mi-row').count())) await feed(page, board());
  await expect(window.locator('.mi-row').first()).toBeVisible();
  await page.screenshot({ path: 'test-results/missions-game.png' });
  await page.keyboard.press('j');
  await expect(window).toHaveCount(0);
  await expect(page.locator('#touchMore button[title="Missions"]')).toHaveCount(1);
  await expectNoPageErrors(errors);
});
