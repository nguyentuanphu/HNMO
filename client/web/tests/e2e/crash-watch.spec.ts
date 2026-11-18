import { expect, test } from '@playwright/test';

test('guide documents Crash watch and /control renders unread crash notice banner and ack flow cleanly', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto('/guide.html');
  await expect(page.locator('body')).toContainText('Crash watch');

  let ackCalled = false;
  await page.route('**/api/control/status', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        webGame: true,
        database: true,
        gameServer: false,
        adminPanel: true,
        launcherEnabled: true,
        setupNeeded: false,
        autoStartEnabled: false,
        serviceInstalled: false,
        backups: [],
        backupTotalBytes: 0,
        disk: { driveRoot: 'C:\\', freeDriveBytes: 5e10, totalDriveBytes: 1e11, saveBytes: 1e6, logsBytes: 1e5, backupsBytes: 0, databaseBytes: 1e5, lowDiskWarningMb: 2048, isLowDisk: false },
        maintenanceSettings: { maxLogDays: 14, maxLogTotalMb: 200, maxLogFileMb: 10, dbLogRetentionDays: 90, autoBackupEnabled: true, autoBackupHourSydney: 4, backupKeepDaily: 7, backupKeepWeekly: 4, backupKeepMonthly: 3, vacuumIntervalHours: 24, lowDiskWarningMb: 2048, autoRestartOnCrash: true, maxCrashRestartsPerHour: 5, crashRestartDelaySeconds: 3 },
        crashWatch: {
          autoRestartOnCrash: true,
          maxRestartsPerHour: 5,
          delaySeconds: 3,
          hasDiscordWebhook: false,
          unacknowledgedCount: ackCalled ? 0 : 1,
          incidents: [
            {
              id: 'cw-1',
              occurredAtSydney: '2026-11-17 10:45:00',
              exitCode: 139,
              autoRestarted: true,
              delaySeconds: 3,
              summaryEn: 'Game server crashed (exit code 139). Auto-restarting in 3s (#1 this hour).',
              summaryVi: 'Máy chủ gặp sự cố (mã 139). Đang tự khởi động lại sau 3 giây (lần 1 trong giờ).',
              acknowledged: ackCalled,
            },
          ],
        },
        adminUrl: 'http://127.0.0.1:8095/',
        gameUrl: 'http://127.0.0.1:8080/',
      }),
    });
  });

  await page.route('**/api/control/logs*', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ files: [], lines: [] }) });
  });

  await page.route('**/api/control/crash-watch/ack', async (route) => {
    ackCalled = true;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ ok: true, cleared: 1 }) });
  });

  await page.goto('/control');
  const banner = page.locator('#crashBanner');
  await expect(banner).toBeVisible();
  await expect(banner).toContainText('139');
  await expect(page.locator('#crashRows')).toContainText('2026-11-17 10:45:00');

  await page.locator('#btnAckCrash').click();
  await expect(banner).toBeHidden();
  expect(ackCalled).toBe(true);
  expect(errors).toEqual([]);
});
