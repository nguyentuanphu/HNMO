// HNMO's Season 9 Muun window (Alt+N, F5 2B / F5 2C): it asks for the state, shows the active Muun with its bonus and
// condition, sends the bag actions, and says the system is off outside Season 9. The server is replaced by states fed
// into the window.
import { test, expect, type Page } from '@playwright/test';

interface Sent { name: string; fields: Record<string, unknown> }
type Win = Window & {
  __sent: Sent[];
  __chat: string[];
  __muun: { handle(bytes: Uint8Array): boolean; open(): void; isOpen(): boolean };
};

async function feed(page: Page, payload: unknown): Promise<void> {
  await page.evaluate(async (json) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    (window as unknown as Win).__muun.handle(write('ServerToClient', 'HnmoMuunState', { Json: json }));
  }, JSON.stringify(payload));
}

const luki = {
  slot: 12, group: 13, number: 212, name: 'Luki', level: 1, maxLevel: 4, evolved: false,
  bonusEn: 'Increase Defense +44', bonusVi: 'Tăng phòng thủ +44', bonus: 44, applies: true,
  conditionEn: 'From 18:00 to 24:59 (Sydney time)', conditionVi: 'Từ 18:00 đến 24:59 (giờ Sydney)',
  canEvolve: false, levelUpCost: 10,
};

test('the Muun window shows the active Muun and sends the bag actions', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/index.html');
  await page.evaluate(async () => {
    const { MuunWindow } = await import('/js/ui/muunWindow.js' as string) as {
      MuunWindow: new (opts: { send: (n: string, f: Record<string, unknown>) => void; message: (t: string) => void }) => Win['__muun'];
    };
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    w.__muun = new MuunWindow({ send: (n, f) => w.__sent.push({ name: n, fields: { ...f } }), message: (t) => w.__chat.push(t) });
    w.__muun.open();
  });

  const panel = page.locator('#hnmo-muun-panel');
  await expect(panel).toBeVisible();
  expect(await page.evaluate(() => (window as unknown as Win).__sent[0])).toEqual({ name: 'HnmoMuunCommand', fields: { Action: 1, Slot: 0 } });

  await feed(page, {
    v: 1, enabled: true, active: luki, energy: 12, stones: 0, converters: 1,
    bag: [
      { slot: 12, group: 13, number: 212, name: 'Luki', level: 1, evolved: false, active: true, energy: 4 },
      { slot: 13, group: 13, number: 214, name: 'Tony', level: 1, evolved: false, active: false, energy: 6 },
    ],
    event: { result: 'Equipped', textEn: 'Luki is now your active Muun.', textVi: 'Luki đã thành Muun đồng hành.' },
  });

  await expect(panel.locator('#muun-active')).toContainText('Luki');
  await expect(panel.locator('#muun-active')).toContainText('Increase Defense +44');
  expect(await page.evaluate(() => (window as unknown as Win).__chat)).toContain('Luki is now your active Muun.');

  await panel.locator('[data-muun-slot="13"] button', { hasText: 'Convert' }).click();
  await panel.locator('#muun-level-up').click();
  await panel.locator('[data-muun-slot="13"] button', { hasText: 'Make active' }).click();
  const sent = await page.evaluate(() => (window as unknown as Win).__sent.slice(1).map((s) => s.fields));
  expect(sent).toEqual([{ Action: 5, Slot: 13 }, { Action: 6, Slot: 0 }, { Action: 2, Slot: 13 }]);

  await feed(page, { v: 1, enabled: false, active: null, bag: [], energy: 0 });
  await expect(panel).toContainText('Muuns are off on this server');
  expect(errors).toEqual([]);
});
