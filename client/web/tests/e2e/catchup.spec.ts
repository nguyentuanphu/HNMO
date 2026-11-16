// HNMO Catch-up Level & Reset Cap e2e test: website #catchup section and in-game panel (F5 3D).
import { test, expect } from '@playwright/test';

type Win = Window & {
  __cu: { handle(bytes: Uint8Array): boolean; open(): void; isOpen(): boolean };
};

test('public website #catchup displays today level cap and daily reset limit cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.route('**/website/catchup', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      v: 1,
      enabled: true,
      now: Date.now(),
      nextDayAt: Date.now() + 8 * 3600_000,
      levelCap: { enabled: true, todayCap: 320, initialCap: 300, hardMax: 400, perDay: 10, triggered: true, firstPlayer: 'TopWizard' },
      resetCap: { enabled: true, todayDailyLimit: 5, initialDailyLimit: 3, perDay: 1, hardMax: 20, triggerResets: 5, triggered: true, firstPlayer: 'TopWizard' },
    }),
  }));

  await page.goto('/home.html#catchup');
  await expect(page.locator('#catchup-cards')).toContainText('Lv.320 / 400');
  await expect(page.locator('#catchup-cards')).toContainText('TopWizard');
  await expect(page.locator('#catchup-cards')).toContainText('Today\'s daily reset limit: 5');
  expect(errors).toEqual([]);
});

test('in-game CatchupBanner renders packet F5 3D state cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');

  await page.evaluate(async () => {
    const { CatchupBanner } = await import('/js/ui/catchupBanner.js' as string) as {
      CatchupBanner: new (send: (n: string, f: Record<string, unknown>) => void) => Win['__cu'];
    };
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    w.__cu = new CatchupBanner(() => undefined);
    w.__cu.open();
    w.__cu.handle(write('ServerToClient', 'HnmoCatchupCapState', {
      Json: JSON.stringify({
        v: 1,
        enabled: true,
        now: Date.now(),
        nextDayAt: Date.now() + 3600_000,
        levelCap: { enabled: true, todayCap: 310, initialCap: 300, hardMax: 400, perDay: 10, triggered: true, firstPlayer: 'KnightZero' },
        resetCap: { enabled: false, todayDailyLimit: 0, initialDailyLimit: 0, perDay: 0, hardMax: 0, triggerResets: 0, triggered: false, firstPlayer: '' },
      }),
    }));
  });

  await expect(page.locator('#hnmo-catchup-panel')).toBeVisible();
  await expect(page.locator('#hnmo-catchup-panel')).toContainText('Lv.310 / 400');
  await expect(page.locator('#hnmo-catchup-panel')).toContainText('KnightZero');
  expect(errors).toEqual([]);
});
