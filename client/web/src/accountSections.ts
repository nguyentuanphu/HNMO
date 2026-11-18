/** An account feature can register a section without changing the dashboard's navigation. */
export interface AccountSection {
  id: string;
  label: Readonly<{ en: string; vi: string }>;
  element: HTMLElement;
  /** Called when an authenticated player opens the section. Credentials stay in the dashboard. */
  open?: (context: AccountSectionContext) => Promise<void>;
}

export interface AccountSectionContext {
  language: 'en' | 'vi';
  /** Same-origin portal calls only; the dashboard supplies the current player's credentials. */
  request<T>(path: string, body?: Readonly<Record<string, unknown>>): Promise<T>;
}

const sections = new Map<string, AccountSection>();
const listeners = new Set<() => void>();

export function accountSections(): readonly AccountSection[] {
  return [...sections.values()];
}

export function onAccountSectionsChanged(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Returns a removal callback for feature modules which unmount their section. */
export function registerAccountSection(section: AccountSection): () => void {
  if (!/^[a-z][a-z0-9-]*$/.test(section.id) || sections.has(section.id)) {
    throw new Error(`Invalid or duplicate account section: ${section.id}`);
  }
  sections.set(section.id, section);
  for (const listener of listeners) listener();
  return () => {
    sections.delete(section.id);
    for (const listener of listeners) listener();
  };
}
