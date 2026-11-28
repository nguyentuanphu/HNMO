// The Arena's guild war, played by the machine: two guilds ("Red": test0-2, "Blue": test3-5, made in the test save
// beforehand, see docs/plans/arena-2026-11-20.md) sign up from the Arena window after a game master (testgm) opened the
// sign-up with /startguildwar; with the minimum lowered to 3 in the Arena's settings they fight 3 against 3 on the
// Valley of Loren (map 30). Kills and the flags (79,27 / 89,27 / 100,33) give points shown on every score bar; after
// the 15 minutes the bar says who won and everyone is back on the arena map.
import { test, expect, type Page, type BrowserContext } from '@playwright/test';

interface Fighter { id: number; name: string; kind: string; dead?: boolean; cell: { x: number; y: number } }
interface ArenaGame {
  mapNumber: number;
  online: { hero: Fighter; characters: Map<number, Fighter>; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  combat: { attack(target: unknown): void };
  arena?: { open(): void };
}
type Win = Window & { __game: ArenaGame };

const PLAYERS = 6;
const GM = process.env.GM_ACCOUNT ?? 'testgm';
const FLAGS = [{ x: 79, y: 27 }, { x: 89, y: 27 }, { x: 100, y: 33 }];

async function enterArena(page: Page, account: string): Promise<void> {
  // With several browsers on the test machine the Arena's server list can come late: start over then.
  await expect(async () => {
    await page.goto('/index.html?devlogin=1');
    await page.fill('#username', account);
    await page.fill('#password', account);
    await page.click('button.primary');
    await page.locator('.season-arena .choice').first().click({ timeout: 60_000 });
  }).toPass({ timeout: 300_000 });
  await page.waitForSelector('#characters .choice', { timeout: 90_000 });
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 240_000 });
}

const scoreBar = (page: Page) => page.locator('#arena-scoreboard');
const barText = async (page: Page) => (await scoreBar(page).textContent().catch(() => null)) ?? '';
const heroName = (page: Page) => page.evaluate(() => document.querySelector('#heroName')?.textContent ?? '');
const mapOf = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.mapNumber);
/** "Red 3 : 5 Blue · flags …". */
const score = (text: string) => { const m = /(\d+) : (\d+)/.exec(text); return m ? Number(m[1]) + Number(m[2]) : -1; };

test('the Arena guild war, played by the machine', async ({ browser }) => {
  test.skip(!process.env.ARENA_GUILD_WAR, 'Plays a whole guild war (about 25 minutes); needs the Arena program, two guilds in the save and the minimum lowered: set ARENA_GUILD_WAR=1.');
  test.setTimeout(45 * 60_000);
  const contexts: BrowserContext[] = [];
  const pages: Page[] = [];
  const errors: string[] = [];
  const open = async (account: string) => {
    const context = await browser.newContext();
    contexts.push(context);
    const page = await context.newPage();
    page.on('pageerror', (error) => errors.push(`${account}: ${error.message}`));
    await enterArena(page, account);
    return page;
  };
  try {
    for (let i = 0; i < PLAYERS; i++) pages.push(await open(`test${i}`));
    const gm = await open(GM);
    await gm.evaluate(() => (window as unknown as Win).__game.online.say('/startguildwar 90'));

    // Every member signs up for their guild.
    for (const page of pages) {
      await page.evaluate(() => (window as unknown as Win).__game.arena?.open());
      await page.locator('#arena-guild-join').click({ timeout: 60_000 });
      await expect(page.locator('#arena-guild-leave')).toBeVisible({ timeout: 30_000 });
    }
    await pages[0].screenshot({ path: 'test-results/arena-guild-signup.png' });

    // The war starts: Red against Blue on map 30, each guild on its own side.
    for (const page of pages) await expect(scoreBar(page)).toBeVisible({ timeout: 4 * 60_000 });
    for (const page of pages) await expect.poll(() => mapOf(page), { timeout: 120_000 }).toBe(30);
    // The bar ends with "you: <guild>".
    const sides = await Promise.all(pages.map(async (page) => /you: (.+)$/.exec(await barText(page))?.[1] ?? ''));
    expect(sides.slice(0, 3).every((s) => s === sides[0]) && sides.slice(3).every((s) => s !== sides[0])).toBe(true);
    await pages[0].screenshot({ path: 'test-results/arena-guild-start.png' });
    const names = await Promise.all(pages.map(heroName));

    // Each fighter goes to a flag (spread over the three) and attacks the nearest enemy close by.
    const fight = async () => {
      await Promise.all(pages.map((page, i) => page.evaluate(({ enemies, goal }) => {
        const g = (window as unknown as Win).__game;
        if (g.online.hero.dead || g.mapNumber !== 30) return;
        const near = [...g.online.characters.values()]
          .filter((c) => c.kind === 'player' && !c.dead && enemies.includes(c.name))
          .sort((a, b) => Math.hypot(a.cell.x - g.online.hero.cell.x, a.cell.y - g.online.hero.cell.y) - Math.hypot(b.cell.x - g.online.hero.cell.x, b.cell.y - g.online.hero.cell.y))[0];
        if (near && Math.hypot(near.cell.x - g.online.hero.cell.x, near.cell.y - g.online.hero.cell.y) < 8) { g.combat.attack(near); return; }
        g.online.walkTo(goal);
      }, { enemies: names.filter((_, j) => sides[j] !== sides[i]), goal: FLAGS[i % 3] }).catch(() => undefined)));
    };

    // Points come in (a flag taken or a kill) and show on every bar.
    await expect.poll(async () => { await fight(); return score(await barText(pages[0])); }, { timeout: 8 * 60_000, intervals: [3000] }).toBeGreaterThan(0);
    await pages[0].screenshot({ path: 'test-results/arena-guild-points.png' });

    // On until the end: Victory / Defeat / Draw, then everyone back on the arena map.
    await expect.poll(async () => { await fight(); return /Victory|Defeat|Draw/.test(await barText(pages[0])); }, { timeout: 20 * 60_000, intervals: [3000] }).toBe(true);
    const finals = await Promise.all(pages.map(barText));
    await pages[0].screenshot({ path: 'test-results/arena-guild-end.png' });
    console.log(`guild war: ${finals.join(' | ')}`);
    for (const page of pages) await expect.poll(() => mapOf(page), { timeout: 180_000 }).toBe(6);
    expect(errors).toEqual([]);
  } finally {
    for (const context of contexts) await context.close().catch(() => undefined);
  }
});
