// A long walk goes on after the server puts the hero back a little. The server walks a step or two behind the
// hero; when the next part of a walk (15 steps at most per packet) starts from a cell it hasn't reached, it refuses
// that part and sends the hero's position back (ObjectMoved). The hero used to stop there for good, short of where
// the player clicked (pets.spec's walks into the field ended so, now and then). Here the correction is fed to the
// client in the middle of a walk across Lorencia: the hero still arrives.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface WalkGame {
  mapNumber: number;
  hero: { cell: { x: number; y: number }; isWalking: boolean };
  online: { playerId: number; goal?: { x: number; y: number } | null; continuedAt?: number; resumesLeft?: number; say(text: string): void; walkTo(cell: { x: number; y: number }): boolean };
  connection: { dispatch(bytes: Uint8Array): void };
}
type Win = Window & { __game: WalkGame };
const game = <T>(page: Page, f: (g: WalkGame) => T) => page.evaluate(`(${f.toString()})(window.__game)`) as Promise<T>;

// Two walkable cells in Lorencia, 42 cells apart: the town's east gate and the field beyond it.
const TOWN = { x: 134, y: 129 }, FIELD = { x: 176, y: 126 };

async function walkAndWait(page: Page, cell: { x: number; y: number }): Promise<void> {
  await page.evaluate((target) => (window as unknown as Win).__game.online.walkTo(target), cell);
  await expect.poll(() => game(page, (g) => (!g.hero.isWalking && !g.online.goal ? `${g.hero.cell.x},${g.hero.cell.y}` : 'walking')), { timeout: 90_000 }).toBe(`${cell.x},${cell.y}`);
}

test('a long walk goes on after the server puts the hero back a little', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test4');
  if (await game(page, (g) => g.mapNumber) !== 0) {
    await game(page, (g) => g.online.say('/move lorencia'));
    await expect.poll(() => game(page, (g) => g.mapNumber), { timeout: 60_000 }).toBe(0);
  }
  await walkAndWait(page, TOWN);

  // Right after the second part of the walk goes out (the first part is 15 steps), the "server" puts the hero
  // a step back on its way, as the server's refusal of that part does. The part is caught as the client sends it
  // (walkNextPart called again for the walk) and the correction comes right after, whatever the frame rate: the test
  // machine draws a frame or two a second, sometimes less. "Back" is a cell the hero stood on a step before.
  const back = await page.evaluate(async (target) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const g = (window as unknown as Win).__game;
    const online = g.online as unknown as { walkNextPart(): boolean; walkTo(cell: { x: number; y: number }): boolean };
    const visited: { x: number; y: number }[] = [];
    let recording = true;
    const record = (): void => {
      const last = visited[visited.length - 1];
      if (!last || last.x !== g.hero.cell.x || last.y !== g.hero.cell.y) visited.push({ x: g.hero.cell.x, y: g.hero.cell.y });
      if (recording) requestAnimationFrame(record);
    };
    record();
    const original = online.walkNextPart;
    let parts = 0;
    const answer = new Promise<{ x: number; y: number; resumesLeft?: number; goal: { x: number; y: number } | null }>((resolve, reject) => {
      const timer = setTimeout(() => { online.walkNextPart = original; recording = false; reject(new Error(`the walk sent no second part: ${g.hero.cell.x},${g.hero.cell.y}`)); }, 60_000);
      online.walkNextPart = function walkNextPart(this: unknown): boolean {
        const sent = original.call(online);
        if (sent && ++parts === 2) {
          online.walkNextPart = original;
          // After update() has noted the part as sent (continuedAt), as the server's answer comes after it.
          setTimeout(() => {
            recording = false;
            clearTimeout(timer);
            // A step back, where the server is (it walks a step or two behind): a correction to a cell the server
            // isn't at would be answered by the server's own correction on the next part, from further away.
            const here = g.hero.cell;
            const near = visited.filter((c) => Math.max(Math.abs(c.x - here.x), Math.abs(c.y - here.y)) === 1);
            const to = near[near.length - 1] ?? { x: here.x, y: here.y };
            const resumesLeft = g.online.resumesLeft;
            g.connection.dispatch(write('ServerToClient', 'ObjectMoved', { HeaderCode: 0x15, ObjectId: g.online.playerId, PositionX: to.x, PositionY: to.y }));
            // The client's decision, read at once (later frames bring the real server's answers to the walk).
            resolve({ ...to, resumesLeft, goal: g.online.goal ? { x: g.online.goal.x, y: g.online.goal.y } : null });
          }, 0);
        }
        return sent;
      };
    });
    online.walkTo(target);
    return answer;
  }, FIELD);
  expect(back.goal, `the goal is kept after the hero was put back to ${back.x},${back.y} (resumes left before: ${back.resumesLeft})`).toEqual(FIELD);
  await expect.poll(() => game(page, (g) => (!g.hero.isWalking && !g.online.goal ? `${g.hero.cell.x},${g.hero.cell.y}` : 'walking')), { timeout: 90_000 }).toBe(`${FIELD.x},${FIELD.y}`);

  // A big jump (a knock-back, a pull, more than 3 cells) still ends the walk where it puts the hero.
  await page.evaluate((target) => (window as unknown as Win).__game.online.walkTo(target), TOWN);
  await page.evaluate(async (fromX) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const g = (window as unknown as Win).__game;
    const started = performance.now();
    while (!(g.online.goal && g.hero.cell.x <= fromX - 4)) {
      if (performance.now() - started > 30_000) throw new Error(`the hero didn't walk back: ${g.hero.cell.x},${g.hero.cell.y}`);
      await new Promise((resolve) => requestAnimationFrame(resolve));
    }
    g.connection.dispatch(write('ServerToClient', 'ObjectMoved', { HeaderCode: 0x15, ObjectId: g.online.playerId, PositionX: g.hero.cell.x + 6, PositionY: g.hero.cell.y }));
  }, FIELD.x);
  expect(await game(page, (g) => g.online.goal ?? null)).toBeNull();
  await expectNoPageErrors(errors);
});
