// One host setting controls the entire public website, independent of world/account selection.
const WEBSITE_THEMES = ['classic', 'azure', 'ember', 'jade', 'violet', 'parchment'] as const;
let settingsRequest: Promise<string> | undefined;

/** Applies only a supported public website appearance. Never reads player preferences or URL themes. */
export function applyWebsiteTheme(value: unknown): void {
  const theme = typeof value === 'string' && WEBSITE_THEMES.some(candidate => candidate === value) ? value : 'classic';
  document.documentElement.dataset.websiteTheme = theme;
}

/** Shares the same host settings response between the early theme loader and sign-in configuration. */
export function loadWebsiteSettings(): Promise<string> {
  settingsRequest ??= readWebsiteSettings();
  return settingsRequest;
}

async function readWebsiteSettings(): Promise<string> {
  try {
    const response = await fetch('/website/settings', { cache: 'no-cache' });
    if (!response.ok) { applyWebsiteTheme('classic'); return ''; }
    const settings = (await response.json()) as { googleClientId?: unknown; theme?: unknown };
    applyWebsiteTheme(settings.theme);
    return typeof settings.googleClientId === 'string' ? settings.googleClientId : '';
  } catch {
    applyWebsiteTheme('classic');
    return ''; // All original features and ordinary sign-in remain available.
  }
}
