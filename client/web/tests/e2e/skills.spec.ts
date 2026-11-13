// The skill bar like the desktop's: the learned skills from the server with their icons, the
// skill popup of the current-skill slot, Ctrl + a number over a skill puts it on that key, and the
// number keys pick the current skill.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface SkillView { skills: { skills: Map<number, number>; hotkeys: number[]; current: number } }

const skillsOf = (page: Page) => page.evaluate(() => {
  const s = (window as unknown as { __game: SkillView }).__game.skills;
  return { skills: [...s.skills], hotkeys: [...s.hotkeys], current: s.current };
});

test('skill list, skill popup, hotkeys', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400'); // test400's knight knows two skills
  await expect.poll(async () => (await skillsOf(page)).skills.length).toBeGreaterThanOrEqual(2);
  const hud = page.locator('.rml-main_frame');
  // The current skill's icon, a sprite of skill_icons.rcss.
  await expect(hud.locator('#current_skill_slot .skill-icon')).toHaveAttribute('data-decorator', /^image\(skill-icon-/);

  // The current-skill slot opens the popup with every learned skill.
  await hud.locator('#current_skill_slot').click();
  const cells = hud.locator('#skill_grid .skill-cell');
  const { skills } = await skillsOf(page);
  await expect(cells).toHaveCount(skills.length);

  // Ctrl + 2 over the second skill puts it on key 2; the row shows it.
  const [secondIndex] = skills[1];
  const second = cells.nth(1);
  await second.hover();
  await page.keyboard.press('Control+Digit2');
  await expect.poll(async () => (await skillsOf(page)).hotkeys[2]).toBe(secondIndex);
  await expect(hud.locator('#skill_slot_1 .skill-hotkey-label')).toHaveText('2');
  await expect(hud.locator('#skill_slot_1 .skill-icon')).toHaveAttribute('data-decorator', /^image\(skill-icon-/);
  await page.screenshot({ path: 'test-results/skills.png' });

  // A click on a popup skill makes it the current one and closes the popup.
  const [firstIndex] = skills[0];
  await hud.locator('#current_skill_slot').click(); // close
  await page.keyboard.press('Digit2');
  await expect.poll(async () => (await skillsOf(page)).current).toBe(secondIndex);
  await expect(hud.locator('#current_skill_slot .skill-hotkey-label')).toHaveText('2');
  await hud.locator('#current_skill_slot').click();
  await cells.first().click();
  await expect.poll(async () => (await skillsOf(page)).current).toBe(firstIndex);
  await expect(cells).toHaveCount(0);
  await expectNoPageErrors(errors);
});
