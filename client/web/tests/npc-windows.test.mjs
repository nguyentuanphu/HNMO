import test from 'node:test';
import assert from 'node:assert/strict';
import { installExtraNpcWindows, EXTRA_NPC_WINDOW } from '../public/js/ui/npcWindows.js';
import { MIX_NPC_WINDOWS } from '../public/js/ui/mixWindow.js';
import { MixType } from '../public/js/game/mixRecipes.js';
import { write } from '../public/js/net/packets.js';

function fakeInventory() {
  const listeners = [];
  return {
    openWindow: null,
    onChange(fn) {
      listeners.push(fn);
      return () => {
        const i = listeners.indexOf(fn);
        if (i >= 0) listeners.splice(i, 1);
      };
    },
    triggerWindow(kind) {
      this.openWindow = kind;
      for (const fn of [...listeners]) fn('window');
    },
  };
}

test('MIX_NPC_WINDOWS maps 0x07 Pet Trainer, 0x11 Elphis, 0x17 Seed Master, 0x18 Seed Researcher', () => {
  assert.equal(MIX_NPC_WINDOWS[0x07], MixType.TRAINER);
  assert.equal(MIX_NPC_WINDOWS[0x11], MixType.ELPIS);
  assert.equal(MIX_NPC_WINDOWS[0x17], MixType.EXTRACT_SEED);
  assert.equal(MIX_NPC_WINDOWS[0x18], MixType.ATTACH_SOCKET);
});

test('installExtraNpcWindows handles 0x09 Lahap, 0x19 Stat Re-Initializer, 0x25 Julia, 0x26 Lucky Item, and C1 94 Golden Archer', async () => {
  const inventory = fakeInventory();
  const sent = [];
  const messages = [];
  let packetHandler = null;
  let confirmResult = true;
  let chooseResult = 0;

  const dispose = installExtraNpcWindows({
    inventory,
    send: (name, fields) => sent.push({ name, fields }),
    onPacket: (fn) => { packetHandler = fn; return () => {}; },
    message: (text) => messages.push(text),
    confirm: async () => confirmResult,
    choose: async () => chooseResult,
  });

  // 0x25 Julia -> EnterMarketPlaceRequest on confirm, CloseNpcRequest on cancel
  confirmResult = true;
  inventory.triggerWindow(EXTRA_NPC_WINDOW.JULIA);
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(sent.at(-1).name, 'EnterMarketPlaceRequest');
  assert.equal(inventory.openWindow, null);

  confirmResult = false;
  inventory.triggerWindow(EXTRA_NPC_WINDOW.JULIA);
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(sent.at(-1).name, 'CloseNpcRequest');
  assert.equal(inventory.openWindow, null);

  // 0x09 Lahap -> LahapJewelMixRequest + CloseNpcRequest
  chooseResult = 1; // Bless x20
  inventory.triggerWindow(EXTRA_NPC_WINDOW.LAHAP);
  await new Promise((r) => setTimeout(r, 10));
  assert.deepEqual(sent.slice(-2), [
    { name: 'LahapJewelMixRequest', fields: { Operation: 0, Item: 0, MixingStackSize: 1, UnmixingSourceSlot: 0 } },
    { name: 'CloseNpcRequest', fields: {} },
  ]);

  // 0x19 & 0x26 -> notice + CloseNpcRequest
  inventory.triggerWindow(EXTRA_NPC_WINDOW.STAT_REINITIALIZER);
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(sent.at(-1).name, 'CloseNpcRequest');

  inventory.triggerWindow(EXTRA_NPC_WINDOW.LUCKY_ITEM);
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(sent.at(-1).name, 'CloseNpcRequest');
  assert.equal(messages.length, 2);

  // Golden Archer (C1 94 EventChipRegistrationResult)
  chooseResult = 0;
  packetHandler(write('ServerToClient', 'EventChipRegistrationResult', {
    Result: 0, RegisteredCount: 5, RemainingInventoryCount: 2, MutoNumber2: 0xffff, MutoNumber3: 0xffff,
  }));
  await new Promise((r) => setTimeout(r, 10));
  assert.deepEqual(sent.at(-1), { name: 'EventChipRegistrationRequest', fields: { Type: 0, ItemIndex: 0 } });

  chooseResult = null;
  packetHandler(write('ServerToClient', 'EventChipRegistrationResult', {
    Result: 0, RegisteredCount: 6, RemainingInventoryCount: 1, MutoNumber2: 0xffff, MutoNumber3: 0xffff,
  }));
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(sent.at(-1).name, 'EventChipExitDialog');

  dispose();
});
