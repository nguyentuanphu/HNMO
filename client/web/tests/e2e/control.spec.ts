import { expect, test } from '@playwright/test';
import { execFileSync, spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import zlib from 'node:zlib';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BRIDGE_DIR = path.resolve(__dirname, '../../bridge');
const PUBLIC_DIR = path.resolve(__dirname, '../../public');

test.describe('/control page logs, retention, backups, and disk usage', () => {
  test('renders disk usage, searches active + gzipped logs, saves settings, backs up and restores in EN + VI', async ({ page }) => {
    const tmpSave = fs.mkdtempSync(path.join(os.tmpdir(), 'hnmo-control-e2e-'));
    const logsDir = path.join(tmpSave, 'logs');
    fs.mkdirSync(logsDir, { recursive: true });
    fs.writeFileSync(path.join(logsDir, 'server.log'), 'live-line: Server ready on port 55901\n', 'utf8');
    fs.writeFileSync(path.join(logsDir, 'database.log'), 'db-line: PostgreSQL checkpoint complete\n', 'utf8');
    fs.writeFileSync(
      path.join(logsDir, 'server.2026-11-15.1.log.gz'),
      zlib.gzipSync(Buffer.from('archived-line: Player Alice traded item\n', 'utf8')),
    );
    fs.writeFileSync(path.join(tmpSave, 'hnmo-state.json'), '{"v":1,"sections":{"test":"before"}}', 'utf8');

    const port = 18088;
    const bridgeDll = path.join(BRIDGE_DIR, 'bin/Release/net10.0/HNMO.dll');
    if (!fs.existsSync(bridgeDll)) {
      execFileSync('dotnet', ['build', path.join(BRIDGE_DIR, 'HNMO.Web.csproj'), '-c', 'Release'], { stdio: 'inherit' });
    }

    const child: ChildProcessWithoutNullStreams = spawn(
      'dotnet',
      [
        bridgeDll,
        `--urls=http://127.0.0.1:${port}`,
        `--HNMO:WebRoot=${PUBLIC_DIR}`,
        `--Launcher:SavePath=${tmpSave}`,
        '--Launcher:Enabled=false',
        `--Launcher:WebPort=${port}`,
      ],
      { cwd: BRIDGE_DIR },
    );

    try {
      const base = `http://127.0.0.1:${port}`;
      for (let i = 0; i < 40; i++) {
        try {
          const res = await fetch(`${base}/health`);
          if (res.ok) break;
        } catch {
          // Wait for bridge startup.
        }
        await new Promise((r) => setTimeout(r, 250));
      }

      await page.goto(`${base}/control`, { waitUntil: 'networkidle' });
      await expect(page.locator('#diskSummary')).toContainText('Drive (');

      // Help toggle and EN/VI language switch
      await page.click('#helpBtn');
      await expect(page.locator('#helpBox')).toBeVisible();
      await page.click('#langVi');
      await expect(page.locator('[data-i18n="title"]')).toHaveText('Bảng điều khiển HNMO');
      await page.click('#langEn');

      // Log search across both active and gzipped logs
      await page.fill('#logSearch', 'archived-line');
      await page.click('#btnLogSearch');
      await expect(page.locator('#logOutput')).toContainText('Player Alice traded item');

      // Save retention settings and verify persisted to host-settings.json
      await page.fill('#cfgMaxLogDays', '30');
      await page.click('#btnSaveMaint');
      await expect(page.locator('#statusMsg')).toContainText('Retention settings saved');
      const savedSettings = JSON.parse(fs.readFileSync(path.join(tmpSave, 'host-settings.json'), 'utf8'));
      expect(savedSettings.Maintenance.MaxLogDays).toBe(30);

      // Backup now, mutate state file, then restore via UI button
      await page.click('#btnBackup');
      await expect(page.locator('#backupRows tr')).toHaveCount(1);
      fs.writeFileSync(path.join(tmpSave, 'hnmo-state.json'), '{"corrupted":true}', 'utf8');
      await page.locator('#backupRows button[data-restore]').first().click();
      await expect(page.locator('#statusMsg')).toContainText('restored');
      expect(fs.readFileSync(path.join(tmpSave, 'hnmo-state.json'), 'utf8')).toContain('"before"');
    } finally {
      child.kill();
      fs.rmSync(tmpSave, { recursive: true, force: true });
    }
  });
});

for (const [language, disk, log, backup, error] of [
  ['de', 'Laufwerk', 'Alle Protokolle', 'Sicherung erstellt: fictional.zip', 'Das Startprogramm ist deaktiviert.'],
  ['zh-CN', '磁盘', '所有日志', '已创建备份：fictional.zip', '启动器已禁用。'],
]) {
  test(`control runtime messages stay localized in ${language}`, async ({ page }) => {
    await page.route('**/api/control/status', route => route.fulfill({ json: {
      disk: { driveRoot: '/', isLowDisk: true, lowDiskWarningMb: 64 },
      backups: [],
      crashWatch: {
        unacknowledgedCount: 1,
        incidents: [{ occurredAtSydney: '2026-11-24 22:01', exitCode: 17, autoRestarted: true, delaySeconds: 12 }],
      },
    } }));
    await page.route('**/api/control/logs?*', route => route.fulfill({ json: { files: [], lines: [] } }));
    await page.route('**/api/control/backup', route => route.fulfill({ json: { fileName: 'fictional.zip' } }));
    await page.route('**/api/control/start', route => route.fulfill({
      status: 400, json: { message: 'Launcher is disabled.', messageKey: 'launcherDisabled' },
    }));

    await page.goto(`/control?lang=${language}`);
    await expect(page.locator('html')).toHaveAttribute('lang', language);
    await expect(page.locator('#diskSummary')).toContainText(disk);
    await expect(page.locator('#crashBannerDetail')).toContainText(/17.*12/);
    await expect(page.locator('#logFileSelect option').first()).toHaveText(log);
    await page.locator('#btnBackup').click();
    await expect(page.locator('#statusMsg')).toHaveText(backup);
    await page.locator('#btnStart').click();
    await expect(page.locator('#statusMsg')).toHaveText(error);
  });
}

test('launcher serves the full console only inside its primary local admin origin', async ({ request }) => {
  const consolePage = '/control/console';
  expect((await request.get(consolePage)).status()).toBe(403);
  expect((await request.get('/control.html')).status()).toBe(404);
  expect((await request.get('/control-recovery.html')).status()).toBe(404);

  const frame = { 'Sec-Fetch-Dest': 'iframe', Referer: 'http://127.0.0.1:8095/' };
  const accepted = await request.get(consolePage, { headers: frame });
  expect(accepted.status()).toBe(200);
  expect(accepted.headers()['content-security-policy']).toContain('frame-ancestors http://127.0.0.1:8095');
  expect(await accepted.text()).toContain('btnCheckPorts');
  expect((await request.get(consolePage, {
    headers: { ...frame, Referer: 'http://127.0.0.1:8096/' },
  })).status()).toBe(403);
  expect((await request.get(consolePage, {
    headers: { ...frame, 'X-Forwarded-For': '192.0.2.1' },
  })).status()).toBe(403);

  const ports = await request.get('/api/control/ports');
  expect(ports.status()).toBe(200);
  expect((await ports.json() as { service: string }[]).map(row => row.service)).toEqual(['web', 'admin', 'game', 'database']);
});
