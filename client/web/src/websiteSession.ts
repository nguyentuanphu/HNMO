import { deviceId } from './deviceId.js';

const privatePaths = ['/portal/account/', '/portal/gift-box/', '/portal/gift-codes/', '/portal/resets/', '/portal/safety/', '/portal/topup-milestones/', '/portal/market/', '/portal/jewel-bank/', '/portal/lucky-number/', '/portal/escrow/'];
const originalFetch = window.fetch.bind(window);
let installed = false;
/** Adapt existing widgets to the HttpOnly bridge session without copying a password into each form/request. */
export function installSessionTransport(): void {
  if (installed) return;
  installed = true;
  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url = new URL(input instanceof Request ? input.url : String(input), location.href);
    const account = url.origin === location.origin && (url.pathname === '/portal/login-ticket' || privatePaths.some(path => url.pathname.startsWith(path)));
    if (!account) return originalFetch(input, init);
    const headers = new Headers(init?.headers ?? (input instanceof Request ? input.headers : undefined));
    headers.set('X-HNMO-Session', '1');
    let body = init?.body;
    if (typeof body === 'string') {
      try {
        const parsed: unknown = JSON.parse(body);
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
          const clean = { ...parsed } as Record<string, unknown>;
          for (const key of Object.keys(clean)) if (['loginname', 'password'].includes(key.toLowerCase())) delete clean[key];
          body = JSON.stringify(clean);
        }
      } catch { /* The handler rejects malformed JSON. */ }
    }
    const response = await originalFetch(input, { ...init, headers, body, credentials: 'same-origin', cache: 'no-store' });
    if (response.status === 401) window.dispatchEvent(new Event('hnmo-session-expired'));
    return response;
  };
}

export async function hasWebsiteSession(): Promise<boolean> {
  const response = await originalFetch('/portal/session', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Session availability could not be checked.');
  const body: unknown = await response.json();
  if (!body || typeof body !== 'object' || !('authenticated' in body) || typeof body.authenticated !== 'boolean') throw new Error('Session availability could not be checked.');
  return body.authenticated;
}

export async function signInWebsite(loginName: string, password: string): Promise<Response> {
  return originalFetch('/portal/session', { method: 'POST', credentials: 'same-origin', cache: 'no-store', headers: { 'Content-Type': 'application/json', 'X-HNMO-Session': '1' }, body: JSON.stringify({ loginName, password }) });
}

export async function signOutWebsite(): Promise<void> {
  const response = await originalFetch('/portal/session', { method: 'DELETE', credentials: 'same-origin', headers: { 'X-HNMO-Session': '1' }, cache: 'no-store' });
  if (!response.ok) throw new Error('Sign-out was not confirmed.');
}

export async function enterGame(): Promise<void> {
  if (!await hasWebsiteSession()) { location.href = '/account.html?play=1'; return; }
  const response = await fetch('/portal/login-ticket', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-HNMO-Session': '1' }, credentials: 'same-origin', cache: 'no-store', body: JSON.stringify({ deviceId: deviceId() }) });
  if (response.status === 401) { location.href = '/account.html?play=1'; return; }
  const body: unknown = await response.json();
  if (!response.ok || !body || typeof body !== 'object' || !('ticket' in body) || typeof body.ticket !== 'string' || !('loginName' in body) || typeof body.loginName !== 'string') throw new Error('Game entry was not confirmed.');
  location.href = `/index.html#${new URLSearchParams({ ticket: body.ticket, account: body.loginName })}`;
}
