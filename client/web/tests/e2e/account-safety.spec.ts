// HNMO's account safety in the web game and on the account page.
// In the game: the account safety state (HnmoAccountSafetyState, C2 F5 21) is fed through the game's connection (the
// test server may run without account safety), so what's checked is the second password box when the server asks for
// it (a trade), the tooltip lines of a locked item, Alt + right click (lock and unlock), and the vault's lock as on the
// desktop (VaultProtectionInformation C1 83: the title, the PIN box before an item leaves a locked vault, the lock
// button's set and remove flows, the answers). The packets the game sends for these are recorded, never forwarded:
// the test accounts' vault PINs on the shared server stay as they are. The server's rules are covered by its unit
// tests (AccountSafetyTest) and the website's endpoints by WebsiteAccountSafetyControllerTests.
// On the account page: the panel with answered requests (the second password, the items with lock and unlock, the
// logins and trades), in English and Vietnamese.
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
interface SafetyView {
  vaultProtected: boolean; vaultUnlocked: boolean; vaultNeedsPin: boolean;
  state: { locks: unknown[] } | null;
  tooltipLines(storage: number, slot: number): { text: string }[];
  toggleLock(storage: number, slot: number): Promise<void>;
  handle(bytes: Uint8Array): boolean;
}
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    accountSafety?: SafetyView;
    itemWindows?: { vault: { model: { title: string; storage_locked: boolean; storage_lock_click(): void }; onItemClick?: (slot: number, item: unknown) => boolean } };
    chat?: { input?: unknown };
  };
  __sent: Sent[];
  __feeding?: boolean;
};

const RECORDED = new Set(['HnmoAccountSafetyRequest', 'HnmoAccountSafetyAction', 'UnlockVault', 'SetVaultPin', 'RemoveVaultPin']);

const STATE = (event?: Record<string, unknown>, extra: Record<string, unknown> = {}) => ({
  v: 1, enabled: true, hasPin: true, entered: false,
  required: { vault: true, trade: true, store: true, valuable: false }, valuableLevel: 9,
  itemLock: { enabled: true, waitHours: 24 },
  locks: [{ storage: 'inventory', slot: 12, name: 'Kris +7' }, { storage: 'vault', slot: 3, name: 'Shield', unlockAt: '2026-11-16T10:00:00Z' }],
  event, ...extra,
});

/** The HnmoAccountSafetyState packet (C2 F5 21); the server escapes non-ASCII text, so the JSON is plain ASCII. */
function packet(value: unknown): number[] {
  const text = JSON.stringify(value).replace(/[\u0080-￿]/g, (c) => `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`);
  const json = Buffer.from(text, 'ascii');
  const length = 5 + json.length + 1;
  return [0xc2, length >> 8, length & 0xff, 0xf5, 0x21, ...json, 0];
}

async function feedBytes(page: Page, bytes: number[]): Promise<void> {
  await page.evaluate((data) => {
    const w = window as unknown as Win;
    w.__feeding = true;
    try { w.__game.connection.dispatch(new Uint8Array(data)); } finally { w.__feeding = false; }
  }, bytes);
}

const feed = (page: Page, value: unknown) => feedBytes(page, packet(value));
/** VaultProtectionInformation (C1 04 83 state). */
const vault = (page: Page, state: number) => feedBytes(page, [0xc1, 0x04, 0x83, state]);

/** From now on the account safety takes only the states and vault answers this test feeds. */
async function onlyFedStates(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as Win;
    const safety = w.__game.accountSafety;
    if (!safety) return;
    const handle = safety.handle.bind(safety);
    safety.handle = (bytes: Uint8Array) => {
      const ours = (bytes[0] === 0xc2 && bytes[3] === 0xf5 && bytes[4] === 0x21) || (bytes[0] === 0xc1 && bytes[2] === 0x83);
      if (ours && !w.__feeding) return true;
      return handle(bytes);
    };
  });
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoAccountSafety') || s.name.endsWith('VaultPin') || s.name === 'UnlockVault'));
const lastSent = async (page: Page, name: string) => (await sent(page)).filter((s) => s.name === name).at(-1)?.fields;

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

async function answerPin(page: Page, pin: string): Promise<void> {
  const box = page.locator('.confirm-dialog.hnmo-safety-pin');
  await expect(box).toHaveCount(1, { timeout: 30_000 });
  const input = box.locator('.social-dialog-input');
  await input.fill(pin);
  await box.locator('#gcd_btn_primary').click();
  // Closed, or the next box of the flow (another PIN box, with an empty field) took its place.
  await expect.poll(async () => (await box.count()) === 0 || (await input.inputValue().catch(() => '')) === '').toBe(true);
}

test('in the game: the second password box, locked items and the vault lock as on the desktop', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript((recorded) => {
    // Record what the game sends from the start; the account safety's and the vault lock's packets aren't forwarded.
    const w = window as unknown as Win;
    w.__sent = [];
    const timer = setInterval(() => {
      const connection = w.__game?.connection;
      if (!connection || (connection as unknown as { __wrapped?: boolean }).__wrapped) return;
      const send = connection.send.bind(connection);
      connection.send = (group, name, fields) => {
        w.__sent.push({ name, fields: { ...(fields ?? {}) } });
        if (!recorded.includes(name)) send(group, name, fields);
      };
      (connection as unknown as { __wrapped?: boolean }).__wrapped = true;
      clearInterval(timer);
    }, 5);
  }, [...RECORDED]);
  await enterWorld(page, 'test5');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoAccountSafetyRequest')), { timeout: 60_000 }).toBe(true);
  await page.waitForFunction(() => !!(window as unknown as Win).__game.accountSafety, null, { timeout: 120_000 });
  await onlyFedStates(page);

  // The server asks for the second password (a trade): the PIN box; its answer goes in HnmoAccountSafetyAction.
  await feed(page, STATE({ type: 'prompt', action: 'trade' }));
  await answerPin(page, '1234');
  await expect.poll(() => lastSent(page, 'HnmoAccountSafetyAction')).toEqual({ Action: 1, Vault: false, Slot: 0, Pin: 1234 });

  // Locked items: the tooltip lines (and the waiting unlock's time); Alt + right click locks or asks the unlock.
  const lines = (storage: number, slot: number) => page.evaluate(([s, i]) => (window as unknown as Win).__game.accountSafety!.tooltipLines(s, i).map((l) => l.text), [storage, slot] as const);
  await expect.poll(() => lines(0, 12)).toEqual(['', 'Locked (account safety)']);
  expect((await lines(2, 3))[2]).toMatch(/^Unlocks at .+ \(Sydney time\)$/);
  expect(await lines(0, 13)).toEqual([]);
  await page.evaluate(() => (window as unknown as Win).__game.accountSafety!.toggleLock(0, 20));
  await expect.poll(() => lastSent(page, 'HnmoAccountSafetyAction')).toEqual({ Action: 2, Vault: false, Slot: 20, Pin: 0xffff });
  void page.evaluate(() => (window as unknown as Win).__game.accountSafety!.toggleLock(0, 12));
  await answerPin(page, '0042');
  await expect.poll(() => lastSent(page, 'HnmoAccountSafetyAction')).toEqual({ Action: 3, Vault: false, Slot: 12, Pin: 42 });
  await page.evaluate(() => (window as unknown as Win).__game.accountSafety!.toggleLock(2, 3));
  await expect.poll(() => lastSent(page, 'HnmoAccountSafetyAction')).toEqual({ Action: 2, Vault: true, Slot: 3, Pin: 0xffff });

  // The vault's lock: protected and locked → "Storage (Close)", an item click asks the PIN first (UnlockVault).
  await vault(page, 1);
  const safety = () => page.evaluate(() => { const s = (window as unknown as Win).__game.accountSafety!; return { protected: s.vaultProtected, needsPin: s.vaultNeedsPin }; });
  await expect.poll(safety).toEqual({ protected: true, needsPin: true });
  const model = () => page.evaluate(() => { const m = (window as unknown as Win).__game.itemWindows!.vault.model; return { title: m.title, locked: m.storage_locked }; });
  // The window syncs its model when it draws (the vault isn't open here).
  await page.evaluate(() => (window as unknown as { __game: { itemWindows: { vault: { updateModel(): void } } } }).__game.itemWindows.vault.updateModel());
  await expect.poll(model).toEqual({ title: 'Storage (Close)', locked: true });
  expect(await page.evaluate(() => (window as unknown as Win).__game.itemWindows!.vault.onItemClick?.(0, {}))).toBe(true);
  await answerPin(page, '5555');
  await expect.poll(() => lastSent(page, 'UnlockVault')).toEqual({ Pin: 5555, Password: '' });
  await vault(page, 10);
  await expect(page.locator('.confirm-dialog .gcd-line')).toHaveText('Incorrect password');
  await page.locator('.confirm-dialog #gcd_btn_primary').click();
  await vault(page, 12);
  await expect.poll(safety).toEqual({ protected: true, needsPin: false });
  expect(await page.evaluate(() => (window as unknown as Win).__game.itemWindows!.vault.onItemClick?.(0, {}))).toBe(false);

  // The lock button with a PIN: the account password removes it (RemoveVaultPin).
  void page.evaluate(() => (window as unknown as Win).__game.itemWindows!.vault.model.storage_lock_click());
  const password = page.locator('.confirm-dialog.hnmo-vault-password');
  await expect(password.locator('.gcd-line')).toHaveText(['Warehouse Lock/Unlock', 'Enter your account password.']);
  await password.locator('.social-dialog-input').fill('secret');
  await password.locator('#gcd_btn_primary').click();
  await expect.poll(() => lastSent(page, 'RemoveVaultPin')).toEqual({ Password: 'secret' });

  // Unprotected: a new PIN, again, the account password (SetVaultPin); two different PINs are refused.
  await vault(page, 0);
  await feed(page, STATE(undefined, { hasPin: false, locks: [] }));
  await expect.poll(safety).toEqual({ protected: false, needsPin: false });
  void page.evaluate(() => (window as unknown as Win).__game.itemWindows!.vault.model.storage_lock_click());
  await answerPin(page, '4321');
  await answerPin(page, '9999');
  await expect(page.locator('.confirm-dialog .gcd-line')).toHaveText('Password is incorrect');
  await page.locator('.confirm-dialog #gcd_btn_primary').click();
  void page.evaluate(() => (window as unknown as Win).__game.itemWindows!.vault.model.storage_lock_click());
  await answerPin(page, '4321');
  await answerPin(page, '4321');
  await password.locator('.social-dialog-input').fill('secret');
  await password.locator('#gcd_btn_primary').click();
  await expect.poll(() => lastSent(page, 'SetVaultPin')).toEqual({ Pin: 4321, Password: 'secret' });
  await page.screenshot({ path: 'test-results/account-safety-game.png' });

  // Vietnamese: the lock line of the tooltip.
  await page.locator('.language-picker button[data-lang="vi"]:visible').first().click();
  await feed(page, STATE());
  await expect.poll(() => lines(0, 12), { timeout: 30_000 }).toEqual(['', 'Đã khóa (bảo vệ tài khoản)']);
  await page.locator('.language-picker button[data-lang="en"]:visible').first().click();
  expect(errors).toEqual([]);
  await page.goto('about:blank');
});

test('the account page: second password, item lock, logins and trades', async ({ page }) => {
  const calls: { url: string; body: Record<string, unknown> }[] = [];
  const items = [
    { id: 'a1', character: 'test5Dk', place: 'bag', slot: 12, name: 'Kris +7', locked: false, valuable: false },
    { id: 'a2', character: '', place: 'vault', slot: 3, name: 'Excellent Shield', locked: true, valuable: true },
  ];
  let hasPin = false;
  const state = (result?: string, until?: string) => ({
    v: 1, loginName: 'test5', result, until, online: false, hasPin, pinChangedAt: hasPin ? '2026-11-15T01:00:00Z' : undefined,
    options: { vault: true, trade: true, store: true, valuable: false, valuableLevel: 9, pinChange: true, itemLock: true, waitHours: 24, maxTries: 5, lockoutMinutes: 30, loginDays: 30, tradeDays: 90 },
    logins: [
      { at: '2026-11-15T02:00:00Z', address: '203.0.x.x', source: 'website', success: true },
      { at: '2026-11-15T01:30:00Z', address: '198.51.x.x', source: 'game', success: false },
    ],
    trades: [{ at: '2026-11-14T22:00:00Z', kind: 'trade', character: 'test5Dk', partner: 'test6Dw', gave: ['Kris +7'], got: [], zenGave: 0, zenGot: 150000 }],
    items: items.map((i) => ({ ...i })),
  });
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  await page.route('**/portal/safety/account', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    calls.push({ url: 'account', body });
    if (body.password !== 'test5') return route.fulfill({ status: 401, json: { error: 'login' } });
    return route.fulfill({ json: state() });
  });
  await page.route('**/portal/safety/pin', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    calls.push({ url: 'pin', body });
    if (hasPin && body.oldPin !== '1234') return route.fulfill({ json: state('wrong') });
    hasPin = body.newPin !== '';
    return route.fulfill({ json: state('ok') });
  });
  await page.route('**/portal/safety/item', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    calls.push({ url: 'item', body });
    const item = items.find((i) => i.id === body.itemId)!;
    if (!body.unlock) { item.locked = true; return route.fulfill({ json: state('Locked') }); }
    if (body.pin !== '1234') return route.fulfill({ json: state('wrong') });
    Object.assign(item, { unlockAt: '2026-11-16T03:00:00Z' });
    return route.fulfill({ json: state('UnlockStarted', '2026-11-16T03:00:00Z') });
  });
  page.on('dialog', (dialog) => void dialog.accept());
  await page.addInitScript(() => { try { localStorage.setItem('hnmo.language', 'en'); } catch { /* none */ } });
  await page.goto('/account.html#safety');
  const panel = page.locator('#safety');
  await expect(panel.locator('h2')).toHaveText('Account safety');
  await page.fill('#safetyAccount', 'test5');
  await page.fill('#safetyPassword', 'wrong');
  await panel.locator('#safetyLogin button').click();
  await expect(page.locator('#safetyMessage')).toHaveText('The account name or password is wrong.');
  await page.fill('#safetyPassword', 'test5');
  await panel.locator('#safetyLogin button').click();
  await expect(page.locator('#safetyPinState')).toHaveText('Your account has no second password yet.');
  await expect(page.locator('#safetyRules')).toContainText('Needed to trade.');
  await expect(page.locator('#safetyOldLabel')).toBeHidden();

  // The logins (masked addresses, game or website, success or not) and the trades.
  const logins = page.locator('#safetyLogins .safety-login');
  await expect(logins).toHaveCount(2);
  await expect(logins.nth(0)).toContainText('203.0.x.x');
  await expect(logins.nth(0)).toContainText('website');
  await expect(logins.nth(1).locator('td.fail')).toHaveText('wrong password');
  await expect(page.locator('#safetyTrades .safety-trade')).toContainText('test5Dk → test6Dw');
  await expect(page.locator('#safetyTrades .safety-trade')).toContainText('150,000 zen');

  // Set the second password: four digits twice.
  await page.fill('#safetyNewPin', '1234');
  await page.fill('#safetyRepeatPin', '1243');
  await page.locator('#safetyPinSave').click();
  await expect(page.locator('#safetyMessage')).toHaveText('The two new second passwords differ.');
  await page.fill('#safetyRepeatPin', '1234');
  await page.locator('#safetyPinSave').click();
  await expect(page.locator('#safetyMessage')).toHaveText('Saved.');
  await expect(page.locator('#safetyPinState')).toContainText('Your account has a second password');
  await expect(page.locator('#safetyOldLabel')).toBeVisible();
  expect(calls.at(-1)).toEqual({ url: 'pin', body: { loginName: 'test5', password: 'test5', oldPin: '', newPin: '1234' } });

  // The items: lock one, unlock the other with the second password (it waits).
  const rows = page.locator('#safetyItems .safety-item');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText('test5Dk · bag');
  await rows.nth(0).locator('button').click();
  await expect(page.locator('#safetyMessage')).toHaveText('Kris +7 is locked.');
  await expect(rows.nth(0)).toContainText('locked');
  await rows.nth(1).locator('button[data-action="unlock"]').click();
  await expect(page.locator('#safetyMessage')).toHaveText('Enter your second password for the unlock.');
  await page.fill('#safetyUnlockPin', '1234');
  await rows.nth(1).locator('button[data-action="unlock"]').click();
  await expect(page.locator('#safetyMessage')).toContainText('Excellent Shield unlocks at');
  await expect(rows.nth(1)).toContainText('unlocks');
  expect(calls.at(-1)?.body).toEqual({ loginName: 'test5', password: 'test5', itemId: 'a2', unlock: true, pin: '1234' });
  await page.screenshot({ path: 'test-results/account-safety-page.png', fullPage: true });

  // Vietnamese.
  await panel.locator('[data-safety-lang="vi"]').click();
  await expect(panel.locator('h2')).toHaveText('Bảo vệ tài khoản');
  await expect(page.locator('#safetyPinState')).toContainText('Tài khoản của bạn đã có mật khẩu cấp 2');
  await expect(logins.nth(1).locator('td.fail')).toHaveText('sai mật khẩu');
  await expect(rows.nth(0)).toContainText('túi đồ');
});
