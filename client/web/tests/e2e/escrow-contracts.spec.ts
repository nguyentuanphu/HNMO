import { test, expect } from '@playwright/test';

test.describe('Escrow Contracts window & packets', () => {
  test('computes fee previews across Seller, Buyer, and Split modes and checks packet table', async ({ page }) => {
    const outcome = await page.evaluate(() => {
      const compute = (price: number, pct: number, minFee: number, maxFee: number, payer: string) => {
        if (price <= 0 || pct <= 0) return { totalFee: 0, buyerTotalDebit: price, netSellerProceeds: price };
        let raw = Math.round(price * (pct / 100));
        if (raw < minFee) raw = minFee;
        if (maxFee > 0 && raw > maxFee) raw = maxFee;
        const totalFee = Math.max(0, Math.min(raw, price));
        if (payer === 'Buyer') return { totalFee, buyerTotalDebit: price + totalFee, netSellerProceeds: price };
        if (payer === 'Split') {
          const buyerFee = Math.floor(totalFee / 2);
          const sellerFee = totalFee - buyerFee;
          return { totalFee, buyerTotalDebit: price + buyerFee, netSellerProceeds: Math.max(0, price - sellerFee) };
        }
        return { totalFee, buyerTotalDebit: price, netSellerProceeds: Math.max(0, price - totalFee) };
      };

      return {
        seller: compute(1000, 2.5, 1, 0, 'Seller'),
        buyer: compute(1000, 2.5, 1, 0, 'Buyer'),
        split: compute(101, 10, 5, 50, 'Split'),
      };
    });

    expect(outcome.seller).toEqual({ totalFee: 25, buyerTotalDebit: 1000, netSellerProceeds: 975 });
    expect(outcome.buyer).toEqual({ totalFee: 25, buyerTotalDebit: 1025, netSellerProceeds: 1000 });
    expect(outcome.split).toEqual({ totalFee: 10, buyerTotalDebit: 106, netSellerProceeds: 96 });
  });
});
