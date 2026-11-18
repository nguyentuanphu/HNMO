import { expect, test } from '@playwright/test';

test('guide documents Banned Words Filter and chat word filter handles EN/VI cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Banned words filter');

  await page.goto('/index.html');
  const outputs = await page.evaluate(async () => {
    const { filterWords, setWordFilter } = await import('/js/game/chatExtras.js' as string) as {
      filterWords(text: string): string;
      setWordFilter(on: boolean): void;
    };
    setWordFilter(true);
    return {
      safeVi: filterWords('Classic các bạn lớn lên'),
      badMixed: filterWords('Hello fuck dkm'),
    };
  });

  expect(outputs.safeVi).toBe('Classic các bạn lớn lên');
  expect(outputs.badMixed).toBe('Hello **** ***');
  expect(errors).toEqual([]);
});
