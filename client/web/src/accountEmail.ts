import type { AccountTextKey } from './accountPageTexts.js';
import type { AccountSectionContext } from './accountSections.js';

interface Options {
  text(key: AccountTextKey): string;
  language(): 'en' | 'vi';
  request: AccountSectionContext['request'];
  message(key: AccountTextKey): void;
  error(error: unknown): void;
}

/** Confirmation codes stay in this tab; expiry and single use are enforced by the server. */
export class AccountEmail {
  readonly root = document.createElement('section');
  private readonly status = document.createElement('p');
  private readonly address = document.createElement('input');
  private readonly code = document.createElement('input');
  private readonly buttons: HTMLButtonElement[] = [];
  private email = '';
  private enabled = false;
  private busy = false;
  private loaded = false;

  constructor(private readonly options: Options) {
    this.root.id = 'accountEmailChange'; this.root.hidden = true;
    const heading = document.createElement('h3'); heading.dataset.at = 'emailTitle'; heading.textContent = options.text('emailTitle');
    const help = document.createElement('p'); help.dataset.at = 'emailHelp'; help.textContent = options.text('emailHelp');
    this.status.setAttribute('role', 'status');
    this.address.type = 'email'; this.address.maxLength = 254; this.address.autocomplete = 'email'; this.address.required = true;
    this.code.type = 'text'; this.code.minLength = 24; this.code.maxLength = 24; this.code.pattern = '[a-fA-F0-9]{24}';
    this.code.autocomplete = 'one-time-code'; this.code.required = true;
    this.root.append(heading, help, this.status,
      this.form(this.address, 'emailNew', 'emailNewHint', 'emailSend', 'emailSendHint', 'request'),
      this.form(this.code, 'emailCode', 'emailCodeHint', 'emailConfirm', 'emailConfirmHint', 'confirm'));
  }

  refreshLanguage(signedIn: boolean): void {
    this.root.hidden = !signedIn;
    this.status.textContent = this.loaded ? this.options.text(this.enabled ? 'emailCurrent' : 'emailOff').replace('{0}', this.email || this.options.text('emailNone')) : '';
    for (const control of [this.address, this.code, ...this.buttons]) control.disabled = this.busy || !this.enabled;
  }

  async open(): Promise<void> {
    const state = await this.options.request<{ enabled: boolean; email: string }>('/portal/account/email/state');
    this.enabled = state.enabled; this.email = state.email; this.loaded = true; this.refreshLanguage(true);
  }

  private form(input: HTMLInputElement, caption: AccountTextKey, hint: AccountTextKey, action: AccountTextKey, actionHint: AccountTextKey, operation: 'request' | 'confirm'): HTMLFormElement {
    const form = document.createElement('form'); form.dataset.emailAction = operation;
    const label = document.createElement('label'); const name = document.createElement('span');
    name.dataset.at = caption; name.textContent = this.options.text(caption);
    input.dataset.atTitle = hint; input.title = this.options.text(hint); label.append(name, input);
    const submit = document.createElement('button'); submit.type = 'submit'; submit.dataset.at = action;
    submit.textContent = this.options.text(action); submit.dataset.atTitle = actionHint; submit.title = this.options.text(actionHint);
    this.buttons.push(submit); form.append(label, submit);
    form.addEventListener('submit', event => {
      event.preventDefault(); if (!this.busy && this.enabled) void this.submit(operation);
    });
    return form;
  }

  private async submit(operation: 'request' | 'confirm'): Promise<void> {
    this.busy = true; this.refreshLanguage(true);
    try {
      const result = await this.options.request<{ sent?: boolean; changed?: boolean }>(`/portal/account/email/${operation}`, operation === 'request'
        ? { newEmail: this.address.value.trim(), language: this.options.language() }
        : { code: this.code.value.trim() });
      if (operation === 'request' ? result.sent !== true : result.changed !== true) throw new Error('Invalid email confirmation response.');
      this.code.value = '';
      if (operation === 'confirm') { this.address.value = ''; await this.open(); }
      this.options.message(operation === 'request' ? 'emailSent' : 'emailChanged');
    } catch (error) { this.options.error(error); }
    finally { this.busy = false; this.refreshLanguage(true); }
  }
}
