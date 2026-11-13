// Skill cooldowns like the desktop's (CheckSkillDelay, the skill bar's wipe): a skill that cools down
// shows a dark wipe over its icon that shrinks as the delay runs out, and can't be cast meanwhile.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface CooldownGame {
  cooldowns: { tryStart(skill: number, delayMs: number): boolean; remaining(skill: number): number };
  skills: { skillAt(index: number): number; current: number };
}
type Win = Window & { __game: CooldownGame };

test('a skill cooling down shows the wipe on the skill bar until it is ready', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400');
  const hud = page.locator('.rml-main_frame');
  await expect(hud.locator('#current_skill_slot .skill-icon')).toHaveAttribute('data-decorator', /^image\(skill-icon-/, { timeout: 90_000 });
  const wipe = hud.locator('#current_skill_slot .cooldown-wipe');
  const height = () => wipe.evaluate((element) => (element as HTMLElement).getBoundingClientRect().height);
  expect(await height()).toBe(0);
  // The current skill starts an 8-second delay (as casting it would); a second start is refused.
  const started = await page.evaluate(() => { const g = (window as unknown as Win).__game; const skill = g.skills.skillAt(g.skills.current); return [g.cooldowns.tryStart(skill, 8000), g.cooldowns.tryStart(skill, 8000)]; });
  expect(started).toEqual([true, false]);
  await expect.poll(height, { timeout: 10_000 }).toBeGreaterThan(0);
  const first = await height();
  await page.waitForTimeout(3_000);
  expect(await height()).toBeLessThan(first);
  await page.screenshot({ path: 'test-results/cooldown.png' });
  // Ready again: the wipe is gone.
  await expect.poll(height, { timeout: 20_000 }).toBe(0);
  await expectNoPageErrors(errors);
});
