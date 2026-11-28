import { readFile } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

type Shortcuts = typeof import('../../src/game/keyboardShortcuts.js');
type TestWindow = Window & {
  openChat: boolean;
  alt: ReturnType<Shortcuts['trackAltNames']>;
  optionB: number;
  enter: number;
  minimap: number;
};

test.beforeEach(async ({ page }) => {
  const script = await readFile(new URL('../../public/js/game/keyboardShortcuts.js', import.meta.url), 'utf8');
  await page.setContent('<main><input><textarea></textarea><select><option>First</option></select><div contenteditable>Editor</div><div role="textbox" tabindex="0">Role</div><div id="shadow"></div><button>Button</button></main>');
  await page.addScriptTag({ type: 'module', content: `${script}
    window.openChat = false;
    installKeyboardShortcuts(() => window.openChat);
    window.alt = trackAltNames();
    window.optionB = 0;
    window.enter = 0;
    window.minimap = 0;
    window.addEventListener('keydown', (event) => {
      if (optionKey(event, 'KeyB')) window.optionB++;
      if (panelKey(event, 'Enter')) window.enter++;
      if (gameKey(event, 'Tab') && !hasKeyboardControl(event)) {
        event.preventDefault();
        window.minimap++;
      }
    });
    const shadow = document.querySelector('#shadow').attachShadow({ mode: 'open' });
    shadow.innerHTML = '<div></div>';
    shadow.firstElementChild.attachShadow({ mode: 'open' }).innerHTML = '<input aria-label="Shadow">';
  ` });
  await page.waitForFunction(() => (window as unknown as TestWindow).alt !== undefined);
});

test('editors, nested shadow roots, and chat retain game keys', async ({ page }) => {
  for (const editor of ['main > input', 'textarea', 'select', '[contenteditable]', '[role=textbox]', '[aria-label=Shadow]']) {
    await page.locator(editor).focus();
    await page.keyboard.press('Alt+b');
  }
  await page.evaluate(() => {
    const w = window as unknown as TestWindow;
    document.querySelector('button')!.focus();
    w.openChat = true;
  });
  await page.keyboard.press('Alt+b');
  expect(await page.evaluate(() => (window as unknown as TestWindow).optionB)).toBe(0);
  await page.evaluate(() => { (window as unknown as TestWindow).openChat = false; });
  await page.keyboard.press('Alt+b');
  expect(await page.evaluate(() => (window as unknown as TestWindow).optionB)).toBe(1);
});

test('Alt tap toggles labels, chords and clicks only hold them', async ({ page }) => {
  const labels = () => page.evaluate(() => {
    const { held, toggled } = (window as unknown as TestWindow).alt;
    return [held, toggled];
  });
  await page.keyboard.down('Alt');
  await expect.poll(labels).toEqual([true, false]);
  await page.keyboard.up('Alt');
  await expect.poll(labels).toEqual([false, true]);
  await page.keyboard.press('Alt+b');
  await expect.poll(labels).toEqual([false, true]);
  await page.keyboard.down('Alt');
  await page.mouse.click(300, 200);
  await page.keyboard.up('Alt');
  await expect.poll(labels).toEqual([false, true]);
  await page.keyboard.down('Alt');
  await page.locator('input').first().focus();
  await expect.poll(labels).toEqual([false, true]);
  await page.keyboard.up('Alt');
});

test('IME composition and native button activation do not confirm a dialog', async ({ page }) => {
  await page.evaluate(() => {
    const field = document.querySelector('input')!;
    field.focus();
    field.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true, composed: true }));
  });
  await page.keyboard.press('Enter');
  await page.keyboard.press('Escape');
  expect(await page.evaluate(() => (window as unknown as TestWindow).enter)).toBe(0);
  await page.evaluate(() => document.querySelector('input')!.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true, composed: true })));
  await page.keyboard.press('Enter');
  expect(await page.evaluate(() => (window as unknown as TestWindow).enter)).toBe(1);
  await page.locator('button').focus();
  await page.keyboard.press('Enter');
  expect(await page.evaluate(() => (window as unknown as TestWindow).enter)).toBe(1);
});

test('Tab belongs to the game area and keeps native control navigation', async ({ page }) => {
  await page.mouse.click(300, 200);
  await page.keyboard.press('Tab');
  expect(await page.evaluate(() => (window as unknown as TestWindow).minimap)).toBe(1);
  await page.locator('button').focus();
  await page.keyboard.press('Shift+Tab');
  await expect(page.locator('button')).not.toBeFocused();
  await page.locator('input').first().focus();
  await page.keyboard.press('Tab');
  await expect(page.locator('textarea')).toBeFocused();
  expect(await page.evaluate(() => (window as unknown as TestWindow).minimap)).toBe(1);
});
