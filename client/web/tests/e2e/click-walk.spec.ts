import { expect, test } from '@playwright/test';
import { enterWorld } from './helpers.js';

// A left click on the ground walks the hero to that cell, also with NPCs drawn with the player
// model standing next to it (their size "character" once turned into NaN, and they took every click).
test('a click on the ground walks to the clicked cell', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  await enterWorld(page, 'test2');
  await page.waitForTimeout(5000);
  await page.evaluate(`(() => { const g = window.__game; window.__clicks = [];
    const walk = g.online.walkTo.bind(g.online); g.online.walkTo = (c, o) => { window.__clicks.push({ walk: { x: c.x, y: c.y } }); return walk(c, o); };
    const talk = g.online.talkTo.bind(g.online); g.online.talkTo = (n) => { window.__clicks.push({ talk: n.name }); return talk(n); }; })()`);
  // No NPC may have a broken (NaN) size.
  const broken = await page.evaluate(`[...window.__game.online.characters.values()].filter((c) => !Number.isFinite(c.group.scale.x)).map((c) => c.name)`);
  expect(broken).toEqual([]);
  let tried = 0;
  for (const [dx, dy] of [[2, 0], [0, 2], [-2, 0], [0, -2], [3, 3], [-3, -3], [3, -3], [-3, 3]]) {
    if (tried === 4) break;
    // Leave out cells where someone stands (a click there rightly talks or attacks).
    const taken = await page.evaluate(`((dx, dy) => { const g = window.__game, h = g.online.hero;
      return [...g.online.characters.values()].some((c) => Math.abs(c.cell.x - h.cell.x - dx) <= 1 && Math.abs(c.cell.y - h.cell.y - dy) <= 1); })(${dx}, ${dy})`);
    if (taken) continue;
    tried++;
    const target = await page.evaluate(`((dx, dy) => { const g = window.__game, h = g.online.hero, V = g.camera.position.constructor;
      const want = { x: h.cell.x + dx, y: h.cell.y + dy };
      const w = h.group.parent.localToWorld(new V((want.x + 0.5) * 100, (want.y + 0.5) * 100, h.group.position.z));
      g.camera.updateMatrixWorld(); const s = w.project(g.camera); const r = document.querySelector('#view').getBoundingClientRect();
      return { want, x: r.left + (s.x + 1) / 2 * r.width, y: r.top + (1 - s.y) / 2 * r.height }; })(${dx}, ${dy})`) as { want: { x: number; y: number }; x: number; y: number };
    const before = await page.evaluate('window.__clicks.length') as number;
    await page.mouse.click(target.x, target.y);
    await expect.poll(() => page.evaluate('window.__clicks.length')).toBeGreaterThan(before);
    const click = await page.evaluate(`window.__clicks[${before}]`) as { walk?: { x: number; y: number }; talk?: string };
    expect(click.talk, `a ground click at ${JSON.stringify(target.want)} talked to an NPC`).toBeUndefined();
    // The terrain's height moves the hit a little: the clicked cell, or one next to it.
    expect(Math.abs(click.walk!.x - target.want.x)).toBeLessThanOrEqual(1);
    expect(Math.abs(click.walk!.y - target.want.y)).toBeLessThanOrEqual(1);
    await expect.poll(() => page.evaluate('window.__game.online.hero.isWalking'), { timeout: 15_000 }).toBe(false);
    const end = await page.evaluate('({ ...window.__game.online.hero.cell })') as { x: number; y: number };
    expect(end).toEqual(click.walk);
  }
  expect(tried).toBe(4);
});
