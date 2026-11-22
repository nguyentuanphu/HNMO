/** Only public host-brand fields may be applied; unknown metadata is ignored. */
export interface WebsiteBrand {
  logoUrl?: string;
  accentColor?: string;
  backgroundColor?: string;
  panelColor?: string;
  textColor?: string;
}

/** Validate again at the presentation boundary, including replies from older/custom bridges. */
export function readWebsiteBrand(value: unknown): WebsiteBrand {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const source = value as Record<string, unknown>;
  const brand: WebsiteBrand = {};
  for (const key of ['accentColor', 'backgroundColor', 'panelColor', 'textColor'] as const) {
    if (typeof source[key] === 'string' && /^#[0-9a-f]{6}$/i.test(source[key])) brand[key] = source[key];
  }
  const logo = source.logoUrl;
  if (typeof logo !== 'string' || !logo || logo.length > 500 || /[\s\\<>"']/.test(logo)) return brand;
  if (logo.startsWith('/') && !logo.startsWith('//')) { brand.logoUrl = logo; return brand; }
  try {
    const url = new URL(logo);
    if (url.protocol === 'https:' && !url.username && !url.password) brand.logoUrl = url.href;
  } catch { /* Invalid logos leave the configured game name readable. */ }
  return brand;
}

/** Adds appearance only; never rebuilds a feature panel or changes a gameplay value. */
export function applyWebsiteBrand(value: unknown): void {
  const brand = readWebsiteBrand(value);
  const root = document.documentElement;
  for (const [key, property] of [['accentColor', '--gold'], ['backgroundColor', '--bg'], ['panelColor', '--panel'], ['textColor', '--text']] as const) {
    const color = brand[key];
    if (color) root.style.setProperty(property, color); else root.style.removeProperty(property);
  }
  for (const logo of document.querySelectorAll<HTMLImageElement>('img[data-brand-logo]')) {
    logo.hidden = !brand.logoUrl;
    logo.referrerPolicy = 'no-referrer';
    logo.addEventListener('error', () => { logo.hidden = true; }, { once: true });
    if (brand.logoUrl) logo.src = brand.logoUrl; else logo.removeAttribute('src');
  }
}
