// HNMO Event & Boss Timetable (H key, (i) guide button, and website home schedule section).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    timetable?: { isOpen: boolean; handle(bytes: Uint8Array): void };
  };
  __sent: Sent[];
};

const HOUR = 3_600_000;

function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-\uffff]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x33, ...json, 0];
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

test('in the game: H key and (i) guide open the timetable with Sydney time, maps and drops', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test7');
  await page.evaluate(() => {
    const w = window as unknown as Win;
    w.__sent = w.__sent ?? [];
    const orig = w.__game.connection.send.bind(w.__game.connection);
    w.__game.connection.send = (group, name, fields) => {
      w.__sent.push({ name, fields: fields ?? {} });
      return orig(group, name, fields);
    };
  });

  const payload = {
    v: 1,
    off: false,
    now: Date.now(),
    timeZone: 'Australia/Sydney',
    entries: [
      {
        id: 'event-bloodcastle',
        category: 'event',
        nameEn: 'Blood Castle',
        nameVi: 'Huyết Lâu (Blood Castle)',
        mapsEn: 'Blood Castle (Devias)',
        mapsVi: 'Huyết Lâu (Devias)',
        dropsEn: 'Jewel of Chaos, Zen',
        dropsVi: 'Ngọc Hỗn Nguyên, Zen',
        schedule: '01:00, 05:00, 09:00',
        durationMin: 20,
        active: false,
        nextStart: Date.now() + HOUR,
        endsAt: null,
      },
      {
        id: 'invasion-find-gm',
        category: 'invasion',
        nameEn: 'Find the GM',
        nameVi: 'Truy tìm GM',
        mapsEn: 'Lorencia, Noria, Devias',
        mapsVi: 'Lorencia, Noria, Devias',
        dropsEn: 'First finder(s) (1): 50 WCoinC + 500,000 Zen',
        dropsVi: 'Top 1 tìm thấy trước: 50 WCoinC + 500,000 Zen',
        schedule: '01:00, 07:00, 13:00, 19:00',
        durationMin: 15,
        active: true,
        nextStart: Date.now() - 60_000,
        endsAt: Date.now() + 600_000,
      },
    ],
    findGmWinners: [{ name: 'FinderOne', map: 'Lorencia', coins: 50, coinType: 'WCoinC', zen: 500000, at: Date.now() - 7200_000 }],
  };

  await page.keyboard.press('h');
  await feed(page, payload);

  const modal = page.locator('#hnmoTimetable');
  await expect(modal).toBeVisible();
  await expect(modal).toContainText('Blood Castle');
  await expect(modal).toContainText('Jewel of Chaos, Zen');
  await expect(modal).toContainText('Find the GM');
  await expect(modal).toContainText('FinderOne');

  // Filter to Invasions only.
  await modal.getByRole('button', { name: 'Invasions' }).click();
  await expect(modal).toContainText('Find the GM');
  await expect(modal).not.toContainText('Blood Castle');

  await page.keyboard.press('Escape');
  await expect(modal).toBeHidden();

  // Open via the (i) guide button.
  await page.locator('#guideButton').click();
  await page.locator('#guideTimetable').click();
  await feed(page, payload);
  await expect(modal).toBeVisible();

  await expectNoPageErrors(errors);
});

test('on the website home page: #schedule renders the full timetable from /website/timetable', async ({ page }) => {
  await page.route('**/website/timetable', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        v: 1,
        off: false,
        now: Date.now(),
        timeZone: 'Australia/Sydney',
        entries: [
          {
            id: 'invasion-rabbits',
            category: 'invasion',
            nameEn: 'Lunar Rabbit Invasion',
            nameVi: 'Thỏ Ngọc',
            mapsEn: 'Lorencia, Noria, Devias',
            mapsVi: 'Lorencia, Noria, Devias',
            dropsEn: 'Box of Heaven, Jewels',
            dropsVi: 'Hộp Thiên Đường, Ngọc',
            schedule: '00:15, 04:15',
            durationMin: 20,
            active: false,
            nextStart: Date.now() + HOUR,
            endsAt: null,
          },
        ],
        findGmWinners: [],
      }),
    });
  });

  await page.goto('/home.html');
  await expect(page.locator('#timetable-table')).toContainText('Lunar Rabbit Invasion');
  await expect(page.locator('#timetable-table')).toContainText('Box of Heaven, Jewels');
});
