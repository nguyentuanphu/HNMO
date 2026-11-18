import { expect, test } from '@playwright/test';

test('guide documents Event alerts, sw-alerts.js is served, and eventAlertsWidget mounts cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Event alerts');

  const swResp = await page.request.get('/sw-alerts.js');
  expect(swResp.status()).toBe(200);
  const swText = await swResp.text();
  expect(swText).toContain('showNotification');

  await page.goto('/index.html');
  await page.route('**/website/event-alerts', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        off: false,
        leadTimeMinutes: 5,
        categories: ['blood_castle', 'world_boss'],
        vapidPublicKey: 'BElDemoPublicKeyForUiCheck0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ',
      }),
    });
  });

  const text = await page.evaluate(async () => {
    const mod = await import('/js/ui/eventAlertsWidget.js' as string) as {
      mountEventAlertsWidget(el: HTMLElement): Promise<void>;
    };
    const host = document.createElement('div');
    document.body.append(host);
    await mod.mountEventAlertsWidget(host);
    return host.textContent ?? '';
  });

  expect(text).toContain('Blood Castle');
  expect(text).toContain('World Bosses');
  expect(text).toContain('Enable Browser Alerts');
  expect(errors).toEqual([]);
});
