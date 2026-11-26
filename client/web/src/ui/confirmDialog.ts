// A question in the game (the desktop's CGenericConfirmDialog, generic_confirm_dialog.rml): the lines,
// OK and Cancel. Doesn't block the page like the browser's confirm(), so packets keep flowing while it's open.

import { showConfirm } from './gameDialogs.js';

export function confirmInGame(text: string, okOnly = false): Promise<boolean> {
  return showConfirm({ lines: [text], showCancel: !okOnly }).then((answer) => answer.button === 'primary');
}

/** A message with only OK (the desktop's CreateOkMessageBox). */
export function messageInGame(text: string): Promise<void> {
  return confirmInGame(text, true).then(() => undefined);
}

/** An input prompt inside the game UI (does not block packets like window.prompt). */
export function promptInGame(
  text: string,
  input: { masked?: boolean; maxLength?: number; numericOnly?: boolean; initialText?: string } = {},
  tag?: string,
): Promise<string | null> {
  return showConfirm({ lines: [text], showCancel: true, input, tag }).then((answer) => (answer.button === 'primary' ? answer.text : null));
}
