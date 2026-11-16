// Language controls shared by the public account page and the in-game guide frame.
import { gameName, loadGameName, showCommunityLinks } from './gameName.js';
import { currentLanguage, onLanguageChange, savedLanguage, setLanguage, translate } from './i18n.js';

const PICKERS = '[data-site-language], [data-gift-lang], [data-reset-lang], [data-safety-lang]';
let synchronizing = false;

function buttonLanguage(button: HTMLElement): string | undefined {
  return button.dataset.siteLanguage ?? button.dataset.giftLang ?? button.dataset.resetLang ?? button.dataset.safetyLang;
}

function updateLabels(): void {
  const language = currentLanguage();
  for (const button of document.querySelectorAll<HTMLElement>('[data-site-language]')) {
    button.setAttribute('aria-pressed', String(buttonLanguage(button) === language));
  }
  const label = document.body.dataset.pageLabel ?? '';
  document.title = label ? `${gameName()} · ${translate(label) ?? label}` : gameName();
}

async function chooseLanguage(language: string): Promise<void> {
  if (synchronizing) return;
  synchronizing = true;
  try {
    // Existing account sections keep their own state. Update them before translating the rest.
    for (const button of document.querySelectorAll<HTMLButtonElement>(PICKERS)) {
      if (!button.dataset.siteLanguage && buttonLanguage(button) === language) button.click();
    }
    await setLanguage(language);
  } catch {
    updateLabels(); // Keep the previous selection if the translation file is unavailable.
  } finally {
    synchronizing = false;
  }
}

document.addEventListener('click', (event) => {
  const button = event.target instanceof Element ? event.target.closest<HTMLElement>(PICKERS) : null;
  const language = button && buttonLanguage(button);
  if (!synchronizing && (language === 'en' || language === 'vi')) void chooseLanguage(language);
});

onLanguageChange(updateLabels);
void loadGameName().then(() => { showCommunityLinks(); updateLabels(); });
void chooseLanguage(savedLanguage());
