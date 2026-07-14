import { chromium, type FullConfig } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import {
  OPERATOR_URL,
  ADMIN_USER,
  ADMIN_PASSWORD,
  ADMIN_STORAGE,
  OPERATOR_USER,
  OPERATOR_PASSWORD,
  OPERATOR_STORAGE,
} from './env';
import { ensureOperatorUser } from './keycloak-admin';

/**
 * Prepara las DOS sesiones que necesita el flujo completo y las guarda como
 * storageState para reusarlas sin volver a loguear:
 *   - Administrador (admin@umbral.local) → crea/activa misiones (pestaña Misiones).
 *   - Operador (operador.e2e@umbral.local) → gestiona la sesión en vivo (pestaña
 *     Sesiones). El front separa la UI por rol de forma excluyente, así que hace
 *     falta un usuario por cada rol (ver env.ts).
 *
 * Los participantes NO pasan por aquí: entran por código de invitación (anónimos).
 *
 * El front operador usa keycloak-js con onLoad:'login-required', de modo que al
 * abrirlo redirige al formulario de login del realm `umbral`. Llenamos ese
 * formulario estándar (#username / #password / #kc-login) y, al volver, la
 * cookie SSO de Keycloak queda en el storageState: en los tests siguientes
 * keycloak-js reingresa en silencio sin mostrar el formulario.
 */
export default async function globalSetup(_config: FullConfig) {
  // El realm no trae usuario `operator`: lo aprovisionamos (idempotente) antes
  // de intentar loguearlo.
  await ensureOperatorUser();

  const browser = await chromium.launch();
  try {
    await loginAndSave(browser, ADMIN_USER, ADMIN_PASSWORD, ADMIN_STORAGE);
    await loginAndSave(browser, OPERATOR_USER, OPERATOR_PASSWORD, OPERATOR_STORAGE);
  } finally {
    await browser.close();
  }
}

async function loginAndSave(
  browser: import('@playwright/test').Browser,
  username: string,
  password: string,
  storagePath: string,
) {
  mkdirSync(dirname(storagePath), { recursive: true });

  // Contexto limpio por identidad: cada uno arma su propia cookie SSO de Keycloak.
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();

  await page.goto(OPERATOR_URL, { waitUntil: 'domcontentloaded' });

  // Redirección automática al login del realm.
  await page.waitForURL(
    (url) => url.href.includes('/realms/umbral/protocol/openid-connect/auth'),
    { timeout: 30_000 },
  );

  await page.fill('#username', username);
  await page.fill('#password', password);
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

  await context.storageState({ path: storagePath });
  await context.close();
}
