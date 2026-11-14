// The social windows' small questions (the desktop's FriendDialogs: CGenericConfirmDialog with Yes / No,
// an OK notice and an input field), drawn with generic_confirm_dialog.rml (ui/gameDialogs.ts): they never
// block the page, so packets keep flowing while one is open.

import { showConfirm } from './gameDialogs.js';

const TAG = 'social-dialog';

/** An OK-only notice (FriendDialogs::Notice). */
export async function noticeInGame(text: string): Promise<void> {
  await showConfirm({ lines: [text], tag: TAG });
}

/** A question with an input field (CGenericConfirmDialog's input): the text, or null when cancelled. */
export async function promptInGame(text: string, options: { masked?: boolean; maxLength: number; value?: string; allowEmpty?: boolean }): Promise<string | null> {
  const answer = await showConfirm({
    lines: [text], showCancel: true, tag: TAG,
    input: { masked: options.masked ?? false, maxLength: options.maxLength, initialText: options.value },
    // An empty entry keeps the box open (KeepOpen), unless it may be empty.
    accept: (value) => Boolean(value) || (options.allowEmpty ?? false),
  });
  return answer.button === 'primary' ? answer.text : null;
}

/** A Yes / No question (FriendDialogs::Question). */
export async function askInGame(text: string): Promise<boolean> {
  const answer = await showConfirm({ lines: [text], primaryLabel: 'Yes', showCancel: true, cancelLabel: 'No', tag: TAG });
  return answer.button === 'primary';
}
