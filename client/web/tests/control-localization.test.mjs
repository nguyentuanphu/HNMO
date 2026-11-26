import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { runInNewContext } from 'node:vm';

const control = readFileSync(new URL('../public/control.html', import.meta.url), 'utf8');
const script = control.match(/<script>([\s\S]*?)<\/script>/)[1];
const { i18n, consoleCopy, dynamicCopy, locales } = runInNewContext(
  script.slice(0, script.indexOf('    let lang =')) + '\n({ i18n, consoleCopy, dynamicCopy, locales })',
);

function placeholders(copy) {
  return [...copy.matchAll(/\{(\w+)\}/g)].map(match => match[1]).sort();
}

test('all control labels, actions and runtime messages have four complete translations', () => {
  const englishKeys = Object.keys(i18n.en).sort();
  const usedKeys = [...control.matchAll(/data-i18n="([^"]+)"/g)].map(match => match[1]);
  for (const locale of ['en', 'vi', 'de', 'zh']) {
    assert.deepEqual(Object.keys(i18n[locale]).sort(), englishKeys, locale);
    assert.equal(consoleCopy[locale].length, consoleCopy.en.length, locale);

    for (const key of usedKeys) {
      assert.ok(i18n[locale][key]?.trim(), `${locale}: missing HTML label ${key}`);
    }
    for (const [key, text] of Object.entries(i18n[locale])) {
      assert.ok(text.trim(), `${locale}: empty translation ${key}`);
    }
    for (const text of consoleCopy[locale]) {
      assert.ok(text.trim(), `${locale}: empty action label`);
    }
  }

  const dynamicKeys = [...control.matchAll(/(?:data-copy(?:-title)?="|\bcopy\(\s*')([\w]+)/g)].map(match => match[1]);
  for (const key of dynamicKeys) {
    assert.ok(Object.hasOwn(dynamicCopy, key), `missing runtime key ${key}`);
  }
  assert.deepEqual([...locales], ['en', 'vi', 'de', 'zh']);
  for (const [key, translations] of Object.entries(dynamicCopy)) {
    assert.equal(translations.length, locales.length, key);
    for (const [index, text] of translations.entries()) {
      assert.ok(text?.trim(), `${locales[index]}: missing runtime key ${key}`);
      assert.deepEqual(placeholders(text), placeholders(translations[0]), `${locales[index]}: ${key}`);
    }
  }
});
