import { expect, test } from '@playwright/test';

test('guide documents Player Rankings (Shift+R) and Add Stats (Shift+A) and packet codecs round-trip F5 46 / F5 47', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Shift+R');
  await expect(page.locator('body')).toContainText('Shift+A');

  await page.goto('/index.html');
  const encoded = await page.evaluate(async () => {
    const { write, read } = await import('/js/net/packets.js' as string) as {
      write(group: string, name: string, values: Record<string, unknown>): Uint8Array;
      read<T>(group: string, name: string, bytes: Uint8Array): T;
    };
    const req = write('ClientToServer', 'HnmoRankingCommand', { Action: 1, Payload: '{"str":50}' });
    const stateBytes = write('ServerToClient', 'HnmoRankingState', { Json: '{"enableRankings":true,"characters":[]}' });
    const parsed = read<{ Json: string }>('ServerToClient', 'HnmoRankingState', stateBytes);
    return { req: [...req], stateHeader: [...stateBytes.slice(0, 5)], json: parsed.Json };
  });

  expect(encoded.req[0]).toBe(0xc1);
  expect(encoded.req[2]).toBe(0xf5);
  expect(encoded.req[3]).toBe(0x46);
  expect(encoded.stateHeader[0]).toBe(0xc2);
  expect(encoded.stateHeader[3]).toBe(0xf5);
  expect(encoded.stateHeader[4]).toBe(0x47);
  expect(JSON.parse(encoded.json)).toEqual({ enableRankings: true, characters: [] });
  expect(errors).toEqual([]);
});
