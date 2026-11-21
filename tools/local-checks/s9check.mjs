// Which season the test login lands in: the server columns, the season the page sets, and whether /data/s9/ files load.
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-t110/client/web/package.json');
const { chromium } = require('playwright');
const b = await chromium.launch({ headless: true });
const p = await b.newPage({ viewport: { width: 1280, height: 720 } });
const urls = []; p.on('request', (r) => urls.push(r.url()));
try {
  await p.goto('http://127.0.0.1:8081/index.html');
  await p.fill('#username', 'test3'); await p.fill('#password', 'test3'); await p.click('button.primary');
  await p.waitForSelector('.season-column .choice, #servers .choice', { timeout: 120000 });
  console.log('columns', JSON.stringify(await p.evaluate(() => [...document.querySelectorAll('.season-column')].map((c) => `${c.className}: ${c.querySelectorAll('.choice').length}`))));
  await p.click('.season-column .choice >> nth=0').catch(() => p.click('#servers .choice >> nth=0'));
  await p.waitForSelector('#characters .choice', { timeout: 120000 });
  await p.click('#characters .choice >> nth=0');
  await p.waitForFunction(() => window.__viewerReady, null, { timeout: 240000 });
  await p.waitForTimeout(3000);
  const s9 = urls.filter((u) => u.includes('/s9/'));
  console.log('season', await p.evaluate(() => document.documentElement.dataset.season ?? null), 's9 requests', s9.length, JSON.stringify(s9.filter((u) => /player\.bmd/i.test(u))));
} catch (e) { console.log('STOPPED', e.message.split('\n')[0]); }
await b.close();
