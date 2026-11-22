import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const webDir = path.resolve(__dirname, '..');

test('guide.html documents the updated realm and balance rules with EN -> VI translations', () => {
  const guideHtml = fs.readFileSync(path.join(webDir, 'public', 'guide.html'), 'utf8');
  const viOverrides = JSON.parse(fs.readFileSync(path.join(webDir, 'tools', 'lang', 'vi-overrides.json'), 'utf8'));
  const viDict = JSON.parse(fs.readFileSync(path.join(webDir, 'public', 'data', 'lang', 'vi.json'), 'utf8')).texts;

  // Stale rule text must be gone.
  assert.doesNotMatch(guideHtml, /guild master in Devias \(level 100\)/i);

  const requiredPhrases = [
    'Join a guild from level 6; create a guild at the Guild Master in Devias (level 150 by default)',
    'From level 6 (outside trades and NPC dialogs); buyers must be in view range',
    'Monsters keep their spawn spot: when lured too far away they return home and heal. Experience in a shared fight splits by damage dealt, and jewels drop by monster level band.',
    "Blood Castle runs for 15 minutes and warping to Crywolf requires level 300. Mini games refuse entry while dead, trading, duelling, or inside another NPC dialog. In Kanturu, strike Maya's hands — her body stays invulnerable.",
    'Quests can only be accepted and finished at the quest NPC.',
    'Riding the Horn of Fenrir requires the second class or higher.',
    'Scheduled rate multipliers in Sydney time announced in chat and on the timetable. When Happy Hour and another bonus event overlap, only the larger multiplier applies (zen rate bonuses apply separately).',
  ];

  for (const phrase of requiredPhrases) {
    assert.ok(guideHtml.includes(phrase), `guide.html is missing English rule text: ${phrase}`);
    assert.ok(
      typeof viOverrides[phrase] === 'string' && viOverrides[phrase].trim().length > 0,
      `vi-overrides.json is missing Vietnamese translation for: ${phrase}`,
    );
    assert.equal(
      viDict[phrase],
      viOverrides[phrase],
      `vi.json must match vi-overrides.json for: ${phrase}`,
    );
  }
});

test('home.html and website.ts expose bilingual EN/VI realm rule summaries', () => {
  const homeHtml = fs.readFileSync(path.join(webDir, 'public', 'home.html'), 'utf8');
  const websiteTs = fs.readFileSync(path.join(webDir, 'src', 'website.ts'), 'utf8');

  assert.ok(homeHtml.includes('id="realm-rules"'), 'home.html should include #realm-rules container');
  assert.ok(homeHtml.includes('data-t="realmRulesTitle"'), 'home.html should bind realmRulesTitle');
  assert.match(websiteTs, /realmRulesTitle:\s*"Key realm rules"/);
  assert.match(websiteTs, /realmRulesTitle:\s*"Quy tắc máy chủ chính"/);
});
