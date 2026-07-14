import { test, expect } from '@playwright/test';
import { OPERATOR_URL, ADMIN_STORAGE } from '../env';

/**
 * Fase 3 — creación de misión por la UI de administración.
 *
 * El "test estrella" siembra la misión por API; este test cubre el camino que
 * quedó pendiente: el administrador crea la misión, le agrega una etapa Trivia
 * y la activa, todo por la UI real (pestaña Misiones, solo visible para el rol
 * admin). Usa la identidad de administrador (ADMIN_STORAGE).
 */

test('creación por UI admin: crear misión + etapa Trivia + activar', async ({ browser }) => {
  test.setTimeout(120_000);
  const id = Date.now().toString().slice(-6);
  const missionName = `E2E MisionUI ${id}`;
  const stageTitle = `Etapa ${id}`;

  const adminCtx = await browser.newContext({ storageState: ADMIN_STORAGE });
  const admin = await adminCtx.newPage();

  try {
    await admin.goto(OPERATOR_URL);
    // El admin aterriza en la pestaña Misiones (única de su rol junto a las de gestión).
    await expect(admin.getByRole('heading', { name: 'Misiones' })).toBeVisible({ timeout: 30_000 });

    // ── Crear la misión (la duración trae 30 por defecto) ────────────────────
    await admin.locator('#mission-name').fill(missionName);
    await admin.getByRole('button', { name: /Crear misión/ }).click();

    // La tarjeta de la misión recién creada (Inactiva) aparece en la lista.
    const missionRow = admin
      .locator('div')
      .filter({ has: admin.getByRole('heading', { name: missionName, exact: true }) })
      .filter({ has: admin.getByRole('button', { name: /Etapas/ }) })
      .last();
    await expect(missionRow).toBeVisible({ timeout: 15_000 });

    // ── Agregar una etapa Trivia (la misión Inactiva permite editar etapas) ──
    await missionRow.getByRole('button', { name: /Etapas/ }).click();
    const stageForm = admin.locator('form').filter({ hasText: 'Agregar etapa' });
    await expect(stageForm).toBeVisible();

    // Textboxes en orden: [Título, Pregunta, Opción 1, Opción 2].
    await stageForm.getByRole('textbox').first().fill(stageTitle);
    await stageForm.getByRole('textbox').nth(1).fill('¿Pregunta de prueba E2E?');
    await stageForm.getByPlaceholder('Opción 1').fill('Correcta');
    await stageForm.getByPlaceholder('Opción 2').fill('Incorrecta');
    await stageForm.getByRole('radio').first().check(); // marca la Opción 1 como correcta
    await stageForm.getByRole('button', { name: /Agregar etapa/ }).click();

    // La etapa aparece listada como "1. {título}".
    await expect(admin.getByText(`1. ${stageTitle}`)).toBeVisible({ timeout: 15_000 });

    // ── Activar la misión ────────────────────────────────────────────────────
    await missionRow.getByRole('button', { name: 'Activar' }).click();
    // Al activarse, el botón pasa a "Desactivar" (solo existe con la misión activa).
    await expect(missionRow.getByRole('button', { name: 'Desactivar' })).toBeVisible({ timeout: 15_000 });
    await expect(missionRow.getByText('Activa', { exact: true })).toBeVisible();
  } finally {
    await adminCtx.close();
  }
});
