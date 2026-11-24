// One quest window (Part 8 Item 1): QuestWindows keeps strictly one quest document open at a
// time in the primary right-hand slot (column 0), closing my_quest_info when quest_progress_etc
// opens, closing progress when T opens my_quest_info, and closing onboarding on quest open.
import { test, expect } from '@playwright/test';
import { dismissOnboarding, enterWorld, expectNoPageErrors } from './helpers.js';

interface QuestGameView {
  __game: {
    quests?: {
      isMyQuestOpen: boolean;
      showProgress(index: number, byEtc: boolean): Promise<void>;
      hideProgress(): void;
      toggleMyQuest(): void;
      log?: { scripts: { progress: Map<number, unknown> } } | null;
    };
    onboarding?: { isOpen: boolean; open(): void };
  };
}

const QUEST_WINDOWS = '.rml-my_quest_info, .rml-quest_progress, .rml-quest_progress_etc, .rml-npc_dialogue, .rml-npc_quest';
const OPEN = { timeout: 60_000 };

test('at most one quest window stays open across My Quest (T) and Quest Progress Etc', async ({ page }) => {
  test.setTimeout(4 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));

  await enterWorld(page, 'test9');
  await dismissOnboarding(page);
  await expect.poll(() => page.evaluate(() => Boolean((window as unknown as QuestGameView).__game.quests?.log)), OPEN).toBe(true);

  // 1. T opens my_quest_info alone.
  await page.keyboard.press('t');
  await expect(page.locator('.rml-my_quest_info #panel')).toBeVisible(OPEN);
  await expect(page.locator(QUEST_WINDOWS)).toHaveCount(1);
  await page.screenshot({ path: 'test-results/one-quest-my-quest.png' });

  // 2. Showing byEtc progress closes my_quest_info instead of opening a second side-by-side Quest window.
  const firstQuestIndex = await page.evaluate(() => {
    const log = (window as unknown as QuestGameView).__game.quests?.log;
    const first = log ? log.scripts.progress.keys().next().value : undefined;
    return first ?? 0;
  });
  expect(firstQuestIndex).toBeGreaterThan(0);

  await page.evaluate((idx) => (window as unknown as QuestGameView).__game.quests!.showProgress(idx, true), firstQuestIndex);
  await expect(page.locator('.rml-quest_progress_etc #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-my_quest_info')).toHaveCount(0);
  await expect(page.locator(QUEST_WINDOWS)).toHaveCount(1);

  // Placed in column 0 (right edge), not shifted to column 1.
  const atColZero = await page.evaluate(() => {
    const panel = document.querySelector('.rml-quest_progress_etc')?.shadowRoot?.querySelector<HTMLElement>('#panel');
    if (!panel) return false;
    return panel.getBoundingClientRect().right >= window.innerWidth - 20;
  });
  expect(atColZero).toBe(true);
  await page.screenshot({ path: 'test-results/one-quest-progress-etc.png' });

  // 3. Pressing T closes quest_progress_etc and shows my_quest_info as the single quest window.
  await page.keyboard.press('t');
  await expect(page.locator('.rml-my_quest_info #panel')).toBeVisible(OPEN);
  await expect(page.locator('.rml-quest_progress_etc')).toHaveCount(0);
  await expect(page.locator(QUEST_WINDOWS)).toHaveCount(1);

  await page.keyboard.press('Escape');
  await expect(page.locator(QUEST_WINDOWS)).toHaveCount(0);

  await expectNoPageErrors(errors);
});
