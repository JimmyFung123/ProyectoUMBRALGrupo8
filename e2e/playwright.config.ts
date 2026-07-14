import { defineConfig, devices } from '@playwright/test';
import 'dotenv/config';

/**
 * Config E2E de UMBRAL. Serial (workers:1) a propósito: los tests comparten un
 * único stack con estado (misiones/sesiones/equipos en Postgres), así que
 * paralelizar los pisaría entre sí. El día que aislemos datos por test se puede
 * subir la paralelización.
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],

  // Loguea al operador una vez y guarda su sesión de Keycloak (storageState).
  globalSetup: './global-setup.ts',

  use: {
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
