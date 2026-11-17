import { expect, test, type Page } from '@playwright/test';
import type { VipOperation, VipStateView } from '../../src/vipModels.js';

const offer = { levelId: 1, nameEn: '<Bronze>', nameVi: '<Đồng>', days: 7, price: 100, coinType: 'WCoinC' as const, offerToken: 'a'.repeat(64), experiencePercent: 5, zenPercent: 5, dailyCoins: 10, dailyCoinType: 'GoblinPoints' as const };
const initial = (): VipStateView => ({ enabled: true, active: null, offers: [offer], pending: null, daily: { available: false, claimed: false } });
const applied = (): VipStateView => ({ ...initial(), active: { ...offer, untilUtc: '2026-11-17T14:30:00Z' }, status: 'Applied', daily: { available: true, claimed: false } });

async function fixture(page: Page, language: 'en' | 'vi'): Promise<void> {
  await page.route('**/vip-component-fixture.html', route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><html lang="${language}"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><body style="font-family:sans-serif;max-width:900px;margin:12px"><div id="changes">0</div><div id="errors"></div><div id="responses">0</div><script type="module">
    import { AccountVip } from '/js/accountVip.js';
    const vip = new AccountVip({
      language: () => document.documentElement.lang === 'vi' ? 'vi' : 'en', accountKey: () => 'ann',
      request: async (path, body = {}) => {
        const response = await fetch(path, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ loginName: 'ann', password: 'fixture-secret', ...body }) });
        if (!response.ok) throw new Error('Fixture response failed');
        const json = await response.json();
        const count = document.getElementById('responses'); count.textContent = String(Number(count.textContent) + 1);
        return json;
      },
      changed: () => { const count = document.getElementById('changes'); count.textContent = String(Number(count.textContent) + 1); },
      error: () => { document.getElementById('errors').textContent = 'fixture error'; },
    });
    document.addEventListener('fixture-reset-vip', () => vip.reset());
    document.body.append(vip.root); await vip.open();
    </script></body></html>` }));
  await page.route('**/portal/account/vip/state', async route => {
    expect(route.request().postDataJSON()).toEqual({ loginName: 'ann', password: 'fixture-secret' });
    await route.fulfill({ json: initial() });
  });
  await page.goto('/vip-component-fixture.html');
  await expect(page.locator('#accountVip')).toContainText(language === 'vi' ? 'Chọn gói mua' : 'Choose an offer');
}

for (const language of ['en', 'vi'] as const) {
  test(`VIP account reviews price and retries the same operation (${language})`, async ({ page }) => {
    await page.setViewportSize({ width: language === 'vi' ? 375 : 1280, height: 820 });
    const requests: VipOperation[] = [];
    await page.route('**/portal/account/vip/purchase', async route => {
      const body = route.request().postDataJSON() as VipOperation & { loginName: string; password: string };
      expect(body.loginName).toBe('ann'); expect(body.password).toBe('fixture-secret');
      expect(body.levelId).toBe(1); expect(body.days).toBe(7); expect(body.offerToken).toBe(offer.offerToken);
      requests.push({ operationId: body.operationId, levelId: body.levelId, days: body.days, offerToken: body.offerToken });
      await route.fulfill({ json: requests.length === 1 ? { ...initial(), status: 'Pending', pending: requests[0] } : applied() });
    });
    await page.route('**/portal/account/vip/daily', async route => {
      expect(route.request().postDataJSON()).toEqual({ loginName: 'ann', password: 'fixture-secret' });
      await route.fulfill({ json: { ...applied(), status: 'DailyApplied', daily: { available: false, claimed: true } } });
    });
    await fixture(page, language);
    const root = page.locator('#accountVip');
    await root.getByRole('button', { name: language === 'vi' ? 'Cách dùng trang này' : 'How to use this page' }).click();
    await expect(root.locator('#accountVipHelp')).toBeVisible();
    for (const button of await root.getByRole('button').all()) await expect(button).toHaveAttribute('title', /.+/);
    await expect(root.getByRole('heading', { name: language === 'vi' ? '<Đồng>' : '<Bronze>', exact: true })).toBeVisible();
    await expect(root.locator('bronze, đồng')).toHaveCount(0);
    await root.getByRole('button', { name: language === 'vi' ? 'Xem lại giao dịch' : 'Review purchase', exact: true }).click();
    expect(requests).toHaveLength(0);
    await expect(root).toContainText(language === 'vi' ? 'Xem lại: <Đồng>, 7 ngày, 100 W Coin (C).' : 'Review: <Bronze>, 7 days, 100 W Coin (C).');
    await root.getByRole('button', { name: language === 'vi' ? 'Hủy' : 'Cancel', exact: true }).click();
    expect(requests).toHaveLength(0);
    await root.getByRole('button', { name: language === 'vi' ? 'Xem lại giao dịch' : 'Review purchase', exact: true }).click();
    await root.getByRole('button', { name: language === 'vi' ? 'Xác nhận mua' : 'Confirm purchase', exact: true }).click();
    const retry = root.getByRole('button', { name: language === 'vi' ? 'Thử lại giao dịch đang chờ' : 'Retry pending purchase', exact: true });
    await expect(retry).toBeVisible();
    expect(requests).toHaveLength(1);
    await expect(root.getByRole('button', { name: language === 'vi' ? 'Xem lại giao dịch' : 'Review purchase', exact: true })).toHaveCount(0);
    await retry.click();
    await expect(root).toContainText(language === 'vi' ? 'Giao dịch VIP đã được ghi nhận' : 'Your VIP purchase is recorded');
    expect(requests).toHaveLength(2); expect(requests[1]).toEqual(requests[0]);
    await expect(root).toContainText('18/11/2026'); await expect(root).toContainText('01:30');
    await root.getByRole('button', { name: language === 'vi' ? 'Nhận coin hằng ngày' : 'Collect daily coins', exact: true }).click();
    await expect(root).toContainText(language === 'vi' ? 'Bạn đã nhận coin VIP hôm nay.' : 'Today’s VIP coins have already been collected.');
    await expect(page.locator('#changes')).toHaveText('2');
    await expect(page.locator('#errors')).toBeEmpty();
    const storage = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
    expect(storage).not.toContain('fixture-secret');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  });

  test(`VIP account price changes and logout do not confirm a stale view (${language})`, async ({ page }) => {
    let requests = 0;
    let finish: (() => void) | undefined;
    const blocked = new Promise<void>(resolve => { finish = resolve; });
    await page.route('**/portal/account/vip/purchase', async route => {
      requests++;
      if (requests === 1) await route.fulfill({ json: { ...initial(), status: 'PriceChanged' } });
      else { await blocked; await route.fulfill({ json: applied() }); }
    });
    await fixture(page, language);
    const root = page.locator('#accountVip');
    const review = () => root.getByRole('button', { name: language === 'vi' ? 'Xem lại giao dịch' : 'Review purchase', exact: true });
    const confirm = () => root.getByRole('button', { name: language === 'vi' ? 'Xác nhận mua' : 'Confirm purchase', exact: true });
    await review().click(); await confirm().click();
    await expect(root).toContainText(language === 'vi' ? 'Gói VIP đã thay đổi.' : 'The offer has changed.');
    await expect(page.locator('#changes')).toHaveText('0');
    await review().click(); await confirm().click();
    await expect.poll(() => requests).toBe(2);
    await page.evaluate(() => document.dispatchEvent(new Event('fixture-reset-vip')));
    finish!();
    await expect(page.locator('#responses')).toHaveText('3');
    await expect(root).toBeHidden(); await expect(root).toBeEmpty();
    await expect(page.locator('#changes')).toHaveText('0');
    await expect(page.locator('#errors')).toBeEmpty();
  });
}
