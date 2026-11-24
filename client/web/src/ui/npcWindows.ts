// Handles non-mix, non-shop NpcWindowResponse (C3 30) kinds and Golden Archer (C1 94)
// so the server's NPC dialog never stays open without a web UI or clean CloseNpcRequest:
//   0x09  Lahap (256/546)              -> jewel mix menu (LahapJewelMixRequest) + CloseNpcRequest
//   0x19  Stat Re-Initializer          -> notice + CloseNpcRequest
//   0x25  Julia (547)                  -> warp confirmation (EnterMarketPlaceRequest) or CloseNpcRequest
//   0x26  Lucky Item Combination (579) -> notice + CloseNpcRequest
//   C1 94 EventChipRegistrationResult  -> Golden Archer dialog (EventChipRegistrationRequest / EventChipExitDialog)

import { read, type PacketValues } from '../net/packets.js';
import type { Inventory } from '../game/inventory.js';
import { confirmInGame } from './confirmDialog.js';
import { chooseInGame } from './mixWindow.js';

export const EXTRA_NPC_WINDOW = {
  LAHAP: 0x09,
  STAT_REINITIALIZER: 0x19,
  JULIA: 0x25,
  LUCKY_ITEM: 0x26,
} as const;

const EVENT_CHIP_REGISTRATION_RESULT_CODE = 0x94;

export interface NpcWindowCoordinatorOptions {
  inventory: Inventory;
  send: (name: string, fields: PacketValues) => void;
  onPacket: (listener: (bytes: Uint8Array) => void) => () => void;
  message: (text: string) => void;
  confirm?: (text: string) => Promise<boolean>;
  choose?: (title: string, options: { label: string; lines: readonly string[] }[], cancelLabel: string) => Promise<number | null>;
}

interface EventChipResultPacket {
  Result: number;
  RegisteredCount: number;
  RemainingInventoryCount: number;
}

export function installExtraNpcWindows(options: NpcWindowCoordinatorOptions): () => void {
  const { inventory, send, onPacket, message } = options;
  const confirm = options.confirm ?? confirmInGame;
  const choose = options.choose ?? chooseInGame;
  let busy = false;

  const closeNpc = () => {
    send('CloseNpcRequest', {});
    inventory.openWindow = null;
  };

  const handleExtraWindow = async (kind: number) => {
    if (busy) return;
    busy = true;
    try {
      if (kind === EXTRA_NPC_WINDOW.JULIA) {
        const ok = await confirm('Market Union Member Julia\n\nWarp to Loren Market?');
        if (ok) {
          send('EnterMarketPlaceRequest', {});
          inventory.openWindow = null;
        } else {
          closeNpc();
        }
        return;
      }

      if (kind === EXTRA_NPC_WINDOW.LAHAP) {
        const choice = await choose('Lahap — Jewel Mix', [
          { label: 'Compress Jewel of Bless x10', lines: ['Combine 10 Jewels of Bless into 1 bundle.'] },
          { label: 'Compress Jewel of Bless x20', lines: ['Combine 20 Jewels of Bless into 1 bundle.'] },
          { label: 'Compress Jewel of Bless x30', lines: ['Combine 30 Jewels of Bless into 1 bundle.'] },
          { label: 'Compress Jewel of Soul x10', lines: ['Combine 10 Jewels of Soul into 1 bundle.'] },
          { label: 'Compress Jewel of Soul x20', lines: ['Combine 20 Jewels of Soul into 1 bundle.'] },
          { label: 'Compress Jewel of Soul x30', lines: ['Combine 30 Jewels of Soul into 1 bundle.'] },
        ], 'Close');
        if (choice !== null) {
          const item = choice < 3 ? 0 : 1; // 0 = Bless, 1 = Soul
          const stackSize = choice % 3; // 0 = Ten, 1 = Twenty, 2 = Thirty
          send('LahapJewelMixRequest', { Operation: 0, Item: item, MixingStackSize: stackSize, UnmixingSourceSlot: 0 });
        }
        closeNpc();
        return;
      }

      if (kind === EXTRA_NPC_WINDOW.STAT_REINITIALIZER) {
        message('Stat Re-Initializer is not yet supported in the web client.');
        closeNpc();
        return;
      }

      if (kind === EXTRA_NPC_WINDOW.LUCKY_ITEM) {
        message('Lucky Item combination is not yet supported in the web client.');
        closeNpc();
      }
    } finally {
      busy = false;
    }
  };

  const handleGoldenArcher = async (pkt: EventChipResultPacket) => {
    if (busy) return;
    busy = true;
    try {
      if (pkt.Result === 1) {
        message(`Golden Archer: No Rena found in inventory (Registered: ${pkt.RegisteredCount}).`);
        send('EventChipExitDialog', {});
        return;
      }
      const choice = await choose('Golden Archer', [
        {
          label: 'Register Rena',
          lines: [`Registered: ${pkt.RegisteredCount}`, `In inventory: ${pkt.RemainingInventoryCount}`],
        },
      ], 'Close');
      if (choice === 0) {
        send('EventChipRegistrationRequest', { Type: 0, ItemIndex: 0 });
      } else {
        send('EventChipExitDialog', {});
      }
    } finally {
      busy = false;
    }
  };

  const offInv = inventory.onChange((what) => {
    if (what !== 'window' || inventory.openWindow === null) return;
    const kind = inventory.openWindow;
    if (
      kind === EXTRA_NPC_WINDOW.LAHAP
      || kind === EXTRA_NPC_WINDOW.STAT_REINITIALIZER
      || kind === EXTRA_NPC_WINDOW.JULIA
      || kind === EXTRA_NPC_WINDOW.LUCKY_ITEM
    ) {
      void handleExtraWindow(kind);
    }
  });

  const offPkt = onPacket((bytes) => {
    if (bytes.length >= 4 && bytes[0] === 0xc1 && bytes[2] === EVENT_CHIP_REGISTRATION_RESULT_CODE) {
      const pkt = read<EventChipResultPacket>('ServerToClient', 'EventChipRegistrationResult', bytes);
      void handleGoldenArcher(pkt);
    }
  });

  return () => {
    offInv();
    offPkt();
  };
}
