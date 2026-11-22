// A skill whose stat requirements the hero doesn't meet isn't cast (the desktop's CanExecuteSkill checks
// CSkillManager::AreSkillAttributeRequirementsMet; the server refuses it too): testgm's Magic Gladiator knows the
// wizard's spells but has 26 energy, so Poison (140 energy) sends nothing and plays nothing, while Energy Ball (no
// requirement) is cast.
import { test, expect, type Page } from '@playwright/test';
import net from 'node:net';
import { expectNoPageErrors } from './helpers.js';

function gameServerAvailable(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = net.connect(44406, '127.0.0.1');
    socket.once('connect', () => { socket.destroy(); resolve(true); });
    socket.once('error', () => resolve(false));
    socket.setTimeout(2_000, () => { socket.destroy(); resolve(false); });
  });
}

interface ReqGame {
  online: { hero: { cell: { x: number; y: number }; isWalking: boolean }; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void; changingMap?: boolean };
  mapNumber: number;
  stats: { energy: number };
  combat: {
    cast(target: unknown, cell: { x: number; y: number } | null, skill?: number): boolean; castable(skill: number): boolean; casting: boolean; cancel(): void; inSafeZone(): boolean;
    connection: { send(group: string, name: string, fields: unknown): void };
    events: { onSkill?: (caster: unknown, skill: number, target: unknown, cell: unknown) => void };
  };
}
type Win = Window & { __game: ReqGame; __sent: string[]; __skills: number[] };

const POISON = 1, ENERGY_BALL = 17;

async function enterAsGladiator(page: Page): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', 'testgm');
  await page.fill('#password', 'testgm');
  await page.click('button.primary');
  await page.waitForSelector('.season-column .choice, #servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('.season-column .choice')) await page.click('.season-column .choice >> nth=0');
  else if (await page.isVisible('#servers .choice')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await page.click('#characters .choice:has-text("testgmMg")');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 180_000 });
}

test('a spell the hero lacks the energy for is not cast', async ({ page }) => {
  test.skip(!(await gameServerAvailable()), 'No live game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterAsGladiator(page);
  await page.waitForFunction(() => (window as unknown as Win).__game?.combat?.connection !== undefined, null, { timeout: 120_000 });
  await page.waitForTimeout(3_000);
  // Out of town (no skills in a safe zone): Lorencia's fields.
  if (await page.evaluate(() => (window as unknown as Win).__game.mapNumber) !== 0) {
    await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
    await page.waitForFunction(() => { const g = (window as unknown as Win).__game; return g.mapNumber === 0 && !g.online.changingMap; }, null, { timeout: 90_000 });
    await page.waitForTimeout(3_000);
  }
  for (const goal of [{ x: 176, y: 126 }, { x: 190, y: 128 }, { x: 205, y: 140 }]) {
    if (!(await page.evaluate(() => (window as unknown as Win).__game.combat.inSafeZone()))) break;
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => page.evaluate(() => (window as unknown as Win).__game.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
  }
  expect(await page.evaluate(() => (window as unknown as Win).__game.combat.inSafeZone())).toBe(false);
  expect(await page.evaluate(() => (window as unknown as Win).__game.stats.energy)).toBeLessThan(140);
  await page.evaluate(() => {
    const w = window as unknown as Win; const c = w.__game.combat;
    w.__sent = []; w.__skills = [];
    const send = c.connection.send.bind(c.connection);
    c.connection.send = (group, name, fields) => { w.__sent.push(name); send(group, name, fields); };
    const onSkill = c.events.onSkill;
    // The hero's own casts only (others near the field cast too).
    c.events.onSkill = (caster, skill, target, cell) => { if (caster === w.__game.online.hero) w.__skills.push(skill); onSkill?.(caster, skill, target, cell); };
  });
  const cast = (skill: number) => page.evaluate((n) => {
    const g = (window as unknown as Win).__game; const w = window as unknown as Win;
    w.__sent = []; w.__skills = [];
    // At the hero's own cell (a spell's target check is the server's; the client's checks come first).
    return { castable: g.combat.castable(n), cast: g.combat.cast((g as unknown as { online: { hero: unknown } }).online.hero, { ...g.online.hero.cell }, n) };
  }, skill);

  const poison = await cast(POISON);
  await page.waitForTimeout(1_500);
  const afterPoison = await page.evaluate(() => { const w = window as unknown as Win; return { sent: w.__sent.filter((n) => /Skill|HitRequest/.test(n)), skills: w.__skills }; });
  expect(afterPoison.sent).toEqual([]);
  expect(afterPoison.skills).toEqual([]);
  expect(poison.castable).toBe(false);
  // Refused at once, as CanExecuteSkill does: the hero doesn't keep the monster as a target for plain attacks.
  expect(poison.cast).toBe(false);

  await page.evaluate(() => (window as unknown as Win).__game.combat.cancel());
  const ball = await cast(ENERGY_BALL);
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__sent.filter((n) => /Skill/.test(n))), { timeout: 10_000 }).toContain('TargetedSkill');
  expect(ball.castable).toBe(true);
  await page.evaluate(() => (window as unknown as Win).__game.combat.cancel());
  await expectNoPageErrors(errors);
});
