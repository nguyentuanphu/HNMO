// The Arena's battle royale, played by the machine: a game master (testgm) opens the sign-up with /startroyale, then
// ROYALE_PLAYERS accounts (test0…, default 4) sign up from the Arena window. At the start everyone stands somewhere on
// Lorencia (map 0) with a status bar ("4 left · zone (x, y) r 120 · …"); they head for the zone and fight whoever is
// near. Every fall shows on everyone's bar, the fallen are out and go back to the arena map (6); the last one standing
// sees "You won!", the others their place, and the survivor goes home too.
// Needs the Arena program next to the web client (BASE_URL), see docs/plans/arena-2026-11-20.md.
import { test, expect, type Page, type BrowserContext } from '@playwright/test';

interface Fighter { id: number; name: string; kind: string; dead?: boolean; cell: { x: number; y: number } }
interface ArenaGame {
  mapNumber: number;
  online: { hero: Fighter; characters: Map<number, Fighter>; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  combat: { attack(target: unknown): void };
  arena?: { open(): void };
}
type Win = Window & { __game: ArenaGame };

const PLAYERS = Number(process.env.ROYALE_PLAYERS ?? 4);
const GM = process.env.GM_ACCOUNT ?? 'testgm';

async function enterArena(page: Page, account: string): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.locator('.season-arena .choice').first().click({ timeout: 90_000 });
  await page.waitForSelector('#characters .choice', { timeout: 90_000 });
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 240_000 });
}

const bar = (page: Page) => page.locator('#arena-scoreboard');
const barText = async (page: Page) => (await bar(page).textContent().catch(() => null)) ?? '';
const mapOf = (page: Page) => page.evaluate(() => (window as unknown as Win).__game.mapNumber);
const heroName = (page: Page) => page.evaluate(() => document.querySelector('#heroName')?.textContent ?? '');
const leftOf = (text: string) => Number(/(\d+) left/.exec(text)?.[1] ?? -1);

test('the Arena battle royale, played by the machine', async ({ browser }) => {
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

    // The game master opens the sign-up; the match starts 90 s later.
    await gm.evaluate(() => (window as unknown as Win).__game.online.say('/startroyale 90'));
    for (const page of pages) {
      await page.evaluate(() => (window as unknown as Win).__game.arena?.open());
      await page.locator('#arena-royale-join').click({ timeout: 60_000 });
      await expect(page.locator('#arena-royale-leave')).toBeVisible({ timeout: 30_000 });
    }
    await pages[0].screenshot({ path: 'test-results/arena-royale-signup.png' });

    // The start: everyone on Lorencia with the status bar, all standing.
    for (const page of pages) await expect(bar(page)).toBeVisible({ timeout: 4 * 60_000 });
    for (const page of pages) await expect.poll(() => mapOf(page), { timeout: 120_000 }).toBe(0);
    for (const page of pages) expect(leftOf(await barText(page))).toBe(PLAYERS);
    await pages[0].screenshot({ path: 'test-results/arena-royale-start.png' });
    const names = await Promise.all(pages.map(heroName));

    // Each one heads for the zone's centre and attacks the nearest other fighter close by.
    const fightWithZone = async () => {
      const text = await barText(pages[0]);
      await Promise.all(pages.map(async (page, i) => {
        const own = await barText(page);
        await page.evaluate(({ others, text: t }) => {
          const g = (window as unknown as Win).__game;
          if (g.online.hero.dead || g.mapNumber !== 0) return;
          const near = [...g.online.characters.values()]
            .filter((c) => c.kind === 'player' && !c.dead && others.includes(c.name))
            .sort((a, b) => Math.hypot(a.cell.x - g.online.hero.cell.x, a.cell.y - g.online.hero.cell.y) - Math.hypot(b.cell.x - g.online.hero.cell.x, b.cell.y - g.online.hero.cell.y))[0];
          if (near && Math.hypot(near.cell.x - g.online.hero.cell.x, near.cell.y - g.online.hero.cell.y) < 12) { g.combat.attack(near); return; }
          const zone = /→ \((\d+), (\d+)\)/.exec(t);
          if (zone) g.online.walkTo({ x: Number(zone[1]), y: Number(zone[2]) });
        }, { others: names.filter((_, j) => j !== i), text: own || text }).catch(() => undefined);
      }));
    };

    // The first fall shows on every bar.
    await expect.poll(async () => { await fightWithZone(); return leftOf(await barText(pages[0])); }, { timeout: 15 * 60_000, intervals: [3000] }).toBeLessThan(PLAYERS);
    await pages[0].screenshot({ path: 'test-results/arena-royale-first-fall.png' });

    // The fallen go back to the arena map.
    const out = await Promise.all(pages.map(async (page) => (await barText(page)).includes('you are out')));
    const outIndex = out.findIndex(Boolean);
    if (outIndex >= 0) await expect.poll(() => mapOf(pages[outIndex]), { timeout: 120_000 }).toBe(6);

    // Fight on until one stands (or the time ends): one "You won!", the others their place.
    await expect.poll(async () => {
      await fightWithZone();
      const texts = await Promise.all(pages.map(barText));
      return texts.some((t) => t.includes('Winner'));
    }, { timeout: 20 * 60_000, intervals: [3000] }).toBe(true);
    const finals = await Promise.all(pages.map(barText));
    await pages[finals.findIndex((t) => t.includes('Winner'))].screenshot({ path: 'test-results/arena-royale-end.png' });
    const won = finals.filter((t) => t.includes('You won!'));
    console.log(`battle royale of ${PLAYERS}: ${finals.join(' | ')}`);
    expect(won.length).toBeLessThanOrEqual(1);

    // Everyone ends on the arena map.
    for (const page of pages) await expect.poll(() => mapOf(page), { timeout: 180_000 }).toBe(6);
    expect(errors).toEqual([]);
  } finally {
    for (const context of contexts) await context.close().catch(() => undefined);
  }
});
