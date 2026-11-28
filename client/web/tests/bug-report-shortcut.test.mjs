// Unit tests of the bug report shortcut (src/ui/bugReportWindow.ts): Alt+B on a PC and Option+B on a Mac, where the key
// gives "∫" (so the shortcut goes by the key's place, event.code), and never with Ctrl, Cmd or Shift.
// Run: node --test client/web/tests/*.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
let isBugReportShortcut;
try {
  ({ isBugReportShortcut } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/bugReportWindow.js')).href));
} catch {
  // The module draws with the browser's DOM only when used; if it can't load here, the test says so.
}

const skip = !isBugReportShortcut && 'bugReportWindow.js not loadable here';
const press = (code, mods = {}) => ({ code, altKey: false, ctrlKey: false, metaKey: false, shiftKey: false, ...mods });

test('Alt+B on a PC and Option+B on a Mac open the window', { skip }, () => {
  assert.equal(isBugReportShortcut(press('KeyB', { altKey: true })), true);
  // A Mac's Option+B gives key "∫" but the same code.
  assert.equal(isBugReportShortcut({ ...press('KeyB', { altKey: true }), key: '∫' }), true);
});

test('other keys and modifiers do not', { skip }, () => {
  assert.equal(isBugReportShortcut(press('KeyB')), false);
  assert.equal(isBugReportShortcut(press('KeyN', { altKey: true })), false);
  assert.equal(isBugReportShortcut(press('KeyB', { altKey: true, ctrlKey: true })), false);
  assert.equal(isBugReportShortcut(press('KeyB', { altKey: true, metaKey: true })), false);
  assert.equal(isBugReportShortcut(press('KeyB', { altKey: true, shiftKey: true })), false);
});
