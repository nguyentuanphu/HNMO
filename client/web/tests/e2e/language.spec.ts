import { expect, test } from '@playwright/test';
import { enterWorld } from './helpers.js';

// English is the default; the picker switches the interface to Vietnamese and back, and the
// browser remembers the choice.
test('the login screen switches between English and Vietnamese', async ({ page }) => {
  await page.goto('/');
  const picker = page.locator('#loginLanguage');
  await expect(picker).toHaveValue('en');
  await expect(page.locator('label[for="username"]')).toHaveText('Account');
  await picker.selectOption('vi');
  await expect(page.locator('label[for="username"]')).toHaveText('Tài khoản');
  await expect(page.locator('label[for="password"]')).toHaveText('Mật khẩu');
  await expect(page.locator('#loginForm button.primary')).toHaveText('Kết nối');
  // Remembered after a reload.
  await page.reload();
  await expect(page.locator('#loginLanguage')).toHaveValue('vi');
  await expect(page.locator('label[for="username"]')).toHaveText('Tài khoản');
  // And back to English.
  await page.locator('#loginLanguage').selectOption('en');
  await expect(page.locator('label[for="username"]')).toHaveText('Account');
  await expect(page.locator('#loginForm button.primary')).toHaveText('Connect');
});

test('every language of the list can be chosen (German and Chinese)', async ({ page }) => {
  await page.goto('/');
  const picker = page.locator('#loginLanguage');
  await expect(picker.locator('option')).toHaveCount(13);
  await picker.selectOption('de');
  await expect(page.locator('label[for="username"]')).toHaveText('Konto');
  await expect(page.locator('label[for="password"]')).toHaveText('Passwort');
  await picker.selectOption('zh-TW');
  await expect(page.locator('label[for="username"]')).toHaveText('帳戶');
  await page.reload();
  await expect(page.locator('#loginLanguage')).toHaveValue('zh-TW');
  await expect(page.locator('label[for="password"]')).toHaveText('密碼');
});

test('game windows show Vietnamese, and players\' chat stays as typed', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  await enterWorld(page, 'test3');
  // Like the character test: the hero's stats are there before the window opens.
  await page.waitForFunction(() => (window as unknown as { __game: { hero?: { name?: string } } }).__game.hero?.name);
  await page.locator('select.language-select:not(#loginLanguage)').selectOption('vi');
  const window = page.locator('.rml-character_info');
  // The C key works once the game has set up its windows, a moment after the world shows.
  await expect(async () => {
    if (!(await window.count())) await page.keyboard.press('c');
    await expect(window).toHaveCount(1, { timeout: 5_000 });
  }).toPass({ timeout: 60_000 });
  // The character window: its stat names in Vietnamese (STR, AGI → Sức mạnh, Nhanh nhẹn).
  await expect(window.getByText('Sức mạnh').first()).toBeVisible({ timeout: 10_000 });
  await expect(window.getByText('Nhanh nhẹn').first()).toBeVisible();
  await page.screenshot({ path: 'test-results/language-vi.png' });
  // A chat line that happens to be an interface word isn't translated.
  await page.evaluate(() => {
    const log = document.querySelector('#chatLog')!;
    log.removeAttribute('hidden');
    const line = document.createElement('div');
    line.textContent = 'STR';
    log.append(line);
  });
  await page.waitForTimeout(300);
  await expect(page.locator('#chatLog div').last()).toHaveText('STR');
  // Back to English: the open window's texts come back.
  await page.locator('select.language-select:not(#loginLanguage)').selectOption('en');
  await expect(window.getByText('STR', { exact: true }).first()).toBeVisible({ timeout: 10_000 });
});
