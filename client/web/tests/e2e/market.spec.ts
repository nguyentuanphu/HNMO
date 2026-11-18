// HNMO Item Market e2e tests: website #market section (/website/market feed) and in-game F6 modal (F5 39 / F5 38).
import { test, expect, type Page } from '@playwright/test';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __sent: Sent[];
  __chat: string[];
  __market: {
    handle(bytes: Uint8Array): boolean;
    open(): void;
    close(): void;
    isOpen(): boolean;
  };
};

async function feedMarketState(page: Page, payload: unknown): Promise<void> {
  await page.evaluate(async (json) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    const bytes = write('ServerToClient', 'HnmoMarketState', { Json: json });
    w.__market.handle(bytes);
  }, JSON.stringify(payload));
}

test('in-game Item Market window opens with bag sell list, browse filter, buy and escrow claim packets', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');

  await page.evaluate(async () => {
    const { MarketWindow } = await import('/js/ui/marketWindow.js' as string) as {
      MarketWindow: new (opts: { send: (n: string, f: Record<string, unknown>) => void; message: (t: string) => void }) => Win['__market'];
    };
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    w.__market = new MarketWindow({
      send: (n, f) => w.__sent.push({ name: n, fields: { ...f } }),
      message: (t) => w.__chat.push(t),
    });
    w.__market.open();
  });

  await expect(page.locator('#hnmo-market-panel')).toBeVisible();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoMarketCommand'))).toBe(true);

  await feedMarketState(page, {
    v: 1,
    enabled: true,
    allowWebsite: true,
    now: Date.now(),
    rules: { allowZen: true, allowWCoinC: true, allowWCoinP: false, allowGoblinPoints: false, feePercent: 5, listingZenFee: 10000, maxListings: 10 },
    listings: [
      {
        id: 'mkt001',
        sellerName: 'ElfVendor',
        isMine: false,
        currency: 'WCoinC',
        currencyCode: 1,
        price: 250,
        listedAt: Date.now() - 60_000,
        expiresAt: Date.now() + 3600_000,
        item: { Group: 14, Number: 13, Level: 0, Durability: 1, Skill: false, Luck: false, OptionLevel: 0, ExcellentOptions: 0, NameEn: 'Jewel of Bless', NameVi: 'Ngọc Ước Nguyện' },
      },
    ],
    deliveries: [{ Id: 'd1', Reason: 'Purchased', NameEn: 'Jewel of Soul', NameVi: 'Ngọc Tâm Linh', Level: 0 }],
    pendingZen: 95000,
    bagItems: [{ slot: 12, group: 0, number: 19, level: 9, nameEn: 'Sword of Archangel', nameVi: 'Kiếm Thiên Sứ', banned: false }],
  });

  await expect(page.locator('#hnmo-market-panel')).toContainText('Jewel of Bless');
  await expect(page.locator('#hnmo-market-panel')).toContainText('250 WCoinC');
  await expect(page.locator('#market-claim-btn')).toBeVisible();

  await page.locator('#hnmo-market-panel').getByRole('button', { name: 'Buy' }).click();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoMarketCommand' && s.fields.Action === 3 && s.fields.ListingId === 'mkt001'))).toBe(true);

  await page.locator('#market-claim-btn').click();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoMarketCommand' && s.fields.Action === 5))).toBe(true);

  await page.locator('#hnmo-market-panel').getByRole('button', { name: /Sell from Bag/ }).click();
  await page.locator('#market-list-submit').click();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.some((s) => s.name === 'HnmoMarketCommand' && s.fields.Action === 2))).toBe(true);

  expect(errors).toEqual([]);
});

test('public website #market section renders active listings and handles disabled state cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.route('**/website/market', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      v: 1,
      enabled: true,
      allowWebsite: true,
      now: Date.now(),
      rules: { allowZen: true, allowWCoinC: true, allowWCoinP: true, allowGoblinPoints: false, feePercent: 5, listingZenFee: 0, maxListings: 10 },
      listings: [
        {
          id: 'web001',
          sellerName: 'MerchantDK',
          isMine: false,
          currency: 'WCoinC',
          currencyCode: 1,
          price: 1200,
          listedAt: Date.now(),
          expiresAt: Date.now() + 86400_000,
          item: { Group: 12, Number: 15, Level: 0, Durability: 1, Skill: false, Luck: false, OptionLevel: 0, ExcellentOptions: 0, NameEn: 'Jewel of Chaos', NameVi: 'Ngọc Hỗn Nguyên' },
        },
      ],
      deliveries: [],
      pendingZen: 0,
      bagItems: [],
    }),
  }));

  await page.goto('/home.html#market');
  await expect(page.locator('#market-table')).toContainText('Jewel of Chaos');
  await expect(page.locator('#market-table')).toContainText('1,200 WCoinC');
  expect(errors).toEqual([]);
});
