// HNMO Daily Lucky Number (L key in game, plus /website/lucky-number on home.html).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    luckyNumber?: { isOpen: boolean; handle(bytes: Uint8Array): void };
  };
  __sent: Sent[];
};

function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-\uffff]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x35, ...json, 0];
}

async function feed(page: Page, value: unknown): Promise<void> {
  await page.evaluate((bytes) => {
    const w = window as unknown as Win;
    w.__game.connection.dispatch(new Uint8Array(bytes));
  }, packet(value));
}

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('in the game: L opens Daily Lucky Number, sends pick and claim commands', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test7');
  await page.evaluate(() => {
    const w = window as unknown as Win;
    w.__sent = [];
    const orig = w.__game.connection.send.bind(w.__game.connection);
    w.__game.connection.send = (group, name, fields) => {
      w.__sent.push({ name, fields: fields ?? {} });
      if (name === 'HnmoLuckyNumberCommand') return;
      return orig(group, name, fields);
    };
  });

  await page.keyboard.press('l');
  await feed(page, {
    v: 1,
    enabled: true,
    allowWebsite: true,
    now: Date.now(),
    timeZone: 'Australia/Sydney',
    drawTime: '20:00',
    roundKey: '2026-11-16',
    nextDrawAt: Date.now() + 3600_000,
    minNumber: 0,
    maxNumber: 99,
    maxPicksPerDay: 2,
    entryCostZen: 0,
    odds: { exactPercent: 1, lastDigitPercent: 9 },
    prizes: {
      exact: { coins: 100, coinType: 'WCoinC', jewelCount: 2, jewelName: 'Jewel of Bless' },
      lastDigit: { enabled: true, coins: 15, coinType: 'GoblinPoints', jewelCount: 0, jewelName: '' },
    },
    totalPicksToday: 5,
    myPicks: [],
    owed: [{ id: 'o1', dateKey: '2026-11-15', tier: 'exact', count: 2, name: 'Jewel of Bless' }],
    recentDraws: [{ dateKey: '2026-11-15', winningNumber: 42, drawnAt: Date.now() - 86400_000, pickCount: 8, winners: [{ name: 'Hero', number: 42, tier: 'exact', coins: 100, coinType: 'WCoinC', jewelCount: 2 }] }],
  });

  const modal = page.locator('#hnmoLuckyNumber');
  await expect(modal).toBeVisible();
  await expect(modal).toContainText('100 WCoinC + 2x Jewel of Bless');
  await expect(modal).toContainText('2026-11-15');

  await modal.locator('input[type="number"]').fill('42');
  await modal.getByRole('button', { name: 'Pick number' }).click();
  await modal.getByRole('button', { name: 'Take' }).click();

  const sent = await page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name === 'HnmoLuckyNumberCommand'));
  expect(sent.some((s) => s.fields.Action === 2 && s.fields.Number === 42)).toBe(true);
  expect(sent.some((s) => s.fields.Action === 3)).toBe(true);

  await expectNoPageErrors(errors);
});

test('on the website home page: #lucky-number renders from /website/lucky-number', async ({ page }) => {
  await page.route('**/website/lucky-number', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        enabled: true,
        roundKey: '2026-11-16',
        nextDrawAt: Date.now() + 3600_000,
        timeZone: 'Australia/Sydney',
        minNumber: 0,
        maxNumber: 99,
        odds: { exactPercent: 1, lastDigitPercent: 9 },
        prizes: {
          exact: { coins: 100, coinType: 'WCoinC', jewelCount: 2, jewelName: 'Jewel of Bless' },
          lastDigit: { enabled: true, coins: 15, coinType: 'GoblinPoints', jewelCount: 0, jewelName: '' },
        },
        recentDraws: [{ dateKey: '2026-11-15', winningNumber: 77, winners: [{ name: 'LuckyHero', number: 77, tier: 'exact' }] }],
      }),
    });
  });

  await page.goto('/home.html');
  await expect(page.locator('#lucky-draws')).toContainText('#77');
  await expect(page.locator('#lucky-draws')).toContainText('LuckyHero');
});
