import test from 'node:test';
import assert from 'node:assert/strict';
import { websiteEventTime } from '../public/js/websiteEventTime.js';

test('event time uses Sydney daylight saving and keeps the visitor on the correct preceding date', () => {
  const value = websiteEventTime('2026-11-22T00:00:00Z', 'en', 'America/New_York');
  assert.match(value.sydney, /22 Nov 2026.*11:00/);
  assert.match(value.visitor, /21 Nov 2026.*19:00/);
});

test('Sydney winter offset comes from the timezone database, including Vietnamese formatting', () => {
  for (const language of ['en', 'vi']) {
    const value = websiteEventTime('2026-06-22T00:00:00Z', language, 'UTC');
    assert.match(value.sydney, /10:00/); assert.match(value.visitor, /00:00/);
  }
});

test('an invalid event instant never turns into the current time or an invented event', () => {
  assert.equal(websiteEventTime('not-an-instant', 'vi', 'UTC'), null);
  assert.equal(websiteEventTime('2026-11-22T10:00:00', 'en', 'UTC'), null);
});
