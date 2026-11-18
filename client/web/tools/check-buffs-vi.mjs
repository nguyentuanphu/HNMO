// Checks buff tooltip translations without needing a running game or a browser.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const DEFAULT_FILE = new URL('../../src/bin/Data/Buffs/Buffs.json', import.meta.url);
const FORMAT = /%%|(?<![\d.%])%(?:\d+\$)?[-+ #0]*(?:\d+|\*)?(?:\.\d+)?(?:I64|ll|l|h)?[diuoxXfFeEgGcs%]|\{[A-Za-z0-9_]+(?:,[+-]?\d+)?(?::[^{}]*)?\}/g;
const NUMBERS_AND_PERCENT = /[+-]?\d+(?:[.,]\d+)?%?|%/g;

function checkText(english, vietnamese, label) {
  assert.equal(typeof vietnamese, 'string', `${label}: missing Vietnamese text`);
  assert.ok(vietnamese.trim(), `${label}: empty Vietnamese text`);
  assert.equal(typeof english, 'string', `${label}: English text must be a string`);
  assert.deepEqual(vietnamese.match(FORMAT) ?? [], english.match(FORMAT) ?? [], `${label}: placeholders changed`);
  const numbers = (text) => text.replace(FORMAT, '').match(NUMBERS_AND_PERCENT) ?? [];
  assert.deepEqual(numbers(vietnamese), numbers(english), `${label}: numbers or percentages changed`);
}

const file = JSON.parse(readFileSync(process.argv[2] ?? DEFAULT_FILE, 'utf8'));
assert.ok(Array.isArray(file.buffs) && file.buffs.length, 'Expected a nonempty buffs array');
let nameOnlyDescriptions = 0;
for (const buff of file.buffs) {
  checkText(buff.displayName?.en, buff.displayName?.vi, `Buff ${buff.number} displayName.vi`);
  // Some original entries have no English description. Their Vietnamese-only descriptions
  // explain the existing name and must not introduce any numerical claims or placeholders.
  const englishDescription = buff.description?.en ?? '';
  if (!englishDescription) nameOnlyDescriptions++;
  checkText(englishDescription, buff.description?.vi, `Buff ${buff.number} description.vi`);
}
console.log(`Checked ${file.buffs.length} Vietnamese buff names and descriptions (${nameOnlyDescriptions} name-only descriptions); numbers and placeholders match.`);
