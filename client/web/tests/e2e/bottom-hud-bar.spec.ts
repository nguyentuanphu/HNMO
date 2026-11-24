// Bottom HUD bar (#btn_cshop, #btn_chainfo, #btn_myinven, #btn_friend, #btn_window) + WindowMenu + SysMenu.
import { test, expect, type Page } from '@playwright/test';
import { chatText, clickAt, dismissOnboarding, ensureLorenciaSafeZone, enterWorld, expectNoPageErrors } from './helpers.js';

const OPEN = { timeout: 60_000 };

async function clickHudButton(page: Page, selector: string): Promise<void> {
  const box = await page.locator(`.rml-main_frame ${selector}`).boundingBox();
  if (!box) throw new Error(`Missing HUD button: ${selector}`);
  const center = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
  const occluded = await page.evaluate(({ x, y, sel }) => {
    const top = document.elementFromPoint(x, y);
    if (!top) return 'elementFromPoint returned null';
    const host = document.querySelector('.rml-main_frame');
    if (top !== host) return `covered by ${top.tagName}.${top.className}`;
    const btn = host?.shadowRoot?.querySelector(sel);
    if (!btn) return `missing inner ${sel}`;
    const innerTop = host?.shadowRoot?.elementFromPoint(x, y);
    return innerTop && (innerTop === btn || btn.contains(innerTop)) ? null : `shadow covered by ${innerTop?.tagName}#${innerTop?.id}`;
  }, { ...center, sel: selector });
  expect(occluded, `${selector} clickability`).toBeNull();
  await clickAt(page, center);
}

test('bottom HUD buttons open their windows, highlight while open, and WindowMenu/SysMenu work', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test400');
  await ensureLorenciaSafeZone(page);
  await dismissOnboarding(page);
  await expect(page.locator('.rml-main_frame #hud_layout')).toBeVisible(OPEN);

  // Cash Shop (#btn_cshop)
  await clickHudButton(page, '#btn_cshop');
  await expect(page.locator('.rml-in_game_shop #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_cshop')).toHaveClass(/\bopen\b/);
  await clickHudButton(page, '#btn_cshop');
  await expect(page.locator('.rml-in_game_shop')).toHaveCount(0);
  await expect(page.locator('.rml-main_frame #btn_cshop')).not.toHaveClass(/\bopen\b/);

  // Character Info (#btn_chainfo)
  await clickHudButton(page, '#btn_chainfo');
  await expect(page.locator('.rml-character_info #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_chainfo')).toHaveClass(/\bopen\b/);
  await clickHudButton(page, '#btn_chainfo');
  await expect(page.locator('.rml-character_info')).toHaveCount(0);

  // Inventory (#btn_myinven)
  await clickHudButton(page, '#btn_myinven');
  await expect(page.locator('.rml-my_inventory #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_myinven')).toHaveClass(/\bopen\b/);
  await clickHudButton(page, '#btn_myinven');
  await expect(page.locator('.rml-my_inventory')).toHaveCount(0);

  // Friends (#btn_friend)
  await clickHudButton(page, '#btn_friend');
  await expect(page.locator('.rml-friend_shell #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_friend')).toHaveClass(/\bopen\b/);
  await clickHudButton(page, '#btn_friend');
  await expect(page.locator('.rml-friend_shell')).toBeHidden();
  await expect(page.locator('.rml-main_frame #btn_friend')).not.toHaveClass(/\bopen\b/);

  // Window Menu (#btn_window) & its 6 entries: System (Esc), Help (F1), Guild (G), Move (M), Map (Tab), Gens (B)
  await clickHudButton(page, '#btn_window');
  await expect(page.locator('.rml-window_menu #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_window')).toHaveClass(/\bopen\b/);
  await expect(page.locator('.rml-window_menu .menu-row')).toHaveCount(6);

  // Entry 2: Guild (G)
  await page.locator('.rml-window_menu .menu-row').nth(2).click();
  await expect(page.locator('.rml-guild_info #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-window_menu')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-guild_info')).toHaveCount(0);

  // Entry 3: Move (M)
  await clickHudButton(page, '#btn_window');
  await expect(page.locator('.rml-window_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-window_menu .menu-row').nth(3).click();
  await expect(page.locator('.rml-move_command #panel')).toBeVisible(OPEN);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-move_command')).toHaveCount(0);

  // Entry 4: Map (Tab)
  await clickHudButton(page, '#btn_window');
  await expect(page.locator('.rml-window_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-window_menu .menu-row').nth(4).click();
  await expect(page.locator('.rml-mini_map .clip').first()).toBeVisible(OPEN);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-mini_map')).toHaveCount(0);

  // Entry 1: Help (F1)
  await clickHudButton(page, '#btn_window');
  await expect(page.locator('.rml-window_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-window_menu .menu-row').nth(1).click();
  await expect(page.locator('#guide')).toBeVisible(OPEN);
  await page.locator('#guide .close').click();
  await expect(page.locator('#guide')).toBeHidden();

  // Entry 5: Gens (B)
  await clickHudButton(page, '#btn_window');
  await expect(page.locator('.rml-window_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-window_menu .menu-row').nth(5).click();
  await expect(page.locator('.rml-window_menu')).toHaveCount(0);
  const gensHandled = (await page.locator('.rml-gens_ranking #panel').count()) > 0
    || (await chatText(page)).toLowerCase().includes('gens');
  expect(gensHandled).toBe(true);
  if ((await page.locator('.rml-gens_ranking #panel').count()) > 0) {
    await page.keyboard.press('Escape');
  }

  // Entry 0: System (Esc) -> opens SysMenu -> Options opens .rml-option_window, Close closes SysMenu
  await clickHudButton(page, '#btn_window');
  await expect(page.locator('.rml-window_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-window_menu .menu-row').nth(0).click();
  await expect(page.locator('.rml-sys_menu #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-sys_menu #btn_exit_game')).toBeVisible();
  await expect(page.locator('.rml-sys_menu #btn_option')).toBeVisible();
  await expect(page.locator('.rml-sys_menu #btn_close')).toBeVisible();

  await page.locator('.rml-sys_menu #btn_option').click();
  await expect(page.locator('.rml-sys_menu')).toHaveCount(0);
  await expect(page.locator('.rml-option_window #panel')).toBeVisible(OPEN);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-option_window')).toHaveCount(0);

  // Press Esc with no window open -> opens SysMenu; click #btn_exit_game / #btn_select_server -> confirm dialog Cancel; click #btn_close -> closes SysMenu
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-sys_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-sys_menu #btn_exit_game').click();
  await expect(page.locator('.rml-generic_confirm_dialog #panel')).toBeVisible(OPEN);
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);

  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-sys_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-sys_menu #btn_select_server').click();
  await expect(page.locator('.rml-generic_confirm_dialog #panel')).toBeVisible(OPEN);
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);

  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-sys_menu #panel')).toBeVisible(OPEN);
  await page.locator('.rml-sys_menu #btn_close').click();
  await expect(page.locator('.rml-sys_menu')).toHaveCount(0);

  // Key U toggles WindowMenu
  await page.keyboard.press('u');
  await expect(page.locator('.rml-window_menu #panel')).toBeVisible(OPEN);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-window_menu')).toHaveCount(0);

  await expectNoPageErrors(errors);
});

test('low-level character clicking #btn_friend sees the level 6 restriction notice', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test0');
  await dismissOnboarding(page);
  await expect(page.locator('.rml-main_frame #hud_layout')).toBeVisible(OPEN);
  const level = await page.evaluate(() => (window as unknown as { __game: { stats: { level: number } } }).__game.stats.level);
  expect(level).toBeLessThan(6);

  await clickHudButton(page, '#btn_friend');
  await expect(page.locator('.rml-friend_shell')).toHaveCount(0);
  expect(await chatText(page)).toContain('at least level 6');
  await expectNoPageErrors(errors);
});
