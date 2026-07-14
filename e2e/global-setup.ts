import { chromium, type FullConfig } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { OPERATOR_URL, OPERATOR_USER, OPERATOR_PASSWORD, OPERATOR_STORAGE } from './env';

/**
 * Autentica al operador UNA sola vez contra Keycloak y guarda la sesión
 * (storageState) para reusarla en todos los tests del operador.
 *
 * Los participantes NO pasan por aquí: entran por código de invitación (anónimos),
 * así que solo el operador necesita este login previo.
 *
 * El front operador usa keycloak-js con onLoad:'login-required', de modo que al
 * abrirlo redirige al formulario de login del realm `umbral`. Llenamos ese
 * formulario estándar de Keycloak (#username / #password / #kc-login) y, al
 * volver, la cookie SSO de Keycloak queda en el storageState: en los tests
 * siguientes keycloak-js reingresa en silencio sin mostrar el formulario.
 */
export default async function globalSetup(_config: FullConfig) {
  mkdirSync(dirname(OPERATOR_STORAGE), { recursive: true });

  const browser = await chromium.launch();
  const page = await browser.newPage({ ignoreHTTPSErrors: true });

  await page.goto(OPERATOR_URL, { waitUntil: 'domcontentloaded' });

  // Redirección automática al login del realm.
  await page.waitForURL(
    (url) => url.href.includes('/realms/umbral/protocol/openid-connect/auth'),
    { timeout: 30_000 },
  );

  await page.fill('#username', OPERATOR_USER);
  await page.fill('#password', OPERATOR_PASSWORD);
  await Promise.all([
    page.waitForURL((url) => url.origin === new URL(OPERATOR_URL).origin, { timeout: 30_000 }),
    page.click('#kc-login'),
  ]);

  // Espera a que la SPA cierre el splash "Verificando sesión…" (sesión lista).
  await page
    .getByText('Verificando sesión')
    .waitFor({ state: 'hidden', timeout: 20_000 })
    .catch(() => { /* si ya no está, seguimos */ });
  await page.waitForLoadState('networkidle').catch(() => { /* best-effort */ });

  await page.context().storageState({ path: OPERATOR_STORAGE });
  await browser.close();
}
