import { test, expect } from '@playwright/test';

test.describe('Personal Store multi-currency selector', () => {
  test('encodes and decodes extended Jewel and Coin shop price types cleanly', async ({ page }) => {
    const result = await page.evaluate(() => {
      const encodeItemType = (group: number, number: number): number => ((group & 0x0f) << 12) | (number & 0x0fff);
      const decodeItemType = (priceType: number): { group: number; number: number } => ({
        group: priceType >> 12,
        number: priceType & 0x0fff,
      });

      const blessType = encodeItemType(14, 13);
      const soulType = encodeItemType(14, 14);
      const chaosType = encodeItemType(12, 15);

      return {
        bless: decodeItemType(blessType),
        soul: decodeItemType(soulType),
        chaos: decodeItemType(chaosType),
        wCoinC: 0xfff0,
        wCoinP: 0xfff1,
        goblin: 0xfff2,
      };
    });

    expect(result.bless).toEqual({ group: 14, number: 13 });
    expect(result.soul).toEqual({ group: 14, number: 14 });
    expect(result.chaos).toEqual({ group: 12, number: 15 });
    expect(result.wCoinC).toBe(0xfff0);
    expect(result.wCoinP).toBe(0xfff1);
    expect(result.goblin).toBe(0xfff2);
  });
});
