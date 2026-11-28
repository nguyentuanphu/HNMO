import { fileURLToPath } from 'node:url';
import { defineConfig } from '@playwright/test';

const baseURL = process.env.BASE_URL ?? 'http://127.0.0.1:8081';
const adminURL = process.env.ADMIN_URL ?? 'http://127.0.0.1:8095';
const engines = ['chromium', 'firefox', 'webkit'] as const;

export default defineConfig({
  testDir: 'tests/shortcuts',
  timeout: 240_000,
  workers: 1,
  webServer: process.env.SHORTCUTS_START_DEMO === '1' ? [
    {
      command: 'dotnet run -c Release --no-build --no-launch-profile -- -demo -autostart -resolveIP:loopback',
      cwd: fileURLToPath(new URL('../../server/src/Startup', import.meta.url)),
      env: { ASPNETCORE_URLS: adminURL, HNMO_DEVELOPER_HUB: 'off' },
      url: adminURL,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      command: 'dotnet run -c Release --no-build --no-launch-profile',
      cwd: fileURLToPath(new URL('./bridge', import.meta.url)),
      env: { HNMO_DEVELOPER_HUB: 'off' },
      url: baseURL,
      timeout: 120_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ] : undefined,
  retries: 0,
  use: {
    baseURL,
    viewport: { width: 1280, height: 720 },
    trace: { mode: 'retain-on-failure', screenshots: false },
    screenshot: 'only-on-failure',
    serviceWorkers: 'block',
  },
  projects: engines.map((browserName) => ({
    name: browserName,
    use: {
      browserName,
      ...(browserName === 'chromium' && process.platform === 'linux' ? {
        launchOptions: { args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] },
      } : {}),
    },
  })),
});
