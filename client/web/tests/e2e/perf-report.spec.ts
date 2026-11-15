// The performance report (HNMO extra): the settings window's "Performance report" button (and Ctrl+Shift+P)
// records 15 seconds of play, shows a summary in a game window and downloads the whole report as JSON.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { enterWorld } from './helpers.js';

const ACCOUNT = process.env.PERF_ACCOUNT ?? 'test7';

test('the performance report records, shows a summary and downloads', async ({ page }) => {
  test.setTimeout(5 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, ACCOUNT);
  await page.waitForFunction(() => (window as unknown as { __game: { hud?: { doc?: unknown } } }).__game.hud?.doc, null, { timeout: 120_000 });

  // The settings window's Graphics tab has the button.
  await page.keyboard.press('o');
  await page.locator('.option-tab-btn').nth(1).click();
  await page.locator('#hnmo_perf_report .hnmo-perf-button').click();
  const window = page.locator('.hnmo-perf-report');
  await expect(window).toBeVisible({ timeout: 60_000 });
  await expect(window.locator('.pr-fill')).toBeVisible();
  // The settings window closes while it records.
  await expect(page.locator('.option-tab-btn')).toHaveCount(0);
  await expect(window.locator('.pr-download')).toBeVisible({ timeout: 90_000 });

  const report = await page.evaluate(() => (window as unknown as { __game: { perfReport: { lastReport: unknown } } }).__game.perfReport.lastReport) as {
    format: string; frames: { count: number; averageFps: number; sectionsAverageMs: Record<string, number> };
    profile: { available: boolean; topSelf: { name: string; file: string }[] }; sceneAtEnd: { renderer: { calls: number }; canvas: { width: number } };
  };
  expect(report.format).toBe('hnmo-performance-report');
  expect(report.frames.count).toBeGreaterThan(5);
  expect(report.frames.sectionsAverageMs.render).toBeGreaterThan(0);
  expect(report.sceneAtEnd.renderer.calls).toBeGreaterThan(0);
  expect(report.sceneAtEnd.canvas.width).toBeGreaterThan(0);
  // The game page allows the JS Self-Profiling API (the bridge's Document-Policy header).
  expect(report.profile.available).toBe(true);
  expect(report.profile.topSelf.length).toBeGreaterThan(0);
  await expect(window.locator('.pr-head').first()).toBeVisible();
  await expect(window.locator('.pr-line').first()).toContainText('ms');
  await page.screenshot({ path: 'test-results/perf-report.png' });

  const [download] = await Promise.all([page.waitForEvent('download'), window.locator('.pr-download').click()]);
  expect(download.suggestedFilename()).toMatch(/^hnmo-performance-.*\.json$/);
  const saved = JSON.parse(readFileSync(await download.path(), 'utf8')) as { format: string; frameTimesMs: number[] };
  expect(saved.format).toBe('hnmo-performance-report');
  expect(saved.frameTimesMs.length).toBe(report.frames.count);

  // Ctrl+Shift+P records again from anywhere.
  await window.locator('.pr-close').click();
  await expect(window).toHaveCount(0);
  await page.keyboard.press('Control+Shift+KeyP');
  await expect(page.locator('.hnmo-perf-report .pr-fill')).toBeVisible({ timeout: 30_000 });
  await page.goto('about:blank');
  expect(errors, errors.join('\n')).toEqual([]);
});
