// HNMO Jewel Bank & Master Skill Reset e2e tests: website #jewel-bank section and in-game F7 window (F5 3A / F5 3B).
import { test, expect, type Page } from '@playwright/test';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __sent: Sent[];
  __chat: string[];
  __jb: {
    handle(bytes: Uint8Array): boolean;
    open(): void;
    close(): void;
    isOpen(): boolean;
  };
};

async function feedJewelBankState(page: Page, payload: unknown): Promise<void> {
  await page.evaluate(async (json) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    const bytes = write('ServerToClient', 'HnmoJewelBankState', { Json: json });
    w.__jb.handle(bytes);
  }, JSON.stringify(payload));
}

test('in-game Jewel Bank window opens, deposits all jewels, withdraws bless, and clears master skills', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');

  await page.evaluate(async () => {
    const { JewelBankWindow } = await import('/js/ui/jewelBankWindow.js' as string) as {
      JewelBankWindow: new (opts: { send: (n: string, f: Record<string, unknown>) => void; message: (t: string) => void }) => Win['__jb'];
    };
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    w.__jb = new JewelBankWindow({
      send: (n, f) => w.__sent.push({ name: n, fields: { ...f } }),
      message: (t) => w.__chat.push(t),
    });
    w.__jb.open();
  });

  await expect(page.locator('#hnmo-jewel-bank-panel')).toBeVisible();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoJewelBankCommand'))).toBe(true);

  await feedJewelBankState(page, {
    v: 1,
    enabled: true,
    allowWebsite: true,
    rules: { withdrawZenFee: 0, depositZenFee: 0, safeZoneOnly: true, allowClearMasterSkills: true, clearMasterZen: 5000000, clearMasterCoins: 0, clearMasterCoinType: 'GoblinPoints' },
    jewels: [
      { key: 'bless', nameEn: 'Jewel of Bless', nameVi: 'Ngọc Ước Nguyện', group: 14, number: 13, enabled: true, maxCapacity: 10000, balance: 24 },
      { key: 'soul', nameEn: 'Jewel of Soul', nameVi: 'Ngọc Tâm Linh', group: 14, number: 14, enabled: true, maxCapacity: 10000, balance: 9 },
    ],
    pending: [],
    bagJewels: [{ slot: 12, key: 'bless', nameEn: 'Jewel of Bless', nameVi: 'Ngọc Ước Nguyện' }],
    master: { freePoints: 5, spentPoints: 30 },
  });

  await expect(page.locator('#hnmo-jewel-bank-panel')).toContainText('Jewel of Bless');
  await expect(page.locator('#hnmo-jewel-bank-panel')).toContainText('24 / 10,000');

  await page.locator('#jb-deposit-all').click();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoJewelBankCommand' && s.fields.Action === 3))).toBe(true);

  await page.locator('[data-jb-withdraw="bless"]').click();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoJewelBankCommand' && s.fields.Action === 4 && s.fields.Key === 'bless'))).toBe(true);

  await page.locator('#jb-clear-master').click();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoJewelBankCommand' && s.fields.Action === 5))).toBe(true);

  expect(errors).toEqual([]);
});

test('public website #jewel-bank section displays configured jewels cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.route('**/website/jewel-bank', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      v: 1,
      enabled: true,
      jewels: [
        { key: 'bless', nameEn: 'Jewel of Bless', nameVi: 'Ngọc Ước Nguyện', enabled: true, maxCapacity: 10000 },
        { key: 'soul', nameEn: 'Jewel of Soul', nameVi: 'Ngọc Tâm Linh', enabled: true, maxCapacity: 10000 },
      ],
    }),
  }));

  await page.goto('/home.html#jewel-bank');
  await expect(page.locator('#jb-table')).toContainText('Jewel of Bless');
  await expect(page.locator('#jb-table')).toContainText('10,000');
  expect(errors).toEqual([]);
});
