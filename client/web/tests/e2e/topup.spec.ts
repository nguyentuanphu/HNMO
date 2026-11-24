// The top-up page: generates a local NAPAS 247 VietQR SVG code (no external service) with
// the bank BIN, account number, holder, amount and transfer message ("HNMO <account>").
import { expect, test } from '@playwright/test';

const INFO = {
  enabled: true, accountName: 'NGUYEN VAN TEST', vndPerCoin: 100, minimumVnd: 10000, coinType: 'WCoinC', prefix: 'HNMO',
  banks: [{ code: 'TCB', name: 'Techcombank', bin: '970407', account: '19030000000001' }, { code: 'VCB', name: 'Vietcombank', bin: '970436', account: '0011000000001' }],
};

test('generates local VietQR SVG with bank, account, amount and message without external calls', async ({ page }) => {
  let externalQrCalls = 0;
  await page.route('https://img.vietqr.io/**', (route) => {
    externalQrCalls++;
    return route.abort();
  });
  await page.route('**/topup/info', (route) => route.fulfill({ json: INFO }));
  await page.route('**/website/topup-milestones', (route) => route.fulfill({
    json: {
      enabled: true,
      steps: [{ id: 'step-100k', thresholdVnd: 100000, titleEn: 'First Support', titleVi: 'Mốc 100k', coinType: 'WCoinC', coins: 200, zen: 0, items: [] }],
    },
  }));

  await page.goto('/topup.html?account=test2');
  await expect(page.locator('#topup')).toBeVisible();
  await page.getByRole('button', { name: '100.000 đ' }).click();
  await expect(page.locator('#coins')).toHaveText('1.000');
  await expect(page.locator('#message')).toHaveText('HNMO test2');

  const src = await page.locator('#qr').getAttribute('src') ?? '';
  expect(src.startsWith('data:image/svg+xml;charset=utf-8,')).toBe(true);
  const payload = await page.locator('#qr').getAttribute('data-vietqr') ?? '';
  expect(payload).toContain('970407');
  expect(payload).toContain('19030000000001');
  expect(payload).toContain('5406100000');
  expect(payload).toContain('HNMO test2');
  expect(payload).toMatch(/6304[0-9A-F]{4}$/);

  // Switch to the other bank.
  await page.getByRole('button', { name: 'Vietcombank' }).click();
  const payload2 = await page.locator('#qr').getAttribute('data-vietqr') ?? '';
  expect(payload2).toContain('970436');
  expect(payload2).toContain('0011000000001');
  expect(externalQrCalls).toBe(0);

  // Milestones preview shown.
  await expect(page.locator('#milestones')).toBeVisible();

  // No QR code without an account name.
  await page.locator('#account').fill('');
  await expect(page.locator('#qr')).toBeHidden();
  await expect(page.locator('#needAccount')).toBeVisible();
});

test('says it is closed when the host has not set it up', async ({ page }) => {
  await page.route('**/topup/info', (route) => route.fulfill({ json: { enabled: false } }));
  await page.route('**/website/topup-milestones', (route) => route.fulfill({ json: { enabled: false } }));
  await page.goto('/topup.html');
  await expect(page.locator('#off')).toBeVisible();
  await expect(page.locator('#topup')).toBeHidden();
});

test('the webhooks refuse calls without the key', async ({ request }) => {
  expect((await request.post('/topup/casso', { data: { data: [] } })).status()).toBe(401);
  expect((await request.post('/topup/sepay', { data: {}, headers: { Authorization: 'Apikey wrong' } })).status()).toBe(401);
});

test('signed-in test order displays a fictional VietQR destination and keeps the same retry ID', async ({ page }) => {
  const operations: string[] = [];
  await page.route('**/topup/info', (route) => route.fulfill({ json: { enabled: false } }));
  await page.route('**/website/topup-milestones', (route) => route.fulfill({ json: { enabled: false } }));
  await page.route('**/website/test-topup', (route) => route.fulfill({ json: {
    enabled: true, bank: true, stripe: false, packages: [{ id: 'test', englishName: 'Fictional package', vietnameseName: 'Gói hư cấu', priceVnd: 25000, coins: 15 }],
  } }));
  await page.route('**/portal/account/test-topup/orders', (route) => {
    const fields = route.request().postDataJSON() as { operationId: string; packageId: string; provider: string };
    expect(route.request().headers()['x-hnmo-session']).toBe('1');
    expect(Object.keys(fields).sort()).toEqual(['operationId', 'packageId', 'provider']);
    expect(fields.packageId).toBe('test');
    expect(fields.provider).toBe('bank');
    operations.push(fields.operationId);
    return route.fulfill({ json: { amount: 25000, reference: 'TEST-ORDER-fictional', credited: operations.length > 1 } });
  });
  await page.goto('/topup.html');
  const panel = page.locator('#testPackages');
  await expect(panel).toContainText('Đừng chuyển tiền');
  const buy = panel.getByRole('button', { name: /Gói hư cấu/ });
  await buy.click();
  await expect(panel.getByRole('status')).toContainText('TEST-ORDER-fictional');
  await expect(panel.locator('img')).toHaveAttribute('src', /^data:image\/svg\+xml/);
  await buy.click();
  await expect(panel.getByRole('status')).toContainText('đã xác nhận coin thử');
  await expect(panel.locator('img')).toBeHidden();
  expect(operations).toHaveLength(2);
  expect(operations[0]).toBe(operations[1]);
});

test('returning from Stripe test checkout does not claim the payment is confirmed', async ({ page }) => {
  await page.route('**/topup/info', (route) => route.fulfill({ json: { enabled: false } }));
  await page.route('**/website/test-topup', (route) => route.fulfill({ json: {
    enabled: true, bank: false, stripe: true,
    packages: [{ id: 'test', englishName: 'Fictional package', vietnameseName: 'Gói hư cấu', priceVnd: 25000, coins: 15 }],
  } }));
  await page.goto('/topup.html?stripe-test=returned');
  const panel = page.locator('#testPackages');
  await expect(panel.getByRole('status')).toContainText('Cần xác nhận thanh toán có chữ ký');
  await expect(panel.getByRole('button', { name: /Thẻ Stripe thử nghiệm/ })).toBeVisible();
  await expect(panel.getByRole('button', { name: /Mô phỏng ngân hàng/ })).toHaveCount(0);
});
