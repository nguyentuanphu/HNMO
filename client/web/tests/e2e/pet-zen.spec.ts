// A collecting pet picks up zen (PetActionCollecter): the game master gets a Panda (/item 13 80), wears it
// in the helper slot, and a zen drop next to the hero makes the Panda fly to it and ask the server for
// it (PickupItemRequest with the drop's id). The zen is a made-up drop fed to the client, so the server
// refuses it; what's checked is the pet's request.
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface PetGame {
  hero: { cell: { x: number; y: number }; companions: { kind: string; position: { x: number; y: number } }[] };
  inventory: { items: Map<number, { group: number; number: number }>; move(a: number, b: number, c: number, d: number): void };
  ground: { drops: Map<number, { id: number; item: { group: number; number: number } | null }>; pickUp(drop: unknown): void };
  online: { say(text: string): void };
  connection: { send(group: string, name: string, fields: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
}
type Win = Window & { __game: PetGame; __pickups: number[] };
const game = <T>(page: Page, f: (g: PetGame) => T) => page.evaluate(`(${f.toString()})(window.__game)`) as Promise<T>;
const OPEN = { timeout: 60_000 };

test('a Panda picks up zen next to the hero', async ({ page }) => {
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'testgm');
  const isPanda = (g: PetGame) => { const it = g.inventory.items.get(8); return !!it && it.group === 13 && it.number === 80; };
  if (!(await game(page, isPanda))) {
    const old = await game(page, (g) => g.inventory.items.has(8));
    if (old) {
      // Something else is worn there: into the bag first, before the Panda comes. The game master's bag can have
      // room for one big item only (the demo's testgm wears a 2x2 Fenrir and has one 2x2 gap): the Panda,
      // picked up first, took that gap and the worn item had nowhere to go.
      const free = await page.evaluate(async () => {
        const { BAG } = await import('/js/game/inventory.js' as string) as { BAG: unknown };
        const inventory = (window as unknown as { __game: { inventory: { items: Map<number, unknown>; firstEmptySlot(s: unknown, g: unknown, i: unknown): number | null; move(a: number, b: number, c: number, d: number): void } } }).__game.inventory;
        const free = inventory.firstEmptySlot(inventory.items, BAG, inventory.items.get(8));
        if (free !== null) inventory.move(0, 8, 0, free);
        return free;
      });
      expect(free, 'the bag has room for the item worn in the helper slot').not.toBeNull();
      await expect.poll(() => game(page, (g) => g.inventory.items.has(8)), OPEN).toBe(false);
    }
    // A Panda from the game master's command, picked up and put in the helper slot (8).
    await game(page, (g) => g.online.say('/item 13 80'));
    await expect.poll(() => game(page, (g) => {
      const drop = [...g.ground.drops.values()].find((d) => d.item?.group === 13 && d.item.number === 80);
      if (!drop) return false;
      g.ground.pickUp(drop);
      return true;
    }), OPEN).toBe(true);
    await expect.poll(() => game(page, (g) => [...g.inventory.items].some(([slot, it]) => slot >= 12 && it.group === 13 && it.number === 80)), OPEN).toBe(true);
    await game(page, (g) => { const slot = [...g.inventory.items].find(([s, it]) => s >= 12 && it.group === 13 && it.number === 80)![0]; g.inventory.move(0, slot, 0, 8); });
    await expect.poll(() => game(page, isPanda), OPEN).toBe(true);
  }
  await expect.poll(() => game(page, (g) => g.hero.companions.map((c) => c.kind)), OPEN).toContain('panda');

  // The pick-up requests the client sends from here on.
  await page.evaluate(() => {
    const w = window as unknown as Win;
    w.__pickups = [];
    const connection = w.__game.connection;
    const send = connection.send.bind(connection);
    connection.send = (group, name, fields) => { if (name === 'PickupItemRequest') w.__pickups.push(Number(fields.ItemId)); return send(group, name, fields); };
  });
  // Zen on the ground two cells from the hero (MoneyDroppedExtended, as the server sends it).
  await page.evaluate(async () => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const g = (window as unknown as Win).__game;
    g.connection.dispatch(write('ServerToClient', 'MoneyDroppedExtended', { IsFreshDrop: true, Id: 0x1234, PositionX: g.hero.cell.x + 2, PositionY: g.hero.cell.y, Amount: 5000 }));
  });
  await expect.poll(() => game(page, (g) => g.ground.drops.has(0x1234)), OPEN).toBe(true);
  await page.screenshot({ path: 'test-results/pet-zen.png' });
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__pickups), OPEN).toContain(0x1234);
  await expectNoPageErrors(errors);
});
