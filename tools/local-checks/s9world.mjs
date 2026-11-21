// Sets the demo's first world to a season on the admin panel's Worlds page (test admin from a local file, never printed).
import fs from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-t110/client/web/package.json');
const { chromium } = require('playwright');
const [ADMIN, SEASON] = [process.argv[2] ?? 'http://127.0.0.1:8095', process.argv[3] ?? 'Season9'];
const password = fs.readFileSync(`${process.env.HNMO_WORK || 'C:/hnmo-work'}/demo-admin-pass.txt`, 'utf8').trim();
const b = await chromium.launch({ headless: true });
const p = await b.newPage({ viewport: { width: 1400, height: 1000 } });
try {
  await p.goto(`${ADMIN}/worlds`, { waitUntil: 'networkidle' });
  if (await p.locator('#login-name').count()) {
    await p.fill('#login-name', 'demoadmin');
    await p.fill('#login-password', password);
    await p.click('button[type=submit]');
    await p.waitForTimeout(4000);
    await p.goto(`${ADMIN}/worlds`, { waitUntil: 'networkidle' });
  }
  await p.waitForTimeout(2000);
  console.log('before', JSON.stringify(await p.locator('.world-season-badge').allInnerTexts()));
  await p.locator('button:has-text("Edit")').first().click();
  await p.selectOption('#editSeason', SEASON);
  await p.click('button:has-text("Save Changes")');
  await p.waitForTimeout(3000);
  console.log('after', JSON.stringify(await p.locator('.world-season-badge').allInnerTexts()));
} catch (e) {
  console.log('STOPPED', e.message.split('\n')[0]);
  await p.screenshot({ path: `${process.env.HNMO_WORK || 'C:/hnmo-work'}/s9world-stopped.png` }).catch(() => {});
}
await b.close();
