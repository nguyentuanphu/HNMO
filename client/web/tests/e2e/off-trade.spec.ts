import { expect, test } from '@playwright/test';

test('guide documents Offline trade (/offtrade) and client encodes /offtrade chat packet cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('/offtrade');

  await page.goto('/index.html');
  const bytes = await page.evaluate(async () => {
    const { write } = await import('/js/net/packets.js' as string) as {
      write(group: string, name: string, values: Record<string, unknown>): Uint8Array;
    };
    return [...write('ClientToServer', 'PublicChatMessage', { Character: 'Hero', Message: '/offtrade' })];
  });

  expect(bytes.length).toBeGreaterThanOrEqual(13);
  expect(bytes[0]).toBe(0xc1);
  expect(bytes[2]).toBe(0x00);
  expect(errors).toEqual([]);
});
