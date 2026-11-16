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
