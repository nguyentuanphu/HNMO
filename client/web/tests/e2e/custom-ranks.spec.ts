import { expect, test } from '@playwright/test';

test('guide documents Custom ranks/titles, packet codecs encode F5 48/49, and nameplate decorator renders badges cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Custom ranks');

  await page.goto('/index.html');
  const result = await page.evaluate(async () => {
    const packets = await import('/js/net/packets.js' as string) as {
      write(group: string, name: string, values: Record<string, unknown>): Uint8Array;
      read<T>(group: string, name: string, bytes: Uint8Array): T;
    };
    const customRanks = await import('/js/game/customRanks.js' as string) as {
      CustomRanksManager: new (options: { send: (name: string, fields: Record<string, unknown>) => void }) => {
        applyJson(json: string): void;
        nameplateBadge(name: string): { text: string; colorHex: string } | null;
        chatPrefix(name: string): string | null;
      };
    };
    const social = await import('/js/game/social.js' as string) as {
      setCustomRankLookup(fn: ((name: string) => { text: string; colorHex: string } | null) | null): void;
      decorateLabel(character: { id: number; kind: string; name: string }, element: HTMLElement): void;
    };

    const cmdBytes = packets.write('ClientToServer', 'HnmoCustomRanksCommand', {
      Action: 1,
      RankId: 'legend',
    });
    const decodedCmd = packets.read<{ Action: number; RankId: string }>('ClientToServer', 'HnmoCustomRanksCommand', cmdBytes);

    const stateBytes = packets.write('ServerToClient', 'HnmoCustomRanksState', {
      Json: JSON.stringify({
        v: 1,
        enabled: true,
        showByNameplate: true,
        showInChat: true,
        me: null,
        tiers: [],
        players: [{ name: 'Alice', rankId: 'legend', titleEn: '[Legend]', titleVi: '[Huyền Thoại]', colorHex: '#fb7185' }],
      }),
    });
    const decodedState = packets.read<{ Json: string }>('ServerToClient', 'HnmoCustomRanksState', stateBytes);

    const mgr = new customRanks.CustomRanksManager({ send: () => {} });
    mgr.applyJson(decodedState.Json);

    social.setCustomRankLookup((name) => mgr.nameplateBadge(name));
    const el = document.createElement('span');
    social.decorateLabel({ id: 7, kind: 'player', name: 'Alice' }, el);

    return {
      cmdHeader: [...cmdBytes.slice(0, 4)],
      stateHeader: [...stateBytes.slice(0, 5)],
      decodedCmd,
      labelHtml: el.innerHTML,
      chatPrefix: mgr.chatPrefix('Alice'),
    };
  });

  expect(result.cmdHeader[0]).toBe(0xc1);
  expect(result.cmdHeader[2]).toBe(0xf5);
  expect(result.cmdHeader[3]).toBe(0x48);
  expect(result.stateHeader[0]).toBe(0xc2);
  expect(result.stateHeader[3]).toBe(0xf5);
  expect(result.stateHeader[4]).toBe(0x49);
  expect(result.decodedCmd.Action).toBe(1);
  expect(result.decodedCmd.RankId).toBe('legend');
  expect(result.labelHtml).toContain('[Legend]');
  expect(result.labelHtml).toContain('Alice');
  expect(result.chatPrefix).toBe('[Legend]');
  expect(errors).toEqual([]);
});
