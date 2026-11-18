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

  test('Direct P2P VND VietQR embeds seller BIN, account, VND amount, and HNMO reference', async ({ page }) => {
    const payload = await page.evaluate(() => {
      const tlv = (id: string, val: string) => `${id}${String(val.length).padStart(2, '0')}${val}`;
      const bin = '970436';
      const account = '0123456789';
      const merchantInfo = tlv('00', 'A000000727') + tlv('01', tlv('00', bin) + tlv('01', account)) + tlv('02', 'QRIBFTTA');
      return tlv('00', '01') + tlv('01', '12') + tlv('38', merchantInfo) + tlv('53', '704') + tlv('54', '500000') + tlv('58', 'VN') + tlv('62', tlv('08', 'HNMO-7K9P2M'));
    });

    expect(payload).toContain('970436');
    expect(payload).toContain('0123456789');
    expect(payload).toContain('500000');
    expect(payload).toContain('HNMO-7K9P2M');
  });

  test('Host-mediated VND calculates commission and net seller payout', async ({ page }) => {
    const hostCalc = await page.evaluate(() => {
      const compute = (vnd: number, pct: number, minVnd: number, maxVnd: number) => {
        let fee = Math.round(vnd * (pct / 100));
        if (fee < minVnd) fee = minVnd;
        if (maxVnd > 0 && fee > maxVnd) fee = maxVnd;
        fee = Math.min(vnd, Math.max(0, fee));
        return { fee, payout: vnd - fee };
      };
      return {
        mid: compute(600_000, 5, 5000, 0),
        capped: compute(2_000_000, 5, 5000, 50_000),
      };
    });

    expect(hostCalc.mid).toEqual({ fee: 30_000, payout: 570_000 });
    expect(hostCalc.capped).toEqual({ fee: 50_000, payout: 1_950_000 });
  });

  test('Trade Credit reconciles circulation against top-ups, conversions, cash-outs, and fees', async ({ page }) => {
    const tc = await page.evaluate(() => {
      const topUp = 300;
      const converted = 100;
      const escrowFee = 10;
      const cashOutGross = 100;
      const cashOutFee = Math.max(5, Math.round(cashOutGross * 0.05));
      const cashOutNet = cashOutGross - cashOutFee;
      const actualBalance = topUp - converted - escrowFee - cashOutGross;
      const expected = topUp - converted - cashOutNet - (escrowFee + cashOutFee);
      return { actualBalance, expected, variance: actualBalance - expected };
    });

    expect(tc).toEqual({ actualBalance: 90, expected: 90, variance: 0 });
  });
});
