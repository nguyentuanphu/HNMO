// Unit tests for #18 Ultimate Autoplay improvements: cyan quest sparkle trail, larger sparkles, and live policy refresh.
// Run: node --test client/web/tests/ultimate-autoplay.test.mjs (after npm run build).
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const sparkleGoalMod = await import(pathToFileURL(path.resolve(__dirname, '../public/js/game/sparkleGoal.js')).href);
const sparklesSource = fs.readFileSync(path.resolve(__dirname, '../public/js/game/autoPlaySparkles.js'), 'utf8');
const uapWindowSource = fs.readFileSync(path.resolve(__dirname, '../public/js/ui/ultimateAutoPlayWindow.js'), 'utf8');

const { SPARKLE_COLORS, SPARKLE_POINT_SIZE, onboardingTargetCell, resolveSparkleGoal } = sparkleGoalMod;

test('sparkles use larger particle size with radial glow map and cyan quest color', () => {
  assert.ok(typeof SPARKLE_POINT_SIZE === 'number' && SPARKLE_POINT_SIZE >= 48, `Expected SPARKLE_POINT_SIZE >= 48, got ${SPARKLE_POINT_SIZE}`);
  assert.equal(SPARKLE_COLORS.quest, 0x38bdf8);
  assert.match(sparklesSource, /size:\s*56\b/);
  assert.match(sparklesSource, /DataTexture/);
});

test('resolveSparkleGoal returns cyan quest trail for active same-map guide objective when no autoplay/boss target is set', () => {
  const activeOnboarding = {
    status: 'active',
    current: { where: { map: 0, x: 132, y: 126 } },
  };

  assert.deepEqual(onboardingTargetCell(activeOnboarding, 0), { x: 132, y: 126 });
  assert.equal(onboardingTargetCell(activeOnboarding, 3), null, 'different map must not point to Lorencia coords');
  assert.equal(onboardingTargetCell({ ...activeOnboarding, status: 'done' }, 0), null);
  assert.equal(onboardingTargetCell({ status: 'active', current: { where: { map: 0, x: -1, y: -1 } } }, 0), null);

  assert.deepEqual(
    resolveSparkleGoal({ autoPlayTarget: null, bossCell: null, questCell: onboardingTargetCell(activeOnboarding, 0) }),
    { kind: 'quest', cell: { x: 132, y: 126 } },
  );

  assert.deepEqual(
    resolveSparkleGoal({ autoPlayTarget: { x: 140, y: 120 }, bossCell: { x: 150, y: 150 }, questCell: { x: 132, y: 126 } }),
    { kind: 'autoplay', cell: { x: 140, y: 120 } },
  );

  assert.deepEqual(
    resolveSparkleGoal({ autoPlayTarget: null, bossCell: { x: 150, y: 150 }, questCell: { x: 132, y: 126 } }),
    { kind: 'boss', cell: { x: 150, y: 150 } },
  );
});

test('UltimateAutoPlayWindow exposes unthrottled refreshPolicy for live VIP updates', () => {
  assert.match(uapWindowSource, /refreshPolicy\s*\(\)/);
});
