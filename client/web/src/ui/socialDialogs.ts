// The social windows' small questions (the desktop's CGenericConfirmDialog with an input field, the
// OK notice and the guild's appointment box), drawn like ui/confirmDialog.ts: they never block the
// page, so packets keep flowing while one is open.

interface DialogButton { label: string; className?: string; value: string }

function dialog(lines: string, buttons: DialogButton[], input?: { masked: boolean; maxLength: number; value?: string }): Promise<{ button: string; text: string }> {
  return new Promise((resolve) => {
    const box = document.createElement('div');
    box.className = 'chip confirm-dialog social-dialog';
    box.setAttribute('role', 'dialog');
    Object.assign(box.style, {
      position: 'fixed', left: '50%', top: '35%', transform: 'translate(-50%, -50%)', zIndex: '96', pointerEvents: 'auto',
      display: 'grid', gap: '10px', padding: '16px', minWidth: 'min(320px, calc(100vw - 32px))', textAlign: 'center',
    });
    const text = document.createElement('div');
    text.style.whiteSpace = 'pre-line';
    text.textContent = lines;
    box.append(text);
    let field: HTMLInputElement | null = null;
    if (input) {
      field = document.createElement('input');
      field.className = 'social-dialog-input';
      field.type = input.masked ? 'password' : 'text';
      field.maxLength = input.maxLength;
      field.autocomplete = 'off';
      field.value = input.value ?? '';
      box.append(field);
    }
    const row = document.createElement('div');
    row.className = 'buttons';
    const done = (button: string) => {
      box.remove();
      window.removeEventListener('keydown', onKey, true);
      resolve({ button, text: field?.value.trim() ?? '' });
    };
    for (const button of buttons) {
      const element = document.createElement('button');
      element.type = 'button';
      element.className = button.className ?? '';
      element.textContent = button.label;
      element.addEventListener('click', () => done(button.value));
      row.append(element);
    }
    box.append(row);
    const onKey = (event: KeyboardEvent) => {
      // Keys typed into the field stay there; the game's hotkeys don't see them.
      if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); done(buttons[0].value); }
      else if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); done('cancel'); }
      else if (field && document.activeElement === field) event.stopPropagation();
    };
    box.addEventListener('pointerdown', (event) => event.stopPropagation());
    window.addEventListener('keydown', onKey, true);
    document.body.append(box);
    (field ?? row.querySelector('button'))?.focus();
  });
}

/** An OK-only notice (FriendDialogs::Notice). */
export async function noticeInGame(text: string): Promise<void> {
  await dialog(text, [{ label: 'OK', className: 'primary confirm-ok', value: 'ok' }]);
}

/** A question with an input field (CGenericConfirmDialog's input): the text, or null when cancelled. */
export async function promptInGame(text: string, options: { masked?: boolean; maxLength: number; value?: string; allowEmpty?: boolean }): Promise<string | null> {
  for (;;) {
    const answer = await dialog(text, [
      { label: 'OK', className: 'primary confirm-ok', value: 'ok' },
      { label: 'Cancel', className: 'confirm-cancel', value: 'cancel' },
    ], { masked: options.masked ?? false, maxLength: options.maxLength, value: options.value });
    if (answer.button !== 'ok') return null;
    if (answer.text || options.allowEmpty) return answer.text;
  }
}

/** A Yes / No question (FriendDialogs::Question). */
export async function askInGame(text: string): Promise<boolean> {
  const answer = await dialog(text, [
    { label: 'Yes', className: 'primary confirm-ok', value: 'yes' },
    { label: 'No', className: 'confirm-cancel', value: 'no' },
  ]);
  return answer.button === 'yes';
}

/** A box of choices; the value of the button pressed, 'cancel' on Escape. */
export async function chooseInGame(text: string, choices: { label: string; value: string; primary?: boolean }[]): Promise<string> {
  const answer = await dialog(text, choices.map((choice) => ({ label: choice.label, value: choice.value, className: choice.primary ? 'primary confirm-ok' : 'confirm-cancel' })));
  return answer.button;
}
