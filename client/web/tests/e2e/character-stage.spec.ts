// The character choice as a 3D stage (the desktop's CharacterScene, PARITY.md section 1): world 74 loads behind the list,
// the account's characters stand on their places, a click on one picks it (its button is marked), and a double click
// on it enters the game. The list's buttons keep working as before (the other specs use them).
import { test, expect, type Page } from '@playwright/test';

interface StageView { slots: number[]; selectedSlot: number | null; screenPointOf(slot: number): { x: number; y: number } | null }
type Win = Window & { __game: { characterStage?: StageView | null; mapNumber: number | null }; __viewerReady?: boolean };

async function toCharacters(page: Page, account: string): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('#servers .choice >> nth=0')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
}

test('the characters stand on the 3D stage; a click picks one, a double click enters', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => { if (message.text().includes('Character stage')) errors.push(message.text()); });
  await toCharacters(page, 'test3');

  await expect(page.locator('body.character-stage')).toHaveCount(1, { timeout: 120_000 });
  const slots = await page.evaluate(() => (window as unknown as Win).__game.characterStage?.slots ?? []);
  expect(slots.length).toBeGreaterThan(0);
  // The first character of the list is picked at once; its button is marked.
  const slot = await page.evaluate(() => (window as unknown as Win).__game.characterStage?.selectedSlot ?? -1);
  expect(slots).toContain(slot);
  await expect(page.locator(`#characters .choice[data-slot="${slot}"]`)).toHaveClass(/selected/);
  await page.waitForTimeout(1500);
  await page.screenshot({ path: 'test-results/character-stage.png' });

  // A click on the character in the world picks it.
  await page.evaluate(() => (window as unknown as Win).__game.characterStage && ((window as unknown as Win).__game.characterStage as unknown as { select(s: number | null): void }).select(null));
  const point = await page.evaluate((s) => (window as unknown as Win).__game.characterStage?.screenPointOf(s) ?? null, slot);
  expect(point).not.toBeNull();
  await page.mouse.click(point!.x, point!.y);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.characterStage?.selectedSlot ?? null)).toBe(slot);
  await expect(page.locator(`#characters .choice[data-slot="${slot}"]`)).toHaveClass(/selected/);

  // A double click on it enters the game; the stage is put away.
  await page.mouse.dblclick(point!.x, point!.y);
  await page.waitForFunction(() => (window as unknown as Win).__viewerReady, null, { timeout: 180_000 });
  expect(await page.evaluate(() => (window as unknown as Win).__game.characterStage ?? null)).toBeNull();
  await expect(page.locator('body.character-stage')).toHaveCount(0);
  expect(errors).toEqual([]);
});

test('the list buttons still enter at once, as every other spec does', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  await toCharacters(page, 'test4');
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as Win).__viewerReady, null, { timeout: 180_000 });
  expect(await page.evaluate(() => (window as unknown as Win).__game.mapNumber)).not.toBeNull();
});
