import test from 'node:test';
import assert from 'node:assert/strict';

test('AudioSystem pauses on setHidden(true) and resumes on setHidden(false) and window focus', async () => {
  const listeners = new Map();
  let playCalls = 0;
  let pauseCalls = 0;
  let failNextPlay = false;

  class FakeAudio {
    constructor() {
      this.loop = false;
      this.preload = 'none';
      this.src = '';
      this.volume = 1;
      this.muted = false;
      this.paused = true;
    }
    addEventListener() {}
    removeAttribute() { this.src = ''; }
    load() {}
    canPlayType() { return ''; }
    play() {
      playCalls++;
      if (failNextPlay) {
        failNextPlay = false;
        this.paused = true;
        return Promise.reject(new Error('NotAllowedError'));
      }
      this.paused = false;
      return Promise.resolve();
    }
    pause() {
      pauseCalls++;
      this.paused = true;
    }
  }

  globalThis.Audio = FakeAudio;
  globalThis.window = {
    addEventListener(type, fn) {
      if (!listeners.has(type)) listeners.set(type, []);
      listeners.get(type).push(fn);
    },
    removeEventListener() {},
  };
  globalThis.document = {
    visibilityState: 'visible',
    addEventListener(type, fn) {
      if (!listeners.has(type)) listeners.set(type, []);
      listeners.get(type).push(fn);
    },
  };

  const { AudioSystem } = await import('../public/js/audio/audioSystem.js');
  const system = new AudioSystem();
  system.unlock();
  system.playMusic('Data/Music/main_theme.mp3');
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(system.isMusicPlaying, true);

  // Out of sight: pauses
  system.setHidden(true);
  assert.equal(system.isHiddenPaused, true);
  assert.equal(system.isMusicPlaying, false);
  assert.equal(pauseCalls, 1);

  // Visibility returns while unfocused so first play() fails; focus later resumes it
  failNextPlay = true;
  system.setHidden(false);
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(system.isHiddenPaused, false);
  assert.equal(system.isMusicPlaying, false);

  for (const fn of listeners.get('focus') ?? []) fn();
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(system.isMusicPlaying, true);
  assert.ok(playCalls >= 3);
});
