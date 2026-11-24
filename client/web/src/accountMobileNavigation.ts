import type { AccountSection } from './accountSections.js';

const primarySections = new Set(['overview', 'items', 'coins']);

export class AccountMobileNavigation {
  private readonly nav = document.querySelector<HTMLElement>('#accountMobileNav')!;
  private readonly dashboard = document.querySelector<HTMLElement>('#accountDashboard')!;
  private readonly more = document.querySelector<HTMLButtonElement>('#accountMoreButton')!;
  private readonly dialog = document.querySelector<HTMLDialogElement>('#accountMore')!;
  private readonly sections = document.querySelector<HTMLElement>('#accountMoreSections')!;
  private characters = false;

  constructor(
    private readonly language: () => 'en' | 'vi',
    private readonly select: (id: string) => void,
  ) {
    this.nav.addEventListener('click', event => {
      const target = (event.target as Element).closest<HTMLButtonElement>('[data-mobile-section]');
      const section = target?.dataset.mobileSection;
      if (!section) return;
      this.characters = section === 'characters';
      this.select(this.characters ? 'overview' : section);
      if (this.characters) {
        const heading = document.getElementById('accountCharactersHeading');
        heading?.focus({ preventScroll: true });
        heading?.scrollIntoView({ block: 'start' });
      }
    });
    this.more.addEventListener('click', () => {
      this.dialog.showModal();
      this.more.setAttribute('aria-expanded', 'true');
    });
    document.querySelector('#accountMoreClose')!.addEventListener('click', () => this.dialog.close());
    this.dialog.addEventListener('close', () => {
      this.more.setAttribute('aria-expanded', 'false');
      if (!this.dashboard.hidden && this.more.getClientRects().length) this.more.focus();
    });
    matchMedia('(max-width: 600px)').addEventListener('change', event => {
      if (!event.matches) this.dialog.close();
    });
    new MutationObserver(() => {
      if (this.dashboard.hidden) this.dialog.close();
    }).observe(this.dashboard, { attributes: true, attributeFilter: ['hidden'] });
  }

  update(sections: readonly AccountSection[], active: string, signedIn: boolean): void {
    this.nav.hidden = !signedIn;
    if (active !== 'overview') this.characters = false;
    const selection = this.characters ? 'characters' : active;
    for (const button of this.nav.querySelectorAll<HTMLButtonElement>('[data-mobile-section]')) {
      button.setAttribute('aria-current', String(button.dataset.mobileSection === selection));
    }
    this.more.setAttribute('aria-current', String(!primarySections.has(active)));
    this.sections.replaceChildren(...sections.filter(section => !primarySections.has(section.id)).map(section => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.accountSection = section.id;
      button.textContent = section.label[this.language()];
      button.setAttribute('aria-controls', section.element.id);
      button.setAttribute('aria-current', String(section.id === active));
      button.addEventListener('click', () => {
        this.dialog.close();
        this.select(section.id);
      });
      return button;
    }));
  }
}
