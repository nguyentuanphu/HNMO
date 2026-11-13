// NPC dialogues and quests like on the desktop: talking to Lorencia's Elf Soldier opens its dialogue (npc_dialogue.rml)
// with its words and answers; "Accept a quest." lists its quests, choosing Spider Hunt shows the quest's words
// (quest_progress.rml), accepting it shows its requirements and rewards; T opens the quest info window
// (my_quest_info.rml) with the quest, its summary and requirements; giving it up empties the list again.
// test0's first character is level 1: Spider Hunt is for levels 1 to 14 (Quests.cs, group 18).
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Npc { id: number; kind: string; monsterNumber: number | null; cell: { x: number; y: number } }
interface QuestGame {
  online: { characters: Map<number, Npc>; hero: { cell: { x: number; y: number } }; walkTo(cell: { x: number; y: number }): boolean; talkTo(npc: unknown): void; say(text: string): void };
}
type Win = Window & { __game: QuestGame };

const ELF_SOLDIER = 257; // Lorencia (130, 128) and three more in town
const OPEN = { timeout: 90_000 };

// The rows' texts with the wrapped lines joined (the desktop wraps answers to two lines of 160 pixels).
const texts = async (page: Page, selector: string) => (await page.locator(selector).allTextContents()).map((text) => text.replace(/\s*\n\s*/g, ' ').trim());

async function talkToElfSoldier(page: Page): Promise<void> {
  const nearest = () => page.evaluate((number) => {
    const { online } = (window as unknown as Win).__game;
    const { x, y } = online.hero.cell;
    const npcs = [...online.characters.values()].filter((c) => c.monsterNumber === number);
    npcs.sort((a, b) => Math.hypot(a.cell.x - x, a.cell.y - y) - Math.hypot(b.cell.x - x, b.cell.y - y));
    return npcs[0]?.id ?? null;
  }, ELF_SOLDIER);
  await expect.poll(nearest, OPEN).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await nearest())!);
}

// Pages the NPC's words (the right arrow) until the answers show.
async function pageToAnswers(page: Page, next: string, answers: string): Promise<void> {
  for (let i = 0; i < 6 && !(await page.locator(answers).first().isVisible()); i++) {
    await page.locator(next).click();
    await page.waitForTimeout(500);
  }
  await expect(page.locator(answers).first()).toBeVisible();
}

async function giveUpAll(page: Page): Promise<void> {
  const quest = page.locator('.rml-my_quest_info .quest-row');
  for (let i = 0; i < 5 && (await quest.count()) > 0; i++) {
    await quest.first().click();
    await page.locator('.rml-my_quest_info #btn_quest_giveup').click();
    await page.locator('.confirm-dialog .confirm-ok').click();
    await page.waitForTimeout(3_000);
  }
}

test('talks to an NPC, accepts a quest and finds it in the quest window', async ({ page }) => {
  test.setTimeout(10 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test0');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(6_000);

  // A quest left from an earlier run is given up first (T, select it, give up, OK).
  await page.keyboard.press('t');
  await expect(page.locator('.rml-my_quest_info #panel')).toBeVisible(OPEN);
  await page.waitForTimeout(2_000);
  await giveUpAll(page);
  await expect(page.locator('.rml-my_quest_info .quest-empty-msg')).toContainText('There are no active quests.');
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-my_quest_info')).toHaveCount(0);

  // Talking to the Elf Soldier (the game calls it Shadow Phantom Soldier) opens its dialogue: its name, its words and, after the last page, its answers.
  await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 131, y: 130 }));
  await talkToElfSoldier(page);
  const dialogue = page.locator('.rml-npc_dialogue #panel');
  await expect(dialogue).toBeVisible(OPEN);
  await expect(page.locator('.rml-npc_dialogue #title')).toHaveText('Shadow Phantom Soldier'); // the desktop's name of NPC 257
  await expect.poll(async () => (await texts(page, '.rml-npc_dialogue .nd-line')).join(' ')).toContain('Welcome to the world of Mu.');
  await pageToAnswers(page, '.rml-npc_dialogue #btn_npc_next', '.rml-npc_dialogue .nd-answer-row');
  expect(await texts(page, '.rml-npc_dialogue .nd-answer-row')).toEqual(['1. Receive damage and defense increase buff.', '2. Accept a quest.']);
  await page.screenshot({ path: 'test-results/quests-dialogue.png' });

  // "Accept a quest." asks the server for the NPC's quests (AvailableQuestsRequest): Spider Hunt and "Go back."
  await page.locator('.rml-npc_dialogue .nd-answer-row', { hasText: 'Accept a quest.' }).click();
  await expect.poll(() => texts(page, '.rml-npc_dialogue .nd-answer-row'), OPEN).toContain('1. [Q]Spider Hunt!');
  await expect(page.locator('.rml-npc_dialogue .nd-line').first()).toContainText('Select a quest.');

  // Choosing it (QuestSelectRequest) opens the quest's window with its subject and words; the dialogue closes.
  await page.locator('.rml-npc_dialogue .nd-answer-row', { hasText: 'Spider Hunt!' }).click();
  const progress = page.locator('.rml-quest_progress #panel');
  await expect(progress).toBeVisible(OPEN);
  await expect(page.locator('.rml-npc_dialogue')).toHaveCount(0);
  await expect(page.locator('.rml-quest_progress .qp-subject')).toHaveText('Spider Hunt!');
  await pageToAnswers(page, '.rml-quest_progress #btn_next', '.rml-quest_progress .qp-answer-row');
  expect((await texts(page, '.rml-quest_progress .qp-answer-row'))[0]).toBe('1.I accept your quest. (Accept)');
  await page.screenshot({ path: 'test-results/quests-progress.png' });

  // Accepting (QuestProceedRequest) shows what the quest asks for and gives.
  await page.locator('.rml-quest_progress .qp-answer-row').first().click();
  await expect.poll(() => texts(page, '.rml-quest_progress .qp-reward-row'), OPEN).toContain('Mon.: Spider x 0/10');
  const rows = await texts(page, '.rml-quest_progress .qp-reward-row');
  expect(rows).toContain('Requirements');
  expect(rows).toContain('Exp.: 2000');
  await expect(page.locator('.rml-quest_progress #btn_complete')).toHaveClass(/disabled/);
  await page.screenshot({ path: 'test-results/quests-accepted.png' });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-quest_progress')).toHaveCount(0);

  // T: the quest info window lists the quest; selecting it shows its summary, requirements and rewards.
  await page.keyboard.press('t');
  await expect(page.locator('.rml-my_quest_info #panel')).toBeVisible(OPEN);
  await expect.poll(() => texts(page, '.rml-my_quest_info .quest-row'), OPEN).toEqual(['1.Spider Hunt!']);
  await page.locator('.rml-my_quest_info .quest-row').first().click();
  await expect.poll(() => texts(page, '.rml-my_quest_info .quest-content-row'), OPEN).toContain('Mon.: Spider x 0/10');
  expect(await texts(page, '.rml-my_quest_info .quest-content-row')).toContain('Spider Hunt!');
  await page.screenshot({ path: 'test-results/quests-info.png' });

  // Giving up (QuestCancelRequest, after the question) takes it off the list.
  await giveUpAll(page);
  await expect(page.locator('.rml-my_quest_info .quest-empty-msg')).toContainText('There are no active quests.', OPEN);
  await expectNoPageErrors(errors);
});

// The class change quests: Sebina the Priestess in Devias opens the NPC quest window (npc_quest.rml) with the quest's
// title, the dialog page and its numbered answers. quest1's first character is level 150 (TestAccounts/Quest150.cs).
test('talks to a class change quest NPC', async ({ page }) => {
  test.setTimeout(10 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'quest1');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move devias'));
  await page.waitForTimeout(8_000);
  await page.evaluate(() => (window as unknown as Win).__game.online.walkTo({ x: 184, y: 34 }));
  const sebina = () => page.evaluate(() => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.monsterNumber === 235)?.id ?? null);
  await expect.poll(sebina, OPEN).not.toBeNull();
  await page.evaluate((id) => { const g = (window as unknown as Win).__game; g.online.talkTo(g.online.characters.get(id)); }, (await sebina())!);
  await expect(page.locator('.rml-npc_quest #content_root')).toBeVisible(OPEN);
  await expect.poll(async () => (await texts(page, '.rml-npc_quest .nq-message-line')).join(' '), OPEN).not.toBe('');
  await expect.poll(() => page.locator('.rml-npc_quest .nq-answer-row').count()).toBeGreaterThan(0);
  await expect(page.locator('.rml-npc_quest .nq-quest-title')).not.toHaveText('');
  await page.screenshot({ path: 'test-results/quests-npc-quest.png' });
  await page.keyboard.press('Escape');
  await expect(page.locator('.rml-npc_quest')).toHaveCount(0);
  await expectNoPageErrors(errors);
});
