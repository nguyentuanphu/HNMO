// HNMO Combining Leftovers e2e tests: website #leftovers section and in-game F10 modal (F5 41).
import { test, expect } from '@playwright/test';

type Win = Window & {
  __lo: { handle(bytes: Uint8Array): boolean; open(): void; isOpen(): boolean };
};

const samplePayload = {
  v: 1,
  enabled: true,
  exchangeNpcNumber: 546,
  effectiveMaxBoxLevel: 3,
  zen: 1_500_000,
  notice: { status: 'ok', messageEn: 'Combined: Box +1 x2 + JoL + JoC -> Box +2.', messageVi: 'Ghép thành công.' },
  recipes: [
    {
      id: 'box-2-to-3',
      nameEn: 'Box +2 x2 + JoL + JoC -> Box +3',
      nameVi: 'Hộp +2 x2 + Sinh Mệnh + Sáng Tạo -> Hộp +3',
      enabled: true,
      successRatePercent: 90,
      zenCost: 1_000_000,
      outputGroup: 14,
      outputNumber: 11,
      outputLevel: 10,
      requiredStageBoxLevel: 3,
      blockedBySeasonStage: false,
      canCombine: true,
      inputs: [
        { group: 14, number: 11, exactLevel: 9, count: 2, nameEn: 'Box of Kundun +2', nameVi: 'Hộp Kundun +2', have: 2 },
        { group: 14, number: 16, exactLevel: -1, count: 1, nameEn: 'Jewel of Life', nameVi: 'Ngọc Sinh Mệnh', have: 1 },
      ],
    },
  ],
  offers: [
    {
      id: 'ex-rena',
      nameEn: '5 × Rena -> 25 Goblin Points',
      nameVi: 'Đổi 5 Rena -> 25 Goblin Points',
      enabled: true,
      inputCount: 5,
      have: 6,
      rewardCoins: 25,
      rewardCoinType: 'GoblinPoints',
      rewardZen: 0,
      rewardItemCount: 0,
      canExchange: true,
    },
  ],
};

test('public website #leftovers renders upgrade recipes and exchange offers cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.route('**/website/leftovers', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(samplePayload),
  }));

  await page.goto('/home.html#leftovers');
  await expect(page.locator('#leftovers-recipes')).toContainText('Box +2 x2 + JoL + JoC -> Box +3');
  await expect(page.locator('#leftovers-offers')).toContainText('5 × Rena -> 25 Goblin Points');
  expect(errors).toEqual([]);
});

test('in-game F10 Leftovers window renders recipes and exchange tabs from F5 41 cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');

  await page.evaluate(async (json) => {
    const { LeftoversWindow } = await import('/js/ui/leftoversWindow.js' as string) as {
      LeftoversWindow: new (send: (n: string, f: Record<string, unknown>) => void) => Win['__lo'];
    };
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    w.__lo = new LeftoversWindow(() => undefined);
    w.__lo.open();
    w.__lo.handle(write('ServerToClient', 'HnmoLeftoversState', { Json: json }));
  }, JSON.stringify(samplePayload));

  await expect(page.locator('#hnmo-leftovers-panel')).toBeVisible();
  await expect(page.locator('#hnmo-leftovers-panel')).toContainText('Box +2 x2 + JoL + JoC -> Box +3');
  await page.locator('#hnmo-leftovers-panel').getByRole('button', { name: 'Surplus Exchange' }).click();
  await expect(page.locator('#hnmo-leftovers-panel')).toContainText('5 × Rena -> 25 Goblin Points');
  expect(errors).toEqual([]);
});
