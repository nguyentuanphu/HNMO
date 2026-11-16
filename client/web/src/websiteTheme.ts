// Unknown or missing settings preserve the existing website appearance.
const WEBSITE_THEMES = ['classic', 'azure', 'ember', 'jade', 'violet', 'parchment'] as const;

/** Applies only a supported public website appearance. */
export function applyWebsiteTheme(value: unknown): void {
  const theme = typeof value === 'string' && WEBSITE_THEMES.some((candidate) => candidate === value) ? value : 'classic';
  document.documentElement.dataset.websiteTheme = theme;
}

/** Loads public settings and applies the appearance before sign-in is configured. */
export async function loadWebsiteSettings(): Promise<string> {
  try {
    const response = await fetch('/website/settings', { cache: 'no-cache' });
    if (!response.ok) return '';
    const settings = (await response.json()) as { googleClientId?: unknown; theme?: unknown };
    applyWebsiteTheme(settings.theme);
    return typeof settings.googleClientId === 'string' ? settings.googleClientId : '';
  } catch {
    return ''; // Classic and ordinary account sign-in remain available offline.
  }
}
