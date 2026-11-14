// The top-up page: a VietQR code for the host's bank account with the amount and the transfer message
// ("HNMO <account>") filled in; Casso / SePay then report the transfer and the coins are added.
import { expect, test } from '@playwright/test';

const INFO = {
  enabled: true, accountName: 'NGUYEN VAN TEST', vndPerCoin: 100, minimumVnd: 10000, coinType: 'WCoinC', prefix: 'HNMO',
  banks: [{ code: 'TCB', name: 'Techcombank', bin: '970407', account: '19030000000001' }, { code: 'VCB', name: 'Vietcombank', bin: '970436', account: '0011000000001' }],
};

test('shows the VietQR code with the amount and the account in the message', async ({ page }) => {
  await page.route('**/topup/info', (route) => route.fulfill({ json: INFO }));
  await page.route('https://img.vietqr.io/**', (route) => route.fulfill({ body: '', contentType: 'image/png' }));
  await page.goto('/topup.html?account=test2');
  await expect(page.locator('#topup')).toBeVisible();
  await page.getByRole('button', { name: '100.000 đ' }).click();
  await expect(page.locator('#coins')).toHaveText('1.000');
  await expect(page.locator('#message')).toHaveText('HNMO test2');
  const qr = new URL(await page.locator('#qr').getAttribute('src') ?? '');
  expect(qr.pathname).toBe('/image/970407-19030000000001-compact2.png');
  expect(qr.searchParams.get('amount')).toBe('100000');
  expect(qr.searchParams.get('addInfo')).toBe('HNMO test2');
  // The other bank.
  await page.getByRole('button', { name: 'Vietcombank' }).click();
  expect(new URL(await page.locator('#qr').getAttribute('src') ?? '').pathname).toBe('/image/970436-0011000000001-compact2.png');
  // No QR code without an account name.
  await page.locator('#account').fill('');
  await expect(page.locator('#qr')).toBeHidden();
  await expect(page.locator('#needAccount')).toBeVisible();
});

test('says it is closed when the host has not set it up', async ({ page }) => {
  await page.route('**/topup/info', (route) => route.fulfill({ json: { enabled: false } }));
  await page.goto('/topup.html');
  await expect(page.locator('#off')).toBeVisible();
  await expect(page.locator('#topup')).toBeHidden();
});

test('the webhooks refuse calls without the key', async ({ request }) => {
  expect((await request.post('/topup/casso', { data: { data: [] } })).status()).toBe(401);
  expect((await request.post('/topup/sepay', { data: {}, headers: { Authorization: 'Apikey wrong' } })).status()).toBe(401);
});
