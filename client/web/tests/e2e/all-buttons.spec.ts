// Exhaustive button sweep across every HUD bar, menu, and openable game window.
import { test, expect, type Page } from '@playwright/test';
import { chatText, clickAt, dismissOnboarding, enterWorld, expandHudTools, expectNoPageErrors } from './helpers.js';

const OPEN = { timeout: 60_000 };

async function clickUnoccluded(page: Page, hostSelector: string, innerSelector: string): Promise<void> {
  const loc = page.locator(`${hostSelector} ${innerSelector}`).first();
  await expect(loc).toBeVisible(OPEN);
  const box = await loc.boundingBox();
  if (!box) throw new Error(`Missing box for ${hostSelector} ${innerSelector}`);
  const center = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
  const occluded = await page.evaluate(({ x, y, hSel, iSel }) => {
    const top = document.elementFromPoint(x, y);
    const host = document.querySelector(hSel);
    if (!host || !top || top !== host) return `host mismatch: top=${top?.tagName}.${top?.className} vs ${hSel}`;
    const btn = host.shadowRoot?.querySelector(iSel);
    const innerTop = host.shadowRoot?.elementFromPoint(x, y);
    return innerTop && btn && (innerTop === btn || btn.contains(innerTop))
      ? null
      : `shadow mismatch: innerTop=${innerTop?.tagName}#${innerTop?.id}`;
  }, { ...center, hSel: hostSelector, iSel: innerSelector });
  expect(occluded, `${hostSelector} ${innerSelector} clickability`).toBeNull();
  await clickAt(page, center);
}

test('all HUD, menu, and window buttons open, act, or close as on the desktop client', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await enterWorld(page, 'test400');
  await dismissOnboarding(page);
  await page.evaluate(() => (window as unknown as { __game: { online: { say(t: string): void } } }).__game.online.say('/move lorencia'));
  await page.waitForTimeout(3_000);
  await expect(page.locator('.rml-main_frame #hud_layout')).toBeVisible(OPEN);

  // 1. Top utility buttons (inside touch-expandable #hudTools): Sound, Guide (i), Options (cog).
  await expandHudTools(page);
  const mutedBefore = await page.locator('#soundButton').getAttribute('aria-pressed');
  await page.locator('#soundButton').click();
  expect(await page.locator('#soundButton').getAttribute('aria-pressed')).not.toBe(mutedBefore);
  await page.locator('#soundButton').click();

  await page.locator('#guideButton').click();
  await expect(page.locator('#guide')).toBeVisible(OPEN);
  await page.locator('#guide .close').click();
  await expect(page.locator('#guide')).toBeHidden();

  await expandHudTools(page);
  await page.locator('#settingsButton').click();
  const optWin = page.locator('.rml-option_window');
  await expect(optWin.locator('#panel')).toBeVisible(OPEN);
  const visibleOptTabs = optWin.locator('.option-tab-btn:not(.hnmo-left-out)');
  const optTabCount = await visibleOptTabs.count();
  expect(optTabCount).toBeGreaterThanOrEqual(3);
  for (let t = 0; t < optTabCount; t++) {
    await visibleOptTabs.nth(t).click();
    await expect(visibleOptTabs.nth(t)).toHaveClass(/\bactive-tab\b/);
  }
  await optWin.locator('.option-close-btn').click();
  await expect(optWin).toHaveCount(0);

  // 2. Main bottom HUD skill selector + CashShop / Character / Inventory / Friends / WindowMenu.
  await clickUnoccluded(page, '.rml-main_frame', '#current_skill_slot');
  await expect(page.locator('.rml-main_frame #skill_grid .skill-cell').first()).toBeVisible(OPEN);
  await page.locator('.rml-main_frame #skill_grid .skill-cell').first().click();
  await expect(page.locator('.rml-main_frame #skill_grid')).toHaveClass(/\bhidden\b/);

  // Cash Shop (#btn_cshop -> #igs_refresh, #igs_close).
  await clickUnoccluded(page, '.rml-main_frame', '#btn_cshop');
  const igs = page.locator('.rml-in_game_shop');
  await expect(igs.locator('#panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_cshop')).toHaveClass(/\bopen\b/);
  await igs.locator('#igs_refresh').click();
  await igs.locator('#igs_close').click();
  await expect(igs).toHaveCount(0);

  // Character Window (#btn_chainfo -> #btn_pet -> PetInfoWindow -> #btn_quest -> #btn_exit).
  await clickUnoccluded(page, '.rml-main_frame', '#btn_chainfo');
  const charWin = page.locator('.rml-character_info');
  await expect(charWin.locator('#panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_chainfo')).toHaveClass(/\bopen\b/);

  await charWin.locator('#btn_pet').click();
  const petWin = page.locator('.rml-pet_info');
  await expect(petWin.locator('#panel')).toBeVisible(OPEN);
  await petWin.locator('.tab-btn-darkspirit').click();
  await expect(petWin.locator('.tab-btn-darkspirit')).toHaveClass(/\bactive\b/);
  await petWin.locator('.tab-btn-darkhorse').click();
  await petWin.locator('#btn_exit').click();
  await expect(petWin).toHaveCount(0);

  // Character -> Quest -> tabs, Scroll, Book, Exit.
  await charWin.locator('#btn_quest').click();
  const questWin = page.locator('.rml-my_quest_info');
  await expect(questWin.locator('#panel')).toBeVisible(OPEN);
  await questWin.locator('.tab-btn-jobchange').click();
  await expect(questWin.locator('.tab-btn-jobchange')).toHaveClass(/\bactive\b/);
  await questWin.locator('.tab-btn-castletemple').click();
  await expect(questWin.locator('.tab-btn-castletemple')).toHaveClass(/\bactive\b/);
  await questWin.locator('.tab-btn-quest').click();
  await questWin.locator('#btn_quest_open').click();
  await expect.poll(() => chatText(page)).toMatch(/quest/i);
  await questWin.locator('#btn_quest_giveup').click();
  await questWin.locator('#btn_exit').click();
  await expect(questWin).toHaveCount(0);

  // Inventory (#btn_myinven -> #btn_expand -> extension #btn_exit -> #btn_exit).
  await clickUnoccluded(page, '.rml-main_frame', '#btn_myinven');
  const invWin = page.locator('.rml-my_inventory');
  await expect(invWin.locator('#panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_myinven')).toHaveClass(/\bopen\b/);
  await invWin.locator('#btn_expand').click();
  await expect(page.locator('.rml-inventory_extension #panel')).toBeVisible(OPEN);
  await page.locator('.rml-inventory_extension #btn_exit').click();
  await expect(page.locator('.rml-inventory_extension')).toHaveCount(0);
  await invWin.locator('#btn_exit').click();
  await expect(invWin).toHaveCount(0);

  // Friends (#btn_friend -> 3 tabs -> #btn_close).
  await clickUnoccluded(page, '.rml-main_frame', '#btn_friend');
  const friendWin = page.locator('.rml-friend_shell');
  await expect(friendWin.locator('#panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-main_frame #btn_friend')).toHaveClass(/\bopen\b/);
  await friendWin.locator('#tabs .btn').nth(1).click();
  await expect(friendWin.locator('#tabs .btn').nth(1)).toHaveClass(/\bselected\b/);
  await friendWin.locator('#tabs .btn').nth(2).click();
  await expect(friendWin.locator('#tabs .btn').nth(2)).toHaveClass(/\bselected\b/);
  await friendWin.locator('#tabs .btn').nth(0).click();
  await friendWin.locator('#btn_close').click();
  await expect(friendWin).toBeHidden();

  // 3. WindowMenu (#btn_window) -> Guild (G), Move (M), Map (Tab), Help (F1), Gens (B), System (Esc).
  await clickUnoccluded(page, '.rml-main_frame', '#btn_window');
  const winMenu = page.locator('.rml-window_menu');
  await expect(winMenu.locator('#panel')).toBeVisible(OPEN);
  await winMenu.locator('.menu-row').nth(2).click(); // Guild (G)
  await expect(page.locator('.rml-guild_info #panel')).toBeVisible(OPEN);
  await page.locator('.rml-guild_info #btn_exit').click();
  await expect(page.locator('.rml-guild_info')).toHaveCount(0);

  await clickUnoccluded(page, '.rml-main_frame', '#btn_window');
  await winMenu.locator('.menu-row').nth(3).click(); // Move (M)
  await expect(page.locator('.rml-move_command #panel')).toBeVisible(OPEN);
  await page.locator('.rml-move_command #close_bar').click();
  await expect(page.locator('.rml-move_command')).toHaveCount(0);

  await clickUnoccluded(page, '.rml-main_frame', '#btn_window');
  await winMenu.locator('.menu-row').nth(4).click(); // Map (Tab)
  await expect(page.locator('.rml-mini_map .clip').first()).toBeVisible(OPEN);
  await page.locator('.rml-mini_map .close').first().click();
  await expect(page.locator('.rml-mini_map')).toHaveCount(0);

  await clickUnoccluded(page, '.rml-main_frame', '#btn_window');
  await winMenu.locator('.menu-row').nth(1).click(); // Help (F1)
  await expect(page.locator('#guide')).toBeVisible(OPEN);
  await page.locator('#guide .close').click();

  await clickUnoccluded(page, '.rml-main_frame', '#btn_window');
  await winMenu.locator('.menu-row').nth(5).click(); // Gens (B)
  expect((await page.locator('.rml-gens_ranking #panel').count()) > 0 || (await chatText(page)).toLowerCase().includes('gens')).toBe(true);
  if ((await page.locator('.rml-gens_ranking #panel').count()) > 0) await page.keyboard.press('Escape');

  await clickUnoccluded(page, '.rml-main_frame', '#btn_window');
  await winMenu.locator('.menu-row').nth(0).click(); // System (Esc)
  const sysMenu = page.locator('.rml-sys_menu');
  await expect(sysMenu.locator('#panel')).toBeVisible(OPEN);
  await sysMenu.locator('#btn_exit_game').click();
  await expect(page.locator('.rml-generic_confirm_dialog #panel')).toBeVisible(OPEN);
  await page.locator('.rml-generic_confirm_dialog #gcd_btn_cancel').click();
  await expect(page.locator('.rml-generic_confirm_dialog')).toHaveCount(0);
  await clickUnoccluded(page, '.rml-main_frame', '#btn_window');
  await winMenu.locator('.menu-row').nth(0).click();
  await expect(sysMenu.locator('#panel')).toBeVisible(OPEN);
  await sysMenu.locator('#btn_close').click();
  await expect(sysMenu).toHaveCount(0);

  // 4. Party (P), Command (D, all 12 buttons) -> Chat Command List (chat_command.rml).
  await page.keyboard.press('p');
  await expect(page.locator('.rml-party_info #panel')).toBeVisible(OPEN);
  await page.locator('.rml-party_info #btn_exit').click();
  await expect(page.locator('.rml-party_info')).toHaveCount(0);

  await page.keyboard.press('d');
  const cmdWin = page.locator('.rml-command_window');
  await expect(cmdWin.locator('#panel')).toBeVisible(OPEN);
  for (let i = 0; i < 11; i++) {
    await cmdWin.locator('.cmd-btn').nth(i).click();
    await expect(cmdWin.locator('.cmd-btn').nth(i)).toHaveClass(/\bselected\b/);
  }
  await cmdWin.locator('.cmd-btn').nth(11).click(); // Special -> Chat Command
  await expect(cmdWin).toHaveCount(0);
  await expect(page.locator('.rml-chat_command #panel')).toBeVisible(OPEN);
  await page.locator('.rml-chat_command #btn_exit').click();
  await expect(page.locator('.rml-chat_command')).toHaveCount(0);

  // 5. AutoPlay bar + Config + Ultimate HUD bar.
  await page.locator('.rml-mu_helper_bar #btn_config').click();
  const helperCfg = page.locator('.rml-mu_helper_config');
  await expect(helperCfg.locator('#panel')).toBeVisible(OPEN);
  await helperCfg.locator('#tab_1').click();
  await expect(helperCfg.locator('#tab_1')).toHaveClass(/\bactive\b/);
  await helperCfg.locator('#tab_2').click();
  await expect(helperCfg.locator('#tab_2')).toHaveClass(/\bactive\b/);
  await helperCfg.locator('#tab_0').click();
  await helperCfg.locator('#btn_exit').click();
  await expect(helperCfg).toHaveCount(0);
  await page.locator('.rml-mu_helper_bar #btn_config').click();
  await expect(helperCfg.locator('#panel')).toBeVisible(OPEN);
  await helperCfg.locator('#btn_save').click();
  await expect(helperCfg).toHaveCount(0);

  // Safe zone guard on Start button, then step outside Lorencia safe zone to verify live Start/Stop & HUD bar.
  await page.locator('.rml-mu_helper_bar #btn_start').click();
  await expect.poll(() => chatText(page)).toMatch(/safe zone|vùng an toàn/i);

  await page.evaluate(() => (window as unknown as { __game: { online: { say(t: string): void } } }).__game.online.say('/move lorencia'));
  await page.waitForTimeout(3_000);
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 70 }]) {
    await page.evaluate((cell) => (window as unknown as { __game: { online: { walkTo(c: { x: number; y: number }): boolean } } }).__game.online.walkTo(cell), goal);
    await expect.poll(
      () => page.evaluate(() => (window as unknown as { __game: { online: { hero: { isWalking: boolean } } } }).__game.online.hero.isWalking),
      { timeout: 60_000 },
    ).toBe(false);
  }

  await page.locator('.rml-mu_helper_bar #btn_start').click();
  await expect(page.locator('.rml-mu_helper_bar #btn_stop')).toBeVisible(OPEN);
  await page.locator('.rml-mu_helper_bar #btn_stop').click();
  await expect(page.locator('.rml-mu_helper_bar #btn_start')).toBeVisible(OPEN);

  // Floating Ultimate HUD bar is hidden by default because all Ultimate Autoplay controls live in Window 1 Tab 4.
  await expect(page.locator('#hnmo-ultimate-hud')).toBeHidden();
  await page.keyboard.press('Alt+z');
  await expect(helperCfg.locator('#panel')).toBeVisible(OPEN);
  await expect(helperCfg.locator('#tab_4')).toHaveClass(/\bactive\b/);
  await helperCfg.locator('#hnmo_page_ultimate [data-uap-tab="combat"]').click();
  await expect(helperCfg.locator('#hnmo_page_ultimate #uap-add-spot')).toBeVisible(OPEN);
  await helperCfg.locator('#btn_exit').click();
  await expect(helperCfg).toHaveCount(0);

  // Return hero cleanly to Lorencia safe zone so shared accounts stay town-ready.
  await page.evaluate(() => (window as unknown as { __game: { online: { say(t: string): void } } }).__game.online.say('/move lorencia'));
  await page.waitForTimeout(3_000);

  // 6. Community (K).
  await page.keyboard.press('k');
  await expect(page.locator('.rml-hnmo_community #panel')).toBeVisible(OPEN);
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-hnmo_community')).toHaveCount(0);

  await expectNoPageErrors(errors);
});
