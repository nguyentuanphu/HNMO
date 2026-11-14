// The packaged folder keeps its sounds as Opus (Data/Audio/Opus.json, package-hnmo.sh): the game then
// asks for the .opus names and plays them; without that file it asks for the original names.
import { readFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';

const OPUS = readFileSync(new URL('../fixtures/eBow.opus', import.meta.url));

async function loadSound(page: import('@playwright/test').Page): Promise<boolean> {
  return page.evaluate(async () => {
    const audio = (await import('/js/audio/audioSystem.js' as string)) as { AudioSystem: new () => { preload(ids: string[]): Promise<void> } };
    const system = new audio.AudioSystem();
    await system.preload(['Data/Sound/eBow.wav']);
    const sounds = (system as unknown as { sounds: Map<string, unknown> }).sounds;
    return [...sounds.values()].some((sound) => sound !== null);
  });
}

test('with Opus.json the game loads and decodes the .opus sound', async ({ page }) => {
  const asked: string[] = [];
  await page.route('**/data/Audio/Opus.json', (route) => route.fulfill({ json: { opus: true } }));
  await page.route('**/*.opus', (route) => { asked.push(new URL(route.request().url()).pathname); return route.fulfill({ body: OPUS, contentType: 'audio/ogg' }); });
  await page.goto('/');
  expect(await loadSound(page)).toBe(true);
  expect(asked).toContain('/data/Sound/eBow.opus');
});

test('without Opus.json the game asks for the original file', async ({ page }) => {
  const asked: string[] = [];
  page.on('request', (request) => { if (/\.(opus|wav)$/i.test(request.url())) asked.push(new URL(request.url()).pathname); });
  await page.goto('/');
  expect(await loadSound(page)).toBe(true);
  expect(asked).toContain('/data/Sound/eBow.wav');
  expect(asked.some((url) => url.endsWith('.opus'))).toBe(false);
});
