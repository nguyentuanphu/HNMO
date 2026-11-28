// The Arena's team battle, played by the machine: ARENA_PLAYERS accounts (test0…, default 6) log in to the Arena,
// queue for a team battle from the Arena window, and are put into two teams on the Valley of Loren map (map 30): a
// full 5v5 with ten players, or a 3v3 after the minute of waiting with six. They meet in the middle and fight: kills
// count for the killer's team on everyone's score bar, the fallen come back at their team's gate, and at the end
// (10 minutes, or 30 kills) the score bar says who won and everyone is back on the arena map.
// Needs the Arena program running next to the web client (BASE_URL), see docs/plans/arena-2026-11-20.md.
import { test, expect, type Page, type BrowserContext } from '@playwright/test';

interface Fighter { id: number; name: string; kind: string; dead?: boolean; cell: { x: number; y: number } }
interface ArenaGame {
  mapNumber: number;
  online: { hero: Fighter; characters: Map<number, Fighter>; walkTo(cell: { x: number; y: number }): boolean };
  combat: { attack(target: unknown): void };
  arena?: { open(): void };
}
type Win = Window & { __game: ArenaGame };

const PLAYERS = Number(process.env.ARENA_PLAYERS ?? 6);
const RED_GATE = { x1: 60, y1: 24, x2: 66, y2: 30 };
const BLUE_GATE = { x1: 112, y1: 24, x2: 118, y2: 30 };
const LONG = { timeout: 15 * 60_000 };

async function enterArena(page: Page, account: string): Promise<void> {
  await page.goto('/index.html?devlogin=1');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.locator('.season-arena .choice').first().click({ timeout: 90_000 });
  await page.waitForSelector('#characters .choice', { timeout: 90_000 });
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 240_000 });
}

const scoreBar = (page: Page) => page.locator('#arena-scoreboard');
const heroName = (page: Page) => page.evaluate(() => document.querySelector('#heroName')?.textContent ?? '');
const mapOf = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.mapNumber);
const cellOf = (page: Page) => page.evaluate(() => ({ ...(window as unknown as Win).__game.online.hero.cell }));
const inside = (c: { x: number; y: number }, g: typeof RED_GATE, slack = 3) => c.x >= g.x1 - slack && c.x <= g.x2 + slack && c.y >= g.y1 - slack && c.y <= g.y2 + slack;

/** The score bar's numbers: "Red 3 : 5 Blue …". */
async function score(page: Page): Promise<{ red: number; blue: number; text: string }> {
  const text = (await scoreBar(page).textContent()) ?? '';
  const m = /(\d+) : (\d+)/.exec(text);
  return { red: m ? Number(m[1]) : -1, blue: m ? Number(m[2]) : -1, text };
}

test('the Arena team battle, played by the machine', async ({ browser }) => {
  test.skip(!process.env.ARENA_BATTLES, 'Plays a whole team battle (about 20 minutes) and needs the Arena program next to the web client: set ARENA_BATTLES=1.');
  test.setTimeout(40 * 60_000);
  const contexts: BrowserContext[] = [];
  const pages: Page[] = [];
  const errors: string[] = [];
  try {
    for (let i = 0; i < PLAYERS; i++) {
      const context = await browser.newContext();
      contexts.push(context);
      const page = await context.newPage();
      page.on('pageerror', (error) => errors.push(`test${i}: ${error.message}`));
      await enterArena(page, `test${i}`);
      pages.push(page);
    }

    // Everyone queues from the Arena window.
    for (const page of pages) {
      // Six game pages drawn in software leave the last one almost no frames; in front it draws, so the click lands.
      await page.bringToFront();
      await page.evaluate(() => (window as unknown as Win).__game.arena?.open());
      await page.locator('#arena-team-join').click({ timeout: 60_000 });
      await expect(page.locator('#arena-team-leave')).toBeVisible({ timeout: 30_000 });
    }
    await pages[0].screenshot({ path: 'test-results/arena-team-queue.png' });

    // The battle starts (six players: a 3v3 after the minute of waiting): everyone is on map 30 with a score bar.
    for (const page of pages) await expect(scoreBar(page)).toBeVisible({ timeout: 4 * 60_000 });
    for (const page of pages) await expect.poll(() => mapOf(page), { timeout: 120_000 }).toBe(30);
    const sides = await Promise.all(pages.map(async (page) => ((await scoreBar(page).textContent()) ?? '').includes('you: Red') ? 'red' : 'blue'));
    const names = await Promise.all(pages.map(heroName));
    const red = pages.filter((_, i) => sides[i] === 'red');
    const blue = pages.filter((_, i) => sides[i] === 'blue');
    expect(red.length).toBe(PLAYERS / 2);
    expect(blue.length).toBe(PLAYERS / 2);
    for (const [i, page] of pages.entries()) {
      await expect.poll(async () => inside(await cellOf(page), sides[i] === 'red' ? RED_GATE : BLUE_GATE), { timeout: 60_000 }).toBe(true);
    }
    await red[0].screenshot({ path: 'test-results/arena-team-start.png' });

    // Both teams walk to the middle and each fighter goes for the nearest living enemy.
    const enemiesOf = (i: number) => names.filter((_, j) => sides[j] !== sides[i]);
    const fight = async () => {
      await Promise.all(pages.map((page, i) => page.evaluate(({ enemies, goal }) => {
        const g = (window as unknown as Win).__game;
        if (g.online.hero.dead) return;
        const near = [...g.online.characters.values()]
          .filter((c) => c.kind === 'player' && !c.dead && enemies.includes(c.name))
          .sort((a, b) => Math.hypot(a.cell.x - g.online.hero.cell.x, a.cell.y - g.online.hero.cell.y) - Math.hypot(b.cell.x - g.online.hero.cell.x, b.cell.y - g.online.hero.cell.y))[0];
        if (near) g.combat.attack(near);
        else g.online.walkTo(goal);
      }, { enemies: enemiesOf(i), goal: sides[i] === 'red' ? { x: 88, y: 27 } : { x: 90, y: 27 } }).catch(() => undefined)));
    };

    // The first kill: every score bar shows it.
    await expect.poll(async () => { await fight(); const s = await score(pages[0]); return s.red + s.blue; }, { timeout: 8 * 60_000, intervals: [3000] }).toBeGreaterThan(0);
    const first = await score(pages[0]);
    for (const page of pages) await expect.poll(async () => (await score(page)).red + (await score(page)).blue, { timeout: 30_000 }).toBeGreaterThanOrEqual(first.red + first.blue);
    await pages[0].screenshot({ path: 'test-results/arena-team-first-kill.png' });

    // The fallen come back at their own team's gate (after 5 s).
    const fallen = await Promise.all(pages.map((page) => page.evaluate(() => !!(window as unknown as Win).__game.online.hero.dead)));
    const deadIndex = fallen.findIndex(Boolean);
    if (deadIndex >= 0) {
      const page = pages[deadIndex];
      await expect.poll(() => page.evaluate(() => !(window as unknown as Win).__game.online.hero.dead), { timeout: 60_000 }).toBe(true);
      expect(inside(await cellOf(page), sides[deadIndex] === 'red' ? RED_GATE : BLUE_GATE, 6)).toBe(true);
      await page.screenshot({ path: 'test-results/arena-team-respawn.png' });
    }

    // Fight on until the battle ends (10 minutes, or 30 kills): the bar says who won, then everyone goes home.
    await expect.poll(async () => { await fight(); return /Victory|Defeat|Draw/.test((await score(pages[0])).text); }, { ...LONG, intervals: [3000] }).toBe(true);
    const end = await score(pages[0]);
    for (const [i, page] of pages.entries()) {
      const text = (await score(page)).text;
      const won = end.red === end.blue ? 'Draw' : (end.red > end.blue) === (sides[i] === 'red') ? 'Victory' : 'Defeat';
      expect(text).toContain(won);
    }
    await pages[0].screenshot({ path: 'test-results/arena-team-end.png' });
    for (const page of pages) await expect(scoreBar(page)).toBeHidden({ timeout: 120_000 });
    for (const page of pages) await expect.poll(() => mapOf(page), { timeout: 120_000 }).toBe(6);
    console.log(`team battle: ${PLAYERS / 2}v${PLAYERS / 2}, red ${end.red} : ${end.blue} blue`);
    expect(errors).toEqual([]);
  } finally {
    for (const context of contexts) await context.close().catch(() => undefined);
  }
});
