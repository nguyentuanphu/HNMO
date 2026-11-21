import { test, expect } from '@playwright/test';

interface LanguageModule {
  setLanguage(language: string): Promise<void>;
  translate(english: string): string | null;
}

test('Vietnamese buff durations and browser dialog messages retain values and restore English', async ({ page }) => {
  await page.route('**/buff-language-check', route => route.fulfill({
    contentType: 'text/html',
    body: '<!doctype html><html><body><p>Duration period: 1 hour 2 min 3 sec</p><p>Duration period: 2 min 3 sec</p><p>Duration period: 3 sec</p></body></html>',
  }));
  await page.goto('/buff-language-check');
  const messages = await page.evaluate(async () => {
    const modulePath = '/js/i18n.js';
    const i18n = await import(modulePath) as LanguageModule;
    await i18n.setLanguage('vi');
    return [
      'Do you want to cancel the Phòng hộ thép skill?',
      "Would you like to repair Fenrir's horn?",
      'An expensive item. Check the item, please. Are you sure you want to sell it?',
      'How much zen to deposit?',
      'How much zen to withdraw?',
      'Enter the amount of Zen you would like to trade.',
    ].map(message => i18n.translate(message));
  });
  await expect(page.locator('p')).toHaveText([
    'Thời gian duy trì: 1 giờ 2 phút 3 giây',
    'Thời gian duy trì: 2 phút 3 giây',
    'Thời gian duy trì: 3 giây',
  ]);
  expect(messages[0]).toBe('Bạn có muốn hủy hiệu ứng kỹ năng Phòng hộ thép không?');
  expect(messages.every(message => message !== null)).toBe(true);
  await page.evaluate(async () => {
    const modulePath = '/js/i18n.js';
    const i18n = await import(modulePath) as LanguageModule;
    await i18n.setLanguage('en');
  });
  await expect(page.locator('p')).toHaveText([
    'Duration period: 1 hour 2 min 3 sec',
    'Duration period: 2 min 3 sec',
    'Duration period: 3 sec',
  ]);
});
