// Top-up milestones ("thưởng mốc nạp"): game window (U key + fed packets) and public home page section.
import { test, expect, type Page } from '@playwright/test';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __sent: Sent[];
  __chat: string[];
  __window: {
    open(): void;
    toggle(): void;
    isOpen: boolean;
    handlePacket(bytes: Uint8Array): boolean;
  };
};

const SAMPLE_STATE = {
  v: 1,
  enabled: true,
  allowWebsite: true,
  totalVnd: 550_000,
  nextThresholdVnd: 1_000_000,
  steps: [
    { id: 'step-100k', thresholdVnd: 100_000, titleEn: 'First Support (100k VND)', titleVi: 'Mốc ủng hộ 100k', coinType: 'WCoinC', coins: 200, zen: 0, items: [], reached: true, claimed: true, claimable: false },
    { id: 'step-500k', thresholdVnd: 500_000, titleEn: 'Silver Patron (500k VND)', titleVi: 'Mốc ủng hộ 500k', coinType: 'WCoinC', coins: 1200, zen: 0, items: [{ group: 14, number: 14, level: 0, quantity: 5, name: 'Jewel of Soul' }], reached: true, claimed: false, claimable: true },
    { id: 'step-1m', thresholdVnd: 1_000_000, titleEn: 'Gold Patron (1M VND)', titleVi: 'Mốc ủng hộ 1 triệu', coinType: 'WCoinC', coins: 3000, zen: 0, items: [], reached: false, claimed: false, claimable: false },
  ],
  owed: [],
};

async function feed(page: Page, payload: unknown): Promise<void> {
  await page.evaluate(async (json) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    w.__window.handlePacket(write('ServerToClient', 'HnmoTopUpMilestonesState', { Json: json }));
  }, JSON.stringify(payload));
}

test('the top-up milestones game window renders progress and sends claims', async ({ page }) => {
  await page.goto('/index.html');
  await page.evaluate(async () => {
    const { TopUpMilestonesWindow } = await import('/js/ui/topUpMilestonesWindow.js' as string);
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    w.__window = new TopUpMilestonesWindow({
      send: (name: string, fields: Record<string, unknown>) => w.__sent.push({ name, fields }),
      message: (text: string) => w.__chat.push(text),
    });
    window.addEventListener('keydown', (e) => {
      if (e.key.toLowerCase() === 'u') w.__window.toggle();
    });
  });

  await page.keyboard.press('u');
  await expect(page.locator('#hnmo_topup_milestones')).toBeVisible();
  const initialSent = await page.evaluate(() => (window as unknown as Win).__sent);
  expect(initialSent[0].name).toBe('HnmoTopUpMilestonesCommand');
  expect(initialSent[0].fields.Action).toBe(0);

  await feed(page, SAMPLE_STATE);
  await expect(page.locator('#hnmo_topup_milestones')).toContainText('550');
  await expect(page.locator('[data-step-id="step-100k"] button')).toBeDisabled();
  await expect(page.locator('[data-step-id="step-500k"] button')).toBeEnabled();

  await page.locator('[data-step-id="step-500k"] button').click();
  const afterClaim = await page.evaluate(() => (window as unknown as Win).__sent);
  expect(afterClaim[afterClaim.length - 1]).toEqual({
    name: 'HnmoTopUpMilestonesCommand',
    fields: { Action: 1, Argument: 'step-500k' },
  });
});

test('the website home page shows public top-up milestones from the API', async ({ page }) => {
  await page.route('**/website/topup-milestones', (route) => route.fulfill({ json: SAMPLE_STATE }));
  await page.goto('/home.html');
  await expect(page.locator('#topup-milestones-section')).toBeVisible();
  await expect(page.locator('#topup-milestones-list')).toContainText('100');
  await expect(page.locator('#topup-milestones-list')).toContainText(/1[,.]200 WCoinC/);
});
