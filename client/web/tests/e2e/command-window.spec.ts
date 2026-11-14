// The command window (D, command_window.rml, ui/commandWindow.ts): its twelve buttons; Whisper armed and a
// right click on a player next to the hero puts the name in the chat's whisper box; Follow armed and a right
// click makes the hero walk after the player; Escape closes it. Two players: test0 and test1.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors, screenPointOf } from './helpers.js';

interface Cell { x: number; y: number }
interface Player { id: number; kind: string; name: string; cell: Cell }
interface CommandGame {
  hero: { name: string };
  online: { hero: { cell: Cell; isWalking: boolean }; characters: Map<number, Player>; walkTo(cell: Cell): boolean; say(text: string): void; followId: number | null };
  chat: { whisperTarget: string; setWhisperTarget(name: string): void };
  commandWindow: { isOpen: boolean; armed: number };
}
type Win = Window & { __game: CommandGame };
const OPEN = { timeout: 60_000 };
const game = <T>(page: Page, f: (g: CommandGame) => T) => page.evaluate(`(${f.toString()})(window.__game)`) as Promise<T>;

test('the command window: buttons, whisper and follow on a right click', async ({ browser }) => {
  test.setTimeout(10 * 60_000);
  const contextA = await browser.newContext(), contextB = await browser.newContext();
  const a = await contextA.newPage(), b = await contextB.newPage();
  const errors: string[] = [];
  a.on('pageerror', (error) => errors.push(error.message));
  try {
    await enterWorld(a, 'test0');
    await enterWorld(b, 'test1');
    for (const page of [a, b]) await game(page, (g) => g.online.say('/move lorencia'));
    await a.waitForTimeout(10_000);
    const bName = await game(b, (g) => g.hero.name);
    const bOnA = () => a.evaluate((n) => [...(window as unknown as Win).__game.online.characters.values()].find((c) => c.kind === 'player' && c.name === n)?.cell ?? null, bName);
    const aCell = () => game(a, (g) => ({ ...g.online.hero.cell }));
    // B walks next to A.
    await expect(async () => {
      const at = await aCell();
      await b.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), { x: at.x + 1, y: at.y });
      await expect.poll(async () => { const c = await bOnA(); const h = await aCell(); return !!c && Math.abs(c.x - h.x) <= 2 && Math.abs(c.y - h.y) <= 2; }, { timeout: 20_000 }).toBe(true);
    }).toPass(OPEN);
    await a.waitForTimeout(3_000); // B's last steps

    // D opens it: twelve buttons with the desktop's names.
    await a.keyboard.press('d');
    const window_ = a.locator('.rml-command_window');
    await expect(window_.locator('.cmd-btn')).toHaveCount(12, OPEN);
    await expect(window_.locator('.cmd-label')).toHaveText(['Trade', 'Buy', 'Party', 'Whisper', 'Guild', 'Alliance', 'Hostility guild', 'Suspend hostilities', 'Add Friend', 'Follow', 'Duel', 'Special']);

    // Whisper, then a right click on B: B's name is in the whisper box.
    await window_.locator('.cmd-btn').nth(3).click();
    await expect(window_.locator('.cmd-btn').nth(3)).toHaveClass(/selected/);
    let point = (await screenPointOf(a, bName))!;
    await a.mouse.move(point.x, point.y);
    await expect(window_.locator('#target_name')).toHaveText(bName, { timeout: 10_000 });
    await a.screenshot({ path: 'test-results/command-window.png' });
    await a.mouse.click(point.x, point.y, { button: 'right' });
    await expect.poll(() => game(a, (g) => g.chat.whisperTarget)).toBe(bName);
    await expect.poll(() => game(a, (g) => g.commandWindow.armed)).toBe(-1);
    await game(a, (g) => g.chat.setWhisperTarget(''));

    // Follow, then a right click on B; B walks away and A walks after it.
    await window_.locator('.cmd-btn').nth(9).click();
    point = (await screenPointOf(a, bName))!;
    await a.mouse.click(point.x, point.y, { button: 'right' });
    await expect.poll(() => game(a, (g) => g.online.followId)).not.toBeNull();
    const start = await aCell();
    await b.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), { x: start.x + 1, y: start.y + 5 });
    await expect.poll(async () => { const h = await aCell(); return Math.abs(h.y - start.y); }, OPEN).toBeGreaterThanOrEqual(3);

    // Escape closes the window.
    await a.keyboard.press('Escape');
    await expect(window_).toHaveCount(0);
    await expectNoPageErrors(errors);
  } finally {
    await contextA.close();
    await contextB.close();
  }
});
