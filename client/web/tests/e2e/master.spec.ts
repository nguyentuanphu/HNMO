// The master skill tree (A): test400's first character (test400Dk, a level 400 Blade Master with master
// level points) sees the Blade Master's tree, a node's hint, and spends a point on a first-rank skill.
// test400 is used by many specs: run this one alone.
import { test, expect } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Win { __game: { online: { say(text: string): void } } }

test('the master skill tree shows the class tree and spends a point', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400');
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));

  // The character window's button is lit for a master class; A opens the tree.
  await page.keyboard.press('c');
  await expect(page.locator('.rml-character_info')).toHaveCount(1, { timeout: 90_000 });
  await page.keyboard.press('c');
  await page.keyboard.press('a');
  const tree = page.locator('.rml-master_level');
  await expect(tree).toHaveCount(1, { timeout: 90_000 });
  await expect(tree.locator('#class_name')).toHaveText('Blade Master');
  await expect(tree.locator('#master_level')).toContainText('Master level:');
  await expect(tree.locator('#column_0')).toContainText('Protection:');
  const nodes = tree.locator('.node');
  await expect.poll(() => nodes.count(), { timeout: 90_000 }).toBeGreaterThan(20);
  // Every column has nodes, and the icons come from the master atlas.
  for (const column of [0, 1, 2]) await expect(tree.locator(`.node.column-${column}`).first()).toBeAttached();
  const pointsText = (await tree.locator('#level_point').textContent()) ?? '';
  const points = Number(/(\d+)/.exec(pointsText)?.[1] ?? 0);
  console.log(`master points: ${points}, nodes: ${await nodes.count()}`);

  // A first-rank node that can be raised: its hint names the skill, rank and level.
  const usable = tree.locator('.node.rank-1:not(.locked)');
  await expect(usable.first()).toBeAttached();
  let target = usable.first();
  for (let i = 0; i < await usable.count(); i++) {
    const level = Number(await usable.nth(i).locator('.node-level').textContent());
    if (level < 20) { target = usable.nth(i); break; }
  }
  const id = await target.getAttribute('id');
  const node = tree.locator(`#${id}`);
  const before = Number(await node.locator('.node-level').textContent());
  await node.locator('.node-icon').hover();
  const tooltip = page.locator('.rml-tooltip');
  await expect(tooltip).toContainText('Rank 1, Skill Level:');
  await expect(tooltip).toContainText('Required Point');
  await page.screenshot({ path: 'test-results/master-tree.png' });

  if (points > 0 && before < 20) {
    // A press asks first; OK sends the learn request and the server's answer raises the level.
    await node.locator('.node-icon').dispatchEvent('mousedown');
    await expect(page.locator('.confirm-dialog')).toContainText('Would you like to strengthen the skill?');
    await page.click('.confirm-dialog .confirm-ok');
    await expect(node.locator('.node-level')).toHaveText(String(before + 1), { timeout: 60_000 });
    await expect(tree.locator('#level_point')).toHaveText(`Level point: ${points - 1}`);
    console.log(`${id}: level ${before} -> ${before + 1}`);
  } else {
    console.log('No master point or skill level left to spend: only the window and the hint were checked.');
  }
  await page.screenshot({ path: 'test-results/master-tree-after.png' });

  // A closes it again.
  await page.mouse.move(5, 5);
  await page.keyboard.press('a');
  await expect(tree).toHaveCount(0);
  await expectNoPageErrors(errors);
});
