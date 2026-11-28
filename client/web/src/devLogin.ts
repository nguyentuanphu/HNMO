// The game's own login form is for developers and the tests only (players log in on the website, owner 2026-11-28).
// It shows with ?devlogin=1 in the address (kept for the tab) or localStorage 'hnmo.devLogin' = '1', and only on a test
// host: this computer (localhost, 127.0.0.1, [::1]) or a name ending in .localhost or .test. On any other host the
// switch is ignored and not remembered: players never see the password form.

/** Whether a host name is a test host (where the developers' login form may show). */
export function isTestHost(hostname: string): boolean {
  const host = hostname.trim().toLowerCase().replace(/\.$/, '');
  if (host === 'localhost' || host === '127.0.0.1' || host === '[::1]' || host === '::1') return true;
  return host.endsWith('.localhost') || host.endsWith('.test');
}

/** Whether the developers' login form shows on this page (the switch, on a test host only). */
export function devLoginOn(where: Pick<Location, 'hostname' | 'search'> = location): boolean {
  if (!isTestHost(where.hostname)) return false;
  const asked = new URLSearchParams(where.search).get('devlogin') === '1';
  try {
    if (asked) sessionStorage.setItem('hnmo.devLogin', '1');
    return asked || sessionStorage.getItem('hnmo.devLogin') === '1' || localStorage.getItem('hnmo.devLogin') === '1';
  } catch {
    return asked;
  }
}
