import test from 'node:test';
import assert from 'node:assert/strict';
import { readWebsiteBrand } from '../public/js/websiteBrand.js';

test('host colors and logo are a narrow validated public projection', () => {
  assert.deepEqual(readWebsiteBrand({ accentColor: '#A1b2C3', backgroundColor: '#101010', panelColor: '#202020', textColor: '#fafafa', logoUrl: '/images/own-logo.png', adminPath: 'private', email: 'private@example.test' }), {
    accentColor: '#A1b2C3', backgroundColor: '#101010', panelColor: '#202020', textColor: '#fafafa', logoUrl: '/images/own-logo.png',
  });
  assert.deepEqual(readWebsiteBrand({ logoUrl: 'https://example.test/logo.png' }), { logoUrl: 'https://example.test/logo.png' });
});

test('bad metadata cannot become CSS, markup, an external protocol or URL credentials', () => {
  for (const value of [undefined, null, false, 'red', []]) assert.deepEqual(readWebsiteBrand(value), {});
  for (const logoUrl of ['//example.test/logo.png', '/\\example.test/logo.png', 'javascript:alert(1)', 'data:image/svg+xml,test', 'http://example.test/logo.png', 'https://user:secret@example.test/logo.png', '/logo x.png', '/<img>.png', '/'+ 'x'.repeat(501)]) assert.deepEqual(readWebsiteBrand({ logoUrl }), {});
  for (const accentColor of ['red', '#fff', '#123456;display:none', '#12345g', 123456]) assert.deepEqual(readWebsiteBrand({ accentColor }), {});
});
