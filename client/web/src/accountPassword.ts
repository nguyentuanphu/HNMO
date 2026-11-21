import type { AccountTextKey } from './accountPageTexts.js';

interface PasswordFormOptions {
  text(key: AccountTextKey): string;
  request<T>(path: string, body: Readonly<Record<string, unknown>>): Promise<T>;
  message(key: AccountTextKey): void;
  error(error: unknown): void;
  changed(): void;
}

function field(id: string, key: AccountTextKey, hint: AccountTextKey, options: PasswordFormOptions): HTMLLabelElement {
  const label = document.createElement('label');
  const caption = document.createElement('span');
  caption.dataset.at = key;
  caption.textContent = options.text(key);
  const input = document.createElement('input');
  input.id = id; input.type = 'password'; input.autocomplete = 'new-password';
  input.required = true; input.minLength = 3; input.maxLength = 20;
  input.dataset.atTitle = hint; input.title = options.text(hint);
  label.append(caption, input);
  return label;
}

/** Uses the signed-in tab's credentials; new passwords are never saved in browser storage. */
export function mountAccountPasswordForm(root: HTMLElement, options: PasswordFormOptions): void {
  const form = document.createElement('form');
  form.id = 'accountPasswordChange'; form.className = 'panel'; form.hidden = true;
  const heading = document.createElement('h3');
  heading.dataset.at = 'changePassword'; heading.textContent = options.text('changePassword');
  const help = document.createElement('p');
  help.dataset.at = 'passwordHelp'; help.textContent = options.text('passwordHelp');
  const submit = document.createElement('button');
  submit.type = 'submit'; submit.dataset.at = 'changePassword'; submit.textContent = options.text('changePassword');
  submit.dataset.atTitle = 'changePasswordHint'; submit.title = options.text('changePasswordHint');
  form.append(heading, help, field('accountNewPassword', 'newPassword', 'newPasswordHint', options), field('accountConfirmPassword', 'confirmPassword', 'confirmPasswordHint', options), submit);
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (submit.disabled) return;
    const input = form.querySelector<HTMLInputElement>('#accountNewPassword')!;
    const confirmation = form.querySelector<HTMLInputElement>('#accountConfirmPassword')!;
    if (!/^[\x20-\x7e]{3,20}$/.test(input.value)) { options.message('passwordInvalid'); return; }
    if (input.value !== confirmation.value) { options.message('passwordMismatch'); return; }
    submit.disabled = true;
    try {
      const result = await options.request<{ changed: boolean }>('/portal/account/password', { newPassword: input.value });
      if (result.changed !== true) { options.message('serverError'); return; }
      form.reset();
      options.changed();
    } catch (error) { options.error(error); }
    finally { submit.disabled = false; }
  });
  root.append(form);
}
