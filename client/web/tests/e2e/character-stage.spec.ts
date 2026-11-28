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
  // Each character's name, level and class over it; the desktop's plates; the HNMO mark; no list box.
  await expect(page.locator('.stage-label')).toHaveCount(slots.length);
  await expect(page.locator(`.stage-label[data-slot="${slot}"]`)).toHaveClass(/picked/);
  await expect(page.locator('.stage-label').first()).toContainText(/Lv \d+/);
  await expect(page.locator('#stageBrand')).toHaveText('HNMO');
  await expect(page.locator('#btn_create')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('#btn_connect')).toBeVisible();
  expect((await page.locator('#characters .choice').first().boundingBox())?.width ?? 0).toBeLessThanOrEqual(2);
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

test('a picked character is deleted after a yes and the account\'s code; a wrong code is refused', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  await toCharacters(page, 'test6');
  await expect(page.locator('body.character-stage')).toHaveCount(1, { timeout: 120_000 });
  const create = page.locator('#characters .choice', { hasText: 'Create a character' });
  test.skip(await create.count() === 0, 'test6 has five characters already');
  // A character of this test only, so the others' characters stay.
  const name = `Del${Date.now() % 1_000_000}`;
  await create.click();
  await page.fill('#newName', name);
  await page.click('#createIt');
  const button = page.locator('#characters .choice', { hasText: name });
  await expect(button).toHaveCount(1, { timeout: 30_000 });
  const slot = Number(await button.getAttribute('data-slot'));
  await expect.poll(() => page.evaluate((s) => (window as unknown as Win).__game.characterStage?.slots.includes(s) ?? false, slot), { timeout: 60_000 }).toBe(true);

  const pick = async () => {
    await page.waitForTimeout(500);
    const point = await page.evaluate((s) => (window as unknown as Win).__game.characterStage?.screenPointOf(s) ?? null, slot);
    expect(point).not.toBeNull();
    await page.mouse.click(point!.x, point!.y);
    await expect(page.locator(`#characters .choice[data-slot="${slot}"]`)).toHaveClass(/selected/);
  };
  await pick();
  await page.locator('#btn_delete').click();
  await expect(page.locator('#deleteQuestion')).toContainText(name);
  await page.click('#deleteYes');
  await page.fill('#deleteCode', 'not-the-password');
  await page.click('#deleteOk');
  await expect(page.locator('#loginError')).toContainText(/wrong|không đúng/i, { timeout: 30_000 });
  await expect(page.locator('#characters .choice', { hasText: name })).toHaveCount(1);

  await pick();
  await page.locator('#btn_delete').click();
  await page.click('#deleteYes');
  await page.fill('#deleteCode', 'test6');
  await page.screenshot({ path: 'test-results/character-stage-delete.png' });
  await page.click('#deleteOk');
  await expect(page.locator('#characters .choice', { hasText: name })).toHaveCount(0, { timeout: 30_000 });
  await expect.poll(() => page.evaluate((s) => (window as unknown as Win).__game.characterStage?.slots.includes(s) ?? true, slot), { timeout: 30_000 }).toBe(false);
});

test('Create shows the chosen class in 3D where the new character will stand; locked classes are greyed out', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  await toCharacters(page, 'test7');
  await expect(page.locator('body.character-stage')).toHaveCount(1, { timeout: 120_000 });
  const create = page.locator('#btn_create');
  test.skip(await page.locator('#characters .choice[data-create]').count() === 0, 'test7 has five characters already');
  await expect(create).toBeVisible({ timeout: 30_000 });
  await create.click();
  await expect(page.locator('#newClass')).toBeVisible();
  const preview = page.locator('.stage-label[data-preview]');
  await expect(preview).toBeVisible({ timeout: 60_000 });
  const firstClass = await page.locator('#newClass option:checked').textContent();
  await expect(preview).toContainText(firstClass!.trim());
  // Another class: the preview follows.
  await page.selectOption('#newClass', '2');
  await expect(preview).toContainText('Fairy Elf', { timeout: 60_000 });
  await page.waitForTimeout(1500);
  await page.screenshot({ path: 'test-results/character-stage-create.png' });
  // Locked classes (by the account's unlock flags) can't be picked.
  const disabled = await page.locator('#newClass option[disabled]').allTextContents();
  for (const text of disabled) expect(text).toMatch(/locked|chưa mở/);
  // Back: the preview goes.
  await page.click('#cancelIt');
  await expect(preview).toHaveCount(0, { timeout: 30_000 });
  await expect(page.locator('#btn_create')).toBeVisible();
});

test('the list buttons still enter at once, as every other spec does', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  await toCharacters(page, 'test4');
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as Win).__viewerReady, null, { timeout: 180_000 });
  expect(await page.evaluate(() => (window as unknown as Win).__game.mapNumber)).not.toBeNull();
});
