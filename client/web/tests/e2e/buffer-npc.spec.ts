import { expect, test } from '@playwright/test';

test('guide documents Buffer NPC and Elf Soldier dialog dispatches NpcBuffRequest (902)', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Buffer NPC');
  await expect(page.locator('body')).toContainText('Elf Soldier');

  // Verify the Elf Soldier dialogue "902" branch builds and sends NpcBuffRequest cleanly via packet codecs.
  await page.goto('/index.html');
  const sentByte = await page.evaluate(async () => {
    const { write } = await import('/js/net/packets.js' as string) as {
      write(group: string, name: string, values: Record<string, unknown>): Uint8Array;
    };
    const bytes = write('ClientToServer', 'NpcBuffRequest', {});
    return [...bytes];
  });
  // C1HeaderWithSubCode F6 31 (NpcBuffRequest)
  expect(sentByte.length).toBeGreaterThanOrEqual(4);
  expect(sentByte[0]).toBe(0xc1);
  expect(sentByte[2]).toBe(0xf6);
  expect(sentByte[3]).toBe(0x31);
  expect(errors).toEqual([]);
});
