// HNMO Marriage (Shift+M in game and #marriage couples registry on home.html).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
  };
  __sent: Sent[];
};

function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-\uffff]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x37, ...json, 0];
}

async function feed(page: Page, value: unknown): Promise<void> {
  await page.evaluate((bytes) => {
    (window as unknown as Win).__game.connection.dispatch(new Uint8Array(bytes));
  }, packet(value));
}

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('in the game: Shift+M opens Marriage window, displays bonus/title and sends commands', async ({ page }) => {
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
      if (name === 'HnmoMarriageCommand') return;
      return orig(group, name, fields);
    };
  });

  await page.keyboard.press('Shift+M');
  await feed(page, {
    v: 1,
    enabled: true,
    now: Date.now(),
    rules: {
      minLevel: 100,
      requireCeremonyLocation: true,
      ceremonyMapNumber: 2,
      ceremonyX: 210,
      ceremonyY: 23,
      ceremonyZenCost: 1000000,
      allowCustomTitle: true,
      partyExpBonusPercent: 10,
      partyDefenseBonus: 15,
      divorceZenFee: 2000000,
      remarryCooldownHours: 24,
      allowSpouseWarp: true,
    },
    me: {
      character: 'test7',
      married: true,
      spouse: 'SoulMate',
      titleEn: 'Eternal Pair',
      titleVi: 'Uyên Ương',
      customTitle: 'Eternal Pair',
      marriedAt: Date.now() - 86400_000,
      bonusActiveNow: true,
      incomingProposalFrom: null,
      outgoingProposalTo: null,
    },
    couples: [{ id: 'c1', partnerA: 'test7', partnerB: 'SoulMate', title: 'Eternal Pair', marriedAt: Date.now() - 86400_000, days: 1 }],
  });

  const modal = page.locator('#hnmoMarriage');
  await expect(modal).toBeVisible();
  await expect(modal).toContainText('Married to SoulMate');
  await expect(modal).toContainText('Party couple buff ACTIVE');

  await modal.getByRole('button', { name: 'Warp to spouse' }).click();
  const sent = await page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name === 'HnmoMarriageCommand'));
  expect(sent.some((s) => s.fields.Action === 7)).toBe(true);

  await expectNoPageErrors(errors);
});

test('on the website home page: #marriage renders registered couples from /website/marriage', async ({ page }) => {
  await page.route('**/website/marriage', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        enabled: true,
        rules: { minLevel: 100, partyExpBonusPercent: 10, partyDefenseBonus: 15, ceremonyZenCost: 1000000 },
        couples: [{ partnerA: 'Romeo', partnerB: 'Juliet', title: 'Forever', marriedAt: Date.now() - 172800_000, days: 2 }],
      }),
    });
  });

  await page.goto('/home.html');
  await expect(page.locator('#marriage-couples')).toContainText('Romeo');
  await expect(page.locator('#marriage-couples')).toContainText('Forever');
});
