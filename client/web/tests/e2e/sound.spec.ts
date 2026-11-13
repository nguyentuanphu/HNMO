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
