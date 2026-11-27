// Unit tests for chat, whisper and messenger window resizing and per-browser localStorage persistence.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Provide a minimal localStorage & window stub before importing chat/chatExtras modules.
const store = new Map();
globalThis.localStorage = {
  getItem: (k) => (store.has(k) ? store.get(k) : null),
  setItem: (k, v) => { store.set(k, String(v)); },
  removeItem: (k) => { store.delete(k); },
  clear: () => { store.clear(); },
  key: () => null,
  get length() { return store.size; },
};
globalThis.window = {
  addEventListener: () => {},
  removeEventListener: () => {},
  setTimeout: () => 0,
  clearTimeout: () => {},
  innerWidth: 1280,
  innerHeight: 720,
  matchMedia: () => ({ matches: false }),
};
globalThis.document = { body: { append: () => {} }, querySelector: () => null };

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const extras = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/chatExtras.js')).href);
const chatMod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/chat.js')).href);

test('chat/whisper size defaults, clamps, and persists per browser in localStorage', () => {
  store.clear();
  assert.deepEqual(extras.loadChatSize(), { width: 281, lines: 6 });

  const chat = new chatMod.Chat();
  assert.deepEqual(chat.windowSize, { width: 281, lines: 6 });

  const updated = chat.setWindowSize(520, 14);
  assert.deepEqual(updated, { width: 520, lines: 14 });
  assert.deepEqual(extras.loadChatSize(), { width: 520, lines: 14 });

  // A new Chat instance (simulating reload in the same browser) restores the persisted size.
  const reloadedChat = new chatMod.Chat();
  assert.deepEqual(reloadedChat.windowSize, { width: 520, lines: 14 });

  // Extreme values clamp cleanly inside safe bounds.
  const clamped = chat.setWindowSize(9999, 1);
  assert.deepEqual(clamped, { width: 640, lines: 3 });
  assert.deepEqual(extras.loadChatSize(), { width: 640, lines: 3 });
});

test('messenger shell and drawer sizes persist independently per browser window key', () => {
  store.clear();
  const friendBounds = { minW: 290, maxW: 720, minH: 150, maxH: 560 };
  const letterBounds = { minW: 360, maxW: 760, minH: 182, maxH: 540 };
  const drawerBounds = { minW: 170, maxW: 560, minH: 46, maxH: 360 };

  assert.equal(extras.loadWindowSize('friend_shell', friendBounds), null);
  assert.deepEqual(
    extras.saveWindowSize('friend_shell', { width: 460, height: 320 }, friendBounds),
    { width: 460, height: 320 },
  );
  assert.deepEqual(
    extras.saveWindowSize('letter_read', { width: 540, height: 300 }, letterBounds),
    { width: 540, height: 300 },
  );
  assert.deepEqual(
    extras.saveWindowSize('letter_write', { width: 510, height: 280 }, letterBounds),
    { width: 510, height: 280 },
  );
  assert.deepEqual(
    extras.saveWindowSize('mobile_chat', { width: 320, height: 140 }, drawerBounds),
    { width: 320, height: 140 },
  );

  assert.deepEqual(extras.loadWindowSize('friend_shell', friendBounds), { width: 460, height: 320 });
  assert.deepEqual(extras.loadWindowSize('letter_read', letterBounds), { width: 540, height: 300 });
  assert.deepEqual(extras.loadWindowSize('letter_write', letterBounds), { width: 510, height: 280 });
  assert.deepEqual(extras.loadWindowSize('mobile_chat', drawerBounds), { width: 320, height: 140 });
});
