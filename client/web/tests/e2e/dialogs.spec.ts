// The game's small dialogs are the desktop's own documents (ui/gameDialogs.ts): a question in
// generic_confirm_dialog.rml answers OK / Cancel, a second one waits for the first, an empty text entry
// keeps its box open, Escape cancels; the chaos machine's method menu is generic_menu_dialog.rml
// (chaos-mix kind) and answers the button's index, Close / Escape answer null; the auto play extras
// open in the game's frame. Screenshots go to test-results/dialogs-*.png.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

type Answers = Window & { __answers: unknown[] };
const answers = (page: Page) => page.evaluate(() => (window as unknown as Answers).__answers);
const ask = (page: Page, code: string) => page.evaluate(`(window.__answers ??= [], void (${code}))`);

test('the dialogs are the desktop documents and answer like them', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test7');
  await page.waitForSelector('.rml-main_frame', { state: 'attached', timeout: 120_000 });
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());

  // Two questions: the second waits for the first.
  await ask(page, `import('/js/ui/confirmDialog.js').then((m) => { m.confirmInGame('First question?').then((a) => window.__answers.push(a)); m.confirmInGame('Second question?').then((a) => window.__answers.push(a)); })`);
  const panel = page.locator('.confirm-dialog #panel');
  await expect(panel).toContainText('First question?');
  await expect(page.locator('.confirm-dialog #gcd_btn_primary')).toHaveClass(/stock-ok/);
  await page.screenshot({ path: 'test-results/dialogs-confirm.png' });
  await page.click('.confirm-dialog .confirm-ok');
  await expect(panel).toContainText('Second question?');
  await page.click('.confirm-dialog .confirm-cancel');
  await expect(page.locator('.confirm-dialog')).toHaveCount(0);
  expect(await answers(page)).toEqual([true, false]);

  // A text entry: empty keeps the box open, a name answers it; Escape cancels the next.
  await ask(page, `import('/js/ui/socialDialogs.js').then((m) => { m.promptInGame('Name?', { maxLength: 10 }).then((a) => window.__answers.push(a)); m.promptInGame('Again?', { maxLength: 10 }).then((a) => window.__answers.push(a)); })`);
  await expect(panel).toContainText('Name?');
  await page.click('.confirm-dialog .confirm-ok');
  await expect(panel).toContainText('Name?');
  await page.fill('.confirm-dialog .social-dialog-input', 'test1Dk');
  await page.keyboard.press('Enter');
  await expect(panel).toContainText('Again?');
  await page.keyboard.press('Escape');
  await expect(page.locator('.confirm-dialog')).toHaveCount(0);
  expect((await answers(page)).slice(2)).toEqual(['test1Dk', null]);

  // A title and Yes / No.
  await ask(page, `import('/js/ui/socialDialogs.js').then((m) => m.askInGame('Accept?').then((a) => window.__answers.push(a)))`);
  await expect(page.locator('.confirm-dialog #gcd_btn_primary')).toHaveText('Yes');
  await expect(page.locator('.confirm-dialog #gcd_btn_cancel')).toHaveText('No');
  await page.click('.confirm-dialog .confirm-ok');
  await ask(page, `import('/js/ui/cashShop.js').then((m) => m.messageInGame('Purchase Failed', 'This item has sold out.').then(() => window.__answers.push('ok')))`);
  await expect(page.locator('.message-dialog .message-title')).toHaveText('Purchase Failed');
  await page.click('.message-dialog .confirm-ok');
  await expect.poll(() => answers(page).then((a) => a.slice(4))).toEqual([true, 'ok']);

  // The chaos machine's method menu: the box of its kind, a button's index; Close answers null.
  const menu = `import('/js/ui/mixWindow.js').then((m) => m.chooseInGame('Select method of combination', [{ label: 'Regular Combination', lines: ['Wings, 7 types of Fruit'] }, { label: 'Chaos Weapon Combination', lines: ['Chaos Nature Bow'] }, { label: 'Item Option Combination', lines: ['Add 380 Item Option'] }], 'Close').then((a) => window.__answers.push(a)))`;
  await ask(page, menu);
  await expect(page.locator('.mix-method-dialog #panel[data-kind="chaos-mix"]')).toBeVisible();
  await expect(page.locator('.mix-method-dialog .window-shell-header-title')).toHaveText('Select method of combination');
  await page.screenshot({ path: 'test-results/dialogs-menu.png' });
  await page.click('.mix-method-dialog .choose-option-1');
  await expect(page.locator('.mix-method-dialog')).toHaveCount(0);
  await ask(page, menu);
  await expect(page.locator('.mix-method-dialog #panel')).toBeVisible();
  await page.click('.mix-method-dialog .choose-close');
  await expect(page.locator('.mix-method-dialog')).toHaveCount(0);
  await ask(page, menu);
  await expect(page.locator('.mix-method-dialog #panel')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.locator('.mix-method-dialog')).toHaveCount(0);
  await expect.poll(() => answers(page).then((a) => a.slice(6))).toEqual([1, null, null]);

  // The auto play extras: the game's frame, a checkbox and Close.
  await page.keyboard.press('Shift+Z');
  const extras = page.locator('.auto-play-extras #panel');
  await expect(extras).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.auto-play-extras .checkbox-box:visible')).toHaveCount(5);
  await page.screenshot({ path: 'test-results/dialogs-extras.png' });
  await page.click('.auto-play-extras .ape-close');
  await expect(page.locator('.auto-play-extras')).toHaveCount(0);
  await expectNoPageErrors(errors);
});
