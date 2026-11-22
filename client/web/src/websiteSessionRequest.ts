const privatePaths = ['/portal/account/', '/portal/gift-box/', '/portal/gift-codes/', '/portal/resets/', '/portal/safety/', '/portal/topup-milestones/', '/portal/market/', '/portal/jewel-bank/', '/portal/lucky-number/', '/portal/escrow/'];

/** Only existing same-origin account operations may receive the website session marker. */
export function isWebsiteSessionRequest(url: URL, origin: string): boolean {
  return url.origin === origin && (url.pathname === '/portal/login-ticket' || privatePaths.some(path => url.pathname.startsWith(path)));
}

/** Keep action fields, including a new password, while removing the old per-form identity. */
export function websiteSessionBody(body: BodyInit | null | undefined): BodyInit | null | undefined {
  if (typeof body !== 'string') return body;
  try {
    const parsed: unknown = JSON.parse(body);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return body;
    const clean = { ...parsed } as Record<string, unknown>;
    for (const key of Object.keys(clean)) if (['loginname', 'password'].includes(key.toLowerCase())) delete clean[key];
    return JSON.stringify(clean);
  } catch { return body; } // The server rejects malformed JSON; do not invent a successful action.
}
