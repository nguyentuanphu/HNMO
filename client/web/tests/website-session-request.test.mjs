import test from 'node:test';
import assert from 'node:assert/strict';
import { isWebsiteSessionRequest, websiteSessionBody } from '../public/js/websiteSessionRequest.js';

test('session transport strips only current identity, preserving the requested action and new password', () => {
  const payload = { LoginName: 'another-account', PASSWORD: 'current-secret', newPassword: 'replacement', operationId: 'stable-retry', code: 'ABC', characterName: 'Knight' };
  const result = JSON.parse(websiteSessionBody(JSON.stringify(payload)));
  assert.deepEqual(result, { newPassword: 'replacement', operationId: 'stable-retry', code: 'ABC', characterName: 'Knight' });
  assert.equal(payload.PASSWORD, 'current-secret');
  assert.equal(JSON.stringify(result).includes('current-secret'), false);
});

test('session metadata stays on account routes of this exact origin', () => {
  const origin = 'https://host.test';
  for (const path of ['/portal/account/email/confirm', '/portal/gift-box/collect', '/portal/login-ticket', '/portal/market/buy']) assert.equal(isWebsiteSessionRequest(new URL(path, origin), origin), true);
  for (const path of ['/portal/session', '/portal/register', '/portal/setup', '/portal/google', '/website/data']) assert.equal(isWebsiteSessionRequest(new URL(path, origin), origin), false);
  assert.equal(isWebsiteSessionRequest(new URL('https://outside.test/portal/account/overview'), origin), false);
  assert.equal(isWebsiteSessionRequest(new URL('https://host.test:8443/portal/account/overview'), origin), false);
});

test('malformed and non-object bodies remain errors for the server instead of becoming invented actions', () => {
  for (const value of ['[', 'null', '[]', '5']) assert.equal(websiteSessionBody(value), value);
  assert.equal(websiteSessionBody(undefined), undefined);
});
