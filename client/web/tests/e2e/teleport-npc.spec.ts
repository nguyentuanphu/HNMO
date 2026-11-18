import { expect, test } from '@playwright/test';

test('guide documents Teleport NPC, packet codecs encode F5 4A/4B, and TeleportNpcWindow renders warp choices cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Teleport NPC');

  await page.goto('/index.html');
  const result = await page.evaluate(async () => {
    const packets = await import('/js/net/packets.js' as string) as {
      write(group: string, name: string, values: Record<string, unknown>): Uint8Array;
      read<T>(group: string, name: string, bytes: Uint8Array): T;
    };
    const tp = await import('/js/ui/teleportNpcWindow.js' as string) as {
      TeleportNpcWindow: new (send: (name: string, fields: Record<string, unknown>) => void) => {
        handle(bytes: Uint8Array): boolean;
      };
    };

    const sent: { name: string; fields: Record<string, unknown> }[] = [];
    const cmdBytes = packets.write('ClientToServer', 'HnmoTeleportNpcCommand', {
      Action: 1,
      DestinationId: 'devias',
    });
    const decodedCmd = packets.read<{ Action: number; DestinationId: string }>('ClientToServer', 'HnmoTeleportNpcCommand', cmdBytes);

    const stateBytes = packets.write('ServerToClient', 'HnmoTeleportNpcState', {
      Json: JSON.stringify({
        v: 1,
        enabled: true,
        open: true,
        closeAfterWarp: true,
        me: { level: 50, resets: 1, vipLevel: 0, zen: 20000, isPk: false },
        destinations: [
          {
            id: 'devias',
            nameEn: 'Devias Outpost',
            nameVi: 'Devias',
            mapNumber: 2,
            targetX: 211,
            targetY: 41,
            minLevel: 20,
            maxLevel: 0,
            minResets: 0,
            zenCost: 5000,
            vipOnly: false,
            allowPk: true,
            eligible: true,
            reasonEn: '',
            reasonVi: '',
          },
        ],
      }),
    });

    const win = new tp.TeleportNpcWindow((name, fields) => sent.push({ name, fields }));
    const handled = win.handle(stateBytes);
    const panel = document.querySelector('#hnmo-teleport-npc-panel');
    const warpBtn = panel?.querySelector('button.hnmo-btn') as HTMLButtonElement | null;
    warpBtn?.click();

    return {
      cmdHeader: [...cmdBytes.slice(0, 4)],
      stateHeader: [...stateBytes.slice(0, 5)],
      decodedCmd,
      handled,
      panelText: panel?.textContent ?? '',
      sent,
    };
  });

  expect(result.cmdHeader[0]).toBe(0xc1);
  expect(result.cmdHeader[2]).toBe(0xf5);
  expect(result.cmdHeader[3]).toBe(0x4a);
  expect(result.stateHeader[0]).toBe(0xc2);
  expect(result.stateHeader[3]).toBe(0xf5);
  expect(result.stateHeader[4]).toBe(0x4b);
  expect(result.decodedCmd.Action).toBe(1);
  expect(result.decodedCmd.DestinationId).toBe('devias');
  expect(result.handled).toBe(true);
  expect(result.panelText).toContain('Devias Outpost');
  expect(result.panelText).toContain('5,000 Zen');
  expect(result.sent.some((s) => s.name === 'HnmoTeleportNpcCommand' && s.fields.DestinationId === 'devias')).toBe(true);
  expect(errors).toEqual([]);
});
