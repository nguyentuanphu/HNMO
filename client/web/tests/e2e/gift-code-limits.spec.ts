import { test, expect } from '@playwright/test';
import { fileURLToPath } from 'node:url';

const messages = {
  en: ['This code was already used from this network.', 'This code was already used on this device.', 'Your network address is unavailable. Reconnect and try again.'],
  vi: ['Gift code này đã được dùng từ mạng này.', 'Gift code này đã được dùng trên thiết bị này.', 'Chưa xác định được địa chỉ mạng. Hãy kết nối lại rồi thử lại.'],
};

for (const language of ['en', 'vi'] as const) {
  test(`website identity refusals and persistent device id (${language})`, async ({ page }) => {
    const calls: Record<string, string>[] = [];
    let result = 'AlreadyUsedNetwork';
    await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
    await page.route('**/website/gift-codes', route => route.fulfill({ json: { enabled: true, website: true, codes: [] } }));
    await page.route('**/portal/gift-codes/characters', route => route.fulfill({
      json: { loginName: 'ann', characters: [{ name: 'Ann', level: 50, resets: 0 }] },
    }));
    await page.route('**/portal/gift-codes/redeem', route => {
      calls.push(route.request().postDataJSON() as Record<string, string>);
      return route.fulfill({ json: { result, character: 'Ann' } });
    });
    await page.goto('/account.html#gift');
    await page.fill('#giftAccount', 'ann');
    await page.fill('#giftPassword', 'secret');
    await page.locator('#giftLogin button').click();
    await expect(page.locator('#giftForm')).toBeVisible();
    await page.fill('#giftCode', 'GIFT');
    await page.locator('#giftForm button').click();
    await expect(page.locator('#giftMessage')).toHaveText(messages[language][0]);
    result = 'AlreadyUsedDevice';
    await page.locator('#giftForm button').click();
    await expect(page.locator('#giftMessage')).toHaveText(messages[language][1]);
    result = 'NetworkAddressUnknown';
    await page.locator('#giftForm button').click();
    await expect(page.locator('#giftMessage')).toHaveText(messages[language][2]);
    expect(calls[0].deviceId).toMatch(/^[0-9a-f-]{36}$/i);
    expect(calls[1].deviceId).toBe(calls[0].deviceId);
    await page.reload();
    const saved = await page.evaluate(async () => {
      const path = '/js/deviceId.js';
      const { deviceId } = await import(path) as typeof import('../../src/deviceId.js');
      return deviceId();
    });
    expect(saved).toBe(calls[0].deviceId);
    await expect(page.locator('#gift [data-gt="intro"]')).toContainText(language === 'vi' ? 'mỗi mạng' : 'per network');
  });
}

test('game shares the website device id and sends the extended packet with bilingual refusals', async ({ page }) => {
  await page.route('**/gift-code-check', route => route.fulfill({
    contentType: 'text/html',
    body: '<!doctype html><html><head><script type="importmap">{"imports":{"three":"/vendor/three/build/three.module.js","three/":"/vendor/three/"}}</script></head><body></body></html>',
  }));
  await page.route('**/vendor/three/build/three.module.js', route => route.fulfill({
    path: fileURLToPath(new URL('../../node_modules/three/build/three.module.js', import.meta.url)),
    contentType: 'text/javascript',
  }));
  await page.goto('/gift-code-check');
  const result = await page.evaluate(async () => {
    const idPath = '/js/deviceId.js', windowPath = '/js/ui/giftCodesWindow.js';
    const i18nPath = '/js/i18n.js', packetPath = '/js/net/packets.js';
    const { deviceId } = await import(idPath) as typeof import('../../src/deviceId.js');
    const { GiftCodesWindow } = await import(windowPath) as typeof import('../../src/ui/giftCodesWindow.js');
    const { setLanguage } = await import(i18nPath) as typeof import('../../src/i18n.js');
    const { write, read } = await import(packetPath) as typeof import('../../src/net/packets.js');
    const id = deviceId();
    const sent: { name: string; bytes: number[]; decoded: Record<string, unknown> }[] = [];
    const panel = new GiftCodesWindow({
      send: (name, fields) => {
        const bytes = write('ClientToServer', name, fields);
        sent.push({ name, bytes: [...bytes], decoded: read('ClientToServer', name, bytes) });
      },
      message: () => undefined, itemName: () => '',
    });
    const commandAccepted = panel.command('/CoDe GIFT');
    const unrelatedCommand = panel.command('/party');
    panel.redeem(' GIFT ');
    const texts: string[][] = [];
    for (const language of ['en', 'vi']) {
      await setLanguage(language);
      texts.push(['AlreadyUsedNetwork', 'AlreadyUsedDevice', 'NetworkAddressUnknown'].map(value => panel.resultText({ type: 'redeem', result: value })));
    }
    Object.defineProperty(window, 'localStorage', { get: () => { throw new DOMException('blocked'); } });
    return { id, sent, texts, commandAccepted, unrelatedCommand, unknown: deviceId() };
  });
  expect(result.commandAccepted).toBe(true);
  expect(result.unrelatedCommand).toBe(false);
  expect(result.sent).toHaveLength(2);
  expect(result.sent[1]).toEqual(result.sent[0]);
  expect(result.sent[0].name).toBe('HnmoGiftCodeRedeemExtended');
  expect(result.sent[0].bytes.slice(0, 4)).toEqual([0xc1, 72, 0xf5, 0x23]);
  expect(result.sent[0].bytes).toHaveLength(72);
  expect(result.sent[0].decoded).toEqual({ GiftCode: 'GIFT', DeviceId: result.id });
  expect(result.texts).toEqual([messages.en, messages.vi]);
  expect(result.unknown).toBe('');
});
