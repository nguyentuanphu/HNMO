import assert from 'node:assert/strict';
import { test } from 'node:test';
import { canHandleKey, gameKey, installKeyboardShortcuts, matchesModifiers, optionKey } from '../public/js/game/keyboardShortcuts.js';

globalThis.document = { activeElement: null };
const event = (overrides = {}) => ({
  code: 'KeyB', key: 'b', altKey: false, ctrlKey: false, metaKey: false, shiftKey: false,
  getModifierState: () => false, composedPath: () => [], ...overrides,
});

for (const letter of 'ABEMOPUWZ') {
  test(`Option+${letter} follows code across layouts`, () => {
    const code = `Key${letter}`;
    for (const key of ['Dead', '∫', 'ø']) {
      const press = event({ code, key, altKey: true });
      assert.equal(optionKey(press, code), true);
      assert.equal(optionKey(press, 'KeyX'), false);
    }
  });
}

test('modifier matching rejects extra keys and handles macOS AltGraph', () => {
  const option = { altKey: true };
  for (const modifier of ['ctrlKey', 'metaKey', 'shiftKey']) {
    assert.equal(optionKey(event({ ...option, [modifier]: true }), 'KeyB'), false);
  }
  const graph = event({ ...option, getModifierState: (key) => key === 'AltGraph' });
  for (const platform of ['Win32', 'Linux x86_64']) assert.equal(matchesModifiers(graph, 'alt', platform), false);
  assert.equal(matchesModifiers(graph, 'alt', 'MacIntel'), true);
  assert.equal(matchesModifiers({ ...graph, ctrlKey: true }, 'alt', 'MacIntel'), false);
});

test('handled, repeated and composing events never activate shortcuts', () => {
  for (const blocked of [{ repeat: true }, { defaultPrevented: true }, { isComposing: true }, { keyCode: 229 }, { key: 'Process' }]) {
    assert.equal(canHandleKey(event(blocked)), false);
    assert.equal(optionKey(event({ altKey: true, ...blocked }), 'KeyB'), false);
  }
});

test('open chat reserves game shortcuts even after its text field loses focus', () => {
  globalThis.window = new EventTarget();
  let chatIsOpen = true;
  installKeyboardShortcuts(() => chatIsOpen);
  try {
    assert.equal(gameKey(event({ key: 'i' }), 'i'), false);
    assert.equal(optionKey(event({ altKey: true }), 'KeyB'), false);
    chatIsOpen = false;
    assert.equal(gameKey(event({ key: 'i' }), 'i'), true);
    assert.equal(optionKey(event({ altKey: true }), 'KeyB'), true);
  } finally {
    chatIsOpen = false;
    delete globalThis.window;
  }
});
