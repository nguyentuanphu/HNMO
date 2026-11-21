// Blood Slash (HNMO skill 701) hits its target: the server has it as an explicit-target area skill (three hits), so
// the web game sends it as an AreaSkill with the target as ExtraTargetId. testgm's Dark Knight casts it at a monster
// in Noria's fields and the server's hits on that monster are counted.
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

interface Mob { id: number; kind: string; dead: boolean; name?: string; cell: { x: number; y: number } }
interface SlashGame {
  mapNumber: number;
  online: {
    characters: Map<number, Mob>; changingMap?: boolean; goal?: unknown;
    hero: { cell: { x: number; y: number }; isWalking: boolean };
    walkTo(cell: { x: number; y: number }): boolean; cellNextTo(cell: { x: number; y: number }): { x: number; y: number } | null; say(text: string): void;
  };
  combat: {
    cast(target: unknown, cell: { x: number; y: number } | null, skill?: number): boolean; cancel(): void;
    hit(packet: { ObjectId: number; HealthDamage: number; ShieldDamage: number }): void;
    connection: { send(group: string, name: string, fields: unknown): void };
  };
}
type Win = Window & { __game: SlashGame; __hits: { id: number; damage: number; at: number }[]; __sent: string[]; __skillAt: number };

const BLOOD_SLASH = 701;
const FIELDS = [{ x: 152, y: 116 }, { x: 148, y: 125 }, { x: 147, y: 91 }, { x: 140, y: 84 }, { x: 155, y: 120 }, { x: 143, y: 100 }];

async function enterAsKnight(page: Page): Promise<void> {
  await page.goto('/index.html');
  await page.fill('#username', 'testgm');
  await page.fill('#password', 'testgm');
  await page.click('button.primary');
  await page.waitForSelector('.season-column .choice, #servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('.season-column .choice')) await page.click('.season-column .choice >> nth=0');
  else if (await page.isVisible('#servers .choice')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await page.click('#characters .choice:has-text("testgmDk")');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 180_000 });
}

const idle = (page: Page) => page.waitForFunction(() => { const o = (window as unknown as Win).__game.online; return !o.goal && !o.hero.isWalking; }, null, { timeout: 60_000 }).catch(() => {});
const living = (page: Page) => page.evaluate(() => [...(window as unknown as Win).__game.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead && !/ of /.test(c.name ?? '')).length);

test('Blood Slash hits the monster it is cast at', async ({ page }) => {
  test.skip(!(await gameServerAvailable()), 'No live game server runs on 44406.');
  test.setTimeout(8 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterAsKnight(page);
  await page.waitForFunction(() => (window as unknown as Win).__game?.combat?.connection !== undefined, null, { timeout: 120_000 });
  await page.waitForTimeout(3_000);
  if (await page.evaluate(() => (window as unknown as Win).__game.mapNumber) !== 3) {
    await page.evaluate(() => (window as unknown as Win).__game.online.say('/move noria'));
    await page.waitForFunction(() => { const g = (window as unknown as Win).__game; return g.mapNumber === 3 && !g.online.changingMap; }, null, { timeout: 90_000 });
    await page.waitForTimeout(3_000);
  }
  await page.evaluate(() => {
    const w = window as unknown as Win; const c = w.__game.combat;
    w.__hits = []; w.__sent = []; w.__skillAt = 0;
    const hit = c.hit.bind(c);
    c.hit = (packet) => { w.__hits.push({ id: packet.ObjectId & 0x7fff, damage: packet.HealthDamage + packet.ShieldDamage, at: performance.now() }); hit(packet); };
    const send = c.connection.send.bind(c.connection);
    c.connection.send = (group, name, fields) => { w.__sent.push(name); if (/Skill/.test(name) && !w.__skillAt) w.__skillAt = performance.now(); send(group, name, fields); };
  });

  let hitTarget = false;
  const sentNames: string[] = [];
  for (let attempt = 0; attempt < 5 && !hitTarget; attempt++) {
    for (let i = 0; i < FIELDS.length * 2 && (await living(page)) === 0; i++) {
      await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), FIELDS[i % FIELDS.length]);
      await idle(page);
      await page.waitForTimeout(2_000);
    }
    // Next to the nearest monster, then the cast at it.
    await page.evaluate(() => {
      const g = (window as unknown as Win).__game; const hero = g.online.hero.cell;
      const m = [...g.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead && !/ of /.test(c.name ?? ''))
        .sort((a, b) => Math.hypot(a.cell.x - hero.x, a.cell.y - hero.y) - Math.hypot(b.cell.x - hero.x, b.cell.y - hero.y))[0];
      if (m && Math.hypot(m.cell.x - hero.x, m.cell.y - hero.y) > 1.5) { const n = g.online.cellNextTo(m.cell); if (n) g.online.walkTo(n); }
    });
    await idle(page);
    const targetId = await page.evaluate((skill) => {
      const w = window as unknown as Win; const g = w.__game; const hero = g.online.hero.cell;
      const m = [...g.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead && !/ of /.test(c.name ?? ''))
        .sort((a, b) => Math.hypot(a.cell.x - hero.x, a.cell.y - hero.y) - Math.hypot(b.cell.x - hero.x, b.cell.y - hero.y))[0];
      if (!m) return null;
      w.__hits = []; w.__sent = []; w.__skillAt = 0;
      g.combat.cast(m, { ...m.cell }, skill);
      return m.id;
    }, BLOOD_SLASH);
    if (targetId === null) continue;
    // Software rendering runs at a few frames a second: the swing (and so the request) comes late, and a monster that
    // walked off out of the skill's 2 cells isn't hit. Up to 8 s for a hit; else the next monster.
    const read = () => page.evaluate((id) => { const w = window as unknown as Win; return { sent: w.__sent.filter((n) => /Skill|HitRequest/.test(n)), hits: w.__hits.filter((h) => h.id === id && h.damage > 0 && w.__skillAt > 0 && h.at >= w.__skillAt) }; }, targetId);
    let result = await read();
    for (let t = 0; t < 16 && result.hits.length === 0; t++) { await page.waitForTimeout(500); result = await read(); }
    await page.evaluate(() => (window as unknown as Win).__game.combat.cancel());
    sentNames.push(...result.sent);
    // Only the skill's own hits count: none from plain attacks (HitRequest) or from before the skill went out.
    if (result.hits.length === 0 || result.sent.includes('HitRequest')) continue;
    expect(result.sent[0]).toBe('AreaSkill');
    hitTarget = true;
  }
  expect(hitTarget, `no hit after ${sentNames.join(', ')}`).toBe(true);
  await expectNoPageErrors(errors);
});
