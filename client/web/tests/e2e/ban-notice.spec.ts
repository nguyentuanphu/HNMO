import { expect, test } from '@playwright/test';

for (const language of ['en', 'vi'] as const) {
  test(`Login refusal surfaces HnmoBanNotice (F5 29) reason and Sydney expiry (${language})`, async ({ page }) => {
    await page.addInitScript(lang => localStorage.setItem('hnmo.language', lang), language);
    await page.route('**/ban-notice-fixture.html', route => route.fulfill({
      contentType: 'text/html',
      body: `<!doctype html><html><meta charset="utf-8"><body>
        <div id="out"></div>
        <script type="module">
          import { setLanguage } from '/js/i18n.js';
          import { formatBanNoticeMessage } from '/js/net/session.js';
          import { write, read } from '/js/net/packets.js';
          await setLanguage('${language}');
          const payload = {
            kind: 'AccountBan',
            reasonEn: 'Macro abuse',
            reasonVi: 'Dùng macro',
            expiresAtUtc: '2026-11-20T04:30:00Z',
            permanent: false,
            sydneyEn: '20/11/2026 15:30 (Sydney)',
            sydneyVi: '20/11/2026 15:30 (Sydney)',
            messageEn: 'Account is banned (20/11/2026 15:30 (Sydney)). Reason: Macro abuse',
            messageVi: 'Tài khoản đang bị khóa (20/11/2026 15:30 (Sydney)). Lý do: Dùng macro',
          };
          const bytes = write('ServerToClient', 'HnmoBanNotice', { Json: JSON.stringify(payload) });
          const decoded = read('ServerToClient', 'HnmoBanNotice', bytes);
          document.getElementById('out').textContent = JSON.stringify({
            header: [bytes[0], bytes[3], bytes[4]],
            text: formatBanNoticeMessage(JSON.parse(decoded.Json)),
          });
          document.body.dataset.ready = 'true';
        </script></body></html>`,
    }));

    await page.goto('/ban-notice-fixture.html');
    await expect(page.locator('body')).toHaveAttribute('data-ready', 'true');
    const result = JSON.parse((await page.locator('#out').textContent()) ?? '{}') as { header: number[]; text: string };
    expect(result.header).toEqual([0xc2, 0xf5, 0x29]);
    expect(result.text).toContain('20/11/2026 15:30 (Sydney)');
    expect(result.text).toContain(language === 'vi' ? 'Dùng macro' : 'Macro abuse');
  });
}
