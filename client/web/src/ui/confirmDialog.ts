// A question in the game (the desktop's confirm message box): the lines, OK and Cancel. Doesn't block
// the page like the browser's confirm(), so packets keep flowing while it's open.

export function confirmInGame(text: string): Promise<boolean> {
  return new Promise((resolve) => {
    const box = document.createElement('div');
    box.className = 'chip confirm-dialog';
    box.setAttribute('role', 'dialog');
    Object.assign(box.style, {
      position: 'fixed', left: '50%', top: '35%', transform: 'translate(-50%, -50%)', zIndex: '95', pointerEvents: 'auto',
      display: 'grid', gap: '10px', padding: '16px', minWidth: 'min(320px, calc(100vw - 32px))', textAlign: 'center',
    });
    const lines = document.createElement('div');
    lines.style.whiteSpace = 'pre-line';
    lines.textContent = text;
    const buttons = document.createElement('div');
    buttons.className = 'buttons';
    const ok = document.createElement('button');
    ok.type = 'button'; ok.className = 'primary confirm-ok'; ok.textContent = 'OK';
    const cancel = document.createElement('button');
    cancel.type = 'button'; cancel.className = 'confirm-cancel'; cancel.textContent = 'Cancel';
    buttons.append(ok, cancel);
    box.append(lines, buttons);
    const done = (answer: boolean) => { box.remove(); window.removeEventListener('keydown', onKey, true); resolve(answer); };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); done(true); }
      if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); done(false); }
    };
    ok.addEventListener('click', () => done(true));
    cancel.addEventListener('click', () => done(false));
    box.addEventListener('pointerdown', (event) => event.stopPropagation());
    window.addEventListener('keydown', onKey, true);
    document.body.append(box);
    ok.focus();
  });
}
