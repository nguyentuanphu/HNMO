// HNMO's account safety in the web game, and the vault's lock as the desktop client has it (CStorageInventory):
//  - The vault's lock button: without a PIN it asks a new 4-digit PIN, the PIN again and the account password
//    (SetVaultPin, C1 83 01); with one it asks the account password to remove it (RemoveVaultPin, C1 83 02). Taking
//    an item out of a locked vault asks the PIN first (UnlockVault, C1 83 00). The server answers with
//    VaultProtectionInformation (C1 83): unprotected, locked, wrong PIN, already locked, unlocked, wrong password.
//    The title says "Storage (Close)" while it's protected, as on the desktop.
//  - The second password is that PIN: the server asks for it (HnmoAccountSafetyState, C2 F5 21, after the game asked
//    once with HnmoAccountSafetyRequest, F5 20) when a trade, the personal store, the vault or a valuable item needs it;
//    the web game shows the desktop's PIN box (generic_confirm_dialog with its number field) and sends it with
//    HnmoAccountSafetyAction (F5 22), which also locks an item or asks its unlock. The server answers in the chat.
//  - Locked items get a line in their tooltip. Alt + right click on an item of the bag or the vault locks it (or asks
//    its unlock with the PIN); /lockitem and /unlockitem do the same from the chat (the desktop has only those).

import { read, type PacketValues } from '../net/packets.js';
import { Storage } from './inventory.js';
import { translate } from '../i18n.js';
import { showConfirm } from '../ui/gameDialogs.js';
import type { TooltipLine } from './itemTooltip.js';

/** A locked item of the state. */
export interface SafetyLock { storage: 'inventory' | 'vault'; slot: number; name: string; unlockAt?: string }

/** The account safety state from the server. */
export interface SafetyState {
  v: number; enabled: boolean; hasPin: boolean; entered: boolean; lockedOutUntil?: string;
  required: { vault: boolean; trade: boolean; store: boolean; valuable: boolean };
  valuableLevel: number; itemLock: { enabled: boolean; waitHours: number }; locks: SafetyLock[];
  event?: { type: 'prompt' | 'pin' | 'lock'; action?: string; result?: string; triesLeft?: number; until?: string; name?: string };
}

/** The vault's protection state (VaultProtectionInformation). */
export const VaultProtection = { Unprotected: 0, Locked: 1, WrongPin: 10, AlreadyLocked: 11, Unlocked: 12, WrongPassword: 13 } as const;

/** The actions of HnmoAccountSafetyAction. */
export const SAFETY_ACTION = { EnterPin: 1, LockItem: 2, UnlockItem: 3 } as const;

export interface AccountSafetyOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** An OK box (CreateOkMessageBox). */
  message: (text: string) => void;
  /** The vault's lock state changed (redraw the vault window). */
  changed: () => void;
}

/** A text of the game in the player's language (the desktop's English text is the key). */
export function text(english: string): string {
  return translate(english) ?? english;
}

export class AccountSafety {
  private current: SafetyState | null = null;
  /** CStorageInventory::m_bLock: the vault has a PIN. */
  vaultProtected = false;
  /** CStorageInventory::m_bCorrectPassword: the PIN was entered. */
  vaultUnlocked = false;
  /** A PIN box is open (only one at a time). */
  private asking = false;

  constructor(private readonly options: AccountSafetyOptions) {}

  /** The last state from the server (for tests and the guide). */
  get state(): SafetyState | null { return this.current; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoAccountSafetyRequest', {});
  }

  /** Whether moving things out of the vault needs the PIN first (CStorageInventory's IsStorageLocked && !IsCorrectPassword). */
  get vaultNeedsPin(): boolean { return this.vaultProtected && !this.vaultUnlocked; }

  /** The lock of an item at a storage's slot, if it's locked. */
  lockAt(storage: number, slot: number): SafetyLock | null {
    const kind = storage === Storage.VAULT ? 'vault' : storage === Storage.INVENTORY ? 'inventory' : null;
    return kind ? this.current?.locks.find((l) => l.storage === kind && l.slot === slot) ?? null : null;
  }

  /** The tooltip lines of a locked item. */
  tooltipLines(storage: number, slot: number): TooltipLine[] {
    const itemLock = this.lockAt(storage, slot);
    if (!itemLock) return [];
    const line = (value: string, color: string): TooltipLine => ({ text: value, color, bold: false, background: null, spacer: false });
    const lines = [line('', 'white'), line(text('Locked (account safety)'), 'red')];
    lines[0].spacer = true;
    if (itemLock.unlockAt) lines.push(line(text('Unlocks at %s (Sydney time)').replace('%s', AccountSafety.sydney(itemLock.unlockAt)), 'yellow'));
    return lines;
  }

  /** A packet from the server: the state (C2 F5 21) or the vault's protection (C1 83). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] === 0xc1 && bytes[2] === 0x83 && bytes.length >= 4) {
      this.vaultState(bytes[3]);
      return true;
    }
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x21) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoAccountSafetyState', bytes);
    let state: SafetyState;
    try {
      state = JSON.parse(Json) as SafetyState;
    } catch {
      console.warn('Account safety: unreadable state');
      return true;
    }
    this.current = state;
    if (state.entered && state.hasPin) this.vaultUnlocked = true;
    if (!state.hasPin) { this.vaultProtected = false; this.vaultUnlocked = false; }
    else this.vaultProtected = true;
    this.options.changed();
    if (state.event?.type === 'prompt' && !state.entered) void this.askPin();
    return true;
  }

  /** CStorageInventory::ProcessToReceiveStorageStatus. */
  private vaultState(value: number): void {
    switch (value) {
      case VaultProtection.Unprotected: this.vaultProtected = false; this.vaultUnlocked = false; break;
      case VaultProtection.Locked: this.vaultProtected = true; this.vaultUnlocked = false; break;
      case VaultProtection.WrongPin: this.options.message(text('Incorrect password')); break;
      case VaultProtection.AlreadyLocked: this.options.message(text('Inventory is already locked')); break;
      case VaultProtection.Unlocked: this.vaultProtected = true; this.vaultUnlocked = true; break;
      case VaultProtection.WrongPassword: this.options.message(text('The password you have entered is incorrect.')); break;
      default: return;
    }
    this.options.changed();
  }

  /** ShowVaultPinVerifyDialog: the 4-digit PIN; it unlocks the vault (UnlockVault). */
  async verifyVaultPin(): Promise<void> {
    const pin = await this.pinBox([text('Password Verification'), text('Choose 4 digits for password')]);
    if (pin !== null) this.options.send('UnlockVault', { Pin: Number(pin), Password: '' });
  }

  /** The second password when the server asks for it (a trade, the store, a valuable item): HnmoAccountSafetyAction. */
  async askPin(): Promise<void> {
    const pin = await this.pinBox([text('Second password'), text('Choose 4 digits for password')]);
    if (pin !== null) this.options.send('HnmoAccountSafetyAction', { Action: SAFETY_ACTION.EnterPin, Vault: false, Slot: 0, Pin: Number(pin) });
  }

  /** The vault's lock button (storage_lock_click): remove the PIN with the account password, or choose a new one. */
  async lockButton(): Promise<void> {
    if (this.vaultProtected) {
      const answer = await showConfirm({
        lines: [text('Warehouse Lock/Unlock'), text('Enter your account password.')],
        input: { masked: true, maxLength: 20 }, tag: 'hnmo-vault-password',
      });
      if (answer.button === 'primary' && answer.text) this.options.send('RemoveVaultPin', { Password: answer.text });
      return;
    }
    const first = await this.pinBox([text('Choose new password'), text('Choose 4 digits for password')]);
    if (first === null) return;
    const again = await this.pinBox([text('Verify new password'), text('Enter password again')]);
    if (again === null) return;
    if (again !== first) { this.options.message(text('Password is incorrect')); return; }
    const answer = await showConfirm({
      lines: [text('Enter your account password.')], input: { masked: true, maxLength: 20 }, tag: 'hnmo-vault-password',
    });
    if (answer.button === 'primary' && answer.text) this.options.send('SetVaultPin', { Pin: Number(first), Password: answer.text });
  }

  /** Alt + right click on an item: lock it, or ask its unlock with the PIN. */
  async toggleLock(storage: number, slot: number): Promise<void> {
    if (!this.current?.itemLock.enabled && !this.lockAt(storage, slot)) {
      this.options.message(text('The item lock isn\'t on for this server.'));
      return;
    }
    const vault = storage === Storage.VAULT;
    if (!this.lockAt(storage, slot)) {
      this.options.send('HnmoAccountSafetyAction', { Action: SAFETY_ACTION.LockItem, Vault: vault, Slot: slot, Pin: 0xffff });
      return;
    }
    const lock = this.lockAt(storage, slot);
    if (lock?.unlockAt) {
      // Locking again cancels the waiting unlock.
      this.options.send('HnmoAccountSafetyAction', { Action: SAFETY_ACTION.LockItem, Vault: vault, Slot: slot, Pin: 0xffff });
      return;
    }
    const pin = this.current?.hasPin ? await this.pinBox([text('Unlock the item'), text('Choose 4 digits for password')]) : '';
    if (pin === null) return;
    this.options.send('HnmoAccountSafetyAction', { Action: SAFETY_ACTION.UnlockItem, Vault: vault, Slot: slot, Pin: pin === '' ? 0xffff : Number(pin) });
  }

  /** A 4-digit number box (Mode::NumericKeypad); null when cancelled. */
  private async pinBox(lines: string[]): Promise<string | null> {
    if (this.asking) return null;
    this.asking = true;
    try {
      const answer = await showConfirm({
        lines, input: { masked: true, maxLength: 4, numericOnly: true }, tag: 'hnmo-safety-pin',
        accept: (value) => /^[0-9]{4}$/.test(value),
      });
      return answer.button === 'primary' && /^[0-9]{4}$/.test(answer.text) ? answer.text : null;
    } finally {
      this.asking = false;
    }
  }

  /** A time in Sydney time. */
  static sydney(iso: string): string {
    return new Intl.DateTimeFormat(undefined, { timeZone: 'Australia/Sydney', weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hour12: false }).format(Date.parse(iso));
  }
}
