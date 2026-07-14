import { test, expect } from '@playwright/test';
import { OPERATOR_URL, PARTICIPANT_URL, OPERATOR_STORAGE } from '../env';

/**
 * Fase 0 — Smoke E2E.
 * Valida que la tubería completa esté sana antes de escribir los flujos ricos:
 *   1. El operador queda autenticado (Keycloak + gateway + front operador).
 *   2. El front participante carga su pantalla de ingreso.
 * Si estos dos pasan, la infra E2E (stack levantado + auth + navegación) funciona.
 */
test.describe('Fase 0 — smoke', () => {
  test('el operador queda autenticado y entra a su tablero', async ({ browser }) => {
    const context = await browser.newContext({ storageState: OPERATOR_STORAGE });
    const page = await context.newPage();

    await page.goto(OPERATOR_URL);

    // Con la sesión de Keycloak ya guardada, entra sin ver el login: el splash
    // "Verificando sesión…" desaparece y NO rebota al formulario de Keycloak.
    await expect(page.getByText('Verificando sesión')).toBeHidden();
    await expect(page.locator('#kc-login')).toHaveCount(0);
    expect(new URL(page.url()).origin).toBe(new URL(OPERATOR_URL).origin);

    await context.close();
  });

  test('el participante ve la pantalla para unirse a una sesión', async ({ page }) => {
    await page.goto(PARTICIPANT_URL);

    await expect(page.getByText(/Ingresá el código de sesión/i)).toBeVisible();
    await expect(page.getByPlaceholder('Ej: ABC123')).toBeVisible();
    await expect(page.getByRole('button', { name: /Entrar a la sesión/i })).toBeVisible();
  });
});
