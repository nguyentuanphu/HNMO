// Sound and music like the desktop's: the map's music plays (Lorencia's town: its main theme), and
// the sound button mutes everything.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface AudioView { currentMusic: string | null; getSettings(): { muted: boolean } }

test('the map music plays and the sound button mutes it', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test5');
  await page.evaluate(() => (window as unknown as { __game: { online: { say(text: string): void } } }).__game.online.say('/move lorencia'));
  const music = () => page.evaluate(async () => {
    const audio = (await import('/js/audio/index.js' as string)) as { audioSystem(): AudioView };
    return audio.audioSystem().currentMusic;
  });
  await expect.poll(music, { timeout: 60_000 }).toMatch(/main_theme|pub/i);
  await page.click('#soundButton');
  await expect(page.locator('#soundButton')).toHaveAttribute('aria-pressed', 'true');
  await page.click('#soundButton');
  await expect(page.locator('#soundButton')).toHaveAttribute('aria-pressed', 'false');
  await expectNoPageErrors(errors);
});

// Owner's report (2026-11-21): on a phone the music went on after leaving for the home screen. Out of sight (another
// tab here, as a minimised window or the home screen), the sound pauses and the game stays connected; seen again, it
// plays on.
test('the sound pauses while the game is out of sight', async ({ page, context }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test6');
  // In Lorencia's town, where its music plays (another test may have left the hero out in a field).
  await page.evaluate(() => (window as unknown as { __game: { online: { say(text: string): void } } }).__game.online.say('/move lorencia'));
  await page.mouse.click(5, 5); // a gesture lets the browser play sound
  const state = () => page.evaluate(async () => {
    const audio = (await import('/js/audio/index.js' as string)) as { audioSystem(): { isMusicPlaying: boolean; isHiddenPaused: boolean } };
    const system = audio.audioSystem();
    return { playing: system.isMusicPlaying, paused: system.isHiddenPaused, hidden: document.visibilityState };
  });
  await expect.poll(async () => (await state()).playing, { timeout: 60_000 }).toBe(true);
  const other = await context.newPage();
  await other.bringToFront();
  await page.evaluate(() => {
    // Headless Chromium keeps every tab "visible": say what a phone or a minimised window says.
    Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await expect.poll(state, { timeout: 60_000 }).toEqual({ playing: false, paused: true, hidden: 'hidden' });
  await page.evaluate(() => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'visible' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await page.bringToFront();
  await expect.poll(state, { timeout: 60_000 }).toEqual({ playing: true, paused: false, hidden: 'visible' });
  await other.close();
  await expectNoPageErrors(errors);
});
