import { expect, test, type Page } from '@playwright/test';

const offer = { levelId: 1, nameEn: 'Bronze', nameVi: 'Đồng', days: 7, price: 100, coinType: 'WCoinC', offerToken: 'a'.repeat(64), experiencePercent: 5, zenPercent: 5, dailyCoins: 10, dailyCoinType: 'GoblinPoints' };

async function fixture(page: Page, language: 'en' | 'vi'): Promise<void> {
  await page.route('**/vip-game-fixture.html', route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><body><button id="open">Open fixture</button><button id="disconnect">Disconnect fixture</button><button id="late">Late fixture reply</button><div id="messages"></div><div id="sent" hidden></div><script type="module">
    import { VipWindow } from '/js/ui/vipWindow.js';
    import { setLanguage } from '/js/i18n.js';
    import { write, read } from '/js/net/packets.js';
    await setLanguage('${language}');
    const offer = ${JSON.stringify(offer)};
    const state = { enabled: true, accountKey: 'ann', active: null, offers: [offer], pending: null, daily: { available: false, claimed: false } };
    const active = { ...offer, untilUtc: '2026-11-17T14:30:00Z' };
    let purchases = 0; let late;
    function reply(id, next) { vip.handle(write('ServerToClient', 'HnmoVipState', { RequestId: id, Json: JSON.stringify(next) })); }
    const vip = new VipWindow({ send: (name, fields) => {
      const bytes = write('ClientToServer', name, fields);
      const row = document.createElement('div'); row.textContent = JSON.stringify({ name, fields, bytes: [...bytes] }); document.getElementById('sent').append(row);
      if (name !== 'HnmoVipRequest') return;
      const decoded = read('ClientToServer', name, bytes); const request = JSON.parse(decoded.Json);
      if (request.action === 'state') queueMicrotask(() => reply(fields.RequestId, state));
      if (request.action === 'purchase') {
        purchases++;
        const operation = { operationId: request.operationId, levelId: request.levelId, days: request.days, offerToken: request.offerToken };
        const result = purchases === 1 ? { ...state, pending: operation, status: 'Pending' } : { ...state, active, status: 'Applied', daily: { available: true, claimed: false } };
        late = () => reply(fields.RequestId, result);
        if (!document.body.hasAttribute('data-hold-purchase')) queueMicrotask(late);
      }
      if (request.action === 'daily') queueMicrotask(() => reply(fields.RequestId, { ...state, active, status: 'DailyApplied', daily: { available: false, claimed: true } }));
    }, message: text => { document.getElementById('messages').textContent = text; } });
    document.getElementById('open').onclick = () => vip.open();
    document.getElementById('disconnect').onclick = () => { vip.reset(); vip.close(); };
    document.getElementById('late').onclick = () => late?.();
    document.body.dataset.ready = 'true';
  </script></body></html>` }));
  await page.goto('/vip-game-fixture.html');
  await expect(page.locator('body')).toHaveAttribute('data-ready', 'true');
}

for (const language of ['en', 'vi'] as const) {
  test(`VIP game packets preserve reviewed purchase and retry (${language})`, async ({ page }) => {
    await page.setViewportSize({ width: language === 'vi' ? 375 : 1280, height: 820 });
    await fixture(page, language);
    await page.locator('#open').click();
    const panel = page.locator('#hnmo-vip-panel');
    await expect(panel).toBeVisible();
    await expect(panel).toContainText(language === 'vi' ? 'Đồng' : 'Bronze');
    await panel.getByRole('button', { name: language === 'vi' ? 'Cách dùng trang này' : 'How to use this page' }).click();
    await expect(panel.locator('#accountVipHelp')).toBeVisible();
    for (const button of await panel.getByRole('button').all()) await expect(button).toHaveAttribute('title', /.+/);
    await panel.getByRole('button', { name: language === 'vi' ? 'Xem lại giao dịch' : 'Review purchase', exact: true }).click();
    await expect(page.locator('#sent > div')).toHaveCount(1);
    await panel.getByRole('button', { name: language === 'vi' ? 'Xác nhận mua' : 'Confirm purchase', exact: true }).click();
    await panel.getByRole('button', { name: language === 'vi' ? 'Thử lại giao dịch đang chờ' : 'Retry pending purchase', exact: true }).click();
    await expect(panel).toContainText(language === 'vi' ? 'Giao dịch VIP đã được ghi nhận' : 'Your VIP purchase is recorded');
    await expect(panel).toContainText('18/11/2026'); await expect(panel).toContainText('01:30');
    const records = await page.locator('#sent > div').allTextContents();
    const requests = records.map(value => JSON.parse(value) as { name: string; fields: { RequestId: number; Json: string }; bytes: number[] }).filter(value => value.name === 'HnmoVipRequest');
    expect(requests).toHaveLength(3);
    const bodies = requests.map(value => JSON.parse(value.fields.Json) as Record<string, unknown>);
    expect(bodies[0]).toEqual({ action: 'state' });
    expect(bodies[1]).toEqual({ action: 'purchase', levelId: 1, days: 7, offerToken: 'a'.repeat(64), operationId: expect.stringMatching(/^[a-f0-9-]{36}$/i) });
    expect(bodies[2]).toEqual(bodies[1]);
    for (const request of requests) {
      expect(request.bytes.slice(0, 4)).toEqual([0xc1, request.bytes.length, 0xf5, 0x25]);
      const idBytes = new Uint8Array(request.bytes.slice(4, 8));
      expect(new DataView(idBytes.buffer).getUint32(0, true)).toBe(request.fields.RequestId);
      expect(request.fields.RequestId).toBeGreaterThan(0);
      expect(request.bytes.length).toBeLessThanOrEqual(255);
    }
    expect(new Set(requests.map(value => value.fields.RequestId)).size).toBe(3);
    await panel.getByRole('button', { name: language === 'vi' ? 'Nhận coin hằng ngày' : 'Collect daily coins', exact: true }).click();
    await expect(panel).toContainText(language === 'vi' ? 'Quà coin VIP hôm nay đã được ghi nhận.' : 'Today’s VIP bonus is already recorded.');
    await panel.getByRole('button', { name: language === 'vi' ? 'Đóng' : 'Close', exact: true }).click();
    await expect(panel).toBeHidden();
    await page.locator('#late').click();
    await expect(panel).toBeHidden();
    await expect(page.locator('#messages')).toBeEmpty();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    const secrets = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
    expect(secrets).not.toContain('password');
  });
}


test('VIP game disconnect rejects an outstanding purchase view and ignores its later packet', async ({ page }) => {
  await fixture(page, 'en');
  await page.locator('#open').click();
  const panel = page.locator('#hnmo-vip-panel');
  await panel.getByRole('button', { name: 'Review purchase', exact: true }).click();
  await page.evaluate(() => document.body.setAttribute('data-hold-purchase', 'true'));
  await panel.getByRole('button', { name: 'Confirm purchase', exact: true }).click();
  await expect(page.locator('#sent > div')).toHaveCount(2);
  await page.keyboard.press('Escape');
  await expect(panel).toBeHidden();
  await page.locator('#disconnect').click();
  await page.locator('#late').click();
  await expect(panel).toBeHidden();
  await expect(page.locator('#messages')).toBeEmpty();
  const pending = await page.evaluate(() => sessionStorage.getItem('hnmo.vip.operation.ANN'));
  expect(pending).toContain('operationId');
  expect(pending).not.toContain('password');
});
