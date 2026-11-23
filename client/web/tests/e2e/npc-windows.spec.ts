// NpcWindowResponse (C3 30) kinds 0x07, 0x09, 0x11, 0x17, 0x18, 0x19, 0x25, 0x26 and Golden Archer (C1 94).
import { test, expect } from '@playwright/test';
import { dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

interface WinGame {
  connection: {
    send(group: string, name: string, fields: Record<string, unknown>): void;
    dispatch(bytes: Uint8Array): void;
  };
  inventory: { openWindow: number | null };
  itemWindows: { closeAll(): void };
}

test('NpcWindowResponse (C3 30) kinds and Golden Archer (C1 94) open dialogs and close cleanly', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test0');
  await dismissOnboarding(page);

  await page.evaluate(() => {
    const g = (window as unknown as { __game: WinGame }).__game;
    const w = window as unknown as { __sent: string[] };
    w.__sent = [];
    const orig = g.connection.send.bind(g.connection);
    g.connection.send = (group, name, fields) => {
      w.__sent.push(name);
      return orig(group, name, fields);
    };
  });

  // 0x07 Pet Trainer -> opens MixWindow and closes with CloseNpcRequest
  await page.evaluate(() => (window as unknown as { __game: WinGame }).__game.connection.dispatch(new Uint8Array([0xc3, 0x04, 0x30, 0x07])));
  await expect(page.locator('.rml-mix_inventory #panel')).toBeVisible({ timeout: 30_000 });
  await page.evaluate(() => (window as unknown as { __game: WinGame }).__game.itemWindows.closeAll());
  await expect(page.locator('.rml-mix_inventory')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent)).toContain('CloseNpcRequest');

  // 0x11 Elphis -> opens MixWindow and closes with CraftingDialogCloseRequest
  await page.evaluate(() => { (window as unknown as { __sent: string[] }).__sent.length = 0; (window as unknown as { __game: WinGame }).__game.connection.dispatch(new Uint8Array([0xc3, 0x04, 0x30, 0x11])); });
  await expect(page.locator('.rml-mix_inventory #panel')).toBeVisible({ timeout: 30_000 });
  await page.evaluate(() => (window as unknown as { __game: WinGame }).__game.itemWindows.closeAll());
  await expect(page.locator('.rml-mix_inventory')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent)).toContain('CraftingDialogCloseRequest');

  // 0x17 Seed Master -> choice menu -> cancel sends CloseNpcRequest
  await page.evaluate(() => { (window as unknown as { __sent: string[] }).__sent.length = 0; (window as unknown as { __game: WinGame }).__game.connection.dispatch(new Uint8Array([0xc3, 0x04, 0x30, 0x17])); });
  await expect(page.locator('.rml-generic_menu_dialog #panel')).toBeVisible({ timeout: 30_000 });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-generic_menu_dialog')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent)).toContain('CloseNpcRequest');

  // 0x18 Seed Researcher -> choice menu -> cancel sends CloseNpcRequest
  await page.evaluate(() => { (window as unknown as { __sent: string[] }).__sent.length = 0; (window as unknown as { __game: WinGame }).__game.connection.dispatch(new Uint8Array([0xc3, 0x04, 0x30, 0x18])); });
  await expect(page.locator('.rml-generic_menu_dialog #panel')).toBeVisible({ timeout: 30_000 });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-generic_menu_dialog')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent)).toContain('CloseNpcRequest');

  // 0x09 Lahap -> choice menu -> cancel sends CloseNpcRequest
  await page.evaluate(() => { (window as unknown as { __sent: string[] }).__sent.length = 0; (window as unknown as { __game: WinGame }).__game.connection.dispatch(new Uint8Array([0xc3, 0x04, 0x30, 0x09])); });
  await expect(page.locator('.rml-generic_menu_dialog #panel')).toBeVisible({ timeout: 30_000 });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-generic_menu_dialog')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent)).toContain('CloseNpcRequest');

  // 0x25 Julia -> confirm dialog -> cancel sends CloseNpcRequest
  await page.evaluate(() => { (window as unknown as { __sent: string[] }).__sent.length = 0; (window as unknown as { __game: WinGame }).__game.connection.dispatch(new Uint8Array([0xc3, 0x04, 0x30, 0x25])); });
  await expect(page.locator('.rml-generic_confirm_dialog #panel')).toBeVisible({ timeout: 30_000 });
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent)).toContain('CloseNpcRequest');

  // 0x19 Stat Re-Initializer & 0x26 Lucky Item Combination -> close server dialog cleanly + show notice
  for (const kind of [0x19, 0x26]) {
    await page.evaluate((k) => { (window as unknown as { __sent: string[] }).__sent.length = 0; (window as unknown as { __game: WinGame }).__game.connection.dispatch(new Uint8Array([0xc3, 0x04, 0x30, k])); }, kind);
    await expect(page.locator('.rml-generic_confirm_dialog #panel')).toBeVisible({ timeout: 30_000 });
    expect(await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent)).toContain('CloseNpcRequest');
    await page.keyboard.press('Escape');
    await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);
  }

  // Golden Archer (EventChipRegistrationResult C1 94) -> opens dialog and closes with EventChipExitDialog + CloseNpcRequest
  await page.evaluate(async () => {
    const { write } = await import('/js/net/packets.js' as string) as { write(g: string, n: string, v: Record<string, unknown>): Uint8Array };
    (window as unknown as { __sent: string[] }).__sent.length = 0;
    (window as unknown as { __game: WinGame }).__game.connection.dispatch(write('ServerToClient', 'EventChipRegistrationResult', { Result: 0, RegisteredCount: 3, RemainingInventoryCount: 1, MutoNumber2: -1, MutoNumber3: -1 }));
  });
  await expect(page.locator('.rml-generic_confirm_dialog #panel')).toBeVisible({ timeout: 30_000 });
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);
  const sent = await page.evaluate(() => (window as unknown as { __sent: string[] }).__sent);
  expect(sent).toContain('EventChipExitDialog');
  expect(sent).toContain('CloseNpcRequest');

  await expectNoPageErrors(errors);
});
