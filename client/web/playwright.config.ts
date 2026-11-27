// Browser tests of the web game against a running HNMO server and web client
// (BASE_URL, default the developer web client on port 8081). See tests/e2e/README.md.
import { defineConfig } from '@playwright/test';

// The servers these tests start (the bridge in control/performance-mode specs) never report to the real developer hub
// as a host: "off" turns the hub off ("off", not empty: Windows drops an empty environment variable).
process.env.HNMO_DEVELOPER_HUB ??= 'off';

export default defineConfig({
  testDir: 'tests/e2e',
  timeout: 5 * 60_000,
  expect: { timeout: 60_000 },
  workers: 1, // one game server; tests log in with different accounts but share the world
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.BASE_URL ?? 'http://127.0.0.1:8081',
    viewport: { width: 1280, height: 720 },
    // The game's service worker (sw-game.js, the installable web app) would answer requests itself, and
    // Playwright's page.route never sees those: tests that answer for the server must get every request.
    serviceWorkers: 'block',
    screenshot: 'only-on-failure',
    trace: process.env.CI ? 'on-first-retry' : 'off',
    launchOptions: {
      // WebGL without a graphics card
      args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-proxy-server'],
    },
  },
});
